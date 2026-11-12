using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// A joined line and optional area fill, batched into a maskable UI mesh.
    /// </summary>
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public sealed class LineChartGraphic : MaskableGraphic
    {
        // Constants
        // Six vertices per sample plus two end caps, below uGUI's 65k vertex limit
        internal const int MaxPoints = 8000;
        const int MaxCapSegments = 32;

        // Mesh Data
        [SerializeField, HideInInspector] private List<Vector2> points = new();
        [SerializeField, HideInInspector] private Vector2 before;
        [SerializeField, HideInInspector] private Vector2 after;
        [SerializeField, HideInInspector] private float thickness;
        [SerializeField, HideInInspector] private bool roundStart;
        [SerializeField, HideInInspector] private bool roundEnd;
        [SerializeField, HideInInspector] private float baseline;
        [SerializeField, HideInInspector] private float plotHeight;
        [SerializeField, HideInInspector] private bool showFill;
        [SerializeField, HideInInspector] private Color fillColor;
        [SerializeField, HideInInspector] private float bottomOpacity;

        // Runtime State
        float pixelWidth;

        /// <summary>
        /// Updates a chunk of the generated line and invalidates its mesh when required.
        /// Round caps apply only where this chunk starts or ends the whole line.
        /// </summary>
        internal void SetData(List<Vector2> source, int start, int count, float width, bool roundCaps, Color tint,
            bool fill, Color areaColor, float bottom, float height, float fade)
        {
            var previous = source[Mathf.Max(0, start - 1)];
            var next = source[Mathf.Min(source.Count - 1, start + count)];
            bool capStart = roundCaps && start == 0;
            bool capEnd = roundCaps && start + count == source.Count;
            float pixel = ChartMesh.PixelWidth(rectTransform, canvas);
            bool changed = points.Count != count || before != previous || after != next
                || thickness != width || roundStart != capStart || roundEnd != capEnd
                || showFill != fill || plotHeight != height
                || fillColor != areaColor || baseline != bottom || bottomOpacity != fade
                || !Mathf.Approximately(pixelWidth, pixel);

            if (!changed)
            {
                for (int i = 0; i < count; i++)
                {
                    if (points[i] != source[start + i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                points.Clear();

                for (int i = 0; i < count; i++)
                    points.Add(source[start + i]);

                before = previous;
                after = next;
                thickness = width;
                roundStart = capStart;
                roundEnd = capEnd;
                showFill = fill;

                ColorTransition.SetColor(this, 1, fillColor, areaColor, static (target, value) =>
                {
                    var line = (LineChartGraphic)target; 
                    line.fillColor = value; 
                    line.SetVerticesDirty();
                });

                baseline = bottom;
                plotHeight = height;
                bottomOpacity = fade;
                pixelWidth = pixel;
                SetVerticesDirty();
            }

            ColorTransition.SetGraphicColor(this, tint);

            if (raycastTarget)
                raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            int count = Mathf.Min(points.Count, MaxPoints);
            if (count < 2)
                return;

            if (showFill && fillColor.a > 0)
            {
                Color bottomColor = fillColor;
                bottomColor.a *= bottomOpacity;
                float inverseHeight = 1 / Mathf.Max(0.001f, plotHeight);

                for (int i = 0; i < count; i++)
                {
                    // Evaluate one chart-wide vertical gradient. Assigning full opacity at
                    // every curve point makes neighboring triangles fade at different rates.
                    Color topColor = fillColor;
                    topColor.a *= Mathf.Lerp(bottomOpacity, 1, Mathf.Clamp01((points[i].y - baseline) * inverseHeight));
                    vh.AddVert(new Vector2(points[i].x, baseline), bottomColor, Vector2.zero);
                    vh.AddVert(points[i], topColor, Vector2.zero);

                    if (i > 0)
                        ChartMesh.Quad(vh, (i - 1) * 2, (i - 1) * 2 + 1, i * 2 + 1, i * 2);
                }
            }

            if (thickness <= 0 || color.a <= 0)
                return;

            int offset = vh.currentVertCount;
            float half = thickness * 0.5f;

            // Automatic one-screen-pixel antialiasing, independent of chart/Canvas scale
            pixelWidth = ChartMesh.PixelWidth(rectTransform, canvas);

            float fringe = Mathf.Min(half, pixelWidth);
            Color clear = color;
            clear.a = 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 incoming = points[i] - (i == 0 ? before : points[i - 1]);
                Vector2 outgoing = (i == count - 1 ? after : points[i + 1]) - points[i];

                if (incoming.sqrMagnitude < 0.0000001f)
                    incoming = outgoing;
                if (outgoing.sqrMagnitude < 0.0000001f)
                    outgoing = incoming;

                Vector2 n0 = new Vector2(-incoming.y, incoming.x).normalized;
                Vector2 n1 = new Vector2(-outgoing.y, outgoing.x).normalized;
                Vector2 miter = (n0 + n1).normalized;

                // Bounded joins prevent spikes on sharp changes and degenerate samples.
                miter /= Mathf.Max(0.5f, Vector2.Dot(miter, n1));
                vh.AddVert(points[i] - miter * (half + fringe), clear, Vector2.zero);
                vh.AddVert(points[i] - miter * half, color, Vector2.zero);
                vh.AddVert(points[i] + miter * half, color, Vector2.zero);
                vh.AddVert(points[i] + miter * (half + fringe), clear, Vector2.zero);

                if (i == 0)
                    continue;

                int a = offset + (i - 1) * 4;
                int b = offset + i * 4;

                ChartMesh.Quad(vh, a, a + 1, b + 1, b);
                ChartMesh.Quad(vh, a + 1, a + 2, b + 2, b + 1);
                ChartMesh.Quad(vh, a + 2, a + 3, b + 3, b + 2);
            }

            if (!roundStart && !roundEnd)
                return;

            // Enough segments to keep each arc within a quarter pixel of a true circle
            int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.PI * Mathf.Sqrt((half + fringe) / (pixelWidth * 2))), 2, MaxCapSegments);

            if (roundStart)
                AddCap(vh, offset, points[0], points[1] - points[0], -1, half, fringe, segments, clear);

            if (roundEnd)
                AddCap(vh, offset + (count - 1) * 4, points[count - 1], points[count - 1] - points[count - 2], 1, half, fringe, segments, clear);
        }

        // A half disc past an end sample, from its + side vertices around to its - side vertices. Sharing
        // those vertices keeps the cap seamless with the line; the solid part fans from the + side vertex.
        void AddCap(VertexHelper vh, int end, Vector2 center, Vector2 travel, float side,
            float half, float fringe, int segments, Color clear)
        {
            if (travel.sqrMagnitude < 0.0000001f)
                return;

            travel.Normalize();
            Vector2 normal = new(-travel.y, travel.x);
            Vector2 outward = travel * side;
            float stepCos = Mathf.Cos(Mathf.PI / segments);
            float stepSin = Mathf.Sin(Mathf.PI / segments);
            float cos = 1, sin = 0;
            int solid = end + 2;
            int outer = end + 3;

            for (int i = 1; i <= segments; i++)
            {
                int nextSolid = end + 1;
                int nextOuter = end;

                if (i < segments)
                {
                    (cos, sin) = (cos * stepCos - sin * stepSin, sin * stepCos + cos * stepSin);
                    Vector2 direction = normal * cos + outward * sin;
                    nextSolid = vh.currentVertCount;
                    nextOuter = nextSolid + 1;
                    vh.AddVert(center + direction * half, color, Vector2.zero);
                    vh.AddVert(center + direction * (half + fringe), clear, Vector2.zero);
                }

                if (i > 1)
                    vh.AddTriangle(end + 2, solid, nextSolid);

                ChartMesh.Quad(vh, solid, outer, nextOuter, nextSolid);
                solid = nextSolid;
                outer = nextOuter;
            }
        }
    }

    /// <summary>
    /// Builds straight or monotone cubic paths for line chart samples.
    /// </summary>
    internal static class ChartCurve
    {
        // Monotone cubic Hermite interpolation expressed as Bezier control points.
        // Harmonic-mean tangents flatten extrema and avoid invented peaks between samples.
        internal static void Build(List<Vector2> source, List<Vector2> output, bool smooth, int subdivisions)
        {
            output.Clear();

            if (source.Count == 0)
                return;

            if (!smooth)
            {
                output.AddRange(source);
                return;
            }

            output.Add(source[0]);

            for (int i = 0; i < source.Count - 1; i++)
            {
                Vector2 a = source[i];
                Vector2 b = source[i + 1];
                int steps = Mathf.Clamp(subdivisions, 2, 32);

                // Dense datasets do not need dozens of subdivisions per subpixel segment
                steps = Mathf.Min(steps, Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4)));

                float dx = b.x - a.x;
                double c1 = a.y + Tangent(source, i) * dx / 3;
                double c2 = b.y - Tangent(source, i + 1) * dx / 3;

                for (int j = 1; j < steps; j++)
                {
                    double t = (double)j / steps;
                    double u = 1 - t;
                    double y = u * u * u * a.y + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b.y;
                    output.Add(new Vector2(Mathf.Lerp(a.x, b.x, (float)t),
                        Mathf.Clamp((float)y, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y))));
                }

                output.Add(b);
            }
        }

        static double Tangent(List<Vector2> p, int i)
        {
            if (p.Count < 2)
                return 0;

            if (i == 0)
                return Slope(p[0], p[1]);

            if (i == p.Count - 1)
                return Slope(p[i - 1], p[i]);

            double a = Slope(p[i - 1], p[i]);
            double b = Slope(p[i], p[i + 1]);
            if (a == 0 || b == 0 || (a < 0) != (b < 0))
                return 0;

            double h0 = p[i].x - p[i - 1].x;
            double h1 = p[i + 1].x - p[i].x;
            double w0 = 2 * h1 + h0;
            double w1 = h1 + 2 * h0;
            return (w0 + w1) / (w0 / a + w1 / b);
        }

        static double Slope(Vector2 a, Vector2 b) => b.x > a.x ? ((double)b.y - a.y) / ((double)b.x - a.x) : 0;
    }
}