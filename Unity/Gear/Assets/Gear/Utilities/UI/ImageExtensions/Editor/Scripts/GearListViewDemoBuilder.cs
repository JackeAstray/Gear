using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ListView;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>ListView 演示卡：LoopScrollRect + 行模板 + CSV 数据。</summary>
    internal static class GearListViewDemoBuilder
    {
        internal static void AddListViewCard(RectTransform content, string title, string description)
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

            // 先建为 inactive，等字段全部就绪再激活，避免 LoopScrollRect.Awake 读到空引用
            GameObject listGo = new GameObject("ListView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(GearListView));
            listGo.SetActive(false);
            listGo.transform.SetParent(clip, false);
            if (uiLayer >= 0) listGo.layer = uiLayer;

            RectTransform listRect = (RectTransform)listGo.transform;
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.pivot = new Vector2(0.5f, 0.5f);
            listRect.sizeDelta = new Vector2(300f, 96f);

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
            templateRect.sizeDelta = new Vector2(300f, 36f);
            template.SetActive(false);

            ImageEx rowBackground = template.GetComponent<ImageEx>();
            rowBackground.DrawShape = DrawShape.Rectangle;
            rowBackground.Rectangle = new RectangleImg { CornerRadius = new Vector4(8f, 8f, 8f, 8f) };
            rowBackground.color = new Color(1f, 1f, 1f, 0.06f);
            rowBackground.raycastTarget = true;

            HorizontalLayoutGroup rowLayout = template.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(10, 10, 4, 4);
            rowLayout.spacing = 4f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            TextMeshProUGUI nameCell = CreateCell(template.transform, "Name", "Name");
            TextMeshProUGUI valueCell = CreateCell(template.transform, "Value", "Value");
            TextMeshProUGUI rankCell = CreateCell(template.transform, "Rank", "Rank");
            GearListRow row = template.GetComponent<GearListRow>();
            row.Configure(new[] { nameCell, valueCell, rankCell }, rowBackground);

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

            listGo.SetActive(true);
            list.SetData(CreateRows(12));
            list.ColumnWidths = new[] { 150f, 70f, 50f };
        }

        private static TextMeshProUGUI CreateCell(Transform parent, string name, string text)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 12f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            return label;
        }

        private static GearListRowData[] CreateRows(int count)
        {
            GearListRowData[] rows = new GearListRowData[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new GearListRowData(
                    "Item " + (i + 1),
                    (1000 + i * 37).ToString(),
                    "#" + (i + 1));
            }
            return rows;
        }
    }
}
