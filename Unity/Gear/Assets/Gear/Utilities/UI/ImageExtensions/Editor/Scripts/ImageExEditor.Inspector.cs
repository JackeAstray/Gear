using System;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Common;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageExEditor partial part：OnInspectorGUI 主面板。
    /// </summary>
    public partial class ImageExEditor : ImageEditor
    {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            FixShaderChannelGUI();

            RaycastControlsGUI();
            EditorGUILayout.PropertyField(m_Color);
            EditorGUILayout.Space();

            CameraTextureGUI();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(spShape, new GUIContent("绘制形状"));

            if (spShape.enumValueIndex != (int)DrawShape.None)
            {
                EditorGUILayout.BeginVertical("Box");
                if (!spShape.hasMultipleDifferentValues)
                {
                    switch ((DrawShape)spShape.enumValueIndex)
                    {
                        case DrawShape.Circle:
                            EditorGUILayout.PropertyField(spCircle);
                            break;
                        case DrawShape.Rectangle:
                            EditorGUILayout.PropertyField(spRectangle);
                            break;
                        case DrawShape.Pentagon:
                            EditorGUILayout.PropertyField(spPentagon);
                            break;
                        case DrawShape.Triangle:
                            EditorGUILayout.PropertyField(spTriangle);
                            break;
                        case DrawShape.Hexagon:
                            EditorGUILayout.PropertyField(spHexagon);
                            break;
                        case DrawShape.ChamferBox:
                            EditorGUILayout.PropertyField(spChamferBox);
                            break;
                        case DrawShape.Quadrilateral:
                            EditorGUILayout.PropertyField(spQuadrilateral);
                            break;
                        case DrawShape.NStarPolygon:
                            EditorGUILayout.PropertyField(spNStarPolygon);
                            break;
                        case DrawShape.Heart:
                            EditorGUILayout.PropertyField(spHeart);
                            break;
                        case DrawShape.BlobbyCross:
                            EditorGUILayout.PropertyField(spBlobbyCross);
                            break;
                        case DrawShape.Squircle:
                            EditorGUILayout.PropertyField(spSquircle);
                            break;
                        case DrawShape.NTriangleRounded:
                            EditorGUILayout.PropertyField(spNTriangleRounded);
                            break;

                        default:
                            // 非法枚举值（旧序列化残留）：跳过绘制而非抛异常，避免 Inspector 中断；限频告警
                            if (!m_invalidShapeLogged)
                            {
                                m_invalidShapeLogged = true;
                                Log.Warning("ImageEx.DrawShape 存在非法值 {0}，已跳过形状参数绘制", spShape.enumValueIndex);
                            }
                            break;
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            AdditionalShapeDataGUI();
            EditorGUILayout.Space();

            ImageTypeGUI();

            SpriteGUI();

            if (!spSprite.hasMultipleDifferentValues && spSprite.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(spPreserveAspect,
                    new GUIContent("图片保持纵横比", "Sprite 按原始比例缩放（不铺满矩形）。注意：过渡纹理的「保持纵横比」只作用于图案，不会改变图片比例。"));
            }

            SetShowNativeSize(spSprite.objectReferenceValue != null, true);
            NativeSizeButtonGUI();

            EditorGUILayout.Space();
            SharedMaterialGUI();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spGradient);
                // Phase 3: 渐变纹理子选项
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spEnableGradientTex, new GUIContent("渐变纹理"));
                if (spEnableGradientTex.boolValue)
                {
                    EditorGUILayout.PropertyField(spGradientTex, new GUIContent("纹理"));
                }
                EditorGUILayout.PropertyField(spGradientOffset, new GUIContent("偏移"));
                EditorGUILayout.PropertyField(spGradientScale, new GUIContent("缩放"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spBlurType, new GUIContent("模糊类型"));
                if (spBlurType.enumValueIndex != (int)ImageEx.BlurType.None)
                {
                    EditorGUILayout.PropertyField(spBlurIntensity, new GUIContent("模糊强度"));
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 采样增强（SAMPLING） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spSamplingMode, new GUIContent("采样模式"));
                if (spSamplingMode.enumValueIndex != (int)ImageEx.SamplingFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spSamplingIntensity, new GUIContent("采样强度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 目标模式（TARGET MODE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spTargetMode, new GUIContent("目标模式"));
                if (spTargetMode.enumValueIndex != (int)ImageEx.TargetMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spTargetColor, new GUIContent("目标颜色"));
                    EditorGUILayout.PropertyField(spTargetRange, new GUIContent("目标范围"));
                    EditorGUILayout.PropertyField(spTargetSoftness, new GUIContent("目标柔和度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 细节纹理（DETAIL FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spDetailMode, new GUIContent("细节滤镜"));
                if (spDetailMode.enumValueIndex != (int)ImageEx.DetailFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spDetailTex, new GUIContent("纹理"));
                    EditorGUILayout.PropertyField(spDetailTexScale, new GUIContent("缩放"));
                    EditorGUILayout.PropertyField(spDetailTexOffset, new GUIContent("偏移"));
                    EditorGUILayout.PropertyField(spDetailTexSpeed, new GUIContent("速度"));
                    EditorGUILayout.PropertyField(spDetailIntensity, new GUIContent("强度"));
                    EditorGUILayout.PropertyField(spDetailThreshold, new GUIContent("阈值"));

                    // 细节颜色（HDR）
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spDetailColor.hasMultipleDifferentValues;
                    Color newDetailColor = EditorGUILayout.ColorField(new GUIContent("颜色"), spDetailColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spDetailColor.colorValue = newDetailColor;
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 混合模式（BLEND TYPE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spBlendType, new GUIContent("混合模式"));
            }
            EditorGUILayout.EndVertical();

            // ==================== 色调滤镜（TONE FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spToneFilter, new GUIContent("色调滤镜"));
                if (spToneFilter.enumValueIndex != (int)ImageEx.ToneFilter.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spToneIntensity, new GUIContent("滤镜强度"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            // ==================== 独立颜色滤镜（COLOR FILTER） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spColorFilterMode, new GUIContent("颜色滤镜"));
                if (spColorFilterMode.enumValueIndex != (int)ImageEx.ColorMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spColorValue, new GUIContent("滤镜值"));
                    EditorGUILayout.PropertyField(spColorIntensity, new GUIContent("滤镜强度"));
                    EditorGUILayout.PropertyField(spColorGlow, new GUIContent("发光"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spTransitionMode, new GUIContent("过渡模式"));
                if (spTransitionMode.enumValueIndex != (int)ImageEx.TransitionMode.None)
                {
                    ImageEx.TransitionMode mode = (ImageEx.TransitionMode)spTransitionMode.enumValueIndex;

                    EditorGUILayout.PropertyField(spTransitionRate, new GUIContent("过渡进度"));

                    if (mode == ImageEx.TransitionMode.Cutoff || mode == ImageEx.TransitionMode.Dissolve ||
                        mode == ImageEx.TransitionMode.Mask || mode == ImageEx.TransitionMode.Melt ||
                        mode == ImageEx.TransitionMode.Burn || mode == ImageEx.TransitionMode.Pattern ||
                        mode == ImageEx.TransitionMode.Fade || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUILayout.LabelField("过渡纹理", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(spTransitionTex, new GUIContent("纹理"));
                        EditorGUILayout.PropertyField(spTransitionTexScale, new GUIContent("缩放"));
                        EditorGUILayout.PropertyField(spTransitionTexOffset, new GUIContent("偏移"));
                        EditorGUILayout.PropertyField(spTransitionSpeed, new GUIContent("速度"));
                        EditorGUILayout.PropertyField(spTransitionTexRotation, new GUIContent("旋转"));
                        if (mode == ImageEx.TransitionMode.Pattern)
                        {
                            // Pattern 在着色器里恒定按正方形纹素采样，开关对它没有意义
                            EditorGUILayout.LabelField(
                                new GUIContent("保持纵横比", "Pattern 模式始终按正方形纹素采样，任意旋转角度都不会被矩形宽高比拉长"),
                                new GUIContent("始终开启"));
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(spTransitionKeepAspectRatio,
                                new GUIContent("保持纵横比", "按像素空间各向同性采样过渡纹理：任意矩形比例下纹理都不会被拉长"));
                        }
                        EditorGUILayout.PropertyField(spTransitionReverse, new GUIContent("反向"));

                        // 过渡高级参数
                        EditorGUILayout.PropertyField(spTransitionClamp, new GUIContent("强制钳制", "对过渡 UV 做钳制，防止纹理采样越界"));
                        EditorGUILayout.PropertyField(spTransitionTexClampPadding, new GUIContent("钳制边距", "纹理钳制的额外边距（0~4）"));
                        EditorGUILayout.PropertyField(spTransitionUseUv0, new GUIContent("使用 UV0", "过渡采样使用 UV0 通道"));
                        EditorGUILayout.PropertyField(spTransitionScreenSpace,
                            new GUIContent("屏幕空间采样", "过渡纹理按「面板的屏幕中心 / 轴向 / 高度」构造刚性 2D 坐标系：贴住面板、随面板移动旋转缩放，但不吃 3D 平面透视 —— 摄像机任意角度、纹理任意旋转角度都保持正常比例"));
                        EditorGUI.indentLevel--;
                    }

                    if (mode == ImageEx.TransitionMode.Dissolve || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Melt || mode == ImageEx.TransitionMode.Burn ||
                        mode == ImageEx.TransitionMode.Blaze || mode == ImageEx.TransitionMode.Mask)
                    {
                        EditorGUILayout.PropertyField(spTransitionWidth, new GUIContent("过渡宽度"));
                    }
                    else if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        EditorGUILayout.PropertyField(spTransitionWidth, new GUIContent("图案大小"));
                    }

                    if (mode == ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(spTransitionGradientValue, new GUIContent("过渡渐变"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            serializedObject.ApplyModifiedProperties();
                            foreach (Object obj in targets)
                            {
                                ((ImageEx)obj).RefreshTransitionGradient();
                            }
                        }
                    }

                    if (mode == ImageEx.TransitionMode.Dissolve || mode == ImageEx.TransitionMode.Shiny ||
                        mode == ImageEx.TransitionMode.Melt || mode == ImageEx.TransitionMode.Burn ||
                        mode == ImageEx.TransitionMode.Mask)
                    {
                        EditorGUILayout.PropertyField(spTransitionSoftness, new GUIContent("过渡柔和度"));
                    }

                    if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        Vector2 range = spTransitionRange.vector2Value;
                        float min = range.x;
                        float max = range.y;
                        EditorGUILayout.MinMaxSlider(new GUIContent("图案范围"), ref min, ref max, 0f, 1f);
                        spTransitionRange.vector2Value = new Vector2(min, max);
                    }

                    if (mode == ImageEx.TransitionMode.Pattern)
                    {
                        EditorGUILayout.PropertyField(spTransitionPatternReverse, new GUIContent("图案反向"));
                        EditorGUILayout.PropertyField(spPatternArea, new GUIContent("图案区域"));
                    }

                    if (mode != ImageEx.TransitionMode.Fade && mode != ImageEx.TransitionMode.Cutoff && mode != ImageEx.TransitionMode.Blaze)
                    {
                        EditorGUILayout.PropertyField(spTransitionColorFilter, new GUIContent("颜色滤镜"));

                        // 过渡颜色（HDR）
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.showMixedValue = spTransitionColor.hasMultipleDifferentValues;
                        Color newTransitionColor = EditorGUILayout.ColorField(new GUIContent("过渡颜色"), spTransitionColor.colorValue, true, true, true);
                        EditorGUI.showMixedValue = false;
                        if (EditorGUI.EndChangeCheck()) spTransitionColor.colorValue = newTransitionColor;

                        EditorGUILayout.PropertyField(spTransitionColorGlow, new GUIContent("发光"));
                    }

                    EditorGUILayout.PropertyField(spTransitionAutoPlaySpeed, new GUIContent("自动播放速度"));
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("Box");
            {
                // Shadow mode styled similarly to Transition Mode: a single field then indented settings.
                EditorGUILayout.PropertyField(spAppendShadow, new GUIContent("阴影模式"));
                if (spAppendShadow.boolValue)
                {
                    EditorGUILayout.LabelField("阴影设置", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(spShadowOffsetLocal, new GUIContent("阴影偏移 (本地)"));

                    // Shadow type dropdown：隐藏 SdfOutline（SDF 阴影已停用），只保留旧模式。
                    if (!spShadowMode.hasMultipleDifferentValues)
                    {
                        ImageEx.ShadowMode[] shadowModes =
                        {
                            ImageEx.ShadowMode.Shadow,
                            ImageEx.ShadowMode.Shadow3,
                            ImageEx.ShadowMode.Mirror,
                            ImageEx.ShadowMode.Outline8,
                        };
                        string[] shadowLabels = { "Shadow", "Shadow 3", "Mirror", "Outline8" };
                        int current = System.Array.IndexOf(shadowModes, (ImageEx.ShadowMode)spShadowMode.intValue);
                        if (current < 0) current = 0;
                        int next = EditorGUILayout.Popup(new GUIContent("阴影类型"), current, shadowLabels);
                        spShadowMode.intValue = (int)shadowModes[Mathf.Clamp(next, 0, shadowModes.Length - 1)];
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(spShadowMode, new GUIContent("阴影类型"));
                    }

                    // 阴影颜色（HDR）
                    SerializedProperty spShadowColor = serializedObject.FindProperty("shadowColor");
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spShadowColor.hasMultipleDifferentValues;
                    Color newShadowColor = EditorGUILayout.ColorField(new GUIContent("阴影颜色"), spShadowColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spShadowColor.colorValue = newShadowColor;

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowBlurIntensity"), new GUIContent("阴影衰减距离"));
                    if (spShadowScale != null)
                        EditorGUILayout.PropertyField(spShadowScale, new GUIContent("阴影缩放"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingWidth"), new GUIContent("采样宽度"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("samplingScale"), new GUIContent("采样缩放"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("allowOutOfBoundsShadow"), new GUIContent("允许超出边界阴影"));

                    // Show 'show source' only when ShadowMode is Mirror
                    if (!spShadowMode.hasMultipleDifferentValues && spShadowMode.intValue == (int)ImageEx.ShadowMode.Mirror)
                    {
                        EditorGUILayout.PropertyField(spShadowMirrorDirection, new GUIContent("镜像方向"));
                        EditorGUILayout.PropertyField(spShadowMirrorScale, new GUIContent("镜像缩放"));
                        EditorGUILayout.PropertyField(spShadowMirrorOffset, new GUIContent("镜像偏移"));
                        if (spShadowMirrorShowSource != null)
                            EditorGUILayout.PropertyField(spShadowMirrorShowSource, new GUIContent("显示原图"));
                        if (spShadowMirrorTintMix != null)
                            EditorGUILayout.PropertyField(spShadowMirrorTintMix, new GUIContent("镜像混合"));
                    }

                    EditorGUILayout.PropertyField(spShadowColorFilter, new GUIContent("阴影颜色滤镜"));
                    EditorGUILayout.PropertyField(spShadowColorGlow, new GUIContent("阴影发光"));

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            // ==================== 边缘效果（EDGE MODE） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.PropertyField(spEdgeMode, new GUIContent("边缘效果"));
                if (spEdgeMode.enumValueIndex != (int)ImageEx.EdgeMode.None)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spEdgeWidth, new GUIContent("边缘宽度"));

                    EditorGUILayout.PropertyField(spEdgeColorFilterMode, new GUIContent("颜色滤镜"));

                    // 边缘颜色（HDR）
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = spEdgeColor.hasMultipleDifferentValues;
                    Color newEdgeColor = EditorGUILayout.ColorField(new GUIContent("边缘颜色"), spEdgeColor.colorValue, true, true, true);
                    EditorGUI.showMixedValue = false;
                    if (EditorGUI.EndChangeCheck()) spEdgeColor.colorValue = newEdgeColor;

                    EditorGUILayout.PropertyField(spEdgeColorGlow, new GUIContent("发光"));

                    // Shiny 模式特有参数
                    if (!spEdgeMode.hasMultipleDifferentValues && spEdgeMode.enumValueIndex == (int)ImageEx.EdgeMode.Shiny)
                    {
                        EditorGUILayout.LabelField("高光设置", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(spEdgeShinyRate, new GUIContent("高光位置"));
                        EditorGUILayout.PropertyField(spEdgeShinyWidth, new GUIContent("高光宽度"));
                        EditorGUILayout.PropertyField(spEdgeShinyAutoPlaySpeed, new GUIContent("自动速度"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            if (spShape.enumValueIndex == (int)DrawShape.None)
            {
                // 翻转控件（放置在“阴影”设置下，仅在DrawShape==None时启用）
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("Box");
                {
                    EditorGUILayout.LabelField("翻转", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spFlipHorizontal, new GUIContent("水平翻转"));
                    EditorGUILayout.PropertyField(spFlipVertical, new GUIContent("垂直翻转"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            // ==================== 内阴影（INNER SHADOW） ====================
            bool showInnerShadow = spShape.hasMultipleDifferentValues || spShape.enumValueIndex != (int)DrawShape.None;
            if (showInnerShadow)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("Box");
                {
                    EditorGUILayout.LabelField("内阴影", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spEnableInnerShadow, new GUIContent("启用"));

                    if (spEnableInnerShadow.hasMultipleDifferentValues || spEnableInnerShadow.boolValue)
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUI.showMixedValue = spInnerShadowColor.hasMultipleDifferentValues;
                        Color newInnerShadowColor = EditorGUILayout.ColorField(
                            new GUIContent("颜色"), spInnerShadowColor.colorValue, true, true, true);
                        EditorGUI.showMixedValue = false;
                        if (EditorGUI.EndChangeCheck()) spInnerShadowColor.colorValue = newInnerShadowColor;

                        EditorGUILayout.PropertyField(spInnerShadowWidth, new GUIContent("宽度（像素）"));
                        EditorGUILayout.PropertyField(spInnerShadowSoftness, new GUIContent("柔和度（0-1）"));
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            // ==================== 虚线（DASH） ====================
            if (spEnableDashedOutline.hasMultipleDifferentValues || spEnableDashedOutline.intValue != 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("Box");
                {
                    EditorGUILayout.LabelField("虚线（像素级）", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(spDashLength, new GUIContent("长度（像素）"));
                    EditorGUILayout.PropertyField(spDashGap, new GUIContent("间隔（像素）"));
                    EditorGUILayout.PropertyField(spDashSoftness, new GUIContent("柔边"));
                    EditorGUILayout.PropertyField(spDashRounded, new GUIContent("圆头帽"));
                    EditorGUILayout.PropertyField(spDashOffset, new GUIContent("相位偏移（像素）"));
                    EditorGUILayout.HelpBox(
                        "长度 = 0 时使用旧的 UV 比例虚线；> 0 时矩形沿外轮廓周长连续排布（跨角不断开）、圆形按周长排布，并随「自定义时间」平移。",
                        MessageType.None);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            // ==================== 进度裁剪（CLIP / PROGRESS） ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("进度裁剪", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spClipMode, new GUIContent("模式"));

                if (spClipMode.hasMultipleDifferentValues || spClipMode.enumValueIndex != (int)ImageEx.ClipMode.None)
                {
                    EditorGUILayout.PropertyField(spClipAmount, new GUIContent("比例"));
                    EditorGUILayout.PropertyField(spClipReverse, new GUIContent("反向"));
                    EditorGUILayout.PropertyField(spClipSoftness, new GUIContent("柔和度（像素）"));
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            // ==================== 图案填充 / 边缘辉光 ====================
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("图案填充（Pattern）", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spPatternMode, new GUIContent("模式"));
                if (spPatternMode.hasMultipleDifferentValues || spPatternMode.enumValueIndex != (int)ImageEx.PatternMode.None)
                {
                    EditorGUILayout.PropertyField(spPatternSize, new GUIContent("单元格（像素）"));
                    EditorGUILayout.PropertyField(spPatternGap, new GUIContent("留白（像素）"));
                    EditorGUILayout.PropertyField(spPatternAngle, new GUIContent("旋转（度）"));
                    EditorGUILayout.PropertyField(spPatternOffset, new GUIContent("偏移（像素）"));
                    EditorGUILayout.PropertyField(spPatternSpeed, new GUIContent("流速（像素/秒）"));
                    EditorGUILayout.PropertyField(spPatternColor, new GUIContent("颜色"));
                    EditorGUILayout.PropertyField(spPatternSoftness, new GUIContent("柔边（像素）"));
                    EditorGUILayout.PropertyField(spPatternOpacity, new GUIContent("不透明度"));
                    EditorGUILayout.PropertyField(spPatternFalloff, new GUIContent("径向衰减起止"));
                    EditorGUILayout.PropertyField(spPatternOrigin, new GUIContent("衰减原点"));
                    EditorGUILayout.HelpBox("图案按形状像素坐标铺开，跟随 RectTransform 尺寸，不会在非正方形图形上被拉伸。", MessageType.None);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("Box");
            {
                EditorGUILayout.LabelField("边缘辉光（Border Glow）", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(spEnableBorderGlow, new GUIContent("启用"));
                if (spEnableBorderGlow.hasMultipleDifferentValues || spEnableBorderGlow.boolValue)
                {
                    EditorGUILayout.PropertyField(spBorderGlowColor, new GUIContent("颜色"));
                    EditorGUILayout.PropertyField(spBorderGlowWidth, new GUIContent("宽度（像素）"));
                    EditorGUILayout.PropertyField(spBorderGlowSoftness, new GUIContent("羽化（像素）"));
                    EditorGUILayout.PropertyField(spBorderGlowAmbient, new GUIContent("环境光比例"));
                    EditorGUILayout.PropertyField(spBorderGlowFollow, new GUIContent("跟随指针"));
                    EditorGUILayout.PropertyField(spBorderGlowRadius, new GUIContent("跟随半径（像素）"));
                    EditorGUILayout.PropertyField(spBorderGlowIntensity, new GUIContent("强度"));
                    EditorGUILayout.HelpBox("给物体挂 ImageExPointerGlow 后，指针位置会写入辉光；不挂时按「环境光比例」整圈常亮。", MessageType.None);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            //刷新
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            Repaint();
        }
    }
}
