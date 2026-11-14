using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 数字滚动：把当前显示值平滑过渡到目标值，支持格式化和立即赋值。
    /// <para>适合金币、分数、伤害数字；使用 unscaled time，暂停菜单里也能完成。</para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Counter")]
    public sealed class GearCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float value;
        [SerializeField, Min(0f)] private float duration = 0.6f;
        [SerializeField] private string format = "{0:N0}";
        [SerializeField] private bool autoPlayOnEnable;

        private float current;
        private float start;
        private float target;
        private float elapsed;
        private bool animating;

        public float Value => value;
        public TMP_Text Label => label;
        public string Format { get => format; set => format = value; }

        private void OnEnable()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            target = value;

            if (autoPlayOnEnable)
            {
                current = 0f;
                start = 0f;
                elapsed = 0f;
                animating = true;
                Apply(0f);
            }
            else
            {
                current = value;
                animating = false;
                Apply(value);
            }
        }

        /// <summary>运行时绑定标签。</summary>
        public void Bind(TMP_Text targetLabel)
        {
            label = targetLabel;
            Apply(current);
        }

        /// <summary>设置目标值；immediate=true 时跳过滚动。</summary>
        public void SetValue(float newValue, bool immediate = false)
        {
            value = newValue;
            target = newValue;

            if (immediate || duration <= 0f)
            {
                current = target;
                animating = false;
                Apply(current);
                return;
            }

            start = current;
            elapsed = 0f;
            animating = true;
        }

        public void SetValueImmediate(float newValue) => SetValue(newValue, true);

        public void SetFormat(string newFormat)
        {
            format = newFormat;
            Apply(current);
        }

        private void Update()
        {
            if (!animating) return;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(duration, 1e-4f));
            current = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            Apply(current);

            if (t >= 1f)
            {
                current = target;
                animating = false;
                Apply(target);
            }
        }

        private void Apply(float v)
        {
            if (label != null)
                label.text = string.Format(format, v);
        }
    }
}
