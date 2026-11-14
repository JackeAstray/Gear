using UnityEditor;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageEx 画廊的完整目录：形状 / 渐变 / 图案 / 描边 / 阴影 / 裁剪 / 辉光 / 滤镜 / 过渡。
    /// 统一由 <see cref="ImageExGalleryBuilder.AddCard"/> 生成到 UI Example 的 Content 下。
    /// </summary>
    internal static class ImageExGalleryCatalog
    {
        private static readonly Color Accent = new Color(0.49f, 0.36f, 0.99f);
        private static readonly Color Cyan = new Color(0.14f, 0.75f, 0.95f);
        private static readonly Color Green = new Color(0.25f, 0.78f, 0.52f);
        private static readonly Color Orange = new Color(1f, 0.69f, 0.25f);
        private static readonly Color Surface = new Color(0.11f, 0.12f, 0.17f);

        private static Sprite s_saber;
        private static Texture2D s_detail;
        private static Texture2D s_transitionMelt;
        private static Texture2D s_transitionNoise;

        internal static void Populate(RectTransform content)
        {
            AddGradients(content);
            AddShapes(content);
            AddPatterns(content);
            AddOutlines(content);
            AddShadows(content);
            AddClips(content);
            AddGlows(content);
            AddFiltersAndTransitions(content);
            ImageExGalleryCatalogExtra.Populate(content);
        }

        private static void AddGradients(RectTransform content)
        {
            ImageExGalleryBuilder.AddCard(content, "渐变 · Linear", "45° 紫→青，自动流动", p =>
                ApplyGradient(p, GradientType.Linear, 45f, 30f, Accent, Cyan));

            ImageExGalleryBuilder.AddCard(content, "渐变 · Radial", "中心向外，可缩放/偏移", p =>
                ApplyGradient(p, GradientType.Radial, 0f, 0f, Accent, Cyan));

            ImageExGalleryBuilder.AddCard(content, "渐变 · Angular", "新增角向扇面渐变", p =>
                ApplyGradient(p, GradientType.Angular, 0f, 20f, Accent, Orange));

            ImageExGalleryBuilder.AddCard(content, "渐变 · Corner", "四角独立颜色", p =>
            {
                p.color = Color.white;
                var g = p.GradientEffect;
                g.Enabled = true;
                g.GradientType = GradientType.Corner;
                g.CornerGradientColors = new[] { Accent, Cyan, new Color(0.1f, 0.1f, 0.2f), Orange };
                p.GradientEffect = g;
            });
        }

        internal static void AddShapes(RectTransform content)
        {
            AddShape(content, "形状 · Circle", "圆形 + 自动适配矩形", DrawShape.Circle, p =>
                p.Circle = new CircleImg { FitToRect = true });

            AddShape(content, "形状 · Triangle", "三角形 + 圆角", DrawShape.Triangle, p =>
                p.Triangle = new TriangleImg { CornerRadius = new Vector3(0.1f, 0.1f, 0.1f) });

            AddShape(content, "形状 · Rectangle", "圆角矩形（SDF 圆角）", DrawShape.Rectangle, p =>
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(28f, 28f, 28f, 28f) });

            AddShape(content, "形状 · Pentagon", "五边形 + 顶部尖角", DrawShape.Pentagon, p =>
                p.Pentagon = new PentagonImg
                {
                    CornerRadius = new Vector4(0.06f, 0.06f, 0.06f, 0.06f),
                    TipRadius = 0.001f,
                    TipSize = 70f,
                });

            AddShape(content, "形状 · Hexagon", "六边形 + 尖角/圆角", DrawShape.Hexagon, p =>
                p.Hexagon = new HexagonImg
                {
                    CornerRadius = new Vector4(1f, 1f, 1f, 1f),
                    TipSize = new Vector2(50f, 50f),
                    TipRadius = new Vector2(1f, 1f),
                });

            AddShape(content, "形状 · Chamfer Box", "倒角盒子", DrawShape.ChamferBox, p =>
                p.ChamferBox = new ChamferBoxImg
                {
                    ChamferBoxSize = new Vector2(1.25f, 0.9f),
                    ChamferBoxRadius = new Vector4(0.22f, 0.22f, 0.22f, 0.22f),
                });

            AddShape(content, "形状 · Quadrilateral", "四边形/梯形偏移", DrawShape.Quadrilateral, p =>
                p.Quadrilateral = new QuadrilateralImg
                {
                    TopLeftOffset = new Vector2(0.16f, 0f),
                    TopRightOffset = new Vector2(0f, 0.16f),
                    BottomLeftOffset = new Vector2(0f, 0.16f),
                    BottomRightOffset = new Vector2(0.16f, 0f),
                });

            AddShape(content, "形状 · N-Star", "五角星 + 内凹", DrawShape.NStarPolygon, p =>
                p.NStarPolygon = new NStarPolygonImg
                {
                    SideCount = 5f,
                    Inset = 4f,
                    CornerRadius = 0.04f,
                });

            AddShape(content, "形状 · Heart", "心形", DrawShape.Heart, null);

            AddShape(content, "形状 · Blobby Cross", "圆润十字", DrawShape.BlobbyCross, p =>
                p.BlobbyCross = new BlobbyCrossImg { BlobbyCrossTime = 0.35f });

            AddShape(content, "形状 · Squircle", "方圆形", DrawShape.Squircle, p =>
                p.Squircle = new SquircleImg { SquircleTime = 0.25f });

            AddShape(content, "形状 · N-Triangle", "N 边圆角三角形", DrawShape.NTriangleRounded, p =>
                p.NTriangleRounded = new NTriangleRoundedImg { NTriangleRoundedNumber = 3f });
        }

        private static void AddPatterns(RectTransform content)
        {
            AddPattern(content, "图案 · Dots", "点阵 + 径向衰减", ImageEx.PatternMode.Dots,
                new Vector2(18f, 18f), new Vector2(7f, 7f), new Vector2(0.15f, 0.9f));

            AddPattern(content, "图案 · Grid", "网格线", ImageEx.PatternMode.Grid,
                new Vector2(28f, 28f), new Vector2(2f, 2f), Vector2.zero);

            AddPattern(content, "图案 · Stripes", "纵向条纹", ImageEx.PatternMode.Stripes,
                new Vector2(26f, 26f), new Vector2(8f, 8f), Vector2.zero);

            AddPattern(content, "图案 · Checker", "棋盘格", ImageEx.PatternMode.Checker,
                new Vector2(30f, 30f), new Vector2(3f, 3f), Vector2.zero);

            AddPattern(content, "图案 · Rings", "同心圆环", ImageEx.PatternMode.Rings,
                new Vector2(34f, 34f), new Vector2(3f, 3f), Vector2.zero);
        }

        private static void AddOutlines(RectTransform content)
        {
            AddOutline(content, "描边 · Solid", "实线 SDF 描边", false, false);

            AddOutline(content, "虚线 · Square", "方头虚线，沿矩形周长", true, false);

            AddOutline(content, "虚线 · Round Caps", "圆头帽（新增）", true, true);

            ImageExGalleryBuilder.AddCard(content, "虚线 · Circle", "圆形按周长连续排布", p =>
            {
                p.DrawShape = DrawShape.Circle;
                p.Circle = new CircleImg { FitToRect = true };
                p.color = Surface;
                p.OutlineWidth = 4f;
                p.OutlineColor = Accent;
                p.EnableDashedOutline = 1;
                p.DashLength = 26f;
                p.DashGap = 12f;
                p.DashRounded = true;
            });
        }

        private static void AddShadows(RectTransform content)
        {
            ImageExGalleryBuilder.AddCard(content, "投影 · Drop", "方向 Offset + 羽化", p =>
            {
                p.color = new Color(0.82f, 0.85f, 0.9f, 1f);
                p.AppendShadow = true;
                p.Shadow = ImageEx.ShadowMode.Shadow;
                p.ShadowOffsetLocal = new Vector2(0f, -14f);
                p.ShadowBlurIntensity = 10f;
                p.ShadowColor = new Color(0.49f, 0.36f, 0.99f, 0.85f);
            });

            ImageExGalleryBuilder.AddCard(content, "投影 · Spread", "外扩 + 硬边", p =>
            {
                p.color = new Color(0.82f, 0.85f, 0.9f, 1f);
                p.AppendShadow = true;
                p.Shadow = ImageEx.ShadowMode.Shadow;
                p.ShadowOffsetLocal = new Vector2(12f, -12f);
                p.ShadowBlurIntensity = 2f;
                p.ShadowColor = Accent;
            });

            ImageExGalleryBuilder.AddCard(content, "内阴影 · Offset", "向右下偏移", p =>
            {
                p.color = new Color(0.8f, 0.82f, 0.88f, 1f);
                p.EnableInnerShadow = true;
                p.InnerShadowColor = new Color(0f, 0f, 0f, 0.9f);
                p.InnerShadowWidth = 26f;
                p.InnerShadowSoftness = 0.7f;
            });

            ImageExGalleryBuilder.AddCard(content, "内阴影 · Soft", "大宽度 + 柔边", p =>
            {
                p.color = new Color(0.8f, 0.82f, 0.88f, 1f);
                p.EnableInnerShadow = true;
                p.InnerShadowColor = new Color(0.49f, 0.36f, 0.99f, 0.8f);
                p.InnerShadowWidth = 34f;
                p.InnerShadowSoftness = 1f;
            });
        }

        private static void AddClips(RectTransform content)
        {
            AddClip(content, "裁剪 · Horizontal 65%", ImageEx.ClipMode.Horizontal, 0.65f);
            AddClip(content, "裁剪 · Vertical 35%", ImageEx.ClipMode.Vertical, 0.35f);

            ImageExGalleryBuilder.AddCard(content, "裁剪 · Radial 65%", "扇形进度", p =>
            {
                p.DrawShape = DrawShape.Circle;
                p.Circle = new CircleImg { FitToRect = true };
                p.color = Accent;
                p.Clip = ImageEx.ClipMode.Radial;
                p.ClipAmount = 0.65f;
                p.ClipSoftness = 4f;
            });
        }

        private static void AddGlows(RectTransform content)
        {
            ImageExGalleryBuilder.AddCard(content, "辉光 · Ambient", "整圈常亮", p =>
            {
                p.color = Surface;
                p.EnableBorderGlow = true;
                p.BorderGlowColor = Accent;
                p.BorderGlowWidth = 2f;
                p.BorderGlowSoftness = 12f;
                p.BorderGlowAmbient = 0.8f;
                p.BorderGlowFollow = 0f;
            });

            ImageExGalleryBuilder.AddCard(content, "辉光 · Pointer", "指针跟随；静态也保留环境光", p =>
            {
                p.color = Surface;
                p.EnableBorderGlow = true;
                p.BorderGlowColor = Cyan;
                p.BorderGlowWidth = 2f;
                p.BorderGlowSoftness = 14f;
                p.BorderGlowAmbient = 0.5f;
                p.BorderGlowFollow = 1f;
                ImageExPointerGlow glow = p.gameObject.AddComponent<ImageExPointerGlow>();
                glow.HoverOnly = false;
            });
        }

        private static void AddFiltersAndTransitions(RectTransform content)
        {
            ImageExGalleryBuilder.AddCard(content, "滤镜 · Grayscale", "Tone → 灰度", p =>
            {
                ApplySaber(p);
                p.Tone = ImageEx.ToneFilter.Grayscale;
                p.ToneIntensity = 1f;
            });

            ImageExGalleryBuilder.AddCard(content, "滤镜 · HSV", "色相偏移", p =>
            {
                ApplySaber(p);
                p.ColorFilterMode = ImageEx.ColorMode.HsvModifier;
                p.ColorValue = new Color(0.25f, 0.05f, 0.05f, 1f);
                p.ColorIntensity = 1f;
            });

            ImageExGalleryBuilder.AddCard(content, "滤镜 · Edge Shiny", "边缘高光", p =>
            {
                ApplySaber(p);
                p.Edge = ImageEx.EdgeMode.Shiny;
                p.EdgeColor = Accent;
                p.EdgeWidth = 0.6f;
                p.EdgeShinyRate = 0.5f;
                p.EdgeShinyWidth = 0.3f;
                p.EdgeShinyAutoPlaySpeed = 1f;
            });

            ImageExGalleryBuilder.AddCard(content, "滤镜 · Pixelation", "采样像素化", p =>
            {
                ApplySaber(p);
                p.Sampling = ImageEx.SamplingFilter.Pixelation;
                p.SamplingIntensity = 0.55f;
            });

            ImageExGalleryBuilder.AddCard(content, "滤镜 · Target Hue", "目标色相", p =>
            {
                ApplySaber(p);
                p.Target = ImageEx.TargetMode.Hue;
                p.TargetColor = Accent;
                p.TargetRange = 0.2f;
                p.TargetSoftness = 0.5f;
            });

            ImageExGalleryBuilder.AddCard(content, "滤镜 · Detail", "细节纹理叠加", p =>
            {
                ApplySaber(p);
                p.Detail = ImageEx.DetailFilter.Multiply;
                p.DetailTex = s_detail != null ? s_detail : Resources.Load<Texture2D>("Noise/Detail-Hologram");
                p.DetailIntensity = 0.8f;
            });

            ImageExGalleryBuilder.AddCard(content, "过渡 · Fade", "淡入淡出 50%", p =>
            {
                ApplyGradient(p, GradientType.Linear, 30f, 0f, Accent, Cyan);
                p.Transition = ImageEx.TransitionMode.Fade;
                p.TransitionRate = 0.5f;
                p.TransitionWidth = 0.2f;
                p.TransitionSoftness = 0.2f;
                p.TransitionColor = Accent;
            });

            ImageExGalleryBuilder.AddCard(content, "过渡 · Melt", "Melt 纹理过渡", p =>
            {
                ApplySaber(p);
                p.Transition = ImageEx.TransitionMode.Melt;
                p.TransitionTexture = s_transitionMelt != null ? s_transitionMelt : Resources.Load<Texture2D>("Noise/Transition-Melt00");
                p.TransitionRate = 0.5f;
                p.TransitionWidth = 0.25f;
                p.TransitionSoftness = 0.2f;
                p.TransitionColor = Accent;
            });

            ImageExGalleryBuilder.AddCard(content, "过渡 · Shiny", "扫光过渡", p =>
            {
                ApplyGradient(p, GradientType.Linear, 45f, 0f, Accent, Cyan);
                p.Transition = ImageEx.TransitionMode.Shiny;
                p.TransitionRate = 0.5f;
                p.TransitionWidth = 0.2f;
                p.TransitionSoftness = 0.2f;
                p.TransitionColor = Color.white;
            });
        }

        private static void AddShape(RectTransform content, string title, string description, DrawShape shape,
            System.Action<ImageEx> configure)
        {
            ImageExGalleryBuilder.AddCard(content, title, description, p =>
            {
                p.DrawShape = shape;
                p.color = Accent;
                configure?.Invoke(p);
            });
        }

        private static void AddPattern(RectTransform content, string title, string description,
            ImageEx.PatternMode mode, Vector2 size, Vector2 gap, Vector2 falloff)
        {
            ImageExGalleryBuilder.AddCard(content, title, description, p =>
            {
                p.color = Surface;
                p.Pattern = mode;
                p.PatternSize = size;
                p.PatternGap = gap;
                p.PatternColor = Accent;
                p.PatternSoftness = 1.2f;
                p.PatternFalloff = falloff;
                p.PatternSpeed = mode == ImageEx.PatternMode.Dots ? new Vector2(6f, 0f) : Vector2.zero;
            });
        }

        private static void AddOutline(RectTransform content, string title, string description, bool dashed, bool rounded)
        {
            ImageExGalleryBuilder.AddCard(content, title, description, p =>
            {
                p.color = Surface;
                p.OutlineWidth = 4f;
                p.OutlineColor = Accent;
                p.EnableDashedOutline = dashed ? 1 : 0;
                if (dashed)
                {
                    p.DashLength = 22f;
                    p.DashGap = 12f;
                    p.DashRounded = rounded;
                    p.DashSoftness = 0.15f;
                }
            });
        }

        private static void AddClip(RectTransform content, string title, ImageEx.ClipMode mode, float amount)
        {
            ImageExGalleryBuilder.AddCard(content, title, "进度裁剪", p =>
            {
                ApplyGradient(p, GradientType.Linear, 45f, 0f, Accent, Cyan);
                p.Clip = mode;
                p.ClipAmount = amount;
                p.ClipSoftness = 4f;
            });
        }

        internal static void ApplyGradient(ImageEx p, GradientType type, float rotation, float speed, Color a, Color b)
        {
            p.color = Color.white;
            var g = p.GradientEffect;
            g.Enabled = true;
            g.GradientType = type;
            g.Rotation = rotation;
            g.Speed = speed;
            g.Gradient = ImageExGalleryBuilder.MakeGradient(a, b);
            p.GradientEffect = g;
        }

        internal static void ApplySaber(ImageEx p)
        {
            if (s_saber == null)
            {
                s_saber = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Gear/Resources/UI/Sprites/Saber.png");
            }

            if (s_saber != null)
            {
                p.sprite = s_saber;
                p.preserveAspect = true;
                p.color = Color.white;
            }
            else
            {
                p.color = Accent;
            }
        }
    }
}
