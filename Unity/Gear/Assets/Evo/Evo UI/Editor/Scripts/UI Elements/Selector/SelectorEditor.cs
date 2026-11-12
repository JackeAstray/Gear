using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Selector)), NoAutoStaticsCleanup]
    public class SelectorEditor : UnityEditor.Editor
    {
        // Items
        SerializedProperty selectedIndex;
        SerializedProperty items;

        // Settings
        SerializedProperty loop;

        // Animation
        SerializedProperty isHorizontal;
        SerializedProperty invertAnimationDirection;
        SerializedProperty slideOffset;
        SerializedProperty animationDuration;
        SerializedProperty animationEase;
        SerializedProperty animationCurve;

        // Indicator
        SerializedProperty selectedIndicatorAlpha;
        SerializedProperty unselectedIndicatorAlpha;
        SerializedProperty selectedIndicatorSize;
        SerializedProperty indicatorDuration;

        // References
        SerializedProperty contentParent;
        SerializedProperty textObject;
        SerializedProperty iconObject;
        SerializedProperty indicatorParent;
        SerializedProperty indicatorPrefab;
        SerializedProperty prevButton;
        SerializedProperty nextButton;

        // Events
        SerializedProperty onSelectionChanged;

        // Editor
        ReorderableListOptions itemListOptions;
        Action<SerializedProperty> itemDrawer;
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;

        void OnEnable()
        {
            selectedIndex = serializedObject.FindProperty(nameof(selectedIndex));
            items = serializedObject.FindProperty(nameof(items));
            loop = serializedObject.FindProperty(nameof(loop));
            isHorizontal = serializedObject.FindProperty(nameof(isHorizontal));
            invertAnimationDirection = serializedObject.FindProperty(nameof(invertAnimationDirection));
            slideOffset = serializedObject.FindProperty(nameof(slideOffset));
            animationDuration = serializedObject.FindProperty(nameof(animationDuration));
            animationEase = serializedObject.FindProperty(nameof(animationEase));
            animationCurve = serializedObject.FindProperty(nameof(animationCurve));
            selectedIndicatorAlpha = serializedObject.FindProperty(nameof(selectedIndicatorAlpha));
            unselectedIndicatorAlpha = serializedObject.FindProperty(nameof(unselectedIndicatorAlpha));
            selectedIndicatorSize = serializedObject.FindProperty(nameof(selectedIndicatorSize));
            indicatorDuration = serializedObject.FindProperty(nameof(indicatorDuration));
            contentParent = serializedObject.FindProperty(nameof(contentParent));
            textObject = serializedObject.FindProperty(nameof(textObject));
            iconObject = serializedObject.FindProperty(nameof(iconObject));
            indicatorParent = serializedObject.FindProperty(nameof(indicatorParent));
            indicatorPrefab = serializedObject.FindProperty(nameof(indicatorPrefab));
            prevButton = serializedObject.FindProperty(nameof(prevButton));
            nextButton = serializedObject.FindProperty(nameof(nextButton));
            onSelectionChanged = serializedObject.FindProperty(nameof(onSelectionChanged));

            itemDrawer = DrawItem;
            itemListOptions = ReorderableListOptions.WithControlName("Selector.Items")
                .WithTitle("Items")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add an item to populate this selector.")
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
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawItemsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int index = selectedIndex.intValue;
                if (index < 0 || index >= items.arraySize) { EvoEditorGUI.Label(" Selected Item: None"); }
                else
                {
                    string label = items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(Selector.Item.label)).stringValue;
                    EvoEditorGUI.Label(string.IsNullOrEmpty(label) ? $" Selected Item: #{index} (no label)" : $" Selected Item: {label}");
                }
                EvoEditorGUI.Slider(selectedIndex, 0, Mathf.Max(0, items.arraySize - 1),
                    "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawItem(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Selector.Item.label)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Selector.Item.icon)));
#if EVO_LOCALIZATION
                if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                {
                    Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(Selector.Item.tableKey)), "Localization Key");
                    EvoEditorGUI.AddLayoutSpace();
                }
#endif
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Selector.Item.onItemSelection)), FieldOptions.WithoutBackground().WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        static void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(Selector.Item.label)).stringValue = "Selector Item";
            item.FindPropertyRelative(nameof(Selector.Item.icon)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Selector.Item.onItemSelection)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(Selector.Item.tableKey)).stringValue = string.Empty;
#endif
            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            string label = item.FindPropertyRelative(nameof(Selector.Item.label)).stringValue;
            return string.IsNullOrEmpty(label) ? $"Item {index + 1}" : label;
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(loop, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Animation", padding: 3);
                    {
                        EvoEditorGUI.Toggle(isHorizontal);
                        EvoEditorGUI.Toggle(invertAnimationDirection, "Invert Direction");
                        EvoEditorGUI.Property(animationDuration, "Duration");
                        EvoEditorGUI.Property(animationEase, "Ease");

                        if (animationEase.hasMultipleDifferentValues || animationEase.intValue == (int)Ease.Custom)
                            EvoEditorGUI.Property(animationCurve, "Curve");

                        EvoEditorGUI.Property(slideOffset, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Indicator", padding: 3);
                    {
                        EvoEditorGUI.Property(selectedIndicatorSize, "Selected Size");
                        EvoEditorGUI.Property(selectedIndicatorAlpha, "Selected Alpha");
                        EvoEditorGUI.Property(unselectedIndicatorAlpha, "Unselected Alpha");
                        EvoEditorGUI.Property(indicatorDuration, "Duration", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((Selector)target).gameObject);
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
                EvoEditorGUI.Property(contentParent, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(textObject, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(iconObject, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(indicatorParent, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(indicatorPrefab, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(prevButton, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(nextButton, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onSelectionChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}