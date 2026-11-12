using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Progress Bar (Radial) tab: an ability bar. Each ring holds the seconds left on its ability's cooldown. Using an ability
    /// sets the full cooldown, which eases in with the bar's smoothing, then the ring follows the time left every frame.
    /// The passive one charges by itself, fires, and starts over. Options set the number format, ring shape and fill.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCooldowns : MonoBehaviour
    {
        [Serializable]
        class Ability
        {
            public ProgressBar bar;
            public RectTransform slot;
            public Graphic icon;
            public float cooldown = 4;
            public bool passive;
            public Key key;
            public ProceduralShape track;
            public ProceduralShape fill;

            [NonSerialized] public float left;
            [NonSerialized] public float delay;
            [NonSerialized] public float hold;
            [NonSerialized] public TweenHandle pop;
            [NonSerialized] public TweenHandle iconFade;
            [NonSerialized] public TweenHandle textFade;
        }

        [SerializeField] private Ability[] abilities;
        [SerializeField] private float passiveHold = 0.6f;

        [Header("Options")]
        [SerializeField] private CanvasGroup decimalsOption;

        const float FadeDuration = 0.15f;
        const float NoTextIconAlpha = 0.3f;
        const float ReadyScale = 1.12f;
        const float BlockedScale = 0.92f;
        const float PopDuration = 0.35f;

        // Seconds, whole numbers: the options start there
        int textMode = 1;
        int decimals;

        void OnEnable()
        {
            foreach (Ability ability in abilities)
            {
                ability.hold = 0;
                ability.delay = 0;
                ability.left = ability.passive ? ability.cooldown : 0;
                ability.bar.SetValueInstant(ability.left);
                Show(ability, true);
            }
        }

        void OnDisable()
        {
            foreach (Ability ability in abilities)
            {
                ability.pop.Kill();
                ability.slot.localScale = Vector3.one;
            }
        }

        void Update()
        {
            ReadKeys();
            float delta = Time.unscaledDeltaTime;

            foreach (Ability ability in abilities)
            {
                // A passive that just fired stays full for a moment
                if (ability.hold > 0)
                {
                    ability.hold -= delta;
                    if (ability.hold <= 0)
                        Begin(ability);

                    continue;
                }

                if (ability.left <= 0)
                    continue;

                // Let the smoothing finish easing to the full cooldown first
                if (ability.delay > 0)
                {
                    ability.delay -= delta;
                    continue;
                }

                ability.left = Mathf.Max(0, ability.left - delta);
                ability.bar.SetValueInstant(ability.left);

                if (ability.left == 0)
                    Ready(ability);
            }
        }

        public void Use(int index)
        {
            Ability ability = abilities[index];
            if (ability.passive || ability.left > 0 || ability.hold > 0)
            {
                Pop(ability, BlockedScale);
                return;
            }

            Begin(ability);
        }

        void Begin(Ability ability)
        {
            ability.left = ability.cooldown;
            ability.bar.Value = ability.left;
            ability.delay = ability.bar.enableSmoothing ? ability.bar.smoothingDuration : 0;
            Show(ability, false);
        }

        void Ready(Ability ability)
        {
            Show(ability, false);
            Pop(ability, ReadyScale);

            if (ability.passive)
                ability.hold = passiveHold;
        }

        // Ready shows the icon. Cooling shows the seconds, or a dim icon when the ring has no number.
        void Show(Ability ability, bool instant)
        {
            bool ready = ability.left <= 0;
            float duration = instant ? 0 : FadeDuration;
            DemoUtils.FadeRenderer(ability.icon, ready ? 1 : textMode == 2 ? NoTextIconAlpha : 0, duration, ref ability.iconFade);
            DemoUtils.FadeRenderer(ability.bar.valueText, ready ? 0 : 1, duration, ref ability.textFade);
        }

        void Pop(Ability ability, float scale)
        {
            ability.pop.Kill();
            ability.pop = EvoTween.Create(Vector3.one * scale, Vector3.one, PopDuration)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToScale(ability.slot);
        }

        // Number keys use the abilities too, unless a field is taking the typing
        void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].key == Key.None || !keyboard[abilities[i].key].wasPressedThisFrame || Typing())
                    continue;

                Use(i);
            }
        }

        static bool Typing()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
        }

        // Percent, Seconds or None
        public void SetTextMode(int index)
        {
            textMode = index;
            DemoUtils.SetLocked(decimalsOption, index == 2);
            UpdateFormat();

            foreach (Ability ability in abilities)
                Show(ability, false);
        }

        public void SetDecimals(int index)
        {
            decimals = index;
            UpdateFormat();
        }

        void UpdateFormat()
        {
            foreach (Ability ability in abilities)
            {
                ProgressBar bar = ability.bar;
                bar.valueText.gameObject.SetActive(textMode != 2);
                bar.textFormat = textMode == 0 ? "{0}%" : "{0}s";
                bar.displayMultiplier = textMode == 0 ? 100 / ability.cooldown : 1;
                bar.displayFormat = (NumberDisplayFormat)decimals;
                Redraw(ability);
            }
        }

        public void SetThickness(float value)
        {
            foreach (Ability ability in abilities)
                ability.track.OutlineWidth = ability.fill.OutlineWidth = value;
        }

        public void SetSmoothing(bool value)
        {
            foreach (Ability ability in abilities)
                ability.bar.enableSmoothing = value;
        }

        public void SetInvertFill(bool value)
        {
            foreach (Ability ability in abilities)
            {
                ability.bar.invertFill = value;
                Redraw(ability);
            }
        }

        // The bar applies these settings on its next value change, so set the value again to show them now
        static void Redraw(Ability ability) => ability.bar.SetValueInstant(ability.bar.Value);
    }
}
