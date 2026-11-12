using UnityEditor;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Spinner)), CanEditMultipleObjects]
    public class SpinnerEditor : UnityEditor.Editor
    {
        // Settings
        SerializedProperty spinnerStyle;
        SerializedProperty rotationSpeed;
        SerializedProperty minFillAmount;
        SerializedProperty maxFillAmount;
        SerializedProperty ease;
        SerializedProperty customEase;

        // References
        SerializedProperty spinnerShape;
        SerializedProperty spinnerImage;

        void OnEnable()
        {
            spinnerStyle = serializedObject.FindProperty("spinnerStyle");
            rotationSpeed = serializedObject.FindProperty("rotationSpeed");
            minFillAmount = serializedObject.FindProperty("minFillAmount");
            maxFillAmount = serializedObject.FindProperty("maxFillAmount");
            ease = serializedObject.FindProperty("ease");
            customEase = serializedObject.FindProperty("customEase");

            spinnerShape = serializedObject.FindProperty("spinnerShape");
            spinnerImage = serializedObject.FindProperty("spinnerImage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            ReferencesFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(spinnerStyle, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(rotationSpeed, FieldOptions.WithInvertedColor());
                
                    bool mixed = spinnerStyle.hasMultipleDifferentValues;
                    bool simple = !mixed && spinnerStyle.enumValueIndex == (int)Spinner.SpinnerStyle.RadialSimple;
                    bool radial = mixed || simple || spinnerStyle.enumValueIndex == (int)Spinner.SpinnerStyle.Radial;

                    if (radial)
                    {
                        EvoEditorGUI.Property(minFillAmount, simple ? "Fill Amount" : "Min Fill Amount", FieldOptions.WithInvertedColor().WithoutSpace());
                        if (!simple)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.Property(maxFillAmount, FieldOptions.WithInvertedColor());
                        }
                    }

                    if (!simple)
                    {
                        EvoEditorGUI.Property(ease, FieldOptions.WithInvertedColor().WithoutSpace());
                        if (ease.hasMultipleDifferentValues || ease.enumValueIndex == (int)Ease.Custom)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.Property(customEase, "Custom Curve", FieldOptions.WithInvertedColor().WithoutSpace());
                        }
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
                    EvoEditorGUI.Property(spinnerShape, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(spinnerImage, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}