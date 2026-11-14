using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>
    /// 轻量折线图：自绘折线 + 数据点 + 坐标轴，适配 MaskableGraphic 的裁剪/合批。
    /// 不负责坐标文字与悬停交互，这些可由外部 TMP/事件扩展。
    /// </summary>
    [AddComponentMenu("UI/Gear/Charts/Line Chart")]
    public sealed class GearLineChart : MaskableGraphic
    {
        [SerializeField] private float[] values = { 0.2f, 0.55f, 0.35f, 0.8f, 0.6f };
        [SerializeField] private Color lineColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField] private Color pointColor = Color.white;
        [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField, Min(0.5f)] private float lineWidth = 2f;
        [SerializeField, Min(1f)] private float pointRadius = 3f;
        [SerializeField] private bool showAxis = true;

        private readonly List<Vector2> normalized = new List<Vector2>();

        public IReadOnlyList<Vector2> NormalizedPoints => normalized;
        public int ValueCount => values != null ? values.Length : 0;

        public float MinValue { get; private set; }
        public float MaxValue { get; private set; } = 1f;

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

            UpdateNormalized();
            SetVerticesDirty();
        }

        public void SetColors(Color line, Color point, Color axis)
        {
            lineColor = line;
            pointColor = point;
            axisColor = axis;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateNormalized();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            UpdateNormalized();

            Rect rect = GetPixelAdjustedRect();
            if (normalized.Count == 0 || rect.width <= 0f || rect.height <= 0f) return;

            Rect plot = new Rect(rect.xMin + 8f, rect.yMin + 8f,
                Mathf.Max(1f, rect.width - 16f), Mathf.Max(1f, rect.height - 16f));

            if (showAxis)
            {
                GearChartMesh.AddLine(vh, new Vector2(plot.xMin, plot.yMin), new Vector2(plot.xMax, plot.yMin), 1f, axisColor);
                GearChartMesh.AddLine(vh, new Vector2(plot.xMin, plot.yMin), new Vector2(plot.xMin, plot.yMax), 1f, axisColor);
            }

            Vector2 previous = ToLocal(normalized[0], plot);
            for (int i = 1; i < normalized.Count; i++)
            {
                Vector2 current = ToLocal(normalized[i], plot);
                GearChartMesh.AddLine(vh, previous, current, lineWidth, lineColor);
                previous = current;
            }

            for (int i = 0; i < normalized.Count; i++)
            {
                Vector2 point = ToLocal(normalized[i], plot);
                GearChartMesh.AddQuad(vh, new Rect(point.x - pointRadius, point.y - pointRadius,
                    pointRadius * 2f, pointRadius * 2f), pointColor);
            }
        }

        private void UpdateNormalized()
        {
            normalized.Clear();
            if (values == null || values.Length == 0) return;

            MinValue = values[0];
            MaxValue = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                MinValue = Mathf.Min(MinValue, values[i]);
                MaxValue = Mathf.Max(MaxValue, values[i]);
            }

            if (Mathf.Approximately(MaxValue, MinValue))
                MaxValue = MinValue + 1f;

            int count = values.Length;
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0.5f : i / (float)(count - 1);
                float y = Mathf.InverseLerp(MinValue, MaxValue, values[i]);
                normalized.Add(new Vector2(x, y));
            }
        }

        private static Vector2 ToLocal(Vector2 normalizedPoint, Rect plot)
        {
            return new Vector2(
                Mathf.Lerp(plot.xMin, plot.xMax, normalizedPoint.x),
                Mathf.Lerp(plot.yMin, plot.yMax, normalizedPoint.y));
        }
    }
}
