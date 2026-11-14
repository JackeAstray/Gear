using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 双把手区间滑杆：拖动最近的一个把手，自动保持 low ≤ high（可设最小间距）。
    /// <para>
    /// fill 的宽度与两个把手的位置按 track 的实际宽度计算；对外派发 `Vector2(low, high)`。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Range Slider")]
    [DisallowMultipleComponent]
    public sealed class GearRangeSlider : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform lowHandle;
        [SerializeField] private RectTransform highHandle;
        [SerializeField] private float min;
        [SerializeField] private float max = 100f;
        [SerializeField] private float low = 25f;
        [SerializeField] private float high = 75f;
        [SerializeField, Min(0f)] private float minGap;
        [SerializeField] private UnityEvent<Vector2> onValueChanged = new UnityEvent<Vector2>();

        private bool draggingHigh;
        private bool dragging;

        public UnityEvent<Vector2> OnValueChanged => onValueChanged;
        public event Action<Vector2> ValueChanged;

        public RectTransform Track { get => track; set { track = value; ApplyVisual(); } }
        public RectTransform Fill { get => fill; set { fill = value; ApplyVisual(); } }
        public RectTransform LowHandle { get => lowHandle; set { lowHandle = value; ApplyVisual(); } }
        public RectTransform HighHandle { get => highHandle; set { highHandle = value; ApplyVisual(); } }

        public float Min => min;
        public float Max => max;
        public float Low => low;
        public float High => high;
        public Vector2 Values => new Vector2(low, high);

        /// <summary>两个把手之间的最小间距（0 表示允许重合）。</summary>
        public float MinGap
        {
            get => minGap;
            set => minGap = Mathf.Max(0f, value);
        }

        private void OnEnable()
        {
            ApplyVisual();
        }

        /// <summary>直接设置区间；越界 / 间距不足会自动修正。</summary>
        public void SetValues(float newLow, float newHigh, bool notify = true)
        {
            float lo = Mathf.Clamp(Mathf.Min(newLow, newHigh), min, max);
            float hi = Mathf.Clamp(Mathf.Max(newLow, newHigh), min, max);

            if (minGap > 0f && hi - lo < minGap)
            {
                // 优先保证 high 不动；high 贴边时反向推 low
                if (hi + minGap > max) lo = hi - minGap;
                else hi = lo + minGap;
            }

            lo = Mathf.Clamp(lo, min, max);
            hi = Mathf.Clamp(hi, min, max);

            bool changed = !Mathf.Approximately(lo, low) || !Mathf.Approximately(hi, high);
            low = lo;
            high = hi;
            ApplyVisual();

            if (changed && notify)
            {
                onValueChanged?.Invoke(new Vector2(low, high));
                ValueChanged?.Invoke(new Vector2(low, high));
            }
        }

        public void SetMinMax(float newMin, float newMax)
        {
            max = Mathf.Max(newMin + 0.0001f, newMax);
            min = newMin;
            SetValues(low, high, false);
        }

        /// <summary>把归一化指针位置（0-1）应用到最近的把手；测试与外部驱动用。</summary>
        public void ApplyPointer01(float normalized01)
        {
            float v = Mathf.Lerp(min, max, Mathf.Clamp01(normalized01));
            float lowN = Normalized(low);
            float highN = Normalized(high);
            bool highSide = Mathf.Abs(Normalized(v) - highN) <= Mathf.Abs(Normalized(v) - lowN);
            if (highSide) SetValues(low, v);
            else SetValues(v, high);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            draggingHigh = PickHighHandle(eventData);
            dragging = true;
            Drag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            draggingHigh = PickHighHandle(eventData);
            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
        }

        private void Drag(PointerEventData eventData)
        {
            RectTransform target = track != null ? track : (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect rect = target.rect;
            if (rect.width <= 0.0001f) return;

            float normalized = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            float v = Mathf.Lerp(min, max, normalized);
            if (draggingHigh) SetValues(low, v);
            else SetValues(v, high);
        }

        private bool PickHighHandle(PointerEventData eventData)
        {
            RectTransform target = track != null ? track : (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return true;
            }

            Rect rect = target.rect;
            if (rect.width <= 0.0001f) return true;
            float normalized = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            return Mathf.Abs(normalized - Normalized(high)) <= Mathf.Abs(normalized - Normalized(low));
        }

        private float Normalized(float value)
        {
            return max - min <= 0.0001f ? 0f : Mathf.Clamp01((value - min) / (max - min));
        }

        private void ApplyVisual()
        {
            if (track == null) return;

            Rect rect = track.rect;
            float width = rect.width;
            float lowN = Normalized(low);
            float highN = Normalized(high);

            if (lowHandle != null)
            {
                lowHandle.anchoredPosition = new Vector2(rect.xMin + lowN * width, lowHandle.anchoredPosition.y);
            }

            if (highHandle != null)
            {
                highHandle.anchoredPosition = new Vector2(rect.xMin + highN * width, highHandle.anchoredPosition.y);
            }

            if (fill != null)
            {
                float fillWidth = (highN - lowN) * width;
                fill.sizeDelta = new Vector2(fillWidth, fill.sizeDelta.y);
                fill.anchoredPosition = new Vector2(rect.xMin + (lowN + highN) * 0.5f * width, fill.anchoredPosition.y);
            }
        }
    }
}
