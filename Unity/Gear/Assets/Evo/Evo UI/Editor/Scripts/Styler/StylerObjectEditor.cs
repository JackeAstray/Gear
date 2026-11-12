using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(StylerObject))]
    public class StylerObjectEditor : UnityEditor.Editor
    {
        // Properties
        SerializedProperty useDefaultStylerPreset;
        SerializedProperty preset;
        SerializedProperty targetGraphic;
        SerializedProperty targetText;
        SerializedProperty targetGradient;
        SerializedProperty objectType;
        SerializedProperty colorID;
        SerializedProperty fontID;
        SerializedProperty textStyleID;
        SerializedProperty spriteID;
        SerializedProperty gradientID;
        SerializedProperty useCustomColor;
        SerializedProperty overrideAlpha;
        SerializedProperty alphaOverride;

        // Interaction
        SerializedProperty enableInteraction;
        SerializedProperty interactableObject;

        // Color Interaction
        SerializedProperty enableColorInteraction;
        SerializedProperty disabledColor;
        SerializedProperty normalColor;
        SerializedProperty highlightedColor;
        SerializedProperty pressedColor;
        SerializedProperty selectedColor;

        // Font Interaction
        SerializedProperty enableFontInteraction;
        SerializedProperty disabledFont;
        SerializedProperty normalFont;
        SerializedProperty highlightedFont;
        SerializedProperty pressedFont;
        SerializedProperty selectedFont;

        void OnEnable()
        {
            useDefaultStylerPreset =serializedObject.FindProperty("useDefaultStylerPreset");
            preset = serializedObject.FindProperty("preset");
            targetGraphic = serializedObject.FindProperty("targetGraphic");
            targetText = serializedObject.FindProperty("targetText");
            targetGradient = serializedObject.FindProperty("targetGradient");
            objectType = serializedObject.FindProperty("objectType");
            colorID = serializedObject.FindProperty("colorID");
            fontID = serializedObject.FindProperty("fontID");
            textStyleID = serializedObject.FindProperty("textStyleID");
            spriteID = serializedObject.FindProperty("spriteID");
            gradientID = serializedObject.FindProperty("gradientID");
            useCustomColor = serializedObject.FindProperty("useCustomColor");
            overrideAlpha = serializedObject.FindProperty("overrideAlpha");
            alphaOverride = serializedObject.FindProperty("alphaOverride");

            enableInteraction = serializedObject.FindProperty("enableInteraction");
            interactableObject = serializedObject.FindProperty("interactableObject");

            enableColorInteraction = serializedObject.FindProperty("enableColorInteraction");
            disabledColor = serializedObject.FindProperty("disabledColor");
            normalColor = serializedObject.FindProperty("normalColor");
            highlightedColor = serializedObject.FindProperty("highlightedColor");
            pressedColor = serializedObject.FindProperty("pressedColor");
            selectedColor = serializedObject.FindProperty("selectedColor");

            enableFontInteraction = serializedObject.FindProperty("enableFontInteraction");
            disabledFont = serializedObject.FindProperty("disabledFont");
            normalFont = serializedObject.FindProperty("normalFont");
            highlightedFont = serializedObject.FindProperty("highlightedFont");
            pressedFont = serializedObject.FindProperty("pressedFont");
            selectedFont = serializedObject.FindProperty("selectedFont");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();
          
            ReferencesFoldout();
            SettingsFoldout();
            InteractionSection();
          
            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", true, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        StylerEditor.DefaultPresetToggle(useDefaultStylerPreset, preset, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        StylerEditor.PresetField(useDefaultStylerPreset, preset, options: FieldOptions.WithoutSpace());
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(objectType, FieldOptions.WithoutSpace().WithoutBackground());
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        {
                            if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Graphic)
                                EvoEditorGUI.Property(targetGraphic, FieldOptions.WithoutSpace());
                            else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Text)
                                EvoEditorGUI.Property(targetText, FieldOptions.WithoutSpace());
                            else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image)
                                EvoEditorGUI.Property(targetGraphic, "Target Image", FieldOptions.WithoutSpace());
                            else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient)
                                EvoEditorGUI.Property(targetGradient, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;
                    GUI.enabled = preset.objectReferenceValue;

                    // Draw fields
                    if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Text)
                    {
                        StylerEditor.FontDropdown(preset, fontID, "Font ID", invertColor: true);
                        StylerEditor.TextStyleDropdown(preset, textStyleID, "Text Style ID", invertColor: true);
                    }
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image)
                    {
                        StylerEditor.SpriteDropdown(preset, spriteID, "Sprite ID", invertColor: true);
                    }
                    else if (isGradient)
                    {
                        StylerEditor.GradientDropdown(preset, gradientID, "Gradient ID", addSpace: false, invertColor: true);
                    }

                    GUI.enabled = !useCustomColor.boolValue && !enableInteraction.boolValue;

                    // Hide color-specific settings entirely if working with a gradient
                    if (!isGradient)
                    {
                        StylerEditor.ColorDropdown(preset, colorID, overrideAlpha, alphaOverride, "Color ID");
                        GUI.enabled = true;
                        EvoEditorGUI.Toggle(useCustomColor, FieldOptions.WithoutSpace().WithInvertedColor());
                    }
                    else
                    {
                        // Reset GUI enabled state if skipping color fields
                        GUI.enabled = true;
                    }

                    // Info Boxes
                    if (!preset.objectReferenceValue && (!useCustomColor.boolValue || isGradient))
                        EvoEditorGUI.Callout("No preset assigned. Please assign a valid Styler Preset to use the Styler system.", 
                            invertColor: true, spaceAfter: 0);
                    else if (enableInteraction.boolValue && interactableObject.objectReferenceValue && !isGradient)
                        EvoEditorGUI.Callout("Interaction is enabled; visual states will be handled by the interaction system.",
                            invertColor: true, spaceAfter: 0);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void InteractionSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Interaction", true, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;
                    if (isGradient)
                    {
                        EvoEditorGUI.Callout("Interaction is currently not supported for the Gradient type.", invertColor: true, spaceBefore: 0);
                        GUI.enabled = false;
                    }

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableInteraction, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableInteraction.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(interactableObject, "Target Object", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();

                    if (enableInteraction.boolValue && interactableObject.objectReferenceValue)
                    {
                        EvoEditorGUI.AddPropertySpace();

                        // Set Color
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            EvoEditorGUI.Toggle(enableColorInteraction, "Set Color", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                            if (enableColorInteraction.boolValue)
                            {
                                EvoEditorGUI.BeginContainer(nestDepth: 1);
                                StylerEditor.InteractionColors(preset, !useCustomColor.boolValue,
                                    disabledColor, normalColor, highlightedColor, pressedColor, selectedColor);
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndColumn();

                        // Set Font
                        bool isText = objectType.enumValueIndex == (int)StylerObject.ObjectType.Text;
                        if (isText)
                        {
                            EvoEditorGUI.AddPropertySpace();
                            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                            {
                                EvoEditorGUI.Toggle(enableFontInteraction, "Set Font", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                                if (enableFontInteraction.boolValue)
                                {
                                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                                    InteractionFonts();
                                    EvoEditorGUI.EndContainer();
                                }
                            }
                            EvoEditorGUI.EndColumn();
                        }
                    }

                    if (isGradient)
                        GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void InteractionFonts()
        {
            // Array of FontMapping properties matching InteractionState enum order
            SerializedProperty[] fontMappings = new[]
            {
                disabledFont,
                normalFont,
                highlightedFont,
                pressedFont,
                selectedFont
            };

            // Draw each state's font mapping
            string[] stateNames = InteractiveEditor.InteractionStateIDs;
            for (int i = 0; i < stateNames.Length; i++)
            {
                SerializedProperty mapping = fontMappings[i];
                SerializedProperty font = mapping.FindPropertyRelative("font");
                SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                bool isLastItem = i >= stateNames.Length - 1;

                // If preset is assigned, use dropdown
                if (preset.objectReferenceValue != null)
                    StylerEditor.FontDropdown(preset, stylerID, stateNames[i], !isLastItem);
                else if (isLastItem)
                    EvoEditorGUI.Property(font, stateNames[i], FieldOptions.WithoutSpace());
                else
                    EvoEditorGUI.Property(font, stateNames[i]);
            }
        }
    }
}