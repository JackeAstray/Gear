using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dropdown tabs: list size, closing, A–Z order and item icons. On Multi Select also the header format and Select all.
    /// Pages can also hide authored items (the party hides invited players).
    /// </summary>
    [AddComponentMenu("")]
    public class DemoDropdown : MonoBehaviour
    {
        [SerializeField] private Dropdown dropdown;

        [Header("Options")]
        [SerializeField] private CanvasGroup closeOnSelectOption;
        [SerializeField] private CanvasGroup clickOutsideOption;
        [SerializeField] private CanvasGroup namesOption;
        [SerializeField] private Switch selectAllOption;

        static readonly DropdownMultiSelect.HeaderFormat[] Formats =
        {
            DropdownMultiSelect.HeaderFormat.Adaptive, DropdownMultiSelect.HeaderFormat.CommaSeparated, DropdownMultiSelect.HeaderFormat.CountOnly
        };

        readonly HashSet<Dropdown.Item> hidden = new();
        readonly List<Dropdown.Item> picked = new();
        DropdownMultiSelect multi;
        Dropdown.Item[] authored;
        Sprite[] icons;
        bool sorted;

        void Awake()
        {
            CacheItems();
            if (multi != null)
                multi.onSelectionChanged.AddListener(OnSelectionChanged);
        }

        // The editor's sliders can report their value before Awake
        void CacheItems()
        {
            if (authored != null)
                return;

            // The items as authored, for turning Sort A–Z and Icons back off
            authored = dropdown.items.ToArray();
            icons = new Sprite[authored.Length];
            for (int i = 0; i < authored.Length; i++)
                icons[i] = authored[i].icon;

            multi = dropdown as DropdownMultiSelect;
        }

        void OnSelectionChanged(List<int> selection) => DemoUtils.Show(selectAllOption, selection.Count == dropdown.items.Count);

        // The list lays itself out again from these, open or closed
        public void SetItemHeight(float value)
        {
            dropdown.itemHeight = value;
            dropdown.UpdateUI();
        }

        public void SetSpacing(float value)
        {
            dropdown.itemSpacing = (int)value;
            dropdown.UpdateUI();
        }

        public void SetMaxHeight(float value)
        {
            dropdown.maxHeight = value;
            dropdown.UpdateUI();
        }

        // An open list sits under a blocker that also covers its header, so one of these two always stays on
        public void SetCloseOnSelect(bool value)
        {
            dropdown.closeOnItemSelect = value;
            DemoUtils.SetLocked(clickOutsideOption, !value);
        }

        public void SetClickOutside(bool value)
        {
            dropdown.closeOnClickOutside = value;
            DemoUtils.SetLocked(closeOnSelectOption, !value);
        }

        public void SetSorted(bool value)
        {
            CacheItems();
            sorted = value;
            Rebuild();
        }

        // Takes an authored item out of the list or puts it back in its place
        public void SetHidden(Dropdown.Item item, bool value)
        {
            CacheItems();
            if (value ? hidden.Add(item) : hidden.Remove(item))
                Rebuild();
        }

        // The kit only sorts by label: lay out the authored order again and pick the same items
        void Rebuild()
        {
            picked.Clear();
            if (multi != null)
            {
                for (int i = 0; i < dropdown.items.Count; i++)
                {
                    if (dropdown.items[i] != null && multi.IsItemSelected(i))
                        picked.Add(dropdown.items[i]);
                }
            }

            Dropdown.Item selected = dropdown.SelectedItem;
            if (multi != null)
                multi.DeselectAll(false);

            dropdown.items.Clear();
            foreach (Dropdown.Item item in authored)
            {
                if (!hidden.Contains(item))
                    dropdown.items.Add(item);
            }

            if (sorted) { dropdown.SortAlphabetically(); }
            else { dropdown.RefreshItems(); }

            if (multi != null)
            {
                foreach (Dropdown.Item item in picked)
                    multi.SelectItem(dropdown.items.IndexOf(item), false);
            }
            else if (selected != null && dropdown.items.Contains(selected))
            {
                dropdown.SelectItem(selected, false);
            }
        }

        public void SetIcons(bool value)
        {
            for (int i = 0; i < authored.Length; i++)
                authored[i].icon = value ? icons[i] : null;

            dropdown.RefreshItems();
            dropdown.UpdateUI();
        }

        // Multi Select: Adaptive, List or Count. Only Adaptive switches to a count after some names
        public void SetHeaderFormat(int index)
        {
            multi.headerFormat = Formats[index];
            multi.UpdateUI();
            DemoUtils.SetLocked(namesOption, index != 0);
        }

        public void SetNamesBeforeCount(float value)
        {
            CacheItems();
            multi.maxDisplayCount = (int)value;
            multi.UpdateUI();
        }

        public void SetSelectAll(bool value)
        {
            if (value) { multi.SelectAll(); }
            else { multi.DeselectAll(); }
        }
    }
}
