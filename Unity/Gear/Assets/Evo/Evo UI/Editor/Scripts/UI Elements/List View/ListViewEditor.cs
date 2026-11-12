using System;
using System.Collections.Generic;
using Evo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ListView))]
    public class ListViewEditor : UnityEditor.Editor
    {
        ListView listView;

        // Content
        SerializedProperty columns;
        SerializedProperty rows;
        ReorderableListOptions columnListOptions;
        ReorderableListOptions rowListOptions;

        // Style
        SerializedProperty style;
        SerializedProperty styleProvider;
        SerializedProperty useDefaultStylerPreset;
        SerializedProperty stylerPreset;
        SerializedProperty styleMappings;

        // Scroll View
        SerializedProperty scaleMode;
        SerializedProperty virtualizeRows;
        SerializedProperty scrollSensitivity;
        SerializedProperty movementType;
        SerializedProperty elasticity;
        SerializedProperty inertia;
        SerializedProperty decelerationRate;
        SerializedProperty enableSmoothScroll;
        SerializedProperty smoothScrollTime;
        SerializedProperty verticalScrollbar;
        SerializedProperty horizontalScrollbar;
        SerializedProperty autoHideScrollbars;

        // Events
        SerializedProperty onCellClicked;
        SerializedProperty onRowShown;

        // Editor state
        SerializedProperty autoRefresh;
        bool manualRefreshRequested;
        bool refreshScheduled;
        CsvDialogAction pendingCsvDialog;
        List<ColumnSnapshot> columnsBeforeChange;
        List<RowSnapshot> rowsBeforeColumnChange;

        enum CsvDialogAction
        {
            None,
            Import,
            Export
        }

        sealed class ColumnSnapshot
        {
            public readonly ListViewColumn Reference;
            public readonly string Name;
            public readonly Sprite Icon;
            public readonly float Width;
            public readonly bool Flexible;
            public readonly TextAnchor Alignment;
#if EVO_LOCALIZATION
            public readonly string TableKey;
#endif

            public ColumnSnapshot(ListViewColumn column)
            {
                Reference = column;
                Name = column?.columnName;
#if EVO_LOCALIZATION
                TableKey = column?.tableKey;
#endif
                Icon = column?.columnIcon;
                Width = column?.width ?? 0f;
                Flexible = column?.useFlexibleWidth ?? false;
                Alignment = column?.alignment ?? TextAnchor.MiddleCenter;
            }

            public bool Matches(ListViewColumn column)
            {
                return column != null &&
                    Name == column.columnName &&
#if EVO_LOCALIZATION
                    TableKey == column.tableKey &&
#endif
                    Icon == column.columnIcon &&
                    Mathf.Approximately(Width, column.width) &&
                    Flexible == column.useFlexibleWidth &&
                    Alignment == column.alignment;
            }
        }

        sealed class RowSnapshot
        {
            public readonly List<string> Values;
#if EVO_LOCALIZATION
            public readonly List<string> TableKeys;
#endif
            public readonly List<Sprite> Icons;
            public readonly List<GameObject> CustomObjects;
            public readonly List<ListViewCellType> CellTypes;
            public readonly List<bool> DrawBackgrounds;

            public RowSnapshot(ListViewRow row)
            {
                Values = row?.values != null ? new List<string>(row.values) : new List<string>();
#if EVO_LOCALIZATION
                TableKeys = row?.tableKeys != null ? new List<string>(row.tableKeys) : new List<string>();
#endif
                Icons = row?.icons != null ? new List<Sprite>(row.icons) : new List<Sprite>();
                CustomObjects = row?.customObjects != null ? new List<GameObject>(row.customObjects) : new List<GameObject>();
                CellTypes = row?.cellTypes != null ? new List<ListViewCellType>(row.cellTypes) : new List<ListViewCellType>();
                DrawBackgrounds = row?.drawBackgrounds != null ? new List<bool>(row.drawBackgrounds) : new List<bool>();
            }
        }

        void OnEnable()
        {
            InitializeEditorState();
            Undo.undoRedoPerformed += OnUndoRedo;

            if (!Application.isPlaying && listView != null && listView.transform.childCount == 0 && Selection.activeGameObject == listView.gameObject)
                ScheduleRefresh(force: true);
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.delayCall -= DelayedRefresh;
            EditorApplication.delayCall -= OpenPendingCsvDialog;
            refreshScheduled = false;
            pendingCsvDialog = CsvDialogAction.None;
        }

        void InitializeEditorState()
        {
            listView = (ListView)target;

            columns = serializedObject.FindProperty(nameof(ListView.columns));
            rows = serializedObject.FindProperty(nameof(ListView.rows));
            style = serializedObject.FindProperty(nameof(ListView.style));
            styleProvider = serializedObject.FindProperty(nameof(ListView.styleProvider));
            useDefaultStylerPreset = serializedObject.FindProperty(nameof(ListView.useDefaultStylerPreset));
            stylerPreset = serializedObject.FindProperty(nameof(ListView.stylerPreset));
            styleMappings = serializedObject.FindProperty(nameof(ListView.styleMapping));
            scaleMode = serializedObject.FindProperty(nameof(ListView.scaleMode));
            virtualizeRows = serializedObject.FindProperty(nameof(ListView.virtualizeRows));
            scrollSensitivity = serializedObject.FindProperty(nameof(ListView.scrollSensitivity));
            movementType = serializedObject.FindProperty(nameof(ListView.movementType));
            elasticity = serializedObject.FindProperty(nameof(ListView.elasticity));
            inertia = serializedObject.FindProperty(nameof(ListView.inertia));
            decelerationRate = serializedObject.FindProperty(nameof(ListView.decelerationRate));
            enableSmoothScroll = serializedObject.FindProperty(nameof(ListView.enableSmoothScroll));
            smoothScrollTime = serializedObject.FindProperty(nameof(ListView.smoothScrollTime));
            verticalScrollbar = serializedObject.FindProperty(nameof(ListView.verticalScrollbar));
            horizontalScrollbar = serializedObject.FindProperty(nameof(ListView.horizontalScrollbar));
            autoHideScrollbars = serializedObject.FindProperty(nameof(ListView.autoHideScrollbars));
            onCellClicked = serializedObject.FindProperty(nameof(ListView.onCellClicked));
            onRowShown = serializedObject.FindProperty(nameof(ListView.onRowShown));
            autoRefresh = serializedObject.FindProperty(nameof(ListView.autoRefresh));

            columnListOptions = ReorderableListOptions.WithControlName("ListView.Columns")
                .WithTitle("Columns")
                .WithIcon("UI_Column")
                .WithEmptyMessage("Add a column to define the List View layout.")
                .WithAddLabel("Add Column")
                .WithItemInitializer(InitializeColumn)
                .WithDeleteTooltip("Delete column")
                .WithChangeCallbacks(CaptureColumnState, HandleColumnsChanged)
                .WithListClipboard(columns, allowPaste: false);

            rowListOptions = ReorderableListOptions.WithControlName("ListView.Rows")
                .WithTitle("Rows")
                .WithIcon("UI_Row")
                .WithEmptyMessage("Add a row to populate the List View.")
                .WithAddLabel("Add Row")
                .WithItemInitializer(InitializeRow)
                .WithDeleteTooltip("Delete row")
                .WithChangeCallbacks(null, HandleRowsChanged);

        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            PreviewSection();
            ColumnsFoldout();
            RowsFoldout();
            StyleFoldout();
            ScrollViewFoldout();
#if EVO_LOCALIZATION
            ReferencesFoldout();
#endif
            EventsFoldout();
            CsvSection();

            EvoEditorGUI.EndCenteredInspector();

            bool appliedChanges = serializedObject.ApplyModifiedProperties();
            if (appliedChanges)
            {
                listView.isDirty = true;
                ScheduleRefresh();
            }

            if (manualRefreshRequested)
            {
                manualRefreshRequested = false;
                RefreshPreview(reinitialize: true);
            }
        }

        void PreviewSection()
        {
            GUILayout.BeginHorizontal();

            EvoEditorGUI.Toggle(autoRefresh, "Auto Refresh", FieldOptions.WithoutSpace());
            EvoEditorGUI.AddFoldoutSpace();

            if (EvoEditorGUI.Button("Refresh", options: ButtonOptions.WithWidth(62f)))
                manualRefreshRequested = true;

            GUILayout.EndHorizontal();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void ColumnsFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, columns, GetColumnTitle, DrawColumn, columnListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawColumn(SerializedProperty column)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                SerializedProperty flexible = column.FindPropertyRelative(nameof(ListViewColumn.useFlexibleWidth));

                EvoEditorGUI.Property(column.FindPropertyRelative(nameof(ListViewColumn.columnIcon)), "Icon");
                EvoEditorGUI.Property(column.FindPropertyRelative(nameof(ListViewColumn.columnName)), "Name");

#if EVO_LOCALIZATION
                if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                {
                    Localization.Editor.IntegrationGUI.KeyField(column.FindPropertyRelative(nameof(ListViewColumn.tableKey)), "Localization Key");
                    EvoEditorGUI.AddLayoutSpace();
                }
#endif
                EvoEditorGUI.Property(column.FindPropertyRelative(nameof(ListViewColumn.alignment)), "Alignment");
                EvoEditorGUI.Toggle(flexible, "Flexible Width", FieldOptions.WithoutSpace());

                // A flexible column only has a width in Fixed mode, as its minimum
                if (!flexible.boolValue || IsFixed)
                {
                    EvoEditorGUI.AddPropertySpace();
                    EvoEditorGUI.Property(column.FindPropertyRelative(nameof(ListViewColumn.width)),
                        flexible.boolValue ? "Min Width" : "Width", FieldOptions.WithoutSpace());
                }
            }
            EvoEditorGUI.EndContainer();
        }

        void RowsFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, rows, GetRowTitle, DrawRow, rowListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawRow(SerializedProperty row)
        {
            EnsureRowDataConsistency(row, columns.arraySize);

            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                if (columns.arraySize == 0)
                {
                    EvoEditorGUI.Callout("Add a column before editing row cells.", invertColor: true, spaceBefore: 0, spaceAfter: 0);
                    EvoEditorGUI.EndContainer();
                    return;
                }

                SerializedProperty values = row.FindPropertyRelative(nameof(ListViewRow.values));
                SerializedProperty icons = row.FindPropertyRelative(nameof(ListViewRow.icons));
                SerializedProperty customObjects = row.FindPropertyRelative(nameof(ListViewRow.customObjects));
                SerializedProperty cellTypes = row.FindPropertyRelative(nameof(ListViewRow.cellTypes));
                SerializedProperty drawBackgrounds = row.FindPropertyRelative(nameof(ListViewRow.drawBackgrounds));

                for (int i = 0; i < columns.arraySize; i++)
                {
                    DrawCell(row, i, values, icons, customObjects, cellTypes, drawBackgrounds);
                    if (i < columns.arraySize - 1)
                        EvoEditorGUI.AddPropertySpace();
                }
            }
            EvoEditorGUI.EndContainer();
        }

        void DrawCell(SerializedProperty row, int index, SerializedProperty values, SerializedProperty icons,
            SerializedProperty customObjects, SerializedProperty cellTypes, SerializedProperty drawBackgrounds)
        {
            SerializedProperty value = values.GetArrayElementAtIndex(index);
            SerializedProperty icon = icons.GetArrayElementAtIndex(index);
            SerializedProperty customObject = customObjects.GetArrayElementAtIndex(index);
            SerializedProperty cellType = cellTypes.GetArrayElementAtIndex(index);
            SerializedProperty drawBackground = drawBackgrounds.GetArrayElementAtIndex(index);

            EvoEditorGUI.BeginColumn();
            EvoEditorGUI.BeginContainer(GetColumnTitle(columns.GetArrayElementAtIndex(index), index), compactHeader: true);
            {
                EvoEditorGUI.Property(cellType, "Preset", FieldOptions.WithInvertedColor());
                if (cellType.intValue == (int)ListViewCellType.GameObject)
                {
                    EvoEditorGUI.Property(customObject, "Game Object", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(drawBackground, FieldOptions.WithoutSpace().WithInvertedColor());
                    if (customObject.objectReferenceValue == null)
                    {
                        EvoEditorGUI.AddPropertySpace();
                        EvoEditorGUI.Callout("Assign a GameObject or prefab for this cell.", invertColor: true, spaceBefore: 0, spaceAfter: 0);
                    }
                }
                else
                {
                    EvoEditorGUI.Property(icon, "Icon", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(value, "Label", FieldOptions.WithoutSpace().WithInvertedColor());

#if EVO_LOCALIZATION
                    if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        Localization.Editor.IntegrationGUI.KeyField(row.FindPropertyRelative(nameof(ListViewRow.tableKeys)).GetArrayElementAtIndex(index),
                            "Localization Key", options: LayoutOptions.WithInvertedColor());
                    }
#endif
                }
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        void StyleFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Style", false, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(styleProvider, FieldOptions.WithoutBackground().WithoutSpace());
                    if (styleProvider.enumValueIndex == (int)StyleProvider.Styler)
                    {
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        {
                            StylerEditor.PresetFields(useDefaultStylerPreset, stylerPreset);
                            if (stylerPreset.objectReferenceValue == null)
                                EvoEditorGUI.Callout("Assign a Styler Preset to use mapped colors and fonts.", spaceAfter: 0);
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddLayoutSpace();

                    DrawHeaderStyle();
                    DrawCellStyle();
                    DrawBackgroundStyle();
                    DrawLayoutStyle();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawHeaderStyle()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Header", false, FoldoutOptions.WithInvertedColor(), compact: true))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    if (UsesStylerPreset)
                        DrawStylerFontDropdown("Font", ListViewStyle.Type.HeaderFont);
                    else
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerFont)), "Font");

                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerFontSize)), "Font Size");
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerFontStyle)), "Font Style");

                    if (UsesStylerPreset)
                    {
                        DrawStylerColorDropdown("Text Color", ListViewStyle.Type.HeaderTextColor);
                        DrawStylerColorDropdown("Background Color", ListViewStyle.Type.HeaderBackgroundColor, addSpace: false);
                    }
                    else
                    {
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerTextColor)), "Text Color");
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerBackgroundColor)), "Background Color", FieldOptions.WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawCellStyle()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Cell", false, FoldoutOptions.WithInvertedColor(), compact: true))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    if (UsesStylerPreset)
                        DrawStylerFontDropdown("Font", ListViewStyle.Type.RowFont);
                    else
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowFont)), "Font");

                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowFontSize)), "Font Size");
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowFontStyle)), "Font Style");

                    if (UsesStylerPreset)
                    {
                        DrawStylerColorDropdown("Text Color", ListViewStyle.Type.RowTextColor);
                        DrawStylerColorDropdown("Background Color", ListViewStyle.Type.RowBackgroundColor, addSpace: false);
                    }
                    else
                    {
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowTextColor)), "Text Color");
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowBackgroundColor)), "Background Color", FieldOptions.WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawBackgroundStyle()
        {
            bool useStyler = UsesStylerPreset;

            if (EvoEditorGUI.BeginFoldout(target, "Background", false, FoldoutOptions.WithInvertedColor(), compact: true))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    SerializedProperty radiusMode = StyleProperty(nameof(ListViewStyle.radiusMode));
                    SerializedProperty cornerRadius = StyleProperty(nameof(ListViewStyle.cornerRadius));
                    SerializedProperty cornerRadii = StyleProperty(nameof(ListViewStyle.cornerRadii));
                    SerializedProperty independentCorners = StyleProperty(nameof(ListViewStyle.independentCorners));
                    SerializedProperty squircleCorners = StyleProperty(nameof(ListViewStyle.squircleCorners));

                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.shapeSoftness)), "Edge Softness");
                    ProceduralShapeEditor.DrawCornerRadiusControls(radiusMode, cornerRadius, cornerRadii, independentCorners, squircleCorners);
                    EvoEditorGUI.AddLayoutSpace();

                    SerializedProperty overrideOuterCorners = StyleProperty(nameof(ListViewStyle.overrideOuterCorners));
                    EvoEditorGUI.Toggle(overrideOuterCorners, "Override Outer Corners");
                    if (overrideOuterCorners.boolValue)
                    {
                        // The unit follows the list's corner radius
                        bool isPercentage = radiusMode.intValue == (int)ProceduralShape.RadiusMode.Percentage;
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.outerCornerRadius)),
                            isPercentage ? "Outer Radius (%)" : "Outer Radius (px)");
                    }

                    SerializedProperty alternating = StyleProperty(nameof(ListViewStyle.useAlternatingRowColor));
                    EvoEditorGUI.Toggle(alternating, "Alternating Row Color");
                    if (alternating.boolValue)
                    {
                        if (useStyler)
                            DrawStylerColorDropdown("Alternating Color", ListViewStyle.Type.AlternatingRowColor);
                        else
                            EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.alternatingRowColor)), "Alternating Color");
                    }

                    SerializedProperty border = StyleProperty(nameof(ListViewStyle.showBorder));
                    EvoEditorGUI.Toggle(border, FieldOptions.WithoutSpace());
                    if (border.boolValue)
                    {
                        EvoEditorGUI.AddPropertySpace();

                        if (useStyler)
                            DrawStylerColorDropdown("Outline Color", ListViewStyle.Type.BorderColor);
                        else
                            EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.borderColor)), "Outline Color");

                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.borderWidth)), "Outline Width", FieldOptions.WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawLayoutStyle()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Layout", false, FoldoutOptions.WithInvertedColor(), compact: true))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.headerHeight)));

                    // Automatic rows share the List View's height
                    if (IsFixed)
                        EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowHeight)));

                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.iconSize)));
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.columnSpacing)));
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.rowSpacing)));
                    EvoEditorGUI.Property(StyleProperty(nameof(ListViewStyle.contentSpacing)));
                    EvoEditorGUI.ArrayProperty(StyleProperty(nameof(ListViewStyle.contentPadding)), FieldOptions.WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void ScrollViewFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Scroll View", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool fixedMode = IsFixed;
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(scaleMode, FieldOptions.WithoutBackground().WithoutSpace());
                        if (fixedMode)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Toggle(virtualizeRows, "Virtualize Rows", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(fixedMode ? LayoutOptions.WithSpace() : default);

                    if (fixedMode)
                    {
                        EvoEditorGUI.Property(scrollSensitivity, FieldOptions.WithInvertedColor());
                        ScrollRectEditor.MovementTypeField(movementType, elasticity);
                        ScrollRectEditor.InertiaField(inertia, decelerationRate);
                        ScrollRectEditor.SmoothScrollField(enableSmoothScroll, smoothScrollTime);

                        bool hasScrollbar = verticalScrollbar.objectReferenceValue != null || horizontalScrollbar.objectReferenceValue != null;
                        EvoEditorGUI.Property(verticalScrollbar, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(horizontalScrollbar, FieldOptions.WithInvertedColor().WithSpace(hasScrollbar));
                        if (hasScrollbar)
                            EvoEditorGUI.Toggle(autoHideScrollbars, FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

#if EVO_LOCALIZATION
        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, listView.gameObject);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }
#endif

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onCellClicked, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onRowShown, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void CsvSection()
        {
            EvoEditorGUI.AddFoldoutSpace();
            GUILayout.BeginHorizontal();

            if (EvoEditorGUI.Button("Import CSV"))
                QueueCsvDialog(CsvDialogAction.Import);

            EvoEditorGUI.AddFoldoutSpace();

            using (new EditorGUI.DisabledScope(columns.arraySize == 0 || rows.arraySize == 0))
            {
                if (EvoEditorGUI.Button("Export CSV"))
                    QueueCsvDialog(CsvDialogAction.Export);
            }

            GUILayout.EndHorizontal();
        }

        void QueueCsvDialog(CsvDialogAction action)
        {
            if (pendingCsvDialog != CsvDialogAction.None)
                return;

            pendingCsvDialog = action;
            EditorApplication.delayCall += OpenPendingCsvDialog;
        }

        void OpenPendingCsvDialog()
        {
            EditorApplication.delayCall -= OpenPendingCsvDialog;
            CsvDialogAction action = pendingCsvDialog;
            pendingCsvDialog = CsvDialogAction.None;

            if (listView == null)
                return;

            if (action == CsvDialogAction.Import)
            {
                string path = EditorUtility.OpenFilePanel("Import CSV", string.Empty, "csv");
                if (!string.IsNullOrEmpty(path) && listView != null)
                    ImportOptionsWindow.ShowWindow(listView, path);

                return;
            }

            if (action != CsvDialogAction.Export)
                return;

            string defaultName = string.IsNullOrEmpty(listView.gameObject.name)
                ? "ListView_Export.csv"
                : $"{listView.gameObject.name}_Export.csv";
            string exportPath = EditorUtility.SaveFilePanel("Export CSV", string.Empty, defaultName, "csv");
            
            if (!string.IsNullOrEmpty(exportPath) && listView != null)
                ExportOptionsWindow.ShowWindow(listView, exportPath);
        }

        bool UsesStylerPreset => styleProvider.enumValueIndex == (int)StyleProvider.Styler && stylerPreset.objectReferenceValue != null;

        bool IsFixed => scaleMode.intValue == (int)ListView.ScaleMode.Fixed;

        SerializedProperty StyleProperty(string propertyName) => style.FindPropertyRelative(propertyName);

        void DrawStylerColorDropdown(string label, ListViewStyle.Type styleType, bool addSpace = true)
        {
            SerializedProperty mapping = GetOrCreateStyleMapping(styleType);
            StylerEditor.ColorDropdown(stylerPreset,
                mapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.colorID)),
                mapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.overrideAlpha)),
                mapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.alphaOverride)),
                label, addSpace, invertColor: false);
        }

        void DrawStylerFontDropdown(string label, ListViewStyle.Type styleType, bool addSpace = true)
        {
            SerializedProperty mapping = GetOrCreateStyleMapping(styleType);
            StylerEditor.FontDropdown(stylerPreset, mapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.fontID)), label, addSpace);
        }

        SerializedProperty GetOrCreateStyleMapping(ListViewStyle.Type styleType)
        {
            for (int i = 0; i < styleMappings.arraySize; i++)
            {
                SerializedProperty mapping = styleMappings.GetArrayElementAtIndex(i);
                SerializedProperty type = mapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.type));
                if (type.enumValueIndex == (int)styleType)
                    return mapping;
            }

            int index = styleMappings.arraySize;
            styleMappings.arraySize++;
            SerializedProperty newMapping = styleMappings.GetArrayElementAtIndex(index);
            newMapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.type)).enumValueIndex = (int)styleType;
            newMapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.colorID)).stringValue = string.Empty;
            newMapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.overrideAlpha)).boolValue = false;
            newMapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.alphaOverride)).floatValue = 1f;
            newMapping.FindPropertyRelative(nameof(ListViewStyle.Mapping.fontID)).stringValue = string.Empty;
            return newMapping;
        }

        static string GetColumnTitle(SerializedProperty column, int index)
        {
            SerializedProperty name = column.FindPropertyRelative(nameof(ListViewColumn.columnName));
            return name == null || string.IsNullOrWhiteSpace(name.stringValue) ? $"Column {index + 1}" : name.stringValue;
        }

        static string GetRowTitle(SerializedProperty row, int index)
        {
            SerializedProperty values = row.FindPropertyRelative(nameof(ListViewRow.values));
            if (values != null)
            {
                for (int i = 0; i < values.arraySize; i++)
                {
                    string value = values.GetArrayElementAtIndex(i).stringValue;
                    if (!string.IsNullOrWhiteSpace(value))
                        return $"Row {index + 1}: {value}";
                }
            }
            return $"Row {index + 1}";
        }

        static void InitializeColumn(SerializedProperty column)
        {
            int index = GetArrayIndex(column);
            column.FindPropertyRelative(nameof(ListViewColumn.columnName)).stringValue = $"Column {index + 1}";
            column.FindPropertyRelative(nameof(ListViewColumn.columnIcon)).objectReferenceValue = null;
            column.FindPropertyRelative(nameof(ListViewColumn.width)).floatValue = 100f;
            column.FindPropertyRelative(nameof(ListViewColumn.useFlexibleWidth)).boolValue = true;
            column.FindPropertyRelative(nameof(ListViewColumn.alignment)).enumValueIndex = (int)TextAnchor.MiddleCenter;
#if EVO_LOCALIZATION
            column.FindPropertyRelative(nameof(ListViewColumn.tableKey)).stringValue = string.Empty;
#endif
            column.isExpanded = true;
        }

        void InitializeRow(SerializedProperty row)
        {
            EnsureRowDataConsistency(row, columns.arraySize, clearExisting: true);
            row.isExpanded = true;
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

        static void EnsureRowDataConsistency(SerializedProperty row, int targetCount, bool clearExisting = false)
        {
            SerializedProperty values = row.FindPropertyRelative(nameof(ListViewRow.values));
            SerializedProperty icons = row.FindPropertyRelative(nameof(ListViewRow.icons));
            SerializedProperty customObjects = row.FindPropertyRelative(nameof(ListViewRow.customObjects));
            SerializedProperty cellTypes = row.FindPropertyRelative(nameof(ListViewRow.cellTypes));
            SerializedProperty drawBackgrounds = row.FindPropertyRelative(nameof(ListViewRow.drawBackgrounds));

#if EVO_LOCALIZATION
            ResizeArray(row.FindPropertyRelative(nameof(ListViewRow.tableKeys)), targetCount, clearExisting, element => element.stringValue = string.Empty);
#endif
            ResizeArray(values, targetCount, clearExisting, element => element.stringValue = string.Empty);
            ResizeArray(icons, targetCount, clearExisting, element => element.objectReferenceValue = null);
            ResizeArray(customObjects, targetCount, clearExisting, element => element.objectReferenceValue = null);
            ResizeArray(drawBackgrounds, targetCount, clearExisting, element => element.boolValue = true);

            int oldTypeCount = clearExisting ? 0 : cellTypes.arraySize;
            if (clearExisting)
                cellTypes.arraySize = 0;

            cellTypes.arraySize = targetCount;
            for (int i = oldTypeCount; i < targetCount; i++)
            {
                bool hasCustomObject = customObjects.GetArrayElementAtIndex(i).objectReferenceValue != null;
                cellTypes.GetArrayElementAtIndex(i).intValue = hasCustomObject
                    ? (int)ListViewCellType.GameObject
                    : (int)ListViewCellType.None;
            }

            // Migrate the removed Button value, and any unknown serialized value, to None.
            for (int i = 0; i < targetCount; i++)
            {
                SerializedProperty cellType = cellTypes.GetArrayElementAtIndex(i);
                if (cellType.intValue != (int)ListViewCellType.GameObject)
                    cellType.intValue = (int)ListViewCellType.None;
            }
        }

        static void ResizeArray(SerializedProperty array, int targetCount, bool clearExisting, Action<SerializedProperty> initialize)
        {
            int oldSize = clearExisting ? 0 : array.arraySize;
            if (clearExisting)
                array.arraySize = 0;

            array.arraySize = targetCount;
            for (int i = oldSize; i < targetCount; i++)
                initialize(array.GetArrayElementAtIndex(i));
        }

        void CaptureColumnState()
        {
            columnsBeforeChange = new List<ColumnSnapshot>();
            rowsBeforeColumnChange = new List<RowSnapshot>();

            if (listView != null && listView.columns != null)
            {
                foreach (ListViewColumn column in listView.columns)
                    columnsBeforeChange.Add(new ColumnSnapshot(column));
            }

            if (listView != null && listView.rows != null)
            {
                foreach (ListViewRow row in listView.rows)
                    rowsBeforeColumnChange.Add(new RowSnapshot(row));
            }
        }

        void HandleColumnsChanged()
        {
            serializedObject.UpdateIfRequiredOrScript();

            if (columnsBeforeChange != null && rowsBeforeColumnChange != null)
                SynchronizeRowsWithColumns();
            else
                EnsureAllRowsMatchColumnCount();

            serializedObject.ApplyModifiedProperties();
            columnsBeforeChange = null;
            rowsBeforeColumnChange = null;

            listView.isDirty = true;
            ScheduleRefresh();
            Repaint();
        }

        void HandleRowsChanged()
        {
            listView.isDirty = true;
            ScheduleRefresh();
            Repaint();
        }

        void SynchronizeRowsWithColumns()
        {
            int[] oldIndexForNewColumn = BuildColumnMapping();
            int rowCount = Mathf.Min(rows.arraySize, rowsBeforeColumnChange.Count);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(rowIndex);
                SerializedProperty values = row.FindPropertyRelative(nameof(ListViewRow.values));
                SerializedProperty icons = row.FindPropertyRelative(nameof(ListViewRow.icons));
                SerializedProperty customObjects = row.FindPropertyRelative(nameof(ListViewRow.customObjects));
                SerializedProperty cellTypes = row.FindPropertyRelative(nameof(ListViewRow.cellTypes));
                SerializedProperty drawBackgrounds = row.FindPropertyRelative(nameof(ListViewRow.drawBackgrounds));
                RowSnapshot snapshot = rowsBeforeColumnChange[rowIndex];

#if EVO_LOCALIZATION
                SerializedProperty tableKeys = row.FindPropertyRelative(nameof(ListViewRow.tableKeys));
                tableKeys.arraySize = oldIndexForNewColumn.Length;
#endif
                values.arraySize = oldIndexForNewColumn.Length;
                icons.arraySize = oldIndexForNewColumn.Length;
                customObjects.arraySize = oldIndexForNewColumn.Length;
                cellTypes.arraySize = oldIndexForNewColumn.Length;
                drawBackgrounds.arraySize = oldIndexForNewColumn.Length;

                for (int newIndex = 0; newIndex < oldIndexForNewColumn.Length; newIndex++)
                {
                    int oldIndex = oldIndexForNewColumn[newIndex];
#if EVO_LOCALIZATION
                    tableKeys.GetArrayElementAtIndex(newIndex).stringValue = oldIndex >= 0 && oldIndex < snapshot.TableKeys.Count
                        ? snapshot.TableKeys[oldIndex] ?? string.Empty
                        : string.Empty;
#endif

                    values.GetArrayElementAtIndex(newIndex).stringValue = oldIndex >= 0 && oldIndex < snapshot.Values.Count
                            ? snapshot.Values[oldIndex] ?? string.Empty
                            : string.Empty;

                    icons.GetArrayElementAtIndex(newIndex).objectReferenceValue = oldIndex >= 0 && oldIndex < snapshot.Icons.Count
                        ? snapshot.Icons[oldIndex]
                        : null;

                    customObjects.GetArrayElementAtIndex(newIndex).objectReferenceValue = oldIndex >= 0 && oldIndex < snapshot.CustomObjects.Count
                            ? snapshot.CustomObjects[oldIndex]
                            : null;

                    ListViewCellType oldType = oldIndex >= 0 && oldIndex < snapshot.CellTypes.Count &&
                        snapshot.CellTypes[oldIndex] == ListViewCellType.GameObject
                            ? ListViewCellType.GameObject
                            : ListViewCellType.None;

                    cellTypes.GetArrayElementAtIndex(newIndex).intValue = oldIndex >= 0 && oldIndex < snapshot.CellTypes.Count
                            ? (int)oldType
                            : customObjects.GetArrayElementAtIndex(newIndex).objectReferenceValue != null
                                ? (int)ListViewCellType.GameObject
                                : (int)ListViewCellType.None;

                    drawBackgrounds.GetArrayElementAtIndex(newIndex).boolValue =
                        oldIndex < 0 || oldIndex >= snapshot.DrawBackgrounds.Count || snapshot.DrawBackgrounds[oldIndex];
                }
            }

            for (int rowIndex = rowCount; rowIndex < rows.arraySize; rowIndex++)
                EnsureRowDataConsistency(rows.GetArrayElementAtIndex(rowIndex), columns.arraySize);
        }

        int[] BuildColumnMapping()
        {
            int newCount = listView.columns?.Count ?? 0;
            int[] mapping = new int[newCount];
            bool[] oldColumnUsed = new bool[columnsBeforeChange.Count];
            Array.Fill(mapping, -1);

            // Managed references normally survive SerializedProperty moves.
            // This is the unambiguous path, including when two columns have identical settings.
            for (int newIndex = 0; newIndex < newCount; newIndex++)
            {
                ListViewColumn current = listView.columns[newIndex];
                for (int oldIndex = 0; oldIndex < columnsBeforeChange.Count; oldIndex++)
                {
                    if (!oldColumnUsed[oldIndex] && ReferenceEquals(columnsBeforeChange[oldIndex].Reference, current))
                    {
                        mapping[newIndex] = oldIndex;
                        oldColumnUsed[oldIndex] = true;
                        break;
                    }
                }
            }

            // Some serialization operations recreate managed instances, so fall back to their serialized values.
            for (int newIndex = 0; newIndex < newCount; newIndex++)
            {
                if (mapping[newIndex] >= 0)
                    continue;

                for (int oldIndex = 0; oldIndex < columnsBeforeChange.Count; oldIndex++)
                {
                    if (!oldColumnUsed[oldIndex] && columnsBeforeChange[oldIndex].Matches(listView.columns[newIndex]))
                    {
                        mapping[newIndex] = oldIndex;
                        oldColumnUsed[oldIndex] = true;
                        break;
                    }
                }
            }

            // A prefab operation may replace every instance and every value.
            // Preserve cell data by position where possible; genuinely new columns remain empty.
            for (int newIndex = 0; newIndex < newCount; newIndex++)
            {
                if (mapping[newIndex] >= 0 || newIndex >= oldColumnUsed.Length || oldColumnUsed[newIndex])
                    continue;

                mapping[newIndex] = newIndex;
                oldColumnUsed[newIndex] = true;
            }

            return mapping;
        }

        void EnsureAllRowsMatchColumnCount()
        {
            for (int i = 0; i < rows.arraySize; i++)
                EnsureRowDataConsistency(rows.GetArrayElementAtIndex(i), columns.arraySize);
        }

        void OnUndoRedo()
        {
            if (listView == null)
                return;

            listView.isDirty = true;
            ScheduleRefresh(force: true);
            Repaint();
        }

        void ScheduleRefresh(bool force = false)
        {
            if (refreshScheduled || listView == null || Application.isPlaying || (!force && !listView.autoRefresh))
                return;

            refreshScheduled = true;
            EditorApplication.delayCall += DelayedRefresh;
        }

        void DelayedRefresh()
        {
            EditorApplication.delayCall -= DelayedRefresh;
            refreshScheduled = false;
            RefreshPreview(reinitialize: false);
        }

        void RefreshPreview(bool reinitialize)
        {
            if (listView == null || listView.gameObject == null)
                return;

            try
            {
                if (reinitialize)
                    listView.Initialize();

                listView.Refresh();
                EditorUtility.SetDirty(listView);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, listView);
            }
        }

        public sealed class ImportOptionsWindow : EditorWindow
        {
            ListView targetListView;
            string filePath;
            bool hasHeaders = true;
            bool clearExisting;

            public static void ShowWindow(ListView listView, string path)
            {
                ImportOptionsWindow window = GetWindow<ImportOptionsWindow>(true, "CSV Import Options", true);
                window.targetListView = listView;
                window.filePath = path;
                window.minSize = new Vector2(320f, 150f);
                window.maxSize = new Vector2(320f, 150f);
                window.ShowUtility();
            }

            void OnGUI()
            {
                EditorGUILayout.LabelField("Import Options", EditorStyles.boldLabel);
             
                hasHeaders = EditorGUILayout.Toggle(new GUIContent("First Row is Headers", 
                    "Use the first row as column headers."), hasHeaders);
                clearExisting = EditorGUILayout.Toggle(new GUIContent("Clear Existing Data", 
                    "Remove all existing columns and rows before import."), clearExisting);

                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(clearExisting
                    ? "This will replace all current data."
                    : "New columns are appended when the CSV contains more columns.", MessageType.Info);
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Import"))
                    {
                        PerformImport();
                        Close();
                    }

                    if (GUILayout.Button("Cancel"))
                        Close();
                }
                GUILayout.EndHorizontal();
            }

            void PerformImport()
            {
                if (targetListView == null)
                    return;

                Undo.RecordObject(targetListView, "Import List View CSV");
                bool previousAutoRefresh = targetListView.autoRefresh;

                try
                {
                    targetListView.autoRefresh = false;
                    targetListView.LoadFromCSVFile(filePath, hasHeaders, clearExisting);
                }
                finally
                {
                    targetListView.autoRefresh = previousAutoRefresh;
                }

                targetListView.Initialize();
                targetListView.Refresh();

                PrefabUtility.RecordPrefabInstancePropertyModifications(targetListView);
                EditorUtility.SetDirty(targetListView);
            }
        }

        public sealed class ExportOptionsWindow : EditorWindow
        {
            ListView targetListView;
            string filePath;
            bool includeHeaders = true;

            public static void ShowWindow(ListView listView, string path)
            {
                ExportOptionsWindow window = GetWindow<ExportOptionsWindow>(true, "CSV Export Options", true);
                window.targetListView = listView;
                window.filePath = path;
                window.minSize = new Vector2(320f, 130f);
                window.maxSize = new Vector2(320f, 130f);
                window.ShowUtility();
            }

            void OnGUI()
            {
                EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);
                includeHeaders = EditorGUILayout.Toggle(
                    new GUIContent("Include Headers", "Export column names as the first row."), includeHeaders);

                GUILayout.Space(4f);
                int rowCount = targetListView != null ? targetListView.rows.Count : 0;
                int columnCount = targetListView != null ? targetListView.columns.Count : 0;
                EditorGUILayout.HelpBox($"Exporting {rowCount} rows and {columnCount} columns.", MessageType.Info);
                GUILayout.Space(4f);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Export"))
                    {
                        if (targetListView != null)
                            targetListView.SaveToCSVFile(filePath, includeHeaders);

                        Close();
                    }

                    if (GUILayout.Button("Cancel"))
                        Close();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}