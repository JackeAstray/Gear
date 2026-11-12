using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// A responsive grid layout that can fit either axis, use a fixed row or column count,
    /// preserve a cell aspect ratio, or arrange variable-height children in masonry columns.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Flexible Grid Layout"), HelpURL(Constants.HelpUrl + "layout/flexible-grid-layout")]
    public class FlexibleGridLayout : LayoutGroup
    {
        const float MinimumAspectRatio = 0.0001f;

        // Grid Settings
        [Tooltip("Determines how rows and columns are calculated.")]
        public FitType fitType = FitType.AutoFit;
        [SerializeField, Min(1)] private int rows = 2;
        [SerializeField, Min(1)] private int columns = 2;
        [Tooltip("Places variable-height children into independent columns. Vertical fitting is ignored in this mode.")]
        [SerializeField] private bool masonry = false;
      
        // Cell Sizing & Spacing
        [Tooltip("The horizontal and vertical gap between cells.")]
        [SerializeField] private Vector2 spacing = new(10f, 10f);
        [Tooltip("The natural cell size. Auto Fit uses the width to determine how many columns fit.")]
        [SerializeField] private Vector2 minCellSize = new(100f, 100f);

        // Child Control Size
        [Tooltip("Controls each child's width using the calculated cell width.")]
        [SerializeField] private bool controlChildWidth = true;
        [Tooltip("Controls each child's height using the calculated cell height.")]
        [SerializeField] private bool controlChildHeight = true;

        // Stretch Settings
        [Tooltip("Fits the grid cells to the available width. Some fit types enable this automatically.")]
        [SerializeField] private bool fitX = false;
        [Tooltip("Fits the grid cells to the available height. Some fit types enable this automatically.")]
        [SerializeField] private bool fitY = false;

        // Aspect Ratio
        [Tooltip("Keeps the calculated cell width and height at a fixed ratio.")]
        [SerializeField] private bool preserveAspectRatio = false;
        [Tooltip("Cell width divided by cell height.")]
        [SerializeField, Min(MinimumAspectRatio)] private float aspectRatio = 1f;

        // Enums
        public enum FitType
        {
            Uniform = 0,
            Width = 1,
            Height = 2,
            FixedRows = 3,
            FixedColumns = 4,
            AutoFit = 5
        }

        // Runtime Cache
        Vector2 cellSize, naturalCellSize, cachedRectSize;
        int cachedChildCount = -1;
        int calculatedRows, calculatedColumns;
        float requiredWidth, requiredHeight, preferredContentHeight;
        bool verticalFlow, effectiveFitX, effectiveFitY, geometryValid;
        float[] rowHeights = Array.Empty<float>();
        float[] rowPositions = Array.Empty<float>();
        float[] columnHeights = Array.Empty<float>();
        float[] columnPositions = Array.Empty<float>();

        protected override void OnEnable()
        {
            geometryValid = false;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            geometryValid = false;
            base.OnDisable();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            geometryValid = false;
            base.OnDidApplyAnimationProperties();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            geometryValid = false;
            base.OnRectTransformDimensionsChange();
        }

        protected override void OnTransformChildrenChanged()
        {
            geometryValid = false;
            base.OnTransformChildrenChanged();
        }

        protected override void OnChildRectTransformDimensionsChange()
        {
            geometryValid = false;
            base.OnChildRectTransformDimensionsChange();
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            RebuildGeometry();

            int minimumColumns = GetMinimumColumnCount();
            float minimumContentWidth = GetContentSize(naturalCellSize.x, spacing.x, minimumColumns);
            float preferredContentWidth = GetContentSize(naturalCellSize.x, spacing.x, calculatedColumns);

            SetLayoutInputForAxis(padding.horizontal + minimumContentWidth,
#if UNITY_6000_6_OR_NEWER
                float.PositiveInfinity, // No maximum size, matching the pre-6.6 layout behavior.
#endif
                padding.horizontal + preferredContentWidth, effectiveFitX ? 1f : -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            EnsureGeometry();

            float preferredHeight = padding.vertical + preferredContentHeight;
            SetLayoutInputForAxis(preferredHeight,
#if UNITY_6000_6_OR_NEWER
                float.PositiveInfinity,
#endif
                preferredHeight, effectiveFitY ? 1f : -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            EnsureGeometry();

            int childCount = rectChildren.Count;
            if (childCount == 0)
                return;

            float startOffset = GetStartOffset(0, requiredWidth);
            float step = cellSize.x + spacing.x;
            float alignment = GetAlignmentOnAxis(0);
            int lastRow = calculatedRows - 1;
            float lastRowOffset = 0f;

            if (!masonry && !verticalFlow)
            {
                int lastRowCount = childCount - lastRow * calculatedColumns;
                if (lastRowCount < calculatedColumns)
                {
                    float lastRowWidth = GetContentSize(cellSize.x, spacing.x, lastRowCount);
                    lastRowOffset = (requiredWidth - lastRowWidth) * alignment;
                }
            }

            for (int i = 0; i < childCount; i++)
            {
                GetCellPosition(i, out int row, out int column);

                RectTransform child = rectChildren[i];
                float position = startOffset + step * column;
                if (row == lastRow)
                    position += lastRowOffset;

                if (controlChildWidth) { SetChildAlongAxis(child, 0, position, cellSize.x); }
                else
                {
                    float childWidth = Mathf.Max(0f, child.rect.width);
                    SetChildAlongAxis(child, 0, position + (cellSize.x - childWidth) * alignment);
                }
            }
        }

        public override void SetLayoutVertical()
        {
            EnsureGeometry();

            int childCount = rectChildren.Count;
            if (childCount == 0)
                return;

            float alignment = GetAlignmentOnAxis(1);

            if (masonry)
            {
                ResetMasonryPositions();

                for (int i = 0; i < childCount; i++)
                {
                    int column = i % calculatedColumns;
                    RectTransform child = rectChildren[i];
                    float childHeight = GetChildHeight(child);
                    float position = columnPositions[column];

                    if (controlChildHeight)
                        SetChildAlongAxis(child, 1, position, cellSize.y);
                    else
                        SetChildAlongAxis(child, 1, position);

                    columnPositions[column] = position + childHeight + spacing.y;
                }

                return;
            }

            int lastColumn = calculatedColumns - 1;
            float lastColumnOffset = 0f;

            if (verticalFlow)
            {
                int lastColumnCount = childCount - lastColumn * calculatedRows;
                if (lastColumnCount < calculatedRows)
                {
                    float lastColumnHeight = spacing.y * (lastColumnCount - 1);
                    for (int row = 0; row < lastColumnCount; row++)
                        lastColumnHeight += rowHeights[row];

                    lastColumnOffset = (requiredHeight - Mathf.Max(0f, lastColumnHeight)) * alignment;
                }
            }

            for (int i = 0; i < childCount; i++)
            {
                GetCellPosition(i, out int row, out int column);

                RectTransform child = rectChildren[i];
                float position = rowPositions[row];
                if (column == lastColumn)
                    position += lastColumnOffset;

                if (controlChildHeight) { SetChildAlongAxis(child, 1, position, cellSize.y); }
                else
                {
                    float childHeight = Mathf.Max(0f, child.rect.height);
                    SetChildAlongAxis(child, 1, position + (rowHeights[row] - childHeight) * alignment);
                }
            }
        }

        void EnsureGeometry()
        {
            if (!geometryValid || cachedChildCount != rectChildren.Count || cachedRectSize != rectTransform.rect.size)
                RebuildGeometry();
        }

        void RebuildGeometry()
        {
            int childCount = rectChildren.Count;
            DetermineGridStructure(childCount);
            CalculateCellSize();

            requiredWidth = GetContentSize(cellSize.x, spacing.x, calculatedColumns);

            if (childCount == 0)
            {
                requiredHeight = 0f;
                preferredContentHeight = 0f;
            }
            else if (masonry)
            {
                BuildMasonryGeometry(childCount);
                preferredContentHeight = controlChildHeight
                    ? GetMasonryContentHeight(naturalCellSize.y, childCount)
                    : requiredHeight;
            }
            else
            {
                BuildRowGeometry(childCount);
                preferredContentHeight = UsesCalculatedRowHeight
                    ? GetContentSize(naturalCellSize.y, spacing.y, calculatedRows)
                    : requiredHeight;
            }

            cachedChildCount = childCount;
            cachedRectSize = rectTransform.rect.size;
            geometryValid = true;
        }

        void DetermineGridStructure(int childCount)
        {
            verticalFlow = false;
            calculatedRows = 0;
            calculatedColumns = 0;

            bool forceFitX = false;
            bool forceFitY = false;

            if (childCount > 0)
            {
                switch (fitType)
                {
                    case FitType.Uniform:
                        calculatedColumns = Mathf.CeilToInt(Mathf.Sqrt(childCount));
                        calculatedRows = DivideRoundingUp(childCount, calculatedColumns);
                        forceFitX = true;
                        forceFitY = true;
                        break;

                    case FitType.Width:
                        calculatedColumns = Mathf.Min(Mathf.Max(1, columns), childCount);
                        calculatedRows = DivideRoundingUp(childCount, calculatedColumns);
                        forceFitX = true;
                        break;

                    case FitType.Height:
                        calculatedRows = Mathf.Min(Mathf.Max(1, rows), childCount);
                        calculatedColumns = DivideRoundingUp(childCount, calculatedRows);
                        verticalFlow = true;
                        forceFitY = true;
                        break;

                    case FitType.FixedRows:
                        calculatedRows = Mathf.Min(Mathf.Max(1, rows), childCount);
                        calculatedColumns = DivideRoundingUp(childCount, calculatedRows);
                        verticalFlow = true;
                        break;

                    case FitType.FixedColumns:
                        calculatedColumns = Mathf.Min(Mathf.Max(1, columns), childCount);
                        calculatedRows = DivideRoundingUp(childCount, calculatedColumns);
                        break;

                    case FitType.AutoFit:
                    default:
                        calculatedColumns = GetAutoFitColumnCount(childCount);
                        calculatedRows = DivideRoundingUp(childCount, calculatedColumns);
                        forceFitX = true;
                        break;
                }
            }

            effectiveFitX = fitX || forceFitX;
            effectiveFitY = !masonry && (fitY || forceFitY);
        }

        void CalculateCellSize()
        {
            float ratio = GetSafeAspectRatio(aspectRatio);
            naturalCellSize = GetNaturalCellSize(ratio);

            if (calculatedRows == 0 || calculatedColumns == 0)
            {
                cellSize = naturalCellSize;
                return;
            }

            float widthForCells = rectTransform.rect.width - padding.horizontal - spacing.x * (calculatedColumns - 1);
            float heightForCells = rectTransform.rect.height - padding.vertical - spacing.y * (calculatedRows - 1);
            float fittedWidth = Mathf.Max(0f, widthForCells / calculatedColumns);
            float fittedHeight = Mathf.Max(0f, heightForCells / calculatedRows);

            if (!preserveAspectRatio)
            {
                cellSize.x = effectiveFitX ? fittedWidth : naturalCellSize.x;
                cellSize.y = effectiveFitY ? fittedHeight : naturalCellSize.y;
                return;
            }

            if (effectiveFitX && effectiveFitY)
            {
                cellSize.x = Mathf.Min(fittedWidth, fittedHeight * ratio);
                cellSize.y = cellSize.x / ratio;
            }
            else if (effectiveFitX)
            {
                cellSize.x = fittedWidth;
                cellSize.y = fittedWidth / ratio;
            }
            else if (effectiveFitY)
            {
                cellSize.y = fittedHeight;
                cellSize.x = fittedHeight * ratio;
            }
            else
            {
                cellSize = naturalCellSize;
            }
        }

        void BuildRowGeometry(int childCount)
        {
            EnsureBufferCapacity(ref rowHeights, calculatedRows);
            EnsureBufferCapacity(ref rowPositions, calculatedRows);
            Array.Clear(rowHeights, 0, calculatedRows);

            if (UsesCalculatedRowHeight)
            {
                for (int row = 0; row < calculatedRows; row++)
                    rowHeights[row] = cellSize.y;
            }
            else
            {
                for (int i = 0; i < childCount; i++)
                {
                    GetCellPosition(i, out int row, out _);
                    rowHeights[row] = Mathf.Max(rowHeights[row], GetChildHeight(rectChildren[i]));
                }
            }

            requiredHeight = 0f;
            for (int row = 0; row < calculatedRows; row++)
                requiredHeight += rowHeights[row];

            requiredHeight = Mathf.Max(0f, requiredHeight + spacing.y * (calculatedRows - 1));

            float position = GetStartOffset(1, requiredHeight);
            for (int row = 0; row < calculatedRows; row++)
            {
                rowPositions[row] = position;
                position += rowHeights[row] + spacing.y;
            }
        }

        void BuildMasonryGeometry(int childCount)
        {
            EnsureBufferCapacity(ref columnHeights, calculatedColumns);
            EnsureBufferCapacity(ref columnPositions, calculatedColumns);
            Array.Clear(columnHeights, 0, calculatedColumns);

            for (int i = 0; i < childCount; i++)
            {
                int column = i % calculatedColumns;
                int itemRow = i / calculatedColumns;

                if (itemRow > 0)
                    columnHeights[column] += spacing.y;

                columnHeights[column] += GetChildHeight(rectChildren[i]);
            }

            requiredHeight = 0f;
            for (int column = 0; column < calculatedColumns; column++)
                requiredHeight = Mathf.Max(requiredHeight, columnHeights[column]);

            requiredHeight = Mathf.Max(0f, requiredHeight);
            ResetMasonryPositions();
        }

        void ResetMasonryPositions()
        {
            if (calculatedColumns == 0)
                return;

            float startOffset = GetStartOffset(1, requiredHeight);

            for (int column = 0; column < calculatedColumns; column++)
                columnPositions[column] = startOffset;
        }

        void GetCellPosition(int childIndex, out int row, out int column)
        {
            if (masonry || !verticalFlow)
            {
                row = childIndex / calculatedColumns;
                column = childIndex % calculatedColumns;
            }
            else
            {
                row = childIndex % calculatedRows;
                column = childIndex / calculatedRows;
            }
        }

        void InvalidateLayout()
        {
            geometryValid = false;
            SetDirty();
        }

        void EnsureBufferCapacity(ref float[] buffer, int requiredCapacity)
        {
            if (buffer.Length >= requiredCapacity)
                return;

            int capacity = Mathf.Max(4, Mathf.NextPowerOfTwo(requiredCapacity));
            buffer = new float[capacity];
        }

        bool UsesCalculatedRowHeight => controlChildHeight || (!masonry && (effectiveFitY || preserveAspectRatio));

        int DivideRoundingUp(int value, int divisor) => (value + divisor - 1) / divisor;

        int GetAutoFitColumnCount(int childCount)
        {
            float availableWidth = Mathf.Max(0f, rectTransform.rect.width - padding.horizontal);
            float targetCellWidth = GetNaturalCellSize(GetSafeAspectRatio(aspectRatio)).x;
            float step = targetCellWidth + spacing.x;

            if (step <= Mathf.Epsilon)
                return childCount;

            int result = Mathf.FloorToInt((availableWidth + spacing.x) / step);
            return Mathf.Clamp(result, 1, childCount);
        }

        int GetMinimumColumnCount()
        {
            if (cachedChildCount == 0)
                return 0;

            return fitType == FitType.Width || fitType == FitType.FixedColumns ? calculatedColumns : 1;
        }

        float GetChildHeight(RectTransform child) => UsesCalculatedRowHeight ? cellSize.y : Mathf.Max(0f, child.rect.height);

        float GetMasonryContentHeight(float itemHeight, int childCount)
        {
            if (childCount == 0 || calculatedColumns == 0)
                return 0f;

            int largestColumnCount = DivideRoundingUp(childCount, calculatedColumns);
            return GetContentSize(itemHeight, spacing.y, largestColumnCount);
        }

        float GetContentSize(float itemSize, float gap, int count)
        {
            if (count <= 0)
                return 0f;

            return Mathf.Max(0f, itemSize * count + gap * (count - 1));
        }

        float GetFinite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        float GetNonNegativeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }

        float GetSafeAspectRatio(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(MinimumAspectRatio, value);
        }

        Vector2 GetNaturalCellSize(float ratio)
        {
            float width = GetNonNegativeFinite(minCellSize.x);
            float height = GetNonNegativeFinite(minCellSize.y);

            if (preserveAspectRatio)
            {
                width = Mathf.Max(width, height * ratio);
                height = width / ratio;
            }

            return new Vector2(width, height);
        }

        #region Public API
        public Vector2 CellSize => cellSize;
        public int CalculatedRows => calculatedRows;
        public int CalculatedColumns => calculatedColumns;

        public void SetFitType(FitType type)
        {
            if (fitType == type)
                return;

            fitType = type;
            InvalidateLayout();
        }

        public int Rows
        {
            get => rows;
            set
            {
                int clampedValue = Mathf.Max(1, value);
                if (rows == clampedValue)
                    return;

                rows = clampedValue;
                InvalidateLayout();
            }
        }

        public int Columns
        {
            get => columns;
            set
            {
                int clampedValue = Mathf.Max(1, value);
                if (columns == clampedValue)
                    return;

                columns = clampedValue;
                InvalidateLayout();
            }
        }

        public Vector2 MinCellSize
        {
            get => minCellSize;
            set
            {
                Vector2 sanitizedValue = new(GetNonNegativeFinite(value.x), GetNonNegativeFinite(value.y));
                if (minCellSize == sanitizedValue)
                    return;

                minCellSize = sanitizedValue;
                InvalidateLayout();
            }
        }

        public Vector2 Spacing
        {
            get => spacing;
            set
            {
                Vector2 sanitizedValue = new(GetFinite(value.x), GetFinite(value.y));
                if (spacing == sanitizedValue)
                    return;

                spacing = sanitizedValue;
                InvalidateLayout();
            }
        }

        public bool ControlChildWidth
        {
            get => controlChildWidth;
            set
            {
                if (controlChildWidth == value)
                    return;

                controlChildWidth = value;
                InvalidateLayout();
            }
        }

        public bool ControlChildHeight
        {
            get => controlChildHeight;
            set
            {
                if (controlChildHeight == value)
                    return;

                controlChildHeight = value;
                InvalidateLayout();
            }
        }

        public bool FitX
        {
            get => fitX;
            set
            {
                if (fitX == value)
                    return;

                fitX = value;
                InvalidateLayout();
            }
        }

        public bool FitY
        {
            get => fitY;
            set
            {
                if (fitY == value)
                    return;

                fitY = value;
                InvalidateLayout();
            }
        }

        public bool PreserveAspectRatio
        {
            get => preserveAspectRatio;
            set
            {
                if (preserveAspectRatio == value)
                    return;

                preserveAspectRatio = value;
                InvalidateLayout();
            }
        }

        public float AspectRatio
        {
            get => aspectRatio;
            set
            {
                float sanitizedValue = GetSafeAspectRatio(value);
                if (Mathf.Approximately(aspectRatio, sanitizedValue))
                    return;

                aspectRatio = sanitizedValue;
                InvalidateLayout();
            }
        }

        public bool Masonry
        {
            get => masonry;
            set
            {
                if (masonry == value)
                    return;

                masonry = value;
                InvalidateLayout();
            }
        }
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);
            minCellSize = new Vector2(GetNonNegativeFinite(minCellSize.x), GetNonNegativeFinite(minCellSize.y));
            spacing = new Vector2(GetFinite(spacing.x), GetFinite(spacing.y));
            aspectRatio = GetSafeAspectRatio(aspectRatio);
            geometryValid = false;
            base.OnValidate();
        }
#endif
    }
}