using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities;
using Gear.Utilities.UI.Charts;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ListView;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>联动演示卡：饼图扇区点击 → ListView 按分类过滤。</summary>
    internal static class GearChartListLinkDemoBuilder
    {
        internal static void AddLinkedCard(RectTransform content, string title, string description)
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
            panel.offsetMin = new Vector2(10f, 10f);
            panel.offsetMax = new Vector2(-10f, -10f);

            ImageEx panelImage = panelGo.GetComponent<ImageEx>();
            panelImage.DrawShape = DrawShape.Rectangle;
            panelImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
            panelImage.color = new Color(0.07f, 0.08f, 0.12f, 0.94f);
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
            pieRect.anchorMax = new Vector2(0.42f, 1f);
            pieRect.offsetMin = Vector2.zero;
            pieRect.offsetMax = Vector2.zero;
            GearPieChart pie = pieGo.GetComponent<GearPieChart>();
            pie.raycastTarget = true;
            pie.SetData(new[] { 2f, 2f, 2f }, colors);

            // 三个等分扇区：在扇区中心放百分比标签（锚点跟随饼图尺寸，不需要每帧算位置）
            float[] labelAngles = { 30f, -90f, 150f };
            for (int i = 0; i < labelAngles.Length; i++)
            {
                float radians = labelAngles[i] * Mathf.Deg2Rad;
                GameObject labelGo = new GameObject("Slice Label " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(pieGo.transform, false);
                RectTransform labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = new Vector2(0.5f + Mathf.Cos(radians) * 0.34f, 0.5f + Mathf.Sin(radians) * 0.34f);
                labelRect.anchorMax = labelRect.anchorMin;
                labelRect.sizeDelta = new Vector2(40f, 18f);

                TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
                label.text = "33%";
                label.fontSize = 11f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1f, 1f, 1f, 0.9f);
                label.raycastTarget = false;
            }

            // List 在创建阶段先 inactive，等 LoopScrollRect 字段齐了再激活
            GameObject listGo = new GameObject("LinkedList",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(GearListView));
            listGo.SetActive(false);
            listGo.transform.SetParent(panel, false);
            if (uiLayer >= 0) listGo.layer = uiLayer;

            RectTransform listRect = (RectTransform)listGo.transform;
            listRect.anchorMin = new Vector2(0.46f, 0.1f);
            listRect.anchorMax = new Vector2(0.98f, 0.95f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            Image hit = listGo.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(listGo.transform, false);
            if (uiLayer >= 0) viewportGo.layer = uiLayer;
            RectTransform viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            Image viewportHit = viewportGo.GetComponent<Image>();
            viewportHit.color = new Color(0f, 0f, 0f, 0f);
            viewportHit.raycastTarget = true;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            if (uiLayer >= 0) contentGo.layer = uiLayer;
            RectTransform listContent = (RectTransform)contentGo.transform;
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.sizeDelta = Vector2.zero;

            ScrollRect scroll = listGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = listContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24f;

            GameObject template = new GameObject("RowTemplate",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(HorizontalLayoutGroup), typeof(GearListRow));
            template.transform.SetParent(listGo.transform, false);
            if (uiLayer >= 0) template.layer = uiLayer;
            RectTransform templateRect = (RectTransform)template.transform;
            templateRect.sizeDelta = new Vector2(160f, 32f);
            template.SetActive(false);

            ImageEx rowBackground = template.GetComponent<ImageEx>();
            rowBackground.DrawShape = DrawShape.Rectangle;
            rowBackground.Rectangle = new RectangleImg { CornerRadius = new Vector4(8f, 8f, 8f, 8f) };
            rowBackground.color = new Color(1f, 1f, 1f, 0.07f);
            rowBackground.raycastTarget = true;

            HorizontalLayoutGroup rowLayout = template.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(8, 8, 3, 3);
            rowLayout.spacing = 4f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            TextMeshProUGUI category = CreateCell(template.transform, "Category", "A");
            TextMeshProUGUI value = CreateCell(template.transform, "Value", "0");
            GearListRow row = template.GetComponent<GearListRow>();
            row.Configure(new[] { category, value }, rowBackground);

            GameObject emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(TextMeshProUGUI));
            emptyGo.transform.SetParent(listGo.transform, false);
            if (uiLayer >= 0) emptyGo.layer = uiLayer;
            RectTransform emptyRect = (RectTransform)emptyGo.transform;
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = Vector2.zero;
            emptyRect.offsetMax = Vector2.zero;
            TextMeshProUGUI emptyText = emptyGo.GetComponent<TextMeshProUGUI>();
            emptyText.text = "No rows";
            emptyText.alignment = TextAlignmentOptions.Center;
            emptyText.color = new Color(1f, 1f, 1f, 0.45f);
            emptyText.raycastTarget = false;
            emptyGo.SetActive(false);

            LoopScrollRect loop = listGo.AddComponent<LoopScrollRect>();
            loop.scrollRect = scroll;
            loop.itemPrefab = templateRect;
            loop.direction = LoopScrollRect.Direction.Vertical;
            loop.spacing = 4f;
            loop.enablePullStart = false;
            loop.enablePullEnd = false;
            loop.autoCalculateExtraBuffer = true;

            GearListView list = listGo.GetComponent<GearListView>();
            list.Loop = loop;
            list.EmptyState = emptyGo;
            list.ColumnWidths = new[] { 78f, 60f };

            GearListRowData[] rows =
            {
                new GearListRowData("A", "10"),
                new GearListRowData("A", "12"),
                new GearListRowData("B", "20"),
                new GearListRowData("B", "22"),
                new GearListRowData("C", "30"),
                new GearListRowData("C", "33"),
            };

            GearChartListLink link = listGo.AddComponent<GearChartListLink>();
            listGo.SetActive(true);
            link.Bind(pie, list, rows, new[] { "A", "B", "C" }, 0);
        }

        private static TextMeshProUGUI CreateCell(Transform parent, string name, string text)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 11f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            return label;
        }
    }
}
