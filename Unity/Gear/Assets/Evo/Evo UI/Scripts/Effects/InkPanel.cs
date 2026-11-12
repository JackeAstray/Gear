using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// A continuously moving ink silhouette with an independently controlled radial reveal.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Evo/UI/Effects/Ink Panel"), HelpURL(Constants.HelpUrl)]
    public class InkPanel : MaskableGraphic
    {
        // Shape
        [Tooltip("Radial reveal progress. Zero hides the ink; one shows the complete silhouette.")]
        [SerializeField, Range(0f, 1f)] private float reveal = 1f;
        [Tooltip("Normalized contact point. (0.5, 0.5) starts the ink in the center.")]
        [SerializeField] private Vector2 origin = new(0.5f, 0.5f);
        [Tooltip("Zero gives square corners; one gives round ends. The RectTransform includes splatter margins.")]
        [SerializeField, Range(0f, 1f)] private float roundness = 1f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.7f;
        [SerializeField, Range(0f, 1f)] private float droplets = 0.65f;
        [Tooltip("Size and spacing of detached droplets, independent of the ink edge detail.")]
        [SerializeField, Range(0.25f, 3f)] private float dropletScale = 1f;
        [Tooltip("Scale of the edge grain and droplets in canvas units; independent of panel aspect ratio.")]
        [SerializeField, Range(0.5f, 2f)] private float detailScale = 1f;

        // Animation
        [Tooltip("Unscaled animation speed. Keeps moving when Time.timeScale is zero.")]
        [SerializeField, Range(0f, 2f)] private float speed = 0.45f;
        [SerializeField, Range(0f, 1f)] private float motion = 0.65f;
        [SerializeField, Range(0f, 100f)] private float seed = 7f;

        // Noise
        [Tooltip("Optional linear, tileable RGBA noise. Empty generates one shared texture on the GPU when needed.")]
        [SerializeField] private Texture2D noiseTexture;

        // Shader References
        [SerializeField, HideInInspector] Shader embeddedShader;
        [SerializeField, HideInInspector] Shader embeddedNoiseShader;
        const string ShaderName = "Evo/UI/Ink Panel";
        const string NoiseShaderName = "Hidden/Evo/UI/Ink Noise";
        static readonly int InkID = Shader.PropertyToID("_Ink");
        static readonly int MotionID = Shader.PropertyToID("_Motion");
        static readonly int GeometryID = Shader.PropertyToID("_Geometry");
        static readonly int OriginID = Shader.PropertyToID("_Origin");
        static readonly int NoiseID = Shader.PropertyToID("_InkNoise");
        static readonly int BakeRectID = Shader.PropertyToID("_BakeRect");

        // SoftMask consumes Graphic.mainTexture. Coverage is baked only for source panels with children.
        const int MaxMaskSize = 2048;
        const int MaskSizeStep = 32;
        float bakedTime;
        uint bakedNoiseVersion;
        bool usesGeneratedNoise, coverageValid, clockRegistered, warnedNoise;
        readonly Vector3[] maskCorners = new Vector3[4];
        Vector4 bakedInk, bakedMotion, bakedGeometry, bakedOrigin, bakedRect;
        Material instancedMaterial, coverageMaterial;
        RenderTexture coverageTexture;
        Texture bakedNoise;

        [SerializeField, HideInInspector] bool initialized;
#if UNITY_EDITOR
        [System.NonSerialized] float previewReveal = -1f;
#endif

        protected InkPanel()
        {
#if !UNITY_6000_6_OR_NEWER
            // Before Unity 6.6, Graphic defaults to the allocating legacy Mesh path
            useLegacyMeshGeneration = false;
#endif
        }

        protected override void OnEnable()
        {
            LoadShaders();

            if (instancedMaterial == null && embeddedShader != null)
                instancedMaterial = new Material(embeddedShader) { name = "Ink Panel (Instance)", hideFlags = HideFlags.HideAndDontSave };
            if (instancedMaterial == null)
                Debug.LogWarning("[Evo UI] Ink Panel shader is missing. Include an Ink Panel prefab in the build.", this);

            if (!initialized)
            {
                color = Color.black;
                raycastTarget = false;
                initialized = true;
            }

            base.OnEnable();

            if (!clockRegistered)
            {
                InkClock.Register();
                clockRegistered = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleaseResources();
#if UNITY_EDITOR
            previewReveal = -1f;
#endif
        }

        protected override void OnDestroy()
        {
            ReleaseResources();
            base.OnDestroy();
        }

        void OnTransformChildrenChanged()
        {
            if (transform.childCount == 0)
                ReleaseCoverage();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetMaterialDirty();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetMaterialDirty();
        }

        void ReleaseResources()
        {
            if (clockRegistered)
            {
                InkClock.Unregister();
                clockRegistered = false;
            }

            ReleaseCoverage();
            ReleaseNoise();
            DestroyTransient(instancedMaterial);
            instancedMaterial = null;
            warnedNoise = false;
        }

        #region Rendering
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Color32 tint = color;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, new Vector2(-1f, -1f));
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, new Vector2(-1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, new Vector2(1f, -1f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            // StencilMaterial caches copies. Update that copy before SoftMask sees its source CRC.
            Material modified = base.GetModifiedMaterial(baseMaterial);
            if (baseMaterial == instancedMaterial)
                ApplyMaterialProperties(modified);

            return modified;
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            Material source = material;
            if (source == instancedMaterial) { ApplyMaterialProperties(instancedMaterial); }
            else { ReleaseNoise(); }

            base.UpdateMaterial();

            if (source != instancedMaterial)
                return;

            Material renderedMaterial = canvasRenderer.GetMaterial(0);
            if (renderedMaterial != instancedMaterial)
                ApplyMaterialProperties(renderedMaterial);

            for (int i = 0; i < canvasRenderer.popMaterialCount; i++)
                ApplyMaterialProperties(canvasRenderer.GetPopMaterial(i));
        }

        void ApplyMaterialProperties(Material target)
        {
            if (target == null || instancedMaterial == null || target.shader != instancedMaterial.shader)
                return;

            GetParameters(out Vector4 ink, out Vector4 animation, out Vector4 geometry, out Vector4 contact);
            SetParameters(target, ink, animation, geometry, contact, ResolveNoise());
        }

        static void SetParameters(Material target, Vector4 ink, Vector4 animation, Vector4 geometry, Vector4 contact, Texture noise)
        {
            target.SetVector(InkID, ink);
            target.SetVector(MotionID, animation);
            target.SetVector(GeometryID, geometry);
            target.SetVector(OriginID, contact);
            target.SetTexture(NoiseID, noise);
        }

        void GetParameters(out Vector4 ink, out Vector4 animation, out Vector4 geometry, out Vector4 contact)
        {
            Vector2 size = GetPixelAdjustedRect().size;
            float progress = reveal;
#if UNITY_EDITOR
            if (!Application.isPlaying && previewReveal >= 0f)
                progress = previewReveal;
#endif
            ink = new Vector4(Mathf.Clamp01(progress), Mathf.Clamp01(roughness), Mathf.Clamp01(droplets), Mathf.Clamp01(Roundness));
            animation = new Vector4(Mathf.Clamp(speed, 0, 2), Mathf.Clamp01(motion), Mathf.Clamp(seed, 0, 100), Mathf.Clamp(detailScale, 0.5f, 2f));
            geometry = new Vector4(Mathf.Max(size.x, 1) / 600f, Mathf.Max(size.y, 1) / 600f, Mathf.Clamp(dropletScale, 0.25f, 3f), 0);
            contact = new Vector4(Mathf.Clamp01(origin.x) * 1.4f - 0.7f, Mathf.Clamp01(origin.y) * 1.4f - 0.7f, 0, 0);
        }

        void GetRenderParameters(out Vector4 ink, out Vector4 animation, out Vector4 geometry, out Vector4 contact, out Texture noise)
        {
            Material source = material;
            GetParameters(out ink, out animation, out geometry, out contact);
            if (source != null && source.shader == embeddedShader)
            {
                if (source != instancedMaterial)
                {
                    ink = source.GetVector(InkID);
                    animation = source.GetVector(MotionID);
                    geometry = source.GetVector(GeometryID);
                    contact = source.GetVector(OriginID);
                }
                noise = source == instancedMaterial ? ResolveNoise() : source.GetTexture(NoiseID);
            }
            else { noise = Texture2D.grayTexture; }
        }

        void LoadShaders()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
            if (embeddedNoiseShader == null)
                embeddedNoiseShader = Shader.Find(NoiseShaderName);
        }
        #endregion

        #region Noise
        Texture ResolveNoise()
        {
            if (noiseTexture != null)
            {
                ReleaseNoise();
                return noiseTexture;
            }

            GeneratedNoise.EnsureCreated();

            if (!usesGeneratedNoise)
            {
                usesGeneratedNoise = GeneratedNoise.Acquire(embeddedNoiseShader);
                if (!usesGeneratedNoise && !warnedNoise)
                {
                    warnedNoise = true;
                    Debug.LogWarning("[Evo UI] Ink Noise shader is missing. Include an Ink Panel prefab in the build or assign Noise Texture.", this);
                }
            }

            return GeneratedNoise.Texture != null ? GeneratedNoise.Texture : Texture2D.grayTexture;
        }

        void ReleaseNoise()
        {
            if (!usesGeneratedNoise)
                return;

            GeneratedNoise.Release();
            usesGeneratedNoise = false;
        }
        #endregion

        #region SoftMask Coverage
        Texture GetCoverageTexture()
        {
            if (embeddedNoiseShader == null || !embeddedNoiseShader.isSupported)
                return Texture2D.whiteTexture;

            // Arbitrary custom shaders own their own texture semantics.
            if (material == null || material.shader != embeddedShader)
            {
                ReleaseCoverage();
                return base.mainTexture;
            }

            InkClock.Tick();

            GetRenderParameters(out Vector4 ink, out Vector4 animation, out Vector4 geometry, out Vector4 contact, out Texture noise);
            Rect rawRect = rectTransform.rect;
            Rect drawingRect = GetPixelAdjustedRect();
            Vector4 bakeRect = new(rawRect.width / Mathf.Max(drawingRect.width, 0.001f),
                rawRect.height / Mathf.Max(drawingRect.height, 0.001f),
                (rawRect.center.x - drawingRect.center.x) * 2f / Mathf.Max(drawingRect.width, 0.001f),
                (rawRect.center.y - drawingRect.center.y) * 2f / Mathf.Max(drawingRect.height, 0.001f));
            GetCoverageSize(rawRect.size, out int width, out int height);
            if (coverageTexture == null || coverageTexture.width != width || coverageTexture.height != height)
            {
                DestroyTransient(coverageTexture);
                coverageTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "Ink Panel (SoftMask Coverage)",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                coverageValid = false;
            }

            if (!coverageTexture.IsCreated())
            {
                coverageValid = false;
                if (!coverageTexture.Create())
                    return Texture2D.whiteTexture;
            }

            float time = animation.x == 0f || animation.y == 0f ? 0f : InkClock.CurrentTime;
            uint noiseVersion = noise != null ? noise.updateCount : 0;
            if (coverageValid && bakedTime == time && bakedInk == ink && bakedMotion == animation && bakedGeometry == geometry
                && bakedOrigin == contact && bakedNoise == noise && bakedNoiseVersion == noiseVersion && bakedRect == bakeRect)
                return coverageTexture;

            if (coverageMaterial == null)
                coverageMaterial = new Material(embeddedNoiseShader) { name = "Ink Panel (Coverage)", hideFlags = HideFlags.HideAndDontSave };

            SetParameters(coverageMaterial, ink, animation, geometry, contact, noise);
            coverageMaterial.SetVector(BakeRectID, bakeRect);
            Blit(coverageTexture, coverageMaterial, 1);

            bakedTime = time;
            bakedInk = ink;
            bakedMotion = animation;
            bakedGeometry = geometry;
            bakedOrigin = contact;
            bakedNoise = noise;
            bakedNoiseVersion = noiseVersion;
            bakedRect = bakeRect;
            coverageValid = true;
            return coverageTexture;
        }

        void GetCoverageSize(Vector2 pixels, out int width, out int height)
        {
            Canvas targetCanvas = canvas;
            if (targetCanvas != null)
            {
                Camera camera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
                if (camera != null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    rectTransform.GetWorldCorners(maskCorners);
                    Vector2 p0 = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[0]);
                    Vector2 p1 = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[1]);
                    Vector2 p2 = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[2]);
                    Vector2 p3 = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[3]);
                    pixels = new Vector2(Mathf.Max(Vector2.Distance(p0, p3), Vector2.Distance(p1, p2)),
                        Mathf.Max(Vector2.Distance(p0, p1), Vector2.Distance(p3, p2)));
                }
                else
                    pixels *= targetCanvas.scaleFactor;
            }

            // Follow projected size, bound memory use, and avoid reallocating at every pixel of a resize.
            float reduction = Mathf.Min(1f, MaxMaskSize / Mathf.Max(1f, Mathf.Max(pixels.x, pixels.y)));
            width = Mathf.Clamp(Mathf.CeilToInt(pixels.x * reduction / MaskSizeStep) * MaskSizeStep, MaskSizeStep, MaxMaskSize);
            height = Mathf.Clamp(Mathf.CeilToInt(pixels.y * reduction / MaskSizeStep) * MaskSizeStep, MaskSizeStep, MaxMaskSize);
        }

        void ReleaseCoverage()
        {
            DestroyTransient(coverageTexture);
            coverageTexture = null;
            DestroyTransient(coverageMaterial);
            coverageMaterial = null;
            bakedNoise = null;
            coverageValid = false;
        }
        #endregion

        #region Utilities
        static void Blit(RenderTexture destination, Material source, int pass)
        {
            // Both bakes store linear data and can run inside Canvas/camera callbacks
            RenderTexture previous = RenderTexture.active;
            bool previousSRGBWrite = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(null, destination, source, pass);
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSRGBWrite;
            }
        }

        static void DestroyTransient(Object value)
        {
            if (value == null)
                return;
            if (value is RenderTexture texture)
                texture.Release();

            if (Application.isPlaying) { Destroy(value); }
            else { DestroyImmediate(value); }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void Set(ref float field, float value)
        {
            if (!Finite(value) || field == value)
                return;

            field = value;
            SetMaterialDirty();
        }
        #endregion

        #region Public API
        public float Reveal
        {
            get => reveal;
            set => Set(ref reveal, Mathf.Clamp01(value));
        }

        public Vector2 Origin
        {
            get => origin;
            set
            {
                if (!Finite(value.x) || !Finite(value.y))
                    return;

                value = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                if (origin == value)
                    return;

                origin = value;
                SetMaterialDirty();
            }
        }

        public float Roundness
        {
            get => roundness;
            set => Set(ref roundness, Mathf.Clamp01(value));
        }

        public float Roughness
        {
            get => roughness;
            set => Set(ref roughness, Mathf.Clamp01(value));
        }

        public float Droplets
        {
            get => droplets;
            set => Set(ref droplets, Mathf.Clamp01(value));
        }

        public float DropletScale
        {
            get => dropletScale;
            set => Set(ref dropletScale, Mathf.Clamp(value, 0.25f, 3f));
        }

        public float DetailScale
        {
            get => detailScale;
            set => Set(ref detailScale, Mathf.Clamp(value, 0.5f, 2f));
        }

        public float Speed
        {
            get => speed;
            set => Set(ref speed, Mathf.Clamp(value, 0f, 2f));
        }

        public float Motion
        {
            get => motion;
            set => Set(ref motion, Mathf.Clamp01(value));
        }

        public float Seed
        {
            get => seed;
            set => Set(ref seed, Mathf.Clamp(value, 0f, 100f));
        }

        public Texture2D NoiseTexture
        {
            get => noiseTexture;
            set
            {
                if (noiseTexture == value)
                    return;

                noiseTexture = value;
                SetMaterialDirty();
            }
        }

        public void SetReveal(float value) => Reveal = value;

        public override Material defaultMaterial => instancedMaterial != null ? instancedMaterial : base.defaultMaterial;

        // SoftMask already supports alpha textures from arbitrary Graphics. Only its source panel
        // supplies a cached GPU texture; ordinary ink backgrounds keep the one-pass analytic path.
        public override Texture mainTexture
        {
            get
            {
                if (isActiveAndEnabled && transform.childCount > 0 && TryGetComponent(out SoftMask sourceMask) && sourceMask.isActiveAndEnabled)
                    return GetCoverageTexture();

                ReleaseCoverage();
                return Texture2D.whiteTexture;
            }
        }
        #endregion

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            LoadShaders();
            roundness = 1f;
            color = Color.black;
            raycastTarget = false;
            initialized = true;
            rectTransform.sizeDelta = new Vector2(620, 620);
        }

        protected override void OnValidate()
        {
            LoadShaders();
            base.OnValidate();
            coverageValid = false;
        }

        /// <summary>
        /// Overrides the rendered reveal without changing saved data. Negative clears it.
        /// </summary>
        public void SetEditorRevealPreview(float value)
        {
            if (!Finite(value))
                return;

            previewReveal = value < 0f ? -1f : Mathf.Clamp01(value);
            SetMaterialDirty();
        }
