using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ProgressButton))]
    public class ProgressButtonEditor : SelectableEditor
    {
        // Progress Settings
        SerializedProperty holdDuration;
        SerializedProperty resetDuration;
        SerializedProperty progressCurve;
        SerializedProperty resetCurve;
        SerializedProperty stayOnComplete;
        SerializedProperty completeStateDuration;

        // State Animation
        SerializedProperty progressTransitionDuration;
        SerializedProperty animationType;
        SerializedProperty animationCurve;
        SerializedProperty scaleFrom;
        SerializedProperty slideOffset;

        // Settings
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty progressFill;
        SerializedProperty normalStateCG;
        SerializedProperty inProgressStateCG;
        SerializedProperty completeStateCG;

        // Events
        SerializedProperty onClick;
        SerializedProperty onProgressStart;
        SerializedProperty onProgressUpdate;
        SerializedProperty onComplete;
        SerializedProperty onCancel;

        protected override void OnEnable()
        {
            InitializeEditorState();
            UINavigationEditor.PrepareForVisualize(this);
        }

        void InitializeEditorState()
        {
            holdDuration = serializedObject.FindProperty(nameof(holdDuration));
            resetDuration = serializedObject.FindProperty(nameof(resetDuration));
            progressCurve = serializedObject.FindProperty(nameof(progressCurve));
            resetCurve = serializedObject.FindProperty(nameof(resetCurve));
            stayOnComplete = serializedObject.FindProperty(nameof(stayOnComplete));
            completeStateDuration = serializedObject.FindProperty(nameof(completeStateDuration));

            progressTransitionDuration = serializedObject.FindProperty(nameof(progressTransitionDuration));
            animationType = serializedObject.FindProperty(nameof(animationType));
            animationCurve = serializedObject.FindProperty(nameof(animationCurve));
            scaleFrom = serializedObject.FindProperty(nameof(scaleFrom));
            slideOffset = serializedObject.FindProperty(nameof(slideOffset));

            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty(nameof(resolveRaycast));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            interactionState = serializedObject.FindProperty(nameof(interactionState));
            navigation = serializedObject.FindProperty("m_Navigation");

            progressFill = serializedObject.FindProperty(nameof(progressFill));
            normalStateCG = serializedObject.FindProperty(nameof(normalStateCG));
            inProgressStateCG = serializedObject.FindProperty(nameof(inProgressStateCG));
            completeStateCG = serializedObject.FindProperty(nameof(completeStateCG));

            onClick = serializedObject.FindProperty(nameof(onClick));
            onProgressStart = serializedObject.FindProperty(nameof(onProgressStart));
            onProgressUpdate = serializedObject.FindProperty(nameof(onProgressUpdate));
            onComplete = serializedObject.FindProperty(nameof(onComplete));
            onCancel = serializedObject.FindProperty(nameof(onCancel));
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ProgressFoldout();
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

        void ProgressFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Progress", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(holdDuration, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Completion", nestDepth: 1);
                    {
                        bool showDuration = stayOnComplete.hasMultipleDifferentValues || !stayOnComplete.boolValue;
                        EvoEditorGUI.Toggle(stayOnComplete, FieldOptions.WithSpace(showDuration));

                        if (stayOnComplete.hasMultipleDifferentValues || !stayOnComplete.boolValue)
                            EvoEditorGUI.Property(completeStateDuration, "Show Duration", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddLayoutSpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Curves", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(resetDuration);
                        EvoEditorGUI.Property(resetCurve);
                        EvoEditorGUI.Property(progressCurve, FieldOptions.WithoutSpace());
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
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(interactionState, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(animationType, "State Animation", FieldOptions.WithoutSpace().WithoutBackground());
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    {
                        bool hasAnimation = animationType.hasMultipleDifferentValues || animationType.enumValueIndex != 0;
                        EvoEditorGUI.Property(progressTransitionDuration, "Duration", FieldOptions.WithSpace(hasAnimation));

                        if (hasAnimation)
                        {
                            bool showSlide = animationType.hasMultipleDifferentValues || animationType.enumValueIndex == 2;
                            bool showScale = animationType.hasMultipleDifferentValues || animationType.enumValueIndex == 1;

                            EvoEditorGUI.Property(animationCurve, "Curve");

                            if (showScale)
                                EvoEditorGUI.Property(scaleFrom, FieldOptions.WithSpace(showSlide));
                            if (showSlide)
                                EvoEditorGUI.Property(slideOffset, "Offset", FieldOptions.WithoutSpace());
                        }
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
                    EvoEditorGUI.Property(progressFill, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(normalStateCG, "Normal State", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(inProgressStateCG, "In Progress", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(completeStateCG, "Complete", FieldOptions.WithInvertedColor().WithoutSpace());
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
                {
                    EvoEditorGUI.Property(onClick, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onProgressStart, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onProgressUpdate, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onComplete, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onCancel, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}