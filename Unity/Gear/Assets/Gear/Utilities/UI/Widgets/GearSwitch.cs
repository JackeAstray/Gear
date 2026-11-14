using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 开关：点击切换开/关，把手在两点之间缓动，背景色可在开/关色之间过渡。
    /// <para>
    /// 比原生 Toggle 多的是"把手位移 + 背景换色"的默认观感；状态变化走 <see cref="OnValueChanged"/>，
    /// 需要纯代码监听时用 <see cref="ValueChanged"/>。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Switch")]
    [DisallowMultipleComponent]
    public sealed class GearSwitch : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform handle;
        [SerializeField] private ImageEx background;
        [SerializeField] private Vector2 offPosition = new Vector2(-14f, 0f);
        [SerializeField] private Vector2 onPosition = new Vector2(14f, 0f);
        [SerializeField] private Color offColor = new Color(0.2f, 0.22f, 0.28f, 1f);
        [SerializeField] private Color onColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField, Min(0.01f)] private float duration = 0.12f;
        [SerializeField] private bool isOn;
        [SerializeField] private UnityEvent<bool> onValueChanged = new UnityEvent<bool>();

        private float progress;
        private float from;
        private float target;
        private float elapsed;
        private bool animating;

        public UnityEvent<bool> OnValueChanged => onValueChanged;
        public event Action<bool> ValueChanged;
        public bool IsOn => isOn;

        public RectTransform Handle
        {
            get => handle;
            set { handle = value; ApplyImmediate(); }
        }

        public ImageEx Background
        {
            get => background;
            set { background = value; ApplyImmediate(); }
        }

        public Vector2 OffPosition
        {
            get => offPosition;
            set => offPosition = value;
        }

        public Vector2 OnPosition
        {
            get => onPosition;
            set => onPosition = value;
        }

        public Color OffColor
        {
            get => offColor;
            set => offColor = value;
        }

        public Color OnColor
        {
            get => onColor;
            set => onColor = value;
        }

        private void OnEnable()
        {
            progress = isOn ? 1f : 0f;
            animating = false;
            ApplyImmediate();
        }

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Toggle();
        }

        /// <summary>切换开关。</summary>
        public void Toggle() => SetIsOn(!isOn, true);

        /// <summary>设置开关状态；notify=true 时派发事件。</summary>
        public void SetIsOn(bool value, bool notify = true)
        {
            if (isOn == value)
            {
                ApplyImmediate();
                return;
            }

            isOn = value;
            if (notify)
            {
                onValueChanged?.Invoke(value);
                ValueChanged?.Invoke(value);
            }

            from = progress;
            target = value ? 1f : 0f;
            elapsed = 0f;
            animating = true;
            if (!Application.isPlaying)
            {
                progress = target;
                animating = false;
                ApplyImmediate();
            }
        }

        /// <summary>手动推进（测试 / 暂停时驱动）。</summary>
        public void Step(float delta)
        {
            if (!animating || delta <= 0f) return;

            elapsed += delta;
            progress = Mathf.MoveTowards(from, target, elapsed / duration);
            ApplyImmediate();
            if (Mathf.Approximately(progress, target)) animating = false;
        }

        private void ApplyImmediate()
        {
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.Lerp(offPosition, onPosition, progress);
            }

            if (background != null)
            {
                background.color = Color.Lerp(offColor, onColor, progress);
            }
        }
    }
}
