using UnityEditor;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(RadarChart))]
    public class RadarChartEditor : ChartEditorBase
    {

        protected override void DrawSettings()
        {
            SettingField("maximumValue", "Maximum Value");
            SettingField("angleOffset", "Start Angle", addSpace: false);
        }

        protected override void DrawStyle()
        {
            StyleGroup("lineAndFillFoldout", "Line & Fill", true, () =>
            {
                ToggleGroup("showLine", () =>
                {
                    Field("lineThickness", "Thickness", invertColor: true);
                    ColorField("lineColor", addSpace: false, invertColor: true);
                });

                ToggleGroup("showFill", () => ColorField("fillColor", "Fill Color", addSpace: false, invertColor: true));

                ToggleGroup("showPoints", () =>
                {
                    Field("pointSize", "Size", invertColor: true);
                    ColorField("pointColor", addSpace: false, invertColor: true);
                }, addSpace: false);
            });

            DrawGridAndAxesStyle();

            StyleGroup("labelsFoldout", "Labels", false, () => ToggleGroup("showLabels", () =>
            {
                TextStyle("labelFont", "labelFontSize", "labelColor");
                Field("labelOffset", "Offset", invertColor: true);

                ToggleGroup("showValues", () =>
                {
                    Field("displayFormat", "Number Format");
                    ColorField("valueColor", "Value Text Color", addSpace: false);
                }, invertColor: true);

                DrawLabelBackground(addSpace: false, invertColor: true);
            }, addSpace: false));
        }
    }
}