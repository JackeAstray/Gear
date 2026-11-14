using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 像素幕帘转场：把 ImageEx 切成格子，按方向 + 逐格随机延迟揭示/隐藏。
    /// <para>
    /// 只驱动 ImageEx 的 Curtain uniform；支持手动进度、播放一次、自动往返循环。
    /// 适合弹窗出现、卡片切换、加载遮罩。
    /// </para>
    /// </summary>
    public enum GearCurtainDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3,
    }

    [AddComponentMenu("UI/Gear/Pixel Curtain")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageEx))]
    public sealed class GearPixelCurtain : MonoBehaviour
    {
        [SerializeField] private float cell = 24f;
        [SerializeField, Range(0f, 1f)] private float randomness = 0.3f;
        [SerializeField, Range(0f, 1f)] private float softness = 0.08f;
        [SerializeField] private GearCurtainDirection direction = GearCurtainDirection.LeftToRight;
        [SerializeField, Min(0.01f)] private float duration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
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

        public float Cell
        {
            get => cell;
            set { cell = Mathf.Max(2f, value); ApplySettings(); }
        }

        public float Randomness
        {
            get => randomness;
            set { randomness = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float Softness
        {
            get => softness;
            set { softness = Mathf.Clamp01(value); ApplySettings(); }
        }

        public GearCurtainDirection Direction
        {
            get => direction;
            set { direction = value; ApplySettings(); }
        }

        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.01f, value);
        }

        private void Awake()
        {
            current = Mathf.Clamp01(progress);
            ApplySettings();
        }

        private void OnEnable()
        {
            current = Mathf.Clamp01(progress);
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
            this.progress = Mathf.Clamp01(progress);
            current = this.progress;
            target = current;
            animating = false;
            wait = -1f;
            ApplySettings();
            if (image != null) image.CurtainProgress = current;
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
                image.CurtainProgress = current;

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

        /// <summary>把组件配置写给 ImageEx（运行时由 Awake/OnEnable 调用，编辑/测试可手动调用）。</summary>
        public void ApplySettings()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (image == null) return;

            image.EnablePixelCurtain = true;
            image.CurtainCell = cell;
            image.CurtainRandomness = randomness;
            image.CurtainSoftness = softness;
            image.CurtainDirection = (int)direction;
            image.CurtainProgress = current;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cell = Mathf.Max(2f, cell);
            randomness = Mathf.Clamp01(randomness);
            softness = Mathf.Clamp01(softness);
            duration = Mathf.Max(0.01f, duration);
            progress = Mathf.Clamp01(progress);
            current = progress;

            if (image == null) image = GetComponent<ImageEx>();
            if (image != null) ApplySettings();
        }
#endif
    }
}
