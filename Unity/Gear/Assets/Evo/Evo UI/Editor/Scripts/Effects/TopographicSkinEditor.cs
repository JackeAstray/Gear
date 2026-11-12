using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(TopographicSkin)), CanEditMultipleObjects]
    public class TopographicSkinEditor : ImageEditorBase
    {
        SerializedProperty scale;
        SerializedProperty density;
        SerializedProperty seed;
        SerializedProperty lineWidth;
        SerializedProperty majorLineInterval;
        SerializedProperty majorLineWidth;
        SerializedProperty edgeSoftness;
        SerializedProperty falloff;
        SerializedProperty falloffOrigin;
        SerializedProperty falloffStart;
        SerializedProperty falloffEnd;
        SerializedProperty falloffAspect;
        SerializedProperty falloffOutsideOpacity;
        SerializedProperty speed;


        protected override void OnEnable()
        {
            base.OnEnable();

            scale = serializedObject.FindProperty("scale");
            density = serializedObject.FindProperty("density");
            seed = serializedObject.FindProperty("seed");
            lineWidth = serializedObject.FindProperty("lineWidth");
            majorLineInterval = serializedObject.FindProperty("majorLineInterval");
            majorLineWidth = serializedObject.FindProperty("majorLineWidth");
            edgeSoftness = serializedObject.FindProperty("edgeSoftness");
            falloff = serializedObject.FindProperty("falloff");
            falloffOrigin = serializedObject.FindProperty("falloffOrigin");
            falloffStart = serializedObject.FindProperty("falloffStart");
            falloffEnd = serializedObject.FindProperty("falloffEnd");
            falloffAspect = serializedObject.FindProperty("falloffAspect");
            falloffOutsideOpacity = serializedObject.FindProperty("falloffOutsideOpacity");
            speed = serializedObject.FindProperty("speed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            GraphicFoldout();
            bool customMaterial = m_Material.hasMultipleDifferentValues || m_Material.objectReferenceValue != null;
            if (customMaterial)
                EvoEditorGUI.Callout("Topographic settings use the default material. Clear Material to edit them for all selected images.");

            using (new EditorGUI.DisabledScope(customMaterial))
            {
                PatternFoldout();
                LinesFoldout();
                AnimationFoldout();
            }

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void PatternFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Pattern", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(scale, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(density, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(seed, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void LinesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Lines", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(lineWidth, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(majorLineInterval, "Major Line Every", FieldOptions.WithInvertedColor());
                if (majorLineInterval.intValue > 0 || majorLineInterval.hasMultipleDifferentValues)
                    EvoEditorGUI.Property(majorLineWidth, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(edgeSoftness, FieldOptions.WithInvertedColor());
                FalloffSettings();
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void FalloffSettings()
        {
            if (EvoEditorGUI.BeginToggleFoldout(falloff, options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                EvoEditorGUI.Property(falloffOrigin, "Origin");
                EvoEditorGUI.Property(falloffStart, "Start Distance");
                EvoEditorGUI.Property(falloffEnd, "End Distance");
                EvoEditorGUI.Property(falloffAspect, "Aspect");
                EvoEditorGUI.Property(falloffOutsideOpacity, "Outside Opacity", FieldOptions.WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animation", true, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(speed, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}
