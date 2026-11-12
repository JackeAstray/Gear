using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(StrokeDash)), CanEditMultipleObjects]
    public class StrokeDashEditor : UnityEditor.Editor
    {
        SerializedProperty color;
        SerializedProperty raycastTarget;
        SerializedProperty raycastPadding;
        SerializedProperty maskable;
        SerializedProperty thickness;
        SerializedProperty borderRadius;
        SerializedProperty dashLength;
        SerializedProperty gapLength;
        SerializedProperty speed;


        void OnEnable()
        {
            color = serializedObject.FindProperty("m_Color");
            raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            maskable = serializedObject.FindProperty("m_Maskable");
            thickness = serializedObject.FindProperty(nameof(StrokeDash.thickness));
            borderRadius = serializedObject.FindProperty(nameof(StrokeDash.borderRadius));
            dashLength = serializedObject.FindProperty(nameof(StrokeDash.dashLength));
            gapLength = serializedObject.FindProperty(nameof(StrokeDash.gapLength));
            speed = serializedObject.FindProperty(nameof(StrokeDash.speed));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            GraphicFoldout();
            SettingsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void GraphicFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Graphic", true, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(color, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(maskable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(raycastTarget, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(raycastPadding, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(thickness, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(borderRadius, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dashLength, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(gapLength, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(speed, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}