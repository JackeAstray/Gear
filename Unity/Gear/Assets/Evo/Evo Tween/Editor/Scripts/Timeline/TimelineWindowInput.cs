using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Focus, pointer, keyboard, scrolling, clip dragging, and track reordering.
    /// </summary>
    public sealed partial class TimelineWindow
    {
        /// <summary>
        /// IMGUI keeps the last text/numeric field focused until another control explicitly takes focus.
        /// Clear it before processing a primary click so empty timeline areas and custom controls behave
        /// like normal editor surfaces; a field clicked by this event will immediately claim focus again.
        /// </summary>
        static void ReleaseKeyboardFocusOnMouseDown()
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0)
                return;

            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        void HandleRulerInput(Rect rect)
        {
            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(RulerControlHint, FocusType.Passive, rect);

            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                scrubbingRuler = true;
                GUIUtility.hotControl = controlId;
                ScrubTimeline(SnapTime(GetTimelineTimeAt(current.mousePosition.x, rect.x)));
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && scrubbingRuler && GUIUtility.hotControl == controlId)
            {
                // Dragging past either edge scrolls the view along with the playhead
                float time = SnapTime(GetTimelineTimeAt(current.mousePosition.x, rect.x));
                ScrubTimeline(time);
                EnsureTimeVisible(time);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0 && scrubbingRuler && GUIUtility.hotControl == controlId)
            {
                scrubbingRuler = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        // Drags and box selections hold the pointer through controlId, so they keep following it outside the canvas
        void HandleClipInput(Rect rect, int controlId)
        {
            Event current = Event.current;
            Vector2 mouse = current.mousePosition;
            int trackUnderMouse = GetTrackAt(mouse.y);
            EventType type = current.GetTypeForControl(controlId);

            if (clipDragMode != ClipDragMode.None)
            {
                if (type == EventType.MouseDrag && current.button == 0)
                {
                    dragMoved |= Mathf.Abs(mouse.x - dragMouseStartX) > Layout.ClickDragThreshold;
                    float mouseTime = GetTimelineTimeAt(mouse.x);
                    ApplyClipDrag(mouseTime - dragMouseStartTime, EditorGUI.actionKey, current.alt);
                    current.Use();
                }
                else if (type == EventType.MouseUp && current.button == 0)
                {
                    bool moved = dragMoved;
                    EndClipDrag();
                    if (!moved)
                        ApplyPendingClick();

                    Repaint();
                    current.Use();
                }
                else if (type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
                {
                    CancelClipDrag();
                    current.Use();
                }
                return;
            }

            if (boxSelecting)
            {
                HandleBoxSelection(current, type);
                return;
            }

            if (current.type == EventType.MouseMove)
            {
                UpdateHoveredClip(rect, mouse, trackUnderMouse);
                return;
            }

            if (current.type != EventType.MouseDown || !rect.Contains(mouse))
                return;

            // Middle-dragging belongs to timeline panning and must not change the current selection.
            if (current.button == 2)
                return;

            bool onTrack = clips != null && trackUnderMouse >= 0 && trackUnderMouse < clips.arraySize && trackUnderMouse < clipTimings.Count;
            Rect clipRect = onTrack ? GetClipRect(trackUnderMouse, clipTimings[trackUnderMouse]) : default;
            if (!onTrack || !clipRect.Contains(mouse))
            {
                if (current.button == 0)
                {
                    BeginBoxSelection(mouse);
                    GUIUtility.hotControl = controlId;
                }
                else if (current.button == 1)
                    ShowEmptyAreaContextMenu();

                Repaint();
                current.Use();
                return;
            }

            if (current.button == 1)
            {
                ShowClipContextMenu(trackUnderMouse);
                current.Use();
                return;
            }

            if (current.button != 0)
                return;

            if (current.clickCount == 2)
            {
                SelectTrack(trackUnderMouse);
                FrameClip(trackUnderMouse);
                current.Use();
                return;
            }

            SelectOnMouseDown(trackUnderMouse, current);
            BeginClipDrag(trackUnderMouse, GetClipZone(clipRect, mouse.x), GetTimelineTimeAt(mouse.x), mouse.x);
            if (clipDragMode != ClipDragMode.None)
                GUIUtility.hotControl = controlId;

            Repaint();
            current.Use();
        }

        void UpdateHoveredClip(Rect rect, Vector2 mouse, int track)
        {
            if (rect.Contains(mouse) && clips != null && track >= 0 && track < clips.arraySize && track < clipTimings.Count)
            {
                Rect clipRect = GetClipRect(track, clipTimings[track]);
                if (clipRect.Contains(mouse))
                {
                    SetHoveredClip(track, GetClipZone(clipRect, mouse.x));
                    return;
                }
            }

            SetHoveredClip(-1, ClipDragMode.None);
        }

        void SetHoveredClip(int track, ClipDragMode zone)
        {
            if (track == hoveredTrack && zone == hoveredZone)
                return;

            hoveredTrack = track;
            hoveredZone = zone;
            Repaint();
        }

        static ClipDragMode GetClipZone(Rect clipRect, float mouseX)
        {
            float edgeWidth = GetClipEdgeWidth(clipRect);
            return mouseX <= clipRect.x + edgeWidth
                ? ClipDragMode.TrimStart
                : mouseX >= clipRect.xMax - edgeWidth
                    ? ClipDragMode.TrimEnd
                    : ClipDragMode.Move;
        }

        void HandleTrackReorder(Rect rect)
        {
            Event current = Event.current;
            int count = clips?.arraySize ?? 0;

            if (reorderSource >= 0)
            {
                if (current.type == EventType.MouseDrag)
                {
                    reorderTarget = Mathf.Clamp(GetTrackAt(current.mousePosition.y - rect.y), 0, Mathf.Max(0, count - 1));
                    Repaint();
                    current.Use();
                }
                else if (current.type == EventType.MouseUp)
                {
                    int source = reorderSource;
                    int target = reorderTarget;
                    reorderSource = -1;
                    reorderTarget = -1;
                    if (source >= 0 && target >= 0 && source != target)
                        MoveTrack(source, target);
                    current.Use();
                }
                else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
                {
                    reorderSource = -1;
                    reorderTarget = -1;
                    Repaint();
                    current.Use();
                }
                return;
            }

            if (current.type != EventType.MouseDown || !rect.Contains(current.mousePosition))
                return;

            int index = GetTrackAt(current.mousePosition.y - rect.y);
            if (index < 0 || index >= count)
            {
                if (current.button == 1)
                {
                    ShowEmptyAreaContextMenu();
                    current.Use();
                }
                return;
            }

            Rect handleRect = new(rect.x + Layout.TrackGripX - Layout.TrackGripHitPaddingX,
                rect.y + index * Layout.TrackHeight - verticalScroll + (Layout.TrackHeight - Layout.TrackGripHitHeight) * 0.5f,
                Layout.TrackGripWidth + Layout.TrackGripHitPaddingX * 2f, Layout.TrackGripHitHeight);

            if (current.button == 1)
            {
                ShowClipContextMenu(index);
                current.Use();
            }
            else if (current.button == 0 && handleRect.Contains(current.mousePosition))
            {
                // A reorder moves one track, so it selects only that one
                SelectTrack(index);
                reorderSource = index;
                reorderTarget = index;
                current.Use();
            }
            else if (current.button == 0)
            {
                SelectOnMouseDown(index, current);
                ApplyPendingClick();
            }

            Repaint();
        }

        void HandleTimelineScroll(Rect interactionRect, Rect canvasRect, float maxHorizontalScroll)
        {
            Event current = Event.current;
            int panControlId = GUIUtility.GetControlID(TimelinePanControlHint, FocusType.Passive, canvasRect);
            if (panningTimeline && GUIUtility.hotControl != panControlId)
                panningTimeline = false;

            if (panningTimeline)
            {
                EditorGUIUtility.AddCursorRect(canvasRect, MouseCursor.Pan);

                if (current.type == EventType.MouseDrag && current.button == 2)
                {
                    float dragDistance = current.mousePosition.x - panMouseStartX;
                    horizontalScroll = Mathf.Clamp(panScrollStart - dragDistance, 0f, maxHorizontalScroll);
                    Repaint();
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && current.button == 2)
                {
                    panningTimeline = false;
                    GUIUtility.hotControl = 0;
                    Repaint();
                    current.Use();
                }
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 2 && maxHorizontalScroll > 0f && canvasRect.Contains(current.mousePosition))
            {
                panningTimeline = true;
                panMouseStartX = current.mousePosition.x;
                panScrollStart = horizontalScroll;
                GUIUtility.hotControl = panControlId;
                Repaint();
                current.Use();
                return;
            }

            if (current.type != EventType.ScrollWheel || !interactionRect.Contains(current.mousePosition))
                return;

            if (current.shift)
            {
                horizontalScroll = Mathf.Clamp(horizontalScroll + current.delta.y * 24f + current.delta.x * 18f, 0f, maxHorizontalScroll);
            }
            else if (Mathf.Abs(current.delta.x) > Mathf.Abs(current.delta.y))
            {
                // Trackpads and tilt wheels scroll sideways on the x axis, which pans instead of zooming
                horizontalScroll = Mathf.Clamp(horizontalScroll + current.delta.x * 18f, 0f, maxHorizontalScroll);
            }
            else if (current.alt || current.mousePosition.x < canvasRect.x)
            {
                // Keep ordinary track-list scrolling available without taking wheel zoom away from the canvas
                verticalScroll = Mathf.Max(0f, verticalScroll + current.delta.y * 18f);
            }
            else
            {
                float pointerX = current.mousePosition.x - canvasRect.x;
                float cursorTime = GetTimelineTimeAt(pointerX);
                float zoomFactor = Mathf.Pow(1.12f, -current.delta.y);
                float newZoom = Mathf.Clamp(pixelsPerSecond * zoomFactor, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
                if (!Mathf.Approximately(newZoom, pixelsPerSecond))
                {
                    SetZoom(newZoom);
                    horizontalScroll = Mathf.Max(0f, cursorTime * pixelsPerSecond - pointerX);
                }
            }

            Repaint();
            current.Use();
        }

        void HandleKeyboardCommands()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
                return;

            bool actionKey = current.control || current.command;
            if (current.keyCode == KeyCode.Space)
            {
                ToggleTimelinePreview();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                current.Use();
            }
            else if (actionKey && current.keyCode == KeyCode.D)
            {
                DuplicateSelected();
                current.Use();
            }
            else if (actionKey && current.keyCode == KeyCode.C)
            {
                CopySelected();
                current.Use();
            }
            else if (actionKey && current.keyCode == KeyCode.V)
            {
                // One copied clip replaces the selected clips, like the Inspector list; otherwise the copies are added
                if (HasSelection && TweenClipGUI.ClipboardCount == 1)
                    PasteSelected();
                else
                    PasteAsNewClip();
                current.Use();
            }
            else if (actionKey && current.keyCode == KeyCode.A)
            {
                SelectAll();
                current.Use();
            }
            else if (!actionKey && current.keyCode == KeyCode.F)
            {
                FrameSelection();
                current.Use();
            }
            else if (!actionKey && current.keyCode == KeyCode.A)
            {
                FrameAll();
                current.Use();
            }
            else if (!actionKey && current.keyCode == KeyCode.L)
            {
                ToggleLoopPreview();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Home)
            {
                MovePlayhead(0f);
                current.Use();
            }
            else if (current.keyCode == KeyCode.End)
            {
                MovePlayhead(timelineDuration);
                current.Use();
            }
            else if (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.RightArrow)
            {
                int direction = current.keyCode == KeyCode.RightArrow ? 1 : -1;
                if (current.shift)
                    JumpToClipEdge(direction);
                else
                    StepFrame(direction);
                current.Use();
            }
            else if (current.keyCode == KeyCode.UpArrow || current.keyCode == KeyCode.DownArrow)
            {
                SelectAdjacentTrack(current.keyCode == KeyCode.DownArrow ? 1 : -1);
                current.Use();
            }
            else if (current.keyCode == KeyCode.Escape && HasSelection)
            {
                ClearSelection();
                Repaint();
                current.Use();
            }
        }

        void MovePlayhead(float time)
        {
            ScrubTimeline(time);
            EnsureTimeVisible(time);
            Repaint();
        }

        // Steps by one frame of the snapping rate; recording may step past the end to extend the sequence
        void StepFrame(int direction)
        {
            int frameRate = Mathf.Max(1, snapFrameRate);
            float time = Mathf.Max(0f, (Mathf.RoundToInt(CurrentTimelineTime * frameRate) + direction) / (float)frameRate);
            MovePlayhead(recording ? time : Mathf.Min(time, timelineDuration));
        }

        void JumpToClipEdge(int direction)
        {
            float currentTime = CurrentTimelineTime;
            float target = direction > 0 ? Mathf.Max(currentTime, timelineDuration) : 0f;

            for (int i = 0; i < clipTimings.Count; i++)
            {
                ClipTiming timing = clipTimings[i];
                target = GetNearerEdge(timing.Start, currentTime, target, direction);
                target = GetNearerEdge(timing.End, currentTime, target, direction);
            }

            MovePlayhead(target);
        }

        static float GetNearerEdge(float edge, float currentTime, float best, int direction)
        {
            if (direction > 0)
                return edge > currentTime + RecordingTimeTolerance && edge < best ? edge : best;

            return edge < currentTime - RecordingTimeTolerance && edge > best ? edge : best;
        }

        void SelectAdjacentTrack(int direction)
        {
            int count = clips?.arraySize ?? 0;
            if (count == 0)
                return;

            int primary = PrimaryTrack;
            int index = primary < 0
                ? direction > 0 ? 0 : count - 1
                : Mathf.Clamp(primary + direction, 0, count - 1);
            SelectTrack(index);
            trackToReveal = index;
            Repaint();
        }

        /// <summary>
        /// Starts moving or trimming a clip. A move takes every selected clip along; a trim changes only the clip under
        /// the pointer.
        /// </summary>
        void BeginClipDrag(int index, ClipDragMode mode, float mouseTime, float mouseX)
        {
            if (definitionOwner?.targetObject == null || index < 0 || index >= clipTimings.Count)
                return;

            draggedClips.Clear();
            if (mode == ClipDragMode.Move)
            {
                List<int> selection = GetSortedSelection();
                for (int i = 0; i < selection.Count; i++)
                {
                    if (selection[i] < clipTimings.Count)
                        AddDraggedClip(selection[i]);
                }
            }

            if (!IsDragged(index))
                AddDraggedClip(index);

            CollectSnapEdges(index, CurrentTimelineTime);
            ClipTiming timing = clipTimings[index];
            clipDragMode = mode;
            draggedTrack = index;
            dragMouseStartTime = mouseTime;
            dragMouseStartX = mouseX;
            dragMoved = false;
            dragOriginalDuration = timing.Duration;
            dragOriginalStart = timing.Start;
            dragOriginalEnd = timing.End;
            dragGroupStart = timing.GroupStart;
            dragIterations = Mathf.Max(1, timing.Iterations);
            dragPreviewTime = previewGUI?.CurrentTime ?? 0f;

            previewGUI?.BeginLiveEdit();
            Undo.RecordObject(definitionOwner.targetObject, mode != ClipDragMode.Move ? "Trim Tween Clip"
                : draggedClips.Count > 1 ? "Move Tween Clips" : "Move Tween Clip");
        }

        void AddDraggedClip(int index)
        {
            ClipTiming timing = clipTimings[index];
            draggedClips.Add(new DraggedClip(index, timing.Delay, timing.Start));
        }

        bool IsDragged(int index)
        {
            for (int i = 0; i < draggedClips.Count; i++)
            {
                if (draggedClips[i].Index == index)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Edges a dragged clip can lock onto. Clips in later flow groups are left out: their position depends on the
        /// dragged clip, so their edges move while it is dragged. So are the other clips moving with it.
        /// </summary>
        void CollectSnapEdges(int index, float playheadTime)
        {
            dragSnapEdges.Clear();
            dragSnapEdges.Add(0f);
            dragSnapEdges.Add(playheadTime);
            dragSnapEdges.Add(clipTimings[index].GroupStart);

            for (int i = 0; i < index; i++)
            {
                if (IsDragged(i))
                    continue;

                dragSnapEdges.Add(clipTimings[i].Start);
                dragSnapEdges.Add(clipTimings[i].End);
            }

            for (int i = index + 1; i < clipTimings.Count && clipTimings[i].Flow == ClipFlow.Parallel; i++)
            {
                if (IsDragged(i))
                    continue;

                dragSnapEdges.Add(clipTimings[i].Start);
                dragSnapEdges.Add(clipTimings[i].End);
            }
        }

        void ApplyClipDrag(float deltaTime, bool freeDrag, bool fan)
        {
            if (definitionOwner?.targetObject == null || !TryGetTimingProperties(draggedTrack, out SerializedProperty delay, out SerializedProperty duration))
                return;

            switch (clipDragMode)
            {
                case ClipDragMode.Move:
                {
                    float snappedStart = SnapDragStart(dragOriginalStart + deltaTime, dragOriginalEnd - dragOriginalStart, freeDrag);
                    float start = Mathf.Max(dragGroupStart, snappedStart);
                    dragSnappedToEdge &= Mathf.Approximately(start, snappedStart);
                    MoveDraggedClips(start - dragOriginalStart, fan);
                    break;
                }
                case ClipDragMode.TrimStart:
                {
                    float latestStart = Mathf.Max(dragGroupStart, dragOriginalEnd - MinimumClipDuration * dragIterations);
                    float snappedStart = SnapDragTime(dragOriginalStart + deltaTime, freeDrag);
                    float start = Mathf.Clamp(snappedStart, dragGroupStart, latestStart);
                    dragSnappedToEdge &= Mathf.Approximately(start, snappedStart);
                    delay.floatValue = Mathf.Max(0f, start - dragGroupStart);
                    duration.floatValue = Mathf.Max(MinimumClipDuration, (dragOriginalEnd - start) / dragIterations);
                    break;
                }
                case ClipDragMode.TrimEnd:
                {
                    float snappedEnd = SnapDragTime(dragOriginalEnd + deltaTime, freeDrag);
                    float end = Mathf.Max(dragOriginalStart + MinimumClipDuration * dragIterations, snappedEnd);
                    dragSnappedToEdge &= Mathf.Approximately(end, snappedEnd);
                    duration.floatValue = Mathf.Max(MinimumClipDuration, (end - dragOriginalStart) / dragIterations);
                    break;
                }
            }

            definitionOwner.ApplyModifiedProperties();
            EditorUtility.SetDirty(definitionOwner.targetObject);
            definitionOwner.UpdateIfRequiredOrScript();
            BuildClipTimings();
            Repaint();
        }

        /// <summary>
        /// Moves every dragged clip by <paramref name="offset"/>. With <paramref name="fan"/>, each clip moves by a share
        /// of it instead, from none for the selected clip farthest from the pointer to all of it for the dragged one,
        /// which spreads the selection into a stagger.
        /// </summary>
        void MoveDraggedClips(float offset, bool fan)
        {
            int dragged = 0;
            float minimumDelay = float.MaxValue;
            for (int i = 0; i < draggedClips.Count; i++)
            {
                if (draggedClips[i].Index == draggedTrack)
                    dragged = i;
                minimumDelay = Mathf.Min(minimumDelay, draggedClips[i].Delay);
            }

            int pivot = dragged * 2 >= draggedClips.Count ? 0 : draggedClips.Count - 1;
            float span = Mathf.Abs(dragged - pivot);
            fan &= span > 0f;

            // Together, the clips keep their spacing when one reaches its group's start; fanned out, each stops there on its own
            if (!fan)
                offset = Mathf.Max(offset, -minimumDelay);

            for (int i = 0; i < draggedClips.Count; i++)
            {
                placedClips.Add(draggedClips[i].Index);
                placedStarts.Add(draggedClips[i].Start + (fan ? offset * Mathf.Abs(i - pivot) / span : offset));
            }

            PlaceClips();
        }

        // Another clip's edge within a few pixels wins over the frame grid; the action key places the edge freely
        float SnapDragTime(float time, bool freeDrag)
        {
            dragSnappedToEdge = false;
            time = Mathf.Max(0f, time);
            if (freeDrag || !snapEnabled)
                return time;

            if (TryFindSnapEdge(time, out float edge, out _))
            {
                dragSnappedToEdge = true;
                dragSnapEdgeTime = edge;
                return edge;
            }

            return SnapTime(time);
        }

        // A moved clip locks onto whichever of its two edges is closer to another edge
        float SnapDragStart(float start, float length, bool freeDrag)
        {
            dragSnappedToEdge = false;
            start = Mathf.Max(0f, start);
            if (freeDrag || !snapEnabled)
                return start;

            bool startHit = TryFindSnapEdge(start, out float startEdge, out float startDistance);
            bool endHit = TryFindSnapEdge(start + length, out float endEdge, out float endDistance);
            if (startHit && (!endHit || startDistance <= endDistance))
            {
                dragSnappedToEdge = true;
                dragSnapEdgeTime = startEdge;
                return startEdge;
            }

            if (endHit && endEdge - length >= 0f)
            {
                dragSnappedToEdge = true;
                dragSnapEdgeTime = endEdge;
                return endEdge - length;
            }

            return SnapTime(start);
        }

        bool TryFindSnapEdge(float time, out float edge, out float distance)
        {
            float threshold = Layout.SnapMagnetDistance / pixelsPerSecond;
            edge = 0f;
            distance = float.MaxValue;

            for (int i = 0; i < dragSnapEdges.Count; i++)
            {
                float candidateDistance = Mathf.Abs(dragSnapEdges[i] - time);
                if (candidateDistance <= threshold && candidateDistance < distance)
                {
                    edge = dragSnapEdges[i];
                    distance = candidateDistance;
                }
            }

            return distance <= threshold;
        }

        void EndClipDrag()
        {
            ReleasePointerCapture();
            clipDragMode = ClipDragMode.None;
            draggedTrack = -1;
            dragSnappedToEdge = false;
            dragSnapEdges.Clear();
            draggedClips.Clear();
            ApplySerializedSources();
            previewGUI?.EndLiveEdit();
            BuildClipTimings();

            if (recording)
                ScrubTimeline(recordingTime);
        }

        void CancelClipDrag()
        {
            for (int i = 0; i < draggedClips.Count; i++)
            {
                if (TryGetTimingProperties(draggedClips[i].Index, out SerializedProperty delay, out SerializedProperty duration))
                {
                    delay.floatValue = draggedClips[i].Delay;
                    if (draggedClips[i].Index == draggedTrack)
                        duration.floatValue = dragOriginalDuration;
                }
            }

            definitionOwner?.ApplyModifiedProperties();
            pendingClickTrack = -1;
            EndClipDrag();
        }

        void HandleOverviewInput(Rect rect, float span, float windowStart, float windowEnd, float maxHorizontalScroll)
        {
            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(OverviewControlHint, FocusType.Passive, rect);
            Vector2 mouse = current.mousePosition;

            // A narrow window keeps grab zones outside its edges, and the nearer end wins where they overlap
            Rect startHandle = new(windowStart - Layout.OverviewHandleWidth, rect.y, Layout.OverviewHandleWidth * 2f, rect.height);
            Rect endHandle = new(windowEnd - Layout.OverviewHandleWidth, rect.y, Layout.OverviewHandleWidth * 2f, rect.height);
            Rect body = Rect.MinMaxRect(startHandle.xMax, rect.y, endHandle.x, rect.yMax);
            EditorGUIUtility.AddCursorRect(startHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(endHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(body, MouseCursor.Pan);

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseMove:
                    if (rect.Contains(mouse) != overviewHovered)
                    {
                        overviewHovered = !overviewHovered;
                        Repaint();
                    }
                    break;

                case EventType.MouseDown when current.button == 0 && rect.Contains(mouse):
                    if (current.clickCount == 2)
                    {
                        FrameAll();
                        current.Use();
                        break;
                    }

                    overviewDragSpan = span;
                    overviewDragStartTime = horizontalScroll / pixelsPerSecond;
                    overviewDragEndTime = (horizontalScroll + rect.width) / pixelsPerSecond;
                    bool nearEnd = mouse.x >= (windowStart + windowEnd) * 0.5f;
                    if (endHandle.Contains(mouse) && (nearEnd || !startHandle.Contains(mouse)))
                        overviewDragMode = OverviewDragMode.ZoomEnd;
                    else if (startHandle.Contains(mouse))
                        overviewDragMode = OverviewDragMode.ZoomStart;
                    else
                    {
                        // A click beside the window centers the view there, then drags it from that point
                        if (!body.Contains(mouse))
                            horizontalScroll = Mathf.Clamp((mouse.x - rect.x) / rect.width * span * pixelsPerSecond - rect.width * 0.5f, 0f, maxHorizontalScroll);
                        overviewDragMode = OverviewDragMode.Pan;
                    }

                    overviewDragMouseX = mouse.x;
                    overviewDragScroll = horizontalScroll;
                    GUIUtility.hotControl = controlId;
                    Repaint();
                    current.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    ApplyOverviewDrag(rect, mouse.x, maxHorizontalScroll);
                    Repaint();
                    current.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    overviewDragMode = OverviewDragMode.None;
                    Repaint();
                    current.Use();
                    break;
            }
        }

        // Zooming keeps the opposite edge of the view in place. The span stays as it was at the press, so the map
        // doesn't rescale under the pointer while the view grows past the sequence end.
        void ApplyOverviewDrag(Rect rect, float mouseX, float maxHorizontalScroll)
        {
            float scale = rect.width / overviewDragSpan;
            float time = Mathf.Clamp((mouseX - rect.x) / scale, 0f, overviewDragSpan);
            float minimumSpan = rect.width / MaximumPixelsPerSecond;

            switch (overviewDragMode)
            {
                case OverviewDragMode.Pan:
                    horizontalScroll = Mathf.Clamp(overviewDragScroll + (mouseX - overviewDragMouseX) / scale * pixelsPerSecond, 0f, maxHorizontalScroll);
                    break;
                case OverviewDragMode.ZoomEnd:
                    SetZoom(rect.width / Mathf.Max(time - overviewDragStartTime, minimumSpan));
                    horizontalScroll = overviewDragStartTime * pixelsPerSecond;
                    break;
                case OverviewDragMode.ZoomStart:
                    SetZoom(rect.width / Mathf.Max(overviewDragEndTime - time, minimumSpan));
                    horizontalScroll = Mathf.Max(0f, overviewDragEndTime * pixelsPerSecond - rect.width);
                    break;
            }
        }

        // Timeline x is local to the ruler and canvas, with time zero at their left edge
        float TimeToX(float time) => time * pixelsPerSecond - horizontalScroll;

        float GetTimelineTimeAt(float mouseX, float originX = 0f) => Mathf.Max(0f, (mouseX - originX + horizontalScroll) / pixelsPerSecond);

        // The track row at a y local to the track headers or canvas. It can be outside the clip range.
        int GetTrackAt(float localY) => Mathf.FloorToInt((localY + verticalScroll) / Layout.TrackHeight);

        bool TryGetTimingProperties(int index, out SerializedProperty delay, out SerializedProperty duration)
        {
            delay = null;
            duration = null;

            if (clips == null || index < 0 || index >= clips.arraySize)
                return false;

            SerializedProperty settings = clips.GetArrayElementAtIndex(index).FindPropertyRelative("settings");
            delay = settings?.FindPropertyRelative("delay");
            duration = settings?.FindPropertyRelative("duration");
            return delay != null && duration != null;
        }
    }
}
