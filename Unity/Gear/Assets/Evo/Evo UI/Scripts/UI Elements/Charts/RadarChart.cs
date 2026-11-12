using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Renders labeled values on radial axes with optional grid, fill, outline and markers.
    /// </summary>
    [AddComponentMenu("Evo/UI/UI Elements/Radar Chart"), HelpURL(Constants.HelpUrl + "ui-elements/radar-chart")]
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public class RadarChart : ChartBase
    {
        // Chart Data
        [Tooltip("Ordered chart data. Null entries render as zero. Call MarkDirty after direct runtime edits.")]
        public List<DataPoint> dataPoints = new()
        {
            new("Speed", 85f),
            new("Shooting", 70f),
            new("Passing", 75f),
            new("Dribbling", 90f),
            new("Defense", 45f),
            new("Physical", 80f)
        };

        // Chart Settings
        [SerializeField, Range(0f, 360f), Tooltip("Angle of the first radar axis in degrees counterclockwise from the right.")]
        private float angleOffset = 90f;

        [Min(0.001f), Tooltip("Value represented by the outermost grid polygon.")]
        public float maximumValue = 100f;
        [Tooltip("Display the chart axes.")]
        public bool showAxis = true;
        [Tooltip("Display numeric data values beside bars or points. Radar values require Show Labels.")]
        public bool showValues;

        // Axis Settings
        [SerializeField, Range(0f, 40f), Tooltip("Axis line width in local UI units.")]
        private float axisThickness = 2f;

        // Fill Settings
        [SerializeField, Tooltip("Display the filled area enclosed by the chart data.")]
        private bool showFill = true;

        // Line Settings
        [SerializeField, Tooltip("Connect the radar data points with an outline.")]
        private bool showLine = true;
        [SerializeField, Range(0.1f, 20f), Tooltip("Data line width in local UI units.")]
        private float lineThickness = 3f;

        // Point Settings
        [SerializeField, Tooltip("Display a marker at each data point.")]
        private bool showPoints = true;
        [SerializeField, Range(0.1f, 50f), Tooltip("Diameter of each data marker in local UI units.")]
        private float pointSize = 15f;

        // Grid Settings
        [SerializeField, Tooltip("Display grid lines to help compare values.")]
        private bool showGrid = true;
        [SerializeField, Range(1, 30), Tooltip("Number of concentric radar grid polygons, from 1 to 30.")]
        private int gridLevels = 5;
        [SerializeField, Range(0f, 20f), Tooltip("Grid line width in local UI units. Limited by the available radar radius.")]
        private float gridLineThickness = 2f;

        // Label Settings
        [SerializeField, Tooltip("Display chart labels.")]
        private bool showLabels = true;
        [SerializeField, Tooltip("Distance beyond the radar radius for category labels in local UI units.")]
        private float labelOffset = 20f;
        [SerializeField, Tooltip("Category or slice label font size. Cartesian labels may shrink to fit short rows.")]
        private float labelFontSize = 14f;
        [SerializeField, Tooltip("Draw a procedural background behind each label.")]
        private bool drawLabelBackground = false;
        [SerializeField, Tooltip("Horizontal and vertical inset on each side of a label background, in local UI units.")]
        private Vector2 labelPadding = new(8f, 4f);
        [SerializeField, Min(0f), Tooltip("Uniform label background corner radius.")]
        private float labelCornerRadius = 6f;
        [Tooltip("Corner, edge softness and outline settings for label backgrounds.")]
        public ShapeSettings labelShape = new();

        // Styling
        [Tooltip("Color mapping for the axes.")]
        public ColorMapping axisColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for the radar area fill. Use its alpha to control opacity.")]
        public ColorMapping fillColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for the data line. Also supplies the line chart fill color.")]
        public ColorMapping lineColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for data markers.")]
        public ColorMapping pointColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for the grid lines.")]
        public ColorMapping gridColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for category or slice labels.")]
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for label backgrounds.")]
        public ColorMapping labelBackgroundColor = new() { stylerID = "Primary" };
        [Tooltip("Font mapping for category or slice labels. Uses the text provider's default font when no font resolves.")]
        public FontMapping labelFont = new() { stylerID = "Regular" };

        // Runtime State
        float radius;
        float hitRadius;
        readonly List<Vector2> points = new();
        readonly List<Vector2> directions = new();

        // Small markers are hard to point at, so points react within at least this radius
        const float MinHitRadius = 8f;

        #region Styler Metadata
        /// <summary>
        /// Return serialized color mapping field names for Styler integration.
        /// </summary>
        public static string[] GetColorFields() => new[]
        {
            "axisColor", "fillColor", "lineColor", "pointColor", "gridColor", "labelColor",
            "valueColor", "labelBackgroundColor"
        };

        /// <summary>
        /// Return serialized font mapping field names for Styler integration.
        /// </summary>
        public static string[] GetFontFields() => new[] { "labelFont" };
        #endregion

        #region State Collection
        protected override void CollectState(ref ChartState state)
        {
            state.Add(maximumValue);
            state.Add(showAxis);
            state.Add(showValues);
            state.Add(angleOffset);
            state.Add(axisThickness);
            state.Add(showFill);
            state.Add(showLine);
            state.Add(lineThickness);
            state.Add(showPoints);
            state.Add(pointSize);
            state.Add(showGrid);
            state.Add(gridLevels);
            state.Add(gridLineThickness);
            state.Add(showLabels);
            state.Add(labelOffset);
            state.Add(labelFontSize);
            state.Add(drawLabelBackground);
            state.Add(labelPadding);
            state.Add(labelCornerRadius);
            state.Add(labelShape);
            state.Add(Styler.GetColor(styleProvider, axisColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, fillColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, lineColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, pointColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, gridColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, labelColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, labelBackgroundColor, stylerPreset));
            state.Add(GetFont(labelFont));
            state.Add(dataPoints?.Count ?? 0);

            if (dataPoints == null)
                return;

            foreach (var point in dataPoints)
            {
                state.Add(point?.label);
                state.Add(point?.value ?? 0);
            }
        }
        #endregion

        #region Types
        /// <summary>
        /// A labeled value associated with one radar chart axis.
        /// </summary>
        [System.Serializable]
        public new class DataPoint : ChartBase.DataPoint
        {
            public DataPoint(string label = "", float value = 50) : base(label, Positive(value)) { }
        }
        #endregion

        #region Chart Rendering
        protected override void RefreshChart()
        {
            points.Clear();

            if (dataPoints == null || dataPoints.Count < 3)
                return;

            // Calculate actual radius
            float width = RectTransform.rect.width;
            float height = RectTransform.rect.height;
            float minDimension = Mathf.Min(width, height);
            radius = Positive(minDimension / 2f);

            if (radius <= 0)
                return;

            directions.Clear();

            for (int i = 0; i < dataPoints.Count; i++)
            {
                float angle = (360f / dataPoints.Count * i + Mathf.Repeat(Finite(angleOffset), 360)) * Mathf.Deg2Rad;
                directions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }

            // Create containers with full anchors
            GameObject gridContainer = Container("Grid");
            GameObject dataContainer = Container("Data");
            GameObject labelContainer = Container("Labels");

            // Draw components
            Vector2 center = Vector2.zero;

            if (showGrid)
                DrawGrid(gridContainer, center, radius);

            if (showAxis)
                DrawAxes(gridContainer, center, radius);

            DrawData(dataContainer, center, radius);

            if (showLabels)
                DrawLabels(labelContainer, center, radius);
        }

        void DrawGrid(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;
            Color styledGridColor = Styler.GetColor(styleProvider, gridColor, stylerPreset);

            // Draw concentric polygons
            int levels = Mathf.Clamp(gridLevels, 1, 30);
            for (int level = 1; level <= levels; level++)
            {
                float levelRadius = (radius / levels) * level;
                DrawPolygon(container, center, levelRadius, sides, styledGridColor, gridLineThickness, IndexedName("Grid Level ", level, " Side "));
            }
        }

        void DrawAxes(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;
            Color styledAxisColor = Styler.GetColor(styleProvider, axisColor, stylerPreset);

            // Draw main axes (stronger lines from center to each point)
            for (int i = 0; i < sides; i++)
            {
                Vector2 endPoint = center + directions[i] * radius;
                DrawLine(container, center, endPoint, styledAxisColor, axisThickness, IndexedName("Axis ", i));
            }
        }

        void DrawData(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;
            points.Clear();

            // Calculate data points
            for (int i = 0; i < sides; i++)
            {
                // Normalize to 0-1
                float value = Mathf.Clamp01(Positive(dataPoints[i]?.value ?? 0) / Mathf.Max(0.001f, Positive(maximumValue)));
                float pointRadius = radius * value;
                Vector2 point = center + directions[i] * pointRadius;
                points.Add(point);
            }

            // Get styled colors
            Color styledFillColor = Styler.GetColor(styleProvider, fillColor, stylerPreset);
            Color styledLineColor = Styler.GetColor(styleProvider, lineColor, stylerPreset);
            Color styledPointColor = Styler.GetColor(styleProvider, pointColor, stylerPreset);

            // Draw filled area
            if (showFill && points.Count >= 3)
            {
                for (int start = 0, chunk = 0; start < points.Count; start += RadarChartGraphic.MaxSegments, chunk++)
                {
                    var rt = Element(container.transform, chunk == 0 ? "Fill" : IndexedName("Fill ", chunk), typeof(RadarChartGraphic));
                    Layout(rt, Center, Center, Center, center, 2 * radius * Vector2.one);
                    var fill = rt.GetComponent<RadarChartGraphic>();

                    if (points.Count <= RadarChartGraphic.MaxSegments) { fill.SetData(points, center, styledFillColor); }
                    else
                    {
                        fill.SetData(points, start, Mathf.Min(RadarChartGraphic.MaxSegments, points.Count - start) + 1,
                            center, styledFillColor, true);
                    }
                }
            }

            // Draw outline
            if (showLine)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    int nextIndex = (i + 1) % points.Count;
                    DrawLine(container, points[i], points[nextIndex], styledLineColor, lineThickness, IndexedName("Data Line ", i));
                }
            }

            // Draw points
            if (showPoints)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var point = DrawPoint(container, points[i], styledPointColor, pointSize, IndexedName("Data Point ", i));
                    SetPointVisual(i, point, Vector2.zero, new Vector2(2, 2));
                }
            }

            hitRadius = Mathf.Max(showPoints ? Positive(pointSize) * 0.5f : 0, MinHitRadius);
        }

        void DrawLabels(GameObject container, Vector2 center, float radius)
        {
            int sides = dataPoints.Count;

            // Get styled font and colors
            Object styledLabelFont = GetFont(labelFont);
            Color styledLabelColor = Styler.GetColor(styleProvider, labelColor, stylerPreset);
            Color styledValueColor = Styler.GetColor(styleProvider, valueColor, stylerPreset);
            Color styledLabelBackgroundColor = Styler.GetColor(styleProvider, labelBackgroundColor, stylerPreset);

            for (int i = 0; i < sides; i++)
            {
                Vector2 labelPos = center + directions[i] * Positive(radius + Finite(labelOffset));
                Badge(container, IndexedName("Label ", i), dataPoints[i]?.label ?? "", labelPos, styledLabelFont,
                    labelFontSize, styledLabelColor, drawLabelBackground, styledLabelBackgroundColor,
                    labelPadding, labelCornerRadius, labelShape,
                    detail: showValues ? FormatValue(dataPoints[i]?.value ?? 0) : null, detailColor: styledValueColor);
            }
        }
        #endregion

        #region Drawing Helpers
        void DrawPolygon(GameObject container, Vector2 center, float radius, int sides, Color color, float thickness, string prefix)
        {
            for (int i = 0; i < sides; i++)
            {
                Vector2 point1 = center + directions[i] * radius;
                Vector2 point2 = center + directions[(i + 1) % sides] * radius;
                DrawLine(container, point1, point2, color, thickness, IndexedName(prefix, i));
            }
        }

        void DrawLine(GameObject container, Vector2 start, Vector2 end, Color color, float thickness, string name)
        {
            Line(container, start, end, color, thickness, name, Center);
        }

        ProceduralShape DrawPoint(GameObject container, Vector2 position, Color color, float size, string name)
        {
            return Point(container, position, color, size, name, Center);
        }
        #endregion

        #region Pointer Interaction
        protected override int PointCount => dataPoints?.Count ?? 0;

        protected override ChartBase.DataPoint PointAt(int index) => dataPoints[index];

        // The nearest axis by angle gives the index, then its sector or marker is tested
        protected override int HitPoint(Vector2 point)
        {
            int count = points.Count;
            if (count < 3)
                return -1;

            point -= RectTransform.rect.size * 0.5f;
            int focus = FocusIndex;

            if (pointerArea == PointerArea.Element && focus >= 0 && focus < count && IsNearPoint(focus, point))
                return focus;

            float step = 360f / count;
            float first = Mathf.Atan2(directions[0].y, directions[0].x) * Mathf.Rad2Deg;
            float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(Mathf.Repeat(angle - first, 360) / step) % count;

            if (pointerArea == PointerArea.Element)
                return IsNearPoint(index, point) ? index : -1;

            return point.sqrMagnitude <= (radius + hitRadius) * (radius + hitRadius) ? index : -1;
        }

        bool IsNearPoint(int index, Vector2 point)
        {
            float reach = hitRadius + HitMargin(index);
            return (point - points[index]).sqrMagnitude <= reach * reach;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Value represented by the outermost grid polygon.
        /// </summary>
        public float MaximumValue
        {
            get => maximumValue;
            set => SetValue(ref maximumValue, Mathf.Max(0.001f, value));
        }

        /// <summary>
        /// Angle of the first axis in degrees counterclockwise from the right. Wraps at 360.
        /// </summary>
        public float StartAngle
        {
            get => angleOffset;
            set => SetValue(ref angleOffset, Mathf.Repeat(value, 360));
        }

        /// <summary>
        /// Display the outline connecting the data points.
        /// </summary>
        public bool ShowLine
        {
            get => showLine;
            set => SetValue(ref showLine, value);
        }

        /// <summary>
        /// Data line width in local UI units, from 0.1 to 20.
        /// </summary>
        public float LineThickness
        {
            get => lineThickness;
            set => SetValue(ref lineThickness, Mathf.Clamp(value, 0.1f, 20f));
        }

        /// <summary>
        /// Display the filled radar area.
        /// </summary>
        public bool ShowFill
        {
            get => showFill;
            set => SetValue(ref showFill, value);
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
        /// Diameter of each data marker in local UI units, from 0.1 to 50.
        /// </summary>
        public float PointSize
        {
            get => pointSize;
            set => SetValue(ref pointSize, Mathf.Clamp(value, 0.1f, 50f));
        }

        /// <summary>
        /// Display the concentric grid polygons.
        /// </summary>
        public bool ShowGrid
        {
            get => showGrid;
            set => SetValue(ref showGrid, value);
        }

        /// <summary>
        /// Number of concentric grid polygons, from 1 to 30.
        /// </summary>
        public int GridLevels
        {
            get => gridLevels;
            set => SetValue(ref gridLevels, Mathf.Clamp(value, 1, 30));
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
        /// Axis line width in local UI units, from 0 to 40.
        /// </summary>
        public float AxisThickness
        {
            get => axisThickness;
            set => SetValue(ref axisThickness, Mathf.Clamp(value, 0f, 40f));
        }

        /// <summary>
        /// Display the category labels.
        /// </summary>
        public bool ShowLabels
        {
            get => showLabels;
            set => SetValue(ref showLabels, value);
        }

        /// <summary>
        /// Category label font size.
        /// </summary>
        public float LabelFontSize
        {
            get => labelFontSize;
            set => SetValue(ref labelFontSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Distance beyond the plot radius for category labels in local UI units.
        /// </summary>
        public float LabelOffset
        {
            get => labelOffset;
            set => SetValue(ref labelOffset, value);
        }

        /// <summary>
        /// Display each value under its label. Requires Show Labels.
        /// </summary>
        public bool ShowValues
        {
            get => showValues;
            set => SetValue(ref showValues, value);
        }

        /// <summary>
        /// Draw a procedural background behind each label.
        /// </summary>
        public bool DrawLabelBackground
        {
            get => drawLabelBackground;
            set => SetValue(ref drawLabelBackground, value);
        }

        /// <summary>
        /// Horizontal and vertical inset on each side of a label background, in local UI units.
        /// </summary>
        public Vector2 LabelPadding
        {
            get => labelPadding;
            set => SetValue(ref labelPadding, Vector2.Max(Vector2.zero, value));
        }

        /// <summary>
        /// Uniform label background corner radius.
        /// </summary>
        public float LabelCornerRadius
        {
            get => labelCornerRadius;
            set => SetValue(ref labelCornerRadius, Mathf.Max(0, value));
        }

        /// <summary>
        /// Replace the data list and schedule a refresh.
        /// The list is retained by reference; null clears the chart.
        /// </summary>
        public void UpdateData(List<DataPoint> newData) => UpdatePoints(ref dataPoints, newData);

        /// <summary>
        /// Adds a new data point to the chart with a label and value.
        /// </summary>
        public void AddDataPoint(string label, float value) => AddPoint(ref dataPoints, new DataPoint(label, value));

        /// <summary>
        /// Adds a pre-configured DataPoint object to the chart.
        /// </summary>
        public void AddDataPoint(DataPoint point) => AddPoint(ref dataPoints, point);

        /// <summary>
        /// Removes the first data point matching the specified label.
        /// </summary>
        public void RemoveDataPoint(string label)
        {
            if (dataPoints == null)
                return;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (dataPoints[i] != null && dataPoints[i].label == label)
                {
                    RemovePoint(dataPoints, i);
                    return;
                }
            }
        }

        /// <summary>
        /// Removes a data point at a specific index.
        /// </summary>
        public void RemoveDataPoint(int index) => RemovePoint(dataPoints, index);

        /// <summary>
        /// Removes a specific DataPoint object reference.
        /// </summary>
        public void RemoveDataPoint(DataPoint point)
        {
            if (dataPoints != null && dataPoints.Remove(point))
                MarkDirty();
        }

        /// <summary>
        /// Sets data point using the data index.
        /// </summary>
        public void SetDataPoint(int index, float value)
        {
            if (index >= 0 && index < (dataPoints?.Count ?? 0))
            {
                dataPoints[index] ??= new DataPoint();
                dataPoints[index].value = Positive(value);
                MarkDirty();
            }
        }

        /// <summary>
        /// Sets data point using the data label.
        /// </summary>
        public void SetDataPoint(string label, float value)
        {
            if (dataPoints == null)
                return;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (dataPoints[i] != null && dataPoints[i].label == label)
                {
                    SetDataPoint(i, value);
                    return;
                }
            }
        }

        /// <summary>
        /// Sets data point using a specific data reference.
        /// </summary>
        public void SetDataPoint(DataPoint data, string label, float value) => SetPoint(data, label, Positive(value));

        /// <summary>
        /// Updates all chart data.
        /// </summary>
        public void UpdateAllData(float[] values)
        {
            if (values == null || dataPoints == null)
                return;

            for (int i = 0; i < values.Length && i < dataPoints.Count; i++)
            {
                dataPoints[i] ??= new DataPoint();
                dataPoints[i].value = Positive(values[i]);
            }

            MarkDirty();
        }

        /// <summary>
        /// Removes all data points from the chart.
        /// </summary>
        public void ClearDataPoints() => ClearPoints(dataPoints);

        /// <summary>
        /// Set the axis width and schedule a refresh. Same as setting AxisThickness.
        /// </summary>
        public void SetAxisThickness(float value) => AxisThickness = value;
        #endregion

#if EVO_LOCALIZATION
        protected override void LocalizeLabels() => LocalizePoints(dataPoints);
#endif
    }
}