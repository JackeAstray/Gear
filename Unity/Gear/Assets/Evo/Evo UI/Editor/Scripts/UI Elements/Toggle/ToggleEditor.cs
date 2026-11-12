using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Toggle))]
    public class ToggleEditor : SelectableEditor
    {
        // Settings
        SerializedProperty isOn;
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty transitionDuration;
        SerializedProperty checkDuration;
        SerializedProperty checkEase;
        SerializedProperty checkCurve;

        // References
        SerializedProperty checkmark;
        SerializedProperty targetGraphic;

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
            checkDuration = serializedObject.FindProperty(nameof(checkDuration));
            checkEase = serializedObject.FindProperty(nameof(checkEase));
            checkCurve = serializedObject.FindProperty(nameof(checkCurve));

            checkmark = serializedObject.FindProperty(nameof(checkmark));
            targetGraphic = serializedObject.FindProperty("m_TargetGraphic");

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
                    EvoEditorGUI.BeginContainer("Check Animation", nestDepth: 1);
                    {
                        bool showCurve = checkEase.hasMultipleDifferentValues || checkEase.intValue == (int)Ease.Custom;
                        EvoEditorGUI.Property(checkDuration, "Duration");
                        EvoEditorGUI.Property(checkEase, "Ease", FieldOptions.WithSpace(showCurve));

                        if (showCurve)
                            EvoEditorGUI.Property(checkCurve, "Curve", FieldOptions.WithoutSpace());
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
                    EvoEditorGUI.Property(checkmark, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetGraphic, FieldOptions.WithInvertedColor().WithoutSpace());
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