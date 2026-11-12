using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Adaptive pie/donut mesh. Fill and outline share a mesh with no transparent seams.
    /// </summary>
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public class PieChartSlice : MaskableGraphic
    {
        // Geometry
        [Tooltip("Angle of the first slice in degrees counterclockwise from the right. Values wrap at 360 degrees.")]
        public float startAngle;
        [Tooltip("Angular extent of this pie slice in degrees, clamped between 0 and 360.")]
        public float angleSpan = 90;
        [Tooltip("Outer pie slice radius in local UI units.")]
        public float outerRadius = 100;
        [Tooltip("Hole radius in local UI units, clamped between zero and Outer Radius.")]
        public float innerRadius;
        [Tooltip("Minimum tessellation detail. Pie charts distribute this across the circle and increase detail for smooth screen edges.")]
        public int segments = 20;

        // Outline
        [SerializeField, Tooltip("Procedural outline width in local UI units.")]
        private float outlineWidth;
        [SerializeField, Tooltip("Procedural outline color, including opacity.")]
        private Color outlineColor;
        [SerializeField] private float outlineInset = 1;

        // Edge Smoothing
        [SerializeField] private float edgeSmoothing = 1;
        [SerializeField] private Color previousColor;
        [SerializeField] private Color nextColor;
        [SerializeField] private bool blendSliceEdges;

        // Spacing
        [SerializeField] private float spacing;

        // Runtime State
        readonly float[] radii = new float[12];
        readonly Color[] tints = new Color[12];
        readonly float[] fillWeights = new float[12];
        readonly float[] coverage = new float[12];
        readonly float[] edgeAngles = new float[12];
        readonly float[] innerAngles = new float[12];
        int ringCount;

        // Runtime State
        float pixelWidth;

        /// <summary>
        /// Update this generated graphic and invalidate its mesh when necessary.
        /// </summary>
        public void SetData(float start, float span, float outer, float inner, int detail, Color tint, float strokeWidth = 0,
            Color? strokeColor = null, float strokeInset = 1, float smoothing = 1, Color? previous = null, Color? next = null, float gap = 0)
        {
            start = Mathf.Repeat(Finite(start), 360);
            span = Mathf.Clamp(Finite(span), 0, 360);
            outer = Mathf.Max(0, Finite(outer));
            inner = Mathf.Clamp(Finite(inner), 0, outer);
            detail = Mathf.Clamp(detail, 1, 4096);
            strokeWidth = Mathf.Max(0, Finite(strokeWidth));
            strokeInset = Mathf.Clamp01(Finite(strokeInset));
            smoothing = Mathf.Clamp(Finite(smoothing), 0, 3);
            gap = Mathf.Max(0, Finite(gap));

            Color stroke = strokeColor ?? Color.clear;
            float pixel = ChartMesh.PixelWidth(rectTransform, canvas);
            bool blend = previous.HasValue && next.HasValue && span < 360;

            if (startAngle != start || angleSpan != span || outerRadius != outer || innerRadius != inner || segments != detail
                || pixelWidth != pixel || outlineWidth != strokeWidth || outlineColor != stroke || outlineInset != strokeInset
                || edgeSmoothing != smoothing || blendSliceEdges != blend || previousColor != previous.GetValueOrDefault()
                || nextColor != next.GetValueOrDefault() || spacing != gap)
            {
                startAngle = start;
                angleSpan = span;
                outerRadius = outer;
                innerRadius = inner;
                segments = detail;
                pixelWidth = pixel;
                outlineWidth = strokeWidth;
                outlineColor = stroke;
                outlineInset = strokeInset;
                edgeSmoothing = smoothing;
                previousColor = previous.GetValueOrDefault();
                nextColor = next.GetValueOrDefault();
                blendSliceEdges = blend;
                spacing = gap;
                SetVerticesDirty();
            }

            if (color != tint)
                color = tint;

            if (raycastTarget)
                raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float outer = Mathf.Max(0, Finite(outerRadius));
            float inner = Mathf.Clamp(Finite(innerRadius), 0, outer);
            float span = Mathf.Clamp(Finite(angleSpan), 0, 360);

            if (outer <= inner || span <= 0)
                return;

            float inset = Mathf.Clamp01(Finite(outlineInset));
            float stroke = Mathf.Max(0, Finite(outlineWidth));
            float maxStroke = inset > 0 ? (outer - inner) / (inset * (inner > 0 ? 2 : 1)) : outer;

            if (inner > 0 && inset < 1)
                maxStroke = Mathf.Min(maxStroke, inner / (1 - inset));

            stroke = Mathf.Min(stroke, maxStroke);

            float low = inner > 0 ? Mathf.Max(0, inner - stroke * (1 - inset)) : 0;
            float high = outer + stroke * (1 - inset);
            float fillLow = inner > 0 ? inner + stroke * inset : 0;
            float fillHigh = outer - stroke * inset;

            pixelWidth = ChartMesh.PixelWidth(rectTransform, canvas);
            ringCount = 0;

            float feather = Mathf.Min(pixelWidth * Mathf.Clamp(Finite(edgeSmoothing), 0, 3), (high - low) * 0.5f);

            // Spacing moves both straight edges half the gap inward, parallel to the slice boundary,
            // so the gap keeps one width from the center outward. Smoothing straddles each edge line.
            float halfGap = span < 360 ? Mathf.Max(0, Finite(spacing)) * 0.5f : 0;
            float clearEdge = halfGap - feather * 0.5f;
            float solidEdge = halfGap + feather * 0.5f;
            float depth = Mathf.Sin(Mathf.Min(span, 180) * 0.5f * Mathf.Deg2Rad);
            float apex = halfGap > 0 ? Mathf.Max(0, clearEdge) / depth : 0;

            // A slice narrower than the gap disappears into it
            if (apex >= high)
                return;

            float visibleLow = Mathf.Max(low, apex);

            if (feather > 0)
            {
                // Include both sides of every coverage/color transition.
                // The old mesh feathered only the silhouette; the fill/outline boundary stayed jagged.
                Radius(low, visibleLow, high);
                Radius(high, visibleLow, high);

                if (low > 0)
                    Radius(low + feather, visibleLow, high);

                Radius(high - feather, visibleLow, high);
                float transition = Mathf.Min(feather, stroke);

                if (stroke > 0)
                {
                    if (inner > 0)
                    {
                        Radius(fillLow - transition * 0.5f, visibleLow, high);
                        Radius(fillLow + transition * 0.5f, visibleLow, high);
                    }

                    Radius(fillHigh - transition * 0.5f, visibleLow, high);
                    Radius(fillHigh + transition * 0.5f, visibleLow, high);
                }

                // Rings where the edge lines meet keep the tip of a spaced slice sharp
                if (halfGap > 0)
                {
                    Radius(apex, visibleLow, high);
                    Radius(solidEdge / depth, visibleLow, high);
                }

                for (int j = 0; j < ringCount; j++)
                {
                    float r = radii[j];
                    float weight = stroke <= 0 ? 1 : fillHigh <= fillLow
                        ? 0
                        : Mathf.Clamp01((fillHigh + transition * 0.5f - r) / transition);

                    if (stroke > 0 && inner > 0)
                        weight = Mathf.Min(weight, Mathf.Clamp01((r - fillLow + transition * 0.5f) / transition));

                    fillWeights[j] = weight;
                    coverage[j] = Mathf.Clamp01(Mathf.Min(low > 0 ? (r - low) / feather : 1, (high - r) / feather));
                    tints[j] = SurfaceColor(j, color);
                }
            }
            else
            {
                if (inner > 0 && stroke > 0)
                    Band(Mathf.Max(low, visibleLow), fillLow, outlineColor);
                if (fillHigh > fillLow)
                    Band(Mathf.Max(fillLow, visibleLow), fillHigh, color);
                if (stroke > 0)
                    Band(Mathf.Max(fillHigh, visibleLow), high, outlineColor);
            }

            if (ringCount < 2)
                return;

            bool gapEdges = halfGap > 0 && feather > 0;
            bool blendEdges = halfGap <= 0 && blendSliceEdges && feather > 0;

            // Per-ring edge angles. Spaced edges are straight lines, so each ring meets them at its own angle.
            if (halfGap > 0 || blendEdges)
            {
                float deepest = Mathf.Min(span, 180) * 0.5f;

                for (int j = 0; j < ringCount; j++)
                {
                    float r = radii[j];

                    if (halfGap > 0)
                    {
                        edgeAngles[j] = EdgeAngle(clearEdge, r, deepest);
                        innerAngles[j] = EdgeAngle(solidEdge, r, deepest);

                        // Near the tip the slice is narrower than the smoothing, so it fades out
                        if (feather > 0)
                            tints[j].a *= Mathf.Clamp01((r * depth - clearEdge) / feather);
                    }
                    else
                    {
                        edgeAngles[j] = 0;
                        innerAngles[j] = Mathf.Min(span * 0.5f, feather * 0.5f / Mathf.Max(feather, r) * Mathf.Rad2Deg);
                    }
                }
            }

            // Chord error <= 0.1 screen pixel; the vertex count stays below uGUI's limit
            float detailPixel = ChartMesh.PixelWidth(rectTransform, canvas, finestAxis: true);
            float step = 2 * Mathf.Acos(Mathf.Clamp(1 - detailPixel * 0.1f / high, -1, 1)) * Mathf.Rad2Deg;
            int count = Mathf.Clamp(Mathf.Max(segments, Mathf.CeilToInt(span / Mathf.Max(0.01f, step))), 1, 4096);
            float start = Mathf.Repeat(Finite(startAngle), 360);
            bool edgeColumns = gapEdges || blendEdges;
            int columns = count + (edgeColumns ? 2 : 0);

            for (int i = 0; i <= columns; i++)
            {
                for (int j = 0; j < ringCount; j++)
                {
                    float offset = span * i / columns;
                    Color tint = tints[j];

                    if (edgeColumns)
                    {
                        // Outer columns carry the edge: blended toward the neighbor, or transparent beside a gap
                        float edge = edgeAngles[j];
                        float body = innerAngles[j];
                        offset = i == 0 ? edge : i == columns ? span - edge : Mathf.Lerp(body, span - body, (float)(i - 1) / count);

                        if (i == 0 || i == columns)
                        {
                            if (blendEdges)
                                tint = SurfaceColor(j, Color.Lerp(color, i == 0 ? previousColor : nextColor, 0.5f));
                            else
                                tint.a = 0;
                        }
                    }
                    else if (halfGap > 0)
                        offset = Mathf.Lerp(innerAngles[j], span - innerAngles[j], (float)i / count);

                    float angle = (start + offset) * Mathf.Deg2Rad;
                    Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    vh.AddVert(direction * radii[j], tint, Vector2.zero);
                }

                if (i == 0)
                    continue;

                int a = (i - 1) * ringCount;
                int b = i * ringCount;

                for (int j = 0; j < ringCount - 1; j++)
                {
                    if (radii[j + 1] > radii[j])
                        ChartMesh.Quad(vh, a + j, a + j + 1, b + j + 1, b + j);
                }
            }
        }

        void Radius(float radius, float low, float high)
        {
            radius = Mathf.Clamp(radius, low, high);

            // Sorted insert, array sorting allocates in Unity's Mono
            int index = ringCount;
            while (index > 0 && radii[index - 1] > radius)
                index--;

            if (index > 0 && radii[index - 1] == radius)
                return;

            for (int i = ringCount; i > index; i--)
                radii[i] = radii[i - 1];

            radii[index] = radius;
            ringCount++;
        }

        // Angle from the slice boundary where a ring of this radius crosses a line parallel to it
        static float EdgeAngle(float distance, float radius, float deepest)
        {
            if (radius <= 0)
                return 0;

            return Mathf.Min(Mathf.Asin(Mathf.Clamp(distance / radius, -1, 1)) * Mathf.Rad2Deg, deepest);
        }

        Color SurfaceColor(int ring, Color fill)
        {
            Color tint = Color.Lerp(outlineColor, fill, fillWeights[ring]);
            tint.a *= coverage[ring];
            return tint;
        }

        void Band(float start, float end, Color tint)
        {
            if (end <= start)
                return;

            radii[ringCount] = start;
            tints[ringCount++] = tint;
            radii[ringCount] = end;
            tints[ringCount++] = tint;
        }

        static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;
    }
}