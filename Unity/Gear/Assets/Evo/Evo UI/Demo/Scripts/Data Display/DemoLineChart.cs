using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Line Chart tab: the curve choice. The other options set the chart's properties directly.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoLineChart : MonoBehaviour
    {
        [SerializeField] private LineChart chart;

        public void SetCurve(int index) => chart.Curve = (LineChart.CurveMode)index;
    }
}
