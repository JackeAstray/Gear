using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(MenuBar)), NoAutoStaticsCleanup]
    public class MenuBarEditor : UnityEditor.Editor
    {
        // Items
        SerializedProperty items;

        // Settings
        SerializedProperty openMenuOnHover;
        SerializedProperty blockUIWhileOpen;
        SerializedProperty itemMenuOffset;

        // References
        SerializedProperty itemParent;
        SerializedProperty itemPreset;
        SerializedProperty contextMenuPreset;

        // Editor
        ReorderableListOptions itemListOptions;
        ContextMenuItemList menuItemList;
        Action<SerializedProperty> itemDrawer;

        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;

        void OnEnable()
        {
            items = serializedObject.FindProperty(nameof(items));
            openMenuOnHover = serializedObject.FindProperty(nameof(openMenuOnHover));
            blockUIWhileOpen = serializedObject.FindProperty(nameof(blockUIWhileOpen));
            itemMenuOffset = serializedObject.FindProperty(nameof(itemMenuOffset));
            itemParent = serializedObject.FindProperty(nameof(itemParent));
            itemPreset = serializedObject.FindProperty(nameof(itemPreset));
            contextMenuPreset = serializedObject.FindProperty(nameof(contextMenuPreset));

            itemDrawer = DrawItem;
            menuItemList = new ContextMenuItemList(serializedObject, "MenuBar.MenuItems", nestDepth: 1);
            itemListOptions = ReorderableListOptions.WithControlName("MenuBar.Items")
                .WithTitle("Items")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add an item to populate this menu bar.")
                .WithAddLabel("Add Item")
                .WithItemInitializer(InitializeItem)
                .WithDeleteTooltip("Delete item");
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

        void DrawItem(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(MenuBar.Item.label)));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(MenuBar.Item.icon)));
#if EVO_LOCALIZATION
            Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(MenuBar.Item.tableKey)), "Localization Key");
            EvoEditorGUI.AddLayoutSpace();
#endif
            menuItemList.Draw(item.FindPropertyRelative(nameof(MenuBar.Item.menuBarItems)));
            EvoEditorGUI.AddPropertySpace();
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(MenuBar.Item.onClick)), FieldOptions.WithoutBackground().WithoutSpace());
            EvoEditorGUI.EndContainer();
        }

        static void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(MenuBar.Item.label)).stringValue = "Menu Item";
            item.FindPropertyRelative(nameof(MenuBar.Item.icon)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(MenuBar.Item.menuBarItems)).arraySize = 0;
            item.FindPropertyRelative(nameof(MenuBar.Item.onClick)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(MenuBar.Item.tableKey)).stringValue = string.Empty;
#endif
            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            string label = item.FindPropertyRelative(nameof(MenuBar.Item.label)).stringValue;
            return string.IsNullOrEmpty(label) ? $"Item {index + 1}" : label;
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Toggle(openMenuOnHover, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(blockUIWhileOpen, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(itemMenuOffset, FieldOptions.WithInvertedColor().WithoutSpace());
#if EVO_LOCALIZATION
                EvoEditorGUI.AddLayoutSpace();
                Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((MenuBar)target).gameObject);
#endif
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(itemParent, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(itemPreset, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(contextMenuPreset, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}