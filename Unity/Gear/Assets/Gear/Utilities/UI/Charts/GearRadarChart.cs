using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>轻量雷达图：自绘网格、填充、描边与数据点；轴数由 values 长度决定。</summary>
    [AddComponentMenu("UI/Gear/Charts/Radar Chart")]
    public sealed class GearRadarChart : MaskableGraphic
    {
        [SerializeField] private float[] values = { 0.8f, 0.55f, 0.9f, 0.6f, 0.7f };
        [SerializeField, Min(0.001f)] private float maxValue = 1f;
        [SerializeField, Range(1, 8)] private int gridLevels = 4;
        [SerializeField] private Color lineColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField] private Color fillColor = new Color(0.49f, 0.36f, 0.99f, 0.22f);
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color pointColor = Color.white;
        [SerializeField, Min(0.5f)] private float lineWidth = 2f;
        [SerializeField, Min(1f)] private float pointRadius = 3f;

        private readonly List<Vector2> normalized = new List<Vector2>();

        public IReadOnlyList<Vector2> NormalizedPoints => normalized;
        public int AxisCount => normalized.Count;

        public void SetData(IList<float> data)
        {
            if (data == null)
            {
                values = null;
            }
            else
            {
                values = new float[data.Count];
                for (int i = 0; i < data.Count; i++) values[i] = data[i];
            }

            Rebuild();
            SetVerticesDirty();
        }

        public void SetColors(Color line, Color fill, Color grid, Color point)
        {
            lineColor = line;
            fillColor = fill;
            gridColor = grid;
            pointColor = point;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Rebuild();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (normalized.Count < 3) return;

            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f) return;

            Vector2 center = rect.center;
            int n = normalized.Count;

            for (int level = 1; level <= gridLevels; level++)
            {
                float scale = level / (float)gridLevels;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = center + normalized[i] * radius * scale;
                    Vector2 b = center + normalized[(i + 1) % n] * radius * scale;
                    GearChartMesh.AddLine(vh, a, b, 1f, gridColor);
                }
            }

            if (fillColor.a > 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = center + normalized[i] * radius;
                    Vector2 b = center + normalized[(i + 1) % n] * radius;
                    AddTriangle(vh, center, a, b, fillColor);
                }
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 a = center + normalized[i] * radius;
                Vector2 b = center + normalized[(i + 1) % n] * radius;
                GearChartMesh.AddLine(vh, a, b, lineWidth, lineColor);
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 point = center + normalized[i] * radius;
                GearChartMesh.AddQuad(vh, new Rect(point.x - pointRadius, point.y - pointRadius,
                    pointRadius * 2f, pointRadius * 2f), pointColor);
            }
        }

        private void Rebuild()
        {
            normalized.Clear();
            if (values == null || values.Length < 3) return;

            int n = values.Length;
            for (int i = 0; i < n; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
                float value = Mathf.Clamp01(values[i] / Mathf.Max(maxValue, 0.001f));
                normalized.Add(new Vector2(Mathf.Cos(angle) * value, Mathf.Sin(angle) * value));
            }
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.up);
            vh.AddVert(c, color, Vector2.one);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
