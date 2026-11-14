using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 竖向滚轮选择（Picker）：拖动 / 鼠标滚轮 / 键盘手柄上下切换，松手吸附到最近项，可循环。
    /// <para>
    /// <see cref="Content"/> 是滚动容器：它的 anchoredPosition.y 等于 <see cref="Scroll"/>，
    /// 值为 0 时第 0 项落在滚轮中心；第 i 项应放在 content 局部坐标 y = -i * ItemHeight。
    /// </para>
    /// <para>
    /// 手柄/键盘：上下方向键 = 上一项/下一项，确认键 = 确认当前项（派发 settled 事件）。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Picker Wheel")]
    [DisallowMultipleComponent]
    public sealed class GearPickerWheel : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler,
        IPointerDownHandler, ISubmitHandler, IMoveHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text selectedLabel;
        [SerializeField] private List<string> options = new List<string>();
        [SerializeField] private int value;
        [SerializeField, Min(1f)] private float itemHeight = 40f;
        [SerializeField, Min(0.01f)] private float snapSpeed = 14f;
        [SerializeField, Min(0f)] private float dragScale = 1f;
        [SerializeField] private bool wrap;
        [SerializeField] private bool inertia = true;
        [SerializeField, Min(0f)] private float flingScale = 0.15f;
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> onSettled = new UnityEvent<int>();

        private float scroll;
        private bool dragging;
        private bool settling;
        private float dragVelocity;

        public UnityEvent<int> OnValueChanged => onValueChanged;
        public UnityEvent<int> OnSettled => onSettled;
        public event Action<int> ValueChanged;
        public event Action<int> Settled;

        public IReadOnlyList<string> Options => options;
        public int Count => options.Count;
        public int Value => value;
        public float Scroll => scroll;
        public bool IsDragging => dragging;
        public bool IsSettled => !dragging && !settling;
        public float TargetScroll => value * itemHeight;

        public RectTransform Content
        {
            get => content;
            set { content = value; ApplyContent(); }
        }

        /// <summary>可选：显示当前项的文本。</summary>
        public TMP_Text SelectedLabel
        {
            get => selectedLabel;
            set { selectedLabel = value; RefreshLabel(); }
        }

        public bool Wrap
        {
            get => wrap;
            set
            {
                wrap = value;
                SnapTo(this.value);
            }
        }

        public bool Inertia { get => inertia; set => inertia = value; }
        public float DragScale { get => dragScale; set => dragScale = Mathf.Max(0f, value); }
        public float SnapSpeed { get => snapSpeed; set => snapSpeed = Mathf.Max(0.01f, value); }

        public float ItemHeight
        {
            get => itemHeight;
            set
            {
                itemHeight = Mathf.Max(1f, value);
                SnapTo(this.value);
            }
        }

        private void OnEnable()
        {
            SnapTo(value);
        }

        private void OnDisable()
        {
            dragging = false;
            settling = false;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>替换选项；keepValue=false 时回到第 0 项。</summary>
        public void SetOptions(IList<string> items, bool keepValue = true)
        {
            options.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++) options.Add(items[i] ?? string.Empty);
            }

            value = keepValue ? ClampIndex(value) : 0;
            scroll = TargetScroll;
            settling = false;
            ApplyContent();
            RefreshLabel();
        }

        /// <summary>选中第 index 项并吸附过去；notify=true 时派发事件。</summary>
        public void SetValue(int index, bool notify = true)
        {
            int next = ClampIndex(index);
            bool changed = next != value;
            value = next;
            RefreshLabel();
            settling = true;
            if (changed && notify) NotifyValue();
        }

        /// <summary>选中第 index 项但不派发事件。</summary>
        public void SetValueWithoutNotify(int index) => SetValue(index, false);

        /// <summary>立刻把滚动位置钳到目标（不做动画）。</summary>
        public void SnapTo(int index)
        {
            value = ClampIndex(index);
            scroll = TargetScroll;
            settling = false;
            ApplyContent();
            RefreshLabel();
        }

        /// <summary>前进一步 / 后退一步（自动处理循环与边界）。</summary>
        public void Step(int delta)
        {
            if (options.Count == 0) return;
            SetValue(value + delta);
        }

        /// <summary>确认当前项：结束吸附并派发 settled 事件（手柄确认键）。</summary>
        public void Confirm()
        {
            settling = false;
            FireSettled();
        }

        /// <summary>拖动位移（像素，正数 = 往下拖/看更靠后的项）；测试可直接调用。</summary>
        public void ApplyScrollDelta(float pixels)
        {
            if (options.Count == 0) return;
            scroll += pixels * Mathf.Max(0f, dragScale);
            NormalizeScroll();
            ApplyContent();

            int nearest = NearestIndex(scroll);
            if (nearest == value) return;
            value = nearest;
            RefreshLabel();
            NotifyValue();
        }

        /// <summary>结束拖动；按下时的速度可能带一点惯性，最终吸附到最近项。</summary>
        public void ReleaseDrag(float velocityPixelsPerSecond = 0f)
        {
            dragging = false;
            if (options.Count == 0) return;

            float projected = scroll;
            if (inertia && Mathf.Abs(velocityPixelsPerSecond) > 10f)
            {
                projected += velocityPixelsPerSecond * Mathf.Max(0f, flingScale);
            }

            int nearest = NearestIndex(projected);
            bool changed = nearest != value;
            value = nearest;
            settling = true;
            RefreshLabel();
            if (changed) NotifyValue();
        }

        /// <summary>推进吸附动画；正常走 Update，测试里手动驱动。</summary>
        public void Tick(float deltaTime)
        {
            if (dragging || options.Count == 0) return;

            float target = TargetScroll;
            if (wrap) target = NearestEquivalentTarget(target);

            if (Mathf.Abs(target - scroll) <= 0.25f)
            {
                scroll = target;
                ApplyContent();
                if (settling)
                {
                    settling = false;
                    FireSettled();
                }

                return;
            }

            float speed = Mathf.Max(0.01f, snapSpeed);
            float t = 1f - Mathf.Exp(-speed * Mathf.Max(0f, deltaTime));
            scroll = Mathf.Lerp(scroll, target, t);
            if (Mathf.Abs(target - scroll) < 0.25f) scroll = target;
            ApplyContent();
        }

        // ---------------- 指针 / 键盘 ----------------

        public void OnPointerDown(PointerEventData eventData)
        {
            dragging = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            settling = false;
            dragVelocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            float delta = eventData.delta.y;
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            dragVelocity = Mathf.Lerp(dragVelocity, delta / dt, 0.35f);
            ApplyScrollDelta(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ReleaseDrag(inertia ? dragVelocity : 0f);
            dragVelocity = 0f;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Abs(eventData.scrollDelta.y) < 0.01f) return;
            Step(eventData.scrollDelta.y > 0f ? -1 : 1);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Confirm();
        }

        public void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Up:
                case MoveDirection.Left:
                    Step(-1);
                    break;
                case MoveDirection.Down:
                case MoveDirection.Right:
                    Step(1);
                    break;
            }
        }

        // ---------------- 内部 ----------------

        private int ClampIndex(int index)
        {
            if (options.Count == 0) return 0;
            if (wrap) return ((index % options.Count) + options.Count) % options.Count;
            return Mathf.Clamp(index, 0, options.Count - 1);
        }

        private int NearestIndex(float position)
        {
            if (options.Count == 0) return 0;
            float step = Mathf.Max(1f, itemHeight);
            return ClampIndex(Mathf.RoundToInt(position / step));
        }

        private float NearestEquivalentTarget(float target)
        {
            float total = options.Count * Mathf.Max(1f, itemHeight);
            if (total <= 0f) return target;
            while (target - scroll > total * 0.5f) target -= total;
            while (target - scroll < -total * 0.5f) target += total;
            return target;
        }

        private void NormalizeScroll()
        {
            float step = Mathf.Max(1f, itemHeight);
            if (wrap)
            {
                float total = options.Count * step;
                if (total > 0f) scroll = ((scroll % total) + total) % total;
            }
            else
            {
                scroll = Mathf.Clamp(scroll, 0f, Mathf.Max(0f, (options.Count - 1) * step));
            }
        }

        private void ApplyContent()
        {
            if (content == null) return;
            Vector2 position = content.anchoredPosition;
            position.y = scroll;
            content.anchoredPosition = position;
        }

        private void RefreshLabel()
        {
            if (selectedLabel == null) return;
            selectedLabel.text = options.Count == 0 ? string.Empty : options[Mathf.Clamp(value, 0, options.Count - 1)];
        }

        private void NotifyValue()
        {
            onValueChanged?.Invoke(value);
            ValueChanged?.Invoke(value);
        }

        private void FireSettled()
        {
            onSettled?.Invoke(value);
            Settled?.Invoke(value);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            itemHeight = Mathf.Max(1f, itemHeight);
            snapSpeed = Mathf.Max(0.01f, snapSpeed);
            dragScale = Mathf.Max(0f, dragScale);
            flingScale = Mathf.Max(0f, flingScale);
            value = ClampIndex(value);
            scroll = TargetScroll;
            ApplyContent();
            RefreshLabel();
        }
#endif
    }
}
