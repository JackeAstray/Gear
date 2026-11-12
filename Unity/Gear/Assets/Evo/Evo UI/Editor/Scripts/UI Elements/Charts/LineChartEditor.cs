using UnityEditor;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LineChart))]
    public class LineChartEditor : ChartEditorBase
    {
        protected override void DrawSettings() => DrawCartesianSettings();

        protected override void DrawStyle()
        {
            StyleGroup("lineAndFillFoldout", "Line & Fill", true, () =>
            {
                ColorField("lineColor", "Line Color");
                Field("curveMode", "Curve");

                var mode = Property("curveMode");
                if (mode.hasMultipleDifferentValues || mode.enumValueIndex != 0)
                    Field("curveSubdivisions", "Curve Quality");

                Field("lineThickness", "Thickness");
                Field("roundCaps", "Round Caps");
               
                ToggleGroup("showFill", () =>
                {
                    Field("fillOpacity", "Opacity", invertColor: true);
                    Field("fillBottomOpacity", "Bottom Opacity", addSpace: false, invertColor: true);
                });

                ToggleGroup("showPoints", () =>
                {
                    Field("pointSize", "Size", invertColor: true);
                    ColorField("pointColor", addSpace: false, invertColor: true);
                }, addSpace: false);
            });

            DrawGridAndAxesStyle();
            DrawCartesianTextStyle();
        }
    }
}