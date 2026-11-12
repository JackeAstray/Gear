using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Shared inspector layout and controls for all chart components.
    /// </summary>
    public abstract class ChartEditorBase : UnityEditor.Editor
    {
        static readonly Color[] DataPointColors =
        {
            new(0.20f, 0.50f, 1.00f),
            new(1.00f, 0.40f, 0.30f),
            new(0.30f, 0.80f, 0.40f),
            new(1.00f, 0.80f, 0.20f),
            new(0.65f, 0.40f, 0.95f),
            new(0.20f, 0.75f, 0.85f),
            new(1.00f, 0.55f, 0.20f),
            new(0.95f, 0.35f, 0.65f)
        };

        readonly Dictionary<string, SerializedProperty> properties = new();

        SerializedProperty dataPoints;
        ReorderableListOptions dataPointListOptions;


        protected ChartBase Chart => (ChartBase)target;

        protected abstract void DrawSettings();

        protected abstract void DrawStyle();

        protected virtual void OnEnable()
        {
            properties.Clear();
            dataPoints = Property("dataPoints");
            dataPointListOptions = ReorderableListOptions
                .WithControlName(target.GetType().Name + ".DataPoints")
                .WithTitle("Data Points")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add a data point to populate the chart.")
                .WithAddLabel("Add Data Point")
                .WithDeleteTooltip("Delete data point")
                .WithItemInitializer(InitializeDataPoint)
                .WithChangeCallbacks(null, RequestRefresh)
                .WithListClipboard(dataPoints);

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EditorGUI.BeginChangeCheck();
            EvoEditorGUI.BeginCenteredInspector();

            bool manualRefresh = DrawPreviewControls();
            DrawDataPoints();
            SettingsFoldout();
            StyleFoldout();
            InteractionFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();

            bool guiChanged = EditorGUI.EndChangeCheck();
            bool propertiesApplied = serializedObject.ApplyModifiedProperties();

            if (propertiesApplied || guiChanged)
                RequestRefresh();

            if (manualRefresh)
                RequestRefresh(force: true);
        }

        bool DrawPreviewControls()
        {
            GUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(Application.IsPlaying(Chart.gameObject)))
                EvoEditorGUI.Toggle(Property("autoRefresh"), "Auto Refresh", FieldOptions.WithoutSpace());

            EvoEditorGUI.AddFoldoutSpace();

            bool refresh;
            using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(Chart)))
                refresh = EvoEditorGUI.Button("Refresh", options: ButtonOptions.WithWidth(62f));

            GUILayout.EndHorizontal();
            EvoEditorGUI.AddFoldoutSpace();
            return refresh;
        }

        void DrawDataPoints()
        {
            if (serializedObject.isEditingMultipleObjects)
                EditorGUILayout.PropertyField(dataPoints, new GUIContent("Data Points", dataPoints.tooltip), true);
            else
                EvoEditorGUI.ReorderableList(serializedObject, dataPoints, DataPointTitle, DrawDataPoint, dataPointListOptions, compactItems: true);

            EvoEditorGUI.AddFoldoutSpace();
            DrawDataHint();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                DrawSettings();
#if EVO_LOCALIZATION
                EvoEditorGUI.AddLayoutSpace();
                Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, Chart.gameObject);
