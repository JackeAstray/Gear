using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(RadioButtonGroup))]
    public class RadioButtonGroupEditor : UnityEditor.Editor
    {
        RadioButtonGroup rbgTarget;

        // Settings
        SerializedProperty selectedIndex;
        SerializedProperty allowDeselection;
        SerializedProperty useUnscaledTime;
        SerializedProperty stateChangeDelay;
        SerializedProperty targetParent;

        // Indicator
        SerializedProperty indicatorObject;
        SerializedProperty indicatorDirection;
        SerializedProperty indicatorAutoSize;
        SerializedProperty indicatorStretch;
        SerializedProperty indicatorCurve;
        SerializedProperty indicatorDuration;

        // Events
        SerializedProperty onSelectionChanged;

        void OnEnable()
        {
            rbgTarget = (RadioButtonGroup)target;

            selectedIndex = serializedObject.FindProperty(nameof(selectedIndex));
            allowDeselection = serializedObject.FindProperty(nameof(allowDeselection));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            stateChangeDelay = serializedObject.FindProperty(nameof(stateChangeDelay));
            targetParent = serializedObject.FindProperty(nameof(targetParent));

            indicatorObject = serializedObject.FindProperty(nameof(indicatorObject));
            indicatorDirection = serializedObject.FindProperty(nameof(indicatorDirection));
            indicatorAutoSize = serializedObject.FindProperty(nameof(indicatorAutoSize));
            indicatorStretch = serializedObject.FindProperty(nameof(indicatorStretch));
            indicatorCurve = serializedObject.FindProperty(nameof(indicatorCurve));
            indicatorDuration = serializedObject.FindProperty(nameof(indicatorDuration));

            onSelectionChanged = serializedObject.FindProperty(nameof(onSelectionChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ObjectFoldout();
            SettingsFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void ObjectFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Object", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        Transform buttonParent = targetParent.objectReferenceValue as Transform;
                        if (buttonParent == null)
                            buttonParent = rbgTarget.transform;

                        int index = selectedIndex.intValue;
                        Button selectedButton = null;
                        if (index >= 0 && index < buttonParent.childCount)
                            buttonParent.GetChild(index).TryGetComponent(out selectedButton);

                        EvoEditorGUI.Label($" Selected Button: {(selectedButton != null ? selectedButton.name : "None")}");

                        using (new EditorGUI.DisabledScope(Application.isPlaying || buttonParent.childCount == 0))
                        {
                            EvoEditorGUI.Slider(selectedIndex, -1, Mathf.Max(-1, buttonParent.childCount - 1), "Index:",
                                FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
                        }
                    }
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
                    EvoEditorGUI.Toggle(allowDeselection, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetParent, "Custom Parent", FieldOptions.WithInvertedColor());

                    // Indicator
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(indicatorObject, FieldOptions.WithoutBackground().WithoutSpace());
                        if (indicatorObject.objectReferenceValue != null)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Toggle(indicatorAutoSize, "Auto Size");
                            EvoEditorGUI.Property(indicatorDirection, "Direction");
                            EvoEditorGUI.Property(indicatorStretch, "Elastic Stretch");
                            EvoEditorGUI.Property(indicatorCurve, "Animation Curve");
                            EvoEditorGUI.Property(indicatorDuration, "Animation Duration");
                            EvoEditorGUI.Property(stateChangeDelay, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
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
                EvoEditorGUI.Property(onSelectionChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}