using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Renders catalog prefabs in a preview scene and caches the thumbnails under Library.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class CatalogPreviews
    {
        public const int Width = 480;
        public const int Height = 300;

        // Bump to regenerate every cached thumbnail after changing how they're rendered
        const int RenderVersion = 2;
        const int BatchSize = 8;
        const int SettleUpdates = 3; // Lets Edit Mode delayCall refreshes, such as chart drawing, run first
        const double LoadBudget = 0.008;
        const float FramePadding = 1.25f;
        const float MinFrameHeight = 110f; // Stops tiny elements from filling the whole thumbnail
        const float CanvasSpacing = 10000f;
        const string ContentColorID = "Primary";
        static readonly Vector2 CanvasSize = new(2560, 1440);

        // Fixed stages, so thumbnails don't follow the preset's background colors
        static readonly Color DarkBackground = new(0.071f, 0.086f, 0.102f);
        static readonly Color LightBackground = new(0.953f, 0.953f, 0.957f);
        static readonly string CacheFolder = Path.Combine("Library", "Evo UI", "Catalog Previews");

        static readonly Dictionary<CatalogItem, Texture2D> textures = new();
        static readonly Dictionary<CatalogItem, string> keys = new();
        static readonly Dictionary<CatalogItem, string> failed = new(); // Value: the key that failed, retried once it changes
        static readonly List<CatalogItem> queue = new();
        static readonly List<Pending> batch = new();

        static Scene scene;
        static Camera camera;
        static RenderTexture renderTexture;
        static Texture2D readback;
        static Color? background;
        static string presetHash;
        static int settleCount;
        static int users;
        static bool processing;
        static bool waitingForFocus;

        struct Pending
        {
            public CatalogItem Item;
            public string Key;
            public GameObject Canvas;
            public RectTransform Instance;
        }

        /// <summary>
        /// Raised when an item's thumbnail is ready or has failed.
        /// </summary>
        public static event Action<CatalogItem> Changed;

        public static int PendingCount => queue.Count + batch.Count;

        /// <summary>
        /// Templates ship with TextMesh Pro, whose texts throw on every canvas update until its Essential Resources are imported.
        /// </summary>
        public static bool HasTextResources => TMPro.TMP_Settings.LoadDefaultSettings() != null;

        /// <summary>
        /// The background thumbnails are rendered on: a fixed dark or light stage that contrasts with the default
        /// preset's Primary color, or one matching the editor skin without a preset.
        /// </summary>
        public static Color Background
        {
            get
            {
                background ??= GetStageBackground();
                return background.Value;
            }
        }

        static Color GetStageBackground()
        {
            // Light content needs a dark stage, and the other way around
            var preset = Styler.GetDefaultPreset(false);
            if (preset != null && preset.TryGetColor(ContentColorID, out Color content))
                return content.grayscale > 0.5f ? DarkBackground : LightBackground;

            return EditorGUIUtility.isProSkin ? DarkBackground : LightBackground;
        }

        static CatalogPreviews()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        public static void AddUser() => users++;

        public static void RemoveUser()
        {
            users = Mathf.Max(0, users - 1);
            if (users == 0)
                ReleaseAll();
        }

        public static Texture2D Get(CatalogItem item)
        {
            return textures.TryGetValue(item, out Texture2D texture) && texture != null ? texture : null;
        }

        public static bool HasFailed(CatalogItem item) => failed.ContainsKey(item);

        /// <summary>
        /// Queues a thumbnail unless it's already loaded. Prioritized requests jump the queue.
        /// </summary>
        public static void Request(CatalogItem item, bool prioritize = false)
        {
            if (item != null && !failed.ContainsKey(item) && Get(item) == null)
                Enqueue(item, prioritize);
        }

        public static void Refresh(CatalogItem item)
        {
            DeleteCachedFiles(item, null);
            keys.Remove(item);
            failed.Remove(item);
            Enqueue(item, true);
        }

        public static void RefreshAll()
        {
            try
            {
                if (Directory.Exists(CacheFolder))
                    Directory.Delete(CacheFolder, true);
            }
            catch (IOException exception) { Debug.LogWarning($"[Evo UI] Could not clear catalog previews: {exception.Message}"); }

            keys.Clear();
            failed.Clear();
            background = null;
            presetHash = null;

            foreach (CatalogItem item in Catalog.Items)
                Enqueue(item, false);
        }

        // Already-current thumbnails are skipped when processed, so forcing is cheap
        static void Enqueue(CatalogItem item, bool prioritize)
        {
            if (users == 0 || IsRendering(item))
                return;

            int index = queue.IndexOf(item);
            if (index >= 0)
            {
                if (!prioritize)
                    return;

                queue.RemoveAt(index);
            }

            if (prioritize) { queue.Insert(0, item); }
            else { queue.Add(item); }
            waitingForFocus = false;
            StartProcessing();
        }

        static bool IsRendering(CatalogItem item)
        {
            foreach (Pending pending in batch)
            {
                if (pending.Item == item)
                    return true;
            }

            return false;
        }

        static void StartProcessing()
        {
            if (processing)
                return;

            processing = true;
            EditorApplication.update += Process;
        }

        static void StopProcessing()
        {
            if (!processing)
                return;

            processing = false;
            EditorApplication.update -= Process;
        }

        static void Process()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            // delayCall doesn't run while the Editor is in the background, so rendering then would cache empty charts.
            // Rendering also waits for TextMesh Pro resources. Cached thumbnails still load.
            bool canRender = InternalEditorUtility.isApplicationActive && HasTextResources;
            if (canRender) { waitingForFocus = false; }
            else if (waitingForFocus) { return; }

            if (batch.Count > 0)
            {
                if (canRender && ++settleCount >= SettleUpdates)
                    RenderBatch();

                return;
            }

            var stopwatch = Stopwatch.StartNew();
            int index = 0;
            while (index < queue.Count && batch.Count < BatchSize && stopwatch.Elapsed.TotalSeconds < LoadBudget)
            {
                CatalogItem item = queue[index];
                string key = GetKey(item);
                if (key == null)
                {
                    queue.RemoveAt(index);
                    MarkFailed(item, null, null);
                    continue;
                }

                bool current = keys.TryGetValue(item, out string currentKey) && currentKey == key && Get(item) != null;
                if (current || TryLoad(item, key))
                {
                    queue.RemoveAt(index);
                    continue;
                }

                if (!canRender)
                {
                    index++;
                    continue;
                }

                queue.RemoveAt(index);
                BeginRender(item, key);
            }

            // Everything left needs rendering
            if (!canRender && index >= queue.Count)
                waitingForFocus = true;

            if (queue.Count == 0 && batch.Count == 0)
            {
                StopProcessing();
                CloseScene();
            }
        }

        static string GetKey(CatalogItem item)
        {
            string path = item.AssetPath;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || item.Prefab == null)
                return null;

            if (presetHash == null)
            {
                var preset = Styler.GetDefaultPreset(false);
                presetHash = preset != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(preset)).ToString() : "";
            }

            var hash = new Hash128();
            hash.Append(RenderVersion);
            hash.Append(Width);
            hash.Append(Height);
            hash.Append(AssetDatabase.GetAssetDependencyHash(path).ToString());
            hash.Append(presetHash);
            hash.Append(ColorUtility.ToHtmlStringRGB(Background));
            hash.Append(QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
            return guid + "_" + hash;
        }

        static string GetFilePath(string key) => Path.Combine(CacheFolder, key + ".png");

        static bool TryLoad(CatalogItem item, string key)
        {
            string filePath = GetFilePath(key);
            if (!File.Exists(filePath))
                return false;

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(filePath)) || decoded.width != Width || decoded.height != Height)
                    return false;

                SetTexture(item, key, decoded.GetPixels32());
                return true;
            }
            catch (IOException) { return false; }
            finally { Object.DestroyImmediate(decoded); }
        }

        static void BeginRender(CatalogItem item, string key)
        {
            EnsureScene();

            // Each element gets its own canvas, far enough apart to be framed separately
            var canvasObject = EditorUtility.CreateGameObjectWithHideFlags("Catalog Preview Canvas", HideFlags.HideAndDontSave, typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.position = new Vector3(batch.Count * CanvasSpacing, 0, 0);
            canvasRect.sizeDelta = CanvasSize;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            // Instantiating under the canvas keeps the instance out of the open scenes entirely
            var instance = Object.Instantiate(item.Prefab, canvasRect, false);
            item.PrepareInstance(instance);

            batch.Add(new Pending { Item = item, Key = key, Canvas = canvasObject, Instance = instance.transform as RectTransform });
            settleCount = 0;
        }

        static void RenderBatch()
        {
            // A canvas update that throws still releases the batch, so it isn't retried every update
            try
            {
                Canvas.ForceUpdateCanvases();

                foreach (Pending pending in batch)
                {
                    try
                    {
                        if (pending.Instance == null)
                        {
                            MarkFailed(pending.Item, pending.Key, "The preview instance was destroyed while rendering.");
                            continue;
                        }

                        Color32[] pixels = Render(pending.Instance);
                        Save(pending.Item, pending.Key, pixels);
                        SetTexture(pending.Item, pending.Key, pixels);
                    }
                    catch (Exception exception) { MarkFailed(pending.Item, pending.Key, exception.Message); }
                }
            }
            finally
            {
                foreach (Pending pending in batch)
                {
                    if (pending.Canvas != null)
                        Object.DestroyImmediate(pending.Canvas);
                }

                batch.Clear();
            }
        }

        static Color32[] Render(RectTransform target)
        {
            Bounds bounds = GetBounds(target);
            float aspect = (float)Width / Height;
            float frameHeight = Mathf.Max(bounds.size.y, bounds.size.x / aspect, MinFrameHeight) * FramePadding;

            camera.orthographicSize = frameHeight * 0.5f;
            camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 500), Quaternion.identity);
            camera.backgroundColor = Background;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            RenderTexture.active = previous;
            return readback.GetPixels32();
        }

        static Bounds GetBounds(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
                bounds.Encapsulate(corners[i]);

            if (bounds.size.x >= 1 && bounds.size.y >= 1)
                return bounds;

            // Content-sized roots: frame the visible children instead
            foreach (var child in target.GetComponentsInChildren<RectTransform>())
            {
                child.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                    bounds.Encapsulate(corners[i]);
            }

            return bounds;
        }

        static void SetTexture(CatalogItem item, string key, Color32[] pixels)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true, false)
            {
                name = item.Name + " (Catalog Preview)",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // Mipmaps keep text readable on small cards
            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            if (textures.TryGetValue(item, out Texture2D previous) && previous != null)
                Object.DestroyImmediate(previous);

            textures[item] = texture;
            keys[item] = key;
            failed.Remove(item);
            Changed?.Invoke(item);
        }

        static void Save(CatalogItem item, string key, Color32[] pixels)
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);
                byte[] png = ImageConversion.EncodeArrayToPNG(pixels, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, Width, Height);
                File.WriteAllBytes(GetFilePath(key), png);
                DeleteCachedFiles(item, key);
            }
            catch (IOException exception) { Debug.LogWarning($"[Evo UI] Could not cache the preview of '{item.Name}': {exception.Message}"); }
        }

        static void DeleteCachedFiles(CatalogItem item, string keepKey)
        {
            string guid = AssetDatabase.AssetPathToGUID(item.AssetPath);
            if (string.IsNullOrEmpty(guid) || !Directory.Exists(CacheFolder))
                return;

            string keepPath = keepKey != null ? GetFilePath(keepKey) : null;
            foreach (string file in Directory.GetFiles(CacheFolder, guid + "_*.png"))
            {
                if (file != keepPath)
                    File.Delete(file);
            }
        }

        static void MarkFailed(CatalogItem item, string key, string reason)
        {
            failed[item] = key;
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[Evo UI] Could not render the catalog preview of '{item.Name}': {reason}");

            Changed?.Invoke(item);
        }

        static void OnProjectChanged()
        {
            if (users == 0)
                return;

            // Re-render thumbnails whose prefab or default preset changed; old ones stay up until then
            background = null;
            presetHash = null;
            foreach (CatalogItem item in Catalog.Items)
            {
                if (keys.TryGetValue(item, out string key))
                {
                    if (GetKey(item) != key)
                        Enqueue(item, false);
                }
                else if (failed.TryGetValue(item, out string failedKey) && GetKey(item) != failedKey)
                {
                    failed.Remove(item);
                    Enqueue(item, false);
                }
            }
        }

        static void EnsureScene()
        {
            if (camera != null)
                return;

            scene = EditorSceneManager.NewPreviewScene();

            var cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Catalog Preview Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 1;
            camera.farClipPlane = 1000;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                antiAliasing = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            camera.targetTexture = renderTexture;

            readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
        }

        static void CloseScene()
        {
            foreach (Pending pending in batch)
            {
                if (pending.Canvas != null)
                    Object.DestroyImmediate(pending.Canvas);
            }

            batch.Clear();

            if (camera != null)
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
            }

            if (scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            if (readback != null)
                Object.DestroyImmediate(readback);

            camera = null;
            renderTexture = null;
            readback = null;
            scene = default;
        }

        static void ReleaseAll()
        {
            StopProcessing();
            CloseScene();
            queue.Clear();

            foreach (Texture2D texture in textures.Values)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }

            textures.Clear();
            keys.Clear();
            failed.Clear();
            background = null;
            presetHash = null;
        }
    }
}
