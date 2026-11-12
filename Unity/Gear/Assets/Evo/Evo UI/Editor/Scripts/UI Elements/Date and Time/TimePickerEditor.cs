using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TimePicker))]
    public class TimePickerEditor : UnityEditor.Editor
    {
        // Initial Time
        SerializedProperty initialTimeMode;
        SerializedProperty customHour;
        SerializedProperty customMinute;

        // Settings
        SerializedProperty hourFormat;
        SerializedProperty minuteInterval;
        SerializedProperty rowHeight;
        SerializedProperty amLabel;
        SerializedProperty pmLabel;
#if EVO_LOCALIZATION
        SerializedProperty amKey;
        SerializedProperty pmKey;
#endif

        // Effects
        SerializedProperty styleProvider;
        SerializedProperty useDefaultStylerPreset;
        SerializedProperty stylerPreset;
        SerializedProperty soundEffects;
        SerializedProperty tickSFX;

        // References
        SerializedProperty hourWheel;
        SerializedProperty minuteWheel;
        SerializedProperty periodWheel;
        SerializedProperty selectionIndicator;

        // Events
        SerializedProperty onTimeChanged;

        void OnEnable()
        {
            if (target == null)
                return;

            initialTimeMode = serializedObject.FindProperty(nameof(initialTimeMode));
            customHour = serializedObject.FindProperty(nameof(customHour));
            customMinute = serializedObject.FindProperty(nameof(customMinute));

            hourFormat = serializedObject.FindProperty(nameof(hourFormat));
            minuteInterval = serializedObject.FindProperty(nameof(minuteInterval));
            rowHeight = serializedObject.FindProperty(nameof(rowHeight));
            amLabel = serializedObject.FindProperty(nameof(amLabel));
            pmLabel = serializedObject.FindProperty(nameof(pmLabel));
#if EVO_LOCALIZATION
            amKey = serializedObject.FindProperty(nameof(amKey));
            pmKey = serializedObject.FindProperty(nameof(pmKey));
#endif

            styleProvider = serializedObject.FindProperty(nameof(styleProvider));
            useDefaultStylerPreset = serializedObject.FindProperty(nameof(useDefaultStylerPreset));
            stylerPreset = serializedObject.FindProperty(nameof(stylerPreset));
            soundEffects = serializedObject.FindProperty(nameof(soundEffects));
            tickSFX = serializedObject.FindProperty(nameof(tickSFX));

            hourWheel = serializedObject.FindProperty(nameof(hourWheel));
            minuteWheel = serializedObject.FindProperty(nameof(minuteWheel));
            periodWheel = serializedObject.FindProperty(nameof(periodWheel));
            selectionIndicator = serializedObject.FindProperty(nameof(selectionIndicator));

            onTimeChanged = serializedObject.FindProperty(nameof(onTimeChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            StyleFoldout();
            EffectsFoldout();
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
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(initialTimeMode, "Initial Time", FieldOptions.WithoutBackground().WithoutSpace());
                    if (initialTimeMode.hasMultipleDifferentValues || initialTimeMode.enumValueIndex == (int)TimePicker.InitialTimeMode.Custom)
                    {
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        EvoEditorGUI.Property(customHour, "Hour (0-23)");
                        EvoEditorGUI.Property(customMinute, "Minute", FieldOptions.WithoutSpace());
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Property(hourFormat, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(minuteInterval, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(rowHeight, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Period Labels", padding: 3);
                    {
                        EvoEditorGUI.Property(amLabel, "AM");
                        EvoEditorGUI.Property(pmLabel, "PM", FieldOptions.WithoutSpace());
#if EVO_LOCALIZATION
                        if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                        {
                            EvoEditorGUI.AddPropertySpace();
                            Localization.Editor.IntegrationGUI.KeyField(amKey, "AM Key");
                            EvoEditorGUI.AddPropertySpace();
                            Localization.Editor.IntegrationGUI.KeyField(pmKey, "PM Key");
                        }
#endif
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((TimePicker)target).gameObject);
#endif
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout();
        }

        void StyleFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Style", false, FoldoutOptions.WithIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Callout("Each wheel copies one item for all of its rows. Select the items and the separator " +
                        "to restyle them together. Their color and font come from the Styler Object's Color ID and Font ID.",
                        invertColor: true, spaceBefore: 0);
                    if (EvoEditorGUI.Button("Select Text Items", options: ButtonOptions.WithInvertedColor()))
                        Selection.objects = CollectTextItems();
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout();
        }

        Object[] CollectTextItems()
        {
            var selection = new List<Object>();
            foreach (Object item in targets)
            {
                var picker = (TimePicker)item;
                foreach (PickerWheel wheel in new[] { picker.hourWheel, picker.minuteWheel, picker.periodWheel })
                {
                    if (wheel == null)
                        continue;

                    RectTransform template = wheel.ItemTemplate;
                    if (template != null && !selection.Contains(template.gameObject))
                        selection.Add(template.gameObject);

                    // Texts beside the wheels, such as the separator
                    if (wheel.transform.parent == null)
                        continue;

                    foreach (Transform sibling in wheel.transform.parent)
                    {
                        if (!sibling.TryGetComponent<PickerWheel>(out _) && TextProvider.TryGetText(sibling, out _)
                            && !selection.Contains(sibling.gameObject))
                            selection.Add(sibling.gameObject);
                    }
                }
            }

            return selection.ToArray();
        }

        void EffectsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Effects", false, FoldoutOptions.WithIcon("UI_Effect")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(styleProvider, FieldOptions.WithoutBackground().WithoutSpace());
                    if (styleProvider.enumValueIndex == (int)StyleProvider.Styler)
                    {
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        StylerEditor.PresetFields(useDefaultStylerPreset, stylerPreset);
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(soundEffects, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                    if (soundEffects.hasMultipleDifferentValues || soundEffects.boolValue)
                    {
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        if (styleProvider.enumValueIndex == (int)StyleProvider.Styler
                            && !stylerPreset.hasMultipleDifferentValues && stylerPreset.objectReferenceValue != null)
                        {
                            SerializedProperty stylerID = tickSFX.FindPropertyRelative(nameof(AudioMapping.stylerID));
                            StylerEditor.AudioDropdown(stylerPreset, stylerID, "Tick SFX", false, addBackground: true);
                        }
                        else
                        {
                            SerializedProperty audioClip = tickSFX.FindPropertyRelative(nameof(AudioMapping.audioClip));
                            EvoEditorGUI.Property(audioClip, "Tick SFX", FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
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
                    EvoEditorGUI.Property(hourWheel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(minuteWheel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(periodWheel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(selectionIndicator, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onTimeChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}