using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(DistributionBar)), CanEditMultipleObjects, NoAutoStaticsCleanup]
    public class DistributionBarEditor : UnityEditor.Editor
    {
        // Segments
        SerializedProperty segments;
        ReorderableListOptions segmentListOptions;
        Action<SerializedProperty> segmentDrawer;
        static readonly Func<SerializedProperty, int, string> SegmentTitleGetter = GetSegmentTitle;

        // Settings
        SerializedProperty direction;
        SerializedProperty textDisplay;
        SerializedProperty displayFormat;
        SerializedProperty minimumTextPercentage;
        SerializedProperty enableSmoothing;
        SerializedProperty smoothingDuration;
        SerializedProperty smoothingCurve;

        // Events
        SerializedProperty onValuesChanged;

        void OnEnable()
        {
            segments = serializedObject.FindProperty(nameof(segments));
            direction = serializedObject.FindProperty(nameof(direction));
            textDisplay = serializedObject.FindProperty(nameof(textDisplay));
            displayFormat = serializedObject.FindProperty(nameof(displayFormat));
            minimumTextPercentage = serializedObject.FindProperty(nameof(minimumTextPercentage));
            enableSmoothing = serializedObject.FindProperty(nameof(enableSmoothing));
            smoothingDuration = serializedObject.FindProperty(nameof(smoothingDuration));
            smoothingCurve = serializedObject.FindProperty(nameof(smoothingCurve));
            onValuesChanged = serializedObject.FindProperty(nameof(onValuesChanged));

            segmentDrawer = DrawSegment;
            segmentListOptions = ReorderableListOptions.WithControlName("DistributionBar.Segments")
                .WithTitle("Segments")
                .WithIcon("UI_Object")
                .WithEmptyMessage("Add a segment for each side of the distribution.")
                .WithAddLabel("Add Segment")
                .WithItemInitializer(InitializeSegment)
                .WithDeleteTooltip("Remove segment");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            if (serializedObject.isEditingMultipleObjects)
                EditorGUILayout.PropertyField(segments, true);
            else
                EvoEditorGUI.ReorderableList(serializedObject, segments, SegmentTitleGetter, segmentDrawer, segmentListOptions, compactItems: true);

            EvoEditorGUI.AddFoldoutSpace();
            SettingsFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSegment(SerializedProperty segment)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                EvoEditorGUI.Property(segment.FindPropertyRelative(nameof(DistributionBar.Segment.label)));
                EvoEditorGUI.Property(segment.FindPropertyRelative(nameof(DistributionBar.Segment.value)));
                EvoEditorGUI.Property(segment.FindPropertyRelative(nameof(DistributionBar.Segment.fillShape)));
                EvoEditorGUI.Property(segment.FindPropertyRelative(nameof(DistributionBar.Segment.valueText)), FieldOptions.WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        static void InitializeSegment(SerializedProperty segment)
        {
            segment.FindPropertyRelative(nameof(DistributionBar.Segment.label)).stringValue = "Side";
            segment.FindPropertyRelative(nameof(DistributionBar.Segment.value)).floatValue = 0f;
            segment.FindPropertyRelative(nameof(DistributionBar.Segment.fillShape)).objectReferenceValue = null;
            segment.FindPropertyRelative(nameof(DistributionBar.Segment.valueText)).objectReferenceValue = null;
            segment.isExpanded = true;
        }

        static string GetSegmentTitle(SerializedProperty segment, int index)
        {
            string label = segment.FindPropertyRelative(nameof(DistributionBar.Segment.label)).stringValue;
            return string.IsNullOrEmpty(label) ? $"Side {index + 1}" : label;
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(direction, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Text");
                    {
                        EvoEditorGUI.Property(textDisplay);
                        EvoEditorGUI.Property(displayFormat);
                        EvoEditorGUI.Property(minimumTextPercentage, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddLayoutSpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Animation");
                    {
                        EvoEditorGUI.Toggle(enableSmoothing, "Smoothing", FieldOptions.WithoutSpace());
                        if (enableSmoothing.boolValue || enableSmoothing.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.AddLayoutSpace();
                            EvoEditorGUI.Property(smoothingDuration, "Duration");
                            EvoEditorGUI.Property(smoothingCurve, "Curve", FieldOptions.WithoutSpace());
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onValuesChanged, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}