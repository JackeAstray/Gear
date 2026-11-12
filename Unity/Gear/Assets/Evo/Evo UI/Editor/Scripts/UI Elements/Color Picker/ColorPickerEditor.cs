using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ColorPicker))]
    public class ColorPickerEditor : UnityEditor.Editor
    {
        // Settings
        SerializedProperty resetOnRightClick;
        SerializedProperty startColor;
        SerializedProperty pickerType;
        SerializedProperty wheelSize;

        // References
        SerializedProperty colorWheel;
        SerializedProperty colorPreview;
        SerializedProperty colorSelector;
        SerializedProperty hexInput;
        SerializedProperty hueSlider;
        SerializedProperty hueGradient;
        SerializedProperty saturationSlider;
        SerializedProperty saturationGradient;
        SerializedProperty brightnessSlider;
        SerializedProperty brightnessGradient;
        SerializedProperty opacitySlider;
        SerializedProperty opacityGradient;

        // Events
        SerializedProperty onColorChanged;

        void OnEnable()
        {
            resetOnRightClick = serializedObject.FindProperty(nameof(resetOnRightClick));
            startColor = serializedObject.FindProperty(nameof(startColor));
            pickerType = serializedObject.FindProperty(nameof(pickerType));
            wheelSize = serializedObject.FindProperty(nameof(wheelSize));

            colorWheel = serializedObject.FindProperty(nameof(colorWheel));
            colorPreview = serializedObject.FindProperty(nameof(colorPreview));
            colorSelector = serializedObject.FindProperty(nameof(colorSelector));
            hexInput = serializedObject.FindProperty(nameof(hexInput));
            hueSlider = serializedObject.FindProperty(nameof(hueSlider));
            hueGradient = serializedObject.FindProperty(nameof(hueGradient));
            saturationSlider = serializedObject.FindProperty(nameof(saturationSlider));
            saturationGradient = serializedObject.FindProperty(nameof(saturationGradient));
            brightnessSlider = serializedObject.FindProperty(nameof(brightnessSlider));
            brightnessGradient = serializedObject.FindProperty(nameof(brightnessGradient));
            opacitySlider = serializedObject.FindProperty(nameof(opacitySlider));
            opacityGradient = serializedObject.FindProperty(nameof(opacityGradient));

            onColorChanged = serializedObject.FindProperty(nameof(onColorChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            ReferencesFoldout();
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
                    EvoEditorGUI.Toggle(resetOnRightClick, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(startColor, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(pickerType, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(wheelSize, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(colorWheel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(colorPreview, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(colorSelector, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(hexInput, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(hueSlider, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(hueGradient, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(saturationSlider, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(saturationGradient, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(brightnessSlider, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(brightnessGradient, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(opacitySlider, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(opacityGradient, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onColorChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }

    /// <summary>
    /// Picks Procedural Shapes only. An Image Gradient from an older object stays shown until it is replaced.
    /// </summary>
    [CustomPropertyDrawer(typeof(GradientShapeAttribute))]
    public class GradientShapeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => TextDrawers.ObjectField(position, property, label, typeof(ProceduralShape), true);
    }
}