using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Ripple)), CanEditMultipleObjects]
    public class RippleEditor : UnityEditor.Editor
    {
        SerializedProperty ripple;
        SerializedProperty createOnClick;
        SerializedProperty createOnSubmit;


        void OnEnable()
        {
            ripple = serializedObject.FindProperty(nameof(Ripple.ripple));
            createOnClick = serializedObject.FindProperty(nameof(createOnClick));
            createOnSubmit = serializedObject.FindProperty(nameof(createOnSubmit));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            AppearanceFoldout();
            InputFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void AppearanceFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Appearance", true, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                DrawRippleSettings(ripple);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void InputFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Input", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Toggle(createOnClick, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(createOnSubmit, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        public static void DrawRippleSettings(SerializedProperty property)
        {
            if (InteractiveEffectEditor.BeginToggleFoldout(property, "Ripple"))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                InteractiveEffectEditor.DrawTarget(property);
                InteractiveEffectEditor.DrawColor(property);
                EvoEditorGUI.Toggle(property.FindPropertyRelative(nameof(Ripple.Settings.centered)));
                EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Ripple.Settings.coverage)));
                EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Ripple.Settings.softness)));
                EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Ripple.Settings.duration)));
                EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Ripple.Settings.fadeOutDuration)), FieldOptions.WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }
    }
}