using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 墨团揭示：从指定原点向外扩散一团带噪声毛边的墨迹，逐渐显示整个 ImageEx。
    /// <para>
    /// 只驱动 ImageEx 的 InkPanel uniform；支持手动进度、播放一次、自动往返。
    /// 适合章节标题、地图解锁、剧情遮罩。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Ink Panel")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageEx))]
    public sealed class GearInkPanel : MonoBehaviour
    {
        [SerializeField] private Vector2 origin = new Vector2(0.5f, 0.5f);
        [SerializeField, Range(0.001f, 0.5f)] private float softness = 0.08f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.35f;
        [SerializeField, Min(0.01f)] private float duration = 0.7f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool startVisible = true;
        [SerializeField] private bool loop;
        [SerializeField, Min(0.05f)] private float loopInterval = 1.2f;

        private ImageEx image;
        private float current = 1f;
        private float target = 1f;
        private bool animating;
        private float wait = -1f;

        /// <summary>当前进度（0 隐藏，1 显示）。</summary>
        public float Progress => current;

        public Vector2 Origin
        {
            get => origin;
            set { origin = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y)); ApplySettings(); }
        }

        public float Softness
        {
            get => softness;
            set { softness = Mathf.Clamp(value, 0.001f, 0.5f); ApplySettings(); }
        }

        public float Roughness
        {
            get => roughness;
            set { roughness = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.01f, value);
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void OnEnable()
        {
            ApplySettings();
            if (!playOnEnable) return;

            SetProgress(startVisible ? 1f : 0f);
            if (loop) wait = loopInterval;
        }

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>播放显示。</summary>
        public void Show()
        {
            ApplySettings();
            target = 1f;
            animating = true;
            wait = -1f;
        }

        /// <summary>播放隐藏。</summary>
        public void Hide()
        {
            ApplySettings();
            target = 0f;
            animating = true;
            wait = -1f;
        }

        /// <summary>直接设置进度（0-1），不播放动画。</summary>
        public void SetProgress(float progress)
        {
            current = Mathf.Clamp01(progress);
            target = current;
            animating = false;
            wait = -1f;
            ApplySettings();
            if (image != null) image.InkProgress = current;
        }

        /// <summary>手动推进（测试 / 暂停时驱动）。</summary>
        public void Step(float delta)
        {
            if (delta <= 0f) return;
            ApplySettings();
            if (image == null) return;

            if (animating)
            {
                current = Mathf.MoveTowards(current, target, delta / duration);
                image.InkProgress = current;

                if (Mathf.Approximately(current, target))
                {
                    animating = false;
                    if (loop) wait = loopInterval;
                }
                return;
            }

            if (!loop || wait < 0f) return;

            wait -= delta;
            if (wait > 0f) return;

            if (current >= 1f)
                Hide();
            else
                Show();
        }

        /// <summary>把组件配置写给 ImageEx；编辑器/测试可手动调用。</summary>
        public void ApplySettings()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (image == null) return;

            image.EnableInkPanel = true;
            image.InkOrigin = origin;
            image.InkSoftness = softness;
            image.InkRoughness = roughness;
            image.InkProgress = current;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            softness = Mathf.Clamp(softness, 0.001f, 0.5f);
            roughness = Mathf.Clamp01(roughness);
            duration = Mathf.Max(0.01f, duration);
        }
#endif
    }
}
