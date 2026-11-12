using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Scrollbar)), CanEditMultipleObjects]
    public class ScrollbarEditor : SelectableEditor
    {
        // Settings
        SerializedProperty interactable;
        SerializedProperty value;
        SerializedProperty size;
        SerializedProperty numberOfSteps;
        SerializedProperty direction;
        SerializedProperty smoothDuration;
        SerializedProperty smoothCurve;
        SerializedProperty autoHide;
        SerializedProperty hideAlpha;
        SerializedProperty hideTimer;
        SerializedProperty hideDuration;

        // Style
        SerializedProperty transition;
        SerializedProperty targetGraphic;
        SerializedProperty colorBlock;
        SerializedProperty spriteState;
        SerializedProperty animTrigger;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty handleRect;
        SerializedProperty minArrow;
        SerializedProperty maxArrow;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            interactable = serializedObject.FindProperty("m_Interactable");
            value = serializedObject.FindProperty("m_Value");
            size = serializedObject.FindProperty("m_Size");
            numberOfSteps = serializedObject.FindProperty("m_NumberOfSteps");
            direction = serializedObject.FindProperty("m_Direction");
            smoothDuration = serializedObject.FindProperty("smoothDuration");
            smoothCurve = serializedObject.FindProperty("smoothCurve");

            transition = serializedObject.FindProperty("m_Transition");
            targetGraphic = serializedObject.FindProperty("m_TargetGraphic");
            colorBlock = serializedObject.FindProperty("m_Colors");
            spriteState = serializedObject.FindProperty("m_SpriteState");
            animTrigger = serializedObject.FindProperty("m_AnimationTriggers");

            navigation = serializedObject.FindProperty("m_Navigation");

            handleRect = serializedObject.FindProperty("m_HandleRect");
            minArrow = serializedObject.FindProperty("minArrow");
            maxArrow = serializedObject.FindProperty("maxArrow");

            autoHide = serializedObject.FindProperty("autoHide");
            hideAlpha = serializedObject.FindProperty("hideAlpha");
            hideDuration = serializedObject.FindProperty("hideDuration");
            hideTimer = serializedObject.FindProperty("hideTimer");

            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            TransitionFoldout();
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(value, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(size, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(numberOfSteps, FieldOptions.WithInvertedColor());

                    EditorGUI.BeginChangeCheck();
                    EvoEditorGUI.Property(direction, FieldOptions.WithInvertedColor());
                    if (EditorGUI.EndChangeCheck())
                        ChangeDirection();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(autoHide, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (autoHide.boolValue || autoHide.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(hideAlpha, "Alpha");
                            EvoEditorGUI.Property(hideDuration, "Duration");
                            EvoEditorGUI.Property(hideTimer, "Timer", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Smooth Scrolling");
                    {
                        EvoEditorGUI.Property(smoothDuration, "Duration");
                        EvoEditorGUI.Property(smoothCurve, "Animation Curve", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void TransitionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Transition", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(transition, "Transition Mode", FieldOptions.WithoutSpace().WithoutBackground());

                    if (!transition.hasMultipleDifferentValues)
                    {
                        var mode = (UnityEngine.UI.Selectable.Transition)transition.enumValueIndex;
                        if (mode != UnityEngine.UI.Selectable.Transition.None)
                        {
                            EvoEditorGUI.BeginContainer();
                            {
                                switch (mode)
                                {
                                    case UnityEngine.UI.Selectable.Transition.ColorTint:
                                        EvoEditorGUI.Property(targetGraphic);
                                        EvoEditorGUI.Property(colorBlock, "Colors", FieldOptions.WithoutSpace());
                                        break;
                                    case UnityEngine.UI.Selectable.Transition.SpriteSwap:
                                        EvoEditorGUI.Property(targetGraphic);
                                        EvoEditorGUI.Property(spriteState, FieldOptions.WithoutSpace());
                                        break;
                                    case UnityEngine.UI.Selectable.Transition.Animation:
                                        EvoEditorGUI.Property(animTrigger, "Animation Triggers", FieldOptions.WithoutSpace());
                                        break;
                                }
                            }
                            EvoEditorGUI.EndContainer();
                        }
                    }
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
                    EvoEditorGUI.Property(handleRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(minArrow, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(maxArrow, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void ChangeDirection()
        {
            var newDirection = (Scrollbar.Direction)direction.enumValueIndex;
            foreach (var obj in serializedObject.targetObjects)
            {
                var scrollbar = (Scrollbar)obj;
                if (scrollbar.direction == newDirection)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(scrollbar.gameObject, "Change Scrollbar Direction");
                scrollbar.SetDirection(newDirection, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(scrollbar);

                foreach (var rect in scrollbar.GetComponentsInChildren<UnityEngine.RectTransform>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
        }
    }
}