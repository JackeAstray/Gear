using System;
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
    using Toggle = UnityEngine.UIElements.Toggle;
    using ShadowHandling = ShapeSpriteExporter.ShadowHandling;

    /// <summary>
    /// Exports a Procedural Shape as a sprite asset, optionally sliced or tintable, and can replace the shape with an Image.
    /// </summary>
    public class SpriteExportWindow : EditorWindow
    {
        const string PrefPrefix = "Evo.UI.SpriteExport.";
        const float PreviewHeight = 120f;
        const int PreviewResolution = 512;

        [SerializeField] ProceduralShape shape;
        // Undoing a replacement brings the shape back as a new component on this object
        [SerializeField] GameObject shapeObject;
        [SerializeField] ShapeSpriteExporter.Settings settings = new();

        ShapeSpriteExporter.Plan plan;
        string planState;
        bool bakeScheduled, checkScheduled;
        string status;
        bool statusFailed;
        Texture2D checker;
        Texture2D previewTexture;
        Rect previewMain;
        float previewScale;

        Label shapeInfo;
        VisualElement content;
        VisualElement previewBox;
        Image preview;
        readonly VisualElement[] guides = new VisualElement[4];
        Label previewInfo;
        ObjectField folderField;
        TextField nameField;
        Label pathInfo;
        EvoEditorUITK.SegmentedControl<int> scaleControl;
        EvoEditorUITK.SegmentedControl<bool> typeControl;
        EvoEditorUITK.SegmentedControl<bool> colorControl;
        Label typeInfo;
        Label colorInfo;
        Toggle replaceToggle;
        VisualElement replaceOptions;
        Toggle stylerToggle;
        VisualElement shadowRow;
        EvoEditorUITK.SegmentedControl<ShadowHandling> shadowControl;
        VisualElement replaceNotes;
        Label error;
        EvoEditorUITK.StatusView statusView;
        Button exportButton;

        [MenuItem("CONTEXT/ProceduralShape/Export as Sprite...")]
        static void ExportFromContextMenu(MenuCommand command) => ShowWindow(command.context as ProceduralShape);

        /// <summary>
        /// Opens the window on a shape as a floating utility window, which can't be docked.
        /// </summary>
        public static void ShowWindow(ProceduralShape target)
        {
            var window = GetWindow<SpriteExportWindow>(true, "Export as Sprite", true);
            window.SetShape(target);
        }

        #region Lifecycle
        void OnEnable()
        {
            titleContent = new GUIContent("Export as Sprite");
            minSize = new Vector2(340, 480);
            LoadPrefs();

            Undo.undoRedoPerformed += OnUndoRedo;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;

            plan?.Dispose();
            plan = null;
            if (checker != null)
                DestroyImmediate(checker);
            if (previewTexture != null)
                DestroyImmediate(previewTexture);
        }

        // Styler colors can change elsewhere, such as in a preset
        void OnFocus() => ScheduleBake();

        void OnUndoRedo()
        {
            if (shape == null && shapeObject != null && shapeObject.TryGetComponent(out ProceduralShape restored))
                shape = restored;

            ScheduleBake();
            Refresh();
        }

        void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (shape == null)
                return;

            // Preset edits change Styler colors without touching the shape
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    ScheduleBake();
                    return;
                }
            }

            if (checkScheduled || rootVisualElement == null)
                return;

            checkScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                checkScheduled = false;
                if (shape != null && GetShapeState() != planState)
                    ScheduleBake();
            }).StartingIn(100);
        }

        void SetShape(ProceduralShape target)
        {
            shape = target;
            shapeObject = target != null ? target.gameObject : null;
            settings.fileName = target != null ? ShapeSpriteExporter.SanitizeFileName(target.name) : "";
            status = null;
            ScheduleBake();
            Refresh();
        }
        #endregion

        #region Preferences
        static string FolderPref => PrefPrefix + "Folder." + PlayerSettings.productGUID;

        void LoadPrefs()
        {
            string folder = EditorPrefs.GetString(FolderPref, "Assets");
            settings.folder = AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
            settings.scale = Mathf.Clamp(EditorPrefs.GetInt(PrefPrefix + "Scale", 1), 1, 4);
            settings.sliced = EditorPrefs.GetBool(PrefPrefix + "Sliced", false);
            settings.tint = EditorPrefs.GetBool(PrefPrefix + "Tint", false);
            settings.stylerObject = EditorPrefs.GetBool(PrefPrefix + "StylerObject", true);
            settings.shadow = (ShadowHandling)EditorPrefs.GetInt(PrefPrefix + "Shadow", 0);
        }

        void SavePrefs()
        {
            EditorPrefs.SetString(FolderPref, settings.folder);
            EditorPrefs.SetInt(PrefPrefix + "Scale", settings.scale);
            EditorPrefs.SetBool(PrefPrefix + "Sliced", settings.sliced);
            EditorPrefs.SetBool(PrefPrefix + "Tint", settings.tint);
            EditorPrefs.SetBool(PrefPrefix + "StylerObject", settings.stylerObject);
            EditorPrefs.SetInt(PrefPrefix + "Shadow", (int)settings.shadow);
        }
        #endregion

        #region Layout
        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("sprite-export");
            EvoEditorUITK.Apply(this, nameof(SpriteExportWindow));

            ScrollView body = EvoEditorUITK.CreateBody();
            root.Add(body);

            body.Add(EvoEditorUITK.CreateHeading("Shape", "evo-heading--first"));
            shapeInfo = EvoEditorUITK.CreateDescription(null, "sprite-export__shape");
            body.Add(shapeInfo);

            content = new VisualElement();
            body.Add(content);
            CreatePreview(content);
            CreateFileFields(content);
            CreateSpriteFields(content);
            CreateReplaceFields(content);

            error = EvoEditorUITK.CreateDescription(null, "evo-description--warning");
            body.Add(error);

            statusView = new EvoEditorUITK.StatusView(() =>
            {
                status = null;
                Refresh();
            });
            body.Add(statusView);

            var footer = new VisualElement();
            footer.AddToClassList("evo-footer");
            exportButton = EvoEditorUITK.CreateButton(null, null, Export);
            exportButton.AddToClassList("evo-primary");
            footer.Add(exportButton);
            root.Add(footer);

            UpdatePreview();
            Refresh();
        }

        void CreatePreview(VisualElement parent)
        {
            previewBox = new VisualElement();
            previewBox.AddToClassList("evo-well");
            previewBox.AddToClassList("sprite-export__preview");
            previewBox.RegisterCallback<GeometryChangedEvent>(_ => LayoutPreview());
            parent.Add(previewBox);

            preview = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("sprite-export__image");
            preview.style.backgroundImage = GetChecker();
            previewBox.Add(preview);

            for (int i = 0; i < guides.Length; i++)
            {
                guides[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                guides[i].AddToClassList("sprite-export__guide");
                guides[i].AddToClassList(i < 2 ? "sprite-export__guide--vertical" : "sprite-export__guide--horizontal");
                preview.Add(guides[i]);
            }

            previewInfo = EvoEditorUITK.CreateDescription(null, "sprite-export__preview-info");
            parent.Add(previewInfo);
        }

        void CreateFileFields(VisualElement parent)
        {
            parent.Add(EvoEditorUITK.CreateHeading("File"));

            folderField = EvoEditorUITK.CreateObjectField("Folder", typeof(DefaultAsset), AssetDatabase.LoadAssetAtPath<DefaultAsset>(settings.folder),
                "Where the sprite is saved. Drag a folder here from the Project window.", value =>
                {
                    string path = value != null ? AssetDatabase.GetAssetPath(value) : "";
                    if (!AssetDatabase.IsValidFolder(path))
                    {
                        ShowNotification(new GUIContent("Choose a folder."));
                        Refresh();
                        return;
                    }

                    settings.folder = path;
                    SavePrefs();
                    Refresh();
                });
            parent.Add(folderField);

            nameField = EvoEditorUITK.CreateTextField("Name", settings.fileName, "The sprite's file name, without .png.", value =>
            {
                settings.fileName = value;
                Refresh();
            });
            nameField.isDelayed = true;
            parent.Add(nameField);

            pathInfo = EvoEditorUITK.CreateDescription();
            parent.Add(pathInfo);
        }

        void CreateSpriteFields(VisualElement parent)
        {
            parent.Add(EvoEditorUITK.CreateHeading("Sprite"));

            scaleControl = new EvoEditorUITK.SegmentedControl<int>(value => SetOption(() => settings.scale = value));
            for (int scale = 1; scale <= 4; scale++)
                scaleControl.AddOption(scale, $"{scale}x", $"{scale} pixels per unit of the shape. The sprite's Pixels Per Unit matches, so it shows at the same size.");

            parent.Add(CreateRow("Scale", scaleControl));

            typeControl = new EvoEditorUITK.SegmentedControl<bool>(value => SetOption(() => settings.sliced = value));
            typeControl.AddOption(false, "Simple", "Stretches as a whole when resized.")
                .AddOption(true, "Sliced", "Keeps corners and edges as they are when resized, like the shape. The borders are found by comparing the rows and columns that stretching would repeat.");
            parent.Add(CreateRow("Type", typeControl));

            typeInfo = EvoEditorUITK.CreateDescription(null, "sprite-export__info");
            parent.Add(typeInfo);

            colorControl = new EvoEditorUITK.SegmentedControl<bool>(value => SetOption(() => settings.tint = value));
            colorControl.AddOption(false, "Baked", "Every color as the shape shows it.")
                .AddOption(true, "Tintable", "The fill is white, so the Image color sets it. Black shadows stay black.");
            parent.Add(CreateRow("Color", colorControl));

            colorInfo = EvoEditorUITK.CreateDescription(null, "sprite-export__info");
            parent.Add(colorInfo);
        }

        void CreateReplaceFields(VisualElement parent)
        {
            parent.Add(EvoEditorUITK.CreateHeading("Scene"));

            replaceToggle = EvoEditorUITK.CreateToggle("Replace with Image", settings.replace,
                "Swaps the Procedural Shape for an Image that uses the sprite, in one undo step. References to the shape move to the Image.",
                value => SetOption(() => settings.replace = value));
            parent.Add(replaceToggle);

            replaceOptions = new VisualElement();
            replaceOptions.AddToClassList("sprite-export__options");
            parent.Add(replaceOptions);

            stylerToggle = EvoEditorUITK.CreateToggle("Add Styler Object", settings.stylerObject,
                "Keeps the shape's Styler color and interaction colors on the Image. Needs Tintable color.",
                value => SetOption(() => settings.stylerObject = value));
            replaceOptions.Add(stylerToggle);

            shadowControl = new EvoEditorUITK.SegmentedControl<ShadowHandling>(value => SetOption(() => settings.shadow = value));
            shadowControl.AddOption(ShadowHandling.ChildImage, "Child Image", "Bakes the shadow outside the shape into a child Image behind the content.")
                .AddOption(ShadowHandling.Drop, "Drop", "Leaves the outer shadow out.");
            shadowRow = CreateRow("Outer Shadow", shadowControl);
            replaceOptions.Add(shadowRow);

            replaceNotes = new VisualElement();
            replaceOptions.Add(replaceNotes);
        }

        static VisualElement CreateRow(string label, VisualElement control)
        {
            var row = new VisualElement();
            row.AddToClassList("sprite-export__row");

            var text = new Label(label);
            text.AddToClassList("sprite-export__label");
            row.Add(text);
            row.Add(control);
            return row;
        }

        Texture2D GetChecker()
        {
            if (checker != null)
                return checker;

            Color dark = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
            Color light = EditorGUIUtility.isProSkin ? new Color(0.26f, 0.26f, 0.26f) : new Color(0.92f, 0.92f, 0.92f);
            checker = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            checker.SetPixels(new[] { dark, light, light, dark });
            checker.Apply(false, true);
            return checker;
        }
        #endregion

        #region State
        void SetOption(Action change)
        {
            change();
            status = null;
            SavePrefs();
            ScheduleBake();
            Refresh();
        }

        string GetShapeState()
        {
            if (shape == null)
                return "";

            return EditorJsonUtility.ToJson(shape) + shape.rectTransform.rect + shape.GetRadiiPixels(shape.GetDrawingRect()) + shape.isActiveAndEnabled;
        }

        void ScheduleBake()
        {
            if (bakeScheduled || rootVisualElement == null)
                return;

            bakeScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                bakeScheduled = false;
                Bake();
            }).StartingIn(30);
        }

        void Bake()
        {
            plan?.Dispose();
            plan = shape != null ? ShapeSpriteExporter.Prepare(shape, settings) : null;
            planState = GetShapeState();
            UpdatePreview();
            Refresh();
        }

        void Refresh()
        {
            if (exportButton == null)
                return;

            bool hasShape = shape != null;
            content.style.display = hasShape ? DisplayStyle.Flex : DisplayStyle.None;
            shapeInfo.text = hasShape ? GetShapeInfo() : "The shape isn't here anymore. Undo brings back a replaced shape.";

            folderField.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(settings.folder));
            nameField.SetValueWithoutNotify(settings.fileName);
            scaleControl.SetValueWithoutNotify(settings.scale);
            typeControl.SetValueWithoutNotify(settings.sliced);
            colorControl.SetValueWithoutNotify(settings.tint);
            replaceToggle.SetValueWithoutNotify(settings.replace);
            stylerToggle.SetValueWithoutNotify(settings.stylerObject);
            shadowControl.SetValueWithoutNotify(settings.shadow);

            string pathError = ShapeSpriteExporter.GetPathError(settings);
            bool replacesFile = false;
            pathInfo.text = pathError ?? GetPathInfo(out replacesFile);
            pathInfo.EnableInClassList("evo-description--warning", pathError != null || replacesFile);

            RefreshSpriteInfo();
            RefreshReplaceInfo();

            string blocker = pathError ?? plan?.error;
            error.text = hasShape && plan?.error != null ? plan.error : "";
            error.style.display = error.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            statusView.SetState(statusFailed ? 0f : 1f, status, false, statusFailed);
            exportButton.text = settings.replace ? "Export and Replace" : "Export Sprite";
            exportButton.SetEnabled(hasShape && blocker == null && plan?.main != null);
        }

        // Every file the export writes, and whether one of them replaces an existing sprite
        string GetPathInfo(out bool replacesFile)
        {
            var paths = ShapeSpriteExporter.GetPaths(plan, settings);
            var existing = paths.FindAll(path => AssetImporter.GetAtPath(path) != null);
            replacesFile = existing.Count > 0;
            string files = string.Join("\n", paths);
            if (!replacesFile)
                return files;

            return existing.Count == 1
                ? $"{files}\nReplaces the existing {System.IO.Path.GetFileName(existing[0])}. Everything using it updates."
                : $"{files}\nReplaces {existing.Count} existing sprites. Everything using them updates.";
        }

        string GetShapeInfo()
        {
            Rect rect = shape.rectTransform.rect;
            string info = $"{shape.name} · {rect.width:0.##} x {rect.height:0.##} · {ObjectNames.NicifyVariableName(shape.Shape.ToString())}";
            if (!shape.isActiveAndEnabled)
                info += " · Inactive";

            return info;
        }

        void RefreshSpriteInfo()
        {
            ShapeSpriteExporter.Bake main = plan?.main;
            if (main == null)
            {
                previewInfo.text = "";
                typeInfo.text = "";
                colorInfo.text = "";
            }
            else
            {
                string size = $"{main.texture.width} x {main.texture.height} px";
                if (main.Sliced)
                    size += $" · Borders: left {main.border.x}, right {main.border.z}, top {main.border.w}, bottom {main.border.y}";
                if (plan.outline != null)
                    size += $"\nOutline: {plan.outline.texture.width} x {plan.outline.texture.height} px, its own sprite";
                if (plan.shadow != null)
                    size += $"\nShadow: {plan.shadow.texture.width} x {plan.shadow.texture.height} px";

                previewInfo.text = size;
                typeInfo.text = plan.sliceNote ?? (settings.sliced
                    ? "Corners and edges keep their size, and the middle between the guides stretches. The preview shows it at the shape's size."
                    : "The sprite stretches as a whole when its size changes.");
                typeInfo.EnableInClassList("evo-description--warning", plan.sliceNote != null);
                colorInfo.text = GetColorInfo();
            }

            typeInfo.style.display = typeInfo.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            colorInfo.style.display = colorInfo.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        string GetColorInfo()
        {
            if (!settings.tint)
                return "Colors are baked in, as the shape shows them.";

            ProceduralShape.SpriteTint tint = plan.tint;
            if (tint.error != null)
                return "";

            string info = DescribeTint(tint.mainIsFill ? "fill" : "outline", tint.main, true);
            if (tint.separateOutline)
                info += " The outline has its own color, so it's white in a sprite of its own. " + DescribeTint("outline", tint.outline, false);

            return info;
        }

        static string DescribeTint(string layer, ProceduralShape.SpriteTintLayer tint, bool main)
        {
            string lead = main ? $"The {layer} is white. " : "";
            string target = main ? "the Image" : "its Image";
            return tint.mode switch
            {
                ProceduralShape.ColorMode.Styler => $"{lead}Its Styler color, {tint.style.color.stylerID}, goes on {target}.",
                ProceduralShape.ColorMode.Custom => $"{lead}Its color goes on {target}.",
                _ => $"{lead}The shape's color goes on {target}."
            };
        }

        void RefreshReplaceInfo()
        {
            replaceOptions.style.display = settings.replace ? DisplayStyle.Flex : DisplayStyle.None;
            bool stylerAvailable = plan != null && plan.tint.error == null && (ShapeSpriteExporter.UsesStylerObject(plan.tint.main)
                || (plan.tint.separateOutline && ShapeSpriteExporter.UsesStylerObject(plan.tint.outline)));
            stylerToggle.style.display = stylerAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            stylerToggle.SetEnabled(settings.tint);
            shadowRow.style.display = plan != null && plan.hasOuterShadow ? DisplayStyle.Flex : DisplayStyle.None;

            replaceNotes.Clear();
            if (plan == null || !settings.replace)
                return;

            foreach (string note in plan.notes)
                replaceNotes.Add(EvoEditorUITK.CreateDescription(note, "sprite-export__note"));
        }

        // The result as its Images draw it at the shape's size, with the main sprite's borders as guides
        void UpdatePreview()
        {
            if (preview == null)
                return;

            if (previewTexture != null)
                DestroyImmediate(previewTexture);

            previewTexture = ShapeSpriteExporter.CreatePreview(plan, settings, PreviewResolution, out previewMain, out previewScale);
            preview.image = previewTexture;
            preview.style.display = previewTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
            LayoutPreview();
        }

        void LayoutPreview()
        {
            if (previewTexture == null || previewBox == null || plan?.main == null)
                return;

            float width = Mathf.Max(1f, previewBox.contentRect.width - 16f);
            float display = Mathf.Min(width / previewTexture.width, PreviewHeight / previewTexture.height, 4f);
            preview.style.width = previewTexture.width * display;
            preview.style.height = previewTexture.height * display;

            // Sprite pixels to points; the preview texture counts rows from the bottom, the layout from the top
            ShapeSpriteExporter.Bake main = plan.main;
            float pixel = previewScale * display;
            float left = previewMain.xMin * display;
            float right = previewMain.xMax * display;
            float top = (previewTexture.height - previewMain.yMax) * display;
            float bottom = (previewTexture.height - previewMain.yMin) * display;
            SetGuide(guides[0], main.slicedX, left + main.border.x * pixel, true);
            SetGuide(guides[1], main.slicedX, right - main.border.z * pixel, true);
            SetGuide(guides[2], main.slicedY, top + main.border.w * pixel, false);
            SetGuide(guides[3], main.slicedY, bottom - main.border.y * pixel, false);
        }

        static void SetGuide(VisualElement guide, bool visible, float position, bool vertical)
        {
            guide.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (vertical)
                guide.style.left = position;
            else
                guide.style.top = position;
        }
        #endregion

        #region Export
        void Export()
        {
            if (shape == null)
                return;

            ShapeSpriteExporter.Plan fresh = null;
            try
            {
                fresh = ShapeSpriteExporter.Prepare(shape, settings);
                if (fresh.error != null)
                    throw new InvalidOperationException(fresh.error);

                status = ShapeSpriteExporter.Export(fresh, settings, out Object _);
                statusFailed = false;
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Sprite>(ShapeSpriteExporter.GetPath(settings)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                status = exception.Message;
                statusFailed = true;
            }
            finally
            {
                fresh?.Dispose();
            }

            ScheduleBake();
            Refresh();
        }
        #endregion
    }
}
