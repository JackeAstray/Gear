using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Ripple tab: a keypad whose keys each have their own Ripple; the options change all of them together.
    /// Keys fill the code pins, and a complete code lights them in the accent before they clear.
    /// The options start from the first key's ripple.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRipple : MonoBehaviour
    {
        [SerializeField] private Ripple[] ripples;

        [Header("Code")]
        [Tooltip("The fill of each pin, in order.")]
        [SerializeField] private Transform[] pins;
        [Tooltip("The accent fill of each pin, shown once the code is complete.")]
        [SerializeField] private Transform[] donePins;
        [SerializeField] private float doneDuration = 1f;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Switch centered;
        [SerializeField] private Slider coverage;
        [SerializeField] private Slider softness;
        [SerializeField] private Slider opacity;
        [SerializeField] private Slider duration;

        const float PinDuration = 0.2f;
        static readonly Vector3 Empty = new(0f, 0f, 1f);

        int length;

        void Awake()
        {
            Ripple.Settings first = ripples[0].ripple;
            DemoUtils.Show(color, DemoUtils.GetEffectColor(first));
            DemoUtils.Show(centered, first.centered);
            DemoUtils.Show(coverage, first.coverage * 100f);
            DemoUtils.Show(softness, first.softness * 100f);
            DemoUtils.Show(opacity, first.stylerColor.alphaOverride * 100f);
            DemoUtils.Show(duration, first.duration);
        }

        // Each visit starts with an empty code
        void OnEnable() => Clear(false);

        void OnDisable() => CancelInvoke();

        public void Type()
        {
            // A complete code stays lit until it clears
            if (length == pins.Length)
                return;

            Fill(pins[length++], true, true);

            if (length < pins.Length)
                return;

            foreach (Transform pin in donePins)
                Fill(pin, true, true);

            Invoke(nameof(ClearCode), doneDuration);
        }

        public void Delete()
        {
            if (length > 0 && length < pins.Length)
                Fill(pins[--length], false, true);
        }

        void ClearCode() => Clear(true);

        void Clear(bool animate)
        {
            length = 0;
            for (int i = 0; i < pins.Length; i++)
            {
                Fill(pins[i], false, animate);
                Fill(donePins[i], false, animate);
            }
        }

        // Pins pop in with a slight overshoot and shrink out. A zero-length tween still replaces a running one.
        static void Fill(Transform pin, bool on, bool animate)
        {
            Vector3 scale = on ? Vector3.one : Empty;
            if (!animate)
                pin.localScale = scale;

            EvoTween.Create(pin.localScale, scale, animate ? PinDuration : 0f)
                .WithEase(on ? Ease.OutBack : Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToScale(pin);
        }

        public void SetColor(int index)
        {
            foreach (Ripple ripple in ripples)
                DemoUtils.SetEffectColor(ripple, ripple.ripple, index);
        }

        public void SetCentered(bool value)
        {
            foreach (Ripple ripple in ripples)
                ripple.ripple.centered = value;
        }

        // Coverage, softness and opacity are percent bars
        public void SetCoverage(float value)
        {
            foreach (Ripple ripple in ripples)
                ripple.ripple.coverage = value / 100f;
        }

        public void SetSoftness(float value)
        {
            foreach (Ripple ripple in ripples)
                ripple.ripple.softness = value / 100f;
        }

        public void SetOpacity(float value)
        {
            foreach (Ripple ripple in ripples)
                DemoUtils.SetEffectOpacity(ripple, ripple.ripple, value);
        }

        public void SetDuration(float value)
        {
            foreach (Ripple ripple in ripples)
                ripple.ripple.duration = value;
        }
    }
}