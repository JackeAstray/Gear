using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 前后对比滑杆：拖动分隔线，左侧 before 宽度 = value，右侧 after 宽度 = 1-value。
    /// </summary>
    [AddComponentMenu("UI/Gear/Comparison Slider")]
    [DisallowMultipleComponent]
    public sealed class GearComparisonSlider : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform handle;
        [SerializeField] private RectTransform before;
        [SerializeField] private RectTransform after;
        [SerializeField, Range(0f, 1f)] private float value = 0.5f;
        [SerializeField] private UnityEvent<float> onValueChanged = new UnityEvent<float>();

        private bool dragging;

        public UnityEvent<float> OnValueChanged => onValueChanged;
        public event Action<float> ValueChanged;
        public float Value => value;
        public RectTransform Handle { get => handle; set { handle = value; ApplyVisual(); } }
        public RectTransform Track { get => track; set { track = value; ApplyVisual(); } }
        public RectTransform Before { get => before; set { before = value; ApplyVisual(); } }
        public RectTransform After { get => after; set { after = value; ApplyVisual(); } }

        private void OnEnable() => ApplyVisual();

        public void SetValue(float normalized, bool notify = true)
        {
            float next = Mathf.Clamp01(normalized);
            bool changed = !Mathf.Approximately(next, value);
            value = next;
            ApplyVisual();
            if (changed && notify)
            {
                onValueChanged?.Invoke(value);
                ValueChanged?.Invoke(value);
            }
        }

        public void ApplyPointer01(float normalized01) => SetValue(normalized01);

        public void OnPointerDown(PointerEventData eventData)
        {
            dragging = true;
            Drag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData) => dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;

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
            SetValue((local.x - rect.xMin) / rect.width);
        }

        private void ApplyVisual()
        {
            RectTransform target = track != null ? track : before;
            if (target == null) return;

            float width = target.rect.width;
            float left = target.rect.xMin;
            float split = width * value;

            if (handle != null)
                handle.anchoredPosition = new Vector2(left + split, handle.anchoredPosition.y);

            if (before != null)
            {
                before.sizeDelta = new Vector2(split, before.sizeDelta.y);
                before.anchoredPosition = new Vector2(left + split * 0.5f, before.anchoredPosition.y);
            }

            if (after != null)
            {
                after.sizeDelta = new Vector2(Mathf.Max(0f, width - split), after.sizeDelta.y);
                after.anchoredPosition = new Vector2(left + split + (width - split) * 0.5f, after.anchoredPosition.y);
            }
        }
    }
}
