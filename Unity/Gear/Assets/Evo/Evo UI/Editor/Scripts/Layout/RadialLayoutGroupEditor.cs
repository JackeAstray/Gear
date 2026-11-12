using UnityEngine;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(RadialLayoutGroup), true)]
    public class RadialLayoutGroupEditor : UnityEditor.Editor
    {
        SerializedProperty padding;
        SerializedProperty startAngle;
        SerializedProperty endAngle;
        SerializedProperty clockwise;
        SerializedProperty evenDistribution;
        SerializedProperty customSpacing;
        SerializedProperty faceCenter;
        SerializedProperty controlChildSize;
        SerializedProperty childSize;


        void OnEnable()
        {
            padding = serializedObject.FindProperty("m_Padding");
            startAngle = serializedObject.FindProperty("startAngle");
            endAngle = serializedObject.FindProperty("endAngle");
            clockwise = serializedObject.FindProperty("clockwise");
            evenDistribution = serializedObject.FindProperty("evenDistribution");
            customSpacing = serializedObject.FindProperty("customSpacing");
            faceCenter = serializedObject.FindProperty("faceCenter");
            controlChildSize = serializedObject.FindProperty("controlChildSize");
            childSize = serializedObject.FindProperty("childSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            LayoutFoldout();
            DistributionFoldout();
            PresetsSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void LayoutFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Layout", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.ArrayProperty(padding, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(faceCenter, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor().WithoutSpace());
                    {
                        EvoEditorGUI.Toggle(controlChildSize, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (controlChildSize.hasMultipleDifferentValues || controlChildSize.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(childSize, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DistributionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Distribution", true, FoldoutOptions.WithIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(clockwise, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(evenDistribution, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Slider(startAngle, 0f, GetStartAngleLimit(), FieldOptions.WithInvertedColor());
                    using (new EditorGUI.DisabledScope(!evenDistribution.hasMultipleDifferentValues && !evenDistribution.boolValue))
                        EvoEditorGUI.Slider(endAngle, GetEndAngleLimit(), 360f, FieldOptions.WithInvertedColor());
                    using (new EditorGUI.DisabledScope(!evenDistribution.hasMultipleDifferentValues && evenDistribution.boolValue))
                        EvoEditorGUI.Property(customSpacing, "Spacing (Degrees)", FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void PresetsSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Presets", true, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EditorGUILayout.BeginHorizontal();
                    if (EvoEditorGUI.Button("Full Circle", options: ButtonOptions.WithInvertedColor())) { ApplyPreset(360f); }
                    EvoEditorGUI.AddPropertySpace();
                    if (EvoEditorGUI.Button("Half Circle", options: ButtonOptions.WithInvertedColor())) { ApplyPreset(180f); }
                    EvoEditorGUI.AddPropertySpace();
                    if (EvoEditorGUI.Button("Quarter Circle", options: ButtonOptions.WithInvertedColor())) { ApplyPreset(90f); }
                    EditorGUILayout.EndHorizontal();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        float GetStartAngleLimit()
        {
            if (!endAngle.hasMultipleDifferentValues)
                return endAngle.floatValue;

            float limit = 360f;
            foreach (Object selectedTarget in targets)
                limit = Mathf.Min(limit, ((RadialLayoutGroup)selectedTarget).EndAngle);

            return limit;
        }

        float GetEndAngleLimit()
        {
            if (!startAngle.hasMultipleDifferentValues)
                return startAngle.floatValue;

            float limit = 0f;
            foreach (Object selectedTarget in targets)
                limit = Mathf.Max(limit, ((RadialLayoutGroup)selectedTarget).StartAngle);

            return limit;
        }

        void ApplyPreset(float sweep)
        {
            startAngle.floatValue = 0f;
            endAngle.floatValue = sweep;
            clockwise.boolValue = true;
            evenDistribution.boolValue = true;
        }
    }
}