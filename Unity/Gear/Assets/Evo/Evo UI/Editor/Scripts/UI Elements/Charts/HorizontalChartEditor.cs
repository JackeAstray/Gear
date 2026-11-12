using UnityEditor;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(HorizontalChart))]
    public class HorizontalChartEditor : ChartEditorBase
    {
        protected override void DrawSettings() => DrawBarSettings();

        protected override void DrawStyle() => DrawBarStyle();
    }
}