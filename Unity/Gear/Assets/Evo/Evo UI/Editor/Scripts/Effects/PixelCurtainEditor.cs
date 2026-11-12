using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(PixelCurtain)), CanEditMultipleObjects]
    public class PixelCurtainEditor : UnityEditor.Editor
    {
        SerializedProperty adaptToRectSize;
        SerializedProperty cellSize;
        SerializedProperty direction;
        SerializedProperty randomness;
        SerializedProperty fadeWidth;
        SerializedProperty progress;
        SerializedProperty transitionDuration;
        SerializedProperty actionOnEnable;
        SerializedProperty invertFadeOutDirection;
        SerializedProperty cullObject;
        SerializedProperty autoFadeOut;
        SerializedProperty holdDuration;
        SerializedProperty button;
        SerializedProperty targetState;
        SerializedProperty onFadeInComplete;
        SerializedProperty onFadeOutComplete;


        void OnEnable()
        {
            adaptToRectSize = serializedObject.FindProperty(nameof(adaptToRectSize));
            cellSize = serializedObject.FindProperty(nameof(cellSize));
            direction = serializedObject.FindProperty(nameof(direction));
            randomness = serializedObject.FindProperty(nameof(randomness));
            fadeWidth = serializedObject.FindProperty(nameof(fadeWidth));
            progress = serializedObject.FindProperty(nameof(progress));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            actionOnEnable = serializedObject.FindProperty(nameof(actionOnEnable));
            invertFadeOutDirection = serializedObject.FindProperty(nameof(invertFadeOutDirection));
            cullObject = serializedObject.FindProperty(nameof(cullObject));
            autoFadeOut = serializedObject.FindProperty(nameof(autoFadeOut));
            holdDuration = serializedObject.FindProperty(nameof(holdDuration));
            button = serializedObject.FindProperty(nameof(button));
            targetState = serializedObject.FindProperty(nameof(targetState));
            onFadeInComplete = serializedObject.FindProperty(nameof(onFadeInComplete));
            onFadeOutComplete = serializedObject.FindProperty(nameof(onFadeOutComplete));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();
            
            SettingsFoldout();
            ReferencesFoldout();
            EventsFoldout();
            
            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "settingsFoldout", "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Appearance", nestDepth: 1);
                    {
                        EvoEditorGUI.Toggle(adaptToRectSize);
                        EvoEditorGUI.Property(cellSize);
                        EvoEditorGUI.Property(direction);
                        EvoEditorGUI.Property(randomness);
                        EvoEditorGUI.Property(fadeWidth, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Animation", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(progress);
                        EvoEditorGUI.Property(transitionDuration, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Behavior", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(actionOnEnable);
                        EvoEditorGUI.Toggle(invertFadeOutDirection);
                        EvoEditorGUI.Toggle(cullObject);

                        bool showHold = autoFadeOut.hasMultipleDifferentValues || autoFadeOut.boolValue;
                        EvoEditorGUI.Toggle(autoFadeOut, FieldOptions.WithSpace(showHold));

                        if (showHold)
                            EvoEditorGUI.Property(holdDuration, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
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
                    bool showState = button.hasMultipleDifferentValues || button.objectReferenceValue != null;
                    EvoEditorGUI.Property(button, showState
                        ? FieldOptions.WithInvertedColor()
                        : FieldOptions.WithInvertedColor().WithoutSpace());

                    if (showState)
                        EvoEditorGUI.Property(targetState, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onFadeInComplete, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onFadeOutComplete, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}