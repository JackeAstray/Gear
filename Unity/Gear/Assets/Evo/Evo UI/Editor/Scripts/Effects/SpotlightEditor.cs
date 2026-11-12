using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Spotlight)), CanEditMultipleObjects]
    public class SpotlightEditor : UnityEditor.Editor
    {
        SerializedProperty spotlight;

        void OnEnable()
        {
            spotlight = serializedObject.FindProperty(nameof(Spotlight.spotlight));
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
                DrawSpotlightSettings(spotlight);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        public static void DrawSpotlightSettings(SerializedProperty property)
        {
            if (InteractiveEffectEditor.BeginToggleFoldout(property, "Spotlight"))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    InteractiveEffectEditor.DrawTarget(property);
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.visibility)));
                    InteractiveEffectEditor.DrawColor(property);
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.radius)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.softness)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.followDuration)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.fadeInDuration)));
                    EvoEditorGUI.Property(property.FindPropertyRelative(nameof(Spotlight.Settings.fadeOutDuration)), FieldOptions.WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }
    }
}