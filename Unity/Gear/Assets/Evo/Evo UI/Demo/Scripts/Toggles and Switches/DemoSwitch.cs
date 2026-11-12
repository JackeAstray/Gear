using System;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Switch tab: a settings list with one switch per row. Every option applies to all rows:
    /// which switch type shows, how long the handle takes to cross, the curve it moves on and the effects.
    /// The effects are locked while the switches aren't interactable; they only show through hover and press.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSwitch : MonoBehaviour
    {
        [Serializable]
        class Row
        {
            [Tooltip("Regular, Label and Indicator, in the order of the Type options.")]
            public Switch[] types;
        }

        [SerializeField] private Row[] rows;

        [Header("Locks")]
        [SerializeField] private CanvasGroup[] interactionOptions;

        // Smooth, Linear, or Spring: the handle overshoots the end and settles back
        readonly AnimationCurve[] curves =
        {
            AnimationCurve.EaseInOut(0, 0, 1, 1),
            AnimationCurve.Linear(0, 0, 1, 1),
            new(new Keyframe(0, 0, 0, 4.7f), new Keyframe(0.58f, 1.1f, 0, 0), new Keyframe(1, 1, 0, 0))
        };

        int type;

        void Awake() => DemoUtils.SetLocked(interactionOptions, rows.Length > 0 && !rows[0].types[type].IsInteractable(), false);

        // Each row's next type takes over its state; the other options already apply to every type
        public void SetType(int index)
        {
            if (index == type)
                return;

            foreach (Row row in rows)
            {
                Switch current = row.types[type];
                Switch next = row.types[index];

                // Set while inactive, so the switch and its row's icon enable straight into the current state
                next.SetValue(current.IsOn, false);
                next.gameObject.SetActive(true);
                next.SetInteractable(current.IsInteractable());
                current.gameObject.SetActive(false);
            }

            type = index;
        }

        // Motion
        public void SetDuration(float value)
        {
            foreach (Row row in rows)
            {
                foreach (Switch target in row.types)
                    target.handleDuration = value;
            }
        }

        public void SetCurve(int index)
        {
            foreach (Row row in rows)
            {
                foreach (Switch target in row.types)
                    target.handleCurve = curves[index];
            }
        }

        // Effects
        public void SetRipple(bool value)
        {
            foreach (Row row in rows)
            {
                foreach (Switch target in row.types)
                    DemoUtils.SetEffect(target.ripple, value);
            }
        }

        public void SetBorderGlow(bool value)
        {
            foreach (Row row in rows)
            {
                foreach (Switch target in row.types)
                    DemoUtils.SetEffect(target.borderGlow, value);
            }
        }

        // Behavior
        public void SetSounds(bool value)
        {
            foreach (Row row in rows)
            {
                foreach (Switch target in row.types)
                    target.soundEffects = value;
            }
        }

        // Hidden types pick this up from the shown one when they take over
        public void SetInteractable(bool value)
        {
            foreach (Row row in rows)
                row.types[type].SetInteractable(value);

            DemoUtils.SetLocked(interactionOptions, !value);
        }
    }
}
