using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ProgressBar)), CanEditMultipleObjects]
    public class ProgressBarEditor : UnityEditor.Editor
    {
        // Object
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;

        // Settings
        SerializedProperty invertFill;
        SerializedProperty enableSmoothing;
        SerializedProperty smoothingDuration;
        SerializedProperty smoothingCurve;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // References
        SerializedProperty fillRect;
        SerializedProperty valueText;

        // Events
        SerializedProperty onValueChanged;

        void OnEnable()
        {
            value = serializedObject.FindProperty("value");
            minValue = serializedObject.FindProperty("minValue");
            maxValue = serializedObject.FindProperty("maxValue");

            invertFill = serializedObject.FindProperty("invertFill");
            enableSmoothing = serializedObject.FindProperty("enableSmoothing");
            smoothingDuration = serializedObject.FindProperty("smoothingDuration");
            smoothingCurve = serializedObject.FindProperty("smoothingCurve");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            fillRect = serializedObject.FindProperty("fillRect");
            valueText = serializedObject.FindProperty("valueText");

            onValueChanged = serializedObject.FindProperty("onValueChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ObjectFoldout();
            SettingsFoldout();
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
                            var progressBar = (ProgressBar)obj;
                            progressBar.SetValueInstant(progressBar.Value);
                        }

                        serializedObject.Update();
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
                    EvoEditorGUI.Toggle(invertFill, FieldOptions.WithInvertedColor());

                    if (valueText.objectReferenceValue != null || valueText.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Text");
                        {
                            EvoEditorGUI.Property(textFormat);
                            EvoEditorGUI.Property(displayMultiplier);
                            EvoEditorGUI.Property(displayFormat, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();
                        EvoEditorGUI.AddLayoutSpace();
                    }

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Animation");
                    {
                        EvoEditorGUI.Toggle(enableSmoothing, "Smoothing", FieldOptions.WithoutSpace());
                        if (enableSmoothing.boolValue || enableSmoothing.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.Property(smoothingDuration, "Duration");
                            EvoEditorGUI.Property(smoothingCurve, "Curve", FieldOptions.WithoutSpace());
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
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
                    EvoEditorGUI.Property(fillRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(valueText, FieldOptions.WithInvertedColor().WithoutSpace());
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