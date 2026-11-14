using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using Gear.Utilities.UI.Navigation;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 导航类演示卡：GearPageView。单独成文件，避免 ImageExGalleryBuilder 继续膨胀。
    /// </summary>
    internal static class GearNavigationDemoBuilder
    {
        internal static void AddPageViewCard(RectTransform content, string title, string description)
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
            GameObject viewGo = new GameObject("PageView", typeof(RectTransform), typeof(Image), typeof(GearPageView));
            viewGo.transform.SetParent(clip, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) viewGo.layer = uiLayer;

            Image dragArea = viewGo.GetComponent<Image>();
            dragArea.color = new Color(0f, 0f, 0f, 0f);
            dragArea.raycastTarget = true;

            RectTransform viewRect = (RectTransform)viewGo.transform;
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(viewGo.transform, false);
            RectTransform viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10f, 8f);
            viewport.offsetMax = new Vector2(-10f, -38f);

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            RectTransform pageContent = (RectTransform)contentGo.transform;
            pageContent.anchorMin = new Vector2(0f, 1f);
            pageContent.anchorMax = new Vector2(0f, 1f);
            pageContent.pivot = new Vector2(0f, 1f);
            pageContent.sizeDelta = new Vector2(300f, 100f);
            pageContent.anchoredPosition = Vector2.zero;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 0.9f),
                new Color(0.14f, 0.75f, 0.95f, 0.9f),
                new Color(0.25f, 0.78f, 0.52f, 0.9f),
            };

            List<RectTransform> pages = new List<RectTransform>();
            for (int i = 0; i < 3; i++)
            {
                GameObject page = new GameObject("Page " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
                page.transform.SetParent(pageContent, false);
                if (uiLayer >= 0) page.layer = uiLayer;

                ImageEx image = page.GetComponent<ImageEx>();
                image.DrawShape = DrawShape.Rectangle;
                image.Rectangle = new RectangleImg { CornerRadius = new Vector4(12f, 12f, 12f, 12f) };
                image.color = colors[i];
                image.raycastTarget = false;
                ImageExGalleryBuilder.CreateCenteredLabel(page.transform, (i + 1).ToString(), 36f, Color.white);
                pages.Add((RectTransform)page.transform);
            }

            Button prev = ImageExGalleryBuilder.CreateButton(viewGo.transform, "Prev", "Prev",
                new Vector2(8f, -5f), new Vector2(52f, 24f), colors[0]);
            Button next = ImageExGalleryBuilder.CreateButton(viewGo.transform, "Next", "Next",
                new Vector2(66f, -5f), new Vector2(52f, 24f), colors[1]);

            List<Button> indicators = new List<Button>();
            for (int i = 0; i < 3; i++)
            {
                indicators.Add(ImageExGalleryBuilder.CreateButton(viewGo.transform, "Dot " + (i + 1), (i + 1).ToString(),
                    new Vector2(140f + i * 24f, -5f), new Vector2(20f, 20f), colors[i]));
            }

            GearPageView view = viewGo.GetComponent<GearPageView>();
            view.PageSizeOverride = new Vector2(300f, 100f);
            view.NextButton = next;
            view.BackButton = prev;
            view.SetIndicators(indicators);
            view.Configure(viewport, pageContent, pages);
            view.GoTo(0, true);
        }

        internal static void AddPopoverCard(RectTransform content, string title, string description)
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

            Button open = ImageExGalleryBuilder.CreateButton(preview.transform.parent, "OpenPopover", "Open Popover",
                new Vector2(24f, -58f), new Vector2(120f, 30f), new Color(0.49f, 0.36f, 0.99f, 1f));

            GearPopoverTrigger trigger = open.gameObject.AddComponent<GearPopoverTrigger>();
            trigger.SetContent("Popover", "点击外部或按 Esc 关闭", new Vector2(260f, 128f));
        }
    }
}
