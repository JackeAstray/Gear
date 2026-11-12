using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Radar Chart tab: outline and grid switches that lock their bars, and the whole-number grid levels.
    /// The other options set the chart's properties directly.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRadarChart : MonoBehaviour
    {
        [SerializeField] private RadarChart chart;

        [Header("Options")]
        [SerializeField] private CanvasGroup thicknessOption;
        [SerializeField] private CanvasGroup levelsOption;

        void Awake()
        {
            DemoUtils.SetLocked(thicknessOption, !chart.ShowLine, false);
            DemoUtils.SetLocked(levelsOption, !chart.ShowGrid, false);
        }

        public void SetOutline(bool value)
        {
            chart.ShowLine = value;
            DemoUtils.SetLocked(thicknessOption, !value);
        }

        public void SetGrid(bool value)
        {
            chart.ShowGrid = value;
            DemoUtils.SetLocked(levelsOption, !value);
        }

        public void SetGridLevels(float value) => chart.GridLevels = Mathf.RoundToInt(value);
    }
}
