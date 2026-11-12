using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(WarningStripe)), CanEditMultipleObjects]
    public class WarningStripeEditor : ImageEditorBase
    {
        SerializedProperty lineWidth;
        SerializedProperty lineSpacing;
        SerializedProperty lineAngle;
        SerializedProperty scrollSpeed;
        SerializedProperty edgeSoftness;


        protected override void OnEnable()
        {
            base.OnEnable();

            lineWidth = serializedObject.FindProperty("lineWidth");
            lineSpacing = serializedObject.FindProperty("lineSpacing");
            lineAngle = serializedObject.FindProperty("lineAngle");
            scrollSpeed = serializedObject.FindProperty("scrollSpeed");
            edgeSoftness = serializedObject.FindProperty("edgeSoftness");
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

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (m_Material.hasMultipleDifferentValues || m_Material.objectReferenceValue != null)
                    {
                        EvoEditorGUI.Callout("Stripe settings use the default material. " +
                            "Clear Material to edit them for all selected images.");
                    }

                    using (new EditorGUI.DisabledScope(m_Material.hasMultipleDifferentValues || m_Material.objectReferenceValue != null))
                    {
                        EvoEditorGUI.Property(lineWidth, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(lineSpacing, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(lineAngle, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(edgeSoftness, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(scrollSpeed, FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}