using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(VirtualCursorOverride)), CanEditMultipleObjects]
    public class VirtualCursorOverrideEditor : UnityEditor.Editor
    {
        SerializedProperty overrideColor, color, overrideRadius, cornerRadius, overridePadding, targetPadding;
        SerializedProperty overrideOutlineWidth, outlineWidth, overrideCenterSize, centerSize, overrideEdgeSoftness, edgeSoftness;

        void OnEnable()
        {
            overrideColor = serializedObject.FindProperty(nameof(overrideColor));
            color = serializedObject.FindProperty(nameof(color));
            overrideRadius = serializedObject.FindProperty(nameof(overrideRadius));
            cornerRadius = serializedObject.FindProperty(nameof(cornerRadius));
            overridePadding = serializedObject.FindProperty(nameof(overridePadding));
            targetPadding = serializedObject.FindProperty(nameof(targetPadding));
            overrideOutlineWidth = serializedObject.FindProperty(nameof(overrideOutlineWidth));
            outlineWidth = serializedObject.FindProperty(nameof(outlineWidth));
            overrideCenterSize = serializedObject.FindProperty(nameof(overrideCenterSize));
            centerSize = serializedObject.FindProperty(nameof(centerSize));
            overrideEdgeSoftness = serializedObject.FindProperty(nameof(overrideEdgeSoftness));
            edgeSoftness = serializedObject.FindProperty(nameof(edgeSoftness));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            if (EvoEditorGUI.BeginFoldout(target, "Appearance", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    DrawOverride(overrideColor, color, "Color");
                    DrawOverride(overrideRadius, cornerRadius, "Corner Radius");
                    DrawOverride(overridePadding, targetPadding, "Target Padding");
                    DrawOverride(overrideOutlineWidth, outlineWidth, "Outline Width");
                    DrawOverride(overrideCenterSize, centerSize, "Center Size");
                    DrawOverride(overrideEdgeSoftness, edgeSoftness, "Edge Softness", false);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawOverride(SerializedProperty enabled, SerializedProperty value, string label, bool addSpace = true)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(enabled, label, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (enabled.hasMultipleDifferentValues || enabled.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.Property(value, "Value", FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }
    }
}