using System;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>
    /// 把 <see cref="ImageEx"/> 的填充/描边/辉光绑定到 <see cref="GearInteractive"/> 的五态，
    /// 让卡片、按钮、列表项在没有 Selectable 的情况下也能获得 hover / press / selected 视觉。
    /// <para>
    /// 与 <c>ButtonAni</c> 的分工：ButtonAni 是 Button 专用、重写状态过渡；本组件面向 ImageEx，
    /// 只改材质属性（颜色 / 描边 / 辉光），不缩放、不改布局，适合图标卡片与大面板。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/ImageEx State Colors")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageEx))]
    public sealed class ImageExStateColors : MonoBehaviour
    {
        /// <summary>单态视觉。</summary>
        [Serializable]
        public struct StateStyle
        {
            public Color Fill;
            public Color Outline;
            public Color Glow;
            [Range(0f, 1f)] public float GlowIntensity;
        }

        [SerializeField] private ImageEx target;
        [SerializeField] private GearInteractive interactive;

        [SerializeField] private StateStyle normal = new StateStyle
        {
            Fill = Color.white,
            Outline = new Color(1f, 1f, 1f, 0f),
            Glow = Color.white,
            GlowIntensity = 0f,
        };

        [SerializeField] private StateStyle highlighted = new StateStyle
        {
            Fill = Color.white,
            Outline = new Color(1f, 1f, 1f, 0.35f),
            Glow = new Color(0.49f, 0.36f, 0.99f, 1f),
            GlowIntensity = 0.35f,
        };

        [SerializeField] private StateStyle pressed = new StateStyle
        {
            Fill = new Color(0.92f, 0.92f, 0.92f, 1f),
            Outline = new Color(1f, 1f, 1f, 0.55f),
            Glow = new Color(0.49f, 0.36f, 0.99f, 1f),
            GlowIntensity = 0.6f,
        };

        [SerializeField] private StateStyle selected = new StateStyle
        {
            Fill = Color.white,
            Outline = new Color(0.49f, 0.36f, 0.99f, 1f),
            Glow = new Color(0.49f, 0.36f, 0.99f, 1f),
            GlowIntensity = 0.5f,
        };

        [SerializeField] private StateStyle disabled = new StateStyle
        {
            Fill = new Color(0.6f, 0.6f, 0.6f, 0.45f),
            Outline = new Color(1f, 1f, 1f, 0.05f),
            Glow = Color.white,
            GlowIntensity = 0f,
        };

        [SerializeField] private bool applyOutline = true;
        [SerializeField] private bool applyGlow = true;
        [SerializeField] private float transitionDuration = 0.12f;
        [SerializeField] private bool useUnscaledTime = true;

        private Color currentFill;
        private Color currentOutline;
        private Color currentGlow;
        private float currentGlowIntensity;
        private bool hasCurrent;

        private bool transitioning;
        private float transitionStart;
        private float transitionLength;
        private StateStyle transitionFrom;
        private StateStyle transitionTo;

        public ImageEx Target
        {
            get
            {
                if (target == null) target = GetComponent<ImageEx>();
                return target;
            }
            set => target = value;
        }

        public GearInteractive Interactive
        {
            get
            {
                if (interactive == null) interactive = GetComponent<GearInteractive>();
                return interactive;
            }
            set
            {
                if (interactive == value) return;
                Unsubscribe();
                interactive = value;
                Subscribe();
            }
        }

        /// <summary>
        /// 重新解析 Target / Interactive 并订阅状态事件。
        /// 运行时由 OnEnable 自动调用；编辑器预览与 EditMode 测试可手动调用
        /// （编辑模式下普通 MonoBehaviour 的 OnEnable 不会执行）。
        /// </summary>
        public void Bind()
        {
            target = GetComponent<ImageEx>();
            interactive = GetComponent<GearInteractive>();
            Subscribe();

            GearInteractive current = interactive;
            ApplyState(current != null ? current.State : GearInteractionState.Normal, true);
        }

        /// <summary>状态切换的过渡时长（秒）；0 表示立即切换。</summary>
        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = Mathf.Max(0f, value);
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unsubscribe();
            transitioning = false;
        }

        private void Subscribe()
        {
            GearInteractive current = Interactive;
            if (current == null) return;
            current.StateChanged -= HandleStateChanged;
            current.StateChanged += HandleStateChanged;
        }

        private void Unsubscribe()
        {
            if (interactive != null) interactive.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GearInteractionState state)
        {
            ApplyState(state, false);
        }

        /// <summary>读取某态的配置。</summary>
        public StateStyle GetStyle(GearInteractionState state)
        {
            switch (state)
            {
                case GearInteractionState.Highlighted: return highlighted;
                case GearInteractionState.Pressed: return pressed;
                case GearInteractionState.Selected: return selected;
                case GearInteractionState.Disabled: return disabled;
                default: return normal;
            }
        }

        /// <summary>写入某态的配置；若正好处于该态，会按当前过渡设置重新应用。</summary>
        public void SetStyle(GearInteractionState state, StateStyle style)
        {
            switch (state)
            {
                case GearInteractionState.Highlighted: highlighted = style; break;
                case GearInteractionState.Pressed: pressed = style; break;
                case GearInteractionState.Selected: selected = style; break;
                case GearInteractionState.Disabled: disabled = style; break;
                default: normal = style; break;
            }

            GearInteractive current = Interactive;
            if (current != null && current.State == state)
                ApplyState(state, false);
        }

        /// <summary>
        /// 应用状态色。instant=true 时立即写入（编辑预览、初始化、测试使用）。
        /// </summary>
        public void ApplyState(GearInteractionState state, bool instant = false)
        {
            ImageEx image = Target;
            if (image == null) return;

            StateStyle style = GetStyle(state);
            if (instant || transitionDuration <= 0f || !Application.isPlaying || !hasCurrent)
            {
                hasCurrent = true;
                transitioning = false;
                ApplyVisual(style.Fill, style.Outline, style.Glow, style.GlowIntensity, image);
                return;
            }

            transitionFrom = new StateStyle
            {
                Fill = currentFill,
                Outline = currentOutline,
                Glow = currentGlow,
                GlowIntensity = currentGlowIntensity,
            };
            transitionTo = style;
            transitionStart = useUnscaledTime ? Time.unscaledTime : Time.time;
            transitionLength = Mathf.Max(0.0001f, transitionDuration);
            transitioning = true;
        }

        private void Update()
        {
            if (!transitioning) return;

            ImageEx image = Target;
            if (image == null)
            {
                transitioning = false;
                return;
            }

            float now = useUnscaledTime ? Time.unscaledTime : Time.time;
            float t = Mathf.Clamp01((now - transitionStart) / transitionLength);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            Color fill = Color.Lerp(transitionFrom.Fill, transitionTo.Fill, eased);
            Color outline = Color.Lerp(transitionFrom.Outline, transitionTo.Outline, eased);
            Color glow = Color.Lerp(transitionFrom.Glow, transitionTo.Glow, eased);
            float intensity = Mathf.Lerp(transitionFrom.GlowIntensity, transitionTo.GlowIntensity, eased);
            ApplyVisual(fill, outline, glow, intensity, image);

            if (t >= 1f)
            {
                transitioning = false;
                ApplyVisual(transitionTo.Fill, transitionTo.Outline, transitionTo.Glow,
                    transitionTo.GlowIntensity, image);
            }
        }

        private void ApplyVisual(Color fill, Color outline, Color glow, float glowIntensity, ImageEx image)
        {
            currentFill = fill;
            currentOutline = outline;
            currentGlow = glow;
            currentGlowIntensity = glowIntensity;
            hasCurrent = true;

            image.color = fill;
            if (applyOutline) image.OutlineColor = outline;
            if (applyGlow)
            {
                image.BorderGlowColor = glow;
                image.BorderGlowIntensity = glowIntensity;
                bool enable = glowIntensity > 0.001f;
                if (image.EnableBorderGlow != enable) image.EnableBorderGlow = enable;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            transitionDuration = Mathf.Max(0f, transitionDuration);
            if (target == null) target = GetComponent<ImageEx>();
            if (interactive == null) interactive = GetComponent<GearInteractive>();
        }
#endif
    }
}
