using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Visual metrics and skin-aware styles for the timeline window. Values are grouped by the
    /// surface they affect so layout adjustments do not require searching through interaction code.
    /// </summary>
    public sealed partial class TimelineWindow
    {
        static class Layout
        {
            // Window sections and scrolling
            public const float HeaderHeight = 32f;
            public const float TransportHeight = 38f;
            public const float RulerHeight = 28f;
            public const float TrackHeight = 48f;
            public const float TrackHeaderWidth = 232f;
            public const float ScrollbarSize = 13f;
            public const float SplitterWidth = 2f;
            public const float SplitterHitWidth = 7f;
            public const float HorizontalPadding = 6f;
            public const float TimelineEndPadding = 80f;
            public const float PlayheadEdgeMargin = 10f;
            public const float MinimumTimelineViewportWidth = 120f;

            // Overview under the canvas
            public const float OverviewHeight = 16f;
            public const float OverviewInnerPadding = 3f;
            public const float OverviewHandleWidth = 4f;
            public const float OverviewMinimumWindow = 8f;

            // Header controls
            public const float HeaderControlY = 6f;
            public const float HeaderControlHeight = 18f;
            public const float HeaderControlGap = 5f;
            public const float HeaderSectionGap = 6f;
            public const float HeaderDetachWidth = 58f;

            // Playback and timeline controls
            public const float TransportControlHeight = 24f;
            public const float TransportIconButtonWidth = 30f;
            public const float TransportPropertyGap = 6f;
            public const int ModeButtonFontSize = 10;
            public const int TransportPlaybackIconSize = 14;
            public const int TransportToggleIconSize = 12;

            // Track headers, drag handles, and add button
            public const float TrackGripX = 6f;
            public const float TrackGripWidth = 15f;
            public const float TrackGripHeight = 22f;
            public const float TrackGripDotSize = 2f;
            public const float TrackGripDotSpacing = 5f;
            public const int TrackGripColumns = 2;
            public const int TrackGripRows = 3;
            public const float TrackGripHitPaddingX = 2f;
            public const float TrackGripHitHeight = 32f;
            public const float TrackLabelX = 29f;
            public const float TrackLabelRightReserve = 76f;
            public const float TrackTitleY = 7f;
            public const float TrackTitleHeight = 18f;
            public const float TrackSubtitleY = 25f;
            public const float TrackSubtitleHeight = 16f;
            public const float TrackBadgeRightInset = 12f;
            public const float TrackBadgeWidth = 28f;
            public const float TrackBadgeHeight = 22f;
            public const int TrackBadgeIconSize = 13;
            public const float MinimumClipWidth = 18f;
            public const float ClipVerticalInset = 6f;
            public const float ClipBodyTintDark = 0.15f;
            public const float ClipBodyTintLight = 0.12f;
            public const float ClipHoverTint = 0.08f;
            public const float ClipEdgeWidth = 7f;
            public const float ClipEdgeHandleWidth = 2f;
            public const float ClipEdgeHandleInset = 3f;
            public const float ClipLabelHorizontalInset = 6f;
            public const float ClipMinimumLabelWidth = 24f;
            public const float ClipMetaGap = 10f;
            public const float ClipSwatchWidth = 14f;
            public const float ClipSwatchHeight = 9f;
            public const float ClipSwatchGap = 4f;
            public const float ClipSwatchRadius = 2f;
            public const float ClickDragThreshold = 2f;
            public const float FlowGuideDashLength = 3f;
            public const float FlowGuideDotRadius = 3f;
            public const float SelectedRowTint = 0.2f;
            public const float HighlightFillAlpha = 0.12f;
            public const int SparklineMaxSamples = 48;
            public const float SparklineSampleSpacing = 4f;
            public const float SparklineMinimumIterationWidth = 12f;
            public const float SparklineVerticalInset = 6f;
            public const float SparklineFillAlpha = 0.13f;
            public const float SparklineLineAlpha = 0.4f;
            public const float SparklineLineWidth = 2f;
            public const float SnapMagnetDistance = 8f;
            public const float DragTimePillHeight = 16f;
            public const float DragTimePillPadding = 6f;
            public const float TimePillY = 3f;
            public const float AddClipButtonHeight = 26f;
            public const float AddClipHorizontalInset = 8f;
            public const float AddClipTopGap = 8f;
            public const float AddClipBottomGap = 8f;
            public const float AddTrackAreaHeight = AddClipTopGap + AddClipButtonHeight + AddClipBottomGap;

            // Clip inspector spacing
            public const float InspectorPadding = 6f;
            public const float InspectorTitleY = 4f;
            public const float InspectorTitleHeight = 20f;
            public const float InspectorContentY = 30f;
            public const float InspectorTimeWidth = 126f;
            public const float InspectorHeaderGap = 8f;
        }

        static class Typography
        {
            public const int Header = 12;
            public const int Ruler = 9;
            public const int TrackTitle = 10;
            public const int TrackSubtitle = 9;
            public const int ClipTitle = 10;
            public const int ClipMetadata = 9;
            public const int EmptyTitle = 14;
            public const int EmptyBody = 11;
        }

        static Color WindowBackground => EditorGUIUtility.isProSkin
            ? new Color32(52, 52, 52, 255)
            : new Color32(188, 188, 188, 255);

        static Color HeaderBackground => EditorGUIUtility.isProSkin
            ? new Color32(48, 48, 48, 255)
            : new Color32(211, 211, 211, 255);

        // Red ruler while recording, as in Unity's Animation window, so record mode is hard to miss
        static Color RecordingRulerBackground => EditorGUIUtility.isProSkin
            ? new Color32(84, 46, 46, 255)
            : new Color32(222, 170, 170, 255);

        static Color PanelBackground => EditorGUIUtility.isProSkin
            ? new Color32(52, 52, 52, 255)
            : new Color32(202, 202, 202, 255);

        static Color RowBackground => EditorGUIUtility.isProSkin
            ? new Color32(57, 57, 57, 255)
            : new Color32(202, 202, 202, 255);

        static Color AlternateRowBackground => EditorGUIUtility.isProSkin
            ? new Color32(54, 54, 54, 255)
            : new Color32(197, 197, 197, 255);

        static Color DividerColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.11f)
            : new Color(0f, 0f, 0f, 0.18f);

        static Color SplitterBackground => EditorGUIUtility.isProSkin
            ? new Color32(34, 34, 34, 255)
            : new Color32(154, 154, 154, 255);

        static Color MutedText => EditorGUIUtility.isProSkin
            ? new Color(0.67f, 0.7f, 0.75f)
            : new Color(0.27f, 0.29f, 0.33f);

        static Color AccentColor => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.62f, 1f)
            : new Color(0.08f, 0.42f, 0.78f);

        static Color PlayheadColor => new(1f, 0.36f, 0.31f);

        // Flow context: the focused group's rows, the time before the group starts, and the ruler label
        static Color FlowGroupTint => new(AccentColor.r, AccentColor.g, AccentColor.b, EditorGUIUtility.isProSkin ? 0.07f : 0.09f);

        static Color FlowGroupUnreachableShade => new(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.18f : 0.08f);

        static Color FlowPillColor => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.36f, 0.6f)
            : new Color(0.2f, 0.45f, 0.75f);

        static Color DropRejectedColor => new(0.9f, 0.32f, 0.3f);

        static Color BoxSelectionColor => new(AccentColor.r, AccentColor.g, AccentColor.b, 0.8f);

        // Swatches sit on a light base, so transparent colors read as empty
        static Color SwatchBase => new(0.82f, 0.82f, 0.82f);

        static Color SwatchBorder => new(0f, 0f, 0f, 0.35f);

        static Color OverviewWindow => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.05f);

        static Color OverviewWindowHover => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.1f) : new Color(0f, 0f, 0f, 0.08f);

        static Color OverviewBorder => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.3f) : new Color(0f, 0f, 0f, 0.3f);

        static Color OverviewBorderHover => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.55f) : new Color(0f, 0f, 0f, 0.5f);

        void EnsureStyles()
        {
            if (headerTitleStyle != null)
                return;

            headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Typography.Header,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis
            };

            headerTimeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Typography.ClipMetadata,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip
            };

            headerTimeStyle.normal.textColor = MutedText;

            rulerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Typography.Ruler
            };

            rulerLabelStyle.normal.textColor = MutedText;

            trackSectionLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Typography.TrackSubtitle
            };

            trackSectionLabelStyle.normal.textColor = MutedText;

            trackSectionCountStyle = new GUIStyle(trackSectionLabelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            trackTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Typography.TrackTitle,
                clipping = TextClipping.Ellipsis
            };

            trackSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Typography.TrackSubtitle,
                clipping = TextClipping.Ellipsis
            };

            trackSubtitleStyle.normal.textColor = MutedText;

            clipTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = Typography.ClipTitle,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis
            };

            clipTitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? Color.Lerp(EditorStyles.label.normal.textColor, Color.white, 0.22f)
                : Color.Lerp(EditorStyles.label.normal.textColor, Color.black, 0.12f);

            // CalcSize on an ellipsis style returns only its padding, so text is measured with a clipping copy
            clipTitleMeasureStyle = new GUIStyle(clipTitleStyle) { clipping = TextClipping.Clip };

            clipMetaStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = Typography.ClipMetadata,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis
            };

            clipMetaStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? Color.Lerp(MutedText, Color.white, 0.2f)
                : Color.Lerp(MutedText, Color.black, 0.12f);

            clipMetaMeasureStyle = new GUIStyle(clipMetaStyle) { clipping = TextClipping.Clip };

            // What a clip changes reads as content, so it takes the title's color at the metadata size
            clipValueStyle = new GUIStyle(clipMetaStyle);
            clipValueStyle.normal.textColor = clipTitleStyle.normal.textColor;

            dragTimeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = Typography.Ruler,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            dragTimeStyle.normal.textColor = Color.white;

            centeredTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Typography.EmptyTitle
            };

            centeredBodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Typography.EmptyBody
            };

            centeredBodyStyle.normal.textColor = MutedText;

            lockIcon = EditorGUIUtility.IconContent("Unlocked").image as Texture2D;
            lockedIcon = EditorGUIUtility.IconContent("Locked").image as Texture2D;
            snapIcon = EditorGUIUtility.IconContent("SceneViewSnap").image as Texture2D;
            loopIcon = EditorGUIUtility.IconContent("preAudioLoopOff").image as Texture2D;
            fitAllIcon = EditorGUIUtility.IconContent("ContentSizeFitter Icon").image as Texture2D;
            parallelFlowIcon = EvoEditorGUI.GetIcon("Tween_FlowParallel");
            sequentialFlowIcon = EvoEditorGUI.GetIcon("Tween_FlowSequential");
        }
    }
}