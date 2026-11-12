using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ContextMenu))]
    public class ContextMenuEditor : UnityEditor.Editor
    {
        // Content
        SerializedProperty menuItems;
        ContextMenuItemList itemList;

        // Settings
        SerializedProperty is3DObject;
        SerializedProperty usePointerPosition;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty closeOnItemClick;
        SerializedProperty closeOnOutsideClick;
        SerializedProperty triggerButton;
        SerializedProperty dragThreshold;

        // Position & Offset
        SerializedProperty targetRectAnchor;
        SerializedProperty offsetPosition;
        SerializedProperty customOffset;
        SerializedProperty offsetDistance;
        SerializedProperty screenEdgePadding;

        // References
        SerializedProperty menuPreset;
        SerializedProperty targetCanvas;
        SerializedProperty targetCamera;

        // Events
        SerializedProperty onShow;
        SerializedProperty onHide;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            menuItems = serializedObject.FindProperty(nameof(menuItems));
            itemList = new ContextMenuItemList(serializedObject, "ContextMenu.Items");

            is3DObject = serializedObject.FindProperty(nameof(is3DObject));
            usePointerPosition = serializedObject.FindProperty(nameof(usePointerPosition));
            blockUIWhileOpen = serializedObject.FindProperty(nameof(blockUIWhileOpen));
            closeOnItemClick = serializedObject.FindProperty(nameof(closeOnItemClick));
            closeOnOutsideClick = serializedObject.FindProperty(nameof(closeOnOutsideClick));
            triggerButton = serializedObject.FindProperty(nameof(triggerButton));
            dragThreshold = serializedObject.FindProperty(nameof(dragThreshold));

            targetRectAnchor = serializedObject.FindProperty(nameof(targetRectAnchor));
            offsetPosition = serializedObject.FindProperty(nameof(offsetPosition));
            customOffset = serializedObject.FindProperty(nameof(customOffset));
            offsetDistance = serializedObject.FindProperty(nameof(offsetDistance));
            screenEdgePadding = serializedObject.FindProperty(nameof(screenEdgePadding));

            menuPreset = serializedObject.FindProperty(nameof(menuPreset));
            targetCanvas = serializedObject.FindProperty(nameof(targetCanvas));
            targetCamera = serializedObject.FindProperty(nameof(targetCamera));

            onShow = serializedObject.FindProperty(nameof(onShow));
            onHide = serializedObject.FindProperty(nameof(onHide));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ItemsFoldout();
            SettingsFoldout();
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void ItemsFoldout()
        {
            itemList.Draw(menuItems);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(is3DObject, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(usePointerPosition, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(blockUIWhileOpen, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnItemClick, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnOutsideClick, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dragThreshold, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(triggerButton, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Position & Offset");
                    {
                        if (!usePointerPosition.boolValue && !is3DObject.boolValue)
                            EvoEditorGUI.Property(targetRectAnchor);
                        
                        EvoEditorGUI.Property(offsetPosition);

                        if (offsetPosition.enumValueIndex == 0)
                            EvoEditorGUI.Property(customOffset);
                        else
                            EvoEditorGUI.Property(offsetDistance, "Distance");

                        EvoEditorGUI.Property(screenEdgePadding, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((ContextMenu)target).gameObject);
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
                    EvoEditorGUI.Property(menuPreset, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetCanvas, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetCamera, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(onShow, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onHide, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }

    /// <summary>
    /// Shared context-menu item editing for standalone menus and menu-bar dropdowns.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal sealed class ContextMenuItemList
    {
        const int MaxSubmenuDepth = 4;
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;
        static readonly string[] ItemTypeNames = Array.ConvertAll(Enum.GetNames(typeof(ContextMenu.Item.ItemType)), ObjectNames.NicifyVariableName);

        readonly SerializedObject serializedObject;
        readonly int nestDepth;
        readonly ReorderableListOptions[] itemListOptions;
        readonly Action<SerializedProperty>[] itemDrawers;

        public ContextMenuItemList(SerializedObject serializedObject, string controlName, int nestDepth = 0)
        {
            this.serializedObject = serializedObject;
            this.nestDepth = nestDepth;

            itemListOptions = new ReorderableListOptions[MaxSubmenuDepth + 1];
            itemDrawers = new Action<SerializedProperty>[MaxSubmenuDepth + 1];

            for (int depth = 0; depth <= MaxSubmenuDepth; depth++)
            {
                int capturedDepth = depth;
                string title = depth == 0 ? "Menu Items" : "Submenu Items";
                string emptyMessage = depth == 0
                    ? "Add an item to populate this context menu."
                    : "Add an item to populate this submenu.";

                itemDrawers[depth] = item => DrawItem(item, capturedDepth);
                ReorderableListOptions options = ReorderableListOptions
                    .WithControlName($"{controlName}.Depth{depth}")
                    .WithTitle(title)
                    .WithEmptyMessage(emptyMessage)
                    .WithAddLabel("Add Item")
                    .WithItemInitializer(InitializeItem)
                    .WithDeleteTooltip("Delete item");

                itemListOptions[depth] = depth == 0 ? options.WithIcon("UI_Object") : options;
            }
        }

        public void Draw(SerializedProperty items)
        {
            EvoEditorGUI.ReorderableList(serializedObject, items, ItemTitleGetter, itemDrawers[0],
                itemListOptions[0], compactItems: true, compactHeader: nestDepth > 0);
        }

        void DrawItem(SerializedProperty item, int depth)
        {
            SerializedProperty type = item.FindPropertyRelative(nameof(ContextMenu.Item.type));
            if (type == null)
            {
                EvoEditorGUI.Callout("This item has no serialized data.", invertColor: true, spaceBefore: 0, spaceAfter: 0);
                return;
            }

            // Unity can expose this nested enum as an Integer inside MenuBar.Item
            ContextMenu.Item.ItemType itemType = (ContextMenu.Item.ItemType)type.intValue;
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: Math.Min(nestDepth + depth + 1, MaxSubmenuDepth));
            {
                EvoEditorGUI.Dropdown(type, ItemTypeNames, "Type", itemType == ContextMenu.Item.ItemType.Separator ? FieldOptions.WithoutSpace() : default);
                switch (itemType)
                {
                    case ContextMenu.Item.ItemType.Button:
                        DrawNamedItemFields(item);
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ContextMenu.Item.buttonPrefab)), "Prefab Override", FieldOptions.WithoutSpace());
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ContextMenu.Item.onClick)), "On Click", FieldOptions.WithoutBackground().WithoutSpace());
                        break;

                    case ContextMenu.Item.ItemType.Submenu:
                        DrawNamedItemFields(item);
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Toggle(item.FindPropertyRelative(nameof(ContextMenu.Item.expandOnHover)), "Expand On Hover", FieldOptions.WithoutSpace());
                        DrawSubmenuItems(item, depth);
                        break;

                    case ContextMenu.Item.ItemType.CustomObject:
                        EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ContextMenu.Item.customObject)), "Custom Object", FieldOptions.WithoutSpace());
                        break;
                }
            }
            EvoEditorGUI.EndContainer();
        }

        void DrawSubmenuItems(SerializedProperty item, int depth)
        {
            SerializedProperty subItems = item.FindPropertyRelative(nameof(ContextMenu.Item.subItems));
            if (subItems == null)
                return;

            EvoEditorGUI.AddLayoutSpace();

            if (depth >= MaxSubmenuDepth)
            {
                EvoEditorGUI.Callout("The inspector supports up to four nested submenu levels. Deeper serialized items are preserved.",
                    invertColor: true, spaceBefore: 0, spaceAfter: 0);
                return;
            }

            int childDepth = depth + 1;
            EvoEditorGUI.ReorderableList(serializedObject, subItems, ItemTitleGetter, itemDrawers[childDepth],
                itemListOptions[childDepth], compactItems: true, compactHeader: true, invertColors: false, boldHeader: false);
        }

        static void DrawNamedItemFields(SerializedProperty item)
        {
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ContextMenu.Item.name)), "Name");
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(ContextMenu.Item.icon)), "Icon", FieldOptions.WithoutSpace());
#if EVO_LOCALIZATION
            if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(item.serializedObject))
            {
                EvoEditorGUI.AddLayoutSpace();
                Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(ContextMenu.Item.tableKey)), "Localization Key");
            }
