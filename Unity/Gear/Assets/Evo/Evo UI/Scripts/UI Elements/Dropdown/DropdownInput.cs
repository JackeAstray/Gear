using System;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Connects a text input to a <see cref="Dropdown"/> and filters items as the user types.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Dropdown))]
    public class DropdownInput : MonoBehaviour
    {
        [SerializeField, TextInput, Tooltip("Text input used to display and filter dropdown values.")]
        private MonoBehaviour inputField;

        [SerializeField, Tooltip("Clears the dropdown selection when this component initializes.")]
        private bool resetIndexOnAwake = true;

        // Cache
        Dropdown dropdown;
        TextInputListener inputListener;

        // Properties
        bool IsConfigured => dropdown != null && TextProvider.IsInput(inputField);
        public Dropdown SourceDropdown => dropdown;
        public MonoBehaviour SourceInputField => inputField;

        void Awake()
        {
            dropdown = GetComponent<Dropdown>();
            inputListener = new TextInputListener { onValueChanged = FilterDropdown, onSelect = OpenDropdown };

            if (IsConfigured && resetIndexOnAwake)
                dropdown.ClearSelection();
        }

        void Start()
        {
            if (!IsConfigured || resetIndexOnAwake || !TryGetItem(dropdown.SelectedIndex, out Dropdown.Item item))
                return;

            string value = GetItemText(item);
            TextProvider.SetValue(inputField, value, false);
            FilterDropdown(value);
        }

        void OnEnable()
        {
            if (!IsConfigured)
                return;

            dropdown.onItemSelected.AddListener(UpdateInputField);
            inputListener.Bind(inputField);
        }

        void OnDisable()
        {
            if (dropdown != null)
                dropdown.onItemSelected.RemoveListener(UpdateInputField);

            inputListener?.Unbind();
        }

        void OpenDropdown() => dropdown.Open();

        void FilterDropdown(string input)
        {
            input ??= string.Empty;

            if (dropdown.SelectedIndex != -1)
            {
                if (!TryGetItem(dropdown.SelectedIndex, out Dropdown.Item selectedItem)
                    || !string.Equals(input, GetItemText(selectedItem), StringComparison.Ordinal))
                {
                    dropdown.ClearSelection();
                }
            }

            dropdown.SetFilter(input);
        }

        void UpdateInputField(int index)
        {
            TextBackend backend = TextProvider.FindInput(inputField);
            if (backend == null || !TryGetItem(index, out Dropdown.Item item))
                return;

            backend.SetValue(inputField, GetItemText(item), true);
            backend.RaiseEndEdit(inputField);
        }

        bool TryGetItem(int index, out Dropdown.Item item)
        {
            item = null;
            if (dropdown == null || dropdown.items == null || index < 0 || index >= dropdown.items.Count)
                return false;

            item = dropdown.items[index];
            return item != null;
        }

        static string GetItemText(Dropdown.Item item) => item.label ?? string.Empty;

        /// <summary>
        /// Sets the input value and applies the corresponding dropdown filter.
        /// </summary>
        public void SetValue(string value)
        {
            if (TextProvider.IsInput(inputField))
                TextProvider.SetValue(inputField, value ?? string.Empty, true);
        }
    }
}