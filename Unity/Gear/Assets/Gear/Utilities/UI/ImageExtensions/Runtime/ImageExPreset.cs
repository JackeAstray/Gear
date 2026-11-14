using UnityEngine;

using Gear.Common;
namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 效果预设：把 <b>大部分</b> Phase 1-3 效果参数保存为 ScriptableObject，
    /// 可在 ImageEx 组件之间共享，也可用于 ImageExTweener 的起止状态。
    /// <para>
    /// 未覆盖的字段：Sprite、材质、相机画面、Raycast 事件；这些属于“资源绑定”而不是“效果参数”。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gear/ImageEx Preset", fileName = "ImageExPreset")]
    public partial class ImageExPreset : ScriptableObject
    {
        [Header("渐变")]
        public bool enableGradientTex;
        public Texture gradientTex;
        [Range(-1, 1)] public float gradientOffset;
        [Range(0.1f, 5)] public float gradientScale = 1f;

        [Header("模糊")]
        public ImageEx.BlurType blurType;
        [Range(0, 1)] public float blurIntensity = 1f;

        [Header("采样增强")]
        public ImageEx.SamplingFilter samplingMode;
        [Range(0, 1)] public float samplingIntensity = 0.5f;
        // 采样细节（此前缺失，同阴影段）
        public float samplingWidth = 1f;
        public float samplingScale = 1f;

        [Header("目标模式")]
        public ImageEx.TargetMode targetMode;
        public Color targetColor = Color.white;
        [Range(0, 1)] public float targetRange = 0.1f;
        [Range(0, 1)] public float targetSoftness = 0.5f;

        [Header("色调滤镜")]
        public ImageEx.ToneFilter toneFilter;
        [Range(0, 1)] public float toneIntensity = 1f;

        [Header("颜色滤镜")]
        public ImageEx.ColorMode colorFilterMode;
        public Color colorValue = Color.white;
        [Range(0, 1)] public float colorIntensity = 1f;
        public bool colorGlow;

        [Header("过渡")]
        public ImageEx.TransitionMode transitionMode;
        public Texture transitionTexture;
        public Vector2 transitionTexScale = Vector2.one;
        public Vector2 transitionTexOffset = Vector2.zero;
        public float transitionTexRotation;
        public bool transitionKeepAspectRatio;
        [Range(0, 1)] public float transitionRate;
        [ColorUsage(true, true)] public Color transitionColor = Color.white;
        [Range(0, 1)] public float transitionWidth = 0.1f;
        [Range(0, 1)] public float transitionSoftness = 0.1f;
        public bool transitionReverse;
        public Vector2 transitionSpeed;
        public bool transitionPatternReverse;
        public float transitionAutoPlaySpeed;
        public ImageEx.ColorMode transitionColorFilter;
        public bool transitionColorGlow;
        public Vector2 transitionRange = new Vector2(0, 1);
        public ImageEx.PatternArea patternArea;

        [Header("形状边缘 / 抖动")]
        [Range(0.001f, 8f)] public float shapeSoftness = 1f;
        public bool dither;

        [Header("边缘效果")]
        public ImageEx.EdgeMode edgeMode;
        [Range(0, 1)] public float edgeWidth = 0.5f;
        public ImageEx.ColorMode edgeColorFilterMode = ImageEx.ColorMode.Replace;
        [ColorUsage(true, true)] public Color edgeColor = Color.white;
        public bool edgeColorGlow;
        [Range(0, 1)] public float edgeShinyRate = 0.5f;
        [Range(0, 1)] public float edgeShinyWidth = 0.5f;
        [Range(-5, 5)] public float edgeShinyAutoPlaySpeed = 1f;

        [Header("内阴影")]
        public bool enableInnerShadow;
        [ColorUsage(true, true)] public Color innerShadowColor = new Color(0f, 0f, 0f, 1f);
        public float innerShadowWidth = 8f;
        [Range(0f, 1f)] public float innerShadowSoftness = 0.5f;

        [Header("虚线")]
        public int enableDashedOutline;
        public float dashLength;
        public float dashGap = 14f;
        [Range(0f, 1f)] public float dashSoftness;
        public bool dashRounded;
        public float dashOffset;

        [Header("投影（SdfOutline）")]

        [Header("图案填充")]
        public ImageEx.PatternMode patternMode;
        public Vector2 patternSize = new Vector2(24f, 24f);
        public Vector2 patternGap = new Vector2(4f, 4f);
        public float patternAngle;
        public Vector2 patternOffset;
        [ColorUsage(true, true)] public Color patternColor = Color.white;
        public float patternSoftness = 1f;
        public Vector2 patternFalloff;
        public Vector2 patternOrigin = new Vector2(0.5f, 0.5f);
        public Vector2 patternSpeed;
        [Range(0f, 1f)] public float patternOpacity = 1f;

        [Header("边缘辉光")]
        public bool enableBorderGlow;
        [ColorUsage(true, true)] public Color borderGlowColor = Color.white;
        public float borderGlowWidth = 2f;
        public float borderGlowSoftness = 8f;
        [Range(0f, 1f)] public float borderGlowAmbient;
        [Range(0f, 1f)] public float borderGlowFollow;
        public float borderGlowRadius = 100f;
        [Range(0f, 1f)] public float borderGlowIntensity = 1f;

        [Header("进度裁剪")]
        public ImageEx.ClipMode clipMode;
        [Range(0, 1)] public float clipAmount = 1f;
        public bool clipReverse;
        public float clipSoftness = 1f;

        [Header("渐变效果（GradientEffect）")]
        public bool gradientEnabled;
        public GradientType gradientType;
        public Gradient gradient = new Gradient();
        public float gradientRotation;
        public bool gradientScreenSpace;
        public bool gradientReverse;
        public float gradientSpeed;
        public Color[] cornerGradientColors;

        [Header("细节纹理")]
        public ImageEx.DetailFilter detailMode;
        public Texture detailTex;
        public Vector2 detailTexScale = Vector2.one;
        public Vector2 detailTexOffset = Vector2.zero;
        public Vector2 detailTexSpeed = Vector2.zero;
        [Range(0, 1)] public float detailIntensity = 1f;
        public Vector2 detailThreshold = new Vector2(0, 1);
        [ColorUsage(true, true)] public Color detailColor = Color.white;

        [Header("混合")]
        public ImageEx.BlendType blendType;

        [Header("阴影")]
        public ImageEx.ShadowMode shadowMode;
        public ImageEx.ColorMode shadowColorFilter = ImageEx.ColorMode.Replace;
        public bool shadowColorGlow;
        // 以下为补齐字段（此前缺失 ⇒ 套预设不还原、且 ImageExReplica 检测不到它们的变化）
        public bool appendShadow = true;
        public Color shadowColor = new Color(0, 0, 0, 0.5f);
        [Range(0, 100)] public float shadowBlurIntensity = 1f;
        public Vector2 shadowOffsetLocal;
        [Range(0.1f, 4f)] public float shadowScale = 1f;
        public ImageEx.ShadowDirection shadowMirrorDirection;
        [Range(0.1f, 4f)] public float shadowMirrorScale = 1f;
        public Vector2 shadowMirrorOffset;
        public bool shadowMirrorShowSource;
        [Range(0, 1)] public float shadowMirrorTintMix;
        public bool allowOutOfBoundsShadow = true;

        /// <summary>
        /// 将预设应用到指定的 ImageEx 组件。
        /// </summary>
        public void ApplyTo(ImageEx target)
        {
            if (target == null) return;

            // 渐变
            target.EnableGradientTex = enableGradientTex;
            target.GradientTex = gradientTex;
            target.GradientOffset = gradientOffset;
            target.GradientScale = gradientScale;

            // 模糊
            target.Blur = blurType;
            target.BlurIntensity = blurIntensity;

            // 采样
            target.Sampling = samplingMode;
            target.SamplingIntensity = samplingIntensity;

            // 目标模式
            target.Target = targetMode;
            target.TargetColor = targetColor;
            target.TargetRange = targetRange;
            target.TargetSoftness = targetSoftness;

            // 色调
            target.Tone = toneFilter;
            target.ToneIntensity = toneIntensity;

            // 颜色滤镜
            target.ColorFilterMode = colorFilterMode;
            target.ColorValue = colorValue;
            target.ColorIntensity = colorIntensity;
            target.ColorGlow = colorGlow;

            // 过渡
            target.Transition = transitionMode;
            target.TransitionTexture = transitionTexture;
            target.TransitionTexScale = transitionTexScale;
            target.TransitionTexOffset = transitionTexOffset;
            target.TransitionTexRotation = transitionTexRotation;
            target.TransitionKeepAspectRatio = transitionKeepAspectRatio;
            target.TransitionRate = transitionRate;
            target.TransitionColor = transitionColor;
            target.TransitionWidth = transitionWidth;
            target.TransitionSoftness = transitionSoftness;
            target.TransitionReverse = transitionReverse;
            target.TransitionSpeed = transitionSpeed;
            target.TransitionPatternReverse = transitionPatternReverse;
            target.TransitionAutoPlaySpeed = transitionAutoPlaySpeed;
            target.TransitionColorFilter = transitionColorFilter;
            target.TransitionColorGlow = transitionColorGlow;
            target.TransitionRange = transitionRange;
            target.TransitionPatternArea = patternArea;

            // 边缘
            target.Edge = edgeMode;
            target.EdgeWidth = edgeWidth;
            target.EdgeColorFilterMode = edgeColorFilterMode;
            target.EdgeColor = edgeColor;
            target.EdgeColorGlow = edgeColorGlow;
            target.EdgeShinyRate = edgeShinyRate;
            target.EdgeShinyWidth = edgeShinyWidth;
            target.EdgeShinyAutoPlaySpeed = edgeShinyAutoPlaySpeed;
            target.ShapeSoftness = shapeSoftness;
            target.Dither = dither;

            target.EnableInnerShadow = enableInnerShadow;
            target.InnerShadowColor = innerShadowColor;
            target.InnerShadowWidth = innerShadowWidth;
            target.InnerShadowSoftness = innerShadowSoftness;

            target.DashRounded = dashRounded;
            target.DashOffset = dashOffset;
            target.EnableDashedOutline = enableDashedOutline;
            target.DashLength = dashLength;
            target.DashGap = dashGap;
            target.DashSoftness = dashSoftness;


            target.Pattern = patternMode;
            target.PatternSize = patternSize;
            target.PatternGap = patternGap;
            target.PatternAngle = patternAngle;
            target.PatternOffset = patternOffset;
            target.PatternColor = patternColor;
            target.PatternSoftness = patternSoftness;
            target.PatternFalloff = patternFalloff;
            target.PatternOrigin = patternOrigin;
            target.PatternSpeed = patternSpeed;
            target.PatternOpacity = patternOpacity;

            target.EnableBorderGlow = enableBorderGlow;
            target.BorderGlowColor = borderGlowColor;
            target.BorderGlowWidth = borderGlowWidth;
            target.BorderGlowSoftness = borderGlowSoftness;
            target.BorderGlowAmbient = borderGlowAmbient;
            target.BorderGlowFollow = borderGlowFollow;
            target.BorderGlowRadius = borderGlowRadius;
            target.BorderGlowIntensity = borderGlowIntensity;

            target.Clip = clipMode;
            target.ClipAmount = clipAmount;
            target.ClipReverse = clipReverse;
            target.ClipSoftness = clipSoftness;

            GradientEffect gradientEffect = target.GradientEffect;
            gradientEffect.Enabled = gradientEnabled;
            gradientEffect.GradientType = gradientType;
            if (gradient != null) gradientEffect.Gradient = gradient;
            gradientEffect.Rotation = gradientRotation;
            gradientEffect.ScreenSpace = gradientScreenSpace;
            gradientEffect.Reverse = gradientReverse;
            gradientEffect.Speed = gradientSpeed;
            if (cornerGradientColors != null) gradientEffect.CornerGradientColors = cornerGradientColors;
            target.GradientEffect = gradientEffect;

            // 细节
            target.Detail = detailMode;
            target.DetailTex = detailTex;
            target.DetailTexScale = detailTexScale;
            target.DetailTexOffset = detailTexOffset;
            target.DetailTexSpeed = detailTexSpeed;
            target.DetailIntensity = detailIntensity;
            target.DetailThreshold = detailThreshold;
            target.DetailColor = detailColor;

            // 混合 & 阴影
            target.Blend = blendType;
            target.Shadow = shadowMode;
            target.ShadowColorFilter = shadowColorFilter;
            target.ShadowColorGlow = shadowColorGlow;
            target.AppendShadow = appendShadow;
            target.ShadowColor = shadowColor;
            target.ShadowBlurIntensity = shadowBlurIntensity;
            target.ShadowOffsetLocal = shadowOffsetLocal;
            target.ShadowScale = shadowScale;
            target.ShadowMirrorDirection = shadowMirrorDirection;
            target.ShadowMirrorScale = shadowMirrorScale;
            target.ShadowMirrorOffset = shadowMirrorOffset;
            target.ShadowMirrorShowSource = shadowMirrorShowSource;
            target.ShadowMirrorTintMix = shadowMirrorTintMix;
            target.AllowOutOfBoundsShadow = allowOutOfBoundsShadow;
            target.SamplingWidth = samplingWidth;
            target.SamplingScale = samplingScale;
        }

        /// <summary>
        /// 从指定的 ImageEx 组件读取当前设置到预设。
        /// </summary>

        private static bool SameGradient(Gradient a, Gradient b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.mode != b.mode) return false;

            GradientColorKey[] colorKeysA = a.colorKeys;
            GradientColorKey[] colorKeysB = b.colorKeys;
            if (colorKeysA.Length != colorKeysB.Length) return false;
            for (int i = 0; i < colorKeysA.Length; i++)
            {
                if (!Mathf.Approximately(colorKeysA[i].time, colorKeysB[i].time)) return false;
                if (colorKeysA[i].color != colorKeysB[i].color) return false;
            }

            GradientAlphaKey[] alphaKeysA = a.alphaKeys;
            GradientAlphaKey[] alphaKeysB = b.alphaKeys;
            if (alphaKeysA.Length != alphaKeysB.Length) return false;
            for (int i = 0; i < alphaKeysA.Length; i++)
            {
                if (!Mathf.Approximately(alphaKeysA[i].time, alphaKeysB[i].time)) return false;
                if (!Mathf.Approximately(alphaKeysA[i].alpha, alphaKeysB[i].alpha)) return false;
            }

            return true;
        }

        private static bool SameColorArray(Color[] a, Color[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!Near(a[i], b[i])) return false;
            }

            return true;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-4f;
        private static bool Near(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 1e-4f && Mathf.Abs(a.g - b.g) < 1e-4f &&
            Mathf.Abs(a.b - b.b) < 1e-4f && Mathf.Abs(a.a - b.a) < 1e-4f;
        private static bool Near(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) < 1e-4f && Mathf.Abs(a.y - b.y) < 1e-4f;
    }
}
