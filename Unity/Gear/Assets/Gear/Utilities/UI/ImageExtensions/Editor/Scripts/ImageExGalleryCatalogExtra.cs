using UnityEditor;
using UnityEngine;
using TMPro;
using Gear.Utilities.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 附加效果目录：在基础 40+ 张之外，补齐形状轮廓、渐变角度、裁剪档位、投影方向、
    /// 全部色调/颜色/采样/目标/细节滤镜、9 种过渡和组合卡片，让 Content 里的例子足够多。
    /// </summary>
    internal static partial class ImageExGalleryCatalogExtra
    {
        private static readonly Color Accent = new Color(0.49f, 0.36f, 0.99f);
        private static readonly Color Cyan = new Color(0.14f, 0.75f, 0.95f);
        private static readonly Color Green = new Color(0.25f, 0.78f, 0.52f);
        private static readonly Color Orange = new Color(1f, 0.69f, 0.25f);
        private static readonly Color Surface = new Color(0.11f, 0.12f, 0.17f);

        private static Texture2D s_detail;
        private static Texture2D s_transition;

        internal static void Populate(RectTransform content)
        {
            AddShapeOutlines(content);
            AddGradientAngles(content);
            AddCornerRadiusModes(content);
            AddGradientZoom(content);
            AddRaycastModes(content);
            AddShapeSoftness(content);
            AddDither(content);
            PopulateShowcase(content);
            AddSoftMask(content);
            AddTextEffects(content);
            AddClipVariants(content);
            AddShadowDirections(content);
            AddToneFilters(content);
            AddColorFilters(content);
            AddSamplingFilters(content);
            AddTargets(content);
            AddDetailFilters(content);
            AddEdges(content);
            AddTransitions(content);
            AddComposites(content);
        }

        private static void AddCornerRadiusModes(RectTransform content) {
            int[] percents = { 50, 100 };
            for (int i = 0; i < percents.Length; i++)
            {
                int captured = percents[i];
                AddCardIfMissing(content, "圆角 · 百分比 " + captured + "%", "100% = 短边的一半", p =>
                {
                    p.DrawShape = DrawShape.Rectangle;
                    p.Rectangle = new RectangleImg
                    {
                        CornerRadius = new Vector4(captured, captured, captured, captured),
                        RadiusMode = CornerRadiusMode.Percentage
                    };
                    p.color = new Color(0.49f, 0.36f, 0.99f, 1f);
                    p.OutlineWidth = 2f;
                    p.OutlineColor = new Color(1f, 1f, 1f, 0.2f);
                });
            }
        }

        private static void AddGradientZoom(RectTransform content) {
            float[] zooms = { 0.5f, 2f, 3f };
            for (int i = 0; i < zooms.Length; i++)
            {
                float captured = zooms[i];
                AddCardIfMissing(content, "渐变 · Zoom " + captured + "x", "线性渐变空间缩放", p =>
                {
                    ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, 45f, 0f, Accent, Cyan);
                    p.GradientScale = captured;
                });
            }
        }

        private static void AddRaycastModes(RectTransform content) {
            AddCardIfMissing(content, "命中 · 形状", "胶囊四角空白不响应点击", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg
                {
                    CornerRadius = new Vector4(100f, 100f, 100f, 100f),
                    RadiusMode = CornerRadiusMode.Percentage
                };
                p.color = Accent;
                p.OutlineWidth = 3f;
                p.OutlineColor = Cyan;
                p.RaycastMode = RaycastDetectionMode.Shape;
                p.raycastTarget = true;
            });

            AddCardIfMissing(content, "命中 · None", "只渲染，不参与点击", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg
                {
                    CornerRadius = new Vector4(100f, 100f, 100f, 100f),
                    RadiusMode = CornerRadiusMode.Percentage
                };
                p.color = new Color(0.25f, 0.78f, 0.52f, 1f);
                p.OutlineWidth = 3f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.3f);
                p.RaycastMode = RaycastDetectionMode.None;
                p.raycastTarget = true;
            });
        }

        /// <summary>
        /// 只补齐本次新增的卡片；已存在同名卡片的场景保持原样，不触碰旧卡片参数。
        /// </summary>
        internal static int AppendNewCards(RectTransform content)
        {
            int before = content.childCount;
            AddCornerRadiusModes(content);
            AddGradientZoom(content);
            AddRaycastModes(content);
            AddShapeSoftness(content);
            AddDither(content);
            PopulateShowcase(content);
            AddSoftMask(content);
            AddTextEffects(content);
            return content.childCount - before;
        }

        private static void AddShapeSoftness(RectTransform content) {
            AddCardIfMissing(content, "形状 · 软边", "SDF 几何边缘羽化", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(28f, 28f, 28f, 28f) };
                p.color = new Color(0.49f, 0.36f, 0.99f, 1f);
                p.ShapeSoftness = 6f;
                p.OutlineWidth = 2f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.2f);
            });
        }

        private static void AddSoftMask(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "遮罩 · 软边", "GearSoftMask 矩形羽化", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(30f, 30f, 30f, 30f) };
                p.color = new Color(0.49f, 0.36f, 0.99f, 1f);
                p.OutlineWidth = 2f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.25f);
            });

            if (preview == null || preview.transform.parent == null)
                return;

            Transform clip = preview.transform.parent;
            GearSoftMask mask = clip.GetComponent<GearSoftMask>();
            if (mask == null)
                mask = clip.gameObject.AddComponent<GearSoftMask>();

            mask.Softness = 18f;
            mask.Radius = 18f;
        }
        private static void AddTextEffects(RectTransform content) {
            AddTextCard(content, "文本 · 打字机 / 波浪", "Play 查看打字机 / 波浪 / 抖动", "打字机\n波浪\n抖动",
                effects =>
                {
                    effects.Typewriter = true;
                    effects.CharactersPerSecond = 12f;
                    effects.Wave = true;
                    effects.WaveAmplitude = 4f;
                    effects.WaveFrequency = 0.8f;
                    effects.WaveSpeed = 1.2f;
                    effects.Shake = true;
                    effects.ShakeAmplitude = 1.2f;
                });
            AddTextCard(content, "文本 · Scramble / Shimmer", "Play 查看乱序显现 / 闪烁", "SCRAMBLE\nSHIMMER",
                effects =>
                {
                    effects.Typewriter = false;
                    effects.Scramble = true;
                    effects.ScrambleDuration = 1.4f;
                    effects.ScrambleFrequency = 26f;
                    effects.Shimmer = true;
                    effects.ShimmerSpeed = 3f;
                    effects.ShimmerIntensity = 0.65f;
                });
            ImageExGalleryBuilder.AddTabGroupCard(content, "导航 · Tab", "点击 A/B/C 切换页面");
            ImageExGalleryBuilder.AddStepperCard(content, "导航 · Stepper", "Next / Back / 校验门");
            GearNavigationDemoBuilder.AddPageViewCard(content, "导航 · PageView", "Prev / Next 滑动页面");
            GearNavigationDemoBuilder.AddPopoverCard(content, "覆盖层 · Popover", "点击按钮打开，外点/Esc 关闭");
            GearEffectsDemoBuilder.AddScrollFadeCard(content, "效果 · 滚动渐隐", "滚动列表上下边缘软遮罩");
            GearDataDisplayDemoBuilder.AddProgressCounterCard(content, "数据 · Progress / Counter", "进度条 + 数字滚动");
            GearListViewDemoBuilder.AddListViewCard(content, "数据 · ListView", "LoopScrollRect 虚拟行 + CSV");
            GearChartDemoBuilder.AddChartCard(content, "数据 · Line / Bar", "自绘折线 + 柱状图");
            GearChartDemoBuilder.AddPieLegendCard(content, "数据 · Pie / Legend", "环形图 + 图例 + hover");
            GearChartListLinkDemoBuilder.AddLinkedCard(content, "数据 · Pie → List", "点击扇区过滤列表");
            GearChartDemoBuilder.AddRadarCard(content, "数据 · Radar", "5 轴雷达图");
        }
        private static void AddTextCard(RectTransform content, string title, string description, string body, System.Action<GearTextEffects> configure)
        {
            ImageEx preview = AddCardIfMissing(content, title, description, p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });
            if (preview == null || preview.transform.parent == null)
                return;

            Transform clip = preview.transform.parent;
            GameObject textGo = new GameObject("TextEffectsPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(GearTextEffects));
            textGo.transform.SetParent(clip, false);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) textGo.layer = uiLayer;

            RectTransform rect = (RectTransform)textGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = textGo.GetComponent<TextMeshProUGUI>();
            label.text = body;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            label.raycastTarget = false;

            GearTextEffects effects = textGo.GetComponent<GearTextEffects>();
            configure?.Invoke(effects);
        }

        private static ImageEx AddCardIfMissing(RectTransform content, string title, string description,
            System.Action<ImageEx> configurePreview)
        {
            // 不能用 Transform.Find：标题里的 "/" 会被当成层级路径（例如「打字机 / 波浪」）
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == title)
                    return null;
            }

            return ImageExGalleryBuilder.AddCard(content, title, description, configurePreview);
        }
        private static void AddShapeOutlines(RectTransform content)
        {
            DrawShape[] shapes =
            {
                DrawShape.Circle, DrawShape.Triangle, DrawShape.Rectangle, DrawShape.Pentagon,
                DrawShape.Hexagon, DrawShape.ChamferBox, DrawShape.Quadrilateral, DrawShape.NStarPolygon,
                DrawShape.Heart, DrawShape.BlobbyCross, DrawShape.Squircle, DrawShape.NTriangleRounded,
            };

            for (int i = 0; i < shapes.Length; i++)
            {
                DrawShape shape = shapes[i];
                ImageExGalleryBuilder.AddCard(content, "轮廓 · " + shape, "只保留 SDF 描边", p =>
                {
                    SetupShape(p, shape);
                    p.color = new Color(0.49f, 0.36f, 0.99f, 0f);
                    p.OutlineWidth = 4f;
                    p.OutlineColor = Accent;
                });
            }
        }

        private static void AddGradientAngles(RectTransform content)
        {
            for (int angle = 0; angle <= 180; angle += 45)
            {
                int captured = angle;
                ImageExGalleryBuilder.AddCard(content, "渐变 · " + captured + "°", "线性渐变角度", p =>
                    ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, captured, 0f, Accent, Cyan));
            }
        }

        private static void AddClipVariants(RectTransform content)
        {
            float[] amounts = { 0.25f, 0.5f, 0.75f, 1f };
            for (int i = 0; i < amounts.Length; i++)
            {
                float amount = amounts[i];
                ImageExGalleryBuilder.AddCard(content, "裁剪 · H " + Mathf.RoundToInt(amount * 100) + "%", "横向进度", p =>
                {
                    ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, 45f, 0f, Accent, Cyan);
                    p.Clip = ImageEx.ClipMode.Horizontal;
                    p.ClipAmount = amount;
                    p.ClipSoftness = 3f;
                });

                ImageExGalleryBuilder.AddCard(content, "裁剪 · R " + Mathf.RoundToInt(amount * 100) + "%", "径向进度", p =>
                {
                    p.DrawShape = DrawShape.Circle;
                    p.Circle = new CircleImg { FitToRect = true };
                    p.color = Accent;
                    p.Clip = ImageEx.ClipMode.Radial;
                    p.ClipAmount = amount;
                    p.ClipSoftness = 3f;
                });
            }
        }

        private static void AddShadowDirections(RectTransform content)
        {
            Vector2[] offsets =
            {
                new Vector2(0f, -20f), new Vector2(0f, 20f),
                new Vector2(-20f, 0f), new Vector2(20f, 0f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 offset = offsets[i];
                ImageExGalleryBuilder.AddCard(content, "投影 · " + offset, "方向偏移", p =>
                {
                    p.color = new Color(0.82f, 0.85f, 0.9f, 1f);
                    p.AppendShadow = true;
                    p.Shadow = ImageEx.ShadowMode.Shadow;
                    p.ShadowOffsetLocal = offset;
                    p.ShadowBlurIntensity = 6f;
                    p.ShadowColor = new Color(0.49f, 0.36f, 0.99f, 0.85f);
                });
            }
        }

        private static void AddToneFilters(RectTransform content)
        {
            ImageEx.ToneFilter[] filters =
            {
                ImageEx.ToneFilter.Grayscale, ImageEx.ToneFilter.Sepia, ImageEx.ToneFilter.Negative,
                ImageEx.ToneFilter.Retro, ImageEx.ToneFilter.Posterize,
            };

            for (int i = 0; i < filters.Length; i++)
            {
                ImageEx.ToneFilter filter = filters[i];
                ImageExGalleryBuilder.AddCard(content, "色调 · " + filter, "Tone Filter", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Tone = filter;
                    p.ToneIntensity = 1f;
                });
            }
        }

        private static void AddColorFilters(RectTransform content)
        {
            ImageEx.ColorMode[] modes =
            {
                ImageEx.ColorMode.Multiply, ImageEx.ColorMode.Additive, ImageEx.ColorMode.Subtractive,
                ImageEx.ColorMode.Replace, ImageEx.ColorMode.MultiplyLuminance, ImageEx.ColorMode.MultiplyAdditive,
                ImageEx.ColorMode.HsvModifier, ImageEx.ColorMode.Contrast,
            };

            for (int i = 0; i < modes.Length; i++)
            {
                ImageEx.ColorMode mode = modes[i];
                ImageExGalleryBuilder.AddCard(content, "颜色 · " + mode, "Color Filter", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.ColorFilterMode = mode;
                    p.ColorValue = mode == ImageEx.ColorMode.HsvModifier
                        ? new Color(0.25f, 0.1f, 0.1f, 1f)
                        : (mode == ImageEx.ColorMode.Contrast
                            ? new Color(0.3f, 0.1f, 0f, 1f)
                            : new Color(0.49f, 0.36f, 0.99f, 1f));
                    p.ColorIntensity = 1f;
                });
            }
        }

        private static void AddSamplingFilters(RectTransform content)
        {
            ImageEx.SamplingFilter[] filters =
            {
                ImageEx.SamplingFilter.Pixelation, ImageEx.SamplingFilter.RgbShift,
                ImageEx.SamplingFilter.EdgeLuminance, ImageEx.SamplingFilter.EdgeAlpha,
            };

            for (int i = 0; i < filters.Length; i++)
            {
                ImageEx.SamplingFilter filter = filters[i];
                ImageExGalleryBuilder.AddCard(content, "采样 · " + filter, "Sampling Filter", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Sampling = filter;
                    p.SamplingIntensity = 0.55f;
                });
            }
        }

        private static void AddTargets(RectTransform content)
        {
            ImageEx.TargetMode[] modes = { ImageEx.TargetMode.Hue, ImageEx.TargetMode.Luminance };
            for (int i = 0; i < modes.Length; i++)
            {
                ImageEx.TargetMode mode = modes[i];
                ImageExGalleryBuilder.AddCard(content, "目标 · " + mode, "Target Mode", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Target = mode;
                    p.TargetColor = Accent;
                    p.TargetRange = 0.2f;
                    p.TargetSoftness = 0.5f;
                });
            }
        }

        private static void AddDetailFilters(RectTransform content)
        {
            ImageEx.DetailFilter[] filters =
            {
                ImageEx.DetailFilter.Masking, ImageEx.DetailFilter.Multiply, ImageEx.DetailFilter.Additive,
                ImageEx.DetailFilter.Subtractive, ImageEx.DetailFilter.Replace, ImageEx.DetailFilter.MultiplyAdditive,
            };

            for (int i = 0; i < filters.Length; i++)
            {
                ImageEx.DetailFilter filter = filters[i];
                ImageExGalleryBuilder.AddCard(content, "细节 · " + filter, "Detail Filter", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Detail = filter;
                    p.DetailTex = s_detail != null ? s_detail : Resources.Load<Texture2D>("Noise/Detail-Hologram");
                    p.DetailIntensity = 0.8f;
                });
            }
        }

        private static void AddEdges(RectTransform content)
        {
            ImageEx.EdgeMode[] modes = { ImageEx.EdgeMode.Plain, ImageEx.EdgeMode.Shiny };
            for (int i = 0; i < modes.Length; i++)
            {
                ImageEx.EdgeMode mode = modes[i];
                ImageExGalleryBuilder.AddCard(content, "边缘 · " + mode, "Edge Shiny", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Edge = mode;
                    p.EdgeColor = Accent;
                    p.EdgeWidth = 0.6f;
                    p.EdgeShinyRate = 0.5f;
                    p.EdgeShinyWidth = 0.3f;
                    p.EdgeShinyAutoPlaySpeed = 1f;
                });
            }
        }

        private static void AddTransitions(RectTransform content)
        {
            ImageEx.TransitionMode[] modes =
            {
                ImageEx.TransitionMode.Fade, ImageEx.TransitionMode.Cutoff, ImageEx.TransitionMode.Dissolve,
                ImageEx.TransitionMode.Shiny, ImageEx.TransitionMode.Mask, ImageEx.TransitionMode.Melt,
                ImageEx.TransitionMode.Burn, ImageEx.TransitionMode.Pattern, ImageEx.TransitionMode.Blaze,
            };

            for (int i = 0; i < modes.Length; i++)
            {
                ImageEx.TransitionMode mode = modes[i];
                ImageExGalleryBuilder.AddCard(content, "过渡 · " + mode, "Transition 50%", p =>
                {
                    ImageExGalleryCatalog.ApplySaber(p);
                    p.Transition = mode;
                    p.TransitionTexture = s_transition != null
                        ? s_transition
                        : Resources.Load<Texture2D>("Noise/Transition-Noise00");
                    p.TransitionRate = 0.5f;
                    p.TransitionWidth = 0.25f;
                    p.TransitionSoftness = 0.2f;
                    p.TransitionColor = Accent;
                });
            }
        }

        private static void AddComposites(RectTransform content)
        {
            ImageExGalleryBuilder.AddCard(content, "组合 · Glass Card", "渐变 + 描边 + 投影", p =>
            {
                ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, 35f, 0f, new Color(0.14f, 0.16f, 0.24f), new Color(0.1f, 0.1f, 0.16f));
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.12f);
                p.AppendShadow = true;
                p.Shadow = ImageEx.ShadowMode.Shadow;
                p.ShadowOffsetLocal = new Vector2(0f, -10f);
                p.ShadowBlurIntensity = 8f;
            });

            ImageExGalleryBuilder.AddCard(content, "组合 · Neon", "渐变 + 图案 + 辉光", p =>
            {
                ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, 45f, 10f, Accent, Cyan);
                p.Pattern = ImageEx.PatternMode.Grid;
                p.PatternColor = new Color(1f, 1f, 1f, 0.25f);
                p.PatternSize = new Vector2(24f, 24f);
                p.PatternGap = new Vector2(1f, 1f);
                p.EnableBorderGlow = true;
                p.BorderGlowColor = Cyan;
                p.BorderGlowAmbient = 0.8f;
                p.BorderGlowWidth = 2f;
                p.BorderGlowSoftness = 10f;
            });

            ImageExGalleryBuilder.AddCard(content, "组合 · Avatar", "Saber + 圆形裁剪 + 描边", p =>
            {
                ImageExGalleryCatalog.ApplySaber(p);
                p.DrawShape = DrawShape.Circle;
                p.Circle = new CircleImg { FitToRect = true };
                p.OutlineWidth = 3f;
                p.OutlineColor = Accent;
            });

            ImageExGalleryBuilder.AddCard(content, "组合 · Badge", "方圆形 + 内阴影 + 辉光", p =>
            {
                p.DrawShape = DrawShape.Squircle;
                p.Squircle = new SquircleImg { SquircleTime = 0.35f };
                p.color = new Color(0.17f, 0.19f, 0.25f, 1f);
                p.EnableInnerShadow = true;
                p.InnerShadowColor = new Color(0f, 0f, 0f, 0.8f);
                p.InnerShadowWidth = 22f;
                p.InnerShadowSoftness = 0.6f;
                p.EnableBorderGlow = true;
                p.BorderGlowColor = Orange;
                p.BorderGlowAmbient = 0.6f;
            });

            ImageExGalleryBuilder.AddCard(content, "组合 · Progress", "圆环 + 径向裁剪", p =>
            {
                p.DrawShape = DrawShape.Circle;
                p.Circle = new CircleImg { FitToRect = true };
                p.color = new Color(0.49f, 0.36f, 0.99f, 0.12f);
                p.OutlineWidth = 12f;
                p.OutlineColor = Accent;
                p.Clip = ImageEx.ClipMode.Radial;
                p.ClipAmount = 0.7f;
                p.ClipSoftness = 2f;
            });
        }

    }
}
