using Gear.Utilities.UI.ImageExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 详情页实时调参：滑杆只通过明确的 ImageEx 适配器读写属性，不碰 SerializedObject。
    /// 面板本身是 Resources/UI/Showcase/ShowcaseCustomizer.prefab。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShowcaseCustomizer : MonoBehaviour
    {
        [SerializeField] private Slider outlineSlider;
        [SerializeField] private Slider glowSlider;
        [SerializeField] private Slider innerShadowSlider;
        [SerializeField] private Slider gradientSlider;
        [SerializeField] private TMP_Text outlineValue;
        [SerializeField] private TMP_Text glowValue;
        [SerializeField] private TMP_Text innerShadowValue;
        [SerializeField] private TMP_Text gradientValue;
        [SerializeField] private Button resetButton;

        private ImageEx target;
        private float initialOutline;
        private float initialGlow;
        private float initialInnerShadow;
        private float initialGradient;

        public ImageEx Target => target;

        public void Configure(Slider outline, Slider glow, Slider innerShadow, Slider gradient,
            TMP_Text outlineLabel, TMP_Text glowLabel, TMP_Text innerShadowLabel, TMP_Text gradientLabel,
            Button reset)
        {
            outlineSlider = outline;
            glowSlider = glow;
            innerShadowSlider = innerShadow;
            gradientSlider = gradient;
            outlineValue = outlineLabel;
            glowValue = glowLabel;
            innerShadowValue = innerShadowLabel;
            gradientValue = gradientLabel;
            resetButton = reset;
            Hook();
        }

        private void OnEnable()
        {
            Hook();
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void Hook()
        {
            if (outlineSlider != null)
            {
                outlineSlider.onValueChanged.RemoveListener(OnOutlineChanged);
                outlineSlider.onValueChanged.AddListener(OnOutlineChanged);
            }

            if (glowSlider != null)
            {
                glowSlider.onValueChanged.RemoveListener(OnGlowChanged);
                glowSlider.onValueChanged.AddListener(OnGlowChanged);
            }

            if (innerShadowSlider != null)
            {
                innerShadowSlider.onValueChanged.RemoveListener(OnInnerShadowChanged);
                innerShadowSlider.onValueChanged.AddListener(OnInnerShadowChanged);
            }

            if (gradientSlider != null)
            {
                gradientSlider.onValueChanged.RemoveListener(OnGradientChanged);
                gradientSlider.onValueChanged.AddListener(OnGradientChanged);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(ResetValues);
                resetButton.onClick.AddListener(ResetValues);
            }
        }

        private void Unhook()
        {
            if (outlineSlider != null) outlineSlider.onValueChanged.RemoveListener(OnOutlineChanged);
            if (glowSlider != null) glowSlider.onValueChanged.RemoveListener(OnGlowChanged);
            if (innerShadowSlider != null) innerShadowSlider.onValueChanged.RemoveListener(OnInnerShadowChanged);
            if (gradientSlider != null) gradientSlider.onValueChanged.RemoveListener(OnGradientChanged);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetValues);
        }

        /// <summary>绑定卡片根 ImageEx，并记录初始值用于 Reset。</summary>
        public void Bind(ImageEx image)
        {
            target = image;
            if (target == null) return;

            initialOutline = target.OutlineWidth;
            initialGlow = target.BorderGlowIntensity;
            initialInnerShadow = target.InnerShadowWidth;
            initialGradient = target.GradientScale;

            SetSlider(outlineSlider, initialOutline);
            SetSlider(glowSlider, initialGlow);
            SetSlider(innerShadowSlider, initialInnerShadow);
            SetSlider(gradientSlider, initialGradient);
            RefreshLabels();
        }

        private void OnOutlineChanged(float value)
        {
            if (target != null) target.OutlineWidth = value;
            RefreshLabels();
        }

        private void OnGlowChanged(float value)
        {
            if (target != null) target.BorderGlowIntensity = value;
            RefreshLabels();
        }

        private void OnInnerShadowChanged(float value)
        {
            if (target != null) target.InnerShadowWidth = value;
            RefreshLabels();
        }

        private void OnGradientChanged(float value)
        {
            if (target != null) target.GradientScale = value;
            RefreshLabels();
        }

        private void ResetValues()
        {
            if (target == null) return;
            target.OutlineWidth = initialOutline;
            target.BorderGlowIntensity = initialGlow;
            target.InnerShadowWidth = initialInnerShadow;
            target.GradientScale = initialGradient;
            Bind(target);
        }

        private void RefreshLabels()
        {
            if (outlineValue != null) outlineValue.text = (target != null ? target.OutlineWidth : 0f).ToString("0.0");
            if (glowValue != null) glowValue.text = (target != null ? target.BorderGlowIntensity : 0f).ToString("0.00");
            if (innerShadowValue != null) innerShadowValue.text = (target != null ? target.InnerShadowWidth : 0f).ToString("0");
            if (gradientValue != null) gradientValue.text = (target != null ? target.GradientScale : 0f).ToString("0.00");
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
        }
    }
}