#endif

        [NoAutoStaticsCleanup]
        static class GeneratedNoise
        {
            public static RenderTexture Texture { get; private set; }
            static Shader generatorShader;
            static int users;

            public static bool Acquire(Shader shader)
            {
                if (shader == null || !shader.isSupported)
                    return false;

                generatorShader = shader;
                if (Texture == null)
                {
                    Texture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    {
                        name = "Ink Panel (Shared Noise)",
                        hideFlags = HideFlags.HideAndDontSave,
                        useMipMap = true,
                        autoGenerateMips = false,
                        filterMode = FilterMode.Trilinear,
                        wrapMode = TextureWrapMode.Repeat
                    };
                }

                EnsureCreated();
                if (!Texture.IsCreated())
                {
                    if (users == 0)
                    {
                        DestroyTransient(Texture);
                        Texture = null;
                        generatorShader = null;
                    }
                    return false;
                }

                users++;
                return true;
            }

            public static void EnsureCreated()
            {
                if (Texture == null || Texture.IsCreated() || generatorShader == null || !Texture.Create())
                    return;

                // Recreate in place after RT loss: stencil and SoftMask materials keep their reference
                Material generator = new(generatorShader) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Blit(Texture, generator, 0);
                    Texture.GenerateMips();
                    Texture.IncrementUpdateCount();
                }
                finally
                {
                    DestroyTransient(generator);
                }
            }

            public static void Release()
            {
                users = Mathf.Max(0, users - 1);
                if (users != 0)
                    return;

                DestroyTransient(Texture);
                Texture = null;
                generatorShader = null;
            }
        }

        /// <summary>
        /// One global float update per frame for all panels. Idle motion never dirties their meshes/materials.
        /// </summary>
        [NoAutoStaticsCleanup]
        static class InkClock
        {
            public static float CurrentTime { get; private set; }
            static readonly int TimeID = Shader.PropertyToID("_EvoInkTime");
            static int users;
            static int frame = -1;

            public static void Register()
            {
                if (users++ != 0)
                    return;

                Canvas.preWillRenderCanvases += Tick;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.update += EditorTick;
                EditorTick();
#endif
                Tick();
            }

            public static void Unregister()
            {
                users = Mathf.Max(0, users - 1);
                if (users != 0)
                    return;

                Canvas.preWillRenderCanvases -= Tick;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.update -= EditorTick;
#endif
                frame = -1;
            }

            public static void Tick()
            {
                GeneratedNoise.EnsureCreated();
                if (!Application.isPlaying || frame == Time.frameCount)
                    return;

                frame = Time.frameCount;
                CurrentTime = Time.unscaledTime;
                Shader.SetGlobalFloat(TimeID, CurrentTime);
            }
#if UNITY_EDITOR
            static void EditorTick()
            {
                if (!Application.isPlaying)
                {
                    GeneratedNoise.EnsureCreated();
                    CurrentTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
                    Shader.SetGlobalFloat(TimeID, CurrentTime);
                }
            }
#endif
        }
    }
}