#endif
        }

        static void InitializeItem(SerializedProperty item)
        {
            if (item.propertyType == SerializedPropertyType.ManagedReference)
            {
                item.managedReferenceValue = new ContextMenu.Item();
                item.isExpanded = true;
                return;
            }

            item.FindPropertyRelative(nameof(ContextMenu.Item.name)).stringValue = "Menu Item";
            item.FindPropertyRelative(nameof(ContextMenu.Item.icon)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ContextMenu.Item.type)).intValue = (int)ContextMenu.Item.ItemType.Button;
            item.FindPropertyRelative(nameof(ContextMenu.Item.buttonPrefab)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ContextMenu.Item.customObject)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(ContextMenu.Item.expandOnHover)).boolValue = true;
            item.FindPropertyRelative(nameof(ContextMenu.Item.subItems)).arraySize = 0;
            item.FindPropertyRelative(nameof(ContextMenu.Item.onClick)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(ContextMenu.Item.tableKey)).stringValue = string.Empty;
#endif

            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            SerializedProperty type = item.FindPropertyRelative(nameof(ContextMenu.Item.type));
            if (type == null)
                return $"Item {index + 1}";

            ContextMenu.Item.ItemType itemType = (ContextMenu.Item.ItemType)type.intValue;
            if (itemType == ContextMenu.Item.ItemType.Separator)
                return "Separator";

            if (itemType == ContextMenu.Item.ItemType.CustomObject)
            {
                UnityEngine.Object prefab = item.FindPropertyRelative(nameof(ContextMenu.Item.customObject)).objectReferenceValue;
                return prefab != null ? prefab.name : "Custom Object";
            }

            string itemName = item.FindPropertyRelative(nameof(ContextMenu.Item.name)).stringValue;
            if (!string.IsNullOrEmpty(itemName))
                return itemName;

            return itemType == ContextMenu.Item.ItemType.Submenu ? "Submenu" : $"Item {index + 1}";
        }
    }
}