using UnityEngine;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FlexibleGridLayout), true)]
    public class FlexibleGridLayoutEditor : UnityEditor.Editor
    {
        SerializedProperty padding;
        SerializedProperty childAlignment;
        SerializedProperty fitType;
        SerializedProperty rows;
        SerializedProperty columns;
        SerializedProperty spacing;
        SerializedProperty minCellSize;
        SerializedProperty masonry;
        SerializedProperty controlChildWidth;
        SerializedProperty controlChildHeight;
        SerializedProperty fitX;
        SerializedProperty fitY;
        SerializedProperty preserveAspectRatio;
        SerializedProperty aspectRatio;


        void OnEnable()
        {
            padding = serializedObject.FindProperty("m_Padding");
            childAlignment = serializedObject.FindProperty("m_ChildAlignment");
            fitType = serializedObject.FindProperty("fitType");
            rows = serializedObject.FindProperty("rows");
            columns = serializedObject.FindProperty("columns");
            spacing = serializedObject.FindProperty("spacing");
            minCellSize = serializedObject.FindProperty("minCellSize");
            masonry = serializedObject.FindProperty("masonry");
            controlChildWidth = serializedObject.FindProperty("controlChildWidth");
            controlChildHeight = serializedObject.FindProperty("controlChildHeight");
            fitX = serializedObject.FindProperty("fitX");
            fitY = serializedObject.FindProperty("fitY");
            preserveAspectRatio = serializedObject.FindProperty("preserveAspectRatio");
            aspectRatio = serializedObject.FindProperty("aspectRatio");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawGridSection();
            DrawSizingSection();
            DrawChildrenSection();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawGridSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Grid", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(fitType, FieldOptions.WithInvertedColor());

                    DrawModeSettings();

                    EvoEditorGUI.Property(childAlignment, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(masonry, FieldOptions.WithInvertedColor().WithoutSpace());

                    DrawMasonryCallout();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void DrawModeSettings()
        {
            if (fitType.hasMultipleDifferentValues)
            {
                EvoEditorGUI.Property(rows, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(columns, FieldOptions.WithInvertedColor());
                return;
            }

            FlexibleGridLayout.FitType mode = (FlexibleGridLayout.FitType)fitType.enumValueIndex;

            switch (mode)
            {
                case FlexibleGridLayout.FitType.Uniform:
                    EvoEditorGUI.Callout("Balances rows and columns automatically, then fits both available axes.",
                        invertColor: true, spaceBefore: 0f, spaceAfter: 2);
                    break;

                case FlexibleGridLayout.FitType.Width:
                    EvoEditorGUI.Property(columns, FieldOptions.WithInvertedColor()
                        .WithTooltip("Maximum number of columns. Cell widths fit the available width."));
                    break;

                case FlexibleGridLayout.FitType.Height:
                    EvoEditorGUI.Property(rows, FieldOptions.WithInvertedColor()
                        .WithTooltip("Maximum number of rows. Children flow top-to-bottom and cell heights fit the available height."));
                    break;

                case FlexibleGridLayout.FitType.FixedRows:
                    EvoEditorGUI.Property(rows, FieldOptions.WithInvertedColor()
                        .WithTooltip("Maximum number of rows. Children flow top-to-bottom without automatic height fitting."));
                    break;

                case FlexibleGridLayout.FitType.FixedColumns:
                    EvoEditorGUI.Property(columns, FieldOptions.WithInvertedColor()
                        .WithTooltip("Maximum number of columns. Children flow left-to-right without automatic width fitting."));
                    break;

                case FlexibleGridLayout.FitType.AutoFit:
                    EvoEditorGUI.Callout("Calculates how many minimum-width cells fit, then expands them evenly across the available width.",
                        invertColor: true, spaceBefore: 0f, spaceAfter: 2f);
                    break;
            }
        }

        void DrawMasonryCallout()
        {
            if (masonry.hasMultipleDifferentValues || !masonry.boolValue)
                return;

            if (!controlChildHeight.hasMultipleDifferentValues && controlChildHeight.boolValue)
            {
                EvoEditorGUI.Callout("Controlled children share one height, so the result behaves like a regular grid. " +
                    "Disable Control Child Height to preserve each child's height.", CalloutType.Warning,
                    invertColor: true, spaceAfter: 0f);
            }
            else if (!fitType.hasMultipleDifferentValues)
            {
                FlexibleGridLayout.FitType mode = (FlexibleGridLayout.FitType)fitType.enumValueIndex;
                if (mode == FlexibleGridLayout.FitType.Height || mode == FlexibleGridLayout.FitType.FixedRows)
                {
                    EvoEditorGUI.Callout("Masonry is column-based. The row value only helps derive the column count, and " +
                        "vertical fitting is ignored.", CalloutType.Warning, invertColor: true, spaceAfter: 0f);
                }
            }
        }

        void DrawSizingSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Sizing", true, FoldoutOptions.WithIcon("UI_Group")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.ArrayProperty(padding, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(spacing, FieldOptions.WithInvertedColor()
                        .WithTooltip("Horizontal and vertical gaps between cells."));
                    EvoEditorGUI.Property(minCellSize, "Natural Cell Size", FieldOptions.WithInvertedColor()
                        .WithTooltip("Used directly by fixed modes and as the target minimum size for Auto Fit."));

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(preserveAspectRatio, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (preserveAspectRatio.hasMultipleDifferentValues || preserveAspectRatio.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(aspectRatio, "Width / Height", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void DrawChildrenSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Children", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(controlChildWidth, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(controlChildHeight, FieldOptions.WithInvertedColor());

                    bool mixedMode = fitType.hasMultipleDifferentValues;
                    bool mixedMasonry = masonry.hasMultipleDifferentValues;
                    FlexibleGridLayout.FitType mode = mixedMode
                        ? FlexibleGridLayout.FitType.FixedColumns
                        : (FlexibleGridLayout.FitType)fitType.enumValueIndex;

                    bool forceFitX = !mixedMode && (mode == FlexibleGridLayout.FitType.Uniform ||
                        mode == FlexibleGridLayout.FitType.Width || mode == FlexibleGridLayout.FitType.AutoFit);
                    bool forceFitY = !mixedMode && !mixedMasonry && !masonry.boolValue &&
                        (mode == FlexibleGridLayout.FitType.Uniform || mode == FlexibleGridLayout.FitType.Height);
                    bool ignoreFitY = !mixedMasonry && masonry.boolValue;

                    using (new EditorGUI.DisabledScope(forceFitX))
                    {
                        if (forceFitX)
                            EvoEditorGUI.Toggle(true, "Fit Available Width", FieldOptions.WithInvertedColor());
                        else
                            EvoEditorGUI.Toggle(fitX, "Fit Available Width", FieldOptions.WithInvertedColor());
                    }

                    using (new EditorGUI.DisabledScope(forceFitY || ignoreFitY))
                    {
                        if (forceFitY || ignoreFitY)
                            EvoEditorGUI.Toggle(forceFitY && !ignoreFitY, "Fit Available Height", FieldOptions.WithInvertedColor().WithoutSpace());
                        else
                            EvoEditorGUI.Toggle(fitY, "Fit Available Height", FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}