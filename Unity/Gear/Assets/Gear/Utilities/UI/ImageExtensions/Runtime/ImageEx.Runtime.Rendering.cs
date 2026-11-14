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
                        ImageHelper.GenerateSdfShadowSprite(vh, EffectivePreserveAspect, canvas, rectTransform,
                            ActiveSprite, color, sdfGrow, shadowSoftness, spriteFillMode);
                        break;
                    }

                    Vector2 effectiveShadowOffset = shadowOffsetLocal;
                    if (flipHorizontal) effectiveShadowOffset.x = -effectiveShadowOffset.x;
                    if (flipVertical) effectiveShadowOffset.y = -effectiveShadowOffset.y;

                    // 单层投影 / 镜像：让 ImageHelper 先写阴影四边形，再写主图，
                    // 保证阴影始终在图像后面（旧实现先写主图再 AddShadowQuad，阴影会盖在图像上）。
                    if (appendShadow && (shadowMode == ShadowMode.Shadow || shadowMode == ShadowMode.Mirror))
                    {
                        ImageHelper.GenerateSimpleSprite(vh, EffectivePreserveAspect, canvas, rectTransform, ActiveSprite,
                            color, falloffDistance, true, effectiveShadowOffset, shadowScale, spriteFillMode);
                        break;
                    }

                    // 其余模式：先绘制主图，再追加阴影（Shadow3 仍按原顺序）
                    ImageHelper.GenerateSimpleSprite(vh, EffectivePreserveAspect, canvas, rectTransform, ActiveSprite,
                        color, falloffDistance, false, Vector2.zero, 1f, spriteFillMode);

                    if (appendShadow && shadowMode == ShadowMode.Shadow3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float iterFade = Mathf.Pow(0.75f, i); // 1.0, 0.75, 0.5625
                            Vector2 iterOffset = effectiveShadowOffset * (i + 1);
                            ImageHelper.AddShadowQuad(vh, EffectivePreserveAspect, canvas, rectTransform,
                                ActiveSprite, color, iterOffset, shadowScale, iterFade, spriteFillMode);
                        }
                    }
                    break;
                case Type.Filled:
                    ImageHelper.GenerateFilledSprite(vh, EffectivePreserveAspect, canvas, rectTransform, ActiveSprite,
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

            // Pooled 模式：把“最终 uniform 完全一致”的实例指向同一份材质，避免每实例 Dynamic 拆批。
            // 有相机画面 / 软遮罩，或 base 材质被 Mask 等修饰器换过时回退逐实例写入，避免共享错误状态。
            if (materialMode == MaterialMode.Pooled
                && ReferenceEquals(mat, baseMaterial)
                && cameraTexture == null
                && GetComponentInParent<GearSoftMask>(true) == null)
            {
                return AcquirePooledMaterial(baseMaterial);
            }

            WriteMaterialUniforms(mat);
            return mat;
        }

        /// <summary>
        /// 为当前设置生成一份样本材质，交给池按 uniform 签名做去重/复用。
        /// </summary>
        private Material AcquirePooledMaterial(Material template)
        {
            Material probe = new Material(template)
            {
                hideFlags = HideFlags.DontSave,
            };
            WriteMaterialUniforms(probe);
            return ImageExMaterialPool.Acquire(probe);
        }

        /// <summary>
        /// 把所有材质 uniform 写到指定材质。Pooled 模式用它为“设置签名”生成样本材质。
        /// </summary>
        private void WriteMaterialUniforms(Material mat)
        {
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
            mat.SetFloat(shapeSoftness_Sp, shapeSoftness);
            mat.SetInt(dither_Sp, dither ? 1 : 0);

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

            // -------------------- 指针聚光（SPOTLIGHT） --------------------
            mat.SetInt(enableSpotlight_Sp, enableSpotlight ? 1 : 0);
            mat.SetColor(spotlightColor_Sp, spotlightColor);
            mat.SetFloat(spotlightRadius_Sp, spotlightRadius);
            mat.SetFloat(spotlightSoftness_Sp, spotlightSoftness);
            mat.SetFloat(spotlightIntensity_Sp, spotlightIntensity);
            mat.SetVector(spotlightPosition_Sp, new Vector4(spotlightPosition.x, spotlightPosition.y, 0f, 0f));
            mat.SetFloat(spotlightHover_Sp, spotlightHover);

            // -------------------- 点阵图案（DOT MATRIX） --------------------
            mat.SetInt(enableDotMatrix_Sp, enableDotMatrix ? 1 : 0);
            mat.SetInt(dotPattern_Sp, (int)dotPattern);
            mat.SetInt(dotShape_Sp, (int)dotShape);
            mat.SetFloat(dotCell_Sp, dotCell);
            mat.SetFloat(dotRadius_Sp, dotRadius);
            mat.SetColor(dotColor_Sp, dotColor);
            mat.SetFloat(dotIntensity_Sp, dotIntensity);
            mat.SetFloat(dotSpeed_Sp, dotSpeed);

            // -------------------- 像素幕帘（PIXEL CURTAIN） --------------------
            mat.SetInt(enablePixelCurtain_Sp, enablePixelCurtain ? 1 : 0);
            mat.SetFloat(curtainProgress_Sp, curtainProgress);
            mat.SetFloat(curtainCell_Sp, curtainCell);
            mat.SetFloat(curtainRandomness_Sp, curtainRandomness);
            mat.SetFloat(curtainSoftness_Sp, curtainSoftness);
            mat.SetInt(curtainDirection_Sp, curtainDirection);

            // -------------------- 地形等值线（TOPOGRAPHIC） --------------------
            mat.SetInt(enableTopographic_Sp, enableTopographic ? 1 : 0);
            mat.SetFloat(topoScale_Sp, topoScale);
            mat.SetFloat(topoDensity_Sp, topoDensity);
            mat.SetFloat(topoLineWidth_Sp, topoLineWidth);
            mat.SetFloat(topoMajorEvery_Sp, topoMajorEvery);
            mat.SetColor(topoColor_Sp, topoColor);
            mat.SetFloat(topoIntensity_Sp, topoIntensity);
            mat.SetFloat(topoSpeed_Sp, topoSpeed);
            mat.SetFloat(topoFalloff_Sp, topoFalloff);

            // -------------------- 墨团揭示（INK PANEL） --------------------
            mat.SetInt(enableInkPanel_Sp, enableInkPanel ? 1 : 0);
            mat.SetFloat(inkProgress_Sp, inkProgress);
            mat.SetVector(inkOrigin_Sp, new Vector4(inkOrigin.x, inkOrigin.y, 0f, 0f));
            mat.SetFloat(inkSoftness_Sp, inkSoftness);
            mat.SetFloat(inkRoughness_Sp, inkRoughness);

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

            // 软遮罩：最近父级 GearSoftMask 的矩形/羽化参数，直接写进本实例材质
            GearSoftMask.ApplyToMaterial(mat, this);

        }

        /// <summary>
        /// 从共享材质初始化值
        /// </summary>

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
