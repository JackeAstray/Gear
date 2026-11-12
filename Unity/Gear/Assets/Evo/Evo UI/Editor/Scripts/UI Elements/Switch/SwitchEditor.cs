using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Switch))]
    public class SwitchEditor : SelectableEditor
    {
        // Settings
        SerializedProperty isOn;
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty transitionDuration;
        SerializedProperty handleDuration;
        SerializedProperty handleCurve;

        // References
        SerializedProperty switchHandle;
        SerializedProperty offCG;
        SerializedProperty onCG;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            InitializeEditorState();
            UINavigationEditor.PrepareForVisualize(this);
        }

        void InitializeEditorState()
        {
            isOn = serializedObject.FindProperty(nameof(isOn));
            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty(nameof(resolveRaycast));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            handleDuration = serializedObject.FindProperty(nameof(handleDuration));
            handleCurve = serializedObject.FindProperty(nameof(handleCurve));

            switchHandle = serializedObject.FindProperty(nameof(switchHandle));
            offCG = serializedObject.FindProperty(nameof(offCG));
            onCG = serializedObject.FindProperty(nameof(onCG));

            navigation = serializedObject.FindProperty("m_Navigation");

            onValueChanged = serializedObject.FindProperty(nameof(onValueChanged));
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            InteractiveEditor.EffectsFoldout(serializedObject);
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(isOn, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Handle Animation", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(handleDuration, "Duration");
                        EvoEditorGUI.Property(handleCurve, "Curve", FieldOptions.WithoutSpace());
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
                    EvoEditorGUI.Property(switchHandle, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(offCG, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(onCG, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}