#endif
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void StyleFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Style", false, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                DrawStylingSource();
                EvoEditorGUI.AddLayoutSpace();
                DrawStyle();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout();
        }

        void InteractionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Interaction", false, FoldoutOptions.WithIcon("UI_Effect")))
            {
                EvoEditorGUI.BeginContainer();
                ToggleGroup("interactable", () =>
                {
                    if (Chart is not PieChart)
                        Field("pointerArea", "Pointer Area");

                    Field("hoverExpand", Chart is PieChart ? "Hover Offset" : "Hover Expand");
                    Field("dimmedOpacity", "Dimmed Opacity");
                    Field("hoverDuration", "Duration");
                    Field("tooltip", addSpace: false);

                    // A Tooltip on the chart or a parent also shows and hides itself for the whole chart
                    SerializedProperty tooltip = Property("tooltip");
                    if (!tooltip.hasMultipleDifferentValues && tooltip.objectReferenceValue is Tooltip item && Chart.transform.IsChildOf(item.transform))
                        EvoEditorGUI.Callout("Move this Tooltip to an object other than the chart or its parents.", spaceBefore: 3, spaceAfter: 0);
                }, addSpace: false, invertColor: true);
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(Property("onPointClicked"), FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(Property("onPointEntered"), FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(Property("onPointExited"), FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout(false);
        }

        void DrawStylingSource()
        {
            SerializedProperty source = Property("styleProvider");
            SerializedProperty useDefault = Property("useDefaultStylerPreset");
            SerializedProperty preset = Property("stylerPreset");
            bool showPreset = source.hasMultipleDifferentValues || source.enumValueIndex == (int)StyleProvider.Styler;

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.Property(source, "Style Provider", FieldOptions.WithoutBackground().WithoutSpace());

            if (showPreset)
            {
                EvoEditorGUI.BeginContainer();
                StylerEditor.PresetFields(useDefault, preset);

                if (!source.hasMultipleDifferentValues && !preset.hasMultipleDifferentValues && preset.objectReferenceValue == null)
                    EvoEditorGUI.Callout("Assign a Styler Preset to use mapped colors and fonts.", spaceAfter: 0);

                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndColumn();
        }

        protected SerializedProperty Property(string name)
        {
            if (!properties.TryGetValue(name, out SerializedProperty property))
                properties[name] = property = serializedObject.FindProperty(name);

            return property;
        }

        protected void Field(string name, string label = null, bool addSpace = true, bool invertColor = false)
        {
            SerializedProperty property = Property(name);
            label ??= ObjectNames.NicifyVariableName(name);
            FieldOptions options = FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor);

            if (property.propertyType == SerializedPropertyType.Boolean)
                EvoEditorGUI.Toggle(property, label, options);
            else
                EvoEditorGUI.Property(property, label, options);
        }

        protected void SettingField(string name, string label = null, bool addSpace = true)
        {
            Field(name, label, addSpace, invertColor: true);
        }

        protected void StyleGroup(string foldoutKey, string title, bool addSpace, Action drawContent)
        {
            if (EvoEditorGUI.BeginFoldout(target, foldoutKey, title, false, FoldoutOptions.WithInvertedColor(), compact: true))
            {
                EvoEditorGUI.BeginContainer();
                drawContent();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndFoldout(addSpace);
        }

        protected void ToggleGroup(string toggle, Action drawContent, bool? showWhenEnabled = true,
            bool addSpace = true, bool invertColor = false)
        {
            SerializedProperty property = Property(toggle);
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColor));
            {
                EvoEditorGUI.Toggle(property, ObjectNames.NicifyVariableName(toggle), FieldOptions.WithoutSpace(), drawIdleBackground: false);

                bool showContent = !showWhenEnabled.HasValue || property.hasMultipleDifferentValues || property.boolValue == showWhenEnabled.Value;
                if (showContent)
                {
                    EvoEditorGUI.BeginContainer();
                    drawContent();
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }

        protected void ColorField(string name, string label = "Color", bool addSpace = true, bool invertColor = false)
        {
            SerializedProperty mapping = Property(name);
            if (UsesStylerPreset)
            {
                StylerEditor.ColorDropdown(Property("stylerPreset"), mapping, label, addSpace, invertColor: invertColor);
                return;
            }

            EvoEditorGUI.Property(mapping.FindPropertyRelative(nameof(ColorMapping.color)), label,
                FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor));
        }

        protected void FontField(string name, string label = "Font", bool addSpace = true, bool invertColor = false)
        {
            SerializedProperty mapping = Property(name);
            if (UsesStylerPreset)
            {
                StylerEditor.FontDropdown(Property("stylerPreset"),
                    mapping.FindPropertyRelative(nameof(FontMapping.stylerID)), label, addSpace, invertColor: invertColor);
                return;
            }

            EvoEditorGUI.Property(mapping.FindPropertyRelative(nameof(FontMapping.font)), label,
                FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor));
        }

        protected void TextStyle(string font, string size, string color, bool addSpace = true)
        {
            FontField(font, invertColor: true);
            Field(size, "Font Size", invertColor: true);
            ColorField(color, "Text Color", addSpace, invertColor: true);
        }

        protected void DrawShapeSettings(string uniformRadius, string settings, bool invertColor = false)
        {
            SerializedProperty shape = Property(settings);
            ProceduralShapeEditor.DrawCornerRadiusControls(
                shape.FindPropertyRelative("radiusMode"),
                Property(uniformRadius),
                shape.FindPropertyRelative("cornerRadii"),
                shape.FindPropertyRelative("independentCorners"),
                shape.FindPropertyRelative("squircleCorners"),
                invertColor);

            EvoEditorGUI.AddLayoutSpace();
            Field(settings + ".softness", "Edge Softness", invertColor: invertColor);

            SerializedProperty outlineWidth = shape.FindPropertyRelative("outlineWidth");
            bool showOutlineColor = outlineWidth.hasMultipleDifferentValues || outlineWidth.floatValue > 0f;
            Field(settings + ".outlineWidth", "Outline Width", addSpace: showOutlineColor, invertColor: invertColor);

            if (showOutlineColor)
                Field(settings + ".outlineColor", "Outline Color", addSpace: false, invertColor: invertColor);
        }

        protected void DrawLabelBackground(bool addSpace = true, bool invertColor = false)
        {
            ToggleGroup("drawLabelBackground", () =>
            {
                ColorField("labelBackgroundColor", "Background Color");
                Field("labelPadding", "Padding");
                DrawShapeSettings("labelCornerRadius", "labelShape");
            }, addSpace: addSpace, invertColor: invertColor);
        }

        protected void DrawBarSettings() => DrawCartesianSettings();

        protected void DrawCartesianSettings()
        {
            DrawScaleSettings();
            SettingField("scaleDivisions", "Scale Divisions");
            SettingField("displayFormat", "Number Format", addSpace: false);
        }

        void DrawScaleSettings()
        {
            if (Chart is LineChart)
            {
                SerializedProperty automatic = Property("autoScale");
                ToggleGroup("autoScale", () =>
                {
                    bool showRange = automatic.hasMultipleDifferentValues || !automatic.boolValue;
                    bool showIncludeZero = automatic.hasMultipleDifferentValues || automatic.boolValue;

                    if (showRange)
                        Field("valueRange", "Value Range", addSpace: showIncludeZero);
                    if (showIncludeZero)
                        Field("includeZero", "Include Zero", addSpace: false);
                }, showWhenEnabled: null, invertColor: true);
            }
            else
            {
                ToggleGroup("autoScale", () => Field("maximumValue", "Maximum Value", addSpace: false),
                    showWhenEnabled: false, invertColor: true);
            }
        }

        protected void DrawBarStyle()
        {
            StyleGroup("barsFoldout", "Bars", true, () =>
            {
                ToggleGroup("useDataColors", () => ColorField("barColor", "Bar Color", addSpace: false, invertColor: true),
                    showWhenEnabled: false);
                Field("gridToBarsOffset", "Plot Padding");
                Field("barSpacing", "Spacing");
                DrawShapeSettings("barCornerRadius", "barShape");
            });

            DrawGridAndAxesStyle();
            DrawCartesianTextStyle();
        }

        protected void DrawGridAndAxesStyle()
        {
            StyleGroup("gridAndAxesFoldout", "Grid & Axes", true, () =>
            {
                ToggleGroup("showGrid", () =>
                {
                    if (Chart is RadarChart)
                        Field("gridLevels", "Levels", invertColor: true);
                    if (Chart is LineChart)
                        Field("verticalGridLines", "Vertical Divisions", invertColor: true);

                    Field("gridLineThickness", "Thickness", invertColor: true);
                    ColorField("gridColor", addSpace: false, invertColor: true);
                });

                ToggleGroup("showAxis", () =>
                {
                    Field("axisThickness", "Thickness", invertColor: true);
                    ColorField("axisColor", addSpace: false, invertColor: true);
                }, addSpace: false);
            });
        }

        protected void DrawCartesianTextStyle()
        {
            StyleGroup("labelsAndValuesFoldout", "Labels & Values", false, () =>
            {
                ToggleGroup("showCategoryLabels", () =>
                {
                    TextStyle("labelFont", "labelFontSize", "labelColor");
                    bool showRotation = Chart is not HorizontalChart;
                    Field("labelPadding", "Padding", addSpace: showRotation, invertColor: true);

                    if (showRotation)
                        Field("labelRotation", "Rotation", addSpace: false, invertColor: true);
                });

                ToggleGroup("showScaleLabels", () =>
                {
                    TextStyle("scaleFont", "scaleFontSize", "scaleColor");
                    bool showRotation = Chart is HorizontalChart;
                    Field("scalePadding", "Padding", addSpace: showRotation, invertColor: true);

                    if (showRotation)
                        Field("labelRotation", "Rotation", addSpace: false, invertColor: true);
                });

                ToggleGroup("showValues", () =>
                {
                    TextStyle("valueFont", "valueFontSize", "valueColor");
                    Field("valuePadding", "Padding", addSpace: false, invertColor: true);
                }, addSpace: false);
            });
        }

        void RequestRefresh() => RequestRefresh(force: false);

        void RequestRefresh(bool force)
        {
            foreach (UnityEngine.Object item in targets)
            {
                if (item is ChartBase chart)
                    chart.RequestEditorRefresh(force);
            }
        }

        void OnUndoRedo() => Repaint();

        void DrawDataHint()
        {
            if (serializedObject.isEditingMultipleObjects)
                return;

            if (Chart is RadarChart && dataPoints.arraySize < 3)
                EvoEditorGUI.Callout("Radar charts need at least three data points.");

            if (Chart is not (PieChart or HorizontalChart or VerticalChart or RadarChart))
                return;

            for (int i = 0; i < dataPoints.arraySize; i++)
            {
                float value = dataPoints.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue;
                if (value >= 0f && !float.IsInfinity(value))
                    continue;

                EvoEditorGUI.Callout("This chart renders finite, non-negative values. Negative and non-finite values render as zero.");
                return;
            }
        }

        static string DataPointTitle(SerializedProperty point, int index)
        {
            string label = point.FindPropertyRelative("label").stringValue;
            return string.IsNullOrWhiteSpace(label) ? "Data Point " + (index + 1) : label;
        }

        void InitializeDataPoint(SerializedProperty point)
        {
            int index = GetArrayIndex(point);
            point.FindPropertyRelative("label").stringValue = "Data Point " + (index + 1);
            point.FindPropertyRelative("value").floatValue = Chart is RadarChart ? 50f : 10f;

            SerializedProperty color = point.FindPropertyRelative("color");
            if (color != null)
                color.colorValue = DataPointColors[index % DataPointColors.Length];

#if EVO_LOCALIZATION
            point.FindPropertyRelative("tableKey").stringValue = string.Empty;
#endif

            point.isExpanded = true;
        }

        static int GetArrayIndex(SerializedProperty property)
        {
            string path = property.propertyPath;
            int start = path.LastIndexOf("data[", StringComparison.Ordinal);
            if (start < 0)
                return 0;

            start += 5;
            int end = path.IndexOf(']', start);
            return end > start && int.TryParse(path[start..end], out int index) ? index : 0;
        }

        static void DrawDataPoint(SerializedProperty point)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true);

            SerializedProperty label = point.FindPropertyRelative("label");
            SerializedProperty value = point.FindPropertyRelative("value");
            SerializedProperty color = point.FindPropertyRelative("color");

            EvoEditorGUI.Property(label, "Label", FieldOptions.WithTooltip(label.tooltip));

            bool showLocalization = false;
#if EVO_LOCALIZATION
            showLocalization = Localization.Editor.IntegrationGUI.IsLocalizationEnabled(point.serializedObject);
#endif
            bool hasFollowingField = color != null || showLocalization;
            EvoEditorGUI.Property(value, "Value", FieldOptions.WithTooltip(value.tooltip).WithSpace(hasFollowingField));

            if (color != null)
                EvoEditorGUI.Property(color, "Color", FieldOptions.WithSpace(showLocalization));

#if EVO_LOCALIZATION
            if (showLocalization)
                Localization.Editor.IntegrationGUI.KeyField(point.FindPropertyRelative("tableKey"), "Localization Key");
#endif

            EvoEditorGUI.EndContainer();
        }

        bool UsesStylerPreset
        {
            get
            {
                SerializedProperty source = Property("styleProvider");
                SerializedProperty preset = Property("stylerPreset");
                return !source.hasMultipleDifferentValues && source.enumValueIndex == (int)StyleProvider.Styler &&
                    !preset.hasMultipleDifferentValues && preset.objectReferenceValue != null;
            }
        }
    }
}