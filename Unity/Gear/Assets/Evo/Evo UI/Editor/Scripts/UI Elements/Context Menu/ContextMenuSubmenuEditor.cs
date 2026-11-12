using UnityEditor;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ContextMenuSubmenu))]
    public class ContextMenuSubmenuEditor : UnityEditor.Editor
    {
        ContextMenuSubmenu submenuTarget;

        SerializedProperty expandDelay;
        SerializedProperty submenuOffset;

        SerializedProperty openAnimation;
        SerializedProperty closeAnimation;
        TweenClipGUI openAnimationGUI;
        TweenClipGUI closeAnimationGUI;
        TweenSequencePreviewGUI openPreviewGUI;
        TweenSequencePreviewGUI closePreviewGUI;

        SerializedProperty submenuButton;
        SerializedProperty submenuPrefab;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            submenuTarget = (ContextMenuSubmenu)target;

            expandDelay = serializedObject.FindProperty(nameof(expandDelay));
            submenuOffset = serializedObject.FindProperty(nameof(submenuOffset));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, submenuTarget, () => submenuTarget.OpenAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, submenuTarget, () => submenuTarget.CloseAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;

            submenuButton = serializedObject.FindProperty(nameof(submenuButton));
            submenuPrefab = serializedObject.FindProperty(nameof(submenuPrefab));
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
            DrawAnimationsFoldout();
            ReferencesFoldout();
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
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(expandDelay, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(submenuOffset, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawAnimationsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Callout("Slide tracks are rebound to the spawned menu visuals. " +
                        "Horizontal offsets are mirrored when the submenu opens to the left.", invertColor: true, spaceBefore: 0);
                    openAnimationGUI.DrawSequence(serializedObject, openAnimation, BeginLiveEdit, EndLiveEdit, openPreviewGUI.DrawControls);
                    EvoEditorGUI.AddPropertySpace();
                    closeAnimationGUI.DrawSequence(serializedObject, closeAnimation, BeginLiveEdit, EndLiveEdit, closePreviewGUI.DrawControls);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", true, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(submenuButton, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(submenuPrefab, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}