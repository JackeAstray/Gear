using System;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Shared Customize targets for the Buttons pages. Each slot is one button on the stage, with one kit button per Type option;
    /// only the current type is active. Effects, sounds, content and interactable apply to every type, so a type switch keeps them.
    /// Options that only show through hover, press or selection are locked while the buttons aren't interactable.
    /// </summary>
    public abstract class DemoButtonPage : MonoBehaviour
    {
        [Serializable]
        protected class Slot
        {
            [Tooltip("One kit button per Type option, in the order of the options.")]
            public Interactive[] types;
        }

        [SerializeField] private Slot[] slots;

        [Header("Locks")]
        [SerializeField] private CanvasGroup[] interactionOptions;

        int type;
        int content;
        bool interactable = true;
        bool[] selected;

        protected int SlotCount => slots.Length;
        protected Interactive Current(int slot) => slots[slot].types[type];

        protected virtual void Awake()
        {
            selected = new bool[slots.Length];
            DemoUtils.SetLocked(interactionOptions, false, false);
        }

        // Type: the next kit button of every slot takes over the current one's selection
        public void SetType(int index)
        {
            if (index == type)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                Interactive next = slots[i].types[index];
                next.gameObject.SetActive(true);
                slots[i].types[type].gameObject.SetActive(false);
                ShowSelection(i, next);
            }

            type = index;
        }

        // Content: 0 icon and text, 1 text, 2 icon
        public void SetContent(int index)
        {
            content = index;

            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                {
                    if (target is Button button)
                        ApplyContent(button);
                }
            }
        }

        // Effects
        public void SetRipple(bool value)
        {
            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                    DemoUtils.SetEffect(target.ripple, value);
            }
        }

        public void SetSpotlight(bool value)
        {
            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                    DemoUtils.SetEffect(target.spotlight, value);
            }
        }

        public void SetBorderGlow(bool value)
        {
            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                    DemoUtils.SetEffect(target.borderGlow, value);
            }
        }

        public void SetScale(bool value)
        {
            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                {
                    target.scaleEffect.enabled = value;
                    if (!value)
                        target.scaleEffect.Clear();
                }
            }
        }

        public void SetSounds(bool value)
        {
            foreach (Slot slot in slots)
            {
                foreach (Interactive target in slot.types)
                    target.soundEffects = value;
            }
        }

        // Behavior
        public void SetInteractable(bool value)
        {
            interactable = value;

            for (int i = 0; i < slots.Length; i++)
            {
                foreach (Interactive target in slots[i].types)
                    target.SetInteractable(value);

                ShowSelection(i, Current(i));
            }

            OnInteractableChanged(value);
            DemoUtils.SetLocked(interactionOptions, !value);
        }

        protected virtual void OnInteractableChanged(bool value) { }

        // The selected look stays on the slot through type switches and interactable changes
        protected void Select(int slot, bool value)
        {
            selected[slot] = value;
            ShowSelection(slot, Current(slot));
        }

        // Sets the label and icon on every type of the slot, then keeps the Content option
        protected void SetLabel(int slot, string text, Sprite icon)
        {
            foreach (Interactive target in slots[slot].types)
            {
                if (target is not Button button)
                    continue;

                button.Text = text;
                button.Icon = icon;
                ApplyContent(button);
            }
        }

        void ApplyContent(Button button)
        {
            button.EnableIcon = content != 1;
            button.EnableText = content != 2;
        }

        void ShowSelection(int slot, Interactive target)
        {
            if (!interactable)
                return;

            if (selected[slot]) { target.SetState(InteractionState.Selected); }
            else if (target.InteractionState == InteractionState.Selected) { target.SetState(InteractionState.Normal); }
        }
    }
}
