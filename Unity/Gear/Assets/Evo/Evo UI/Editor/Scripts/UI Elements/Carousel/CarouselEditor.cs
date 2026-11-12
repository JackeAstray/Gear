using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Carousel)), NoAutoStaticsCleanup]
    public class CarouselEditor : UnityEditor.Editor
    {
        // Content
        SerializedProperty currentIndex;
        SerializedProperty items;
        ReorderableListOptions itemListOptions;
        Action<SerializedProperty> itemDrawer;
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;

        // Settings
        SerializedProperty useUnscaledTime;
        SerializedProperty autoSlide;
        SerializedProperty autoSlideTimer;
        SerializedProperty animationEase;
        SerializedProperty animationCurve;
        SerializedProperty animationDuration;
        SerializedProperty indicatorShrink;
        SerializedProperty orientation;

        // Neighbor Items
        SerializedProperty showNeighbors;
        SerializedProperty neighborOpacity;
        SerializedProperty maxVisibleNeighbors;
        SerializedProperty itemSpacing;

        // References
        SerializedProperty itemParent;
        SerializedProperty itemPreset;
        SerializedProperty indicatorParent;
        SerializedProperty indicatorPreset;

        void OnEnable()
        {
            currentIndex = serializedObject.FindProperty(nameof(currentIndex));
            items = serializedObject.FindProperty(nameof(items));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            autoSlide = serializedObject.FindProperty(nameof(autoSlide));
            autoSlideTimer = serializedObject.FindProperty(nameof(autoSlideTimer));
            animationEase = serializedObject.FindProperty(nameof(animationEase));
            animationCurve = serializedObject.FindProperty(nameof(animationCurve));
            animationDuration = serializedObject.FindProperty(nameof(animationDuration));
            indicatorShrink = serializedObject.FindProperty(nameof(indicatorShrink));
            orientation = serializedObject.FindProperty(nameof(orientation));
            showNeighbors = serializedObject.FindProperty(nameof(showNeighbors));
            neighborOpacity = serializedObject.FindProperty(nameof(neighborOpacity));
            maxVisibleNeighbors = serializedObject.FindProperty(nameof(maxVisibleNeighbors));
            itemSpacing = serializedObject.FindProperty(nameof(itemSpacing));
            itemParent = serializedObject.FindProperty(nameof(itemParent));
            itemPreset = serializedObject.FindProperty(nameof(itemPreset));
            indicatorParent = serializedObject.FindProperty(nameof(indicatorParent));
            indicatorPreset = serializedObject.FindProperty(nameof(indicatorPreset));

            itemDrawer = DrawItem;
            itemListOptions = ReorderableListOptions.WithControlName("Carousel.Items")
                .WithTitle("Items")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add an item to populate this carousel.")
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
                    string title = items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(Carousel.Item.title)).stringValue;
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
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Carousel.Item.title)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Carousel.Item.background)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Carousel.Item.description)));
#if EVO_LOCALIZATION
                if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                {
                    EvoEditorGUI.BeginColumn();
                    EvoEditorGUI.BeginContainer("Localization");
                    {
                        Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(Carousel.Item.titleKey)), options: LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.AddLayoutSpace();
                        Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(Carousel.Item.descriptionKey)), options: LayoutOptions.WithInvertedColor());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
                }
#endif
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Carousel.Item.onClick)), FieldOptions.WithoutBackground().WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        static void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(Carousel.Item.title)).stringValue = "Item title";
            item.FindPropertyRelative(nameof(Carousel.Item.background)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Carousel.Item.description)).stringValue = "Item description";
            item.FindPropertyRelative(nameof(Carousel.Item.onClick)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(Carousel.Item.titleKey)).stringValue = string.Empty;
            item.FindPropertyRelative(nameof(Carousel.Item.descriptionKey)).stringValue = string.Empty;
#endif
            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            string title = item.FindPropertyRelative(nameof(Carousel.Item.title)).stringValue;
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
                        EvoEditorGUI.Toggle(autoSlide, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (autoSlide.boolValue)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(autoSlideTimer, "Timer", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(showNeighbors, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (showNeighbors.boolValue)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(neighborOpacity, "Opacity");
                            EvoEditorGUI.Property(maxVisibleNeighbors, "Max Neighbors Per Side");
                            EvoEditorGUI.Property(itemSpacing, "Spacing", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Property(orientation, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(animationEase, FieldOptions.WithInvertedColor());
                    if (animationEase.hasMultipleDifferentValues || animationEase.intValue == (int)Ease.Custom)
                        EvoEditorGUI.Property(animationCurve, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Property(animationDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(indicatorShrink, FieldOptions.WithInvertedColor().WithoutSpace());

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((Carousel)target).gameObject);
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
                {
                    EvoEditorGUI.Property(itemParent, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(itemPreset, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(indicatorParent, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(indicatorPreset, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}