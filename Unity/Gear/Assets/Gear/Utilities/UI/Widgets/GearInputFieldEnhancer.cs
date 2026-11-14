using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 输入框增强：长度计数、提交策略（单行 Enter / 多行 Ctrl+Enter）、IME 组合态保护与提交清洗。
    /// <para>
    /// 只监听 <see cref="TMP_InputField"/> 的事件，不注册 <c>ITextPreprocessor</c>，也不改写 Text 内容，
    /// 因此与 <c>LanguagesSystem</c> / <c>UIText</c> 的翻译预处理互不干扰（顺序无关）。
    /// 反过来，不要把 <c>UIText</c> 挂在输入框的文本组件上——切语言会覆盖用户输入。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Input Field Enhancer")]
    [DisallowMultipleComponent]
    public sealed class GearInputFieldEnhancer : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private bool showCounter = true;
        [SerializeField, Min(0)] private int maxLength;
        [SerializeField] private string counterFormat = "{0}/{1}";
        [SerializeField] private bool trimOnSubmit = true;
        [SerializeField] private bool clearOnSubmit;
        [SerializeField] private bool allowEmptySubmit;
        [SerializeField] private UnityEvent<string> onSubmitted = new UnityEvent<string>();
        [SerializeField] private UnityEvent<int> onCounterChanged = new UnityEvent<int>();

        private bool bound;

        public UnityEvent<string> OnSubmitted => onSubmitted;
        public event Action<string> Submitted;

        /// <summary>
        /// IME 组合中（例如中文输入法还没上屏）；此时不提交。
        /// <para>
        /// TMP_InputField 没有公开组合态，默认走旧输入系统的 <c>Input.compositionString</c>；
        /// 新输入系统 / 自定义输入法可用 <see cref="CompositionProvider"/> 注入判断。
        /// </para>
        /// </summary>
        public bool IsComposing => CompositionProvider != null ? CompositionProvider() : LegacyInputComposing();

        /// <summary>组合态来源；为空时回退到旧输入系统。</summary>
        public Func<bool> CompositionProvider { get; set; }

        /// <summary>当前字符数（代理对算一个字符）。</summary>
        public int Count => CountCharacters(inputField != null ? inputField.text : string.Empty);

        public TMP_InputField InputField
        {
            get => inputField;
            set
            {
                Unbind();
                inputField = value;
                Bind();
                RefreshCounter();
            }
        }

        public TMP_Text CounterLabel
        {
            get => counterLabel;
            set { counterLabel = value; RefreshCounter(); }
        }

        public bool ShowCounter
        {
            get => showCounter;
            set { showCounter = value; RefreshCounter(); }
        }

        /// <summary>0 表示沿用 InputField 自己的 characterLimit。</summary>
        public int MaxLength
        {
            get => maxLength;
            set
            {
                maxLength = Mathf.Max(0, value);
                if (maxLength > 0 && inputField != null) inputField.characterLimit = maxLength;
                RefreshCounter();
            }
        }

        public string CounterFormat
        {
            get => counterFormat;
            set { counterFormat = string.IsNullOrEmpty(value) ? "{0}/{1}" : value; RefreshCounter(); }
        }

        public bool TrimOnSubmit { get => trimOnSubmit; set => trimOnSubmit = value; }
        public bool ClearOnSubmit { get => clearOnSubmit; set => clearOnSubmit = value; }
        public bool AllowEmptySubmit { get => allowEmptySubmit; set => allowEmptySubmit = value; }

        private void OnEnable()
        {
            Bind();
            RefreshCounter();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (bound || inputField == null) return;
            inputField.onValueChanged.AddListener(OnValueChanged);
            inputField.onSubmit.AddListener(OnSubmit);
            if (maxLength > 0) inputField.characterLimit = maxLength;
            bound = true;
        }

        private void Unbind()
        {
            if (!bound || inputField == null) return;
            inputField.onValueChanged.RemoveListener(OnValueChanged);
            inputField.onSubmit.RemoveListener(OnSubmit);
            bound = false;
        }

        /// <summary>外部按钮（确认键）也可直接调用。</summary>
        public void Submit()
        {
            if (inputField == null) return;
            OnSubmit(inputField.text);
        }

        /// <summary>立即刷新计数文本。</summary>
        public void RefreshCounter()
        {
            int limit = EffectiveLimit();
            if (counterLabel != null && showCounter)
            {
                counterLabel.text = FormatCounter(Count, limit, counterFormat);
            }
            else if (counterLabel != null)
            {
                counterLabel.text = string.Empty;
            }

            onCounterChanged?.Invoke(Count);
        }

        private int EffectiveLimit()
        {
            if (maxLength > 0) return maxLength;
            return inputField != null ? inputField.characterLimit : 0;
        }

        private void OnValueChanged(string _)
        {
            RefreshCounter();
        }

        private void OnSubmit(string value) => TrySubmit(value);

        /// <summary>按当前策略提交；被 IME 组合态或空文本拦截时返回 false。</summary>
        public bool TrySubmit(string value)
        {
            if (IsComposing) return false;
            if (!allowEmptySubmit && string.IsNullOrWhiteSpace(value)) return false;

            bool multiline = inputField != null && IsMultiline(inputField.lineType);
            string sanitized = Sanitize(value, trimOnSubmit, multiline);

            if (clearOnSubmit && inputField != null)
            {
                inputField.SetTextWithoutNotify(string.Empty);
            }

            RefreshCounter();
            onSubmitted?.Invoke(sanitized);
            Submitted?.Invoke(sanitized);
            return true;
        }

        private static bool LegacyInputComposing()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return !string.IsNullOrEmpty(Input.compositionString);
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }

        public static bool IsMultiline(TMP_InputField.LineType lineType)
        {
            return lineType == TMP_InputField.LineType.MultiLineNewline
                || lineType == TMP_InputField.LineType.MultiLineSubmit;
        }

        /// <summary>提交策略：组合中或空文本不提交；多行必须带 Ctrl/Cmd。</summary>
        public static bool ShouldSubmit(bool multiline, bool ctrlOrCmd, bool composing, string text)
        {
            if (composing || string.IsNullOrWhiteSpace(text)) return false;
            return !multiline || ctrlOrCmd;
        }

        /// <summary>按字符数截断（不会把代理对劈开）。</summary>
        public static string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrEmpty(text) || maxCharacters <= 0) return text ?? string.Empty;
            if (CountCharacters(text) <= maxCharacters) return text;

            int count = 0;
            int index = 0;
            while (index < text.Length && count < maxCharacters)
            {
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    index++;
                }

                index++;
                count++;
            }

            return text.Substring(0, index);
        }

        /// <summary>提交前清洗：单行把换行压成空格，可选 Trim。</summary>
        public static string Sanitize(string text, bool trim, bool multiline)
        {
            if (text == null) return string.Empty;
            if (!multiline) text = text.Replace("\r", " ").Replace("\n", " ");
            if (trim) text = text.Trim();
            return text;
        }

        /// <summary>字符数：代理对算一个字符。</summary>
        public static int CountCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                count++;
            }

            return count;
        }

        /// <summary>计数文本：limit≤0 时只显示当前字符数。</summary>
        public static string FormatCounter(int count, int limit, string format)
        {
            if (limit <= 0) return count.ToString();
            if (string.IsNullOrEmpty(format)) format = "{0}/{1}";
            return string.Format(format, count, limit);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxLength = Mathf.Max(0, maxLength);
            if (string.IsNullOrEmpty(counterFormat)) counterFormat = "{0}/{1}";
            RefreshCounter();
        }
#endif
    }
}
