using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>轻量柱状图：自绘柱体，支持等宽/间距/单色；数值标签由外部 TMP 扩展。</summary>
    [AddComponentMenu("UI/Gear/Charts/Bar Chart")]
    public sealed class GearBarChart : MaskableGraphic
    {
        [SerializeField] private float[] values = { 0.35f, 0.7f, 0.5f, 0.9f };
        [SerializeField] private Color barColor = new Color(0.14f, 0.75f, 0.95f, 1f);
        [SerializeField, Min(0f)] private float spacing = 6f;

        private readonly List<Rect> bars = new List<Rect>();

        public IReadOnlyList<Rect> BarRects => bars;
        public int ValueCount => values != null ? values.Length : 0;

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

            RebuildRects();
            SetVerticesDirty();
        }

        public void SetColors(Color barsColor)
        {
            barColor = barsColor;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RebuildRects();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            RebuildRects();
            Color32 color = barColor;
            for (int i = 0; i < bars.Count; i++)
                GearChartMesh.AddQuad(vh, bars[i], color);
        }

        private void RebuildRects()
        {
            bars.Clear();
            if (values == null || values.Length == 0) return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            float max = 0f;
            for (int i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]);
            if (max <= 0f) max = 1f;

            Rect plot = new Rect(rect.xMin + 8f, rect.yMin + 8f,
                Mathf.Max(1f, rect.width - 16f), Mathf.Max(1f, rect.height - 16f));
            float count = values.Length;
            float barWidth = Mathf.Max(1f, (plot.width - spacing * (count - 1f)) / count);

            for (int i = 0; i < values.Length; i++)
            {
                float height = Mathf.Clamp01(values[i] / max) * plot.height;
                float x = plot.xMin + i * (barWidth + spacing);
                bars.Add(new Rect(x, plot.yMin, barWidth, height));
            }
        }
    }
}
