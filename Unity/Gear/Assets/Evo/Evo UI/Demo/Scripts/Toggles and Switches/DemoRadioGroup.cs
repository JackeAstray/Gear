using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Radio Button Group tabs: deselecting, the indicator's motion and the buttons' effects.
    /// The effects are locked while the group isn't interactable; they only show through hover and press.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRadioGroup : MonoBehaviour
    {
        [SerializeField] private RadioButtonGroup group;
        [SerializeField] private Button[] buttons;
        [Tooltip("Optional: dims while nothing is picked, like a confirm button that needs a pick.")]
        [SerializeField] private CanvasGroup confirm;

        [Header("Locks")]
        [SerializeField] private CanvasGroup[] interactionOptions;

        void Awake()
        {
            DemoUtils.SetLocked(interactionOptions, buttons.Length > 0 && !buttons[0].IsInteractable(), false);

            if (confirm != null)
            {
                group.onSelectionChanged.AddListener(OnSelectionChanged);
                DemoUtils.SetLocked(confirm, group.selectedIndex < 0, false);
            }
        }

        void OnSelectionChanged(int index) => DemoUtils.SetLocked(confirm, index < 0);

        // Behavior
        public void SetAllowDeselect(bool value) => group.allowDeselection = value;

        public void SetInteractable(bool value)
        {
            foreach (Button button in buttons)
                button.SetInteractable(value);

            // Coming back from Disabled lands on Normal, so the picked option shows as picked again
            Button selected = group.GetButton();
            if (value && selected != null)
                selected.SetState(InteractionState.Selected);

            DemoUtils.SetLocked(interactionOptions, !value);
        }

        // Effects
        public void SetRipple(bool value)
        {
            foreach (Button button in buttons)
                DemoUtils.SetEffect(button.ripple, value);
        }

        public void SetSpotlight(bool value)
        {
            foreach (Button button in buttons)
                DemoUtils.SetEffect(button.spotlight, value);
        }

        // Indicator
        public void SetDuration(float value) => group.indicatorDuration = value;

        public void SetStretch(float value) => group.indicatorStretch = value;
    }
}
