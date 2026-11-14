using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.Charts;

namespace Gear.Tests
{
    /// <summary>折线/柱状图的数据归一化与柱体几何测试。</summary>
    public class GearChartTests
    {
        private GameObject lineGo;
        private GameObject barGo;

        [TearDown]
        public void TearDown()
        {
            if (lineGo != null) Object.DestroyImmediate(lineGo);
            if (barGo != null) Object.DestroyImmediate(barGo);
        }

        [Test]
        public void LineChart_NormalizesEndpoints()
        {
            lineGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearLineChart));
            RectTransform rect = (RectTransform)lineGo.transform;
            rect.sizeDelta = new Vector2(200f, 100f);

            GearLineChart chart = lineGo.GetComponent<GearLineChart>();
            chart.SetData(new[] { 0f, 5f, 10f });

            Assert.AreEqual(3, chart.NormalizedPoints.Count);
            Assert.AreEqual(0f, chart.NormalizedPoints[0].x, 0.001f);
            Assert.AreEqual(0f, chart.NormalizedPoints[0].y, 0.001f);
            Assert.AreEqual(1f, chart.NormalizedPoints[2].x, 0.001f);
            Assert.AreEqual(1f, chart.NormalizedPoints[2].y, 0.001f);
        }

        [Test]
        public void BarChart_BuildsOneRectPerValue()
        {
            barGo = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearBarChart));
            RectTransform rect = (RectTransform)barGo.transform;
            rect.sizeDelta = new Vector2(200f, 100f);

            GearBarChart chart = barGo.GetComponent<GearBarChart>();
            chart.SetData(new[] { 0f, 1f, 2f });

            Assert.AreEqual(3, chart.BarRects.Count);
            Assert.AreEqual(0f, chart.BarRects[0].height, 0.001f);
            Assert.Greater(chart.BarRects[2].height, chart.BarRects[1].height);
        }

        [Test]
        public void RadarChart_BuildsAxes()
        {
            GameObject radarGo = new GameObject("Radar", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearRadarChart));
            ((RectTransform)radarGo.transform).sizeDelta = new Vector2(120f, 120f);
            GearRadarChart chart = radarGo.GetComponent<GearRadarChart>();
            chart.SetData(new[] { 1f, 0.5f, 0.5f, 0.5f, 0.5f });

            Assert.AreEqual(5, chart.AxisCount);
            Assert.AreEqual(0f, chart.NormalizedPoints[0].x, 0.001f);
            Assert.AreEqual(1f, chart.NormalizedPoints[0].y, 0.001f);

            Object.DestroyImmediate(radarGo);
        }

        [Test]
        public void PieChart_BuildsSlicesAndLegend()
        {            GameObject pieGo = new GameObject("Pie", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearPieChart));
            ((RectTransform)pieGo.transform).sizeDelta = new Vector2(120f, 120f);
            GearPieChart pie = pieGo.GetComponent<GearPieChart>();
            pie.SetData(new[] { 1f, 1f, 1f });
            Assert.AreEqual(3, pie.SliceCount);
            Assert.Greater(pie.SliceSweeps[0], 0f);
            Assert.AreEqual(0, pie.GetSliceIndexAtLocalPoint(new Vector2(0f, 30f)));

            GameObject legendGo = new GameObject("Legend", typeof(RectTransform), typeof(GearChartLegend));
            TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(legendGo.transform, false);
            Image swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            swatch.transform.SetParent(legendGo.transform, false);

            GearChartLegend legend = legendGo.GetComponent<GearChartLegend>();
            legend.Configure(new[] { label }, new Graphic[] { swatch });
            legend.SetEntries(new[] { "A" }, new[] { Color.red });
            Assert.AreEqual("A", label.text);
            Assert.AreEqual(Color.red, swatch.color);

            Object.DestroyImmediate(pieGo);
            Object.DestroyImmediate(legendGo);
        }
    }
}
