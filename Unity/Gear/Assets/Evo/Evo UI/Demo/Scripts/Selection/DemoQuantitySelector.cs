using System;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Quantity Selector tab: an order. Each line has its own Quantity Selector and dims at zero,
    /// the total rolls on a Counter, and Checkout lights while the total is above zero.
    /// The options set the range every selector clamps to and their slide.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoQuantitySelector : MonoBehaviour
    {
        [Serializable]
        class Line
        {
            public QuantitySelector selector;
            public float price;
            public CanvasGroup content;
        }

        [SerializeField] private Line[] lines;
        [SerializeField] private Counter total;
        [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.4f;

        [Header("Options")]
        [SerializeField] private int[] minimums = { 0, 1, 5 };
        [SerializeField] private int[] maximums = { 10, 99, 999 };

        const float FadeDuration = 0.2f;

        void Awake()
        {
            foreach (Line line in lines)
                line.selector.onValueChanged.AddListener(OnQuantityChanged);
        }

        void Start() => Refresh(false);

        void OnQuantityChanged(int quantity) => Refresh(true);

        void Refresh(bool animate)
        {
            float sum = 0f;
            foreach (Line line in lines)
            {
                int quantity = line.selector.CurrentQuantity;
                sum += quantity * line.price;
                DemoUtils.Fade(line.content, quantity > 0 ? 1f : emptyAlpha, animate ? FadeDuration : 0f);
            }

            // Cents, so the float sum doesn't show as 11.299999
            sum = Mathf.Round(sum * 100f) / 100f;
            if (animate) { total.SetValue(sum); }
            else { total.SetValueInstant(sum); }
        }

        public void SetMinimum(int index)
        {
            foreach (Line line in lines)
                line.selector.minQuantity = minimums[index];

            ApplyRange();
        }

        public void SetMaximum(int index)
        {
            foreach (Line line in lines)
                line.selector.maxQuantity = maximums[index];

            ApplyRange();
        }

        public void SetSlide(float value)
        {
            foreach (Line line in lines)
                line.selector.slideOffset = value;
        }

        public void SetDuration(float value)
        {
            foreach (Line line in lines)
                line.selector.animationDuration = value;
        }

        void ApplyRange()
        {
            foreach (Line line in lines)
                line.selector.SetQuantity(line.selector.CurrentQuantity, false);
        }
    }
}
