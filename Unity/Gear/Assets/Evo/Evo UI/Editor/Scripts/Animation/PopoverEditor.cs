using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Popover), true)]
    public class PopoverEditor : UnityEditor.Editor
    {
        Popover popoverTarget;

        // Settings
        SerializedProperty isOpen;
        SerializedProperty manageGameObjectState;
        SerializedProperty closeOnOutsideClick;
        SerializedProperty closeOnEscapeKey;

        // Animations
        SerializedProperty openAnimation;
        SerializedProperty closeAnimation;
        TweenClipGUI openAnimationGUI;
        TweenClipGUI closeAnimationGUI;
        TweenSequencePreviewGUI openPreviewGUI;
        TweenSequencePreviewGUI closePreviewGUI;

        // Events
        SerializedProperty onStateChanged;

        void OnEnable()
        {
            popoverTarget = (Popover)target;

            isOpen = serializedObject.FindProperty(nameof(isOpen));
            manageGameObjectState = serializedObject.FindProperty(nameof(manageGameObjectState));
            closeOnOutsideClick = serializedObject.FindProperty(nameof(closeOnOutsideClick));
            closeOnEscapeKey = serializedObject.FindProperty(nameof(closeOnEscapeKey));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, popoverTarget, () => popoverTarget.openAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, popoverTarget, () => popoverTarget.closeAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;

            onStateChanged = serializedObject.FindProperty(nameof(onStateChanged));
        }

        void OnDisable()
        {
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            AnimationsFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();
            ApplyPendingChanges(controlsChanged);
        }

        void ApplyPendingChanges(bool controlsChanged)
        {
            bool hasChanges = controlsChanged || serializedObject.hasModifiedProperties;
            if (hasChanges)
                BeginLiveEdit();

            serializedObject.ApplyModifiedProperties();

            bool isDragging = openAnimationGUI?.IsDragging == true || closeAnimationGUI?.IsDragging == true;
            if (hasChanges && !isDragging)
                EndLiveEdit();
        }

        void BeginLiveEdit()
        {
            openPreviewGUI?.BeginLiveEdit();
            closePreviewGUI?.BeginLiveEdit();
        }

        void EndLiveEdit()
        {
            openPreviewGUI?.EndLiveEdit();
            closePreviewGUI?.EndLiveEdit();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                        EvoEditorGUI.Toggle(isOpen, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Toggle(manageGameObjectState, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnOutsideClick, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnEscapeKey, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    openAnimationGUI.DrawSequence(serializedObject, openAnimation, BeginLiveEdit, EndLiveEdit, openPreviewGUI.DrawControls);
                    EvoEditorGUI.AddPropertySpace();
                    closeAnimationGUI.DrawSequence(serializedObject, closeAnimation, BeginLiveEdit, EndLiveEdit, closePreviewGUI.DrawControls);
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
                EvoEditorGUI.Property(onStateChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}