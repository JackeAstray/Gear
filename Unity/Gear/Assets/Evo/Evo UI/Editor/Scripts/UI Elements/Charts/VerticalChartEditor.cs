using UnityEditor;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VerticalChart))]
    public class VerticalChartEditor : ChartEditorBase
    {
        protected override void DrawSettings() => DrawBarSettings();

        protected override void DrawStyle() => DrawBarStyle();
    }
}