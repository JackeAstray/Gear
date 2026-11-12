using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Renders a pie or donut chart with optional labels, borders and an external legend.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Pie Chart"), HelpURL(Constants.HelpUrl + "ui-elements/pie-chart")]
    public class PieChart : ChartBase
    {
        // Chart Data
        [Tooltip("Ordered chart data. Null entries render as zero. Call MarkDirty after direct runtime edits.")]
        public List<DataPoint> dataPoints = new()
        {
            new("Category A", 30f, new Color(0.2f, 0.5f, 1f, 1f)),
            new("Category B", 25f, new Color(1f, 0.4f, 0.3f, 1f)),
            new("Category C", 20f, new Color(0.3f, 0.8f, 0.4f, 1f)),
            new("Category D", 15f, new Color(1f, 0.8f, 0.2f, 1f))
        };

        // Chart Settings
        [Range(0f, 360f), Tooltip("Angle of the first slice in degrees counterclockwise from the right.")]
        public float startAngle = 90f;
        [Range(3, 512), Tooltip("Minimum tessellation detail.")]
        public int segments = 100;
        [Range(0f, 0.95f), Tooltip("Hole radius as a fraction of the outer radius. Zero draws a solid pie.")]
        public float innerRadiusRatio;
        [Min(0f), Tooltip("Gap between adjacent slices in local UI units. The gap keeps the same width from the center outward.")]
        public float sliceSpacing;
        [Range(0f, 3f), Tooltip("Smoothing width in screen pixels for the silhouette, outline and slice boundaries.")]
        public float edgeSmoothing = 1;
        [Range(0f, 4f), Tooltip("Decimal places in slice and legend percentages, from 0 to 4.")]
        public int percentageDecimals;
        [Range(0f, 100f), Tooltip("Hide labels below this percentage to reduce overlap on small slices.")]
        public float minimumLabelPercentage;

        // Border Settings
        [Tooltip("Draw an outline along the inner and outer circumference of the pie.")]
        public bool drawBorder = false;
        [Min(0f), Tooltip("Outline width in local UI units. Clamped to the available pie or donut thickness.")]
        public float borderWidth = 2f;
        [Tooltip("Place the circumference outline inside, centered on, or outside the nominal radii.")]
        public BorderPlacement borderPlacement = BorderPlacement.Inside;

        // Label Settings
        [Tooltip("Display chart labels.")]
        public bool showLabels = true;
        [Tooltip("Show percentages on pie slices. Disable to show formatted values.")]
        public bool showPercentages = true;
        [Tooltip("Category or slice label font size. Cartesian labels may shrink to fit short rows.")]
        public float labelFontSize = 24f;
        [Tooltip("Draw a procedural background behind each label.")]
        public bool drawLabelBackground = true;
        [Tooltip("Horizontal and vertical inset on each side of a label background, in local UI units.")]
        public Vector2 labelPadding = new(8f, 4f);
        [Min(0f), Tooltip("Uniform label background corner radius, in pixels or percent according to Label Shape radius mode.")]
        public float labelCornerRadius = 6f;
        [Tooltip("Corner, edge softness and outline settings for label backgrounds.")]
        public ShapeSettings labelShape = new();

        // Legend Settings
        [Tooltip("Display a legend in the assigned container.")]
        public bool showLegend = true;
        [Tooltip("Container for this chart's generated legend.")]
        public RectTransform legendContainer;
        [Tooltip("Vertical spacing and height of each legend row in local UI units.")]
        public float legendItemHeight = 40f;
        [Tooltip("Side length of each legend color swatch in local UI units.")]
        public float legendColorBoxSize = 24f;
        [Tooltip("Font size used by legend labels.")]
        public float legendFontSize = 24f;

        [Min(0f), Tooltip("Uniform swatch corner radius, in pixels or percent according to Legend Shape radius mode.")]
        public float legendCornerRadius = 4f;
        [Tooltip("Corner, edge softness and outline settings for legend swatches.")]
        public ShapeSettings legendShape = new();
        [Tooltip("Show percentages, values, both, or category names only in the legend.")]
        public LegendValueMode legendValues = LegendValueMode.Percentage;
        [Tooltip("Omit legend rows whose rendered value is zero, including negative or non-finite values.")]
        public bool hideZeroValueLegendItems;

        // Styling
        [Tooltip("Color mapping for the pie circumference outline.")]
        public ColorMapping borderColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for category or slice labels.")]
        public ColorMapping labelColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for label backgrounds.")]
        public ColorMapping labelBackgroundColor = new() { stylerID = "Primary" };
        [Tooltip("Color mapping for legend text.")]
        public ColorMapping legendTextColor = new() { stylerID = "Primary" };
        [Tooltip("Font mapping for category or slice labels. Uses the text provider's default font when no font resolves.")]
        public FontMapping labelFont = new() { stylerID = "Regular" };
        [Tooltip("Font mapping for legend text. Uses the text provider's default font when no font resolves.")]
        public FontMapping legendFont = new() { stylerID = "Regular" };

        // Generated Output
        [SerializeField, HideInInspector] private RectTransform legendRoot;

        // Runtime State
        double totalValue;
        readonly List<int> visibleSlices = new();
        float radius;
        static readonly Dictionary<RectTransform, PieChart> legendOwners = new();
        static readonly string[] PercentageFormats = { "F0", "F1", "F2", "F3", "F4" };
        RectTransform claimedLegend;
        string legendName;
        readonly List<LegendRow> legendRows = new();

        // Pointer State
        readonly List<SliceArc> arcs = new();
        float innerRadius;

        protected override void OnEnable()
        {
            // Claim saved output before a duplicate can adopt its external reference
            if (legendRoot != null && OwnsLegend(legendRoot))
                ClaimLegend(legendRoot);

            base.OnEnable();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (!ReferenceEquals(claimedLegend, null) && legendOwners.TryGetValue(claimedLegend, out var owner) && owner == this)
                legendOwners.Remove(claimedLegend);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            borderWidth = Positive(borderWidth);
            sliceSpacing = Positive(sliceSpacing);
            innerRadiusRatio = Mathf.Clamp(Finite(innerRadiusRatio), 0, 0.95f);
            edgeSmoothing = Mathf.Clamp(Finite(edgeSmoothing), 0, 3);
            segments = Mathf.Clamp(segments, 3, 512);

            base.OnValidate();
        }
#endif

        #region Styler Metadata
        /// <summary>
        /// Return serialized color mapping field names for Styler integration.
        /// </summary>
        public static string[] GetColorFields() => new[] { "borderColor", "labelColor", "labelBackgroundColor", "legendTextColor" };

        /// <summary>
        /// Return serialized font mapping field names for Styler integration.
        /// </summary>
        public static string[] GetFontFields() => new[] { "labelFont", "legendFont" };
        #endregion

        #region State Collection
        protected override void CollectState(ref ChartState state)
        {
            state.Add(innerRadiusRatio);
            state.Add(sliceSpacing);
            state.Add(edgeSmoothing);
            state.Add(percentageDecimals);
            state.Add(minimumLabelPercentage);
            state.Add((int)borderPlacement);
            state.Add(startAngle);
            state.Add(segments);
            state.Add(drawBorder);
            state.Add(borderWidth);
            state.Add(showLabels);
            state.Add(showPercentages);
            state.Add(labelFontSize);
            state.Add(drawLabelBackground);
            state.Add(labelPadding);
            state.Add(showLegend);
            state.Add(legendContainer);

            // Reparenting can make a previously valid external container unsafe.
            state.Add(CanUseLegendContainer());
            state.Add(legendItemHeight);
            state.Add(legendColorBoxSize);
            state.Add(legendFontSize);
            state.Add(labelCornerRadius);
            state.Add(labelShape);
            state.Add(legendCornerRadius);
            state.Add(legendShape);
            state.Add((int)legendValues);
            state.Add(hideZeroValueLegendItems);
            state.Add(Styler.GetColor(styleProvider, borderColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, labelColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, labelBackgroundColor, stylerPreset));
            state.Add(Styler.GetColor(styleProvider, legendTextColor, stylerPreset));
            state.Add(GetFont(labelFont));
            state.Add(GetFont(legendFont));
            state.Add(dataPoints?.Count ?? 0);

            if (dataPoints == null)
                return;

            foreach (var point in dataPoints)
            {
                state.Add(point?.label);
                state.Add(point?.value ?? 0);
                state.Add(point?.color ?? Color.clear);
            }
        }
        #endregion

        #region Types
        /// <summary>
        /// Positions the pie outline inside, centered on, or outside the slice radii.
        /// </summary>
        public enum BorderPlacement { Inside, Center, Outside }

        /// <summary>
        /// Selects the value and percentage information displayed in pie chart legend entries.
        /// </summary>
        public enum LegendValueMode { Percentage, Value, ValueAndPercentage, LabelOnly }

        /// <summary>
        /// A labeled value and color defining a pie or donut slice.
        /// </summary>
        [System.Serializable]
        public new class DataPoint : ColoredDataPoint
        {
            public DataPoint(string label = "", float value = 0, Color? color = null) : base(label, value, color ?? Color.blue) { }
        }
        #endregion

        #region Legend Ownership
        bool OwnsLegend(RectTransform root)
        {
            if (root == null)
                return false;

            return !legendOwners.TryGetValue(root, out var owner) || owner == null || owner == this;
        }

        protected override bool IsGeneratedRoot(Transform child)
        {
            if (child == legendContainer)
                return false;

            if (NameOf(child).StartsWith("Pie Legend ", System.StringComparison.Ordinal))
                return child == legendRoot && OwnsLegend(legendRoot);

            return base.IsGeneratedRoot(child);
        }

        protected override void CollectExtraRoots(List<RectTransform> result)
        {
            // Undo can restore an older serialized root reference. Retain the current
            // output for sweeping when the restored settings use another container.
            if (claimedLegend != null && OwnsLegend(claimedLegend) && !result.Contains(claimedLegend))
                result.Add(claimedLegend);


            // A duplicated chart can retain an external reference. Claiming ensures that
            // only one chart uses that root; the other creates its own independent output.
            if (legendRoot != null && !OwnsLegend(legendRoot))
                legendRoot = null;

            if (legendRoot == null && legendContainer != null)
            {
                var candidate = legendContainer.Find(GetLegendName()) as RectTransform;
                if (OwnsLegend(candidate))
                    legendRoot = candidate;
            }

            if (legendRoot == null)
                return;

            ClaimLegend(legendRoot);

            if (!result.Contains(legendRoot))
                result.Add(legendRoot);
        }

        void ClaimLegend(RectTransform root)
        {
            if (!ReferenceEquals(claimedLegend, null) && claimedLegend != root &&
                legendOwners.TryGetValue(claimedLegend, out var previous) && previous == this)
                legendOwners.Remove(claimedLegend);

            legendRoot = claimedLegend = root;
            legendOwners[root] = this;
        }
        #endregion

        #region Chart Rendering
        protected override void RefreshChart()
        {
            arcs.Clear();

            if (dataPoints == null || dataPoints.Count == 0)
                return;

            totalValue = 0;
            visibleSlices.Clear();

            for (int i = 0; i < dataPoints.Count; i++)
            {
                float value = Positive(dataPoints[i]?.value ?? 0);
                totalValue += value;

                if (value > 0)
                    visibleSlices.Add(i);
            }

            if (totalValue <= 0)
            {
                if (showLegend && CanUseLegendContainer())
                    DrawLegend();

                return;
            }

            radius = Positive(Mathf.Min(RectTransform.rect.width, RectTransform.rect.height) * 0.5f);
            if (radius <= 0)
                return;

            float inner = radius * Mathf.Clamp(Finite(innerRadiusRatio), 0, 0.95f);
            if (inner >= radius)
                return;

            var slices = Container("Slices");
            double currentAngle = Mathf.Repeat(Finite(startAngle), 360);

            for (int sliceIndex = 0; sliceIndex < visibleSlices.Count; sliceIndex++)
            {
                int i = visibleSlices[sliceIndex];
                double span = Positive(dataPoints[i]?.value ?? 0) / totalValue * 360.0;

                if (span <= 0)
                    continue;

                Color previous = dataPoints[visibleSlices[(sliceIndex + visibleSlices.Count - 1) % visibleSlices.Count]].color;
                Color next = dataPoints[visibleSlices[(sliceIndex + 1) % visibleSlices.Count]].color;
                var slice = Slice(slices.transform, IndexedName("Slice ", i), (float)currentAngle, (float)span, radius, inner, dataPoints[i].color, previous, next);

                // Hovered slices move out along their middle angle
                if (PointerEnabled)
                {
                    float middle = (float)(currentAngle + span * 0.5) * Mathf.Deg2Rad;
                    arcs.Add(new SliceArc { index = i, start = (float)currentAngle, span = (float)span });
                    SetPointVisual(i, slice, new Vector2(Mathf.Cos(middle), Mathf.Sin(middle)), Vector2.zero);
                }

                currentAngle += span;
            }

            innerRadius = inner;

            if (showLabels)
            {
                var labels = Container("Labels");
                currentAngle = Mathf.Repeat(Finite(startAngle), 360);

                for (int i = 0; i < dataPoints.Count; i++)
                {
                    double fraction = Positive(dataPoints[i]?.value ?? 0) / totalValue;

                    if (fraction <= 0)
                        continue;

                    double span = fraction * 360;
                    float angle = (float)(currentAngle + span * 0.5) * Mathf.Deg2Rad;
                    float labelRadius = (inner + radius) * 0.5f;
                    Vector2 position = visibleSlices.Count == 1 && inner == 0
                        ? Vector2.zero
                        : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * labelRadius;
                    currentAngle += span;

                    if (fraction * 100 < Mathf.Clamp(Finite(minimumLabelPercentage), 0, 100))
                        continue;

                    string text = showPercentages ? Percentage((float)(fraction * 100)) : FormatValue(dataPoints[i].value);
                    Badge(labels, IndexedName("Label ", i), text, position, GetFont(labelFont), labelFontSize,
                        Styler.GetColor(styleProvider, labelColor, stylerPreset), drawLabelBackground,
                        Styler.GetColor(styleProvider, labelBackgroundColor, stylerPreset),
                        labelPadding, labelCornerRadius, labelShape);
                }
            }

            if (showLegend && CanUseLegendContainer())
                DrawLegend();
        }
        #endregion

        #region Slice Rendering
        PieChartSlice Slice(Transform parent, string name, float start, float span, float outer, float inner, Color color, Color previous, Color next)
        {
            float inset = borderPlacement == BorderPlacement.Inside ? 1 : borderPlacement == BorderPlacement.Center ? 0.5f : 0;
            float maxStroke = inset > 0 ? (outer - inner) / (inset * (inner > 0 ? 2 : 1)) : outer;

            if (inner > 0 && inset < 1)
                maxStroke = Mathf.Min(maxStroke, inner / (1 - inset));

            float stroke = drawBorder ? Mathf.Min(Positive(borderWidth), maxStroke) : 0;
            var rt = Element(parent, name, typeof(PieChartSlice));
            Layout(rt, Center, Center, Center, Vector2.zero, (outer + stroke * (1 - inset)) * 2 * Vector2.one);
            var slice = rt.GetComponent<PieChartSlice>();
            slice.SetData(start, span, outer, inner,
                Mathf.Max(1, Mathf.CeilToInt(Mathf.Clamp(segments, 3, 512) * span / 360)), color,
                stroke, Styler.GetColor(styleProvider, borderColor, stylerPreset), inset,
                Mathf.Clamp(Finite(edgeSmoothing), 0, 3), previous, next, Positive(sliceSpacing));

            return slice;
        }
        #endregion

        #region Pointer Interaction
        protected override int PointCount => dataPoints?.Count ?? 0;

        protected override ChartBase.DataPoint PointAt(int index) => dataPoints[index];

        // Matches the slice labels: a percentage of the total, or the formatted value
        protected override string PointValueText(int index)
        {
            float value = Positive(dataPoints[index]?.value ?? 0);
            return showPercentages && totalValue > 0 ? Percentage((float)(value / totalValue * 100)) : FormatValue(dataPoints[index]?.value ?? 0);
        }

        protected override int HitPoint(Vector2 point)
        {
            point -= RectTransform.rect.size * 0.5f;
            float distance = point.magnitude;

            if (arcs.Count == 0 || distance < innerRadius)
                return -1;

            float angle = Mathf.Repeat(Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg, 360);

            for (int i = 0; i < arcs.Count; i++)
            {
                var arc = arcs[i];
                if (distance <= radius + HitMargin(arc.index) && Mathf.Repeat(angle - arc.start, 360) < arc.span)
                    return arc.index;
            }

            return -1;
        }

        /// <summary>
        /// Angles in degrees of a drawn slice, counterclockwise from the right.
        /// </summary>
        struct SliceArc
        {
            public int index;
            public float start, span;
        }
        #endregion

        #region Legend Rendering
        bool CanUseLegendContainer()
        {
            if (legendContainer == null || legendContainer.gameObject.scene != gameObject.scene)
                return false;

            for (Transform item = legendContainer; item != null && item != transform; item = item.parent)
            {
                if (NameOf(item).EndsWith(GenSuffix))
                    return false;
            }

            return true;
        }

        string Percentage(float value) => FormatNumber(value, PercentageFormats[Mathf.Clamp(percentageDecimals, 0, 4)], "%");

        string GetLegendName()
        {
            // The name follows the instance, so it is built once
            if (legendName != null)
                return legendName;

#if UNITY_6000_3_OR_NEWER
            var id = GetEntityId();
#else
            var id = GetInstanceID();
#endif
            return legendName = "Pie Legend " + id + " " + GenSuffix;
        }

        void DrawLegend()
        {
            string name = legendRoot != null && legendRoot.parent == legendContainer ? NameOf(legendRoot) : GetLegendName();
            var root = Element(legendContainer, name);

            Stretch(root);
            ClaimLegend(root);

            // Layout groups on the supplied container should treat this as a single overlay root
            if (!root.TryGetComponent<LayoutElement>(out var layout))
                layout = EnsureComponent<LayoutElement>(root.gameObject);
            if (!layout.ignoreLayout)
                layout.ignoreLayout = true;

            float height = Positive(legendItemHeight);
            float boxSize = Positive(legendColorBoxSize);
            int visibleCount = 0;

            foreach (var point in dataPoints)
            {
                if (!hideZeroValueLegendItems || Positive(point?.value ?? 0) > 0)
                    visibleCount++;
            }

            var font = GetFont(legendFont);
            Color textColor = Styler.GetColor(styleProvider, legendTextColor, stylerPreset);
            float startY = visibleCount * height * 0.5f;
            int row = 0;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (hideZeroValueLegendItems && Positive(dataPoints[i]?.value ?? 0) <= 0)
                    continue;

                var item = Element(root, IndexedName("Item ", i));
                Layout(item, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center, new Vector2(0, startY - (row++ + 0.5f) * height), new Vector2(0, height));

                var box = Shape(item, "Color Box", dataPoints[i]?.color ?? Color.clear, legendCornerRadius, settings: legendShape);
                Layout(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), Vector2.one * boxSize);

                float percentage = totalValue > 0 ? (float)(Positive(dataPoints[i]?.value ?? 0) / totalValue * 100) : 0;
                var label = Text(item, "Label", LegendText(i, dataPoints[i], percentage), font, legendFontSize, textColor, TextAlignmentOptions.MidlineLeft);
                float inset = boxSize + 12;
                Layout(label.rectTransform, Vector2.zero, Vector2.one, Center, new Vector2(inset * 0.5f, 0), new Vector2(-inset, 0));
            }
        }

        string LegendText(int index, DataPoint point, float percentage)
        {
            string label = point?.label ?? "";
            string value = legendValues is LegendValueMode.Value or LegendValueMode.ValueAndPercentage ? FormatValue(point?.value ?? 0) : null;
            string share = legendValues is LegendValueMode.Value or LegendValueMode.LabelOnly ? null : Percentage(percentage);

            // Rows keep their text while its parts are unchanged, so redraws don't allocate
            while (legendRows.Count <= index)
                legendRows.Add(default);

            var row = legendRows[index];
            if (row.text != null && row.label == label && row.value == value && row.share == share)
                return row.text;

            row.label = label;
            row.value = value;
            row.share = share;
            row.text = legendValues switch
            {
                LegendValueMode.LabelOnly => label,
                LegendValueMode.Value => label + " (" + value + ")",
                LegendValueMode.ValueAndPercentage => label + " (" + value + " / " + share + ")",
                _ => label + " (" + share + ")"
            };

            legendRows[index] = row;
            return row.text;
        }

        struct LegendRow
        {
            public string label, value, share, text;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Hole radius as a fraction of the outer radius, from 0 to 0.95. Zero draws a solid pie.
        /// </summary>
        public float InnerRadiusRatio
        {
            get => innerRadiusRatio;
            set => SetValue(ref innerRadiusRatio, Mathf.Clamp(value, 0f, 0.95f));
        }

        /// <summary>
        /// Angle of the first slice in degrees counterclockwise from the right. Wraps at 360.
        /// </summary>
        public float StartAngle
        {
            get => startAngle;
            set => SetValue(ref startAngle, Mathf.Repeat(value, 360));
        }

        /// <summary>
        /// Minimum tessellation detail, from 3 to 512.
        /// </summary>
        public int Segments
        {
            get => segments;
            set => SetValue(ref segments, Mathf.Clamp(value, 3, 512));
        }

        /// <summary>
        /// Decimal places in slice and legend percentages, from 0 to 4.
        /// </summary>
        public int PercentageDecimals
        {
            get => percentageDecimals;
            set => SetValue(ref percentageDecimals, Mathf.Clamp(value, 0, 4));
        }

        /// <summary>
        /// Gap between adjacent slices in local UI units. The gap keeps the same width from the center outward.
        /// </summary>
        public float SliceSpacing
        {
            get => sliceSpacing;
            set => SetValue(ref sliceSpacing, Mathf.Max(0, value));
        }

        /// <summary>
        /// Smoothing width in screen pixels for the silhouette, outline and slice edges, from 0 to 3.
        /// </summary>
        public float EdgeSmoothing
        {
            get => edgeSmoothing;
            set => SetValue(ref edgeSmoothing, Mathf.Clamp(value, 0f, 3f));
        }

        /// <summary>
        /// Draw an outline along the inner and outer circumference.
        /// </summary>
        public bool DrawBorder
        {
            get => drawBorder;
            set => SetValue(ref drawBorder, value);
        }

        /// <summary>
        /// Outline width in local UI units. Clamped to the available thickness.
        /// </summary>
        public float BorderWidth
        {
            get => borderWidth;
            set => SetValue(ref borderWidth, Mathf.Max(0, value));
        }

        /// <summary>
        /// Place the outline inside, centered on, or outside the radii.
        /// </summary>
        public BorderPlacement BorderAlignment
        {
            get => borderPlacement;
            set => SetValue(ref borderPlacement, value);
        }

        /// <summary>
        /// Display chart labels.
        /// </summary>
        public bool ShowLabels
        {
            get => showLabels;
            set => SetValue(ref showLabels, value);
        }

        /// <summary>
        /// Show percentages on slices. Disable to show formatted values.
        /// </summary>
        public bool ShowPercentages
        {
            get => showPercentages;
            set => SetValue(ref showPercentages, value);
        }

        /// <summary>
        /// Hide labels below this percentage, from 0 to 100.
        /// </summary>
        public float MinimumLabelPercentage
        {
            get => minimumLabelPercentage;
            set => SetValue(ref minimumLabelPercentage, Mathf.Clamp(value, 0f, 100f));
        }

        /// <summary>
        /// Slice label font size.
        /// </summary>
        public float LabelFontSize
        {
            get => labelFontSize;
            set => SetValue(ref labelFontSize, Mathf.Max(0, value));
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
        /// Display a legend in the assigned container.
        /// </summary>
        public bool ShowLegend
        {
            get => showLegend;
            set => SetValue(ref showLegend, value);
        }

        /// <summary>
        /// Container for this chart's generated legend.
        /// </summary>
        public RectTransform LegendContainer
        {
            get => legendContainer;
            set => SetValue(ref legendContainer, value);
        }

        /// <summary>
        /// Show percentages, values, both, or category names only in the legend.
        /// </summary>
        public LegendValueMode LegendValues
        {
            get => legendValues;
            set => SetValue(ref legendValues, value);
        }

        /// <summary>
        /// Omit legend rows whose rendered value is zero.
        /// </summary>
        public bool HideZeroValueLegendItems
        {
            get => hideZeroValueLegendItems;
            set => SetValue(ref hideZeroValueLegendItems, value);
        }

        /// <summary>
        /// Font size used by legend labels.
        /// </summary>
        public float LegendFontSize
        {
            get => legendFontSize;
            set => SetValue(ref legendFontSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Height of each legend row in local UI units.
        /// </summary>
        public float LegendItemHeight
        {
            get => legendItemHeight;
            set => SetValue(ref legendItemHeight, Mathf.Max(0, value));
        }

        /// <summary>
        /// Side length of each legend color swatch in local UI units.
        /// </summary>
        public float LegendColorBoxSize
        {
            get => legendColorBoxSize;
            set => SetValue(ref legendColorBoxSize, Mathf.Max(0, value));
        }

        /// <summary>
        /// Uniform swatch corner radius, in pixels or percent according to Legend Shape radius mode.
        /// </summary>
        public float LegendCornerRadius
        {
            get => legendCornerRadius;
            set => SetValue(ref legendCornerRadius, Mathf.Max(0, value));
        }

        /// <summary>
        /// Set a proportional donut hole and schedule a refresh.
        /// </summary>
        public void SetInnerRadiusRatio(float ratio) => InnerRadiusRatio = ratio;

        /// <summary>
        /// Replace the data list and schedule a refresh.
        /// The list is retained by reference; null clears the chart.
        /// </summary>
        public void UpdateData(List<DataPoint> newData) => UpdatePoints(ref dataPoints, newData);

        /// <summary>
        /// Append a data point and schedule a refresh.
        /// Null point references are ignored.
        /// </summary>
        public void AddDataPoint(string label, float value, Color color) => AddPoint(ref dataPoints, new DataPoint(label, value, color));

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
        public void SetDataPoint(int index, string label, float value, Color? color = null)
        {
            SetPointAt(dataPoints, index, label, value, static () => new DataPoint(), color);
        }

        /// <summary>
        /// Update the specified data point and schedule a refresh.
        /// Invalid indices and missing references are ignored.
        /// </summary>
        public void SetDataPoint(DataPoint data, string label, float value, Color? color = null) => SetPoint(data, label, value, color);
        #endregion

#if EVO_LOCALIZATION
        protected override void LocalizeLabels() => LocalizePoints(dataPoints);
#endif
    }
}