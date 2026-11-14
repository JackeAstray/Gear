using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 效果类演示卡：滚动列表 + 软遮罩，滚动时上下边缘渐隐。
    /// </summary>
    internal static class GearEffectsDemoBuilder
    {
        internal static void AddScrollFadeCard(RectTransform content, string title, string description)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == title)
                    return;
            }

            ImageEx preview = ImageExGalleryBuilder.AddCard(content, title, description, p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null || preview.transform.parent == null)
                return;

            Transform clip = preview.transform.parent;
            int uiLayer = LayerMask.NameToLayer("UI");

            GameObject scrollGo = new GameObject("ScrollFade", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(GearScrollFade));
            scrollGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) scrollGo.layer = uiLayer;

            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(8f, 8f);
            scrollRect.offsetMax = new Vector2(-8f, -8f);

            Image scrollHit = scrollGo.GetComponent<Image>();
            scrollHit.color = new Color(0f, 0f, 0f, 0f);
            scrollHit.raycastTarget = true;

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(GearSoftMask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            if (uiLayer >= 0) viewportGo.layer = uiLayer;

            RectTransform viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            Image viewportHit = viewportGo.GetComponent<Image>();
            viewportHit.color = new Color(0f, 0f, 0f, 0f);
            viewportHit.raycastTarget = true;

            GearSoftMask mask = viewportGo.GetComponent<GearSoftMask>();
            mask.Softness = 16f;
            mask.Radius = 10f;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            if (uiLayer >= 0) contentGo.layer = uiLayer;

            RectTransform pageContent = (RectTransform)contentGo.transform;
            pageContent.anchorMin = new Vector2(0f, 1f);
            pageContent.anchorMax = new Vector2(1f, 1f);
            pageContent.pivot = new Vector2(0.5f, 1f);
            pageContent.anchoredPosition = Vector2.zero;
            pageContent.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 0.85f),
                new Color(0.14f, 0.75f, 0.95f, 0.85f),
                new Color(0.25f, 0.78f, 0.52f, 0.85f),
                new Color(1f, 0.69f, 0.25f, 0.85f),
            };

            for (int i = 0; i < 8; i++)
            {
                GameObject row = new GameObject("Row " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(LayoutElement));
                row.transform.SetParent(contentGo.transform, false);
                if (uiLayer >= 0) row.layer = uiLayer;

                LayoutElement rowLayout = row.GetComponent<LayoutElement>();
                rowLayout.preferredHeight = 44f;
                rowLayout.minHeight = 44f;
                rowLayout.flexibleHeight = 0f;

                ImageEx image = row.GetComponent<ImageEx>();
                image.DrawShape = DrawShape.Rectangle;
                image.Rectangle = new RectangleImg { CornerRadius = new Vector4(10f, 10f, 10f, 10f) };
                image.color = colors[i % colors.Length];
                image.raycastTarget = false;
                ImageExGalleryBuilder.CreateCenteredLabel(row.transform, "Row " + (i + 1), 15f, Color.white);
            }

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = pageContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;

            GearScrollFade fade = scrollGo.GetComponent<GearScrollFade>();
            fade.Mask = mask;
        }
    }
}
