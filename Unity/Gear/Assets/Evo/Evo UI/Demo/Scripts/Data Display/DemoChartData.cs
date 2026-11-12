using System;
using System.Collections.Generic;
using Evo.Tween;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Switches any chart between datasets, easing each value to its new target.
    /// Categories are the chart's own data points, read once in reading order. The chart only redraws while values move.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoChartData : MonoBehaviour
    {
        [Serializable]
        public class Dataset
        {
            public float[] values;
        }

        [SerializeField] private ChartBase chart;
        [SerializeField] private Dataset[] datasets;
        [SerializeField, Min(0)] private float duration = 0.45f;

        const int TweenKey = 0x44434844; // DCHD

        IReadOnlyList<ChartBase.DataPoint> points;
        bool bottomUp;
        float[] from, to, current;
        TweenHandle tween;

        void Initialize()
        {
            if (points != null)
                return;

            points = chart switch
            {
                VerticalChart vertical => vertical.dataPoints,
                HorizontalChart horizontal => horizontal.dataPoints,
                LineChart line => line.dataPoints,
                PieChart pie => pie.dataPoints,
                RadarChart radar => radar.dataPoints,
                _ => throw new NotSupportedException(chart.GetType().Name)
            };

            // Horizontal Chart draws its first point at the bottom, so reading order runs backwards through its points
            bottomUp = chart is HorizontalChart;

            int count = points.Count;
            from = new float[count];
            to = new float[count];
            current = new float[count];

            for (int i = 0; i < count; i++)
                current[i] = PointAt(i).value;
        }

        // Option event: the dataset index
        public void SetDataset(int index)
        {
            Initialize();

            float[] target = datasets[index].values;

            for (int i = 0; i < current.Length; i++)
            {
                from[i] = current[i];
                to[i] = target[i];
            }

            tween.Kill();
            tween = EvoTween.Create(0f, 1f, duration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (data, t) => data.Apply(t), TweenKey);
        }

        void Apply(float t)
        {
            for (int i = 0; i < current.Length; i++)
            {
                current[i] = Mathf.LerpUnclamped(from[i], to[i], t);
                PointAt(i).value = current[i];
            }

            chart.MarkDirty();
        }

        ChartBase.DataPoint PointAt(int index) => points[bottomUp ? points.Count - 1 - index : index];
    }
}
