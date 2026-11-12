using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(BorderGlow)), CanEditMultipleObjects]
    public class BorderGlowEditor : UnityEditor.Editor
    {
        SerializedProperty borderGlow;


        void OnEnable()
        {
            borderGlow = serializedObject.FindProperty(nameof(BorderGlow.borderGlow));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            AppearanceFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void AppearanceFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Appearance", true, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                DrawBorderGlowSettings(borderGlow);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        public static void DrawBorderGlowSettings(SerializedProperty property)
        {
            if (InteractiveEffectEditor.BeginToggleFoldout(property, "Border Glow"))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    InteractiveEffectEditor.DrawTarget(property);
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.visibility)));
                    InteractiveEffectEditor.DrawColor(property);
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.width)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.radius)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.softness)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.ambient)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.followDuration)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.fadeInDuration)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(BorderGlow.Settings.fadeOutDuration)), FieldOptions.WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }
    }
}