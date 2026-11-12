using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Star-shaped radar fill with a transparent perimeter fringe; no custom shader required.
    /// </summary>
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public class RadarChartGraphic : MaskableGraphic
    {
        // Constants
        internal const int MaxSegments = 16000;

        // Mesh Data
        [Tooltip("Polygon vertices in local UI coordinates, ordered counterclockwise.")]
        public List<Vector2> points = new();
        [Tooltip("Local-space origin of the radar triangle fan.")]
        public Vector2 center;

        // Chunk State
        [SerializeField, HideInInspector] private bool openChunk;
        [SerializeField, HideInInspector] private Vector2 before;
        [SerializeField, HideInInspector] private Vector2 after;

        // Runtime State
        float pixelWidth;

        /// <summary>
        /// Update this generated graphic and invalidate its mesh when necessary.
        /// </summary>
        public void SetData(List<Vector2> value, Vector2 origin, Color tint)
        {
            if (value != null && value.Count > MaxSegments)
                throw new System.ArgumentException("Use RadarChart to split fills larger than 16000 points into meshes.", nameof(value));

            SetData(value, 0, value?.Count ?? 0, origin, tint, false);
        }

        internal void SetData(List<Vector2> value, int start, int count, Vector2 origin, Color tint, bool chunk)
        {
            float pixel = ChartMesh.PixelWidth(rectTransform, canvas);
            Vector2 previous = count > 0 ? value[(start + value.Count - 1) % value.Count] : origin;
            Vector2 next = count > 0 ? value[(start + count) % value.Count] : origin;
            bool changed = points == null || points.Count != count || center != origin || pixelWidth != pixel
                || openChunk != chunk || before != previous || after != next || ReferenceEquals(points, value);

            if (!changed)
            {
                for (int i = 0; i < count; i++)
                {
                    if (points[i] != value[(start + i) % value.Count])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                points ??= new();

                if (!ReferenceEquals(points, value))
                {
                    points.Clear();

                    for (int i = 0; i < count; i++)
                        points.Add(value[(start + i) % value.Count]);
                }

                openChunk = chunk;
                before = previous;
                after = next;
                center = origin;
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

            if (points == null || points.Count < (openChunk ? 2 : 3))
                return;

            pixelWidth = ChartMesh.PixelWidth(rectTransform, canvas);

            Color clear = color;
            clear.a = 0;
            vh.AddVert(center, color, Vector2.zero);

            int count = Mathf.Min(points.Count, openChunk ? MaxSegments + 1 : MaxSegments);
            for (int i = 0; i < count; i++)
            {
                Vector2 point = points[i];
                Vector2 incoming = point - (openChunk && i == 0 ? before : points[(i + count - 1) % count]);
                Vector2 outgoing = (openChunk && i == count - 1 ? after : points[(i + 1) % count]) - point;
                Vector2 normalBefore = new Vector2(incoming.y, -incoming.x).normalized;
                Vector2 normalAfter = new Vector2(outgoing.y, -outgoing.x).normalized;
                Vector2 miter = (normalBefore + normalAfter).normalized;
                float width = Mathf.Min(pixelWidth, Mathf.Min(incoming.magnitude, outgoing.magnitude) * 0.25f);
                float length = Mathf.Min(width / Mathf.Max(0.25f, Vector2.Dot(miter, normalAfter)), width * 2);
                vh.AddVert(point, color, Vector2.zero);
                vh.AddVert(point + miter * length, clear, Vector2.zero);
            }

            for (int i = 0; i < (openChunk ? count - 1 : count); i++)
            {
                int a = 1 + i * 2;
                int b = 1 + ((i + 1) % count) * 2;
                vh.AddTriangle(0, a, b);
                ChartMesh.Quad(vh, a, a + 1, b + 1, b);
            }
        }
    }

    /// <summary>
    /// Shared mesh math for screen-space antialiasing and quad construction.
    /// </summary>
    internal static class ChartMesh
    {
        /// <summary>
        /// Returns the local-space width of one screen pixel for the supplied UI transform.
        /// </summary>
        internal static float PixelWidth(RectTransform rect, Canvas canvas, bool finestAxis = false)
        {
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 origin = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
            float x = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.right)));
            float y = Vector2.Distance(origin, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up)));
            return 1 / Mathf.Max(0.001f, (finestAxis ? Mathf.Max(x, y) : Mathf.Min(x, y)));
        }

        internal static void Quad(VertexHelper vh, int a, int b, int c, int d)
        {
            vh.AddTriangle(a, b, c);
            vh.AddTriangle(a, c, d);
        }
    }
}