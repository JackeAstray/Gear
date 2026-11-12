using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(RadialSlider)), CanEditMultipleObjects]
    public class RadialSliderEditor : SelectableEditor
    {
        // Object
        SerializedProperty wholeNumbers;
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;

        // Settings
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty clockwise;
        SerializedProperty continuousDrag;
        SerializedProperty angleRange;
        SerializedProperty startAngle;
        SerializedProperty handleOffset;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty fillImage;
        SerializedProperty handleRect;
        SerializedProperty valueText;
        SerializedProperty valueInput;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            wholeNumbers = serializedObject.FindProperty("wholeNumbers");
            value = serializedObject.FindProperty("value");
            minValue = serializedObject.FindProperty("minValue");
            maxValue = serializedObject.FindProperty("maxValue");

            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty("resolveRaycast");
            continuousDrag = serializedObject.FindProperty("continuousDrag");
            clockwise = serializedObject.FindProperty("clockwise");
            angleRange = serializedObject.FindProperty("angleRange");
            startAngle = serializedObject.FindProperty("startAngle");
            handleOffset = serializedObject.FindProperty("handleOffset");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            navigation = serializedObject.FindProperty("m_Navigation");

            fillImage = serializedObject.FindProperty("fillImage");
            handleRect = serializedObject.FindProperty("handleRect");
            valueText = serializedObject.FindProperty("valueText");
            valueInput = serializedObject.FindProperty("valueInput");

            onValueChanged = serializedObject.FindProperty("onValueChanged");

            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ObjectFoldout();
            SettingsFoldout();
            InteractiveEditor.EffectsFoldout(serializedObject);
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void ObjectFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Object", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EditorGUI.BeginChangeCheck();
                    EvoEditorGUI.Slider(value, minValue.floatValue, maxValue.floatValue, "Value", FieldOptions.WithInvertedColor());
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();

                        foreach (var obj in targets)
                        {
                            var slider = (RadialSlider)obj;
                            slider.Refresh();
                            slider.onValueChanged?.Invoke(slider.Value);
                        }
                    }

                    EvoEditorGUI.Property(minValue, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(maxValue, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(continuousDrag, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(clockwise, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(wholeNumbers, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(angleRange, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(startAngle, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(handleOffset, FieldOptions.WithInvertedColor().WithoutSpace());

                    if (valueText.objectReferenceValue != null || valueInput.objectReferenceValue != null ||
                        valueText.hasMultipleDifferentValues || valueInput.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Text & Input");
                        {
                            EvoEditorGUI.Property(textFormat);
                            EvoEditorGUI.Property(displayMultiplier);
                            EvoEditorGUI.Property(displayFormat, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();
                    }
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
                    EvoEditorGUI.Property(fillImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(handleRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(valueText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(valueInput, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}