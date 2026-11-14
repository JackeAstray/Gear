using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI.Charts;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>图表演示卡：折线 + 柱状，最小数据展示。</summary>
    internal static class GearChartDemoBuilder
    {
        internal static void AddChartCard(RectTransform content, string title, string description)
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

            int uiLayer = LayerMask.NameToLayer("UI");
            Transform clip = preview.transform.parent;

            GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            panelGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) panelGo.layer = uiLayer;

            RectTransform panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(12f, 12f);
            panel.offsetMax = new Vector2(-12f, -12f);

            ImageEx panelImage = panelGo.GetComponent<ImageEx>();
            panelImage.DrawShape = DrawShape.Rectangle;
            panelImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
            panelImage.color = new Color(0.07f, 0.08f, 0.12f, 0.92f);
            panelImage.raycastTarget = false;

            CreateChart<GearLineChart>(panel, "Line", new Vector2(0f, 0.12f), new Vector2(0.48f, 0.82f),
                new[] { 0.2f, 0.55f, 0.35f, 0.8f, 0.6f }, new Color(0.49f, 0.36f, 0.99f, 1f));
            CreateChart<GearBarChart>(panel, "Bar", new Vector2(0.52f, 0.12f), new Vector2(1f, 0.82f),
                new[] { 0.35f, 0.7f, 0.5f, 0.9f }, new Color(0.14f, 0.75f, 0.95f, 1f));

            AddLabel(panel, "LINE", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            AddLabel(panel, "BAR", new Vector2(0.52f, 0f), new Vector2(1f, 1f));
        }

        internal static void AddPieLegendCard(RectTransform content, string title, string description)
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

            int uiLayer = LayerMask.NameToLayer("UI");
            Transform clip = preview.transform.parent;

            GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            panelGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) panelGo.layer = uiLayer;

            RectTransform panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(12f, 12f);
            panel.offsetMax = new Vector2(-12f, -12f);

            ImageEx panelImage = panelGo.GetComponent<ImageEx>();
            panelImage.DrawShape = DrawShape.Rectangle;
            panelImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
            panelImage.color = new Color(0.07f, 0.08f, 0.12f, 0.92f);
            panelImage.raycastTarget = false;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
            };

            GameObject pieGo = new GameObject("Pie", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearPieChart));
            pieGo.transform.SetParent(panel, false);
            if (uiLayer >= 0) pieGo.layer = uiLayer;
            RectTransform pieRect = (RectTransform)pieGo.transform;
            pieRect.anchorMin = new Vector2(0.02f, 0.1f);
            pieRect.anchorMax = new Vector2(0.56f, 0.95f);
            pieRect.offsetMin = Vector2.zero;
            pieRect.offsetMax = Vector2.zero;
            GearPieChart pie = pieGo.GetComponent<GearPieChart>();
            pie.raycastTarget = true;
            pie.SetData(new[] { 0.5f, 0.3f, 0.2f }, colors);

            GameObject legendGo = new GameObject("Legend", typeof(RectTransform), typeof(GearChartLegend));
            legendGo.transform.SetParent(panel, false);
            if (uiLayer >= 0) legendGo.layer = uiLayer;
            RectTransform legendRect = (RectTransform)legendGo.transform;
            legendRect.anchorMin = new Vector2(0.6f, 0.1f);
            legendRect.anchorMax = new Vector2(0.98f, 0.9f);
            legendRect.offsetMin = Vector2.zero;
            legendRect.offsetMax = Vector2.zero;

            TMP_Text[] labels = new TMP_Text[3];
            Graphic[] swatches = new Graphic[3];
            string[] names = { "Alpha", "Beta", "Gamma" };
            for (int i = 0; i < 3; i++)
            {
                GameObject row = new GameObject("LegendRow " + i, typeof(RectTransform));
                row.transform.SetParent(legendGo.transform, false);
                if (uiLayer >= 0) row.layer = uiLayer;
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, 22f);
                rowRect.anchoredPosition = new Vector2(0f, -i * 26f);

                GameObject swatch = new GameObject("Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                swatch.transform.SetParent(row.transform, false);
                if (uiLayer >= 0) swatch.layer = uiLayer;
                RectTransform swatchRect = (RectTransform)swatch.transform;
                swatchRect.anchorMin = new Vector2(0f, 0.5f);
                swatchRect.anchorMax = new Vector2(0f, 0.5f);
                swatchRect.pivot = new Vector2(0f, 0.5f);
                swatchRect.sizeDelta = new Vector2(10f, 10f);
                swatchRect.anchoredPosition = new Vector2(2f, 0f);
                Image swatchImage = swatch.GetComponent<Image>();
                swatchImage.raycastTarget = false;

                TextMeshProUGUI label = ImageExGalleryBuilder.CreateCenteredLabel(row.transform, names[i], 12f,
                    new Color(1f, 1f, 1f, 0.8f));
                RectTransform labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(20f, 0f);
                labelRect.offsetMax = Vector2.zero;
                label.alignment = TextAlignmentOptions.Left;

                labels[i] = label;
                swatches[i] = swatchImage;
            }

            GearChartLegend legend = legendGo.GetComponent<GearChartLegend>();
            legend.Configure(labels, swatches);
            legend.SetEntries(names, colors);
        }

        internal static void AddRadarCard(RectTransform content, string title, string description)
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

            int uiLayer = LayerMask.NameToLayer("UI");
            Transform clip = preview.transform.parent;

            GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            panelGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) panelGo.layer = uiLayer;

            RectTransform panel = (RectTransform)panelGo.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(12f, 12f);
            panel.offsetMax = new Vector2(-12f, -12f);

            ImageEx panelImage = panelGo.GetComponent<ImageEx>();
            panelImage.DrawShape = DrawShape.Rectangle;
            panelImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
            panelImage.color = new Color(0.07f, 0.08f, 0.12f, 0.92f);
            panelImage.raycastTarget = false;

            GameObject radarGo = new GameObject("Radar", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearRadarChart));
            radarGo.transform.SetParent(panel, false);
            if (uiLayer >= 0) radarGo.layer = uiLayer;
            RectTransform radarRect = (RectTransform)radarGo.transform;
            radarRect.anchorMin = new Vector2(0.18f, 0.08f);
            radarRect.anchorMax = new Vector2(0.82f, 0.92f);
            radarRect.offsetMin = Vector2.zero;
            radarRect.offsetMax = Vector2.zero;
            GearRadarChart radar = radarGo.GetComponent<GearRadarChart>();
            radar.raycastTarget = false;
            radar.SetData(new[] { 0.8f, 0.55f, 0.9f, 0.6f, 0.7f });

            TextMeshProUGUI label = ImageExGalleryBuilder.CreateCenteredLabel(
                panel, "RADAR · 5 AXES", 10f, new Color(1f, 1f, 1f, 0.45f));
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, -8f);
            label.alignment = TextAlignmentOptions.TopRight;
        }

        private static void CreateChart<T>(RectTransform parent, string name, Vector2 min, Vector2 max,
            float[] data, Color color) where T : MaskableGraphic
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            if (LayerMask.NameToLayer("UI") is int ui && ui >= 0) go.layer = ui;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            T chart = go.GetComponent<T>();
            chart.raycastTarget = false;

            if (chart is GearLineChart line)
                line.SetData(data);
            else if (chart is GearBarChart bar)
            {
                bar.SetData(data);
                bar.SetColors(color);
            }
        }

        private static void AddLabel(RectTransform parent, string text, Vector2 min, Vector2 max)
        {
            TMPro.TextMeshProUGUI label = ImageExGalleryBuilder.CreateCenteredLabel(parent, text, 10f, new Color(1f, 1f, 1f, 0.45f));
            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label.alignment = TMPro.TextAlignmentOptions.TopLeft;
        }
    }
}
