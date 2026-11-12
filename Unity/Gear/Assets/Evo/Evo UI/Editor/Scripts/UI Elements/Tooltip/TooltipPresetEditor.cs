using UnityEditor;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(TooltipPreset))]
    public class TooltipPresetEditor : UnityEditor.Editor
    {
        TooltipPreset presetTarget;

        SerializedProperty tooltipRect;
        SerializedProperty canvasGroup;
        SerializedProperty iconImage;
        SerializedProperty titleText;
        SerializedProperty descriptionText;
        SerializedProperty contentSizeFitter;
        SerializedProperty contentContainer;
        SerializedProperty layoutGroup;
        SerializedProperty layoutElement;

        SerializedProperty openAnimation;
        SerializedProperty closeAnimation;
        TweenClipGUI openAnimationGUI;
        TweenClipGUI closeAnimationGUI;
        TweenSequencePreviewGUI openPreviewGUI;
        TweenSequencePreviewGUI closePreviewGUI;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            presetTarget = (TooltipPreset)target;

            tooltipRect = serializedObject.FindProperty(nameof(tooltipRect));
            canvasGroup = serializedObject.FindProperty(nameof(canvasGroup));
            iconImage = serializedObject.FindProperty(nameof(iconImage));
            titleText = serializedObject.FindProperty(nameof(titleText));
            descriptionText = serializedObject.FindProperty(nameof(descriptionText));
            contentSizeFitter = serializedObject.FindProperty(nameof(contentSizeFitter));
            contentContainer = serializedObject.FindProperty(nameof(contentContainer));
            layoutGroup = serializedObject.FindProperty(nameof(layoutGroup));
            layoutElement = serializedObject.FindProperty(nameof(layoutElement));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, presetTarget, () => presetTarget.openAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, presetTarget, () => presetTarget.closeAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;
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

        void DrawAnimationsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", true, FoldoutOptions.WithIcon("UI_Animation")))
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

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(tooltipRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(canvasGroup, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(iconImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(titleText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(descriptionText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(contentSizeFitter, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(contentContainer, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(layoutGroup, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(layoutElement, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}