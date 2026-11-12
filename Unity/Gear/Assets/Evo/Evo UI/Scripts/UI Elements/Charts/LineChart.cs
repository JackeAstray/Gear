using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Renders labeled samples as a straight or smoothed line with optional markers and fill.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Line Chart"), HelpURL(Constants.HelpUrl + "ui-elements/line-chart")]
    public class LineChart : ChartBase
    {
        // Chart Data
        [Tooltip("Ordered chart data. Null entries render as zero. Call MarkDirty after direct runtime edits.")]
        public List<DataPoint> dataPoints = new()
        {
            new("Q1", 10f),
            new("Q2", 25f),
            new("Q3", 60f),
            new("Q4", 40f)
        };

        // Scale
        [Range(1, 50), Tooltip("Number of intervals on the numeric axis. Scale labels and grid lines use the same divisions.")]
        [UnityEngine.Serialization.FormerlySerializedAs("horizontalGridLines")]
        public int scaleDivisions = 5;
        [Range(1, 40), Tooltip("Number of intervals across the horizontal axis, from 1 to 40.")]
        public int verticalGridLines = 10;
        [Tooltip("Use the data range automatically, or specify a stable range for comparisons.")]
        public bool autoScale = true;
        [Tooltip("Minimum and maximum of the value axis when Auto Scale is disabled. " +
                 "Reversed bounds are sorted; equal bounds are expanded.")]
        public Vector2 valueRange = new(0f, 100f);
        [Tooltip("Include zero in the automatically calculated line chart range.")]
        public bool includeZero;

        // Line
        [SerializeField, Range(0f, 20f), Tooltip("Data line width in local UI units.")]
        private float lineThickness = 3f;
        [Tooltip("Connect samples with straight lines or a smooth curve that does not overshoot the adjacent values.")]
        public CurveMode curveMode = CurveMode.Straight;
        [Range(2, 32), Tooltip("Maximum subdivisions per smooth segment, from 2 to 32. Dense segments use fewer samples.")]
        public int curveSubdivisions = 16;
        [SerializeField]
        private bool roundCaps;

        // Points
        [SerializeField, Tooltip("Display a marker at each data point.")]
        private bool showPoints = true;
        [SerializeField, Range(0f, 50f), Tooltip("Diameter of each data marker in local UI units.")]
        private float pointSize = 12f;

        // Fill
        [Tooltip("Display the filled area enclosed by the chart data.")]
        public bool showFill = true;
        [Range(0f, 1f), Tooltip("Line fill alpha multiplier at the top of the plot.")]
        public float fillOpacity = 0.2f;
        [Range(0f, 1f), Tooltip("Fraction of the fill alpha retained at the bottom of the plot. Zero fades to transparent.")]
        public float fillBottomOpacity = 0f;

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
        [SerializeField, Range(0f, 20f), Tooltip("Grid line width in local UI units.")]
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
        [Tooltip("Color mapping for the data line. Also supplies the line chart fill color.")]
        public ColorMapping lineColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for data markers.")]
        public ColorMapping pointColor = new() { stylerID = "Primary" };

        // Runtime State
        readonly List<Vector2> positions = new();
        readonly List<Vector2> curve = new();
        double rangeMin;
        double rangeMax;
        PlotFrame frame;
        float hitRadius;

        // Small markers are hard to point at, so points react within at least this radius
        const float MinHitRadius = 8f;

        // Properties
        int ScaleDivisionCount => Mathf.Clamp(scaleDivisions, 1, 50);
        int DataCount => dataPoints?.Count ?? 0;
        string DataLabel(int index) => dataPoints[index]?.label;
        float DataValue(int index) => dataPoints[index]?.value ?? 0;
        float ValueClearance => Mathf.Max(Positive(lineThickness) * 0.5f, showPoints ? Positive(pointSize) * 0.5f : 0);

        #region Styler Metadata
        /// <summary>
        /// Return serialized color mapping field names for Styler integration.
        /// </summary>
        public static string[] GetColorFields() => new[]
        {
            "lineColor", "pointColor", "gridColor", "axisColor", "labelColor", "valueColor", "scaleColor"
        };

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

            state.Add(autoScale);
            state.Add((int)curveMode);
            state.Add(curveSubdivisions);
            state.Add(showFill);
            state.Add(fillOpacity);
            state.Add(fillBottomOpacity);
            state.Add(includeZero);
            state.Add(valueRange);
            state.Add(showPoints);
            state.Add(lineThickness);
            state.Add(roundCaps);
            state.Add(verticalGridLines);
            state.Add(pointSize);
            state.Add(Styler.GetColor(styleProvider, lineColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, pointColor, stylerPreset));
        }
        #endregion

        #region Types
        /// <summary>
        /// Controls whether line chart samples are joined by straight segments or a smooth curve.
        /// </summary>
        public enum CurveMode { Straight, Smooth }

        /// <summary>
        /// A labeled sample in a line chart's ordered data series.
        /// </summary>
        [System.Serializable]
        public new class DataPoint : ChartBase.DataPoint
        {
            public DataPoint(string label = "", float value = 0) : base(label, value) { }
        }
        #endregion

        #region Label Preparation
        void PrepareCartesianLabels(GameObject parent, double minimum, double maximum,
            bool horizontal, string categoryPrefix, string scalePrefix)
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
            positions.Clear();

            if (dataPoints == null || dataPoints.Count == 0 || !HasPlotSpace())
                return;

            CalculateRange();

            GameObject grid = Container("Grid");
            GameObject data = Container("Data");
            GameObject labels = Container("Labels");

            PrepareCartesianLabels(labels, rangeMin, rangeMax, false, "X-Label ", "Y-Label ");

            // Markers and strokes reach half their size past the plot. The line's ends do too, while its
            // bounded joins can reach a full line thickness above or below a sample at a sharp corner.
            float stroke = Mathf.Max(showPoints ? Positive(pointSize) * 0.5f : 0, Mathf.Max(showGrid ? Positive(gridLineThickness) * 0.5f : 0,
                showAxis ? Positive(axisThickness) * 0.5f : 0));
            float line = Positive(lineThickness);
            frame = FitPlot(new Vector2(Mathf.Max(stroke, line * 0.5f), Mathf.Max(stroke, line)),
                new Vector3(labelPadding, scalePadding, valuePadding), labelRotation, false,
                bottomStep: 1f / Mathf.Max(1, dataPoints.Count - 1), valueClearance: ValueClearance);
            Rect plot = frame.plot;

            if (!HasPlotSpace(frame))
                return;

            if (showGrid)
                DrawGrid(grid, plot.x, plot.y, plot.width, plot.height);

            if (showAxis)
                DrawCartesianAxes(grid, frame, axisThickness, Styler.GetColor(styleProvider, axisColor, stylerPreset));

            DrawData(data, plot.x, plot.y, plot.width, plot.height);
            DrawLabels(plot.x, plot.y, plot.width, plot.height);
        }

        void DrawGrid(GameObject container, float left, float bottom, float width, float height)
        {
            Color styledGridColor = Styler.GetColor(styleProvider, gridColor, stylerPreset);

            DrawScaleGrid(container, frame, ScaleDivisionCount, gridLineThickness,
                    Styler.GetColor(styleProvider, gridColor, stylerPreset), false);

            // Vertical grid lines
            int divisions = Mathf.Clamp(verticalGridLines, 1, 40);
            for (int i = 0; i <= divisions; i++)
            {
                float x = left + i * width / divisions;
                CreateLine(container, new Vector2(x, bottom), new Vector2(x, bottom + height),
                    styledGridColor, gridLineThickness, IndexedName("V-Grid ", i));
            }
        }

        void DrawData(GameObject container, float left, float bottom, float width, float height)
        {
            if (dataPoints.Count == 0)
                return;

            positions.Clear();

            float xSpacing = width / Mathf.Max(1, dataPoints.Count - 1);
            for (int i = 0; i < dataPoints.Count; i++)
            {
                float x = left + (dataPoints.Count == 1 ? width * 0.5f : i * xSpacing);
                float normalized = Mathf.Clamp01((float)((Finite(dataPoints[i]?.value ?? 0) - rangeMin) / (rangeMax - rangeMin)));
                positions.Add(new Vector2(x, bottom + normalized * height));
            }

            // Get styled colors
            Color styledLineColor = Styler.GetColor(styleProvider, lineColor, stylerPreset);
            Color styledPointColor = Styler.GetColor(styleProvider, pointColor, stylerPreset);

            ChartCurve.Build(positions, curve, curveMode == CurveMode.Smooth, curveSubdivisions);

            Color areaColor = styledLineColor;
            areaColor.a *= Mathf.Clamp01(Finite(fillOpacity));

            // Chunk without dropping samples, and share boundary points/normals between meshes.
            for (int start = 0, chunk = 0; start < curve.Count - 1; start += LineChartGraphic.MaxPoints - 1, chunk++)
            {
                int count = Mathf.Min(LineChartGraphic.MaxPoints, curve.Count - start);
                var rt = Element(container.transform, IndexedName("Series ", chunk), typeof(LineChartGraphic));

                // Geometry uses bottom-left chart coordinates; matching bounds also keep RectMask2D culling correct
                Layout(rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, RectTransform.rect.size);
                rt.GetComponent<LineChartGraphic>().SetData(curve, start, count,
                    Mathf.Min(FitStroke(frame, lineThickness), Mathf.Max(0, frame.edge.y - 2)), roundCaps,
                    styledLineColor, showFill, areaColor, bottom, height, Mathf.Clamp01(Finite(fillBottomOpacity)));
            }

            // Draw points
            for (int i = 0; showPoints && i < positions.Count; i++)
            {
                if (Positive(pointSize) > 0)
                {
                    var point = CreatePoint(container, positions[i], styledPointColor,
                        FitStroke(frame, pointSize), IndexedName("Data Point ", i));
                    SetPointVisual(i, point, Vector2.zero, new Vector2(2, 2));
                }
            }

            hitRadius = Mathf.Max(showPoints ? FitStroke(frame, pointSize) * 0.5f : 0, MinHitRadius);
        }

        void DrawLabels(float left, float bottom, float width, float height)
        {
            float step = width / Mathf.Max(1, dataPoints.Count - 1);
            for (int i = 0; i < dataPoints.Count; i++)
            {
                float x = left + (dataPoints.Count == 1 ? width * 0.5f : i * step);

                if (showCategoryLabels)
                    PlaceBottomLabel(bottomLabels[i], frame, x, step);

                if (showValues)
                    PlaceValue(valueLabels[i], frame, positions[i] + Vector2.up * ValueClearance, step, false);
            }

            int divisions = ScaleDivisionCount;
            for (int i = 0; showScaleLabels && i <= divisions; i++)
                PlaceLeftLabel(leftLabels[i], frame, bottom + (float)i / divisions * height, height / divisions);
        }
        #endregion

        #region Range Calculation
        void CalculateRange()
        {
            rangeMin = Finite(valueRange.x);
            rangeMax = Finite(valueRange.y);

            if (autoScale)
            {
                rangeMin = double.MaxValue;
                rangeMax = double.MinValue;

                foreach (var point in dataPoints)
                {
                    double value = Finite(point?.value ?? 0);
                    rangeMin = System.Math.Min(rangeMin, value);
                    rangeMax = System.Math.Max(rangeMax, value);
                }
            }

            if (autoScale && includeZero)
            {
                rangeMin = System.Math.Min(0, rangeMin);
                rangeMax = System.Math.Max(0, rangeMax);
            }

            if (rangeMax < rangeMin)
                (rangeMin, rangeMax) = (rangeMax, rangeMin);

            if (rangeMax == rangeMin)
            {
                double margin = System.Math.Max(1, System.Math.Abs(rangeMin) * 0.1);
                rangeMin = System.Math.Max(-float.MaxValue, rangeMin - margin);
                rangeMax = System.Math.Min(float.MaxValue, rangeMax + margin);
            }
        }
        #endregion

        #region Drawing Helpers
        void CreateLine(GameObject container, Vector2 start, Vector2 end, Color color, float thickness, string name)
        {
            Line(container, start, end, color, FitStroke(frame, thickness), name, Vector2.zero);
        }

        ProceduralShape CreatePoint(GameObject container, Vector2 position, Color color, float size, string name)
        {
            return Point(container, position, color, size, name, Vector2.zero);
        }
        #endregion

        #region Pointer Interaction
        protected override int PointCount => dataPoints?.Count ?? 0;

        protected override ChartBase.DataPoint PointAt(int index) => dataPoints[index];

        // The index comes from the point spacing, then its column or marker is tested
        protected override int HitPoint(Vector2 point)
        {
            int count = positions.Count;
            if (count == 0)
                return -1;

            Rect plot = frame.plot;
            float spacing = count > 1 ? positions[1].x - positions[0].x : plot.width;
            int focus = FocusIndex;

            if (pointerArea == PointerArea.Element && focus >= 0 && focus < count && IsNearPoint(focus, point))
                return focus;

            int index = count > 1 && spacing > 0 ? Mathf.Clamp(Mathf.RoundToInt((point.x - positions[0].x) / spacing), 0, count - 1) : 0;

            if (pointerArea == PointerArea.Element)
                return IsNearPoint(index, point) ? index : -1;

            bool inColumn = Mathf.Abs(point.x - positions[index].x) <= spacing * 0.5f;
            return inColumn && point.y >= plot.yMin - hitRadius && point.y <= plot.yMax + hitRadius ? index : -1;
        }

        bool IsNearPoint(int index, Vector2 point)
        {
            float radius = hitRadius + HitMargin(index);
            return (point - positions[index]).sqrMagnitude <= radius * radius;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Use the data range automatically. Disable to use Value Range.
        /// </summary>
        public bool AutoScale
        {
            get => autoScale;
            set => SetValue(ref autoScale, value);
        }

        /// <summary>
        /// Minimum and maximum of the value axis while Auto Scale is disabled.
        /// </summary>
        public Vector2 ValueRange
        {
            get => valueRange;
            set => SetValue(ref valueRange, value);
        }

        /// <summary>
        /// Include zero in the automatically calculated range.
        /// </summary>
        public bool IncludeZero
        {
            get => includeZero;
            set => SetValue(ref includeZero, value);
        }

        /// <summary>
        /// Number of intervals on the numeric axis, from 1 to 50.
        /// </summary>
        public int ScaleDivisions
        {
            get => scaleDivisions;
            set => SetValue(ref scaleDivisions, Mathf.Clamp(value, 1, 50));
        }

        /// <summary>
        /// Data line width in local UI units, from 0 to 20.
        /// </summary>
        public float LineThickness
        {
            get => lineThickness;
            set => SetValue(ref lineThickness, Mathf.Clamp(value, 0f, 20f));
        }

        /// <summary>
        /// Round both ends of the data line.
        /// </summary>
        public bool RoundCaps
        {
            get => roundCaps;
            set => SetValue(ref roundCaps, value);
        }

        /// <summary>
        /// Connect samples with straight lines or a smooth curve.
        /// </summary>
        public CurveMode Curve
        {
            get => curveMode;
            set => SetValue(ref curveMode, value);
        }

        /// <summary>
        /// Maximum subdivisions per smooth segment, from 2 to 32.
        /// </summary>
        public int CurveSubdivisions
        {
            get => curveSubdivisions;
            set => SetValue(ref curveSubdivisions, Mathf.Clamp(value, 2, 32));
        }

        /// <summary>
        /// Display the filled area under the line.
        /// </summary>
        public bool ShowFill
        {
            get => showFill;
            set => SetValue(ref showFill, value);
        }

        /// <summary>
        /// Fill alpha multiplier at the top of the plot, from 0 to 1.
        /// </summary>
        public float FillOpacity
        {
            get => fillOpacity;
            set => SetValue(ref fillOpacity, Mathf.Clamp01(value));
        }

        /// <summary>
        /// Fraction of the fill alpha kept at the bottom of the plot, from 0 to 1.
        /// </summary>
        public float FillBottomOpacity
        {
            get => fillBottomOpacity;
            set => SetValue(ref fillBottomOpacity, Mathf.Clamp01(value));
        }

        /// <summary>
        /// Display a marker at each data point.
        /// </summary>
        public bool ShowPoints
        {
            get => showPoints;
            set => SetValue(ref showPoints, value);
        }

        /// <summary>
        /// Diameter of each data marker in local UI units, from 0 to 50.
        /// </summary>
        public float PointSize
        {
            get => pointSize;
            set => SetValue(ref pointSize, Mathf.Clamp(value, 0f, 50f));
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
        /// Number of intervals across the horizontal axis, from 1 to 40.
        /// </summary>
        public int VerticalGridLines
        {
            get => verticalGridLines;
            set => SetValue(ref verticalGridLines, Mathf.Clamp(value, 1, 40));
        }

        /// <summary>
        /// Grid line width in local UI units, from 0 to 20.
        /// </summary>
        public float GridLineThickness
        {
            get => gridLineThickness;
            set => SetValue(ref gridLineThickness, Mathf.Clamp(value, 0f, 20f));
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
        /// Category label font size. Labels may shrink to fit short columns.
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
        /// Gap between data values and their points in local UI units.
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
        public void SetDataPoint(DataPoint targetData, string label, float value) => SetPoint(targetData, label, value);

        /// <summary>
        /// Set the marker diameter and schedule a refresh. Same as setting PointSize.
        /// </summary>
        public void SetPointSize(float newSize) => PointSize = newSize;
        #endregion

#if EVO_LOCALIZATION
        protected override void LocalizeLabels() => LocalizePoints(dataPoints);
#endif

        #region Obsolete API
        [System.Obsolete("Use scaleDivisions instead.")]
        public int horizontalGridLines { get => scaleDivisions; set => scaleDivisions = value; }
        #endregion
    }
}