using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Helper component attached to the item prefab for DropdownMultiSelect.
    /// </summary>
    [DisallowMultipleComponent]
    public class DropdownMultiSelectItem : MonoBehaviour
    {
        [Tooltip("The GameObject to show/hide as a checkmark (e.g. a tick icon Image).")]
        [SerializeField] private GameObject checkmark;

        public bool IsSelected { get; private set; }

        /// <summary>
        /// Sets the selection state and updates the checkmark visibility.
        /// </summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (checkmark != null)
                checkmark.SetActive(selected);
        }

        /// <summary>
        /// Flips the current selection state.
        /// </summary>
        public void Toggle() => SetSelected(!IsSelected);
    }
}