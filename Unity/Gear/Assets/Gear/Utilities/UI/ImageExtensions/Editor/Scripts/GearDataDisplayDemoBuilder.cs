using TMPro;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>数据展示演示卡：GearProgressBar + GearCounter。</summary>
    internal static class GearDataDisplayDemoBuilder
    {
        internal static void AddProgressCounterCard(RectTransform content, string title, string description)
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

            GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            panelGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) panelGo.layer = uiLayer;

            RectTransform panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(16f, 16f);
            panel.offsetMax = new Vector2(-16f, -16f);

            ImageEx panelImage = panelGo.GetComponent<ImageEx>();
            panelImage.DrawShape = DrawShape.Rectangle;
            panelImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(16f, 16f, 16f, 16f) };
            panelImage.color = new Color(0.08f, 0.09f, 0.14f, 0.92f);
            panelImage.raycastTarget = false;

            TextMeshProUGUI heading = ImageExGalleryBuilder.CreateCenteredLabel(
                panel, "PROGRESS / COUNTER", 14f, new Color(1f, 1f, 1f, 0.65f));
            RectTransform headingRect = (RectTransform)heading.transform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(1f, 1f);
            headingRect.pivot = new Vector2(0.5f, 1f);
            headingRect.sizeDelta = new Vector2(0f, 24f);
            headingRect.anchoredPosition = new Vector2(0f, -8f);

            GameObject trackGo = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            trackGo.transform.SetParent(panel, false);
            if (uiLayer >= 0) trackGo.layer = uiLayer;

            RectTransform track = (RectTransform)trackGo.transform;
            track.anchorMin = new Vector2(0f, 1f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(0.5f, 1f);
            track.sizeDelta = new Vector2(0f, 14f);
            track.anchoredPosition = new Vector2(0f, -42f);

            ImageEx trackImage = trackGo.GetComponent<ImageEx>();
            trackImage.DrawShape = DrawShape.Rectangle;
            trackImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(7f, 7f, 7f, 7f) };
            trackImage.color = new Color(1f, 1f, 1f, 0.12f);
            trackImage.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            fillGo.transform.SetParent(track, false);
            if (uiLayer >= 0) fillGo.layer = uiLayer;

            RectTransform fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            ImageEx fillImage = fillGo.GetComponent<ImageEx>();
            fillImage.DrawShape = DrawShape.Rectangle;
            fillImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(7f, 7f, 7f, 7f) };
            fillImage.color = new Color(0.49f, 0.36f, 0.99f, 1f);
            fillImage.raycastTarget = false;

            TextMeshProUGUI percent = ImageExGalleryBuilder.CreateCenteredLabel(
                track, "62%", 12f, new Color(1f, 1f, 1f, 0.9f));
            RectTransform percentRect = (RectTransform)percent.transform;
            percentRect.anchorMin = Vector2.zero;
            percentRect.anchorMax = Vector2.one;
            percentRect.offsetMin = Vector2.zero;
            percentRect.offsetMax = Vector2.zero;

            GearProgressBar bar = trackGo.AddComponent<GearProgressBar>();
            bar.Bind(fill, percent);
            bar.SetFormat("{0:P0}");
            bar.SetValueImmediate(0.62f);

            TextMeshProUGUI counterLabel = ImageExGalleryBuilder.CreateCenteredLabel(
                panel, "1,234", 32f, Color.white);
            RectTransform counterRect = (RectTransform)counterLabel.transform;
            counterRect.anchorMin = new Vector2(0.5f, 0f);
            counterRect.anchorMax = new Vector2(0.5f, 0f);
            counterRect.pivot = new Vector2(0.5f, 0f);
            counterRect.sizeDelta = new Vector2(220f, 48f);
            counterRect.anchoredPosition = new Vector2(0f, 16f);

            GearCounter counter = counterLabel.gameObject.AddComponent<GearCounter>();
            counter.Bind(counterLabel);
            counter.SetFormat("{0:N0}");
            counter.SetValueImmediate(1234f);
        }
    }
}
