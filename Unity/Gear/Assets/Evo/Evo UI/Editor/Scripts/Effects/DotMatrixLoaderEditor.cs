using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(DotMatrixLoader)), CanEditMultipleObjects]
    public class DotMatrixLoaderEditor : ImageEditorBase
    {
        SerializedProperty pattern;
        SerializedProperty dotShape;
        SerializedProperty gridSize;
        SerializedProperty dotSize;
        SerializedProperty inactiveAlpha;
        SerializedProperty inactiveScale;
        SerializedProperty edgeSoftness;
        SerializedProperty speed;
        SerializedProperty phaseOffset;
        SerializedProperty trailLength;


        protected override void OnEnable()
        {
            base.OnEnable();

            pattern = serializedObject.FindProperty("pattern");
            dotShape = serializedObject.FindProperty("dotShape");
            gridSize = serializedObject.FindProperty("gridSize");
            dotSize = serializedObject.FindProperty("dotSize");
            inactiveAlpha = serializedObject.FindProperty("inactiveAlpha");
            inactiveScale = serializedObject.FindProperty("inactiveScale");
            edgeSoftness = serializedObject.FindProperty("edgeSoftness");
            speed = serializedObject.FindProperty("speed");
            phaseOffset = serializedObject.FindProperty("phaseOffset");
            trailLength = serializedObject.FindProperty("trailLength");
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
                        EvoEditorGUI.Callout("Loader settings use the default material. " +
                            "Clear Material to edit them for all selected images.");
                    }

                    using (new EditorGUI.DisabledScope(m_Material.hasMultipleDifferentValues || m_Material.objectReferenceValue != null))
                    {
                        EvoEditorGUI.Property(pattern, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(dotShape, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(gridSize, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(dotSize, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(inactiveAlpha, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(inactiveScale, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(edgeSoftness, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(speed, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(phaseOffset, FieldOptions.WithInvertedColor());

                        using (new EditorGUI.DisabledScope(!pattern.hasMultipleDifferentValues &&
                            pattern.enumValueIndex == (int)DotMatrixLoader.PatternType.Equalizer))
                        {
                            EvoEditorGUI.Property(trailLength, FieldOptions.WithInvertedColor().WithoutSpace());
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}