using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;
using Object = UnityEngine.Object;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;
    using Image = UnityEngine.UIElements.Image;
    using Slider = UnityEngine.UIElements.Slider;
    using Toggle = UnityEngine.UIElements.Toggle;

    /// <summary>
    /// Bulk-edits texture filtering and mipmap settings for selected texture assets.
    /// </summary>
    public class SpriteOptimizerWindow : EditorWindow
    {
        const float DefaultMipmapBias = -1f;
        const int DisplayLimit = 50;

        [SerializeField] bool generateMipmap = true;
        [SerializeField] float mipmapBias = DefaultMipmapBias;
        [SerializeField] TextureImporterMipFilter mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        [SerializeField] FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField] int anisoLevel = 1;

        Texture2D[] selectedTextures = Array.Empty<Texture2D>();
        string applySummary;
        float applyProgress;
        bool applyFailed;

        VisualElement mipmapSettings;
        VisualElement textureList;
        Label emptyMessage;
        EvoEditorUITK.StatusView status;
        VisualElement footer;
        Button applyButton;

        void OnEnable()
        {
            titleContent = new GUIContent("Sprite Optimizer", Resources.Load<Texture2D>("GUI/UI_SpriteOptimizer"));
            minSize = new Vector2(320, 420);
            Selection.selectionChanged += RefreshTextures;
            EditorApplication.projectChanged += RefreshTextures;
            RefreshTextures();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= RefreshTextures;
            EditorApplication.projectChanged -= RefreshTextures;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("sprite-optimizer");
            EvoEditorUITK.Apply(this, nameof(SpriteOptimizerWindow));

            root.Add(EvoEditorUITK.CreateHeader("Optimize Textures",
                "Sets filtering and mipmaps on the selected textures, so sprites stay smooth when scaled down. Mipmaps use extra texture memory."));

            ScrollView body = EvoEditorUITK.CreateBody();
            root.Add(body);

            body.Add(EvoEditorUITK.CreateHeading("Import Settings", "evo-heading--first"));

            var mipmapsField = new Toggle("Generate Mipmaps")
            {
                value = generateMipmap,
                tooltip = "Generate lower-resolution texture levels for minification. Increases texture memory usage."
            };
            mipmapsField.AddToClassList("evo-field");
            mipmapsField.RegisterValueChangedCallback(evt =>
            {
                generateMipmap = evt.newValue;
                UpdateState();
            });
            body.Add(mipmapsField);

            mipmapSettings = new VisualElement();
            body.Add(mipmapSettings);

            var biasRow = new VisualElement();
            biasRow.AddToClassList("optimizer-bias-row");
            mipmapSettings.Add(biasRow);

            var biasField = new Slider("Mipmap Bias", -3f, 3f)
            {
                value = mipmapBias,
                showInputField = true,
                tooltip = "Negative values sharpen mipmaps but can introduce aliasing. Positive values soften them."
            };
            biasField.AddToClassList("evo-field");
            biasField.RegisterValueChangedCallback(evt => mipmapBias = evt.newValue);
            biasRow.Add(biasField);

            var resetBias = new Button(() => biasField.value = DefaultMipmapBias) { text = "Reset", tooltip = $"Reset to {DefaultMipmapBias}" };
            resetBias.AddToClassList("optimizer-reset");
            biasRow.Add(resetBias);

            var mipmapFilterField = new EnumField("Mipmap Filtering", mipmapFilter)
            {
                tooltip = "Box averages pixels. Kaiser produces sharper mip levels but takes longer to import."
            };
            mipmapFilterField.AddToClassList("evo-field");
            mipmapFilterField.RegisterValueChangedCallback(evt => mipmapFilter = (TextureImporterMipFilter)evt.newValue);
            mipmapSettings.Add(mipmapFilterField);

            var filterModeField = new EnumField("Filter Mode", filterMode)
            {
                tooltip = "Point suits pixel art. Bilinear blends pixels. Trilinear also blends mip levels."
            };
            filterModeField.AddToClassList("evo-field");
            filterModeField.RegisterValueChangedCallback(evt => filterMode = (FilterMode)evt.newValue);
            body.Add(filterModeField);

            var anisoField = new SliderInt("Aniso Level", 0, 16)
            {
                value = anisoLevel,
                showInputField = true,
                tooltip = "Improves filtering at steep viewing angles. Usually unnecessary for flat UI (use 0 or 1)."
            };
            anisoField.AddToClassList("evo-field");
            anisoField.RegisterValueChangedCallback(evt => anisoLevel = evt.newValue);
            body.Add(anisoField);

            body.Add(EvoEditorUITK.CreateHeading("Textures"));

            textureList = new VisualElement();
            textureList.AddToClassList("evo-well");
            textureList.AddToClassList("optimizer-textures");
            body.Add(textureList);

            emptyMessage = EvoEditorUITK.CreateDescription("Select textures or sprites in the Project window, or add one below.", "optimizer-empty");
            body.Add(emptyMessage);

            var addField = new ObjectField("Add Texture") { objectType = typeof(Texture2D), allowSceneObjects = false, tooltip = "Add a texture to the selection" };
            addField.AddToClassList("evo-field");
            addField.RegisterValueChangedCallback(evt =>
            {
                addField.SetValueWithoutNotify(null);
                if (evt.newValue is Texture2D texture)
                    AddTexture(texture);
            });
            body.Add(addField);

            status = new EvoEditorUITK.StatusView(() =>
            {
                applySummary = null;
                UpdateState();
            });
            body.Add(status);

            footer = new VisualElement();
            footer.AddToClassList("evo-footer");
            applyButton = new Button(ApplySettingsToSelected);
            applyButton.AddToClassList("evo-primary");
            footer.Add(applyButton);
            root.Add(footer);

            RefreshTextures();
        }

        static List<string> GetTexturePaths(IEnumerable<Object> objects)
        {
            var paths = new List<string>();
            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (Object obj in objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && uniquePaths.Add(path))
                    paths.Add(path);
            }
            return paths;
        }

        static Texture2D GetTextureAtPath(string path)
        {
            return !string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is TextureImporter
                ? AssetDatabase.LoadMainAssetAtPath(path) as Texture2D
                : null;
        }

        void RefreshTextures()
        {
            var textures = new List<Texture2D>();
            foreach (string path in GetTexturePaths(Selection.objects))
            {
                Texture2D texture = GetTextureAtPath(path);
                if (texture != null)
                    textures.Add(texture);
            }

            selectedTextures = textures.ToArray();
            if (textureList == null)
                return;

            textureList.Clear();
            int count = Mathf.Min(selectedTextures.Length, DisplayLimit);
            for (int i = 0; i < count; i++)
                textureList.Add(CreateTextureRow(selectedTextures[i]));

            if (selectedTextures.Length > count)
            {
                textureList.Add(EvoEditorUITK.CreateDescription($"... and {selectedTextures.Length - count} more, also included when applying.", "optimizer-more"));
            }

            bool empty = selectedTextures.Length == 0;
            textureList.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            emptyMessage.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateState();
        }

        VisualElement CreateTextureRow(Texture2D texture)
        {
            var row = new VisualElement { tooltip = AssetDatabase.GetAssetPath(texture) + "\nClick to show in the Project window." };
            row.AddToClassList("optimizer-texture");
            row.RegisterCallback<ClickEvent>(_ => EditorGUIUtility.PingObject(texture));

            var icon = new Image { image = AssetPreview.GetMiniThumbnail(texture), pickingMode = PickingMode.Ignore };
            icon.AddToClassList("optimizer-texture__icon");
            row.Add(icon);

            var name = new Label(texture.name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("optimizer-texture__name");
            row.Add(name);

            var size = new Label($"{texture.width} x {texture.height}") { pickingMode = PickingMode.Ignore };
            size.AddToClassList("optimizer-texture__size");
            row.Add(size);

            var remove = new Button(() => RemoveTexture(texture)) { text = "\u00D7", tooltip = "Remove from the selection" };
            remove.AddToClassList("evo-icon-button");
            remove.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            row.Add(remove);

            return row;
        }

        void AddTexture(Texture2D texture)
        {
            texture = GetTextureAtPath(AssetDatabase.GetAssetPath(texture));
            if (texture == null)
            {
                ShowNotification(new GUIContent("Choose a texture asset with a Texture Importer."));
                return;
            }

            // The list mirrors the Project selection, so editing it edits the selection
            var textures = new List<Texture2D>(selectedTextures);
            if (!textures.Contains(texture))
                textures.Add(texture);

            Selection.objects = textures.ToArray();
        }

        void RemoveTexture(Texture2D texture)
        {
            var textures = new List<Texture2D>(selectedTextures);
            textures.Remove(texture);
            Selection.objects = textures.ToArray();
        }

        void UpdateState()
        {
            if (applyButton == null)
                return;

            mipmapSettings.SetEnabled(generateMipmap);

            // Without textures the empty message explains what to do, so there's nothing to apply
            int count = selectedTextures.Length;
            footer.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            applyButton.text = count == 1 ? "Apply to 1 Texture" : $"Apply to {count} Textures";

            status.SetState(applyProgress, applySummary, false, applyFailed);
        }

        bool SettingsMatch(TextureImporter importer)
        {
            return importer.mipmapEnabled == generateMipmap && importer.filterMode == filterMode && importer.anisoLevel == anisoLevel
                && (!generateMipmap || (Mathf.Approximately(importer.mipMapBias, mipmapBias) && importer.mipmapFilter == mipmapFilter));
        }

        static bool CanEditImporter(TextureImporter importer, out string reason)
        {
            string path = importer.assetPath;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                reason = "Only textures in the project's Assets folder can be edited.";
                return false;
            }

            if (!AssetDatabase.IsOpenForEdit(path + ".meta", out reason, StatusQueryOptions.ForceUpdate))
                return false;

            if ((File.GetAttributes(path + ".meta") & FileAttributes.ReadOnly) != 0)
            {
                reason = "The texture's .meta file is read-only.";
                return false;
            }

            return true;
        }

        void ApplySettingsToSelected()
        {
            if (selectedTextures.Length == 0)
                return;

            // Reimports and project callbacks may replace texture objects and the selection
            List<string> paths = GetTexturePaths(selectedTextures);
            int updated = 0, unchanged = 0, skipped = 0, failed = 0, processed = 0;
            bool cancelled = false;
            bool batchFailed = false;

            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string path in paths)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Updating Textures", path, (float)processed / paths.Count))
                        {
                            cancelled = true;
                            break;
                        }

                        try
                        {
                            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) { skipped++; }
                            else if (SettingsMatch(importer)) { unchanged++; }
                            else if (!CanEditImporter(importer, out string reason))
                            {
                                skipped++;
                                Debug.LogWarning($"Sprite Optimizer skipped '{path}': {reason}", importer);
                            }
                            else
                            {
                                importer.mipmapEnabled = generateMipmap;
                                if (generateMipmap)
                                {
                                    importer.mipMapBias = mipmapBias;
                                    importer.mipmapFilter = mipmapFilter;
                                }
                                importer.filterMode = filterMode;
                                importer.anisoLevel = anisoLevel;
                                EditorUtility.SetDirty(importer);
                                importer.SaveAndReimport();
                                updated++;
                            }
                        }
                        catch (Exception exception)
                        {
                            failed++;
                            Debug.LogError($"Sprite Optimizer failed for '{path}': {exception}");
                        }
                        processed++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
            catch (Exception exception)
            {
                batchFailed = true;
                Debug.LogException(exception);
            }
            finally { EditorUtility.ClearProgressBar(); }

            string result = batchFailed ? "Batch failed; verify imports in the Console." : cancelled ? "Cancelled; completed changes were kept." : "Finished.";
            applySummary = $"{result} Saved settings for {updated} textures; {unchanged} unchanged, {skipped} skipped, {failed} failed, {paths.Count - processed} not processed.";
            applyFailed = batchFailed || failed > 0;
            applyProgress = paths.Count > 0 ? (float)(updated + unchanged) / paths.Count : 0;
            if (!applyFailed && !cancelled && skipped == 0)
                Debug.Log($"Sprite Optimizer: {applySummary}");
            else
                Debug.LogWarning($"Sprite Optimizer: {applySummary}");

            UpdateState();
        }
    }
}
