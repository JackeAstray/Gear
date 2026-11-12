using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ModalWindow), true)]
    public class ModalWindowEditor : UnityEditor.Editor
    {
        ModalWindow mwTarget;

        // Content
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;

        // Settings
        SerializedProperty customContent;
        SerializedProperty closeOnConfirm;
        SerializedProperty closeOnCancel;
        SerializedProperty startBehavior;
        SerializedProperty closeBehavior;
        SerializedProperty navigationMode;

        // Animations
        SerializedProperty openAnimation;
        SerializedProperty closeAnimation;
        TweenClipGUI openAnimationGUI;
        TweenClipGUI closeAnimationGUI;
        TweenSequencePreviewGUI openPreviewGUI;
        TweenSequencePreviewGUI closePreviewGUI;

        // References
        SerializedProperty iconImage;
        SerializedProperty titleText;
        SerializedProperty descriptionText;
        SerializedProperty confirmButton;
        SerializedProperty cancelButton;

        // Events
        SerializedProperty onOpen;
        SerializedProperty onClose;
        SerializedProperty onConfirm;
        SerializedProperty onCancel;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            mwTarget = (ModalWindow)target;

            icon = serializedObject.FindProperty(nameof(icon));
            title = serializedObject.FindProperty(nameof(title));
            description = serializedObject.FindProperty(nameof(description));

            customContent = serializedObject.FindProperty(nameof(customContent));
            closeOnConfirm = serializedObject.FindProperty(nameof(closeOnConfirm));
            closeOnCancel = serializedObject.FindProperty(nameof(closeOnCancel));
            startBehavior = serializedObject.FindProperty(nameof(startBehavior));
            closeBehavior = serializedObject.FindProperty(nameof(closeBehavior));
            navigationMode = serializedObject.FindProperty(nameof(navigationMode));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, mwTarget, () => mwTarget.openAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, mwTarget, () => mwTarget.closeAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;

            iconImage = serializedObject.FindProperty(nameof(iconImage));
            titleText = serializedObject.FindProperty(nameof(titleText));
            descriptionText = serializedObject.FindProperty(nameof(descriptionText));
            confirmButton = serializedObject.FindProperty(nameof(confirmButton));
            cancelButton = serializedObject.FindProperty(nameof(cancelButton));

            onOpen = serializedObject.FindProperty(nameof(onOpen));
            onClose = serializedObject.FindProperty(nameof(onClose));
            onConfirm = serializedObject.FindProperty(nameof(onConfirm));
            onCancel = serializedObject.FindProperty(nameof(onCancel));
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
            ContentSection();
            SettingsFoldout();
            AnimationsFoldout();
            ReferencesFoldout();
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

        void ContentSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Content", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (customContent.boolValue)
                    {
                        EvoEditorGUI.Callout("'Custom Content' is enabled; content will not be managed by this component.", 
                            invertColor: true, spaceBefore: 0, spaceAfter: 0);
                    }
                    else
                    {
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Window", nestDepth: 1);
                        {
                            if (iconImage.objectReferenceValue != null)
                                EvoEditorGUI.Property(icon);

                            EvoEditorGUI.Property(title);
                            EvoEditorGUI.Property(description, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                        EvoEditorGUI.AddLayoutSpace();
                        string[] props = new string[] { "titleKey", "descriptionKey" };
                        Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, mwTarget.gameObject, null, props);
#endif

                        if (mwTarget.confirmButton != null || mwTarget.cancelButton != null)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                            EvoEditorGUI.BeginContainer("Buttons", nestDepth: 1);
                            {
                                if (mwTarget.confirmButton != null && EvoEditorGUI.Button("Edit Confirm Button"))
                                    Selection.activeGameObject = mwTarget.confirmButton.gameObject;

                                GUILayout.Space((mwTarget.confirmButton != null && mwTarget.cancelButton != null) ? 3 : 0);
                               
                                if (mwTarget.cancelButton != null && EvoEditorGUI.Button("Edit Cancel Button"))
                                    Selection.activeGameObject = mwTarget.cancelButton.gameObject;
                            }
                            EvoEditorGUI.EndContainer();
                            EvoEditorGUI.EndColumn();
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(customContent, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnConfirm, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnCancel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(startBehavior, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(closeBehavior, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(navigationMode, FieldOptions.WithInvertedColor().WithoutSpace());
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

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(iconImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(titleText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(descriptionText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(confirmButton, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(cancelButton, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(onOpen, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onClose, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onConfirm, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onCancel, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}