using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 灵活网格：固定列或按 MinCellSize 响应式列数，支持宽高比与 Masonry 瀑布流。
    /// 子项可通过 LayoutElement.preferredHeight 指定瀑布流中的高度。
    /// </summary>
    [AddComponentMenu("UI/Gear/Flexible Grid Layout")]
    public sealed class GearFlexibleGridLayout : LayoutGroup
    {
        public enum FitType
        {
            FixedColumns = 0,
            Responsive = 1,
        }

        [SerializeField] private FitType fitType = FitType.FixedColumns;
        [SerializeField, Min(1)] private int columns = 3;
        [SerializeField, Min(1f)] private float minCellSize = 96f;
        [SerializeField, Min(0.05f)] private float aspectRatio = 1f;
        [SerializeField] private Vector2 spacing = new Vector2(12f, 12f);
        [SerializeField] private bool masonry;

        public FitType Fit
        {
            get => fitType;
            set { fitType = value; SetDirty(); }
        }

        public int Columns
        {
            get => columns;
            set { columns = Mathf.Max(1, value); SetDirty(); }
        }

        public float MinCellSize
        {
            get => minCellSize;
            set { minCellSize = Mathf.Max(1f, value); SetDirty(); }
        }

        /// <summary>单元格宽 / 高；1 为正方形。</summary>
        public float AspectRatio
        {
            get => aspectRatio;
            set { aspectRatio = Mathf.Max(0.05f, value); SetDirty(); }
        }

        public Vector2 Spacing
        {
            get => spacing;
            set { spacing = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y)); SetDirty(); }
        }

        public bool Masonry
        {
            get => masonry;
            set { masonry = value; SetDirty(); }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            float width = rectTransform.rect.width - padding.horizontal;
            int resolved = ResolveColumns(width);
            float cellWidth = ResolveCellWidth(width, resolved);
            SetLayoutInputForAxis(padding.left + padding.right + cellWidth, -1f, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            int resolved = ResolveColumns(rectTransform.rect.width - padding.horizontal);
            float cellWidth = ResolveCellWidth(rectTransform.rect.width - padding.horizontal, resolved);
            float cellHeight = cellWidth / aspectRatio;
            int rows = masonry ? 1 : Mathf.CeilToInt(rectChildren.Count / (float)Mathf.Max(1, resolved));
            float height = padding.vertical + rows * cellHeight + Mathf.Max(0, rows - 1) * spacing.y;
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            Layout();
        }

        public override void SetLayoutVertical()
        {
            Layout();
        }

        private void Layout()
        {
            float available = rectTransform.rect.width - padding.horizontal;
            int resolved = ResolveColumns(available);
            float cellWidth = ResolveCellWidth(available, resolved);
            if (cellWidth <= 0f || resolved <= 0) return;

            float cellHeight = cellWidth / aspectRatio;
            float startX = padding.left;
            float startY = -padding.top;

            if (masonry)
            {
                float[] heights = new float[resolved];
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    int column = 0;
                    for (int c = 1; c < resolved; c++)
                    {
                        if (heights[c] < heights[column]) column = c;
                    }

                    RectTransform child = rectChildren[i];
                    float height = ResolveItemHeight(child, cellHeight);
                    float x = startX + column * (cellWidth + spacing.x);
                    float y = startY - heights[column];
                    Place(child, x, y, cellWidth, height);
                    heights[column] += height + spacing.y;
                }
            }
            else
            {
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    int row = i / resolved;
                    int column = i % resolved;
                    float x = startX + column * (cellWidth + spacing.x);
                    float y = startY - row * (cellHeight + spacing.y);
                    Place(rectChildren[i], x, y, cellWidth, cellHeight);
                }
            }
        }

        private static void Place(RectTransform child, float x, float y, float width, float height)
        {
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);
            child.sizeDelta = new Vector2(width, height);
            child.anchoredPosition = new Vector2(x, y);
        }

        private int ResolveColumns(float available)
        {
            if (fitType == FitType.FixedColumns) return Mathf.Max(1, columns);
            return Mathf.Max(1, Mathf.FloorToInt((available + spacing.x) / (minCellSize + spacing.x)));
        }

        private float ResolveCellWidth(float available, int resolved)
        {
            float width = (available - spacing.x * Mathf.Max(0, resolved - 1)) / Mathf.Max(1, resolved);
            return Mathf.Max(0f, width);
        }

        private static float ResolveItemHeight(RectTransform child, float cellHeight)
        {
            LayoutElement element = child.GetComponent<LayoutElement>();
            return element != null && element.preferredHeight > 0f ? element.preferredHeight : cellHeight;
        }

        private void SetDirty()
        {
            if (!IsActive()) return;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            columns = Mathf.Max(1, columns);
            minCellSize = Mathf.Max(1f, minCellSize);
            aspectRatio = Mathf.Max(0.05f, aspectRatio);
            spacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
            SetDirty();
        }
#endif
    }
}
