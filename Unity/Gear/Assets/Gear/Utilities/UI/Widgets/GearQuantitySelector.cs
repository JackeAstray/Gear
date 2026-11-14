using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 数量选择：减号 / 数值 / 加号，支持 min/max/step、循环与格式化文本。
    /// <para>到达边界时自动禁用对应按钮（循环模式除外），并派发当前值。</para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Quantity Selector")]
    [DisallowMultipleComponent]
    public sealed class GearQuantitySelector : MonoBehaviour
    {
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private TMP_Text label;
        [SerializeField] private int min;
        [SerializeField] private int max = 99;
        [SerializeField] private int step = 1;
        [SerializeField] private int value = 1;
        [SerializeField] private bool wrap;
        [SerializeField] private string format = "{0}";
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();

        private UnityAction minusHandler;
        private UnityAction plusHandler;
        private bool bound;

        public UnityEvent<int> OnValueChanged => onValueChanged;
        public int Value => value;

        /// <summary>数值文本；为空时不影响逻辑。</summary>
        public TMP_Text Label
        {
            get => label;
            set { label = value; Refresh(); }
        }

        /// <summary>减号按钮；赋值后自动绑定点击。</summary>
        public Button MinusButton
        {
            get => minusButton;
            set
            {
                Unbind();
                minusButton = value;
                Bind();
                Refresh();
            }
        }

        /// <summary>加号按钮；赋值后自动绑定点击。</summary>
        public Button PlusButton
        {
            get => plusButton;
            set
            {
                Unbind();
                plusButton = value;
                Bind();
                Refresh();
            }
        }

        /// <summary>是否循环。</summary>
        public bool Wrap
        {
            get => wrap;
            set { wrap = value; Refresh(); }
        }

        /// <summary>文本格式，例如 "{0} 件"。</summary>
        public string Format
        {
            get => format;
            set { format = string.IsNullOrEmpty(value) ? "{0}" : value; Refresh(); }
        }
        public int Min => min;
        public int Max => max;
        public int Step => step;

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>配置范围与初始值；notify=true 时派发初始值。</summary>
        public void Configure(int newMin, int newMax, int newValue, bool notify = false)
        {
            min = newMin;
            max = Mathf.Max(newMin, newMax);
            step = Mathf.Max(1, step);
            value = Mathf.Clamp(newValue, min, max);
            Refresh();
            if (notify) onValueChanged?.Invoke(value);
        }

        /// <summary>直接设值；越界会钳制到 min/max。</summary>
        public void SetValue(int newValue, bool notify = true)
        {
            newValue = Mathf.Clamp(newValue, min, max);
            bool changed = newValue != value;
            value = newValue;
            Refresh();
            if (changed && notify) onValueChanged?.Invoke(value);
        }

        /// <summary>加一步。</summary>
        public void Increment() => Move(step);

        /// <summary>减一步。</summary>
        public void Decrement() => Move(-step);

        public void SetValueWithoutNotify(int newValue) => SetValue(newValue, false);

        private void Move(int delta)
        {
            int next = value + delta;
            if (wrap && max > min)
            {
                int span = max - min + 1;
                next = min + ((next - min) % span + span) % span;
            }

            SetValue(next);
        }

        private void Bind()
        {
            if (bound) return;
            minusHandler = Decrement;
            plusHandler = Increment;
            if (minusButton != null) minusButton.onClick.AddListener(minusHandler);
            if (plusButton != null) plusButton.onClick.AddListener(plusHandler);
            bound = true;
        }

        private void Unbind()
        {
            if (!bound) return;
            if (minusButton != null && minusHandler != null) minusButton.onClick.RemoveListener(minusHandler);
            if (plusButton != null && plusHandler != null) plusButton.onClick.RemoveListener(plusHandler);
            bound = false;
        }

        private void Refresh()
        {
            if (label != null) label.text = string.Format(format, value);
            if (!wrap)
            {
                if (minusButton != null) minusButton.interactable = value > min;
                if (plusButton != null) plusButton.interactable = value < max;
            }
            else
            {
                if (minusButton != null) minusButton.interactable = true;
                if (plusButton != null) plusButton.interactable = true;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            max = Mathf.Max(min, max);
            step = Mathf.Max(1, step);
            value = Mathf.Clamp(value, min, max);
            Refresh();
        }
#endif
    }
}
