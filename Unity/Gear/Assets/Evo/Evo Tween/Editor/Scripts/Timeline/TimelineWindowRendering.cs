using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Header, transport, timeline, inspector, and small reusable drawing primitives.
    /// </summary>
    public sealed partial class TimelineWindow
    {
        void DrawHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, HeaderBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), DividerColor);

            Rect ownerRect = new(rect.x + Layout.HorizontalPadding, rect.y + Layout.HeaderControlY, Layout.TrackHeaderWidth, Layout.HeaderControlHeight);
            Component nextOwner = EditorGUI.ObjectField(ownerRect, GUIContent.none, owner, typeof(Component), true) as Component;
            if (nextOwner != owner)
            {
                Bind(nextOwner);
                GUIUtility.ExitGUI();
            }

            Rect lockRect = new(ownerRect.xMax + Layout.HeaderControlGap, ownerRect.y, Layout.HeaderControlHeight, Layout.HeaderControlHeight);
            if (DrawEvoButton(lockRect, string.Empty, lockSelection ? lockedIcon : lockIcon, GetToggleButtonOptions(lockSelection,
                lockSelection ? "Unlock from this component" : "Keep this component when Selection changes", 13)))
            {
                lockSelection = !lockSelection;
            }

            if (owner == null || sequencePreset == null)
                return;

            if (sequencePropertyPaths.Count > 1)
            {
                Rect sequenceRect = new(lockRect.xMax + Layout.HeaderSectionGap, ownerRect.y, 184f, Layout.HeaderControlHeight);
                int currentIndex = Mathf.Max(0, sequencePropertyPaths.IndexOf(sequencePropertyPath));
                if (EvoEditorGUI.Button(sequenceRect, sequenceDisplayNames[currentIndex], IconLibrary.Expand, ButtonOptions.WithInvertedColor()
                    .WithContentAlignment(ContentAlignment.SpaceBetween).WithIconPosition(IconPosition.AfterText)))
                {
                    GenericMenu menu = new();
                    for (int i = 0; i < sequencePropertyPaths.Count; i++)
                        menu.AddItem(new GUIContent(sequenceDisplayNames[i]), i == currentIndex, SelectSequence, sequencePropertyPaths[i]);

                    menu.DropDown(sequenceRect);
                }
            }

            float right = rect.xMax - Layout.HorizontalPadding;
            if (activePreset != null)
            {
                Rect detachRect = new(right - Layout.HeaderDetachWidth, ownerRect.y, Layout.HeaderDetachWidth, Layout.HeaderControlHeight);
                if (DrawEvoButton(detachRect, "Detach", null, ButtonOptions.WithTooltip("Copy the preset definitions inline and detach the asset.").WithInvertedColor()))
                {
                    DetachPreset();
                    GUIUtility.ExitGUI();
                }
                right = detachRect.x - Layout.HeaderSectionGap;
            }

            Rect presetRect = new(right - Layout.TrackHeaderWidth, ownerRect.y, Layout.TrackHeaderWidth, Layout.HeaderControlHeight);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(presetRect, sequencePreset, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
                serializedFieldsChanged = true;

            Rect badgeRect = new(presetRect.x - 48f - Layout.HeaderSectionGap, presetRect.center.y - Layout.HeaderControlHeight * 0.5f, 48f, Layout.HeaderControlHeight);
            bool usesPreset = activePreset != null;
            if (DrawModeButton(badgeRect, usesPreset ? "Preset" : "Inline", usesPreset, usesPreset ? "Preset-backed sequence." : "Inline sequence."))
                EditorGUIUtility.PingObject(usesPreset ? activePreset : owner);
        }

        void SelectSequence(object propertyPath)
        {
            if (this == null || owner == null || (string)propertyPath == sequencePropertyPath)
                return;

            Bind(owner, (string)propertyPath, force: true);
        }

        void DrawTransport(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), DividerColor);

            float x = rect.x + Layout.HorizontalPadding;
            float y = rect.y + (rect.height - Layout.TransportControlHeight) * 0.5f;

            DrawRecordButton(new Rect(x, y, Layout.TransportIconButtonWidth, Layout.TransportControlHeight));
            x += Layout.TransportIconButtonWidth + Layout.TransportPropertyGap;

            Texture2D playbackIcon = EvoEditorGUI.GetIcon(previewGUI?.IsPlaying == true ? IconLibrary.Pause : IconLibrary.Play);
            Texture2D stopIcon = EvoEditorGUI.GetIcon(IconLibrary.Stop);

            if (DrawEvoButton(new Rect(x, y, Layout.TransportIconButtonWidth, Layout.TransportControlHeight), string.Empty, playbackIcon,
                ButtonOptions.WithTooltip(previewGUI?.IsPlaying == true ? "Pause preview (Space)" : "Play preview (Space)")
                    .WithIconSize(Layout.TransportPlaybackIconSize).WithInvertedColor()))
            {
                ToggleTimelinePreview();
            }

            float stopX = x + Layout.TransportIconButtonWidth + Layout.TransportPropertyGap;
            if (DrawEvoButton(new Rect(stopX, y, Layout.TransportIconButtonWidth, Layout.TransportControlHeight), string.Empty, stopIcon,
                ButtonOptions.WithTooltip("Stop and restore preview").WithIconSize(Layout.TransportPlaybackIconSize).WithInvertedColor()))
            {
                StopTimelinePreview();
            }
            x = stopX + Layout.TransportIconButtonWidth + Layout.TransportPropertyGap;

            Rect loopRect = new(x, y, Layout.TransportIconButtonWidth, Layout.TransportControlHeight);
            if (DrawEvoButton(loopRect, string.Empty, loopIcon, GetToggleButtonOptions(loopPreview,
                loopPreview ? "Looping preview playback (L)" : "Loop preview playback (L)", Layout.TransportPlaybackIconSize)))
            {
                ToggleLoopPreview();
            }
            x = loopRect.xMax + Layout.TransportPropertyGap;

            float duration = timelineDuration;
            float currentTime = CurrentTimelineTime;
            string timeLabel = GetTimeLabel(currentTime, duration);
            Rect timeRect = new(x, y, cachedTimeButtonWidth, Layout.TransportControlHeight);
            if (DrawEvoButton(timeRect, timeLabel, null,
                ButtonOptions.WithTooltip("Current time / sequence length. Click to focus the playhead.").WithInvertedColor()))
            {
                FrameTime(currentTime);
            }
            x = timeRect.xMax + Layout.TransportPropertyGap;

            if (playbackSpeed != null)
            {
                Rect speedRect = new(x, y, 100, Layout.TransportControlHeight);
                EditorGUI.BeginChangeCheck();
                DrawEvoProperty(speedRect, playbackSpeed, "Speed", 44);
                if (EditorGUI.EndChangeCheck())
                    serializedFieldsChanged = true;
            }

            float right = rect.xMax - Layout.HorizontalPadding;
            Rect frameRect = new(right - 66f, y, 66f, Layout.TransportControlHeight);
            if (DrawEvoButton(frameRect, "Fit All", fitAllIcon,
                ButtonOptions.WithTooltip("Fit the complete sequence in the timeline (A). F frames the selected clip.").WithInvertedColor()))
            {
                FrameAll();
            }
            right = frameRect.x - Layout.TransportPropertyGap;

            Rect fpsRect = new(right - 84f, y, 84f, Layout.TransportControlHeight);
            DrawFrameRateControl(fpsRect);
            right = fpsRect.x - Layout.TransportPropertyGap;

            Rect snapRect = new(right - Layout.TransportIconButtonWidth, y, Layout.TransportIconButtonWidth, Layout.TransportControlHeight);
            if (DrawEvoButton(snapRect, string.Empty, snapIcon, GetToggleButtonOptions(snapEnabled,
                snapEnabled ? SnapOnTooltip : "Snap to frames and clip edges", Layout.TransportToggleIconSize)))
            {
                snapEnabled = !snapEnabled;
            }
        }

        void ToggleLoopPreview()
        {
            loopPreview = !loopPreview;
            if (previewGUI != null)
                previewGUI.Loop = loopPreview;

            Repaint();
        }

        void DrawFrameRateControl(Rect rect)
        {
            using (new EditorGUI.DisabledScope(!snapEnabled))
            {
                int nextFrameRate = DrawEvoIntPopup(rect, "FPS", snapFrameRate, FrameRateDisplayNames, FrameRatePresets, 30,
                    "Choose the snapping frame rate");
                if (nextFrameRate != snapFrameRate)
                    snapFrameRate = nextFrameRate;
            }
        }

        void SetZoom(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = DefaultPixelsPerSecond;

            pixelsPerSecond = Mathf.Clamp(value, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
        }

        void DrawWorkspace(Rect rect)
        {
            inspectorWidth = ClampInspectorWidth(inspectorWidth, rect.width);

            Rect timelineRect = new(rect.x, rect.y, Mathf.Max(260f, rect.width - inspectorWidth - Layout.SplitterWidth), rect.height);
            Rect splitterRect = new(timelineRect.xMax, rect.y, Layout.SplitterWidth, rect.height);
            Rect inspectorRect = new(splitterRect.xMax, rect.y, Mathf.Max(0f, rect.xMax - splitterRect.xMax), rect.height);

            DrawInspectorSplitter(splitterRect);
            DrawTimeline(timelineRect);
            DrawInspector(inspectorRect);
        }

        void DrawTimeline(Rect rect)
        {
            EditorGUI.DrawRect(rect, WindowBackground);

            float duration = timelineDuration;
            float baseCanvasWidth = Mathf.Max(0f, rect.width - Layout.TrackHeaderWidth);
            float bodyHeight = Mathf.Max(0f, rect.height - Layout.RulerHeight - Layout.OverviewHeight);
            float naturalContentWidth = duration > 0f ? duration * pixelsPerSecond : 0f;
            float naturalContentHeight = (clips?.arraySize ?? 0) * Layout.TrackHeight + Layout.AddTrackAreaHeight;

            // The overview under the canvas is always shown, so only the vertical scrollbar changes the layout
            bool showVerticalScrollbar = naturalContentHeight > bodyHeight + 0.5f;
            float verticalScrollbarWidth = showVerticalScrollbar ? Layout.ScrollbarSize : 0f;
            bool scrollsHorizontally = naturalContentWidth > baseCanvasWidth - verticalScrollbarWidth + 0.5f;

            Rect cornerRect = new(rect.x, rect.y, Layout.TrackHeaderWidth, Layout.RulerHeight);
            Rect rulerRect = new(cornerRect.xMax, rect.y, Mathf.Max(0f, rect.width - Layout.TrackHeaderWidth - verticalScrollbarWidth), Layout.RulerHeight);
            Rect headersRect = new(rect.x, rect.y + Layout.RulerHeight, Layout.TrackHeaderWidth, bodyHeight);
            Rect canvasRect = new(headersRect.xMax, headersRect.y, Mathf.Max(0f, rect.width - Layout.TrackHeaderWidth - verticalScrollbarWidth), headersRect.height);
            Rect overviewRect = new(canvasRect.x, canvasRect.yMax, canvasRect.width, Layout.OverviewHeight);

            float contentWidth = Mathf.Max(canvasRect.width, naturalContentWidth + (scrollsHorizontally ? Layout.TimelineEndPadding : 0f));
            float contentHeight = Mathf.Max(canvasRect.height, naturalContentHeight);
            timelineViewportWidth = canvasRect.width;
            FollowPlayheadDuringPlayback();
            if (trackToReveal >= 0 && Event.current.type == EventType.Layout)
            {
                int count = clips?.arraySize ?? 0;
                if (trackToReveal < count)
                {
                    float trackTop = trackToReveal * Layout.TrackHeight;
                    verticalScroll = trackToReveal == count - 1
                        ? contentHeight - canvasRect.height
                        : Mathf.Clamp(verticalScroll, trackTop + Layout.TrackHeight - canvasRect.height, trackTop);
                }
                trackToReveal = -1;
            }
            float maxHorizontalScroll = scrollsHorizontally ? Mathf.Max(0f, contentWidth - canvasRect.width) : 0f;
            horizontalScroll = Mathf.Clamp(horizontalScroll, 0f, maxHorizontalScroll);
            verticalScroll = showVerticalScrollbar ? Mathf.Clamp(verticalScroll, 0f, Mathf.Max(0f, contentHeight - canvasRect.height)) : 0f;

            // Allocate scrollbar controls before culled rows so scrolling cannot change their control IDs
            EditorGUI.DrawRect(new Rect(rect.x, overviewRect.y, Layout.TrackHeaderWidth, Layout.OverviewHeight), HeaderBackground);
            EditorGUI.DrawRect(new Rect(overviewRect.xMax, overviewRect.y, verticalScrollbarWidth, Layout.OverviewHeight), HeaderBackground);
            DrawOverview(overviewRect, maxHorizontalScroll);

            if (showVerticalScrollbar)
            {
                verticalScroll = GUI.VerticalScrollbar(new Rect(canvasRect.xMax, canvasRect.y, Layout.ScrollbarSize, canvasRect.height),
                    verticalScroll, canvasRect.height, 0f, contentHeight);
            }

            int clipControlId = GUIUtility.GetControlID(ClipControlHint, FocusType.Passive);
            UpdateHoveredFlowTrack(headersRect);
            int focusedGroup = GetFocusedFlowGroup();

            EditorGUI.DrawRect(cornerRect, HeaderBackground);
            Rect trackSectionRect = new(cornerRect.x + 6f, cornerRect.y - 1, cornerRect.width - 14f, cornerRect.height - 1f);
            int clipCount = clips?.arraySize ?? 0;
            GUI.Label(trackSectionRect, "TRACKS", trackSectionLabelStyle);
            GUI.Label(trackSectionRect, clipCount == 1 ? "1 CLIP" : $"{clipCount} CLIPS", trackSectionCountStyle);
            EditorGUI.DrawRect(new Rect(cornerRect.xMax - 1f, cornerRect.y, 1f, cornerRect.height), DividerColor);
            EditorGUI.DrawRect(new Rect(cornerRect.x, cornerRect.yMax - 1f, cornerRect.width, 1f), DividerColor);

            DrawRuler(rulerRect, focusedGroup);
            DrawTrackHeaders(headersRect, focusedGroup);
            DrawCanvas(canvasRect, focusedGroup, clipControlId);

            HandleTargetDrop(headersRect, canvasRect);
            HandleTimelineScroll(new Rect(headersRect.x, rulerRect.y, headersRect.width + canvasRect.width,
                Layout.RulerHeight + canvasRect.height), canvasRect, maxHorizontalScroll);
            HandleTrackReorder(headersRect);
        }

        void DrawRuler(Rect rect, int focusedGroup)
        {
            EditorGUI.DrawRect(rect, recording ? RecordingRulerBackground : HeaderBackground);
            GUI.BeginGroup(rect);

            GetRulerSteps(out float majorStep, out float minorStep, out int minorsPerMajor);
            int firstTick = Mathf.FloorToInt(GetTimelineTimeAt(0f) / minorStep);
            int lastTick = Mathf.FloorToInt(GetTimelineTimeAt(rect.width) / minorStep) + 1;

            // Integer ticks keep frame-sized steps from drifting off their frames over long timelines
            for (int tick = firstTick; tick <= lastTick; tick++)
            {
                bool major = tick % minorsPerMajor == 0;
                float time = major ? tick / minorsPerMajor * majorStep : tick * minorStep;
                float x = TimeToX(time);
                float tickHeight = major ? 10f : 5f;
                EditorGUI.DrawRect(new Rect(x, rect.height - tickHeight, 1f, tickHeight), DividerColor);

                if (major)
                    GUI.Label(new Rect(x + 4f, 3f, 70f, 16f), FormatRulerTime(time), rulerLabelStyle);
            }

            float playheadX = TimeToX(CurrentTimelineTime);
            if (playheadX >= -2f && playheadX <= rect.width + 2f)
            {
                EditorGUI.DrawRect(new Rect(playheadX - 1f, 0f, 2f, rect.height), PlayheadColor);
                DrawPlayheadCap(playheadX, 0f);
            }

            // While dragging, the time pill names the clip the group waits for once the clip reaches the group start,
            // in the same flow color as the hover label
            if (TryGetDragEdgeTime(out float edgeTime))
            {
                string reason = clipDragMode != ClipDragMode.TrimEnd && edgeTime <= dragGroupStart + RecordingTimeTolerance
                    ? GetFlowGroupLabel(focusedGroup) : null;
                DrawTimePill(TimeToX(edgeTime), Layout.TimePillY, reason != null ? $"{edgeTime:0.###}s · {reason}" : $"{edgeTime:0.###}s",
                    rect.width, reason != null ? FlowPillColor : AccentColor);
            }
            else if (focusedGroup >= 0)
            {
                DrawTimePill(TimeToX(flowGroups[focusedGroup].Start), Layout.TimePillY, GetFlowGroupLabel(focusedGroup), rect.width, FlowPillColor);
            }

            GUI.EndGroup();
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), DividerColor);
            HandleRulerInput(rect);
        }

        void DrawTrackHeaders(Rect rect, int focusedGroup)
        {
            GUI.BeginGroup(rect);
            int count = clips?.arraySize ?? 0;

            for (int i = 0; i < count; i++)
            {
                float y = i * Layout.TrackHeight - verticalScroll;
                Rect rowRect = new(0f, y, rect.width, Layout.TrackHeight);
                if (rowRect.yMax < 0f || rowRect.y > rect.height)
                    continue;

                DrawRowBackground(rowRect, i, focusedGroup);
                EditorGUI.DrawRect(new Rect(rowRect.xMax - 1f, rowRect.y, 1f, rowRect.height), DividerColor);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), DividerColor);

                Rect handleRect = new(Layout.TrackGripX, y + (Layout.TrackHeight - Layout.TrackGripHeight - 1) * 0.5f, Layout.TrackGripWidth, Layout.TrackGripHeight);
                DrawDragHandle(handleRect, reorderSource == i);

                string title = GetTrackTitle(i);
                Component target = GetTargetProperty(i)?.objectReferenceValue as Component;
                Rect titleRect = new(Layout.TrackLabelX, y + Layout.TrackTitleY, rect.width - Layout.TrackLabelRightReserve, Layout.TrackTitleHeight);
                GUI.Label(titleRect, title, trackTitleStyle);

                // Long module paths are ellipsized, so the full title is available on hover
                if (Event.current.type == EventType.Repaint && titleRect.Contains(Event.current.mousePosition))
                {
                    tooltipContent.text = string.Empty;
                    tooltipContent.tooltip = title;
                    GUI.Label(titleRect, tooltipContent, GUIStyle.none);
                }

                GUI.Label(new Rect(Layout.TrackLabelX, y + Layout.TrackSubtitleY,
                    rect.width - Layout.TrackLabelRightReserve, Layout.TrackSubtitleHeight),
                    target != null ? target.name : "Auto-resolve target", trackSubtitleStyle);

                ClipTiming timing = i < clipTimings.Count ? clipTimings[i] : default;
                Rect badgeRect = GetFlowButtonRect(rect.width, y);
                if (DrawFlowButton(badgeRect, timing.Flow))
                {
                    SetFlow(new[] { i }, timing.Flow == ClipFlow.Sequential ? ClipFlow.Parallel : ClipFlow.Sequential);
                    GUIUtility.ExitGUI();
                }

                if (dropTrack == i)
                    DrawDropHighlight(rowRect);
            }

            if (reorderSource >= 0 && reorderTarget >= 0)
                DrawInsertionLine(reorderTarget + (reorderTarget > reorderSource ? 1 : 0), rect);
            else if (dropInsertIndex >= 0)
                DrawInsertionLine(dropInsertIndex, rect);

            float addY = count * Layout.TrackHeight - verticalScroll + Layout.AddClipTopGap;
            Rect addRect = new(Layout.AddClipHorizontalInset, addY, rect.width - Layout.AddClipHorizontalInset * 2f, Layout.AddClipButtonHeight);
            if (addRect.yMax >= 0f && addRect.y <= rect.height)
            {
                GUILayout.BeginArea(addRect);
                if (EvoEditorGUI.Button("Add Clip", IconLibrary.Add, ButtonOptions.WithTooltip("Add a tween module to the sequence")
                    .WithSize(addRect.width, Layout.AddClipButtonHeight).WithIconSize(8).WithoutIdleBackground()))
                {
                    ShowAddMenu();
                }
                GUILayout.EndArea();
            }

            GUI.EndGroup();
        }

        void DrawCanvas(Rect rect, int focusedGroup, int clipControlId)
        {
            GUI.BeginGroup(rect);
            int count = clips?.arraySize ?? 0;

            for (int i = 0; i < count; i++)
            {
                float y = i * Layout.TrackHeight - verticalScroll;
                Rect rowRect = new(0f, y, rect.width, Layout.TrackHeight);
                if (rowRect.yMax < 0f || rowRect.y > rect.height)
                    continue;

                DrawRowBackground(rowRect, i, focusedGroup);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), DividerColor);
                if (dropTrack == i)
                    DrawDropHighlight(rowRect);
            }

            float tracksHeight = Mathf.Clamp(count * Layout.TrackHeight - verticalScroll, 0f, rect.height);
            DrawGrid(rect, tracksHeight);

            if (count == 0)
            {
                GUI.Label(new Rect(20f, 28f, rect.width - 40f, 24f), "No clips yet", centeredTitleStyle);
                GUI.Label(new Rect(20f, 55f, rect.width - 40f, 40f), "Use Add Clip or right-click here to add the first clip.", centeredBodyStyle);
            }
            else
            {
                if (focusedGroup >= 0)
                    DrawFlowGroupGuide(focusedGroup);

                for (int i = 0; i < count; i++)
                    DrawClip(i, rect);
            }

            // While dragging, a guide marks the edge being placed; it turns solid when it locks onto another edge
            if (TryGetDragEdgeTime(out float edgeTime))
            {
                float guideTime = dragSnappedToEdge ? dragSnapEdgeTime : edgeTime;
                Color guideColor = dragSnappedToEdge ? AccentColor : new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.4f);
                EditorGUI.DrawRect(new Rect(TimeToX(guideTime), 0f, 1f, tracksHeight), guideColor);
            }

            if (dropInsertIndex >= 0)
                DrawDropGhost(rect);

            if (boxSelecting && boxSelectionMoved)
                DrawHighlight(GetBoxSelectionRect(), BoxSelectionColor);

            float playheadX = TimeToX(CurrentTimelineTime);
            if (playheadX >= -2f && playheadX <= rect.width + 2f)
                EditorGUI.DrawRect(new Rect(playheadX - 1f, 0f, 2f, rect.height), PlayheadColor);

            HandleClipInput(new Rect(0f, 0f, rect.width, rect.height), clipControlId);
            GUI.EndGroup();
        }

        void DrawRowBackground(Rect rowRect, int index, int focusedGroup)
        {
            EditorGUI.DrawRect(rowRect, GetRowColor(index));
            if (focusedGroup >= 0 && index < clipTimings.Count && clipTimings[index].Group == focusedGroup)
                EditorGUI.DrawRect(rowRect, FlowGroupTint);
        }

        /// <summary>
        /// Shows why a flow group starts where it does: the time before it is shaded on the group's rows, and a dashed
        /// line runs from the end of the clip the group waits for down through the group.
        /// </summary>
        void DrawFlowGroupGuide(int groupIndex)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            FlowGroup group = flowGroups[groupIndex];
            float x = Mathf.Round(TimeToX(group.Start));
            float groupTop = group.First * Layout.TrackHeight - verticalScroll;
            float groupBottom = (group.Last + 1) * Layout.TrackHeight - verticalScroll;
            float zeroX = Mathf.Max(0f, TimeToX(0f));
            if (x > zeroX)
                EditorGUI.DrawRect(Rect.MinMaxRect(zeroX, groupTop, x, groupBottom), FlowGroupUnreachableShade);

            float lineTop = group.After >= 0 ? group.After * Layout.TrackHeight - verticalScroll + Layout.TrackHeight * 0.5f : groupTop;
            for (float y = lineTop; y < groupBottom; y += Layout.FlowGuideDashLength * 2f)
                EditorGUI.DrawRect(new Rect(x, y, 1f, Mathf.Min(Layout.FlowGuideDashLength, groupBottom - y)), AccentColor);

            if (group.After >= 0)
            {
                Color previousColor = Handles.color;
                Handles.color = AccentColor;
                Handles.DrawSolidDisc(new Vector3(x + 0.5f, lineTop, 0f), Vector3.forward, Layout.FlowGuideDotRadius);
                Handles.color = previousColor;
            }
        }

        // Cached until the timings change, since the label is drawn on every repaint while a flow button is hovered
        string GetFlowGroupLabel(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= flowGroups.Count)
                return null;
            if (flowLabelGroup == groupIndex)
                return flowLabel;

            int after = flowGroups[groupIndex].After;
            Component target = after >= 0 ? GetTargetProperty(after)?.objectReferenceValue as Component : null;
            flowLabel = after < 0 ? "sequence start"
                : target != null ? $"after {clipVisuals[after].ShortTitle} ({target.name})"
                : $"after {clipVisuals[after].ShortTitle}";
            flowLabelGroup = groupIndex;
            return flowLabel;
        }

        // A faint fill inside an outline of the given color, for the selection box and drop targets
        static void DrawHighlight(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, Layout.HighlightFillAlpha));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, 1f, Mathf.Max(0f, rect.height - 2f)), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y + 1f, 1f, Mathf.Max(0f, rect.height - 2f)), color);
        }

        void DrawInsertionLine(int index, Rect rect)
        {
            float lineY = Mathf.Clamp(index * Layout.TrackHeight - verticalScroll - 1f, 0f, Mathf.Max(0f, rect.height - 2f));
            EditorGUI.DrawRect(new Rect(0f, lineY, rect.width, 2f), AccentColor);
        }

        /// <summary>
        /// A map of every clip under the canvas, with the visible range as a window. Dragging the window scrolls,
        /// dragging either end zooms, a click beside it jumps there, and a double-click fits the whole sequence.
        /// </summary>
        void DrawOverview(Rect rect, float maxHorizontalScroll)
        {
            float span = overviewDragMode != OverviewDragMode.None ? overviewDragSpan : GetOverviewSpan(rect.width);
            float scale = rect.width / span;
            float windowStart = rect.x + horizontalScroll / pixelsPerSecond * scale;
            float windowEnd = Mathf.Min(rect.xMax, rect.x + (horizontalScroll + rect.width) / pixelsPerSecond * scale);
            HandleOverviewInput(rect, span, windowStart, windowEnd, maxHorizontalScroll);

            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, HeaderBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), DividerColor);

            int count = clipTimings.Count;
            float innerHeight = rect.height - Layout.OverviewInnerPadding * 2f;
            float lineHeight = Mathf.Clamp(innerHeight / Mathf.Max(1, count) - 0.3f, 1f, 2f);
            float lineStep = count > 1 ? (innerHeight - lineHeight) / (count - 1) : 0f;
            for (int i = 0; i < count && i < clipVisuals.Count; i++)
            {
                ClipTiming timing = clipTimings[i];
                Color color = clipVisuals[i].Color;
                color.a = IsSelected(i) ? 1f : 0.7f;
                EditorGUI.DrawRect(new Rect(rect.x + timing.Start * scale, rect.y + Layout.OverviewInnerPadding + i * lineStep,
                    Mathf.Max(1.5f, timing.TotalDuration * scale), lineHeight), color);
            }

            float playheadX = rect.x + CurrentTimelineTime * scale;
            if (playheadX <= rect.xMax)
                EditorGUI.DrawRect(new Rect(playheadX - 0.5f, rect.y + 1f, 1f, rect.height - 1f), PlayheadColor);

            bool active = overviewHovered || overviewDragMode != OverviewDragMode.None;
            Rect window = Rect.MinMaxRect(windowStart, rect.y + 1.5f, Mathf.Max(windowEnd, windowStart + Layout.OverviewMinimumWindow), rect.yMax - 1.5f);
            EvoEditorGUI.DrawRoundedBackground(window, active ? OverviewWindowHover : OverviewWindow);
            EvoEditorGUI.DrawRoundedBorder(window, active ? OverviewBorderHover : OverviewBorder);
            EditorGUI.DrawRect(new Rect(window.x + 3f, window.y + 3.5f, 1f, window.height - 7f), OverviewBorderHover);
            EditorGUI.DrawRect(new Rect(window.xMax - 4f, window.y + 3.5f, 1f, window.height - 7f), OverviewBorderHover);
        }

        // The whole sequence, or more when the view reaches past its end
        float GetOverviewSpan(float viewportWidth) => Mathf.Max(DefaultClipDuration,
            Mathf.Max(timelineDuration, (horizontalScroll + viewportWidth) / pixelsPerSecond));

        // In headers-local row coordinates, matching where DrawTrackHeaders puts the button
        static Rect GetFlowButtonRect(float headerWidth, float rowY) => new(headerWidth - Layout.TrackBadgeWidth - Layout.TrackBadgeRightInset,
            rowY + (Layout.TrackHeight - Layout.TrackBadgeHeight) * 0.5f, Layout.TrackBadgeWidth, Layout.TrackBadgeHeight);

        void UpdateHoveredFlowTrack(Rect headersRect)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseMove)
                return;

            int hovered = -1;
            int count = clips?.arraySize ?? 0;
            if (headersRect.Contains(current.mousePosition))
            {
                Vector2 local = current.mousePosition - headersRect.position;
                int index = GetTrackAt(local.y);
                if (index >= 0 && index < count && GetFlowButtonRect(headersRect.width, index * Layout.TrackHeight - verticalScroll).Contains(local))
                    hovered = index;
            }

            SetHoveredFlowTrack(hovered);
        }

        void SetHoveredFlowTrack(int index)
        {
            if (index == hoveredFlowTrack)
                return;

            hoveredFlowTrack = index;
            Repaint();
        }

        // The group being dragged in, or the group of the hovered flow button
        int GetFocusedFlowGroup()
        {
            int track = clipDragMode != ClipDragMode.None ? draggedTrack : hoveredFlowTrack;
            return track >= 0 && track < clipTimings.Count ? clipTimings[track].Group : -1;
        }

        // Lines follow the ruler's labeled ticks and stop at the last track
        void DrawGrid(Rect rect, float height)
        {
            if (height <= 0f)
                return;

            GetRulerSteps(out float step, out _, out _);
            int first = Mathf.FloorToInt(GetTimelineTimeAt(0f) / step);
            int last = Mathf.FloorToInt(GetTimelineTimeAt(rect.width) / step) + 1;

            for (int i = first; i <= last; i++)
            {
                float x = TimeToX(i * step);
                EditorGUI.DrawRect(new Rect(x, 0f, 1f, height), DividerColor);
            }
        }

        void DrawClip(int index, Rect viewport)
        {
            if (index < 0 || index >= clipTimings.Count)
                return;

            ClipTiming timing = clipTimings[index];
            float naturalWidth = timing.TotalDuration * pixelsPerSecond;
            Rect clipRect = GetClipRect(index, timing);
            if (clipRect.xMax < 0f || clipRect.x > viewport.width || clipRect.yMax < 0f || clipRect.y > viewport.height)
                return;

            if (timing.Delay > 0f)
            {
                float groupX = TimeToX(timing.GroupStart);
                float leadStart = Mathf.Max(0f, groupX);
                float leadEnd = Mathf.Min(viewport.width, clipRect.x);

                if (leadEnd > leadStart)
                {
                    float centerY = clipRect.center.y;
                    EditorGUI.DrawRect(new Rect(leadStart, centerY, leadEnd - leadStart, 1f), new Color(MutedText.r, MutedText.g, MutedText.b, 0.45f));
                    EditorGUI.DrawRect(new Rect(groupX, centerY - 3f, 1f, 7f), MutedText);
                }
            }

            // Visuals are built alongside the timings, so the index checked above is valid for both
            ClipVisual visual = clipVisuals[index];
            // Clips moving along with the dragged one look active too
            ClipDragMode activeZone = clipDragMode == ClipDragMode.None
                ? index == hoveredTrack ? hoveredZone : ClipDragMode.None
                : index == draggedTrack ? clipDragMode : IsDragged(index) ? ClipDragMode.Move : ClipDragMode.None;
            Color neutralBody = EditorGUIUtility.isProSkin ? new Color32(66, 66, 66, 255) : new Color32(225, 225, 225, 255);
            float tint = (EditorGUIUtility.isProSkin ? Layout.ClipBodyTintDark : Layout.ClipBodyTintLight)
                + (activeZone != ClipDragMode.None ? Layout.ClipHoverTint : 0f);
            Color bodyColor = Color.Lerp(neutralBody, visual.Color, tint);
            Color outline = IsSelected(index) ? EvoEditorGUI.Palette.Selection : visual.Color;

            DrawRoundedClipBox(clipRect, bodyColor, outline);

            if (Event.current.type == EventType.Repaint)
            {
                DrawEaseSparkline(clipRect, visual, timing, naturalWidth, viewport.width);

                if (timing.Iterations > 1 && naturalWidth > 30f)
                {
                    Color markerColor = new(visual.Color.r, visual.Color.g, visual.Color.b, 0.5f);
                    for (int iteration = 1; iteration < timing.Iterations; iteration++)
                    {
                        float markerX = clipRect.x + naturalWidth * iteration / timing.Iterations;
                        if (markerX >= clipRect.xMax)
                            break;

                        EditorGUI.DrawRect(new Rect(markerX, clipRect.y + 4f, 1f, clipRect.height - 8f), markerColor);
                    }
                }

                // Trim edges show a handle while hovered or dragged, so the grab zones are discoverable
                if (activeZone == ClipDragMode.TrimStart || activeZone == ClipDragMode.TrimEnd)
                {
                    float handleX = activeZone == ClipDragMode.TrimStart
                        ? clipRect.x + Layout.ClipEdgeHandleInset
                        : clipRect.xMax - Layout.ClipEdgeHandleInset - Layout.ClipEdgeHandleWidth;
                    EditorGUI.DrawRect(new Rect(handleX, clipRect.y + 6f, Layout.ClipEdgeHandleWidth, clipRect.height - 12f), outline);
                }

                DrawClipLabels(clipRect, visual, viewport.width);
            }

            if (clipDragMode == ClipDragMode.None && clipRect.Contains(Event.current.mousePosition))
            {
                tooltipContent.text = string.Empty;
                tooltipContent.tooltip = BuildClipTooltip(index, visual.Title);
                GUI.Label(clipRect, tooltipContent, GUIStyle.none);
            }

            float edgeWidth = GetClipEdgeWidth(clipRect);
            EditorGUIUtility.AddCursorRect(new Rect(clipRect.x, clipRect.y, edgeWidth, clipRect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(clipRect.xMax - edgeWidth, clipRect.y, edgeWidth, clipRect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(clipRect.x + edgeWidth, clipRect.y, Mathf.Max(0f, clipRect.width - edgeWidth * 2f), clipRect.height), MouseCursor.MoveArrow);
        }

        /// <summary>
        /// The property name with the clip's length at the right end, then what the clip changes. The track header
        /// already shows the full module path. Clips without values keep the module path, or the property name when the
        /// path doesn't fit, over the length. Labels stay on the visible part of a clip that is scrolled partly out of view.
        /// </summary>
        void DrawClipLabels(Rect clipRect, ClipVisual visual, float viewportWidth)
        {
            float visibleLeft = Mathf.Max(clipRect.x, 0f);
            float labelWidth = Mathf.Min(clipRect.xMax, viewportWidth) - visibleLeft - Layout.ClipLabelHorizontalInset * 2f;
            if (labelWidth < Layout.ClipMinimumLabelWidth)
                return;

            float labelX = visibleLeft + Layout.ClipLabelHorizontalInset;
            float titleY = Mathf.Round(clipRect.y + (clipRect.height - 28f) * 0.5f);
            Rect secondLine = new(labelX, titleY + 15f, labelWidth, 13f);

            if (!visual.HasValueLine)
            {
                clipMeasureContent.text = visual.Title;
                string title = clipTitleMeasureStyle.CalcSize(clipMeasureContent).x <= labelWidth ? visual.Title : visual.ShortTitle;
                GUI.Label(new Rect(labelX, titleY, labelWidth, 16f), title, clipTitleStyle);
                GUI.Label(secondLine, visual.Meta, clipMetaStyle);
                return;
            }

            clipMeasureContent.text = visual.ShortTitle;
            float titleWidth = clipTitleMeasureStyle.CalcSize(clipMeasureContent).x;
            float metaWidth = MeasureMetaText(visual.Meta);
            bool showMeta = titleWidth + Layout.ClipMetaGap + metaWidth <= labelWidth;

            GUI.Label(new Rect(labelX, titleY, showMeta ? labelWidth - metaWidth - Layout.ClipMetaGap : labelWidth, 16f), visual.ShortTitle, clipTitleStyle);
            if (showMeta)
                GUI.Label(new Rect(labelX + labelWidth - metaWidth, titleY + 1f, metaWidth, 16f), visual.Meta, clipMetaStyle);

            if (visual.HasColors)
                DrawColorChange(secondLine, visual);
            else
                GUI.Label(secondLine, visual.ValueText, clipValueStyle);
        }

        // From and To as swatches, with the alpha percentages after them when only the alpha changes
        void DrawColorChange(Rect rect, ClipVisual visual)
        {
            float x = rect.x;
            float swatchY = Mathf.Round(rect.center.y - Layout.ClipSwatchHeight * 0.5f);
            if (visual.FromCurrent)
                x = DrawValueText(x, rect, "Current");
            else
                x = DrawColorSwatch(new Rect(x, swatchY, Layout.ClipSwatchWidth, Layout.ClipSwatchHeight), visual.FromColor);

            if (x + Layout.ClipSwatchWidth * 2f > rect.xMax)
                return;

            x = DrawValueText(x + Layout.ClipSwatchGap, rect, "→");
            x = DrawColorSwatch(new Rect(x + Layout.ClipSwatchGap, swatchY, Layout.ClipSwatchWidth, Layout.ClipSwatchHeight), visual.ToColor);
            if (visual.ValueText != null)
                GUI.Label(new Rect(x + Layout.ClipSwatchGap * 2f, rect.y, Mathf.Max(0f, rect.xMax - x - Layout.ClipSwatchGap * 2f), rect.height),
                    visual.ValueText, clipValueStyle);
        }

        float DrawValueText(float x, Rect line, string text)
        {
            float width = MeasureMetaText(text);
            GUI.Label(new Rect(x, line.y, width, line.height), text, clipValueStyle);
            return x + width;
        }

        // Whole points plus one, so an ellipsis style drawn at exactly the measured width doesn't clip its own text
        float MeasureMetaText(string text)
        {
            clipMeasureContent.text = text;
            return Mathf.Ceil(clipMetaMeasureStyle.CalcSize(clipMeasureContent).x) + 1f;
        }

        // Drawn over a light base, so a transparent color reads as empty instead of taking the clip's tint
        static float DrawColorSwatch(Rect rect, Color color)
        {
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f, SwatchBase, 0f, Layout.ClipSwatchRadius);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, Layout.ClipSwatchRadius);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f, SwatchBorder, 1f, Layout.ClipSwatchRadius);
            return rect.xMax;
        }

        /// <summary>
        /// Draws the clip's ease as a soft filled shape under a faint line, once per iteration. Yoyo iterations are
        /// mirrored, and overshooting eases rise toward the border. The fill is built from device-pixel columns that are
        /// merged while the curve stays within a pixel, which keeps it seamless without a rect per pixel.
        /// </summary>
        void DrawEaseSparkline(Rect clipRect, ClipVisual visual, ClipTiming timing, float naturalWidth, float viewportWidth)
        {
            int iterations = Mathf.Max(1, timing.Iterations);
            float left = clipRect.x + 2f;
            float iterationWidth = (naturalWidth - 4f) / iterations;
            if (iterationWidth < Layout.SparklineMinimumIterationWidth)
                return;

            float top = clipRect.y + 2f;
            float bottom = clipRect.yMax - 2f;
            float baseline = clipRect.yMax - Layout.SparklineVerticalInset;
            float height = clipRect.height - Layout.SparklineVerticalInset * 2f;
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float pixel = 1f / pixelsPerPoint;
            int samples = Mathf.Clamp(Mathf.RoundToInt(iterationWidth / Layout.SparklineSampleSpacing), 4, Layout.SparklineMaxSamples);
            Color fillColor = new(visual.Color.r, visual.Color.g, visual.Color.b, Layout.SparklineFillAlpha);
            Color previousHandlesColor = Handles.color;
            Handles.color = new Color(visual.Color.r, visual.Color.g, visual.Color.b, Layout.SparklineLineAlpha);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float iterationX = left + iteration * iterationWidth;
                float start = Mathf.Round(Mathf.Max(iterationX, 0f) * pixelsPerPoint) / pixelsPerPoint;
                float end = Mathf.Round(Mathf.Min(iterationX + iterationWidth, viewportWidth) * pixelsPerPoint) / pixelsPerPoint;
                if (end <= start)
                    continue;

                bool mirrored = timing.Loop == LoopMode.Yoyo && iteration % 2 == 1;
                float columnX = start;
                float columnY = GetSparklineY(visual, mirrored, (start + pixel * 0.5f - iterationX) / iterationWidth, baseline, height, top, bottom);
                for (float x = start + pixel; x < end - pixel * 0.5f; x += pixel)
                {
                    float y = GetSparklineY(visual, mirrored, (x + pixel * 0.5f - iterationX) / iterationWidth, baseline, height, top, bottom);
                    if (Mathf.Abs(y - columnY) <= 0.75f && x - columnX < Layout.SparklineSampleSpacing * 1.5f)
                        continue;

                    DrawSparklineColumn(columnX, x, columnY, bottom, pixelsPerPoint, fillColor);
                    columnX = x;
                    columnY = y;
                }
                DrawSparklineColumn(columnX, end, columnY, bottom, pixelsPerPoint, fillColor);

                for (int sample = 0; sample <= samples; sample++)
                {
                    float t = sample / (float)samples;
                    sparklinePoints[sample] = new Vector3(iterationX + t * iterationWidth,
                        GetSparklineY(visual, mirrored, t, baseline, height, top, bottom), 0f);
                }
                Handles.DrawAAPolyLine(Layout.SparklineLineWidth, samples + 1, sparklinePoints);
            }

            Handles.color = previousHandlesColor;
        }

        // Top and bottom snap to device pixels too, otherwise neighbouring columns round their edges differently
        static void DrawSparklineColumn(float left, float right, float top, float bottom, float pixelsPerPoint, Color color)
        {
            float snappedTop = Mathf.Round(top * pixelsPerPoint) / pixelsPerPoint;
            float snappedBottom = Mathf.Round(bottom * pixelsPerPoint) / pixelsPerPoint;
            if (snappedBottom > snappedTop)
                EditorGUI.DrawRect(Rect.MinMaxRect(left, snappedTop, right, snappedBottom), color);
        }

        static float GetSparklineY(ClipVisual visual, bool mirrored, float t, float baseline, float height, float top, float bottom)
        {
            float value = TweenEase.Evaluate(visual.Ease, mirrored ? 1f - t : t, visual.CustomEase);
            return Mathf.Clamp(baseline - value * height, top, bottom);
        }

        static float GetClipEdgeWidth(Rect clipRect) => Mathf.Min(Layout.ClipEdgeWidth, clipRect.width * 0.3f);

        Color GetRowColor(int index)
        {
            Color color = index % 2 == 0 ? RowBackground : AlternateRowBackground;
            return IsSelected(index) ? Color.Lerp(color, EvoEditorGUI.Palette.Selection, Layout.SelectedRowTint) : color;
        }

        // A label centered on x, kept inside the given width
        void DrawTimePill(float x, float y, string text, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text))
                return;

            clipMeasureContent.text = text;
            float pillWidth = Mathf.Ceil(dragTimeStyle.CalcSize(clipMeasureContent).x) + Layout.DragTimePillPadding * 2f;
            Rect pillRect = new(Mathf.Clamp(x - pillWidth * 0.5f, 0f, Mathf.Max(0f, width - pillWidth)), y, pillWidth, Layout.DragTimePillHeight);
            EvoEditorGUI.DrawRoundedBackground(pillRect, color);
            GUI.Label(pillRect, clipMeasureContent, dragTimeStyle);
        }

        void DrawInspector(Rect rect)
        {
            EditorGUI.DrawRect(rect, PanelBackground);
            float inspectorLeft = rect.x + Layout.InspectorPadding;
            float inspectorRight = rect.xMax - Layout.InspectorPadding;

            if (!HasSelection)
            {
                Rect messageArea = new(inspectorLeft, rect.y + Layout.InspectorPadding,
                    Mathf.Max(0f, inspectorRight - inspectorLeft), Mathf.Max(0f, rect.height - Layout.InspectorPadding * 2f));
                const float messageHeight = 82f;
                float messageY = Mathf.Max(messageArea.y, messageArea.center.y - messageHeight * 0.5f);
                GUI.Label(new Rect(messageArea.x + 12f, messageY, Mathf.Max(0f, messageArea.width - 24f), 24f),
                    "Select a clip to edit it", centeredTitleStyle);
                GUI.Label(new Rect(messageArea.x + 14f, messageY + 27f, Mathf.Max(0f, messageArea.width - 28f), 55f),
                    "Click a clip or track. Shift-click or drag a box to select several. Drag a clip to change its delay, and drag either edge to trim it.",
                    centeredBodyStyle);
                return;
            }

            int primary = PrimaryTrack;
            bool several = SelectedCount > 1;
            Rect titleRect = new(inspectorLeft, rect.y + Layout.InspectorTitleY, Mathf.Max(0f, inspectorRight - inspectorLeft), Layout.InspectorTitleHeight);
            TryGetSelectionRange(out float selectionStart, out float selectionEnd);
            float timeWidth = Mathf.Min(Layout.InspectorTimeWidth, titleRect.width * 0.48f);
            Rect timingRect = new((titleRect.xMax - 2) - timeWidth, titleRect.y, timeWidth, titleRect.height);
            Rect clipTitleRect = new((titleRect.x + 2), titleRect.y, Mathf.Max(0f, timingRect.x - titleRect.x - Layout.InspectorHeaderGap), titleRect.height);
            GUI.Label(clipTitleRect, several ? $"{SelectedCount} Clips" : GetTrackTitle(primary), headerTitleStyle);
            GUI.Label(timingRect, $"{selectionStart:0.###}s  →  {selectionEnd:0.###}s", headerTimeStyle);

            // The nested module inspector is by far the most expensive part of this window.
            // Unity sends continuous Layout/Repaint pairs while a dock divider moves, so briefly omit it until the
            // docked size settles. The lightweight timeline stays responsive and the final repaint restores it.
            if (suppressInspectorDetails)
                return;

            float contentOuterInset = Mathf.Max(0f, Layout.InspectorPadding - EvoEditorGUI.Metrics.ContainerPadding);
            Rect scrollRect = new(rect.x + contentOuterInset, rect.y + Layout.InspectorContentY,
                Mathf.Max(0f, rect.width - contentOuterInset * 2f), Mathf.Max(0f, rect.height - Layout.InspectorContentY - Layout.InspectorPadding));
            GUILayout.BeginArea(scrollRect);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

            float availableWidth = scrollRect.width - Layout.ScrollbarSize;
            EditorGUI.BeginChangeCheck();
            if (several)
            {
                drawChoreographyAction ??= DrawChoreographySection;
                detailsGUI.DrawSharedClipDetails(GetSelectedClipProperties(), availableWidth, "Choreography", drawChoreographyAction);
            }
            else
            {
                detailsGUI.DrawClipDetails(clips.GetArrayElementAtIndex(primary), GetTargetProperty(primary), owner, readOnly: false,
                    availableWidth: availableWidth);
            }

            if (EditorGUI.EndChangeCheck())
                serializedFieldsChanged = true;

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawInspectorSplitter(Rect rect)
        {
            EditorGUI.DrawRect(rect, SplitterBackground);
            // Extend into the inspector padding, leaving the adjacent timeline scrollbar unobstructed
            Rect hitRect = new(rect.x, rect.y, Layout.SplitterHitWidth, rect.height);
            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.ResizeHorizontal);

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(InspectorSplitterControlHint, FocusType.Passive, rect);
            if (current.type == EventType.MouseDown && current.button == 0 && hitRect.Contains(current.mousePosition))
            {
                resizingInspector = true;
                GUIUtility.hotControl = controlId;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && resizingInspector && GUIUtility.hotControl == controlId)
            {
                inspectorWidth = ClampInspectorWidth(position.width - current.mousePosition.x, position.width);
                Repaint();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0 && resizingInspector && GUIUtility.hotControl == controlId)
            {
                resizingInspector = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        static float ClampInspectorWidth(float value, float workspaceWidth)
        {
            float maximum = Mathf.Min(MaximumInspectorWidth, Mathf.Max(MinimumInspectorWidth, workspaceWidth * 0.48f));
            return Mathf.Clamp(value, MinimumInspectorWidth, maximum);
        }

        void DrawEmptyState(Rect rect)
        {
            EditorGUI.DrawRect(rect, WindowBackground);
            GUI.Label(new Rect(rect.x + 30f, rect.center.y - 38f, rect.width - 60f, 28f), "Select a Tween Timeline", centeredTitleStyle);
            GUI.Label(new Rect(rect.x + 80f, rect.center.y - 7f, rect.width - 160f, 42f),
                "Choose or drag a component that owns a serialized TweenSequence.", centeredBodyStyle);

            HandleOwnerDragAndDrop(rect);
        }

        void HandleOwnerDragAndDrop(Rect rect)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition) || current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;

            Component draggedOwner = FindOwner(DragAndDrop.objectReferences);
            DragAndDrop.visualMode = draggedOwner != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            if (current.type != EventType.DragPerform || draggedOwner == null)
                return;

            DragAndDrop.AcceptDrag();
            Bind(draggedOwner);
            current.Use();
        }

        Rect GetClipRect(int index, ClipTiming timing)
        {
            float width = Mathf.Max(Layout.MinimumClipWidth, timing.TotalDuration * pixelsPerSecond);
            return new Rect(TimeToX(timing.Start), index * Layout.TrackHeight - verticalScroll + Layout.ClipVerticalInset,
                width, Layout.TrackHeight - Layout.ClipVerticalInset * 2f);
        }

        void FrameAll()
        {
            float availableWidth = Mathf.Max(Layout.MinimumTimelineViewportWidth,
                GetTimelineViewportWidth() - Layout.TimelineEndPadding);
            float duration = Mathf.Max(DefaultClipDuration, timelineDuration);
            SetZoom(availableWidth / duration);
            horizontalScroll = 0f;
            Repaint();
        }

        void FrameSelection()
        {
            if (TryGetSelectionRange(out float start, out float end))
                FrameRange(start, end);
            else
                FrameAll();
        }

        void FrameClip(int index)
        {
            if (index >= 0 && index < clipTimings.Count)
                FrameRange(clipTimings[index].Start, clipTimings[index].End);
        }

        void FrameRange(float start, float end)
        {
            // Half the end padding on both sides keeps the trim handles clear of the viewport edges
            float margin = Layout.TimelineEndPadding * 0.5f;
            float span = Mathf.Max(MinimumClipDuration, end - start);
            SetZoom(Mathf.Max(Layout.MinimumTimelineViewportWidth, GetTimelineViewportWidth() - margin * 2f) / span);
            horizontalScroll = Mathf.Max(0f, start * pixelsPerSecond - margin);
            Repaint();
        }

        void FrameTime(float time)
        {
            float viewportWidth = Mathf.Max(Layout.MinimumTimelineViewportWidth, GetTimelineViewportWidth());
            horizontalScroll = Mathf.Max(0f, time * pixelsPerSecond - viewportWidth * 0.5f);
            Repaint();
        }

        // Pages the view when playback runs past either edge, the way Unity's Timeline follows its playhead
        void FollowPlayheadDuringPlayback()
        {
            if (Event.current.type != EventType.Layout || previewGUI?.IsPlaying != true || clipDragMode != ClipDragMode.None
                || scrubbingRuler || panningTimeline)
                return;

            float playheadX = TimeToX(CurrentTimelineTime);
            if (playheadX < 0f || playheadX > timelineViewportWidth - Layout.PlayheadEdgeMargin)
                horizontalScroll = Mathf.Max(0f, CurrentTimelineTime * pixelsPerSecond - Layout.PlayheadEdgeMargin);
        }

        // Scrolls just enough to keep the playhead a small margin away from the viewport edges
        void EnsureTimeVisible(float time)
        {
            float viewportWidth = GetTimelineViewportWidth();
            float x = TimeToX(time);
            if (x < Layout.PlayheadEdgeMargin)
                horizontalScroll = Mathf.Max(0f, time * pixelsPerSecond - Layout.PlayheadEdgeMargin);
            else if (x > viewportWidth - Layout.PlayheadEdgeMargin)
                horizontalScroll = Mathf.Max(0f, time * pixelsPerSecond + Layout.PlayheadEdgeMargin - viewportWidth);
        }

        // The canvas width from the last timeline pass, or an estimate from the window before the first one
        float GetTimelineViewportWidth() => timelineViewportWidth > 0f
            ? timelineViewportWidth
            : position.width - inspectorWidth - Layout.SplitterWidth - Layout.TrackHeaderWidth - Layout.ScrollbarSize;

        /// <summary>
        /// The timeline duration is not reliable until serialized sources have been updated during the
        /// first GUI layout. Defer framing until then instead of guessing from OnEnable.
        /// </summary>
        void ApplyInitialFrameIfReady()
        {
            if (!frameTimelineOnNextLayout || Event.current.type != EventType.Layout || clips == null)
                return;

            frameTimelineOnNextLayout = false;
            FrameAll();
        }

        float CurrentTimelineTime => clipDragMode != ClipDragMode.None ? dragPreviewTime : recording ? recordingTime : previewGUI?.CurrentTime ?? 0f;

        float SnapTime(float time)
        {
            time = Mathf.Max(0f, time);
            return snapEnabled && snapFrameRate > 0 ? Mathf.Round(time * snapFrameRate) / snapFrameRate : time;
        }

        /// <summary>
        /// Labeled major ticks and the minor ticks between them. While snapping, both fall on whole frames of the
        /// snapping rate, so the playhead and clip edges line up with the ticks around them.
        /// </summary>
        void GetRulerSteps(out float majorStep, out float minorStep, out int minorsPerMajor)
        {
            if (!snapEnabled)
            {
                majorStep = RulerSteps[^1];
                for (int i = 0; i < RulerSteps.Length; i++)
                {
                    if (RulerSteps[i] * pixelsPerSecond >= MinimumMajorTickSpacing)
                    {
                        majorStep = RulerSteps[i];
                        break;
                    }
                }

                minorsPerMajor = 5;
                minorStep = majorStep / minorsPerMajor;
                return;
            }

            // Labels stay on clean decimal steps; only steps that are a whole number of frames qualify
            int majorFrames = snapFrameRate;
            majorStep = 1f;
            for (int i = 0; i < RulerSteps.Length; i++)
            {
                float frames = RulerSteps[i] * snapFrameRate;
                int wholeFrames = Mathf.RoundToInt(frames);
                if (wholeFrames < 1 || Mathf.Abs(frames - wholeFrames) > 0.001f)
                    continue;

                majorFrames = wholeFrames;
                majorStep = RulerSteps[i];
                if (majorStep * pixelsPerSecond >= MinimumMajorTickSpacing)
                    break;
            }

            // The finest whole-frame division of the major step that stays readable
            float frameWidth = pixelsPerSecond / snapFrameRate;
            int minorFrames = majorFrames;
            for (int frames = 1; frames < majorFrames; frames++)
            {
                if (majorFrames % frames == 0 && frames * frameWidth >= MinimumMinorTickSpacing)
                {
                    minorFrames = frames;
                    break;
                }
            }

            minorsPerMajor = majorFrames / minorFrames;
            minorStep = majorStep / minorsPerMajor;
        }

        static string GetTrackTitle(SerializedProperty settings, TweenModule module) => settings != null ? TweenClipGUI.GetModuleTitle(module) : "Missing Clip";

        string GetTrackTitle(int index) => index < clipVisuals.Count ? clipVisuals[index].Title
            : GetTrackTitle(clips.GetArrayElementAtIndex(index).FindPropertyRelative("settings"), GetClipModule(index));

        bool TryGetDragEdgeTime(out float time)
        {
            time = 0f;
            if (clipDragMode == ClipDragMode.None || draggedTrack < 0 || draggedTrack >= clipTimings.Count)
                return false;

            ClipTiming timing = clipTimings[draggedTrack];
            time = clipDragMode == ClipDragMode.TrimEnd ? timing.End : timing.Start;
            return true;
        }

        string BuildClipTooltip(int index, string title)
        {
            if (index < 0 || index >= clipTimings.Count)
                return string.Empty;

            ClipTiming timing = clipTimings[index];
            string looping = timing.Loop == LoopMode.None
                ? "No Loop"
                : $"{timing.Loop} • {timing.Iterations} Iterations";
            return $"{title}\n" + $"{timing.Flow} • {timing.Start:0.###}s – {timing.End:0.###}s\n" +
                   $"{looping}\nDrag to move. Drag either edge to trim. Double-click to frame. Right-click for more actions.";
        }

        /// <summary>
        /// Describes what a clip changes, such as "0 → 1" or "Current → 48". Vectors list only the axes that change, and
        /// axes that change the same way share one entry: "XY 0.92 → 1". Returns null for value types it can't describe.
        /// </summary>
        static string FormatValueChange(SerializedProperty from, SerializedProperty to)
        {
            switch (to.propertyType)
            {
                case SerializedPropertyType.Float:
                    return FormatRange(from != null ? FormatNumber(from.floatValue) : null, FormatNumber(to.floatValue));
                case SerializedPropertyType.Integer:
                    return FormatRange(from?.intValue.ToString(CultureInfo.InvariantCulture), to.intValue.ToString(CultureInfo.InvariantCulture));
                case SerializedPropertyType.Boolean:
                    return FormatRange(from != null ? FormatToggle(from.boolValue) : null, FormatToggle(to.boolValue));
                case SerializedPropertyType.Enum:
                    return FormatRange(from != null ? GetEnumName(from) : null, GetEnumName(to));
                case SerializedPropertyType.Vector2:
                    return FormatVectorChange(from, to, 2);
                case SerializedPropertyType.Vector3:
                    return FormatVectorChange(from, to, 3);
                case SerializedPropertyType.Vector4:
                    return FormatVectorChange(from, to, 4);
                default:
                    return null;
            }
        }

        static string FormatVectorChange(SerializedProperty from, SerializedProperty to, int axisCount)
        {
            const string AxisNames = "XYZW";
            Vector4 end = GetVector(to);
            if (from == null)
                return FormatRange(null, FormatVector(end, axisCount));

            Vector4 start = GetVector(from);
            StringBuilder text = new();
            int merged = 0;
            for (int axis = 0; axis < axisCount; axis++)
            {
                string startText = FormatNumber(start[axis]);
                string endText = FormatNumber(end[axis]);
                if ((merged & (1 << axis)) != 0 || startText == endText)
                    continue;

                if (text.Length > 0)
                    text.Append(" · ");

                text.Append(AxisNames[axis]);
                for (int other = axis + 1; other < axisCount; other++)
                {
                    if (FormatNumber(start[other]) == startText && FormatNumber(end[other]) == endText)
                    {
                        text.Append(AxisNames[other]);
                        merged |= 1 << other;
                    }
                }

                text.Append(' ').Append(startText).Append(" → ").Append(endText);
            }

            return text.Length > 0 ? text.ToString() : FormatVector(end, axisCount);
        }

        // Colors are drawn as swatches, so only an alpha-only change needs text
        static string FormatAlphaChange(SerializedProperty from, SerializedProperty to)
        {
            if (from == null)
                return null;

            Color start = from.colorValue;
            Color end = to.colorValue;
            bool sameColor = FormatNumber(start.r) == FormatNumber(end.r) && FormatNumber(start.g) == FormatNumber(end.g)
                && FormatNumber(start.b) == FormatNumber(end.b);
            return sameColor && FormatNumber(start.a) != FormatNumber(end.a)
                ? $"{Mathf.RoundToInt(start.a * 100f)}% → {Mathf.RoundToInt(end.a * 100f)}%"
                : null;
        }

        static string FormatRange(string from, string to) => $"{from ?? "Current"} → {to}";

        static string FormatNumber(float value)
        {
            string text = value.ToString("0.##", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        static string FormatToggle(bool value) => value ? "On" : "Off";

        static string GetEnumName(SerializedProperty property)
        {
            string[] names = property.enumDisplayNames;
            return property.enumValueIndex >= 0 && property.enumValueIndex < names.Length ? names[property.enumValueIndex] : "?";
        }

        static string FormatVector(Vector4 value, int axisCount)
        {
            StringBuilder text = new("(");
            for (int axis = 0; axis < axisCount; axis++)
                text.Append(axis > 0 ? ", " : string.Empty).Append(FormatNumber(value[axis]));
            return text.Append(')').ToString();
        }

        static Vector4 GetVector(SerializedProperty property) => property.propertyType switch
        {
            SerializedPropertyType.Vector2 => property.vector2Value,
            SerializedPropertyType.Vector3 => property.vector3Value,
            _ => property.vector4Value
        };

        static Color GetClipColor(string title)
        {
            int hash = 17;
            if (title != null)
            {
                for (int i = 0; i < title.Length; i++)
                    hash = unchecked(hash * 31 + title[i]);
            }
            float hue = (Math.Abs(hash % 997) / 997f + 0.52f) % 1f;
            Color color = Color.HSVToRGB(hue, EditorGUIUtility.isProSkin ? 0.48f : 0.58f, EditorGUIUtility.isProSkin ? 0.9f : 0.72f);
            color.a = 1f;
            return color;
        }

        static bool DrawEvoButton(Rect rect, string text, Texture2D icon, ButtonOptions options = default)
        {
            GUILayout.BeginArea(rect);
            bool clicked = EvoEditorGUI.Button(text, icon, options.WithSize(rect.width, rect.height));
            GUILayout.EndArea();
            return clicked;
        }

        static int DrawEvoIntPopup(Rect rect, string label, int selectedValue, string[] displayNames, int[] values, int labelWidth, string tooltip = null)
        {
            Rect contentRect = DrawEvoFieldRow(rect);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            Rect popupRect = EditorGUI.PrefixLabel(contentRect, new GUIContent(label, tooltip));
            int result = EditorGUI.IntPopup(popupRect, selectedValue, displayNames, values);
            EditorGUIUtility.labelWidth = previousLabelWidth;
            return result;
        }

        static void DrawEvoProperty(Rect rect, SerializedProperty property, string label, int labelWidth)
        {
            Rect contentRect = DrawEvoFieldRow(rect);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUI.PropertyField(contentRect, property, new GUIContent(label, property.tooltip));
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        static Rect DrawEvoFieldRow(Rect rect)
        {
            GUILayout.BeginArea(rect);
            EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor(), GUILayout.Width(rect.width), GUILayout.Height(rect.height));
            GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);
            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight,
                               GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(true));
            controlRect.y = Mathf.Round((rect.height - controlRect.height) * 0.5f);
            EvoEditorGUI.EndRow();
            GUILayout.EndArea();

            controlRect.position += rect.position;
            return controlRect;
        }

        static ButtonOptions GetToggleButtonOptions(bool active, string tooltip, int iconSize)
        {
            ButtonOptions options = ButtonOptions.WithTooltip(tooltip).WithIconSize(iconSize).WithInvertedColor();
            return active ? options.WithNormalColor(EvoEditorGUI.Palette.GetBackgroundColor(invert: true, hover: true)) : options;
        }

        bool DrawFlowButton(Rect rect, ClipFlow flow)
        {
            bool sequential = flow == ClipFlow.Sequential;
            Texture2D icon = sequential ? sequentialFlowIcon : parallelFlowIcon;
            string currentMode = sequential ? "Sequential" : "Parallel";
            string nextMode = sequential ? "parallel" : "sequential";
            ButtonOptions options = GetModeButtonOptions(sequential,
                $"{currentMode} flow. Click to switch to {nextMode}.").WithIconSize(Layout.TrackBadgeIconSize);
            return DrawEvoButton(rect, string.Empty, icon, options);
        }

        static bool DrawModeButton(Rect rect, string text, bool active, string tooltip)
        {
            ButtonOptions options = GetModeButtonOptions(active, tooltip).WithFontSize(Layout.ModeButtonFontSize);
            return DrawEvoButton(rect, text, null, options);
        }

        static ButtonOptions GetModeButtonOptions(bool active, string tooltip)
        {
            ButtonOptions options = ButtonOptions.WithTooltip(tooltip).WithInvertedColor();
            if (active)
            {
                Color normal = new(AccentColor.r, AccentColor.g, AccentColor.b, EditorGUIUtility.isProSkin ? 0.34f : 0.25f);
                Color hover = new(AccentColor.r, AccentColor.g, AccentColor.b, EditorGUIUtility.isProSkin ? 0.46f : 0.34f);
                Color pressed = new(AccentColor.r, AccentColor.g, AccentColor.b, EditorGUIUtility.isProSkin ? 0.26f : 0.2f);
                options = options.WithColors(normal, hover, pressed);
            }
            return options;
        }

        static void DrawRoundedClipBox(Rect rect, Color bodyColor, Color outlineColor)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EvoEditorGUI.DrawRoundedBackground(rect, bodyColor);
            EvoEditorGUI.DrawRoundedBorder(rect, outlineColor);
        }

        static void DrawDragHandle(Rect rect, bool active)
        {
            Color color = active ? AccentColor : MutedText;
            float gripWidth = (Layout.TrackGripColumns - 1) * Layout.TrackGripDotSpacing + Layout.TrackGripDotSize;
            float gripHeight = (Layout.TrackGripRows - 1) * Layout.TrackGripDotSpacing + Layout.TrackGripDotSize;
            Vector2 origin = new(rect.center.x - gripWidth * 0.5f, rect.center.y - gripHeight * 0.5f);

            for (int y = 0; y < Layout.TrackGripRows; y++)
            {
                for (int x = 0; x < Layout.TrackGripColumns; x++)
                {
                    EditorGUI.DrawRect(new Rect(origin.x + x * Layout.TrackGripDotSpacing, origin.y + y * Layout.TrackGripDotSpacing,
                        Layout.TrackGripDotSize, Layout.TrackGripDotSize), color);
                }
            }
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);
        }

        static void DrawPlayheadCap(float x, float y)
        {
            Vector3[] points =
            {
                new(x - 5f, y),
                new(x + 5f, y),
                new(x, y + 7f)
            };
            Color previousColor = Handles.color;
            Handles.color = PlayheadColor;
            Handles.DrawAAConvexPolygon(points);
            Handles.color = previousColor;
        }
    }
}