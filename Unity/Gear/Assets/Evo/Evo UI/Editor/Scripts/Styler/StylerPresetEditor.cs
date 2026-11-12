using System;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(StylerPreset)), NoAutoStaticsCleanup]
    public class StylerPresetEditor : UnityEditor.Editor
    {
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;
        static readonly Action<SerializedProperty> AudioItemDrawer = DrawAudioItem;
        static readonly Action<SerializedProperty> ColorItemDrawer = DrawColorItem;
        static readonly Action<SerializedProperty> FontItemDrawer = DrawFontItem;
        static readonly Action<SerializedProperty> GradientItemDrawer = DrawGradientItem;
        static readonly Action<SerializedProperty> ShapeStyleItemDrawer = DrawShapeStyleItem;
        static readonly Action<SerializedProperty> SpriteItemDrawer = DrawSpriteItem;
        static readonly Action<SerializedProperty> TextStyleItemDrawer = DrawTextStyleItem;
        static readonly Action<SerializedProperty, Rect> ColorPreviewDrawer = DrawColorPreview;
        static readonly Action<SerializedProperty, Rect> GradientPreviewDrawer = DrawGradientPreview;

        StylerPreset spTarget;

        SerializedProperty audioItems;
        SerializedProperty colorItems;
        SerializedProperty fontItems;
        SerializedProperty textStyleItems;
        SerializedProperty gradientItems;
        SerializedProperty spriteItems;
        SerializedProperty shapeStyleItems;
        SerializedProperty updateMode;
        SerializedProperty keepOnGlobalApply;

        ReorderableListOptions audioListOptions;
        ReorderableListOptions colorListOptions;
        ReorderableListOptions fontListOptions;
        ReorderableListOptions textStyleListOptions;
        ReorderableListOptions gradientListOptions;
        ReorderableListOptions spriteListOptions;
        ReorderableListOptions shapeStyleListOptions;

        bool isDefaultPreset;
        bool isFallbackPreset;

        void OnEnable() => InitializeEditorState();

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector(true);

            EditorGUI.BeginChangeCheck();
            DrawItemList(audioItems, AudioItemDrawer, audioListOptions);
            DrawItemList(colorItems, ColorItemDrawer, colorListOptions);
            DrawItemList(fontItems, FontItemDrawer, fontListOptions);
            DrawItemList(gradientItems, GradientItemDrawer, gradientListOptions);
            DrawItemList(shapeStyleItems, ShapeStyleItemDrawer, shapeStyleListOptions);
            DrawItemList(spriteItems, SpriteItemDrawer, spriteListOptions);
            DrawItemList(textStyleItems, TextStyleItemDrawer, textStyleListOptions);
            SettingsFoldout();

            if (!isFallbackPreset)
                DrawSetDefault();

            bool controlsChanged = EditorGUI.EndChangeCheck();
            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void InitializeEditorState()
        {
            spTarget = (StylerPreset)target;

            audioItems = serializedObject.FindProperty(nameof(StylerPreset.audioItems));
            colorItems = serializedObject.FindProperty(nameof(StylerPreset.colorItems));
            fontItems = serializedObject.FindProperty(nameof(StylerPreset.fontItems));
            textStyleItems = serializedObject.FindProperty(nameof(StylerPreset.textStyleItems));
            gradientItems = serializedObject.FindProperty(nameof(StylerPreset.gradientItems));
            spriteItems = serializedObject.FindProperty(nameof(StylerPreset.spriteItems));
            shapeStyleItems = serializedObject.FindProperty(nameof(StylerPreset.shapeStyleItems));
            updateMode = serializedObject.FindProperty(nameof(StylerPreset.updateMode));
            keepOnGlobalApply = serializedObject.FindProperty(nameof(StylerPreset.keepOnGlobalApply));

            InitializeItemLists();
            CheckDefaultStatus();
            CheckFallbackStatus();
        }

        void InitializeItemLists()
        {
            audioListOptions = CreateListOptions("StylerPreset.Audio", "Audio", "UI_Audio",
                "Add an audio item to this preset.", "New Audio", InitializeAudioItem);

            colorListOptions = CreateListOptions("StylerPreset.Color", "Color", "UI_Style",
                    "Add a color item to this preset.", "New Color", InitializeColorItem)
                .WithItemPreview(ColorPreviewDrawer, new Vector2(12f, 12f));

            fontListOptions = CreateListOptions("StylerPreset.Font", "Font", "UI_Text",
                "Add a font item to this preset.", "New Font", InitializeFontItem);

            textStyleListOptions = CreateListOptions("StylerPreset.TextStyle", "Text Style", "UI_TextStyle",
                "Add a text style item to this preset.", "New Text Style", InitializeTextStyleItem);

            gradientListOptions = CreateListOptions("StylerPreset.Gradient", "Gradient", "UI_Gradient",
                    "Add a gradient item to this preset.", "New Gradient", InitializeGradientItem)
                .WithItemPreview(GradientPreviewDrawer, new Vector2(20f, 10f));

            spriteListOptions = CreateListOptions("StylerPreset.Sprite", "Sprite", "UI_Sprite",
                "Add a sprite item to this preset.", "New Sprite", InitializeSpriteItem);

            shapeStyleListOptions = CreateListOptions("StylerPreset.ShapeStyle", "Shape Style", "UI_Shape",
                "Add a shape style item to this preset.", "New Shape Style", InitializeShapeStyleItem);
        }

        static ReorderableListOptions CreateListOptions(string controlName, string title, string icon,
            string emptyMessage, string addLabel, Action<SerializedProperty> initializeItem)
        {
            return ReorderableListOptions.WithControlName(controlName)
                .WithTitle(title)
                .WithIcon(icon)
                .WithEmptyMessage(emptyMessage)
                .WithAddLabel(addLabel)
                .WithItemInitializer(initializeItem)
                .WithDeleteTooltip($"Delete {title.ToLowerInvariant()} item");
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(updateMode, FieldOptions.WithTooltip(updateMode.tooltip).WithoutBackground().WithoutSpace());
                    EvoEditorGUI.BeginContainer();

                    string description = updateMode.enumValueIndex == 0
                        ? "Styler objects are updated in the editor and on every change at runtime."
                        : "Styler objects are always updated in the editor and whenever the object is enabled at runtime.";

                    EvoEditorGUI.Callout(description, spaceBefore: 2, spaceAfter: 0);
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Toggle(keepOnGlobalApply, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawItemList(SerializedProperty items, Action<SerializedProperty> drawItem, ReorderableListOptions options)
        {
            EvoEditorGUI.ReorderableList(serializedObject, items, ItemTitleGetter, drawItem, options,
                compactItems: true, compactHeader: false);
            EvoEditorGUI.AddFoldoutSpace();
        }

        #region Item Drawing
        static string GetItemTitle(SerializedProperty item, int index)
        {
            SerializedProperty itemID = item.FindPropertyRelative("itemID");
            return itemID == null || string.IsNullOrEmpty(itemID.stringValue) ? $"Item {index + 1}" : itemID.stringValue;
        }

        static void DrawItem(SerializedProperty item, Styler.ItemType itemType)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true);
            {
                DrawItemID(item, itemType);

                if (itemType == Styler.ItemType.Audio)
                    EvoEditorGUI.Property(item.FindPropertyRelative("audioAsset"), "Audio Clip", FieldOptions.WithoutSpace());
                else if (itemType == Styler.ItemType.Color)
                    EvoEditorGUI.Property(item.FindPropertyRelative("colorValue"), "Color", FieldOptions.WithoutSpace());
                else if (itemType == Styler.ItemType.Font)
                    DrawFontFields(item);
                else if (itemType == Styler.ItemType.TextStyle)
                    DrawTextStyleFields(item);
                else if (itemType == Styler.ItemType.Gradient)
                    EvoEditorGUI.Property(item.FindPropertyRelative("gradientValue"), "Gradient", FieldOptions.WithoutSpace());
                else if (itemType == Styler.ItemType.ShapeStyle)
                    DrawShapeStyleFields(item);
                else if (itemType == Styler.ItemType.Sprite)
                    DrawSpriteFields(item);
            }
            EvoEditorGUI.EndContainer();
        }

        static void DrawItemID(SerializedProperty item, Styler.ItemType itemType)
        {
            SerializedProperty itemID = item.FindPropertyRelative(nameof(Styler.Item.itemID));
            var preset = (StylerPreset)item.serializedObject.targetObject;
            int index = GetItemNumber(item) - 1;
            string currentID = itemID.stringValue;

            LayoutOptions layoutOptions = LayoutOptions.WithBackground(true).WithSpace();
            EvoEditorGUI.BeginRow(layoutOptions);
            GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);
            Rect fieldRect = EditorGUILayout.GetControlRect();
            string newID;

            using (var scope = new EditorGUI.PropertyScope(fieldRect, new GUIContent("ID",
                "Unique identifier for this item. Renaming keeps the old ID working for objects that use it."), itemID))
                newID = EditorGUI.DelayedTextField(fieldRect, scope.content, currentID);

            EvoEditorGUI.EndRow(layoutOptions);

            // Renames run after this pass, since they can ask about other presets that share the ID
            if (newID != currentID)
                EditorApplication.delayCall += () => StylerIDMigration.Rename(preset, itemType, index, currentID, newID);

            SerializedProperty formerIDs = item.FindPropertyRelative(nameof(Styler.Item.formerIDs));
            if (formerIDs.arraySize == 0)
                return;

            var names = new string[formerIDs.arraySize];

            for (int i = 0; i < names.Length; i++)
                names[i] = $"'{formerIDs.GetArrayElementAtIndex(i).stringValue}'";

            EvoEditorGUI.Callout($"Renamed from {string.Join(", ", names)}. Objects that still use " +
                $"{(names.Length == 1 ? "it" : "them")} resolve to this item.", spaceBefore: 2);

            string firstFormerID = formerIDs.GetArrayElementAtIndex(0).stringValue;
            EditorGUILayout.BeginHorizontal();
            if (EvoEditorGUI.Button("Update References", options: ButtonOptions.WithTooltip("Replace former IDs in scenes and prefabs with this ID.")))
                EditorApplication.delayCall += () => StylerIDMigratorWindow.Open(preset, itemType, firstFormerID, currentID);

            EvoEditorGUI.AddPropertySpace();
            if (EvoEditorGUI.Button("Clear", options: ButtonOptions.WithTooltip("Stop resolving former IDs.")))
                EditorApplication.delayCall += () => StylerIDMigration.ClearFormerIDs(preset, itemType, index);

            EditorGUILayout.EndHorizontal();
            EvoEditorGUI.AddPropertySpace();
        }

        static void DrawAudioItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.Audio);

        static void DrawColorItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.Color);

        static void DrawFontItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.Font);

        static void DrawTextStyleItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.TextStyle);

        static void DrawGradientItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.Gradient);

        static void DrawShapeStyleItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.ShapeStyle);

        static void DrawSpriteItem(SerializedProperty item) => DrawItem(item, Styler.ItemType.Sprite);

        static void DrawFontFields(SerializedProperty item)
        {
            SerializedProperty fontAsset = item.FindPropertyRelative(nameof(Styler.FontItem.fontAsset));
            SerializedProperty providerFonts = item.FindPropertyRelative(nameof(Styler.FontItem.providerFonts));
            var backends = TextProvider.Backends;

            for (int i = 0; i < backends.Count; i++)
            {
                TextBackend backend = backends[i];
                string label = backends.Count > 1 ? backend.DisplayName : "Font Asset";
                FieldOptions options = i == backends.Count - 1 ? FieldOptions.WithoutSpace() : default;

                if (backend.FontType.IsAssignableFrom(typeof(TMPro.TMP_FontAsset)))
                {
                    EvoEditorGUI.Property(fontAsset, label, options);
                    continue;
                }

                int index = -1;
                for (int j = 0; j < providerFonts.arraySize && index < 0; j++)
                {
                    if (backend.IsFont(providerFonts.GetArrayElementAtIndex(j).objectReferenceValue))
                        index = j;
                }

                UnityEngine.Object current = index >= 0 ? providerFonts.GetArrayElementAtIndex(index).objectReferenceValue : null;
                UnityEngine.Object font = EvoEditorGUI.ObjectField(current, backend.FontType, label, false, options);

                if (font == current)
                    continue;

                if (index < 0)
                {
                    index = providerFonts.arraySize;
                    providerFonts.InsertArrayElementAtIndex(index);
                }

                if (font != null) { providerFonts.GetArrayElementAtIndex(index).objectReferenceValue = font; }
                else { providerFonts.DeleteArrayElementAtIndex(index); }
            }
        }

        static void DrawTextStyleFields(SerializedProperty item)
        {
            SerializedProperty flagsProperty = item.FindPropertyRelative(nameof(Styler.TextStyleItem.flags));
            Styler.TextStyleFlags flags = DrawStyleFlagsMask(flagsProperty, out Styler.TextStyleFlags added);
            if (flags == Styler.TextStyleFlags.None)
                return;

            if (Includes(flags, Styler.TextStyleFlags.FontStyle))
            {
                SerializedProperty fontStyle = item.FindPropertyRelative("fontStyle");
                bool visible = BeginStyleGroup(fontStyle, "Font Style", Includes(added, Styler.TextStyleFlags.FontStyle));
                if (visible)
                    EvoEditorGUI.Property(fontStyle, "Font Style", FieldOptions.WithInvertedColor().WithoutSpace());

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.TextStyleFlags.Size))
            {
                SerializedProperty fontSize = item.FindPropertyRelative("fontSize");
                bool visible = BeginStyleGroup(fontSize, "Size", Includes(added, Styler.TextStyleFlags.Size));
                if (visible)
                {
                    SerializedProperty enableAutoSizing = item.FindPropertyRelative("enableAutoSizing");
                    EvoEditorGUI.Toggle(enableAutoSizing, "Auto Size", FieldOptions.WithInvertedColor());

                    if (enableAutoSizing.boolValue)
                    {
                        EvoEditorGUI.Property(item.FindPropertyRelative("fontSizeMin"), "Min Size", FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(item.FindPropertyRelative("fontSizeMax"), "Max Size", FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(item.FindPropertyRelative("characterWidthAdjustment"), "WD%", FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(item.FindPropertyRelative("lineSpacingAdjustment"), "Line", FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                    else
                    {
                        EvoEditorGUI.Property(fontSize, "Font Size", FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.TextStyleFlags.Alignment))
            {
                SerializedProperty alignment = item.FindPropertyRelative("alignment");
                bool visible = BeginStyleGroup(alignment, "Alignment", Includes(added, Styler.TextStyleFlags.Alignment));
                if (visible)
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.AddLayoutSpace();
                    EditorGUILayout.PropertyField(alignment, GUIContent.none);
                    EvoEditorGUI.EndColumn();
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.TextStyleFlags.Spacing))
            {
                SerializedProperty characterSpacing = item.FindPropertyRelative("characterSpacing");
                bool visible = BeginStyleGroup(characterSpacing, "Spacing", Includes(added, Styler.TextStyleFlags.Spacing));
                if (visible)
                {
                    EvoEditorGUI.Property(characterSpacing, "Character", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative("wordSpacing"), "Word", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative("lineSpacing"), "Line", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative("paragraphSpacing"), "Paragraph", FieldOptions.WithInvertedColor().WithoutSpace());
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.TextStyleFlags.WrappingAndOverflow))
            {
                SerializedProperty overflowMode = item.FindPropertyRelative("overflowMode");
                bool visible = BeginStyleGroup(overflowMode, "Wrapping & Overflow", Includes(added, Styler.TextStyleFlags.WrappingAndOverflow));
                if (visible)
                {
                    EvoEditorGUI.Toggle(item.FindPropertyRelative("enableWordWrapping"), "Word Wrapping", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(overflowMode, "Overflow", FieldOptions.WithInvertedColor().WithoutSpace());
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.TextStyleFlags.Margin))
            {
                SerializedProperty margin = item.FindPropertyRelative("margin");
                bool visible = BeginStyleGroup(margin, "Margin", Includes(added, Styler.TextStyleFlags.Margin));
                if (visible)
                    EvoEditorGUI.ArrayProperty(margin, "Margin", FieldOptions.WithInvertedColor().WithoutSpace());

                EndStyleGroup(visible);
            }
        }

        static void DrawSpriteFields(SerializedProperty item)
        {
            EvoEditorGUI.Property(item.FindPropertyRelative("spriteAsset"), "Sprite Asset", FieldOptions.WithoutSpace());
            EvoEditorGUI.AddLayoutSpace();

            SerializedProperty applySettings = item.FindPropertyRelative("applyImageSettings");
            bool expanded = BeginOverride(applySettings, "Apply Image Settings");
            if (expanded)
            {
                SerializedProperty imageType = item.FindPropertyRelative("imageType");
                EvoEditorGUI.Property(imageType, "Image Type", FieldOptions.WithInvertedColor());

                var type = (UnityEngine.UI.Image.Type)imageType.enumValueIndex;
                if (type is UnityEngine.UI.Image.Type.Simple or UnityEngine.UI.Image.Type.Filled)
                {
                    EvoEditorGUI.Toggle(item.FindPropertyRelative("preserveAspect"), "Preserve Aspect",
                        FieldOptions.WithInvertedColor().WithoutSpace());
                }
                else
                {
                    EvoEditorGUI.Property(item.FindPropertyRelative("pixelsPerUnitMultiplier"), "PPU Multiplier",
                        FieldOptions.WithInvertedColor().WithoutSpace());
                }

                if (type == UnityEngine.UI.Image.Type.Filled)
                    DrawFilledImageFields(item);
            }
            EndOverride(expanded, addSpace: false);
        }

        static void DrawFilledImageFields(SerializedProperty item)
        {
            EvoEditorGUI.AddLayoutSpace();

            SerializedProperty fillMethod = item.FindPropertyRelative("fillMethod");
            EvoEditorGUI.Property(fillMethod, FieldOptions.WithInvertedColor());

            SerializedProperty fillOrigin = item.FindPropertyRelative("fillOrigin");
            string[] originOptions = GetFillOriginOptions((UnityEngine.UI.Image.FillMethod)fillMethod.enumValueIndex);
            fillOrigin.intValue = Mathf.Clamp(fillOrigin.intValue, 0, originOptions.Length - 1);
            fillOrigin.intValue = EvoEditorGUI.Dropdown(fillOrigin.intValue, originOptions, "Fill Origin",
                FieldOptions.WithInvertedColor());

            EvoEditorGUI.Slider(item.FindPropertyRelative("fillAmount"), 0f, 1f, FieldOptions.WithInvertedColor().WithoutSpace());
            
            if (fillMethod.enumValueIndex <= (int)UnityEngine.UI.Image.FillMethod.Vertical)
                return;

            EvoEditorGUI.AddLayoutSpace();
            EvoEditorGUI.Toggle(item.FindPropertyRelative("fillClockwise"), "Clockwise",
                FieldOptions.WithInvertedColor().WithoutSpace());
        }

        static void DrawShapeStyleFields(SerializedProperty item)
        {
            SerializedProperty flagsProperty = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.flags));
            Styler.ShapeStyleFlags flags = DrawStyleFlagsMask(flagsProperty, out Styler.ShapeStyleFlags added);
            if (flags == Styler.ShapeStyleFlags.None)
                return;

            if (Includes(flags, Styler.ShapeStyleFlags.Graphic))
            {
                SerializedProperty scaleMode = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.scaleMode));
                bool visible = BeginStyleGroup(scaleMode, "Graphic", Includes(added, Styler.ShapeStyleFlags.Graphic));
                if (visible)
                {
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.sprite)), "Source Image", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(scaleMode, "Scale Mode", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.softness)), "Edge Softness", FieldOptions.WithInvertedColor().WithoutSpace());
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.ShapeStyleFlags.Shape))
            {
                SerializedProperty shape = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shape));
                bool visible = BeginStyleGroup(shape, "Shape", Includes(added, Styler.ShapeStyleFlags.Shape));
                if (visible)
                {
                    EvoEditorGUI.Property(shape, "Type", FieldOptions.WithInvertedColor().WithoutSpace());
                    ProceduralShapeEditor.DrawShapeControls(shape,
                        item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.polygonSides)),
                        item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shapeAngle)),
                        item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shapeSkew)));

                    if (shape.intValue != (int)ProceduralShape.ShapeType.Ellipse)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Radius", nestDepth: 1);
                        ProceduralShapeEditor.DrawShapeRadiusControls(shape,
                            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.radiusMode)),
                            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.cornerRadius)),
                            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.independentCorners)),
                            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.squircleCorners)));
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();
                    }
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.ShapeStyleFlags.Fill))
            {
                SerializedProperty fillCenter = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.fillCenter));
                bool visible = BeginStyleGroup(fillCenter, "Fill", Includes(added, Styler.ShapeStyleFlags.Fill));
                if (visible)
                    EvoEditorGUI.Toggle(fillCenter, "Fill Center", FieldOptions.WithInvertedColor().WithoutSpace());

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.ShapeStyleFlags.Outline))
            {
                SerializedProperty outlineWidth = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outlineWidth));
                bool visible = BeginStyleGroup(outlineWidth, "Outline", Includes(added, Styler.ShapeStyleFlags.Outline));
                if (visible)
                    EvoEditorGUI.Property(outlineWidth, "Width", FieldOptions.WithInvertedColor().WithoutSpace());

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.ShapeStyleFlags.InnerShadow))
            {
                SerializedProperty innerShadowSize = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowSize));
                bool visible = BeginStyleGroup(innerShadowSize, "Inner Shadow", Includes(added, Styler.ShapeStyleFlags.InnerShadow));
                if (visible)
                {
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowOffset)), "Offset", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(innerShadowSize, "Size", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowSoftness)), "Softness", FieldOptions.WithInvertedColor().WithoutSpace());
                }

                EndStyleGroup(visible);
            }

            if (Includes(flags, Styler.ShapeStyleFlags.OuterShadow))
            {
                SerializedProperty outerShadowSize = item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowSize));
                bool visible = BeginStyleGroup(outerShadowSize, "Outer Shadow", Includes(added, Styler.ShapeStyleFlags.OuterShadow));
                if (visible)
                {
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowOffset)), "Offset", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(outerShadowSize, "Size", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowSoftness)), "Softness", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowOutsideOnly)), "Outside Only", FieldOptions.WithInvertedColor().WithoutSpace());
                }

                EndStyleGroup(visible);
            }
        }

        static T DrawStyleFlagsMask<T>(SerializedProperty property, out T added) where T : Enum
        {
            var value = (T)Enum.ToObject(typeof(T), property.intValue);
            added = default;

            EvoEditorGUI.BeginRow();
            GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);

            Rect popupRect = EditorGUILayout.GetControlRect();
            GUIContent content = new("Flags", property.tooltip);
            using (var propertyScope = new EditorGUI.PropertyScope(popupRect, content, property))
            {
                popupRect = EditorGUI.PrefixLabel(popupRect, propertyScope.content);
                bool previousMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var newValue = (T)EditorGUI.EnumFlagsField(popupRect, value);
                if (EditorGUI.EndChangeCheck())
                {
                    int newFlags = Convert.ToInt32(newValue);
                    added = (T)Enum.ToObject(typeof(T), newFlags & ~property.intValue);
                    property.intValue = newFlags;
                    property.serializedObject.ApplyModifiedProperties();
                    value = newValue;
                }
                EditorGUI.showMixedValue = previousMixedValue;
            }

            EvoEditorGUI.EndRow();
            return value;
        }

        static bool Includes(Styler.ShapeStyleFlags flags, Styler.ShapeStyleFlags feature) => (flags & feature) != 0;

        static bool Includes(Styler.TextStyleFlags flags, Styler.TextStyleFlags feature) => (flags & feature) != 0;

        // Each feature folds on its own, starting closed, keyed by the path of a field it contains
        static bool BeginStyleGroup(SerializedProperty keyProperty, string title, bool flagAdded)
        {
            UnityEngine.Object preset = keyProperty.serializedObject.targetObject;
            string key = keyProperty.propertyPath;

            // A feature just switched on in Flags opens, so its settings are right there
            if (flagAdded)
                EvoEditorGUI.SetFoldoutState(preset, key, true);

            EvoEditorGUI.AddPropertySpace();
            if (!EvoEditorGUI.BeginFoldout(preset, key, title, false, FoldoutOptions.WithoutBoldText(), compact: true))
                return false;

            EvoEditorGUI.BeginContainer(nestDepth: 1);
            return true;
        }

        static void EndStyleGroup(bool visible)
        {
            if (visible)
                EvoEditorGUI.EndContainer();

            EvoEditorGUI.EndFoldout(addSpace: false);
        }

        static bool BeginOverride(SerializedProperty property, string title)
        {
            EvoEditorGUI.BeginColumn();
            EvoEditorGUI.Toggle(property, title, FieldOptions.WithTooltip(property.tooltip).WithInvertedColor().WithoutSpace(),
                drawIdleBackground: false);

            if (!property.boolValue)
                return false;

            EvoEditorGUI.BeginContainer(compactHeader: true);
            return true;
        }

        static void EndOverride(bool expanded, bool addSpace = true)
        {
            if (expanded)
                EvoEditorGUI.EndContainer();

            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }
        #endregion

        #region Item Helpers
        static void DrawColorPreview(SerializedProperty item, Rect rect)
        {
            SerializedProperty color = item.FindPropertyRelative("colorValue");
            if (color != null)
                EvoEditorGUI.DrawRoundedBackground(rect, color.colorValue);
        }

        static void DrawGradientPreview(SerializedProperty item, Rect rect)
        {
            SerializedProperty gradient = item.FindPropertyRelative("gradientValue");
            if (gradient == null)
                return;

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(rect, gradient, GUIContent.none);
        }

        static void InitializeAudioItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.Audio, "New Audio");
            item.FindPropertyRelative("audioAsset").objectReferenceValue = null;
        }

        static void InitializeColorItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.Color, $"Color {GetItemNumber(item)}");
            item.FindPropertyRelative("colorValue").colorValue = Color.white;
        }

        static void InitializeFontItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.Font, "New Font");
            item.FindPropertyRelative(nameof(Styler.FontItem.fontAsset)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Styler.FontItem.providerFonts)).ClearArray();
        }

        static void InitializeTextStyleItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.TextStyle, $"Text Style {GetItemNumber(item)}");
            item.FindPropertyRelative(nameof(Styler.TextStyleItem.flags)).intValue = (int)Styler.TextStyleFlags.None;
            item.FindPropertyRelative("fontSize").floatValue = 36f;
            item.FindPropertyRelative("enableAutoSizing").boolValue = false;
            item.FindPropertyRelative("fontSizeMin").floatValue = 18f;
            item.FindPropertyRelative("fontSizeMax").floatValue = 72f;
            item.FindPropertyRelative("characterWidthAdjustment").floatValue = 0f;
            item.FindPropertyRelative("lineSpacingAdjustment").floatValue = 0f;
            item.FindPropertyRelative("alignment").intValue = (int)TMPro.TextAlignmentOptions.TopLeft;
            item.FindPropertyRelative("enableWordWrapping").boolValue = true;
            item.FindPropertyRelative("overflowMode").enumValueIndex = 0;
            item.FindPropertyRelative("characterSpacing").floatValue = 0f;
            item.FindPropertyRelative("wordSpacing").floatValue = 0f;
            item.FindPropertyRelative("lineSpacing").floatValue = 0f;
            item.FindPropertyRelative("paragraphSpacing").floatValue = 0f;
            item.FindPropertyRelative("margin").vector4Value = Vector4.zero;
            item.FindPropertyRelative("fontStyle").enumValueIndex = 0;
        }

        static void InitializeGradientItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.Gradient, $"Gradient {GetItemNumber(item)}");
            item.FindPropertyRelative("gradientValue").gradientValue = new Gradient();
        }

        static void InitializeSpriteItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.Sprite, "New Sprite");
            item.FindPropertyRelative("spriteAsset").objectReferenceValue = null;
            item.FindPropertyRelative("applyImageSettings").boolValue = false;
            item.FindPropertyRelative("imageType").enumValueIndex = (int)UnityEngine.UI.Image.Type.Simple;
            item.FindPropertyRelative("preserveAspect").boolValue = false;
            item.FindPropertyRelative("pixelsPerUnitMultiplier").floatValue = 1f;
            item.FindPropertyRelative("fillMethod").enumValueIndex = (int)UnityEngine.UI.Image.FillMethod.Radial360;
            item.FindPropertyRelative("fillOrigin").intValue = 0;
            item.FindPropertyRelative("fillAmount").floatValue = 1f;
            item.FindPropertyRelative("fillClockwise").boolValue = true;
        }

        static void InitializeShapeStyleItem(SerializedProperty item)
        {
            InitializeItem(item, Styler.ItemType.ShapeStyle, $"Shape Style {GetItemNumber(item)}");

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.flags)).intValue = (int)Styler.ShapeStyleFlags.None;

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.sprite)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.scaleMode)).enumValueIndex = (int)ProceduralShape.ScaleMode.Cover;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.softness)).floatValue = 0f;

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shape)).enumValueIndex = (int)ProceduralShape.ShapeType.Rectangle;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.polygonSides)).intValue = 6;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shapeAngle)).floatValue = 0f;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.shapeSkew)).vector2Value = Vector2.zero;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.radiusMode)).enumValueIndex = (int)ProceduralShape.RadiusMode.Pixels;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.independentCorners)).boolValue = false;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.squircleCorners)).boolValue = false;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.cornerRadius)).vector4Value = Vector4.zero;

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.fillCenter)).boolValue = true;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outlineWidth)).floatValue = 0f;

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowOffset)).vector2Value = Vector2.zero;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowSize)).floatValue = 0f;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.innerShadowSoftness)).floatValue = 0f;

            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowOffset)).vector2Value = Vector2.zero;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowSize)).floatValue = 0f;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowSoftness)).floatValue = 0f;
            item.FindPropertyRelative(nameof(Styler.ShapeStyleItem.outerShadowOutsideOnly)).boolValue = false;
        }

        static void InitializeItem(SerializedProperty item, Styler.ItemType itemType, string id)
        {
            item.FindPropertyRelative(nameof(Styler.Item.itemID)).stringValue = id;
            item.FindPropertyRelative(nameof(Styler.Item.formerIDs)).ClearArray();
            item.isExpanded = true;

            // Renaming a new item keeps no former ID until an object picks it
            string path = item.propertyPath;
            SerializedProperty items = item.serializedObject.FindProperty(path[..path.LastIndexOf(".Array.data[", StringComparison.Ordinal)]);

            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty other = items.GetArrayElementAtIndex(i);
                if (other.propertyPath != path && other.FindPropertyRelative(nameof(Styler.Item.itemID)).stringValue == id)
                    return;
            }

            StylerIDMigration.MarkNew((StylerPreset)item.serializedObject.targetObject, itemType, id);
        }

        static int GetItemNumber(SerializedProperty item)
        {
            string path = item.propertyPath;
            int closeBracket = path.LastIndexOf(']');
            int openBracket = closeBracket >= 0 ? path.LastIndexOf('[', closeBracket) : -1;

            if (openBracket >= 0 && int.TryParse(path[(openBracket + 1)..closeBracket], out int index))
                return index + 1;

            return 1;
        }

        static string[] GetFillOriginOptions(UnityEngine.UI.Image.FillMethod method)
        {
            return method switch
            {
                UnityEngine.UI.Image.FillMethod.Horizontal => new[] { "Left", "Right" },
                UnityEngine.UI.Image.FillMethod.Vertical => new[] { "Bottom", "Top" },
                UnityEngine.UI.Image.FillMethod.Radial90 => new[] { "BottomLeft", "TopLeft", "TopRight", "BottomRight" },
                UnityEngine.UI.Image.FillMethod.Radial180 => new[] { "Bottom", "Left", "Top", "Right" },
                UnityEngine.UI.Image.FillMethod.Radial360 => new[] { "Bottom", "Right", "Top", "Left" },
                _ => new[] { "Bottom" }
            };
        }
        #endregion

        #region Default Preset Management
        void DrawSetDefault()
        {
            string btnText = isDefaultPreset ? "Currently Default" : "Set as Default Preset";
            ButtonOptions options = ButtonOptions
                .WithTooltip("Sets this preset as the global default. Preset must be in a Resources folder.")
                .WithHeight(28).WithIconSize(11).WithInvertedColor(isDefaultPreset);

            using (new EditorGUI.DisabledScope(isDefaultPreset))
            {
                if (EvoEditorGUI.Button(btnText, isDefaultPreset ? "UI_DefaultStylerCheck" : null, options))
                    SetAsDefault();
            }
        }

        void SetAsDefault()
        {
            string assetPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(assetPath);

            // Validate Resources path
            if (resourcePath == null)
            {
                EditorUtility.DisplayDialog("Invalid Location", "To set this as the default preset, " +
                    "it must be located inside a 'Resources' folder.", "OK");
                return;
            }

            // Determine target path based on Styler.cs location
            string stylerScriptPath = FindStylerScriptPath();
            if (string.IsNullOrEmpty(stylerScriptPath))
            {
                EditorUtility.DisplayDialog("Error", "Could not locate 'Styler.cs' to determine config save location.", "OK");
                return;
            }

            // Find "Scripts" and strip it to get the root "Evo UI" folder
            // Path: .../Evo UI/Scripts/Styler.cs
            string evoUiRoot;
            int scriptsIndex = stylerScriptPath.LastIndexOf("/Scripts/");
            if (scriptsIndex != -1)
            {
                // Take everything before "/Scripts/"
                evoUiRoot = stylerScriptPath[..scriptsIndex];
            }
            else
            {
                // Fallback: If not in a "Scripts" folder, assume Styler.cs is in the root or deeper custom structure.
                // We'll just go up one level from the file to be safe.
                evoUiRoot = Path.GetDirectoryName(stylerScriptPath);
            }

            // Construct Resources path
            string resourcesDir = Path.Combine(evoUiRoot, "Resources");
            if (!Directory.Exists(resourcesDir))
                Directory.CreateDirectory(resourcesDir);

            // Construct Config path using the Constant constant: "Styler Presets/Config"
            // This ensures we save it exactly where Styler.cs looks for it
            string fullPath = Path.Combine(resourcesDir, Constants.StylerConfigPath + ".txt");
            fullPath = fullPath.Replace('\\', '/'); // Normalize path separators for Unity

            // Ensure subdirectories exist (e.g. "Styler Presets")
            string configDir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            // Write Config
            try
            {
                File.WriteAllText(fullPath, resourcePath);
                AssetDatabase.Refresh();
                Styler.GetDefaultPreset(false, true);
                CheckDefaultStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Styler] Failed to save config file: {e.Message}");
            }
        }

        void CheckDefaultStatus()
        {
            TextAsset config = Resources.Load<TextAsset>(Constants.StylerConfigPath);
            if (config == null)
            {
                isDefaultPreset = false;
                return;
            }

            string path = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(path);

            // Trim to handle potential whitespace or line endings in the text file
            isDefaultPreset = config.text.Trim() == resourcePath;
        }

        void CheckFallbackStatus()
        {
            string currentPath = AssetDatabase.GetAssetPath(spTarget);
            string resourcePath = GetResourcePath(currentPath);
            isFallbackPreset = !string.IsNullOrEmpty(resourcePath) && resourcePath.Replace('\\', '/') == Constants.StylerFallbackPath;
        }

        string GetResourcePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            // Need the path relative to Resources for the config file content
            int resourcesIndex = assetPath.LastIndexOf("/Resources/");
            if (resourcesIndex == -1)
                return null;

            string relativePath = assetPath[(resourcesIndex + 11)..]; // Length of "/Resources/"
            int extensionIndex = relativePath.LastIndexOf(".");
            if (extensionIndex != -1)
                relativePath = relativePath[..extensionIndex];

            return relativePath;
        }

        string FindStylerScriptPath()
        {
            string[] guids = AssetDatabase.FindAssets("Styler t:Script");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "Styler.cs")
                    return path;
            }
            return null;
        }
        #endregion
    }
}