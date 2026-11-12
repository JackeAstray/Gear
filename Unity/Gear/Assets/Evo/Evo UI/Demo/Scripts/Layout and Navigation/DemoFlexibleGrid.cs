using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Flexible Grid Layout tab: a photo gallery whose width follows a handle on its right edge; its height stays put
    /// and the photos scroll inside.
    /// Auto Fit picks the column count from Cell width; Fixed count keeps Columns and stretches the cells.
    /// Masonry lets each photo keep the height of its own aspect ratio.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFlexibleGrid : MonoBehaviour
    {
        [SerializeField] private FlexibleGridLayout grid;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text readout;
        [SerializeField] private AspectRatioFitter[] photoRatios;
        [SerializeField] private float fixedHeight = 150f;
        [SerializeField] private Vector2 widthRange = new(560f, 1320f);
        [SerializeField] private float minCardWidth = 110f;

        [Header("Options")]
        [SerializeField] private CanvasGroup cellOption;
        [SerializeField] private CanvasGroup columnsOption;
        [SerializeField] private CanvasGroup ratioOption;

        RectTransform content;
        bool keepRatio = true;
        int lastColumns = -1;
        float lastWidth = -1f;
        readonly char[] readoutBuffer = new char[64];

        void Awake()
        {
            DemoUtils.SetLocked(columnsOption, true, false);
            ApplyCellMode();
        }

        // The editor's sliders can report their value before Awake
        void CachePhotos()
        {
            content = (RectTransform)grid.transform;
        }

        void LateUpdate()
        {
            CachePhotos();

            // The column count only settles in the layout pass, so the readout and scroll height follow it
            float width = content.rect.width;
            if (grid.CalculatedColumns == lastColumns && Mathf.Approximately(width, lastWidth))
                return;

            lastColumns = grid.CalculatedColumns;
            lastWidth = width;
            if (!WriteReadout(width, lastColumns))
                readout.text = $"{width:0} px - {lastColumns} column{(lastColumns == 1 ? "" : "s")}";

            FitContent();
        }

        // The readout goes through a buffer, so dragging the handle doesn't make a string every frame
        bool WriteReadout(float width, int columns)
        {
            if (!width.TryFormat(readoutBuffer, out int length, "0"))
                return false;

            const string Pixels = " px - ";
            string unit = columns == 1 ? " column" : " columns";
            if (length + Pixels.Length > readoutBuffer.Length || !columns.TryFormat(readoutBuffer.AsSpan(length + Pixels.Length), out int written)
                || length + Pixels.Length + written + unit.Length > readoutBuffer.Length)
                return false;

            Pixels.CopyTo(0, readoutBuffer, length, Pixels.Length);
            length += Pixels.Length + written;
            unit.CopyTo(0, readoutBuffer, length, unit.Length);
            readout.SetCharArray(readoutBuffer, 0, length + unit.Length);
            return true;
        }

        // The grid's height is where its lowest photo ends; the content takes it for scrolling
        void FitContent()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float bottom = 0f;
            for (int i = 0; i < content.childCount; i++)
            {
                var photo = (RectTransform)content.GetChild(i);
                if (photo.gameObject.activeSelf)
                    bottom = Mathf.Min(bottom, photo.localPosition.y + photo.rect.yMin);
            }

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -bottom);
        }

        public void SetFit(int index)
        {
            grid.SetFitType(index == 0 ? FlexibleGridLayout.FitType.AutoFit : FlexibleGridLayout.FitType.Width);
            DemoUtils.SetLocked(cellOption, index != 0);
            DemoUtils.SetLocked(columnsOption, index == 0);
            SetWidth(Width);
            lastColumns = -1;
        }

        public void SetCellWidth(float value)
        {
            grid.MinCellSize = new Vector2(value, grid.MinCellSize.y);
            ApplyCellMode();
        }

        public void SetColumns(float value)
        {
            grid.Columns = (int)value;
            SetWidth(Width);
            lastColumns = -1;
        }

        public void SetSpacing(float value)
        {
            grid.Spacing = new Vector2(value, value);
            SetWidth(Width);
            lastColumns = -1;
        }

        public void SetMasonry(bool value)
        {
            grid.Masonry = value;
            DemoUtils.SetLocked(ratioOption, value);
            ApplyCellMode();
        }

        public void SetKeepRatio(bool value)
        {
            keepRatio = value;
            ApplyCellMode();
        }

        // Masonry hands each photo its own height: its Aspect Ratio Fitter sizes it from the width the grid gives it.
        // In the even grid the grid sizes every photo, so the fitters stay off.
        void ApplyCellMode()
        {
            CachePhotos();

            bool masonry = grid.Masonry;
            grid.ControlChildHeight = !masonry;
            grid.PreserveAspectRatio = !masonry && keepRatio;
            grid.MinCellSize = new Vector2(grid.MinCellSize.x, masonry || keepRatio ? grid.MinCellSize.x / grid.AspectRatio : fixedHeight);

            foreach (AspectRatioFitter ratio in photoRatios)
                ratio.enabled = masonry;

            lastColumns = -1;
        }

        public float Width => panel.rect.width;
        public RectTransform Panel => panel;

        public void SetWidth(float width)
        {
            CachePhotos();

            // Fixed count stretches its cells, so the panel stops shrinking before they get too narrow to read
            float min = widthRange.x;
            if (grid.fitType != FlexibleGridLayout.FitType.AutoFit)
            {
                float chrome = panel.rect.width - content.rect.width;
                min = Mathf.Max(min, chrome + grid.Columns * minCardWidth + (grid.Columns - 1) * grid.Spacing.x);
            }

            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(width, min, widthRange.y));
        }
    }
}