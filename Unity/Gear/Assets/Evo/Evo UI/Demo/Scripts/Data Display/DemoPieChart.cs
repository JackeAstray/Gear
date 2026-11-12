using System;
using Evo.Tween;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Pie Chart tab: label and legend switches, one value choice that both follow, and a slide with a fade that keeps the pie
    /// centered when the legend comes and goes. The value choice locks while neither shows. The other options set the chart's
    /// properties directly.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoPieChart : MonoBehaviour
    {
        [SerializeField] private PieChart chart;
        [SerializeField] private RectTransform group;
        [SerializeField] private CanvasGroup legend;
        [SerializeField] private float legendShift = 210;
        [SerializeField] private float legendDuration = 0.35f;

        [Header("Options")]
        [SerializeField] private CanvasGroup valuesOption;

        TweenHandle legendFade;
        Action hideLegend;
        bool legendShown;

        void Awake()
        {
            hideLegend = () => chart.ShowLegend = false;
            legendShown = chart.ShowLegend;
            DemoUtils.SetLocked(valuesOption, !chart.ShowLabels && !legendShown, false);
        }

        public void SetLabels(bool value)
        {
            chart.ShowLabels = value;
            DemoUtils.SetLocked(valuesOption, !value && !legendShown);
        }

        // The chart keeps the legend until it has faded out, so the switch state says whether it shows
        public void SetLegend(bool value)
        {
            legendShown = value;
            DemoUtils.SetLocked(valuesOption, !value && !chart.ShowLabels);

            // The legend fades in after the chart builds it, and the chart removes it once it has faded out
            legendFade.Kill();
            if (value)
                chart.ShowLegend = true;

            legendFade = DemoUtils.Fade(legend, value ? 1f : 0f, legendDuration);

            if (!value)
                legendFade.OnComplete(hideLegend);

            // The legend sits right of the pie: shift the pair left while it shows, so whatever is visible stays centered
            EvoTween.Create(group.anchoredPosition.x, value ? -legendShift : 0f, legendDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(group, static (rect, x) => rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y), 0x44504945); // DPIE
        }

        // Percent or Value, on the slice labels and in the legend
        public void SetValues(int index)
        {
            bool percent = index == 0;
            chart.ShowPercentages = percent;
            chart.LegendValues = percent ? PieChart.LegendValueMode.Percentage : PieChart.LegendValueMode.Value;
        }
    }
}
