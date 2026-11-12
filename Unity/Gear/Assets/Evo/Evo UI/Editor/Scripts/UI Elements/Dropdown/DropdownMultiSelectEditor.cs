using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(DropdownMultiSelect))]
    public class DropdownMultiSelectEditor : DropdownEditor
    {
        DropdownMultiSelect MultiTarget => (DropdownMultiSelect)dTarget;

        SerializedProperty headerPlaceholder;
        SerializedProperty headerFormat;
        SerializedProperty maxDisplayCount;
        SerializedProperty countSuffix;
        SerializedProperty onItemToggled;
        SerializedProperty onSelectionChanged;

        protected override string ItemsControlName => "DropdownMultiSelect.Items";
        protected override string EmptyItemsMessage => "Add an item to populate this multi-select dropdown.";
        protected override bool ShowCloseOnItemSelect => false;
        protected override void InitializeAdditionalState()
        {
            headerPlaceholder = serializedObject.FindProperty(nameof(headerPlaceholder));
            headerFormat = serializedObject.FindProperty(nameof(headerFormat));
            maxDisplayCount = serializedObject.FindProperty(nameof(maxDisplayCount));
            countSuffix = serializedObject.FindProperty(nameof(countSuffix));
            onItemToggled = serializedObject.FindProperty(nameof(onItemToggled));
            onSelectionChanged = serializedObject.FindProperty(nameof(onSelectionChanged));
        }

        protected override void DrawItemsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int selectedCount = Application.isPlaying ? MultiTarget.SelectedCount : GetDefaultSelectedCount();
                EvoEditorGUI.Label(selectedCount == 0
                    ? " Selected Items: None"
                    : $" Selected Items: {selectedCount} item{(selectedCount == 1 ? string.Empty : "s")}");
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        int GetDefaultSelectedCount()
        {
            int selectedCount = 0;
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative(nameof(Dropdown.Item.defaultSelected)).boolValue)
                    selectedCount++;
            }
            return selectedCount;
        }

        protected override void DrawAdditionalItemProperties(SerializedProperty item)
        {
            EvoEditorGUI.AddLayoutSpace();
            EvoEditorGUI.Toggle(item.FindPropertyRelative(nameof(Dropdown.Item.defaultSelected)),
                "Selected By Default", FieldOptions.WithoutSpace());
        }

        protected override void DrawAdditionalSettings()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.BeginContainer("Header Format");
            {
                EvoEditorGUI.Property(headerPlaceholder, "Placeholder");
                var format = (DropdownMultiSelect.HeaderFormat)headerFormat.enumValueIndex;
                EvoEditorGUI.Property(headerFormat, "Format",
                    format == DropdownMultiSelect.HeaderFormat.CommaSeparated ? FieldOptions.WithoutSpace() : default);

                if (format == DropdownMultiSelect.HeaderFormat.Adaptive)
                    EvoEditorGUI.Property(maxDisplayCount);

                if (format != DropdownMultiSelect.HeaderFormat.CommaSeparated)
                    EvoEditorGUI.Property(countSuffix, FieldOptions.WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());
        }

        protected override void DrawSelectionEvents()
        {
            EvoEditorGUI.Property(onItemToggled, FieldOptions.WithoutBackground());
            EvoEditorGUI.Property(onSelectionChanged, FieldOptions.WithoutBackground());
        }
    }
}