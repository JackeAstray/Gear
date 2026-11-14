using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Interaction;
using Gear.Utilities.UI.Motion;
using Gear.Utilities.UI.Widgets;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 画廊的「第二轮」卡片：圆角同步、五态状态色、指针聚光，以及从主文件迁出的抖动卡。
    /// 独立成 partial 文件，避免 <see cref="ImageExGalleryCatalogExtra"/> 主文件继续膨胀。
    /// </summary>
    internal static partial class ImageExGalleryCatalogExtra
    {
        /// <summary>本轮新增卡片的统一入口，由 Populate / AppendNewCards 调用。</summary>
        internal static void PopulateShowcase(RectTransform content)
        {
            AddRadiusSync(content);
            AddStateColors(content);
            AddSpotlight(content);
            AddAnimatedContainer(content);
            AddRectSway(content);
            AddDockMagnifier(content);
            AddRadialLayout(content);
            AddSpinner(content);
            AddParallax(content);
            AddDistributionBar(content);
            AddWarningStripe(content);
            AddDotMatrix(content);
            AddPixelCurtain(content);
            AddTopographic(content);
            AddTextMotionCard(content);
            AddSpriteFillCard(content);
            AddInkPanel(content);
            PopulateControls(content);
        }

        /// <summary>墨团揭示：从中心扩散，自动往返（Play 模式）。</summary>
        private static void AddInkPanel(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · 墨团揭示",
                "带噪声毛边的墨迹扩散，自动往返（Play 模式）", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(16f, 16f, 16f, 16f) };
                p.color = new Color(0.16f, 0.12f, 0.24f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(0.49f, 0.36f, 0.99f, 0.5f);
            });

            if (preview == null) return;

            GearInkPanel panel = preview.gameObject.AddComponent<GearInkPanel>();
            panel.Origin = new Vector2(0.5f, 0.5f);
            panel.Softness = 0.1f;
            panel.Roughness = 0.5f;
            panel.Duration = 0.8f;
            panel.ApplySettings();
            panel.SetProgress(1f);
        }

        /// <summary>Sprite Fill：Cover 裁切填满，不拉伸。</summary>
        private static void AddSpriteFillCard(RectTransform content)
        {
            AddCardIfMissing(content, "图片 · Cover 裁切", "同图不同比例：裁切填满而不是拉伸", p =>
            {
                ImageExGalleryCatalog.ApplySaber(p);
                p.preserveAspect = false;
                p.SpriteFill = SpriteFillMode.Cover;
            });
        }

        /// <summary>第二轮文本动效：弹跳 / 摇摆 / 颜色循环。</summary>
        private static void AddTextMotionCard(RectTransform content)
        {
            AddTextCard(content, "文本 · 弹跳 / 摇摆", "Play 查看弹跳 / 摇摆 / 颜色循环", "BOUNCE\nSWING\nPALETTE",
                effects =>
                {
                    effects.Typewriter = false;
                    effects.Bounce = true;
                    effects.BounceAmplitude = 6f;
                    effects.BounceSpeed = 2.5f;
                    effects.BouncePhase = 0.3f;
                    effects.Swing = true;
                    effects.SwingAngle = 8f;
                    effects.SwingSpeed = 2f;
                    effects.SwingPhase = 0.35f;
                    effects.Palette = true;
                    effects.PaletteSpeed = 0.6f;
                    effects.PaletteSpread = 0.2f;
                });
        }

        private static ImageEx CreateTile(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tile = go.GetComponent<ImageEx>();
            tile.DrawShape = DrawShape.Rectangle;
            tile.Rectangle = new RectangleImg { CornerRadius = new Vector4(12f, 12f, 12f, 12f) };
            tile.color = color;
            tile.raycastTarget = false;
            return tile;
        }

        /// <summary>错峰出现：4 个方块依次缩放淡入（Play 模式可见）。</summary>
        private static void AddAnimatedContainer(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "动效 · 错峰出现",
                "子项依次缩放 + 淡入，循环播放", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
                new Color(1f, 0.69f, 0.25f, 1f),
            };

            for (int i = 0; i < colors.Length; i++)
            {
                CreateTile(preview.transform, "Item " + (i + 1),
                    new Vector2(-105f + i * 70f, -20f), new Vector2(60f, 60f), colors[i]);
            }

            preview.gameObject.AddComponent<GearAnimatedContainer>();
            GearAnimatedContainer container = preview.GetComponent<GearAnimatedContainer>();
            container.ItemDuration = 0.32f;
            container.Stagger = 0.07f;
            container.FromScale = 0.7f;
            container.Loop = true;
            container.LoopInterval = 1.2f;
        }

        /// <summary>指针倾斜：悬停时卡片朝指针方向轻微旋转（Play 模式）。</summary>
        private static void AddRectSway(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "动效 · 指针倾斜",
                "指针位置驱动旋转，最大 14°（Play 模式）", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(20f, 20f, 20f, 20f) };
                p.color = new Color(0.14f, 0.16f, 0.23f, 1f);
                p.OutlineWidth = 2f;
                p.OutlineColor = new Color(0.49f, 0.36f, 0.99f, 0.9f);
            });

            if (preview == null) return;

            // 留出边距，旋转时不会被 RectMask2D 裁得看不出来
            preview.rectTransform.offsetMin = new Vector2(18f, 18f);
            preview.rectTransform.offsetMax = new Vector2(-18f, -18f);

            GearRectSway sway = preview.gameObject.AddComponent<GearRectSway>();
            sway.MaxAngle = 14f;
        }

        /// <summary>Dock 放大：指针靠近的子项变大，邻居被布局推开（Play 模式）。</summary>
        private static void AddDockMagnifier(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "布局 · Dock 放大",
                "指针附近子项放大，布局自动让位（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = true;
            });

            if (preview == null) return;

            HorizontalLayoutGroup layout = preview.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
                new Color(1f, 0.69f, 0.25f, 1f),
                new Color(0.93f, 0.32f, 0.46f, 1f),
            };

            for (int i = 0; i < colors.Length; i++)
            {
                CreateTile(preview.transform, "Dock " + (i + 1), Vector2.zero, new Vector2(56f, 56f), colors[i]);
            }

            GearDockMagnifier dock = preview.gameObject.AddComponent<GearDockMagnifier>();
            dock.BaseSize = new Vector2(56f, 56f);
            dock.MaxSize = new Vector2(80f, 80f);
            dock.InfluenceRadius = 90f;
        }

        /// <summary>径向布局：子项均匀排布在圆上。</summary>
        private static void AddRadialLayout(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "布局 · 径向",
                "子项按角度均分排布在圆上", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
                new Color(1f, 0.69f, 0.25f, 1f),
                new Color(0.93f, 0.32f, 0.46f, 1f),
                new Color(0.65f, 0.55f, 1f, 1f),
            };

            for (int i = 0; i < colors.Length; i++)
            {
                CreateTile(preview.transform, "Node " + (i + 1), Vector2.zero, new Vector2(42f, 42f), colors[i]);
            }

            GearRadialLayout layout = preview.gameObject.AddComponent<GearRadialLayout>();
            layout.Radius = 52f;
            layout.StartAngle = 0f;
            layout.FillFullCircle = true;
            LayoutRebuilder.MarkLayoutForRebuild(preview.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(preview.rectTransform);
        }

        /// <summary>旋转器：一根指针匀速旋转（Play 模式）。</summary>
        private static void AddSpinner(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "动效 · 旋转器",
                "按固定角速度旋转（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx needle = CreateTile(preview.transform, "Needle", Vector2.zero,
                new Vector2(10f, 76f), new Color(0.49f, 0.36f, 0.99f, 1f));
            needle.Rectangle.CornerRadius = new Vector4(5f, 5f, 5f, 5f);

            GearSpinner spinner = needle.gameObject.AddComponent<GearSpinner>();
            spinner.DegreesPerSecond = 150f;
        }

        /// <summary>视差：指针移动时前景层平移（Play 模式）。</summary>
        private static void AddParallax(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "动效 · 视差",
                "指针位置驱动前景层平移（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = true;
            });

            if (preview == null) return;

            ImageEx layer = CreateTile(preview.transform, "Layer", Vector2.zero,
                new Vector2(220f, 92f), new Color(0.49f, 0.36f, 0.99f, 0.9f));
            layer.Rectangle.CornerRadius = new Vector4(16f, 16f, 16f, 16f);
            layer.transform.SetAsFirstSibling();

            GearRectParallax parallax = preview.gameObject.AddComponent<GearRectParallax>();
            parallax.Target = (RectTransform)layer.transform;
            parallax.MaxOffset = new Vector2(20f, 12f);
        }

        /// <summary>分布条：按权重切分子项宽度。</summary>
        private static void AddDistributionBar(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "布局 · 分布条",
                "按 3:2:1 权重分配宽度", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
            };

            for (int i = 0; i < colors.Length; i++)
            {
                CreateTile(preview.transform, "Part " + (i + 1), Vector2.zero,
                    new Vector2(80f, 68f), colors[i]);
            }

            GearDistributionBar bar = preview.gameObject.AddComponent<GearDistributionBar>();
            bar.SetWeights(3f, 2f, 1f);
        }

        /// <summary>警戒斜纹：直接用 Pattern 的 Stripes 模式，无需新 shader。</summary>
        private static void AddWarningStripe(RectTransform content)
        {
            AddCardIfMissing(content, "效果 · 警戒斜纹", "Pattern Stripes 45°，可滚动", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
                p.color = new Color(0.12f, 0.1f, 0.06f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(1f, 0.69f, 0.25f, 0.45f);
                p.Pattern = ImageEx.PatternMode.Stripes;
                p.PatternSize = new Vector2(26f, 18f);
                p.PatternAngle = 45f;
                p.PatternColor = new Color(1f, 0.69f, 0.25f, 1f);
                p.PatternSpeed = new Vector2(0f, 12f);
                p.PatternOpacity = 0.9f;
            });
        }

        /// <summary>点阵图案：8 种解析图案，用组件驱动 ImageEx 的 DotMatrix uniform。</summary>
        private static void AddDotMatrix(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · 点阵", "8 种解析图案（波浪/雷达/螺旋…）", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(16f, 16f, 16f, 16f) };
                p.color = new Color(0.07f, 0.08f, 0.12f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(0.49f, 0.36f, 0.99f, 0.6f);
            });

            if (preview == null) return;

            GearDotMatrix dots = preview.gameObject.AddComponent<GearDotMatrix>();
            dots.Pattern = DotMatrixPattern.Wave;
            dots.Shape = DotMatrixShape.Circle;
            dots.Cell = 14f;
            dots.Radius = 0.3f;
            dots.Intensity = 0.85f;
            dots.Color = new Color(0.49f, 0.36f, 0.99f, 1f);
            dots.Speed = 1f;
            dots.ApplySettings();
        }

        /// <summary>像素幕帘：自动往返做逐格揭示/隐藏（Play 模式）。</summary>
        private static void AddPixelCurtain(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · 像素幕帘",
                "逐格揭示/隐藏，自动往返（Play 模式）", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(16f, 16f, 16f, 16f) };
                p.color = new Color(0.14f, 0.18f, 0.3f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(0.14f, 0.75f, 0.95f, 0.6f);
            });

            if (preview == null) return;

            GearPixelCurtain curtain = preview.gameObject.AddComponent<GearPixelCurtain>();
            curtain.Cell = 16f;
            curtain.Randomness = 0.4f;
            curtain.Softness = 0.12f;
            curtain.Direction = GearCurtainDirection.LeftToRight;
            curtain.Duration = 0.7f;
            curtain.ApplySettings();
            curtain.SetProgress(1f);
        }

        /// <summary>地形等值线：程序化噪声等高线（可缓慢滚动）。</summary>
        private static void AddTopographic(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · 地形等值线",
                "程序化噪声等高线，可滚动", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(16f, 16f, 16f, 16f) };
                p.color = new Color(0.06f, 0.09f, 0.13f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(0.14f, 0.75f, 0.95f, 0.35f);
            });

            if (preview == null) return;

            GearTopographic topo = preview.gameObject.AddComponent<GearTopographic>();
            topo.Scale = 4f;
            topo.Density = 8f;
            topo.LineWidth = 0.04f;
            topo.MajorEvery = 4f;
            topo.Color = new Color(0.14f, 0.75f, 0.95f, 1f);
            topo.Intensity = 0.55f;
            topo.Speed = 0.3f;
            topo.Falloff = 0.3f;
            topo.ApplySettings();
        }

        private static void AddDither(RectTransform content)
        {
            AddCardIfMissing(content, "抖动 · Dither", "打散 8bit 渐变色带", p =>
            {
                ImageExGalleryCatalog.ApplyGradient(p, GradientType.Linear, 45f, 0f,
                    new Color(0.05f, 0.05f, 0.08f), new Color(0.18f, 0.16f, 0.3f));
                p.Dither = true;
            });
        }

        /// <summary>圆角同步：子卡片跟随父级圆角，父级改半径时子级一起变。</summary>
        private static void AddRadiusSync(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "形状 · 圆角同步", "子卡片跟随父级圆角，内缩 6px", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(30f, 30f, 30f, 30f) };
                p.color = new Color(0.16f, 0.18f, 0.25f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.12f);
            });

            if (preview == null) return;

            GameObject childGo = new GameObject("Synced Child",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            childGo.transform.SetParent(preview.transform, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) childGo.layer = uiLayer;

            RectTransform childRect = (RectTransform)childGo.transform;
            childRect.anchorMin = new Vector2(0.5f, 0.5f);
            childRect.anchorMax = new Vector2(0.5f, 0.5f);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.sizeDelta = new Vector2(180f, 70f);
            childRect.anchoredPosition = new Vector2(0f, -34f);

            ImageEx child = childGo.GetComponent<ImageEx>();
            child.DrawShape = DrawShape.Rectangle;
            child.Rectangle = new RectangleImg();
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchParent;
            child.Rectangle.SyncInset = 6f;
            child.color = new Color(0.49f, 0.36f, 0.99f, 1f);
            child.raycastTarget = false;
        }

        private static ImageExStateColors.StateStyle MakeStyle(Color fill, Color outline, Color glow, float intensity)
        {
            return new ImageExStateColors.StateStyle
            {
                Fill = fill,
                Outline = outline,
                Glow = glow,
                GlowIntensity = intensity,
            };
        }

        /// <summary>五态状态色：hover / press / selected 自动换色与辉光。</summary>
        private static void AddStateColors(RectTransform content)
        {
            Color baseFill = new Color(0.13f, 0.15f, 0.21f, 1f);
            ImageEx preview = AddCardIfMissing(content, "效果 · 五态状态色",
                "hover / press / selected 换色与辉光", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(18f, 18f, 18f, 18f) };
                p.color = baseFill;
                p.OutlineWidth = 2f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.08f);
            });

            if (preview == null) return;

            GearInteractive interactive = preview.gameObject.AddComponent<GearInteractive>();
            ImageExStateColors stateColors = preview.gameObject.AddComponent<ImageExStateColors>();
            stateColors.SetStyle(GearInteractionState.Normal,
                MakeStyle(baseFill, new Color(1f, 1f, 1f, 0.08f), Color.white, 0f));
            stateColors.SetStyle(GearInteractionState.Highlighted,
                MakeStyle(new Color(0.18f, 0.2f, 0.28f, 1f), new Color(0.49f, 0.36f, 0.99f, 1f),
                    new Color(0.49f, 0.36f, 0.99f, 1f), 0.35f));
            stateColors.SetStyle(GearInteractionState.Pressed,
                MakeStyle(new Color(0.11f, 0.12f, 0.17f, 1f), new Color(0.49f, 0.36f, 0.99f, 1f),
                    new Color(0.49f, 0.36f, 0.99f, 1f), 0.6f));
            stateColors.SetStyle(GearInteractionState.Selected,
                MakeStyle(new Color(0.16f, 0.17f, 0.24f, 1f), new Color(0.49f, 0.36f, 0.99f, 1f),
                    new Color(0.49f, 0.36f, 0.99f, 1f), 0.5f));
            stateColors.SetStyle(GearInteractionState.Disabled,
                MakeStyle(new Color(0.6f, 0.6f, 0.6f, 0.45f), new Color(1f, 1f, 1f, 0.05f), Color.white, 0f));

            interactive.Refresh();
            stateColors.Bind();
        }

        /// <summary>指针聚光：悬停时在卡片内部投一团软光。</summary>
        private static void AddSpotlight(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · Spotlight", "指针在卡片内部投射软光", p =>
            {
                p.DrawShape = DrawShape.Rectangle;
                p.Rectangle = new RectangleImg { CornerRadius = new Vector4(20f, 20f, 20f, 20f) };
                p.color = new Color(0.09f, 0.1f, 0.15f, 1f);
                p.OutlineWidth = 1f;
                p.OutlineColor = new Color(1f, 1f, 1f, 0.1f);
            });

            if (preview == null) return;

            ImageExSpotlight spot = preview.gameObject.AddComponent<ImageExSpotlight>();
            spot.Color = new Color(0.65f, 0.55f, 1f, 1f);
            spot.Radius = 150f;
            spot.Softness = 0.65f;
            spot.Intensity = 0.45f;
            spot.ApplySettings();
        }
    }
}
