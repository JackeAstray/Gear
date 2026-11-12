using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(AnimatedContainer)), CanEditMultipleObjects]
    public class AnimatedContainerEditor : UnityEditor.Editor
    {
        SerializedProperty getChildObjects;
        SerializedProperty containerObjects;
        SerializedProperty animationCurve;
        SerializedProperty animationDelay;
        SerializedProperty startScale;
        SerializedProperty scaleDuration;
        SerializedProperty fadeAfterScale;
        SerializedProperty fadeDuration;
        SerializedProperty useUnscaledTime;
        SerializedProperty playOnEnable;
        SerializedProperty playOnce;
        SerializedProperty itemCooldown;

        void OnEnable()
        {
            getChildObjects = serializedObject.FindProperty("getChildObjects");
            containerObjects = serializedObject.FindProperty("containerObjects");
            animationCurve = serializedObject.FindProperty("animationCurve");
            animationDelay = serializedObject.FindProperty("animationDelay");
            startScale = serializedObject.FindProperty("startScale");
            scaleDuration = serializedObject.FindProperty("scaleDuration");
            fadeAfterScale = serializedObject.FindProperty("fadeAfterScale");
            fadeDuration = serializedObject.FindProperty("fadeDuration");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            playOnEnable = serializedObject.FindProperty("playOnEnable");
            playOnce = serializedObject.FindProperty("playOnce");
            itemCooldown = serializedObject.FindProperty("itemCooldown");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ObjectsFoldout();
            AnimationFoldout();
            SettingsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void ObjectsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Objects", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(getChildObjects, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(containerObjects, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animation", true, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(animationCurve, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(animationDelay, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(itemCooldown, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Scale");
                    {
                        EvoEditorGUI.Property(startScale);
                        EvoEditorGUI.Property(scaleDuration, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Fade");
                    {
                        EvoEditorGUI.Property(fadeAfterScale);
                        EvoEditorGUI.Property(fadeDuration, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
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
                    EvoEditorGUI.Toggle(playOnEnable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(playOnce, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}