using UnityEditor;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Notification), true)]
    public class NotificationEditor : UnityEditor.Editor
    {
        Notification nTarget;

        // Content
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;

        // Settings
        SerializedProperty enableStacking;
        SerializedProperty playOnEnable;
        SerializedProperty autoClose;
        SerializedProperty autoCloseDelay;
        SerializedProperty showDurationBar;
        SerializedProperty destroyAfter;
        SerializedProperty styleProvider;
        SerializedProperty useDefaultStylerPreset;
        SerializedProperty stylerPreset;
        SerializedProperty soundEffects;
        SerializedProperty openSFX;
        SerializedProperty closeSFX;

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
        SerializedProperty durationBar;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            nTarget = (Notification)target;

            icon = serializedObject.FindProperty(nameof(icon));
            title = serializedObject.FindProperty(nameof(title));
            description = serializedObject.FindProperty(nameof(description));

            enableStacking = serializedObject.FindProperty(nameof(enableStacking));
            playOnEnable = serializedObject.FindProperty(nameof(playOnEnable));
            autoClose = serializedObject.FindProperty(nameof(autoClose));
            autoCloseDelay = serializedObject.FindProperty(nameof(autoCloseDelay));
            showDurationBar = serializedObject.FindProperty(nameof(showDurationBar));
            destroyAfter = serializedObject.FindProperty(nameof(destroyAfter));

            styleProvider = serializedObject.FindProperty(nameof(styleProvider));
            useDefaultStylerPreset = serializedObject.FindProperty(nameof(useDefaultStylerPreset));
            stylerPreset = serializedObject.FindProperty(nameof(stylerPreset));

            soundEffects  = serializedObject.FindProperty(nameof(soundEffects));
            openSFX = serializedObject.FindProperty(nameof(openSFX));
            closeSFX = serializedObject.FindProperty(nameof(closeSFX));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, nTarget, () => nTarget.openAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, nTarget, () => nTarget.closeAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;

            iconImage = serializedObject.FindProperty(nameof(iconImage));
            titleText = serializedObject.FindProperty(nameof(titleText));
            descriptionText = serializedObject.FindProperty(nameof(descriptionText));
            durationBar = serializedObject.FindProperty(nameof(durationBar));
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
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Notification");
                    {
                        EvoEditorGUI.Property(icon);
                        EvoEditorGUI.Property(title);
                        EvoEditorGUI.Property(description, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    string[] props = new string[] { "titleKey", "descriptionKey" };
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, nTarget.gameObject, null, props);
#endif
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
                    EvoEditorGUI.Toggle(enableStacking, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(playOnEnable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(destroyAfter, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(autoClose, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (autoClose.boolValue)
                        {
                            bool hasDurationBar = durationBar.objectReferenceValue != null || durationBar.hasMultipleDifferentValues;
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(autoCloseDelay, "Delay", FieldOptions.WithSpace(hasDurationBar));
                            if (hasDurationBar)
                                EvoEditorGUI.Toggle(showDurationBar, "Duration Bar", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(soundEffects, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (soundEffects.hasMultipleDifferentValues || soundEffects.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            {
                                EvoEditorGUI.Property(styleProvider, "Style Provider");

                                if (styleProvider.enumValueIndex == (int)StyleProvider.Styler)
                                    StylerEditor.PresetFields(useDefaultStylerPreset, stylerPreset, "Preset", addSpace: true);

                                DrawAudioMapping(openSFX, addSpace: true);
                                DrawAudioMapping(closeSFX, addSpace: false);
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

        void DrawAudioMapping(SerializedProperty mapping, bool addSpace)
        {
            if (styleProvider.enumValueIndex == (int)StyleProvider.Styler && stylerPreset.objectReferenceValue != null)
            {
                SerializedProperty stylerID = mapping.FindPropertyRelative(nameof(AudioMapping.stylerID));
                StylerEditor.AudioDropdown(stylerPreset, stylerID, mapping.displayName, addSpace, addBackground: true);
            }
            else
            {
                SerializedProperty audioClip = mapping.FindPropertyRelative(nameof(AudioMapping.audioClip));
                EvoEditorGUI.Property(audioClip, mapping.displayName, FieldOptions.WithSpace(addSpace));
            }
        }

        void AnimationsFoldout()
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
                    EvoEditorGUI.Property(iconImage, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(titleText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(descriptionText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(durationBar, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}