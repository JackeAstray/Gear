using System;
using Gear.Common;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：网格生成与材质合成（OnPopulateMesh / GetModifiedMaterial / InitValuesFromSharedMaterial）。
    /// </summary>
    public partial class ImageEx
    {

        /// <summary>
        /// 生成网格
        /// </summary>
        /// <param name="vh"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            switch (type)
            {
                case Type.Simple:
                case Type.Sliced:
                    // 单 quad 轮廓阴影：SdfOutline（旧 Outline8 已废弃，统一走这里）
                    if (appendShadow && (shadowMode == ShadowMode.SdfOutline || shadowMode == ShadowMode.Outline8))
                    {
                        float sdfGrow = Mathf.Max(shadowOffsetLocal.magnitude, 1f);
                        float shadowSoftness = Mathf.Max(falloffDistance, shadowBlurIntensity);
                        ImageHelper.GenerateSdfShadowSprite(vh, preserveAspect, canvas, rectTransform,
                            ActiveSprite, color, sdfGrow, shadowSoftness);
                        break;
                    }

                    Vector2 effectiveShadowOffset = shadowOffsetLocal;
                    if (flipHorizontal) effectiveShadowOffset.x = -effectiveShadowOffset.x;
                    if (flipVertical) effectiveShadowOffset.y = -effectiveShadowOffset.y;

                    // 单层投影 / 镜像：让 ImageHelper 先写阴影四边形，再写主图，
                    // 保证阴影始终在图像后面（旧实现先写主图再 AddShadowQuad，阴影会盖在图像上）。
                    if (appendShadow && (shadowMode == ShadowMode.Shadow || shadowMode == ShadowMode.Mirror))
                    {
                        ImageHelper.GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                            color, falloffDistance, true, effectiveShadowOffset, shadowScale);
                        break;
                    }

                    // 其余模式：先绘制主图，再追加阴影（Shadow3 仍按原顺序）
                    ImageHelper.GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, falloffDistance, false, Vector2.zero);

                    if (appendShadow && shadowMode == ShadowMode.Shadow3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float iterFade = Mathf.Pow(0.75f, i); // 1.0, 0.75, 0.5625
                            Vector2 iterOffset = effectiveShadowOffset * (i + 1);
                            ImageHelper.AddShadowQuad(vh, preserveAspect, canvas, rectTransform,
                                ActiveSprite, color, iterOffset, shadowScale, iterFade);
                        }
                    }
                    break;
                case Type.Filled:
                    ImageHelper.GenerateFilledSprite(vh, preserveAspect, canvas, rectTransform, ActiveSprite,
                        color, fillMethod, fillAmount, fillOrigin, fillClockwise, falloffDistance);
                    break;
                default:
                    // 非法 imageType（理论上 setter 已归一化，此处兜底防旧序列化残留）：
                    // 不抛异常，回退 Simple 渲染，避免打断网格重建；限频告警
                    if (!m_invalidImageTypeLogged)
                    {
                        m_invalidImageTypeLogged = true;
                        Log.Warning("ImageEx.imageType 存在非法值 {0}，回退 Simple 渲染", (int)type);
                    }
                    goto case Type.Simple;
            }
        }

        // 非法枚举值限频告警标记（渲染路径不抛异常，防 ArgumentOutOfRangeException 打断 Canvas 重建）
        private bool m_invalidDrawShapeLogged;
        private bool m_invalidImageTypeLogged;

        /// <summary>
        /// 计算当前「特效设置签名」。
        /// 所有特效（形状/描边/过渡/模糊/色调/颜色滤镜/边缘/采样/目标/细节/渐变）现已改为
        /// 读取材质 uniform 的运行时分支，不再使用任何 EnableKeyword/DisableKeyword，
        /// 因此这里不再承担「关键字缓存失效键」的职责，只用于 Shared 模式的串扰诊断：
        /// 共享材质下每个实例都会覆写同一份 uniform，设置不一致时结果由最后写入者决定。
        /// </summary>
        private int ComputeSettingsSignature()
        {
            int signature = 0;
            int strokeState;
            if (strokeWidth > 0 && outlineWidth > 0) strokeState = 1;       // OUTLINED_STROKE
            else if (strokeWidth > 0) strokeState = 2;                     // STROKE
            else if (outlineWidth > 0) strokeState = 3;                    // OUTLINED
            else strokeState = 0;
            signature |= strokeState & 0x3;                                // bits 0-1  描边状态
            signature |= ((int)DrawShape & 0xF) << 2;                      // bits 2-5  形状
            signature |= ((int)transitionMode & 0xF) << 6;                 // bits 6-9  过渡模式
            signature |= ((int)blurType & 0x3) << 10;                      // bits 10-11 模糊
            signature |= ((int)m_ToneFilter & 0x7) << 12;                  // bits 12-14 色调
            signature |= ((int)m_ColorFilterMode & 0xF) << 15;             // bits 15-18 颜色滤镜
            signature |= ((int)m_EdgeMode & 0x3) << 19;                    // bits 19-20 边缘
            signature |= ((int)m_SamplingMode & 0x7) << 21;                // bits 21-23 采样
            signature |= ((int)m_TargetMode & 0x3) << 24;                  // bits 24-25 目标
            signature |= ((int)m_DetailMode & 0x7) << 26;                  // bits 26-28 细节
            int gradState = gradientEffect.Enabled ? ((int)gradientEffect.GradientType + 1) : 0;
            signature |= (gradState & 0x3) << 29;                          // bits 29-30 渐变
            return signature;
        }

        /// <summary>
        /// Shared 模式串扰诊断：同一共享材质被设置不同的多个 ImageEx 使用时告警一次。
        /// 共享材质由多个实例共同写入同一份 uniform，最终状态由最后调用者决定。
        /// </summary>
        private static void DiagnoseSharedMaterialContention(Material shared, int settingsSignature)
        {
            if (shared == null) return;
            if (s_sharedMatLastMask.TryGetValue(shared, out int prevMask))
            {
                if (prevMask != settingsSignature && s_sharedMatWarned.Add(shared))
                {
                    Log.Warning("[ImageEx] 检测到共享材质被多个设置不同的 ImageEx 使用（特效设置签名不一致），"
                        + "渲染结果将由最后一个实例决定，形状/特效可能互相覆盖。"
                        + "建议为每个实例使用独立材质（Material Mode = Dynamic）或保持各实例设置一致。");
                }
                s_sharedMatLastMask[shared] = settingsSignature;
            }
            else
            {
                s_sharedMatLastMask[shared] = settingsSignature;
            }
        }

        /// <summary>
        /// 获取修改后的材质
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {

            Material mat = base.GetModifiedMaterial(baseMaterial);

            // Shared 模式：仅在材质引用变化或首次启用时回读共享材质字段，
            // 避免每次 GetModifiedMaterial（动画期间可能每帧调用）都执行 ~100 次 Get* 回读
            if (m_Material && MaterialMode == MaterialMode.Shared && (!m_sharedValuesLoaded || m_loadedSharedMaterial != m_Material))
            {
                m_sharedValuesLoaded = true;
                m_loadedSharedMaterial = m_Material;
                InitValuesFromSharedMaterial();
            }

            // 特效全部走 uniform 运行时分支，已无关键字需要写入；
            // 仅保留 Shared 模式的串扰诊断（同一共享材质被设置不同的多实例使用时告警一次）。
            DiagnoseSharedMaterialContention(m_Material, ComputeSettingsSignature());

            // 非法 DrawShape（旧版本序列化残留）：不抛异常，避免打断 Canvas 重建；限频告警。
            // shader 侧对非法 _DrawShape 保持 sdfData=0（不渲染形状），与原「不启用形状关键字」语义一致。
            int drawShapeValue = (int)DrawShape;
            if ((drawShapeValue < 0 || drawShapeValue > 12) && !m_invalidDrawShapeLogged)
            {
                m_invalidDrawShapeLogged = true;
                Log.Warning("ImageEx.DrawShape 存在非法值 {0}，已跳过形状渲染", drawShapeValue);
            }

            RectTransform rt = rectTransform;

            mat.SetFloat(outlineWidth_Sp, outlineWidth);
            mat.SetInt(enableDashedOutline_Sp, enableDashedOutline);
            mat.SetFloat(dashLength_Sp, dashLength);
            mat.SetFloat(dashGap_Sp, dashGap);
            mat.SetFloat(dashSoftness_Sp, dashSoftness);
            mat.SetFloat(dashRounded_Sp, dashRounded ? 1f : 0f);
            mat.SetFloat(dashOffset_Sp, dashOffset);
            mat.SetFloat(customTime_Sp, customTime);
            // 已移除 OUTLINED8 - 无操作


            mat.SetFloat(strokeWidth_Sp, strokeWidth);

            mat.SetColor(outlineColor_Sp, OutlineColor);
            mat.SetFloat(falloffDistance_Sp, FalloffDistance);

            // 内阴影（形状路径专用；DrawShape=None 时 shader 端不会调用）
            mat.SetInt(enableInnerShadow_Sp, enableInnerShadow ? 1 : 0);
            mat.SetColor(innerShadowColor_Sp, innerShadowColor);
            mat.SetFloat(innerShadowWidth_Sp, innerShadowWidth);
            mat.SetFloat(innerShadowSoftness_Sp, innerShadowSoftness);

            // 进度裁剪
            mat.SetInt(clipMode_Sp, (int)clipMode);
            mat.SetFloat(clipAmount_Sp, clipAmount);
            mat.SetInt(clipReverse_Sp, clipReverse ? 1 : 0);
            mat.SetFloat(clipSoftness_Sp, clipSoftness);

            mat.SetInt(blurType_Sp, (int)blurType);
            mat.SetFloat(blurIntensity_Sp, blurIntensity);

            mat.SetInt(transitionMode_Sp, (int)transitionMode);
            mat.SetTexture(transitionTex_Sp, transitionTexture);

            Vector2 scale = transitionTexScale;
            Vector2 offset = transitionTexOffset;

            // 「保持纵横比」的宽高比补偿已下沉到着色器（RM_TransitionScaleOffset，用
            // quadAspect + _TransitionTex_TexelSize 在像素空间里算），这里只写用户的缩放/偏移。
            // 同一份 ST 同时用 Vector 与纹理 Tiling/Offset 两种 API 写入：
            // 它们是同一份数据的不同写入口，任一路径失效时另一条仍能把用户值送达着色器。
            var st = new Vector4(scale.x, scale.y, offset.x, offset.y);
            mat.SetVector(transitionTex_ST_Sp, st);
            mat.SetTextureScale(transitionTex_Sp, scale);
            mat.SetTextureOffset(transitionTex_Sp, offset);
            mat.SetFloat(transitionKeepAspectRatio_Sp, transitionKeepAspectRatio ? 1f : 0f);
            mat.SetFloat(transitionScreenSpace_Sp, transitionScreenSpace ? 1f : 0f);
            mat.SetFloat(transitionTexRotation_Sp, transitionTexRotation);
            mat.SetFloat(transitionRate_Sp, transitionRate);
            mat.SetColor(transitionColor_Sp, transitionColor);
            mat.SetFloat(transitionWidth_Sp, transitionWidth);
            mat.SetFloat(transitionSoftness_Sp, transitionSoftness);
            mat.SetInt(transitionReverse_Sp, transitionReverse ? 1 : 0);
            mat.SetVector(transitionTexSpeed_Sp, transitionSpeed);
            mat.SetInt(transitionPatternReverse_Sp, transitionPatternReverse ? 1 : 0);
            mat.SetFloat(transitionAutoPlaySpeed_Sp, transitionAutoPlaySpeed);
            mat.SetInt(transitionColorFilter_Sp, (int)transitionColorFilter);
            mat.SetInt(transitionColorGlow_Sp, transitionColorGlow ? 1 : 0);
            mat.SetTexture(transitionGradientTex_Sp, transitionGradient);
            mat.SetVector(transitionRange_Sp, transitionRange);
            bool runtimeClamp = transitionClamp;
            if (transitionMode == TransitionMode.Shiny || transitionMode == TransitionMode.Mask || transitionMode == TransitionMode.Melt || transitionMode == TransitionMode.Burn)
                runtimeClamp = true;
            mat.SetFloat(transitionClamp_Sp, runtimeClamp ? 1 : 0);
            mat.SetFloat(transitionTexClampPadding_Sp, transitionTexClampPadding);
            mat.SetFloat(transitionUseUv0_Sp, transitionUseUv0 ? 1 : 0);

            // Shadow material params
            mat.SetColor(shadowColor_Sp, shadowColor);
            mat.SetFloat(shadowBlurIntensity_Sp, shadowBlurIntensity);
            mat.SetFloat(samplingWidth_Sp, samplingWidth);
            mat.SetFloat(samplingScale_Sp, samplingScale);
            mat.SetFloat(shadowScale_Sp, shadowScale);
            mat.SetFloat(allowOutOfBoundsShadow_Sp, allowOutOfBoundsShadow ? 1f : 0f);
            mat.SetInt(shadowMode_Sp, (int)shadowMode);
            // SdfOutline 专用：向外扩张距离（像素），与网格侧的外扩量保持一致
            mat.SetFloat(shadowGrow_Sp, Mathf.Max(shadowOffsetLocal.magnitude, 1f));
            mat.SetInt(shadowMirrorDirection_Sp, (int)shadowMirrorDirection);
            mat.SetFloat(shadowMirrorScale_Sp, shadowMirrorScale);
            mat.SetVector(shadowMirrorOffset_Sp, shadowMirrorOffset);
            mat.SetFloat(shadowMirrorShowSource_Sp, shadowMirrorShowSource ? 1f : 0f);
            mat.SetFloat(shadowMirrorTintMix_Sp, shadowMirrorTintMix);
            mat.SetInt(shadowColorFilter_Sp, (int)shadowColorFilter);
            mat.SetInt(shadowColorGlow_Sp, shadowColorGlow ? 1 : 0);

            if (DrawShape != DrawShape.None)
            {
                // 下限钳到 0.001 避免 FalloffDistance=0 时除零得 +∞
                float pixelSize = 1 / Mathf.Max(0.001f, FalloffDistance);
                mat.SetFloat(pixelWorldScale_Sp, Mathf.Clamp(pixelSize, 0f, 999999f));
            }

            triangle.ModifyMaterial(ref mat);
            circle.ModifyMaterial(ref mat, falloffDistance);
            rectangle.ModifyMaterial(ref mat);
            pentagon.ModifyMaterial(ref mat);
            hexagon.ModifyMaterial(ref mat);
            chamferBox.ModifyMaterial(ref mat);
            quadrilateral.ModifyMaterial(ref mat);
            nStarPolygon.ModifyMaterial(ref mat);
            heart.ModifyMaterial(ref mat);
            blobbyCross.ModifyMaterial(ref mat);
            squircle.ModifyMaterial(ref mat);
            nTriangleRounded.ModifyMaterial(ref mat);

            gradientEffect.ModifyMaterial(ref mat);


            mat.SetInt(drawShape_Sp, (int)DrawShape);
            mat.SetInt(flipHorizontal_Sp, flipHorizontal ? 1 : 0);
            mat.SetInt(flipVertical_Sp, flipVertical ? 1 : 0);

            mat.SetFloat(shapeRotation_Sp, shapeRotation);
            mat.SetInt(constrainedRotation_Sp, constrainRotation ? 1 : 0);

            // -------------------- 色调滤镜（TONE） --------------------
            mat.SetFloat(toneIntensity_Sp, m_ToneIntensity);
            mat.SetInt(toneFilter_Sp, (int)m_ToneFilter);

            // -------------------- 独立颜色滤镜（COLOR FILTER） --------------------
            mat.SetInt(colorFilter_Sp, (int)m_ColorFilterMode);
            mat.SetColor(colorValue_Sp, m_ColorValue);
            mat.SetFloat(colorIntensity_Sp, m_ColorIntensity);
            mat.SetInt(colorGlow_Sp, m_ColorGlow ? 1 : 0);

            // -------------------- 边缘效果（EDGE） --------------------
            mat.SetFloat(edgeWidth_Sp, m_EdgeWidth);
            mat.SetInt(edgeColorFilter_Sp, (int)m_EdgeColorFilterMode);
            mat.SetColor(edgeColor_Sp, m_EdgeColor);
            mat.SetInt(edgeColorGlow_Sp, m_EdgeColorGlow ? 1 : 0);
            mat.SetFloat(edgeShinyRate_Sp, m_EdgeShinyRate);
            mat.SetFloat(edgeShinyWidth_Sp, m_EdgeShinyWidth);
            mat.SetFloat(edgeShinyAutoPlaySpeed_Sp, m_EdgeShinyAutoPlaySpeed);
            mat.SetInt(edgeMode_Sp, (int)m_EdgeMode);

            // -------------------- 采样增强（SAMPLING） --------------------
            mat.SetFloat(samplingIntensity_Sp, m_SamplingIntensity);
            mat.SetInt(samplingMode_Sp, (int)m_SamplingMode);

            // -------------------- 目标模式（TARGET） --------------------
            mat.SetColor(targetColor_Sp, m_TargetColor);
            mat.SetFloat(targetRange_Sp, m_TargetRange);
            mat.SetFloat(targetSoftness_Sp, m_TargetSoftness);
            mat.SetInt(targetMode_Sp, (int)m_TargetMode);

            // -------------------- 图案区域（PATTERN AREA） --------------------
            mat.SetInt(patternArea_Sp, (int)m_PatternArea);

            // -------------------- 渐变纹理（GRADIENT TEXTURE） --------------------
            mat.SetFloat(gradientOffset_Sp, m_GradientOffset);
            mat.SetFloat(gradientScale_Sp, m_GradientScale);
            if (m_EnableGradientTex && m_GradientTex != null)
            {
                mat.SetTexture(gradientTex_Sp, m_GradientTex);
                mat.SetInt(gradientTexEnabled_Sp, 1);
            }
            else
            {
                mat.SetTexture(gradientTex_Sp, null);
                mat.SetInt(gradientTexEnabled_Sp, 0);
            }

            // -------------------- 细节纹理（DETAIL FILTER） --------------------
            mat.SetTexture(detailTex_Sp, m_DetailTex);
            mat.SetVector(detailTex_ST_Sp, new Vector4(m_DetailTexScale.x, m_DetailTexScale.y, m_DetailTexOffset.x, m_DetailTexOffset.y));
            mat.SetVector(detailTexSpeed_Sp, m_DetailTexSpeed);
            mat.SetFloat(detailIntensity_Sp, m_DetailIntensity);
            mat.SetVector(detailThreshold_Sp, m_DetailThreshold);
            mat.SetColor(detailColor_Sp, m_DetailColor);
            mat.SetInt(detailMode_Sp, (int)m_DetailMode);

            // -------------------- 图案填充（PATTERN） --------------------
            mat.SetInt(patternMode_Sp, (int)patternMode);
            mat.SetVector(patternSize_Sp, new Vector4(patternSize.x, patternSize.y, 0f, 0f));
            mat.SetVector(patternGap_Sp, new Vector4(patternGap.x, patternGap.y, 0f, 0f));
            mat.SetFloat(patternAngle_Sp, patternAngle);
            mat.SetVector(patternOffset_Sp, new Vector4(patternOffset.x, patternOffset.y, 0f, 0f));
            mat.SetColor(patternColor_Sp, patternColor);
            mat.SetFloat(patternSoftness_Sp, patternSoftness);
            mat.SetVector(patternFalloff_Sp, new Vector4(patternFalloff.x, patternFalloff.y, 0f, 0f));
            mat.SetVector(patternOrigin_Sp, new Vector4(patternOrigin.x, patternOrigin.y, 0f, 0f));
            mat.SetVector(patternSpeed_Sp, new Vector4(patternSpeed.x, patternSpeed.y, 0f, 0f));
            mat.SetFloat(patternOpacity_Sp, patternOpacity);

            // -------------------- 边缘辉光（BORDER GLOW） --------------------
            mat.SetInt(enableBorderGlow_Sp, enableBorderGlow ? 1 : 0);
            mat.SetColor(borderGlowColor_Sp, borderGlowColor);
            mat.SetFloat(borderGlowWidth_Sp, borderGlowWidth);
            mat.SetFloat(borderGlowSoftness_Sp, borderGlowSoftness);
            mat.SetFloat(borderGlowAmbient_Sp, borderGlowAmbient);
            mat.SetFloat(borderGlowFollow_Sp, borderGlowFollow);
            mat.SetFloat(borderGlowRadius_Sp, borderGlowRadius);
            mat.SetFloat(borderGlowIntensity_Sp, borderGlowIntensity);
            mat.SetFloat(borderGlowHover_Sp, borderGlowHover);
            mat.SetVector(borderGlowPosition_Sp, new Vector4(borderGlowPosition.x, borderGlowPosition.y, 0f, 0f));

            // -------------------- 混合模式（BLEND TYPE） --------------------
            // 混合由 shader 的 Blend [_SrcBlend] [_DstBlend] 驱动；
            // 旧代码额外写不存在的 "_BlendType" 属性，Shared 模式回读恒为 0 会覆盖共享材质的混合模式。
            {
                var (src, dst) = ConvertBlendType(m_BlendType);
                mat.SetInt(srcBlend_Sp, (int)src);
                mat.SetInt(dstBlend_Sp, (int)dst);
            }

            // -------------------- 兜底：无精灵、无相机画面时绑定纯白纹理 --------------------
            // 防止动态材质的 _MainTex 被历史状态/共享材质污染成透明或异常纹理，
            // 导致 ImageEx 半透明不可见、且改颜色/赋精灵都无效。
            if (cameraTexture == null && ActiveSprite == null)
            {
                mat.SetTexture(mainTex_Sp, Texture2D.whiteTexture);
            }

            return mat;
        }

        /// <summary>
        /// 从共享材质初始化值
        /// </summary>
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

        private static (UnityEngine.Rendering.BlendMode, UnityEngine.Rendering.BlendMode) ConvertBlendType(BlendType type)
        {
            return type switch
            {
                BlendType.AlphaBlend => (UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha),
                BlendType.Multiply => (UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha),
                BlendType.Additive => (UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One),
                BlendType.SoftAdditive => (UnityEngine.Rendering.BlendMode.OneMinusDstColor, UnityEngine.Rendering.BlendMode.One),
                BlendType.MultiplyAdditive => (UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.One),
                _ => (UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)
            };
        }

        /// <summary>
        /// 由材质的 _SrcBlend/_DstBlend 反查 BlendType（ConvertBlendType 的逆映射）。
        /// 未匹配任何预设组合时回退 AlphaBlend，避免 Shared 回读覆盖手改的混合模式。
        /// </summary>
        private static BlendType BlendTypeFromBlendModes(int src, int dst)
        {
            switch (src)
            {
                case (int)UnityEngine.Rendering.BlendMode.SrcAlpha when dst == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:
                    return BlendType.AlphaBlend;
                case (int)UnityEngine.Rendering.BlendMode.DstColor when dst == (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:
                    return BlendType.Multiply;
                case (int)UnityEngine.Rendering.BlendMode.One when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.Additive;
                case (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.SoftAdditive;
                case (int)UnityEngine.Rendering.BlendMode.DstColor when dst == (int)UnityEngine.Rendering.BlendMode.One:
                    return BlendType.MultiplyAdditive;
                default:
                    return BlendType.AlphaBlend;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 创建材质资产
        /// </summary>
        /// <returns></returns>
        public Material CreateMaterialAssetFromComponentSettings()
        {
            Shader shader = ResolveShader();
            Material matAsset = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            matAsset = GetModifiedMaterial(matAsset);
            string path = EditorUtility.SaveFilePanelInProject("通过ImageEx创建材质",
                "Material", "mat", "选择位置");
            AssetDatabase.CreateAsset(matAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return matAsset;
        }
#endif
    }
}
