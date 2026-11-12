using UnityEditor;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PieChart))]
    public class PieChartEditor : ChartEditorBase
    {

        protected override void DrawSettings()
        {
            SettingField("innerRadiusRatio", "Hole Ratio");
            SettingField("startAngle", "Start Angle");
            SettingField("segments", "Segments");
            SettingField("percentageDecimals", "Percentage Decimals");
            SettingField("displayFormat", "Number Format", addSpace: false);
        }

        protected override void DrawStyle()
        {
            StyleGroup("slicesFoldout", "Slices", true, () =>
            {
                Field("sliceSpacing", "Spacing");
                Field("edgeSmoothing");
                ToggleGroup("drawBorder", () =>
                {
                    Field("borderWidth", "Width", invertColor: true);
                    Field("borderPlacement", "Placement", invertColor: true);
                    ColorField("borderColor", addSpace: false, invertColor: true);
                }, addSpace: false);
            });

            StyleGroup("labelsFoldout", "Labels", true, () => ToggleGroup("showLabels", () =>
            {
                Field("showPercentages", "Show Percentages", invertColor: true);
                Field("minimumLabelPercentage", "Min Label Percentage", invertColor: true);
                TextStyle("labelFont", "labelFontSize", "labelColor");
                DrawLabelBackground(addSpace: false, invertColor: true);
            }, addSpace: false));

            StyleGroup("legendFoldout", "Legend", false, () => ToggleGroup("showLegend", () =>
            {
                Field("legendContainer", "Container", invertColor: true);

                if (!Property("legendContainer").hasMultipleDifferentValues && Property("legendContainer").objectReferenceValue == null)
                    EditorTools.EvoEditorGUI.Callout("Assign a container to display the legend.", spaceBefore: 3, invertColor: true);

                Field("legendValues", "Values", invertColor: true);
                Field("hideZeroValueLegendItems", "Hide Zero Values", invertColor: true);

                TextStyle("legendFont", "legendFontSize", "legendTextColor");
                Field("legendItemHeight", "Item Height", invertColor: true);
                Field("legendColorBoxSize", "Swatch Size", invertColor: true);
                DrawShapeSettings("legendCornerRadius", "legendShape", invertColor: true);
            }, addSpace: false));
        }
    }
}