using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    public partial class ImageExPreset
    {
        public void ReadFrom(ImageEx source)
        {
            if (source == null) return;

            enableGradientTex = source.EnableGradientTex;
            gradientTex = source.GradientTex;
            gradientOffset = source.GradientOffset;
            gradientScale = source.GradientScale;

            blurType = source.Blur;
            blurIntensity = source.BlurIntensity;

            samplingMode = source.Sampling;
            samplingIntensity = source.SamplingIntensity;

            targetMode = source.Target;
            targetColor = source.TargetColor;
            targetRange = source.TargetRange;
            targetSoftness = source.TargetSoftness;

            toneFilter = source.Tone;
            toneIntensity = source.ToneIntensity;

            colorFilterMode = source.ColorFilterMode;
            colorValue = source.ColorValue;
            colorIntensity = source.ColorIntensity;
            colorGlow = source.ColorGlow;

            transitionMode = source.Transition;
            transitionTexture = source.TransitionTexture;
            transitionTexScale = source.TransitionTexScale;
            transitionTexOffset = source.TransitionTexOffset;
            transitionTexRotation = source.TransitionTexRotation;
            transitionKeepAspectRatio = source.TransitionKeepAspectRatio;
            transitionRate = source.TransitionRate;
            transitionColor = source.TransitionColor;
            transitionWidth = source.TransitionWidth;
            transitionSoftness = source.TransitionSoftness;
            transitionReverse = source.TransitionReverse;
            transitionSpeed = source.TransitionSpeed;
            transitionPatternReverse = source.TransitionPatternReverse;
            transitionAutoPlaySpeed = source.TransitionAutoPlaySpeed;
            transitionColorFilter = source.TransitionColorFilter;
            transitionColorGlow = source.TransitionColorGlow;
            transitionRange = source.TransitionRange;
            patternArea = source.TransitionPatternArea;

            edgeMode = source.Edge;
            edgeWidth = source.EdgeWidth;
            edgeColorFilterMode = source.EdgeColorFilterMode;
            edgeColor = source.EdgeColor;
            edgeColorGlow = source.EdgeColorGlow;
            edgeShinyRate = source.EdgeShinyRate;
            edgeShinyWidth = source.EdgeShinyWidth;
            edgeShinyAutoPlaySpeed = source.EdgeShinyAutoPlaySpeed;
            shapeSoftness = source.ShapeSoftness;
            dither = source.Dither;

            enableInnerShadow = source.EnableInnerShadow;
            innerShadowColor = source.InnerShadowColor;
            innerShadowWidth = source.InnerShadowWidth;
            innerShadowSoftness = source.InnerShadowSoftness;

            dashRounded = source.DashRounded;
            dashOffset = source.DashOffset;
            enableDashedOutline = source.EnableDashedOutline;
            dashLength = source.DashLength;
            dashGap = source.DashGap;
            dashSoftness = source.DashSoftness;


            patternMode = source.Pattern;
            patternSize = source.PatternSize;
            patternGap = source.PatternGap;
            patternAngle = source.PatternAngle;
            patternOffset = source.PatternOffset;
            patternColor = source.PatternColor;
            patternSoftness = source.PatternSoftness;
            patternFalloff = source.PatternFalloff;
            patternOrigin = source.PatternOrigin;
            patternSpeed = source.PatternSpeed;
            patternOpacity = source.PatternOpacity;

            enableBorderGlow = source.EnableBorderGlow;
            borderGlowColor = source.BorderGlowColor;
            borderGlowWidth = source.BorderGlowWidth;
            borderGlowSoftness = source.BorderGlowSoftness;
            borderGlowAmbient = source.BorderGlowAmbient;
            borderGlowFollow = source.BorderGlowFollow;
            borderGlowRadius = source.BorderGlowRadius;
            borderGlowIntensity = source.BorderGlowIntensity;

            clipMode = source.Clip;
            clipAmount = source.ClipAmount;
            clipReverse = source.ClipReverse;
            clipSoftness = source.ClipSoftness;

            GradientEffect gradientEffect = source.GradientEffect;
            gradientEnabled = gradientEffect.Enabled;
            gradientType = gradientEffect.GradientType;
            gradient = gradientEffect.Gradient;
            gradientRotation = gradientEffect.Rotation;
            gradientScreenSpace = gradientEffect.ScreenSpace;
            gradientReverse = gradientEffect.Reverse;
            gradientSpeed = gradientEffect.Speed;
            cornerGradientColors = gradientEffect.CornerGradientColors;

            detailMode = source.Detail;
            detailTex = source.DetailTex;
            detailTexScale = source.DetailTexScale;
            detailTexOffset = source.DetailTexOffset;
            detailTexSpeed = source.DetailTexSpeed;
            detailIntensity = source.DetailIntensity;
            detailThreshold = source.DetailThreshold;
            detailColor = source.DetailColor;

            blendType = source.Blend;
            shadowMode = source.Shadow;
            shadowColorFilter = source.ShadowColorFilter;
            shadowColorGlow = source.ShadowColorGlow;
            appendShadow = source.AppendShadow;
            shadowColor = source.ShadowColor;
            shadowBlurIntensity = source.ShadowBlurIntensity;
            shadowOffsetLocal = source.ShadowOffsetLocal;
            shadowScale = source.ShadowScale;
            shadowMirrorDirection = source.ShadowMirrorDirection;
            shadowMirrorScale = source.ShadowMirrorScale;
            shadowMirrorOffset = source.ShadowMirrorOffset;
            shadowMirrorShowSource = source.ShadowMirrorShowSource;
            shadowMirrorTintMix = source.ShadowMirrorTintMix;
            allowOutOfBoundsShadow = source.AllowOutOfBoundsShadow;
            samplingWidth = source.SamplingWidth;
            samplingScale = source.SamplingScale;
        }

        /// <summary>
        /// 逐字段比较与另一预设是否内容相同（带容差），用于变化检测：
        /// 相同则跳过 Apply，避免每帧 SetMaterialDirty 触发 Canvas 重建。
        /// </summary>
        public bool SameAs(ImageExPreset other)
        {
            if (other == null) return false;
            return enableGradientTex == other.enableGradientTex
                && gradientTex == other.gradientTex
                && Near(gradientOffset, other.gradientOffset)
                && Near(gradientScale, other.gradientScale)
                && blurType == other.blurType
                && Near(blurIntensity, other.blurIntensity)
                && samplingMode == other.samplingMode
                && Near(samplingIntensity, other.samplingIntensity)
                && targetMode == other.targetMode
                && Near(targetColor, other.targetColor)
                && Near(targetRange, other.targetRange)
                && Near(targetSoftness, other.targetSoftness)
                && toneFilter == other.toneFilter
                && Near(toneIntensity, other.toneIntensity)
                && colorFilterMode == other.colorFilterMode
                && Near(colorValue, other.colorValue)
                && Near(colorIntensity, other.colorIntensity)
                && colorGlow == other.colorGlow
                && transitionMode == other.transitionMode
                && transitionTexture == other.transitionTexture
                && Near(transitionTexScale, other.transitionTexScale)
                && Near(transitionTexOffset, other.transitionTexOffset)
                && Near(transitionTexRotation, other.transitionTexRotation)
                && transitionKeepAspectRatio == other.transitionKeepAspectRatio
                && Near(transitionRate, other.transitionRate)
                && Near(transitionColor, other.transitionColor)
                && Near(transitionWidth, other.transitionWidth)
                && Near(transitionSoftness, other.transitionSoftness)
                && transitionReverse == other.transitionReverse
                && Near(transitionSpeed, other.transitionSpeed)
                && transitionPatternReverse == other.transitionPatternReverse
                && Near(transitionAutoPlaySpeed, other.transitionAutoPlaySpeed)
                && transitionColorFilter == other.transitionColorFilter
                && transitionColorGlow == other.transitionColorGlow
                && Near(transitionRange, other.transitionRange)
                && patternArea == other.patternArea
                && edgeMode == other.edgeMode
                && Near(edgeWidth, other.edgeWidth)
                && edgeColorFilterMode == other.edgeColorFilterMode
                && Near(edgeColor, other.edgeColor)
                && edgeColorGlow == other.edgeColorGlow
                && Near(edgeShinyRate, other.edgeShinyRate)
                && Near(edgeShinyWidth, other.edgeShinyWidth)
                && Near(edgeShinyAutoPlaySpeed, other.edgeShinyAutoPlaySpeed)
                && Near(shapeSoftness, other.shapeSoftness)
                && dither == other.dither
                && enableInnerShadow == other.enableInnerShadow
                && Near(innerShadowColor, other.innerShadowColor)
                && Near(innerShadowWidth, other.innerShadowWidth)
                && Near(innerShadowSoftness, other.innerShadowSoftness)
                && dashRounded == other.dashRounded
                && Near(dashOffset, other.dashOffset)
                && enableDashedOutline == other.enableDashedOutline
                && Near(dashLength, other.dashLength)
                && Near(dashGap, other.dashGap)
                && Near(dashSoftness, other.dashSoftness)
                && patternMode == other.patternMode
                && Near(patternSize, other.patternSize)
                && Near(patternGap, other.patternGap)
                && Near(patternAngle, other.patternAngle)
                && Near(patternOffset, other.patternOffset)
                && Near(patternColor, other.patternColor)
                && Near(patternSoftness, other.patternSoftness)
                && Near(patternFalloff, other.patternFalloff)
                && Near(patternOrigin, other.patternOrigin)
                && Near(patternSpeed, other.patternSpeed)
                && Near(patternOpacity, other.patternOpacity)
                && enableBorderGlow == other.enableBorderGlow
                && Near(borderGlowColor, other.borderGlowColor)
                && Near(borderGlowWidth, other.borderGlowWidth)
                && Near(borderGlowSoftness, other.borderGlowSoftness)
                && Near(borderGlowAmbient, other.borderGlowAmbient)
                && Near(borderGlowFollow, other.borderGlowFollow)
                && Near(borderGlowRadius, other.borderGlowRadius)
                && Near(borderGlowIntensity, other.borderGlowIntensity)
                && clipMode == other.clipMode
                && Near(clipAmount, other.clipAmount)
                && clipReverse == other.clipReverse
                && Near(clipSoftness, other.clipSoftness)
                && gradientEnabled == other.gradientEnabled
                && gradientType == other.gradientType
                && SameGradient(gradient, other.gradient)
                && Near(gradientRotation, other.gradientRotation)
                && gradientScreenSpace == other.gradientScreenSpace
                && gradientReverse == other.gradientReverse
                && Near(gradientSpeed, other.gradientSpeed)
                && SameColorArray(cornerGradientColors, other.cornerGradientColors)
                && detailMode == other.detailMode
                && detailTex == other.detailTex
                && Near(detailTexScale, other.detailTexScale)
                && Near(detailTexOffset, other.detailTexOffset)
                && Near(detailTexSpeed, other.detailTexSpeed)
                && Near(detailIntensity, other.detailIntensity)
                && Near(detailThreshold, other.detailThreshold)
                && Near(detailColor, other.detailColor)
                && blendType == other.blendType
                && shadowMode == other.shadowMode
                && shadowColorFilter == other.shadowColorFilter
                && shadowColorGlow == other.shadowColorGlow
                && appendShadow == other.appendShadow
                && Near(shadowColor, other.shadowColor)
                && Near(shadowBlurIntensity, other.shadowBlurIntensity)
                && Near(shadowOffsetLocal, other.shadowOffsetLocal)
                && Near(shadowScale, other.shadowScale)
                && shadowMirrorDirection == other.shadowMirrorDirection
                && Near(shadowMirrorScale, other.shadowMirrorScale)
                && Near(shadowMirrorOffset, other.shadowMirrorOffset)
                && shadowMirrorShowSource == other.shadowMirrorShowSource
                && Near(shadowMirrorTintMix, other.shadowMirrorTintMix)
                && allowOutOfBoundsShadow == other.allowOutOfBoundsShadow
                && Near(samplingWidth, other.samplingWidth)
                && Near(samplingScale, other.samplingScale);
        }
    }
}
