using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Range Slider tab: a price filter. Histogram columns inside the range light up and the label counts their items.
    /// Pushing the other handle is a plain field in the kit, so it goes through here.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRangeSlider : MonoBehaviour
    {
        [SerializeField] private RangeSlider slider;

        [Header("Histogram")]
        [Tooltip("One column per step of the range, each with an accent layer that shows while it's inside.")]
        [SerializeField] private UnityEngine.UI.Graphic[] columns;
        [SerializeField] private int[] counts;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string labelFormat = "{0} items available";

        const float FadeDuration = 0.15f;

        bool[] lit;
        TweenHandle[] fades;

        void Awake()
        {
            lit = new bool[columns.Length];
            fades = new TweenHandle[columns.Length];
            slider.onValueChanged.AddListener((lower, upper) => Filter(lower, upper, true));
            Filter(slider.LowerValue, slider.UpperValue, false);
        }

        // A column sits inside when its middle does; the label counts what the lit columns hold
        void Filter(float lower, float upper, bool animate)
        {
            int total = 0;
            float step = (slider.MaxValue - slider.MinValue) / Mathf.Max(1, columns.Length);

            for (int i = 0; i < columns.Length; i++)
            {
                float middle = slider.MinValue + (i + 0.5f) * step;
                bool inside = middle > lower && middle < upper;
                if (!animate || lit[i] != inside)
                    DemoUtils.FadeRenderer(columns[i], inside ? 1f : 0f, animate ? FadeDuration : 0f, ref fades[i]);

                lit[i] = inside;

                if (inside && i < counts.Length)
                    total += counts[i];
            }

            if (label != null)
                label.SetText(labelFormat, total);
        }

        public void SetPushOtherHandle(bool value) => slider.pushOtherHandle = value;
    }
}
