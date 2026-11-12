using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ShowcasePanel)), NoAutoStaticsCleanup]
    public class ShowcasePanelEditor : UnityEditor.Editor
    {
        // Content
        SerializedProperty currentIndex;
        SerializedProperty items;
        ReorderableListOptions itemListOptions;
        Action<SerializedProperty> itemDrawer;
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;

        // Settings
        SerializedProperty useUnscaledTime;
        SerializedProperty setWithTimer;
        SerializedProperty timer;
        SerializedProperty animationDuration;
        SerializedProperty slideOffset;

        // References
        SerializedProperty buttonParent;
        SerializedProperty buttonPreset;
        SerializedProperty textDisplay;
        SerializedProperty backgroundShape;
        SerializedProperty backgroundImage;
        SerializedProperty shadowShape;
        SerializedProperty backgroundShadow;

        void OnEnable()
        {
            currentIndex = serializedObject.FindProperty(nameof(currentIndex));
            items = serializedObject.FindProperty(nameof(items));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            setWithTimer = serializedObject.FindProperty(nameof(setWithTimer));
            timer = serializedObject.FindProperty(nameof(timer));
            animationDuration = serializedObject.FindProperty(nameof(animationDuration));
            slideOffset = serializedObject.FindProperty(nameof(slideOffset));
            buttonParent = serializedObject.FindProperty(nameof(buttonParent));
            buttonPreset = serializedObject.FindProperty(nameof(buttonPreset));
            textDisplay = serializedObject.FindProperty(nameof(textDisplay));
            backgroundShape = serializedObject.FindProperty(nameof(backgroundShape));
            backgroundImage = serializedObject.FindProperty(nameof(backgroundImage));
            shadowShape = serializedObject.FindProperty(nameof(shadowShape));
            backgroundShadow = serializedObject.FindProperty(nameof(backgroundShadow));

            itemDrawer = DrawItem;
            itemListOptions = ReorderableListOptions.WithControlName("ShowcasePanel.Items")
                .WithTitle("Items")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add an item to populate this showcase panel.")
                .WithAddLabel("Add Item")
                .WithItemInitializer(InitializeItem)
                .WithDeleteTooltip("Delete item")
                .WithContentHeader(DrawItemsHeader);
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EvoEditorGUI.ReorderableList(serializedObject, items, ItemTitleGetter, itemDrawer, itemListOptions, compactItems: true, compactHeader: false);
            EvoEditorGUI.AddFoldoutSpace();
            SettingsFoldout();
            ReferencesFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawItemsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int index = currentIndex.intValue;
                if (index < 0 || index >= items.arraySize) { EvoEditorGUI.Label(" Selected Item: None"); }
                else
                {
                    string title = items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(ShowcasePanel.Item.title)).stringValue;
                    EvoEditorGUI.Label(string.IsNullOrEmpty(title) ? $" Selected Item: #{index} (no label)" : $" Selected Item: {title}");
                }

                EvoEditorGUI.Slider(currentIndex, items.arraySize > 0 ? 0 : -1, items.arraySize - 1,
                    "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawItem(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.title)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.url)), "URL");
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.icon)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.background)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.shadowColor)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.description)));
#if EVO_LOCALIZATION
                if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                {
                    EvoEditorGUI.BeginColumn();
                    EvoEditorGUI.BeginContainer("Localization");
                    {
                        Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(ShowcasePanel.Item.titleKey)), options: LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.AddLayoutSpace();
                        Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(ShowcasePanel.Item.descriptionKey)), options: LayoutOptions.WithInvertedColor());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
                }
#endif
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ShowcasePanel.Item.onClick)), FieldOptions.WithoutBackground().WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        static void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.title)).stringValue = "Item Title";
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.url)).stringValue = string.Empty;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.icon)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.background)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.shadowColor)).colorValue = Color.clear;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.description)).stringValue = "Item description";
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.button)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.onClick)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.titleKey)).stringValue = string.Empty;
            item.FindPropertyRelative(nameof(ShowcasePanel.Item.descriptionKey)).stringValue = string.Empty;
#endif
            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            string title = item.FindPropertyRelative(nameof(ShowcasePanel.Item.title)).stringValue;
            return string.IsNullOrEmpty(title) ? $"Item {index + 1}" : title;
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(setWithTimer, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (setWithTimer.boolValue)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(timer, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Property(animationDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(slideOffset, FieldOptions.WithInvertedColor().WithoutSpace());

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((ShowcasePanel)target).gameObject);
#endif
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
                EvoEditorGUI.Property(buttonParent, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(buttonPreset, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(textDisplay, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(backgroundShape, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(shadowShape, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(backgroundImage, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(backgroundShadow, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}