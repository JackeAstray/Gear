using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 进度条：把 0..1 的值映射到 fill 的横向 anchor，可平滑过渡并同步格式化标签。
    /// fill 建议 anchorMin.x=0、anchorMax.x=1，本组件只改 anchorMax.x。
    /// </summary>
    [AddComponentMenu("UI/Gear/Progress Bar")]
    public sealed class GearProgressBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text label;

        [SerializeField, Range(0f, 1f)] private float value = 1f;
        [SerializeField, Min(0f)] private float smoothTime = 0.2f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private string format = "{0:P0}";

        private float current;
        private float target;

        public float Value => value;
        public RectTransform Fill => fill;
        public TMP_Text Label => label;
        public string Format { get => format; set => format = value; }

        private void OnEnable()
        {
            current = value;
            target = value;
            Apply(value);
        }

        /// <summary>运行时绑定 fill 与标签，并立即应用当前值。</summary>
        public void Bind(RectTransform fillRect, TMP_Text valueLabel = null)
        {
            fill = fillRect;
            label = valueLabel;
            Apply(value);
        }

        /// <summary>设置目标值；immediate=true 时跳过平滑。</summary>
        public void SetValue(float newValue, bool immediate = false)
        {
            value = Mathf.Clamp01(newValue);
            target = value;

            if (immediate || smoothTime <= 0f)
            {
                current = target;
                Apply(current);
            }
        }

        public void SetValueImmediate(float newValue) => SetValue(newValue, true);

        public void SetFormat(string newFormat)
        {
            format = newFormat;
            Apply(current);
        }

        private void Update()
        {
            if (fill == null || Mathf.Approximately(current, target)) return;

            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            current = Mathf.MoveTowards(current, target, Mathf.Max(delta / Mathf.Max(smoothTime, 1e-4f), 1e-4f));
            Apply(current);
        }

        private void Apply(float v)
        {
            if (fill != null)
            {
                Vector2 min = fill.anchorMin;
                Vector2 max = fill.anchorMax;
                min.x = 0f;
                max.x = Mathf.Clamp01(v);
                fill.anchorMin = min;
                fill.anchorMax = max;
            }

            if (label != null)
                label.text = string.Format(format, Mathf.Clamp01(v));
        }
    }
}
