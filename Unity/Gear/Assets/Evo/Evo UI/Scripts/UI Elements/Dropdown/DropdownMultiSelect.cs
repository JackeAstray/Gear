using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    /// <summary>
    /// Extends <see cref="Dropdown"/> with independent selection state for multiple items.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Dropdown (Multi Select)"), HelpURL(Constants.HelpUrl + "ui-elements/dropdown")]
    public class DropdownMultiSelect : Dropdown
    {
        // Header
        [Tooltip("Text displayed when no items are selected.")]
        public string headerPlaceholder = "Select items...";
        [Tooltip("Format used to describe selected items in the header.")]
        public HeaderFormat headerFormat = HeaderFormat.Adaptive;

        // Settings
        [Range(1, 10), Tooltip("Maximum number of labels shown before the adaptive header switches to a count.")]
        public int maxDisplayCount = 3;
        [Tooltip("Text appended to the selected count.")]
        public string countSuffix = "selected";

        // Events
        public UnityEvent<int, bool> onItemToggled = new();
        public UnityEvent<List<int>> onSelectionChanged = new();

        /// <summary>
        /// Available formats for selected values in the dropdown header.
        /// </summary>
        public enum HeaderFormat
        {
            CommaSeparated,
            CountOnly,
            Adaptive
        }

        // Properties
        protected override int ScrollTargetIndex => selectedIndices.Count > 0 ? selectedIndices[0] : -1;
        public int SelectedCount => selectedIndices.Count;
        public List<int> SelectedIndices => new(selectedIndices);
        public List<Item> SelectedItems
        {
            get
            {
                var result = new List<Item>(selectedIndices.Count);
                for (int i = 0; i < selectedIndices.Count; i++)
                {
                    int itemIndex = selectedIndices[i];
                    if (itemIndex >= 0 && itemIndex < items.Count && items[itemIndex] != null)
                        result.Add(items[itemIndex]);
                }
                return result;
            }
        }

        // State
        readonly List<int> selectedIndices = new();
        readonly StringBuilder headerBuilder = new();
        bool selectionStarted, selectedBeforeStart;

        protected override void InitializeSelection()
        {
            bool keepSelection = selectedBeforeStart;
            selectionStarted = true;
            base.ClearSelection();

            // Selections made from code before Start, such as restored choices, replace the defaults
            if (keepSelection)
            {
                for (int i = 0; i < items.Count; i++)
                    SetItemSelected(items[i], IsItemSelected(i));

                UpdateHeader();
                return;
            }

            selectedIndices.Clear();

            // Apply default selections without triggering events
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i]?.defaultSelected != true)
                    continue;

                selectedIndices.Add(i);
                SetItemSelected(items[i], true);
            }

            UpdateHeader();
        }

        protected override void SetupItemButton(Button button, Item item)
        {
            ConfigureItemButton(button, item);

            // Prefer the helper already configured on the prefab so its checkmark reference is retained
            if (!button.TryGetComponent<DropdownMultiSelectItem>(out var multiSelectItem))
                multiSelectItem = button.gameObject.AddComponent<DropdownMultiSelectItem>();

            item.multiSelectItem = multiSelectItem;
            multiSelectItem.SetSelected(IsItemSelected(item.index));
            button.onClick.AddListener(() => ToggleItem(item.index));
        }

        protected override void UpdateHeader(Item item = null)
        {
            if (headerButton == null)
                return;

            headerButton.Text = BuildHeaderText();
            headerButton.Icon = null;
        }

        protected override void PrepareVirtualItemButton(Button button)
        {
            if (!button.TryGetComponent<DropdownMultiSelectItem>(out _))
                button.gameObject.AddComponent<DropdownMultiSelectItem>();
        }

        protected override void ApplyVirtualItemState(Button button, Item item)
        {
            button.SetState(InteractionState.Normal, true);
            SetItemSelected(item, IsItemSelected(item.index));
        }

        protected override void OnVirtualItemClick(int index) => ToggleItem(index);

        protected override void OnItemRemoved(int index)
        {
            int removedSelection = selectedIndices.BinarySearch(index);
            if (removedSelection >= 0)
                selectedIndices.RemoveAt(removedSelection);

            for (int i = 0; i < selectedIndices.Count; i++)
            {
                if (selectedIndices[i] > index)
                    selectedIndices[i]--;
            }
        }

        protected override void OnItemsCleared() => selectedIndices.Clear();

        public override void SortAlphabetically(bool ascending = true)
        {
            List<Item> selectedItems = SelectedItems;
            base.SortAlphabetically(ascending);

            selectedIndices.Clear();
            for (int i = 0; i < selectedItems.Count; i++)
            {
                int index = items.IndexOf(selectedItems[i]);
                if (index >= 0)
                    selectedIndices.Add(index);
            }

            selectedIndices.Sort();
            RefreshVirtualItemStates();
            UpdateHeader();
        }

        static void SetItemSelected(Item item, bool selected)
        {
            if (item?.multiSelectItem != null)
                item.multiSelectItem.SetSelected(selected);
        }

        void InvokeSelectionChanged() => onSelectionChanged?.Invoke(new List<int>(selectedIndices));

        void NoteSelectionChange()
        {
            if (!selectionStarted)
                selectedBeforeStart = true;
        }

        void InvokeSelectionEvents(int index, bool selected)
        {
            onItemToggled?.Invoke(index, selected);
            InvokeSelectionChanged();
        }

        string BuildHeaderText()
        {
            if (selectedIndices.Count == 0)
                return headerPlaceholder;

            bool useCount = headerFormat == HeaderFormat.CountOnly ||
                            (headerFormat == HeaderFormat.Adaptive && selectedIndices.Count > maxDisplayCount);

            if (useCount)
                return $"{selectedIndices.Count} {countSuffix}";

            headerBuilder.Clear();
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int itemIndex = selectedIndices[i];
                if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null)
                    continue;

                if (headerBuilder.Length > 0)
                    headerBuilder.Append(", ");

                headerBuilder.Append(items[itemIndex].label);
            }

            return headerBuilder.ToString();
        }

        #region Public API
        /// <summary>
        /// Toggles selection on the item at the given index.
        /// </summary>
        public void ToggleItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            if (selectedIndices.BinarySearch(index) >= 0)
                DeselectItem(index, triggerEvents);
            else
                SelectItem(index, triggerEvents);
        }

        public override void SelectItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            int insertionIndex = selectedIndices.BinarySearch(index);
            if (insertionIndex >= 0)
                return;

            selectedIndices.Insert(~insertionIndex, index);
            SetItemSelected(items[index], true);
            NoteSelectionChange();
            UpdateHeader();

            if (!triggerEvents)
                return;

            InvokeSelectionEvents(index, true);
        }

        /// <summary>
        /// Deselects the item at the given index. Does nothing if not selected.
        /// </summary>
        public void DeselectItem(int index, bool triggerEvents = true)
        {
            if (index < 0 || index >= items.Count)
                return;

            int selectionIndex = selectedIndices.BinarySearch(index);
            if (selectionIndex < 0)
                return;

            selectedIndices.RemoveAt(selectionIndex);
            SetItemSelected(items[index], false);
            NoteSelectionChange();
            UpdateHeader();

            if (!triggerEvents)
                return;

            InvokeSelectionEvents(index, false);
        }

        /// <summary>
        /// Deselects the first item matching the given label.
        /// </summary>
        public bool DeselectItem(string label, bool triggerEvents = true)
        {
            int index = FindItemIndex(label);
            if (index < 0)
                return false;

            DeselectItem(index, triggerEvents);
            return true;
        }

        /// <summary>
        /// Selects every item.
        /// </summary>
        public void SelectAll(bool triggerEvents = true)
        {
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                int insertionIndex = selectedIndices.BinarySearch(i);
                if (insertionIndex >= 0)
                    continue;

                selectedIndices.Insert(~insertionIndex, i);
                SetItemSelected(items[i], true);
                changed = true;

                if (triggerEvents)
                    onItemToggled?.Invoke(i, true);
            }

            if (!changed)
                return;

            NoteSelectionChange();
            UpdateHeader();

            if (triggerEvents)
                InvokeSelectionChanged();
        }

        /// <summary>
        /// Deselects every item.
        /// </summary>
        public void DeselectAll(bool triggerEvents = true)
        {
            if (selectedIndices.Count == 0)
                return;

            for (int i = selectedIndices.Count - 1; i >= 0; i--)
            {
                int itemIndex = selectedIndices[i];
                selectedIndices.RemoveAt(i);
                SetItemSelected(items[itemIndex], false);

                if (triggerEvents)
                    onItemToggled?.Invoke(itemIndex, false);
            }

            NoteSelectionChange();
            UpdateHeader();

            if (triggerEvents)
                InvokeSelectionChanged();
        }

        public override void ClearSelection()
        {
            for (int i = 0; i < selectedIndices.Count; i++)
            {
                int itemIndex = selectedIndices[i];
                if (itemIndex >= 0 && itemIndex < items.Count)
                    SetItemSelected(items[itemIndex], false);
            }

            selectedIndices.Clear();
            NoteSelectionChange();
            base.ClearSelection();
            UpdateHeader();
        }

        /// <summary>
        /// Gets whether an item index is selected.
        /// </summary>
        public bool IsItemSelected(int index) => selectedIndices.BinarySearch(index) >= 0;

        /// <summary>
        /// Gets whether the first item with a matching label is selected.
        /// </summary>
        public bool IsItemSelected(string label)
        {
            int index = FindItemIndex(label);
            return index >= 0 && IsItemSelected(index);
        }
        #endregion
    }
}