using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Interactive))]
    public class InteractiveEditor : SelectableEditor
    {
        // Settings
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onClick;
        SerializedProperty onPointerEnter;
        SerializedProperty onPointerExit;

        internal static readonly string[] InteractionStateIDs =
        {
            "Disabled",
            "Normal",
            "Highlighted",
            "Pressed",
            "Selected"
        };

        static readonly string[] ScaleMultiplierFields =
        {
            nameof(Interactive.ScaleEffect.disabled),
            nameof(Interactive.ScaleEffect.normal),
            nameof(Interactive.ScaleEffect.highlighted),
            nameof(Interactive.ScaleEffect.pressed),
            nameof(Interactive.ScaleEffect.selected)
        };

        protected override void OnEnable()
        {
            InitializeEditorState();
            UINavigationEditor.PrepareForVisualize(this);
        }

        void InitializeEditorState()
        {
            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty(nameof(resolveRaycast));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            interactionState = serializedObject.FindProperty(nameof(interactionState));

            navigation = serializedObject.FindProperty("m_Navigation");

            onClick = serializedObject.FindProperty(nameof(onClick));
            onPointerEnter = serializedObject.FindProperty(nameof(onPointerEnter));
            onPointerExit = serializedObject.FindProperty(nameof(onPointerExit));
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            EffectsFoldout(serializedObject);
            NavigationFoldout(navigation);
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(interactionState, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(onClick, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onPointerEnter, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onPointerExit, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        #region Shared Methods
        internal static void EffectsFoldout(SerializedObject serializedObj)
        {
            if (EvoEditorGUI.BeginFoldout(serializedObj.targetObject, "Effects", false, FoldoutOptions.WithIcon("UI_Effect")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        var styleProvider = serializedObj.FindProperty(nameof(Interactive.styleProvider));
                        var useDefaultStylerPreset = serializedObj.FindProperty(nameof(Interactive.useDefaultStylerPreset));
                        var stylerPreset = serializedObj.FindProperty(nameof(Interactive.stylerPreset));
                      
                        EvoEditorGUI.Property(styleProvider, FieldOptions.WithoutBackground().WithoutSpace());
                        if (styleProvider.enumValueIndex == (int)StyleProvider.Styler)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            StylerEditor.PresetFields(useDefaultStylerPreset, stylerPreset);
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddPropertySpace();
                    DrawScaleEffect(serializedObj);
                    EvoEditorGUI.AddPropertySpace();
                    DrawSoundEffects(serializedObj);
                    EvoEditorGUI.AddPropertySpace();
                    DrawPointerEffects(serializedObj);
                    EvoEditorGUI.AddPropertySpace();
                    DrawCursorSettings(serializedObj);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        internal static void NavigationFoldout(SerializedProperty navigationProperty)
        {
            if (EvoEditorGUI.BeginFoldout(navigationProperty.serializedObject.targetObject, "Navigation", false, FoldoutOptions.WithIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(navigationProperty, "Navigation Mode", FieldOptions.WithoutSpace().WithoutBackground());
                    UINavigationEditor.DrawVisualizeButton();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        internal static void DrawPointerEffects(SerializedObject obj)
        {
            RippleEditor.DrawRippleSettings(obj.FindProperty(nameof(Interactive.ripple)));
            EvoEditorGUI.AddPropertySpace();
            SpotlightEditor.DrawSpotlightSettings(obj.FindProperty(nameof(Interactive.spotlight)));
            EvoEditorGUI.AddPropertySpace();
            BorderGlowEditor.DrawBorderGlowSettings(obj.FindProperty(nameof(Interactive.borderGlow)));
        }

        internal static void DrawScaleEffect(SerializedObject obj)
        {
            var scaleEffect = obj.FindProperty(nameof(Interactive.scaleEffect));
            var enabled = scaleEffect.FindPropertyRelative(nameof(Interactive.ScaleEffect.enabled));

            if (EvoEditorGUI.BeginToggleFoldout(enabled, "Scale", options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                var ease = scaleEffect.FindPropertyRelative(nameof(Interactive.ScaleEffect.ease));
                bool showCurve = ease.hasMultipleDifferentValues || ease.intValue == (int)Ease.Custom;

                EvoEditorGUI.BeginContainer(nestDepth: 1);
                EvoEditorGUI.Property(scaleEffect.FindPropertyRelative(nameof(Interactive.ScaleEffect.target)));
                EvoEditorGUI.Property(scaleEffect.FindPropertyRelative(nameof(Interactive.ScaleEffect.duration)));
                EvoEditorGUI.Property(ease, FieldOptions.WithSpace(showCurve));

                if (showCurve)
                    EvoEditorGUI.Property(scaleEffect.FindPropertyRelative(nameof(Interactive.ScaleEffect.curve)), FieldOptions.WithoutSpace());

                EvoEditorGUI.AddLayoutSpace();
                EvoEditorGUI.BeginColumn();
                EvoEditorGUI.BeginContainer("Multipliers");

                for (int i = 0; i < ScaleMultiplierFields.Length; i++)
                {
                    bool isLast = i == ScaleMultiplierFields.Length - 1;
                    EvoEditorGUI.Property(scaleEffect.FindPropertyRelative(ScaleMultiplierFields[i]), FieldOptions.WithInvertedColor().WithSpace(!isLast));
                }

                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndColumn();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndToggleFoldout(false);
        }

        internal static void DrawSoundEffects(SerializedObject obj)
        {
            var styleProvider = obj.FindProperty(nameof(Interactive.styleProvider));
            var stylerPreset = obj.FindProperty(nameof(Interactive.stylerPreset));
            var soundEffects = obj.FindProperty(nameof(Interactive.soundEffects));
            string[] sfxFields = { "highlightedSFX", "pressedSFX", "selectedSFX" };

            if (EvoEditorGUI.BeginToggleFoldout(soundEffects, options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    for (int i = 0; i < sfxFields.Length; i++)
                    {
                        SerializedProperty mapping = obj.FindProperty(sfxFields[i]);
                        bool isLast = i == sfxFields.Length - 1;

                        if (styleProvider.enumValueIndex == (int)StyleProvider.Styler &&
                            !stylerPreset.hasMultipleDifferentValues && stylerPreset.objectReferenceValue != null)
                        {
                            SerializedProperty stylerID = mapping.FindPropertyRelative(nameof(AudioMapping.stylerID));
                            StylerEditor.AudioDropdown(stylerPreset, stylerID, mapping.displayName, !isLast, addBackground: true);
                        }
                        else
                        {
                            SerializedProperty audioClip = mapping.FindPropertyRelative(nameof(AudioMapping.audioClip));
                            EvoEditorGUI.Property(audioClip, mapping.displayName, FieldOptions.WithSpace(!isLast));
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }

        internal static void DrawCursorSettings(SerializedObject obj)
        {
            var cursorStates = obj.FindProperty(nameof(Interactive.cursorStates));
            if (EvoEditorGUI.BeginFoldout(obj.targetObject, cursorStates.propertyPath, "Cursor States", false,
                FoldoutOptions.WithInvertedColor().WithoutBoldText(), compact: true))
            {
                CursorStates visibleStates = (CursorStates)cursorStates.intValue;
                if (cursorStates.hasMultipleDifferentValues)
                {
                    // Show the union of enabled states without changing any target's mask
                    foreach (Object cursorTarget in obj.targetObjects)
                        visibleStates |= ((Interactive)cursorTarget).cursorStates;
                }

                EvoEditorGUI.BeginContainer(nestDepth: 1);
                DrawCursorStates(cursorStates, visibleStates != CursorStates.None);
                bool firstState = true;
                DrawCursorState(obj, visibleStates, CursorStates.Disabled, nameof(Interactive.disabledCursor), "Disabled", ref firstState);
                DrawCursorState(obj, visibleStates, CursorStates.Normal, nameof(Interactive.normalCursor), "Normal", ref firstState);
                DrawCursorState(obj, visibleStates, CursorStates.Highlighted, nameof(Interactive.highlightedCursor), "Highlighted", ref firstState);
                DrawCursorState(obj, visibleStates, CursorStates.Pressed, nameof(Interactive.pressedCursor), "Pressed", ref firstState);
                DrawCursorState(obj, visibleStates, CursorStates.Selected, nameof(Interactive.selectedCursor), "Selected", ref firstState);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
        #endregion

        #region Cursor Helpers
        static void DrawCursorStates(SerializedProperty property, bool addSpace)
        {
            EvoEditorGUI.BeginRow();
            GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);

            Rect popupRect = EditorGUILayout.GetControlRect();
            GUIContent content = new("States", property.tooltip);
            bool changed;
            using (var propertyScope = new EditorGUI.PropertyScope(popupRect, content, property))
            {
                popupRect = EditorGUI.PrefixLabel(popupRect, propertyScope.content);
                bool previousMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var value = (CursorStates)EditorGUI.EnumFlagsField(popupRect, (CursorStates)property.intValue);
                changed = EditorGUI.EndChangeCheck();
                if (changed)
                    property.intValue = (int)value;
                EditorGUI.showMixedValue = previousMixedValue;
            }

            EvoEditorGUI.EndRow(addSpace ? LayoutOptions.WithSpace() : default);

            if (changed)
            {
                // Save before the visible state containers change and rebuild the layout next pass
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        static void DrawCursorState(SerializedObject obj, CursorStates visibleStates, CursorStates state,
            string propertyName, string title, ref bool firstState)
        {
            if ((visibleStates & state) == 0)
                return;

            if (!firstState)
                EvoEditorGUI.AddLayoutSpace();
            firstState = false;

            var settings = obj.FindProperty(propertyName);
            var texture = settings.FindPropertyRelative(nameof(CursorSettings.texture));
            var hotspot = settings.FindPropertyRelative(nameof(CursorSettings.hotspot));

            EvoEditorGUI.BeginColumn();
            EvoEditorGUI.BeginContainer(title);
            EvoEditorGUI.Property(texture, "Texture", FieldOptions.WithInvertedColor());
            using (new EditorGUI.DisabledScope(!texture.hasMultipleDifferentValues && texture.objectReferenceValue == null))
            {
                EvoEditorGUI.Property(hotspot, "Hotspot", FieldOptions.WithInvertedColor().WithoutSpace());

                if (!texture.hasMultipleDifferentValues && texture.objectReferenceValue is Texture2D tex)
                    DrawCursorTextureValidation(tex);

                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndColumn();
            }

            static void DrawCursorTextureValidation(Texture2D texture)
            {
                string assetPath = AssetDatabase.GetAssetPath(texture);
                var importer = string.IsNullOrEmpty(assetPath) ? null : AssetImporter.GetAtPath(assetPath) as TextureImporter;
                bool needsFix = !texture.isReadable || texture.mipmapCount > 1 ||
                    texture.format != TextureFormat.RGBA32 || !texture.alphaIsTransparency;

                if (!needsFix)
                    return;

                EvoEditorGUI.Callout("Cursor textures must be readable RGBA32, have Alpha Is Transparency enabled, " +
                    "and have no mipmaps.", CalloutType.Error, spaceBefore: 3);

                if (importer == null)
                    return;

                if (EvoEditorGUI.Button("Fix Cursor Texture Settings") && EditorUtility.DisplayDialog(
                    "Fix Cursor Texture", "This will enable Read/Write and Alpha Is Transparency, disable mipmaps, " +
                    "and use uncompressed RGBA32 for the default settings and existing platform overrides." +
                    "\n\nSprite textures keep their type; other textures use the Cursor type. The texture will be reimported.",
                    "Proceed", "Cancel"))
                {
                    FixCursorTextureSettings(importer);
                    GUIUtility.ExitGUI();
                }
            }

            static void FixCursorTextureSettings(TextureImporter importer)
            {
                if (importer.textureType != TextureImporterType.Sprite)
                    importer.textureType = TextureImporterType.Cursor;

                importer.isReadable = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;

                SetCursorPlatformSettings(importer, importer.GetDefaultPlatformTextureSettings());

                // Existing overrides take precedence over the default texture format
                foreach (BuildTargetGroup platform in System.Enum.GetValues(typeof(BuildTargetGroup)))
                {
                    if (platform == BuildTargetGroup.Unknown)
                        continue;

                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform.ToString());
                    if (settings.overridden)
                        SetCursorPlatformSettings(importer, settings);
                }

                importer.SaveAndReimport();
            }

            static void SetCursorPlatformSettings(TextureImporter importer, TextureImporterPlatformSettings settings)
            {
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                settings.allowsAlphaSplitting = false;
                importer.SetPlatformTextureSettings(settings);
            }
        #endregion
        }
    }
}