using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gear.Utilities.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>搜索 / 标签作用域 / 墨团遮罩卡片。</summary>
    internal static partial class ImageExGalleryCatalogExtra
    {
        /// <summary>搜索索引：输入即过滤行（Play 模式）。</summary>
        private static void AddSearchCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "控件 · Search", "输入即过滤，预分词索引（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            ImageEx field = CreateTile(preview.transform, "Search Field", new Vector2(0f, 64f),
                new Vector2(230f, 40f), new Color(0.11f, 0.12f, 0.17f, 1f));
            field.Rectangle.CornerRadius = new Vector4(10f, 10f, 10f, 10f);
            field.OutlineWidth = 1f;
            field.OutlineColor = new Color(1f, 1f, 1f, 0.14f);
            field.raycastTarget = true;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 14f;
            text.alignment = TextAlignmentOptions.Left;
            text.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            text.raycastTarget = false;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);

            GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(field.transform, false);
            TextMeshProUGUI placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholder.text = "搜索…";
            placeholder.fontSize = 14f;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.color = new Color(0.62f, 0.65f, 0.73f, 0.6f);
            placeholder.raycastTarget = false;
            RectTransform placeholderRect = (RectTransform)placeholderGo.transform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 0f);
            placeholderRect.offsetMax = new Vector2(-12f, 0f);

            TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = (RectTransform)field.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.pointSize = 14f;

            string[] labels = { "攻击强化", "防御矩阵", "暴击核心" };
            var rowObjects = new List<GameObject>();
            float[] ys = { 24f, -6f, -36f };
            for (int i = 0; i < labels.Length; i++)
            {
                ImageEx row = CreateTile(preview.transform, "Result " + i, new Vector2(0f, ys[i]),
                    new Vector2(220f, 26f), new Color(0.16f, 0.18f, 0.25f, 1f));
                row.Rectangle.CornerRadius = new Vector4(7f, 7f, 7f, 7f);

                GameObject rowLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rowLabelGo.transform.SetParent(row.transform, false);
                TextMeshProUGUI rowLabel = rowLabelGo.GetComponent<TextMeshProUGUI>();
                rowLabel.text = labels[i];
                rowLabel.fontSize = 13f;
                rowLabel.alignment = TextAlignmentOptions.Left;
                rowLabel.color = new Color(0.94f, 0.95f, 0.98f, 1f);
                rowLabel.raycastTarget = false;
                RectTransform rowLabelRect = (RectTransform)rowLabelGo.transform;
                rowLabelRect.anchorMin = Vector2.zero;
                rowLabelRect.anchorMax = Vector2.one;
                rowLabelRect.offsetMin = new Vector2(10f, 0f);
                rowLabelRect.offsetMax = Vector2.zero;

                rowObjects.Add(row.gameObject);
            }

            GearSearchIndex search = preview.gameObject.AddComponent<GearSearchIndex>();
            search.Build(labels);
            search.InputField = input;
            search.SetRows(rowObjects);
            search.Filter();
        }

        /// <summary>文本标签作用域：同一句里混合 wave / rainbow / shake。</summary>
        private static void AddTextTagCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "文本 · Tag Scope", "同一句里混合 wave / rainbow 标签（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            GameObject textGo = new GameObject("Tagged Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(preview.transform, false);
            TextMeshProUGUI tagged = textGo.GetComponent<TextMeshProUGUI>();
            tagged.fontSize = 20f;
            tagged.alignment = TextAlignmentOptions.Center;
            tagged.color = new Color(0.94f, 0.95f, 0.98f, 1f);
            tagged.raycastTarget = false;
            RectTransform rect = (RectTransform)textGo.transform;
            rect.sizeDelta = new Vector2(230f, 90f);
            rect.anchoredPosition = Vector2.zero;

            GearTextTagScope scope = textGo.AddComponent<GearTextTagScope>();
            scope.Refresh();
            tagged.text = "普通 <wave>波浪</wave>\n<rainbow>彩虹</rainbow> <shake>抖动</shake>";
        }

        /// <summary>墨团 RT 遮罩：覆盖度烘焙成 RT，用模板缓冲遮住子树。</summary>
        private static void AddInkMaskCard(RectTransform content)
        {
            ImageEx preview = AddCardIfMissing(content, "效果 · InkMask", "墨团 RT 遮罩整棵子树（Play 模式）", p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null) return;

            GameObject containerGo = new GameObject("Ink Container",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(Mask));
            containerGo.transform.SetParent(preview.transform, false);
            RectTransform container = (RectTransform)containerGo.transform;
            container.sizeDelta = new Vector2(210f, 150f);
            container.anchoredPosition = Vector2.zero;

            RawImage raw = containerGo.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = Color.white;

            Mask mask = containerGo.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 1f),
                new Color(0.14f, 0.75f, 0.95f, 1f),
                new Color(0.25f, 0.78f, 0.52f, 1f),
            };
            float[] xs = { -66f, 0f, 66f };
            for (int i = 0; i < colors.Length; i++)
            {
                ImageEx tile = CreateTile(container, "Ink Tile " + i, new Vector2(xs[i], 0f),
                    new Vector2(52f, 96f), colors[i]);
                tile.Rectangle.CornerRadius = new Vector4(12f, 12f, 12f, 12f);

                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(tile.transform, false);
                TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
                label.text = "墨";
                label.fontSize = 22f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1f, 1f, 1f, 0.9f);
                label.raycastTarget = false;
                ((RectTransform)labelGo.transform).sizeDelta = new Vector2(52f, 40f);
            }

            GearInkMask ink = containerGo.AddComponent<GearInkMask>();
            ink.SourceShader = Shader.Find("Gear/UI/InkCoverage");
            ink.Origin = new Vector2(0.15f, 0.5f);
            ink.Softness = 0.1f;
            ink.Roughness = 0.45f;
            ink.Progress = 0.55f;
        }
    }
}
