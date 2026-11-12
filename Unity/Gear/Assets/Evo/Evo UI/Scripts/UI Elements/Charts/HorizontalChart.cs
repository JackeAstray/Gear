using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Renders a horizontal bar chart from ordered labeled values.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Horizontal Chart"), HelpURL(Constants.HelpUrl + "ui-elements/horizontal-chart")]
    public class HorizontalChart : ChartBase
    {
        // Chart Data
        [Tooltip("Ordered chart data. Null entries render as zero. Call MarkDirty after direct runtime edits.")]
        public List<DataPoint> dataPoints = new()
        {
            new("Product A", 75f),
            new("Product B", 45f),
            new("Product C", 90f),
            new("Product D", 60f)
        };

        // Scale
        [Range(1, 50), Tooltip("Number of intervals on the numeric axis. Scale labels and grid lines use the same divisions.")]
        [UnityEngine.Serialization.FormerlySerializedAs("verticalGridLines")]
        public int scaleDivisions = 5;
        [Tooltip("Scale positive bars to the largest value. Disable to compare charts against a fixed maximum.")]
        public bool autoScale = true;
        [Min(0.001f), Tooltip("Value represented by the outermost grid line. Bar charts use this only when Auto " +
                              "Scale is disabled. Values above the maximum are clipped.")]
        public float maximumValue = 100f;

        // Bars
        [SerializeField, Min(0f), Tooltip("Inset between the bars and the plot edges in local UI units. Clamped to fit the plot.")]
        private float gridToBarsOffset = 5f;
        [SerializeField, Min(0f), Tooltip("Requested gap between adjacent bars in local UI units. Reduced when needed to fit the data.")]
        private float barSpacing = 10f;
        [SerializeField, Min(0f), Tooltip("Uniform bar corner radius, in pixels or percent according to Bar Shape radius mode.")]
        private float barCornerRadius = 4f;
        [Tooltip("Bar corner, edge softness and outline settings.")]
        public ShapeSettings barShape = new();
        [Tooltip("Use each data point's color instead of the shared bar color.")]
        public bool useDataColors;

        // Label Layout
        [Tooltip("Bottom-axis labels turn when crowded. Text is shortened only when it still cannot fit.")]
        public LabelRotation labelRotation = LabelRotation.Auto;
        [SerializeField, Min(0f), Tooltip("Gap between category labels and the plot.")]
        private float labelPadding = 20f;
        [SerializeField, Min(0f), Tooltip("Gap between data values and their bars or points.")]
        private float valuePadding = 10f;
        [SerializeField, Min(0f), Tooltip("Gap between numeric scale labels and the plot.")]
        private float scalePadding = 10f;

        // Visibility
        [Tooltip("Display the category name beside each bar or below each line point.")]
        public bool showCategoryLabels = true;
        [Tooltip("Display numeric labels along the value axis.")]
        public bool showScaleLabels = true;
        [SerializeField, Tooltip("Display numeric data values beside bars or points.")]
        private bool showValues = true;
        [SerializeField, Tooltip("Display grid lines to help compare values.")]
        private bool showGrid = true;
        [SerializeField, Tooltip("Display the chart axes.")]
        private bool showAxis = true;

        // Grid and Axes
        [SerializeField, Range(0.1f, 20f),
         Tooltip("Grid line width in local UI units. Limited by the available plot space on Cartesian charts.")]
        private float gridLineThickness = 1f;
        [SerializeField, Range(0.1f, 20f), Tooltip("Axis line width in local UI units.")]
        private float axisThickness = 2f;

        // Typography
        [SerializeField, Min(0f), Tooltip("Category or slice label font size. Cartesian labels may shrink to fit short rows.")]
        private float labelFontSize = 12f;
        [SerializeField, Min(0f), Tooltip("Data value font size. Values may shrink to fit the available plot space.")]
        private float valueFontSize = 12f;
        [SerializeField, Min(0f), Tooltip("Numeric axis font size. Scale labels may shrink to fit the available plot space.")]
        private float scaleFontSize = 12f;

        // Styling
        [Tooltip("Color mapping for the grid lines.")]
        public ColorMapping gridColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for the axes.")]
        public ColorMapping axisColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for category or slice labels.")]
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for numeric axis labels.")]
        public ColorMapping scaleColor = new() { stylerID = "Primary" };
        [Tooltip("Font mapping for category or slice labels. Uses the text provider's default font when no font resolves.")]
        public FontMapping labelFont = new() { stylerID = "Regular" };
        [Tooltip("Font mapping for data value labels. Uses the text provider's default font when no font resolves.")]
        public FontMapping valueFont = new() { stylerID = "Regular" };
        [Tooltip("Font mapping for numeric axis labels. Uses the text provider's default font when no font resolves.")]
        public FontMapping scaleFont = new() { stylerID = "Regular" };
        [Tooltip("Shared bar color mapping used when Use Data Colors is disabled.")]
        public ColorMapping barColor = new() { stylerID = "Accent" };

        // Pointer State
        readonly List<Rect> bars = new();
        Rect barPlot;

        // Properties
        int ScaleDivisionCount => Mathf.Clamp(scaleDivisions, 1, 50);
        int DataCount => dataPoints?.Count ?? 0;
        string DataLabel(int index) => dataPoints[index]?.label;
        float DataValue(int index) => dataPoints[index]?.value ?? 0;
        Color DataColor(int index) => dataPoints[index]?.color ?? Color.white;

        #region Styler Metadata
        /// <summary>
        /// Return serialized color mapping field names for Styler integration.
        /// </summary>
        public static string[] GetColorFields() => new[] { "gridColor", "axisColor", "barColor", "labelColor", "valueColor", "scaleColor" };

        /// <summary>
        /// Return serialized font mapping field names for Styler integration.
        /// </summary>
        public static string[] GetFontFields() => new[] { "valueFont", "labelFont", "scaleFont" };
        #endregion

        #region State Collection
        protected override void CollectState(ref ChartState state)
        {
            state.Add((int)labelRotation);
            state.Add(labelPadding);
            state.Add(valuePadding);
            state.Add(scalePadding);
            state.Add(showCategoryLabels);
            state.Add(showScaleLabels);
            state.Add(showValues);
            state.Add(showGrid);
            state.Add(showAxis);
            state.Add(gridLineThickness);
            state.Add(axisThickness);
            state.Add(labelFontSize);
            state.Add(valueFontSize);
            state.Add(scaleFontSize);
            state.Add(scaleDivisions);
            state.Add(Styler.GetColor(styleProvider, gridColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, axisColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, labelColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, scaleColor, stylerPreset));
            state.Add(GetFont(labelFont));
            state.Add(GetFont(valueFont));
            state.Add(GetFont(scaleFont));
            state.Add(DataCount);

            for (int i = 0; i < DataCount; i++)
            {
                state.Add(DataLabel(i));
                state.Add(DataValue(i));
            }

            state.Add(gridToBarsOffset);
            state.Add(barSpacing);
            state.Add(autoScale);
            state.Add(maximumValue);
            state.Add(barCornerRadius);
            state.Add(barShape);
            state.Add(useDataColors);
            state.Add(Styler.GetColor(styleProvider, barColor, stylerPreset));

            for (int i = 0; i < DataCount; i++)
                state.Add(DataColor(i));
        }
        #endregion

        #region Data Model
        /// <summary>
        /// A labeled value with a color for an individual horizontal bar.
        /// </summary>
        [System.Serializable]
        public new class DataPoint : ColoredDataPoint
        {
            public DataPoint(string label = "", float value = 0) : base(label, value) { }
        }
        #endregion

        #region Label Preparation
        void PrepareCartesianLabels(GameObject parent, double minimum, double maximum, bool horizontal, string categoryPrefix, string scalePrefix)
        {
            var font = GetFont(labelFont);
            var numberFont = GetFont(valueFont);
            var scaleTextFont = GetFont(scaleFont);

            Color textColor = Styler.GetColor(styleProvider, labelColor, stylerPreset);
            Color numberColor = Styler.GetColor(styleProvider, valueColor, stylerPreset);
            Color scaleTextColor = Styler.GetColor(styleProvider, scaleColor, stylerPreset);

            for (int i = 0; i < DataCount; i++)
            {
                if (showCategoryLabels)
                    PrepareLabel(horizontal ? leftLabels : bottomLabels, parent, IndexedName(categoryPrefix, i), DataLabel(i), font, labelFontSize, textColor);

                if (showValues)
                    PrepareLabel(valueLabels, parent, IndexedName("Value ", i), FormatValue(DataValue(i)), numberFont, valueFontSize, numberColor);
            }

            for (int i = 0; showScaleLabels && i <= ScaleDivisionCount; i++)
            {
                PrepareLabel(horizontal ? bottomLabels : leftLabels, parent, IndexedName(scalePrefix, i),
                    FormatValue((float)(minimum + (double)i / ScaleDivisionCount * (maximum - minimum))),
                    scaleTextFont, scaleFontSize, scaleTextColor);
            }
        }
        #endregion

        #region Chart Rendering
        protected override void RefreshChart()
        {
            BeginLabels();
            bars.Clear();

            if (DataCount == 0 || !HasPlotSpace())
                return;

            float maximum = autoScale ? 0 : Positive(maximumValue);

            if (autoScale)
            {
                for (int i = 0; i < DataCount; i++)
                    maximum = Mathf.Max(maximum, Positive(DataValue(i)));
            }

            if (maximum <= 0)
                maximum = 1;

            GameObject grid = Container("Grid");
            GameObject data = Container("Data");
            GameObject labels = Container("Labels");

            PrepareCartesianLabels(labels, 0, maximum, true, "Label ", "Scale Label ");

            float edge = Mathf.Max(showAxis ? Positive(axisThickness) * 0.5f : 0, showGrid ? Positive(gridLineThickness) * 0.5f : 0);
            edge = Mathf.Max(edge, Positive(barShape?.softness ?? 0));
            PlotFrame frame = FitPlot(Vector2.one * edge, new Vector3(labelPadding, scalePadding, valuePadding),
                labelRotation, true, bottomStep: 1f / ScaleDivisionCount);

            if (!HasPlotSpace(frame))
                return;

            Rect plot = frame.plot;
            float offset = Mathf.Min(Positive(gridToBarsOffset), Mathf.Min(plot.width, plot.height) * 0.25f);
            float categoryLength = plot.height - offset * 2;
            float numericLength = plot.width - offset;
            float spacing = Mathf.Min(Positive(barSpacing), categoryLength / DataCount * 0.5f);
            float size = Mathf.Max(0, (categoryLength - spacing * (DataCount - 1)) / DataCount);
            float pitch = size + spacing;

            if (showGrid)
            {
                DrawScaleGrid(grid, frame, ScaleDivisionCount, gridLineThickness,
                    Styler.GetColor(styleProvider, gridColor, stylerPreset), true, offset);
            }

            if (showAxis)
                DrawCartesianAxes(grid, frame, axisThickness, Styler.GetColor(styleProvider, axisColor, stylerPreset));

            Color sharedColor = Styler.GetColor(styleProvider, barColor, stylerPreset);

            for (int i = 0; i < DataCount; i++)
            {
                float length = Mathf.Clamp01(Positive(DataValue(i)) / maximum) * numericLength;
                Vector2 position = plot.position + Vector2.one * offset + Vector2.up * (i * pitch);
                var bar = Shape(data.transform, IndexedName("Bar ", i), useDataColors ? DataColor(i) : sharedColor, barCornerRadius,
                    settings: barShape, softnessLimit: Mathf.Max(0, Mathf.Min(frame.edge.x, frame.edge.y) - 2));

                Layout(bar.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, position, new Vector2(length, size));

                // Hovered bars widen on both sides and grow to the right
                if (PointerEnabled)
                {
                    bars.Add(new Rect(position, new Vector2(length, size)));
                    SetPointVisual(i, bar, new Vector2(0, -1), new Vector2(1, 2));
                }

                if (showValues)
                    PlaceValue(valueLabels[i], frame, position + new Vector2(length, size * 0.5f), pitch, true);

                if (!showCategoryLabels)
                    continue;

                PlaceLeftLabel(leftLabels[i], frame, position.y + size * 0.5f, pitch);
            }

            for (int i = 0; showScaleLabels && i <= ScaleDivisionCount; i++)
            {
                float position = offset + i * numericLength / ScaleDivisionCount;
                PlaceBottomLabel(bottomLabels[i], frame, plot.x + position, numericLength / ScaleDivisionCount);
            }

            barPlot = plot;
        }
        #endregion

        #region Pointer Interaction
        protected override int PointCount => DataCount;

        protected override ChartBase.DataPoint PointAt(int index) => dataPoints[index];

        protected override int HitPoint(Vector2 point) => HitBar(bars, barPlot, point, true);
        #endregion

        #region Public API
        /// <summary>
        /// Number of intervals on the numeric axis, from 1 to 50.
        /// </summary>
        public int ScaleDivisions
        {
            get => scaleDivisions;
            set => SetValue(ref scaleDivisions, Mathf.Clamp(value, 1, 50));
        }

        /// <summary>
        /// Scale bars to the largest value. Disable to use Maximum Value.
        /// </summary>
        public bool AutoScale
        {
            get => autoScale;
            set => SetValue(ref autoScale, value);
        }

        /// <summary>
        /// Value of the outermost grid line while Auto Scale is disabled.
        /// </summary>
        public float MaximumValue
        {
            get => maximumValue;
            set => SetValue(ref maximumValue, Mathf.Max(0.001f, value));
        }

        /// <summary>
        /// Use each data point's color instead of the shared bar color.
        /// </summary>
        public bool UseDataColors
        {
            get => useDataColors;
            set => SetValue(ref useDataColors, value);
        }

        /// <summary>
        /// Inset between the bars and the plot edges in local UI units.
        /// </summary>
        public float PlotPadding
        {
            get => gridToBarsOffset;
            set => SetValue(ref gridToBarsOffset, Mathf.Max(0, value));
        }

        /// <summary>
        /// Gap between adjacent bars in local UI units. Reduced when needed to fit the data.
        /// </summary>
        public float BarSpacing
        {
            get => barSpacing;
            set => SetValue(ref barSpacing, Mathf.Max(0, value));
        }

        /// <summary>
        /// Uniform bar corner radius, in pixels or percent according to Bar Shape radius mode.
        /// </summary>
        public float BarCornerRadius
        {
            get => barCornerRadius;
            set => SetValue(ref barCornerRadius, Mathf.Max(0, value));
        }

        /// <summary>
        /// Display grid lines to help compare values.
        /// </summary>
        public bool ShowGrid
        {
            get => showGrid;
            set => SetValue(ref showGrid, value);
        }

        /// <summary>
        /// Grid line width in local UI units, from 0.1 to 20.
        /// </summary>
        public float GridLineThickness
        {
            get => gridLineThickness;
            set => SetValue(ref gridLineThickness, Mathf.Clamp(value, 0.1f, 20f));
        }

        /// <summary>
        /// Display the chart axes.
        /// </summary>
        public bool ShowAxis
        {
            get => showAxis;
            set => SetValue(ref showAxis, value);
        }

        /// <summary>
        /// Axis line width in local UI units, from 0.1 to 20.
        /// </summary>
        public float AxisThickness
        {
            get => axisThickness;
            set => SetValue(ref axisThickness, Mathf.Clamp(value, 0.1f, 20f));
        }

        /// <summary>
        /// Display the category name beside each bar or below each line point.
        /// </summary>
        public bool ShowCategoryLabels
        {
            get => showCategoryLabels;
            set => SetValue(ref showCategoryLabels, value);
        }

        /// <summary>
        /// Category label font size. Labels may shrink to fit short rows.
        /// </summary>
        public float LabelFontSize
        {
            get => labelFontSize;
            set => SetValue(ref labelFontSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Gap between category labels and the plot in local UI units.
        /// </summary>
        public float LabelPadding
        {
            get => labelPadding;
            set => SetValue(ref labelPadding, Mathf.Max(0, value));
        }

        /// <summary>
        /// How bottom-axis labels turn when crowded.
        /// </summary>
        public LabelRotation LabelRotationMode
        {
            get => labelRotation;
            set => SetValue(ref labelRotation, value);
        }

        /// <summary>
        /// Display numeric labels along the value axis.
        /// </summary>
        public bool ShowScaleLabels
        {
            get => showScaleLabels;
            set => SetValue(ref showScaleLabels, value);
        }

        /// <summary>
        /// Numeric axis font size. Scale labels may shrink to fit the plot.
        /// </summary>
        public float ScaleFontSize
        {
            get => scaleFontSize;
            set => SetValue(ref scaleFontSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Gap between numeric scale labels and the plot in local UI units.
        /// </summary>
        public float ScalePadding
        {
            get => scalePadding;
            set => SetValue(ref scalePadding, Mathf.Max(0, value));
        }

        /// <summary>
        /// Display numeric data values beside bars or points.
        /// </summary>
        public bool ShowValues
        {
            get => showValues;
            set => SetValue(ref showValues, value);
        }

        /// <summary>
        /// Data value font size. Values may shrink to fit the plot.
        /// </summary>
        public float ValueFontSize
        {
            get => valueFontSize;
            set => SetValue(ref valueFontSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Gap between data values and their bars in local UI units.
        /// </summary>
        public float ValuePadding
        {
            get => valuePadding;
            set => SetValue(ref valuePadding, Mathf.Max(0, value));
        }

        /// <summary>
        /// Replace the data list and schedule a refresh.
        /// The list is retained by reference; null clears the chart.
        /// </summary>
        public void UpdateData(List<DataPoint> newData) => UpdatePoints(ref dataPoints, newData);

        /// <summary>
        /// Append a data point and schedule a refresh. Null point references are ignored.
        /// </summary>
        public void AddDataPoint(string label, float value) => AddPoint(ref dataPoints, new DataPoint(label, value));

        /// <summary>
        /// Remove the specified data point and schedule a refresh.
        /// Missing labels, references and invalid indices are ignored.
        /// </summary>
        public void RemoveDataPoint(int index) => RemovePoint(dataPoints, index);

        /// <summary>
        /// Remove all data points and schedule a refresh.
        /// </summary>
        public void ClearData() => ClearPoints(dataPoints);

        /// <summary>
        /// Update the specified data point and schedule a refresh.
        /// Invalid indices and missing references are ignored.
        /// </summary>
        public void SetDataPoint(int index, string label, float value) => SetPointAt(dataPoints, index, label, value, static () => new DataPoint());

        /// <summary>
        /// Update the specified data point and schedule a refresh.
        /// Invalid indices and missing references are ignored.
        /// </summary>
        public void SetDataPoint(DataPoint item, string label, float value) => SetPoint(item, label, value);
        #endregion

#if EVO_LOCALIZATION
        protected override void LocalizeLabels() => LocalizePoints(dataPoints);
#endif
    }
}