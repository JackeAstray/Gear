using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    public partial class ImageEx
    {
        public void InitValuesFromSharedMaterial()
        {
            if (m_Material == null) return;
            Material mat = m_Material;

            // 基本设置
            drawShape = (DrawShape)mat.GetInt(drawShape_Sp);

            blurType = (BlurType)mat.GetInt(blurType_Sp);
            blurIntensity = mat.GetFloat(blurIntensity_Sp);

            transitionMode = (TransitionMode)mat.GetInt(transitionMode_Sp);
            transitionTexture = mat.GetTexture(transitionTex_Sp);
            Vector4 st = mat.GetVector(transitionTex_ST_Sp);
            transitionTexScale = new Vector2(st.x, st.y);
            transitionTexOffset = new Vector2(st.z, st.w);
            transitionTexRotation = mat.GetFloat(transitionTexRotation_Sp);
            transitionRate = mat.GetFloat(transitionRate_Sp);
            transitionColor = mat.GetColor(transitionColor_Sp);
            transitionWidth = mat.GetFloat(transitionWidth_Sp);
            transitionSoftness = mat.GetFloat(transitionSoftness_Sp);
            transitionReverse = mat.GetInt(transitionReverse_Sp) == 1;
            transitionSpeed = mat.GetVector(transitionTexSpeed_Sp);
            transitionPatternReverse = mat.GetInt(transitionPatternReverse_Sp) == 1;
            transitionAutoPlaySpeed = mat.GetFloat(transitionAutoPlaySpeed_Sp);
            transitionColorFilter = (ColorMode)mat.GetInt(transitionColorFilter_Sp);
            transitionColorGlow = mat.GetInt(transitionColorGlow_Sp) == 1;
            transitionGradient = mat.GetTexture(transitionGradientTex_Sp);
            transitionRange = mat.GetVector(transitionRange_Sp);
            transitionClamp = mat.GetFloat(transitionClamp_Sp) == 1;
            transitionTexClampPadding = mat.GetFloat(transitionTexClampPadding_Sp);
            transitionUseUv0 = mat.GetFloat(transitionUseUv0_Sp) == 1;

            strokeWidth = mat.GetFloat(strokeWidth_Sp);
            falloffDistance = mat.GetFloat(falloffDistance_Sp);
            shapeSoftness = mat.HasProperty(shapeSoftness_Sp)
                ? Mathf.Clamp(mat.GetFloat(shapeSoftness_Sp), 0.001f, 8f)
                : 1f;
            dither = mat.HasProperty(dither_Sp) && mat.GetInt(dither_Sp) == 1;

            outlineWidth = mat.GetFloat(outlineWidth_Sp);
            outlineColor = mat.GetColor(outlineColor_Sp);
            enableDashedOutline = mat.GetInt(enableDashedOutline_Sp);
            dashLength = mat.GetFloat(dashLength_Sp);
            dashGap = mat.GetFloat(dashGap_Sp);
            dashSoftness = mat.GetFloat(dashSoftness_Sp);
            dashRounded = mat.GetFloat(dashRounded_Sp) > 0.5f;
            dashOffset = mat.GetFloat(dashOffset_Sp);
            customTime = mat.GetFloat(customTime_Sp);

            enableInnerShadow = mat.GetInt(enableInnerShadow_Sp) == 1;
            innerShadowColor = mat.GetColor(innerShadowColor_Sp);
            innerShadowWidth = mat.GetFloat(innerShadowWidth_Sp);
            innerShadowSoftness = mat.GetFloat(innerShadowSoftness_Sp);

            clipMode = (ClipMode)mat.GetInt(clipMode_Sp);
            clipAmount = mat.GetFloat(clipAmount_Sp);
            clipReverse = mat.GetInt(clipReverse_Sp) == 1;
            clipSoftness = mat.GetFloat(clipSoftness_Sp);

            // 阴影镜像方向
            shadowMirrorDirection = (ShadowDirection)mat.GetInt(shadowMirrorDirection_Sp);

            shadowMirrorScale = mat.GetFloat(shadowMirrorScale_Sp);
            shadowMirrorOffset = mat.GetVector(shadowMirrorOffset_Sp);
            shadowMirrorShowSource = mat.GetFloat(shadowMirrorShowSource_Sp) > 0.5f;
            shadowMirrorTintMix = mat.GetFloat(shadowMirrorTintMix_Sp);
            shadowColorFilter = (ColorMode)mat.GetInt(shadowColorFilter_Sp);
            shadowColorGlow = mat.GetInt(shadowColorGlow_Sp) == 1;

            flipHorizontal = mat.GetInt(flipHorizontal_Sp) == 1;
            flipVertical = mat.GetInt(flipVertical_Sp) == 1;
            constrainRotation = mat.GetInt(constrainedRotation_Sp) == 1;
            shapeRotation = mat.GetFloat(shapeRotation_Sp);

            shadowColor = mat.GetColor(shadowColor_Sp);
            shadowBlurIntensity = mat.GetFloat(shadowBlurIntensity_Sp);
            shadowMode = (ShadowMode)mat.GetInt(shadowMode_Sp);
            samplingWidth = mat.GetFloat(samplingWidth_Sp);
            samplingScale = mat.GetFloat(samplingScale_Sp);
            shadowScale = Mathf.Clamp(mat.GetFloat(shadowScale_Sp), 0.1f, 4f);
            allowOutOfBoundsShadow = mat.GetFloat(allowOutOfBoundsShadow_Sp) == 1;

            triangle.InitValuesFromMaterial(ref mat);
            circle.InitValuesFromMaterial(ref mat);
            rectangle.InitValuesFromMaterial(ref mat);
            pentagon.InitValuesFromMaterial(ref mat);
            hexagon.InitValuesFromMaterial(ref mat);
            chamferBox.InitValuesFromMaterial(ref mat);
            quadrilateral.InitValuesFromMaterial(ref mat);
            nStarPolygon.InitValuesFromMaterial(ref mat);
            heart.InitValuesFromMaterial(ref mat);
            blobbyCross.InitValuesFromMaterial(ref mat);
            squircle.InitValuesFromMaterial(ref mat);
            nTriangleRounded.InitValuesFromMaterial(ref mat);

            // GradientEffect
            gradientEffect.InitValuesFromMaterial(ref mat);

            // 色调滤镜
            m_ToneIntensity = mat.GetFloat(toneIntensity_Sp);
            m_ToneFilter = (ToneFilter)mat.GetInt(toneFilter_Sp);

            // 独立颜色滤镜
            m_ColorFilterMode = (ColorMode)mat.GetInt(colorFilter_Sp);
            m_ColorValue = mat.GetColor(colorValue_Sp);
            m_ColorIntensity = mat.GetFloat(colorIntensity_Sp);
            m_ColorGlow = mat.GetInt(colorGlow_Sp) == 1;

            // 边缘效果
            m_EdgeWidth = mat.GetFloat(edgeWidth_Sp);
            m_EdgeColorFilterMode = (ColorMode)mat.GetInt(edgeColorFilter_Sp);
            m_EdgeColor = mat.GetColor(edgeColor_Sp);
            m_EdgeColorGlow = mat.GetInt(edgeColorGlow_Sp) == 1;
            m_EdgeShinyRate = mat.GetFloat(edgeShinyRate_Sp);
            m_EdgeShinyWidth = mat.GetFloat(edgeShinyWidth_Sp);
            m_EdgeShinyAutoPlaySpeed = mat.GetFloat(edgeShinyAutoPlaySpeed_Sp);
            m_EdgeMode = (EdgeMode)mat.GetInt(edgeMode_Sp);

            // 采样增强
            m_SamplingIntensity = mat.GetFloat(samplingIntensity_Sp);
            m_SamplingMode = (SamplingFilter)mat.GetInt(samplingMode_Sp);

            // 目标模式
            m_TargetColor = mat.GetColor(targetColor_Sp);
            m_TargetRange = mat.GetFloat(targetRange_Sp);
            m_TargetSoftness = mat.GetFloat(targetSoftness_Sp);
            m_TargetMode = (TargetMode)mat.GetInt(targetMode_Sp);

            // 图案区域
            m_PatternArea = (PatternArea)mat.GetInt(patternArea_Sp);

            // 细节纹理
            m_DetailTex = mat.GetTexture(detailTex_Sp);
            m_DetailIntensity = mat.GetFloat(detailIntensity_Sp);
            m_DetailThreshold = mat.GetVector(detailThreshold_Sp);
            m_DetailColor = mat.GetColor(detailColor_Sp);
            m_DetailMode = (DetailFilter)mat.GetInt(detailMode_Sp);
            Vector4 detailST = mat.GetVector(detailTex_ST_Sp);
            m_DetailTexScale = new Vector2(detailST.x, detailST.y);
            m_DetailTexOffset = new Vector2(detailST.z, detailST.w);
            m_DetailTexSpeed = mat.GetVector(detailTexSpeed_Sp);

            // 渐变纹理
            m_GradientOffset = mat.GetFloat(gradientOffset_Sp);
            m_GradientScale = mat.GetFloat(gradientScale_Sp);
            m_EnableGradientTex = mat.GetInt(gradientTexEnabled_Sp) == 1;
            m_GradientTex = mat.GetTexture(gradientTex_Sp);

            // 图案填充
            patternMode = (PatternMode)mat.GetInt(patternMode_Sp);
            Vector4 patternSizeValue = mat.GetVector(patternSize_Sp);
            patternSize = new Vector2(patternSizeValue.x, patternSizeValue.y);
            Vector4 patternGapValue = mat.GetVector(patternGap_Sp);
            patternGap = new Vector2(patternGapValue.x, patternGapValue.y);
            patternAngle = mat.GetFloat(patternAngle_Sp);
            Vector4 patternOffsetValue = mat.GetVector(patternOffset_Sp);
            patternOffset = new Vector2(patternOffsetValue.x, patternOffsetValue.y);
            patternColor = mat.GetColor(patternColor_Sp);
            patternSoftness = mat.GetFloat(patternSoftness_Sp);
            Vector4 patternFalloffValue = mat.GetVector(patternFalloff_Sp);
            patternFalloff = new Vector2(patternFalloffValue.x, patternFalloffValue.y);
            Vector4 patternOriginValue = mat.GetVector(patternOrigin_Sp);
            patternOrigin = new Vector2(patternOriginValue.x, patternOriginValue.y);
            Vector4 patternSpeedValue = mat.GetVector(patternSpeed_Sp);
            patternSpeed = new Vector2(patternSpeedValue.x, patternSpeedValue.y);
            patternOpacity = mat.GetFloat(patternOpacity_Sp);

            // 边缘辉光
            enableBorderGlow = mat.GetInt(enableBorderGlow_Sp) == 1;
            borderGlowColor = mat.GetColor(borderGlowColor_Sp);
            borderGlowWidth = mat.GetFloat(borderGlowWidth_Sp);
            borderGlowSoftness = mat.GetFloat(borderGlowSoftness_Sp);
            borderGlowAmbient = mat.GetFloat(borderGlowAmbient_Sp);
            borderGlowFollow = mat.GetFloat(borderGlowFollow_Sp);
            borderGlowRadius = mat.GetFloat(borderGlowRadius_Sp);
            borderGlowIntensity = mat.GetFloat(borderGlowIntensity_Sp);
            borderGlowHover = mat.GetFloat(borderGlowHover_Sp);
            Vector4 borderGlowPositionValue = mat.GetVector(borderGlowPosition_Sp);
            borderGlowPosition = new Vector2(borderGlowPositionValue.x, borderGlowPositionValue.y);

            // 由 shader 实际生效的 _SrcBlend/_DstBlend 反查混合模式（材质不存在 _BlendType 属性）
            m_BlendType = BlendTypeFromBlendModes(mat.GetInt(srcBlend_Sp), mat.GetInt(dstBlend_Sp));
        }
    }
}
