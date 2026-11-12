using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Clip selection: one clip or several, box selection on the canvas, and the choreography edits that act on a
    /// selection.
    /// </summary>
    public sealed partial class TimelineWindow
    {
        // The last selected clip is the primary one. Single-clip actions, framing, and range selection follow it.
        int PrimaryTrack => selectedTracks.Count > 0 ? selectedTracks[^1] : -1;

        int SelectedCount => clips != null ? selectedTracks.Count : 0;

        bool HasSelection => SelectedCount > 0;

        bool IsSelected(int index) => selectedTracks.Contains(index);

        void SelectTrack(int index)
        {
            selectedTracks.Clear();
            if (index >= 0)
                selectedTracks.Add(index);
            selectionAnchor = index;
        }

        void ClearSelection() => SelectTrack(-1);

        void SelectAll()
        {
            selectedTracks.Clear();
            for (int i = 0; i < (clips?.arraySize ?? 0); i++)
                selectedTracks.Add(i);
            selectionAnchor = selectedTracks.Count > 0 ? 0 : -1;
            Repaint();
        }

        /// <summary>
        /// Shift selects the range from the anchor clip, adding to the selection with Ctrl / Cmd. Ctrl / Cmd alone
        /// adds a clip. A clip that is already selected stays selected on the press, so the selection can be dragged;
        /// a click that doesn't drag then reduces the selection to it, or removes it with Ctrl / Cmd.
        /// </summary>
        void SelectOnMouseDown(int index, Event current)
        {
            pendingClickTrack = -1;
            if (current.shift && selectionAnchor >= 0 && selectionAnchor < (clips?.arraySize ?? 0))
            {
                if (!EditorGUI.actionKey)
                    selectedTracks.Clear();

                // The range ends with the clicked clip, which becomes the primary one, but the anchor stays put
                int step = index >= selectionAnchor ? 1 : -1;
                for (int i = selectionAnchor; i != index + step; i += step)
                    MakePrimary(i);
                return;
            }

            if (IsSelected(index))
            {
                pendingClickTrack = index;
                pendingClickDeselects = EditorGUI.actionKey;
                MakePrimary(index);
                return;
            }

            if (!EditorGUI.actionKey)
                selectedTracks.Clear();
            selectedTracks.Add(index);
            selectionAnchor = index;
        }

        // Selects the clip, or moves it to the end of the selection if it is already in it
        void MakePrimary(int index)
        {
            selectedTracks.Remove(index);
            selectedTracks.Add(index);
        }

        void ApplyPendingClick()
        {
            int index = pendingClickTrack;
            pendingClickTrack = -1;
            if (index < 0)
                return;

            if (!pendingClickDeselects)
            {
                SelectTrack(index);
                return;
            }

            selectedTracks.Remove(index);
            selectionAnchor = PrimaryTrack;
        }

        void ClampSelection()
        {
            int count = clips?.arraySize ?? 0;
            for (int i = selectedTracks.Count - 1; i >= 0; i--)
            {
                if (selectedTracks[i] < 0 || selectedTracks[i] >= count)
                    selectedTracks.RemoveAt(i);
            }

            if (selectionAnchor >= count)
                selectionAnchor = PrimaryTrack;
        }

        /// <summary>
        /// The selected indices in track order. The list is reused, so copy it before an edit that changes the selection.
        /// </summary>
        List<int> GetSortedSelection()
        {
            sortedSelection.Clear();
            sortedSelection.AddRange(selectedTracks);
            sortedSelection.Sort();
            return sortedSelection;
        }

        List<SerializedProperty> GetSelectedClipProperties()
        {
            selectedClipProperties.Clear();
            List<int> indices = GetSortedSelection();
            for (int i = 0; i < indices.Count; i++)
                selectedClipProperties.Add(clips.GetArrayElementAtIndex(indices[i]));
            return selectedClipProperties;
        }

        bool TryGetSelectionRange(out float start, out float end)
        {
            start = float.MaxValue;
            end = 0f;
            for (int i = 0; i < selectedTracks.Count; i++)
            {
                int index = selectedTracks[i];
                if (index < 0 || index >= clipTimings.Count)
                    continue;

                start = Mathf.Min(start, clipTimings[index].Start);
                end = Mathf.Max(end, clipTimings[index].End);
            }
            return start <= end;
        }

        void BeginBoxSelection(Vector2 mouse)
        {
            boxSelecting = true;
            boxSelectionMoved = false;
            boxSelectionStart = mouse;
            boxSelectionEnd = mouse;
            boxSelectionBase.Clear();
            if (Event.current.shift || EditorGUI.actionKey)
                boxSelectionBase.AddRange(selectedTracks);
        }

        // Canvas-local like the clip rects. A press without a drag clears the selection, as clicking empty space did before.
        void HandleBoxSelection(Event current, EventType type)
        {
            if (type == EventType.MouseDrag && current.button == 0)
            {
                boxSelectionEnd = current.mousePosition;
                boxSelectionMoved |= Vector2.Distance(boxSelectionEnd, boxSelectionStart) > Layout.ClickDragThreshold;
                if (boxSelectionMoved)
                    SelectClipsInBox();

                Repaint();
                current.Use();
            }
            else if (type == EventType.MouseUp && current.button == 0)
            {
                if (!boxSelectionMoved && boxSelectionBase.Count == 0)
                    ClearSelection();

                EndBoxSelection();
                current.Use();
            }
            else if (type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                selectedTracks.Clear();
                selectedTracks.AddRange(boxSelectionBase);
                EndBoxSelection();
                current.Use();
            }
        }

        void EndBoxSelection()
        {
            ReleasePointerCapture();
            boxSelecting = false;
            Repaint();
        }

        Rect GetBoxSelectionRect() => Rect.MinMaxRect(Mathf.Min(boxSelectionStart.x, boxSelectionEnd.x), Mathf.Min(boxSelectionStart.y, boxSelectionEnd.y),
            Mathf.Max(boxSelectionStart.x, boxSelectionEnd.x), Mathf.Max(boxSelectionStart.y, boxSelectionEnd.y));

        void SelectClipsInBox()
        {
            Rect box = GetBoxSelectionRect();
            selectedTracks.Clear();
            selectedTracks.AddRange(boxSelectionBase);
            for (int i = 0; i < clipTimings.Count; i++)
            {
                if (!IsSelected(i) && GetClipRect(i, clipTimings[i]).Overlaps(box))
                    selectedTracks.Add(i);
            }
            selectionAnchor = PrimaryTrack;
        }

        /// <summary>
        /// Places the selected clips <paramref name="step"/> seconds apart from the top one, by start time. A step of
        /// zero aligns their starts. A clip that would start before its flow group starts with the group instead.
        /// </summary>
        void StaggerSelection(float step)
        {
            if (SelectedCount < 2)
                return;

            List<int> indices = new(GetSortedSelection());
            PerformEdit(step > 0f ? "Stagger Tween Clips" : "Align Tween Clips", () =>
            {
                BuildClipTimings();
                float firstStart = clipTimings[indices[0]].Start;
                for (int i = 0; i < indices.Count; i++)
                {
                    placedClips.Add(indices[i]);
                    placedStarts.Add(firstStart + step * i);
                }

                PlaceClips();
            });
        }

        void DrawChoreographySection(bool invertColor)
        {
            staggerStep = SanitizeNonNegative(EvoEditorGUI.FloatField(staggerStep, "Stagger Step",
                FieldOptions.WithTooltip("Time between the starts of neighboring clips.").WithInvertedColor(invertColor)));

            GUILayout.BeginHorizontal();
            if (EvoEditorGUI.Button("Stagger", ButtonOptions.WithTooltip(
                "Start each clip one step after the clip above it. Alt-drag a clip to fan the selection out by hand.").WithInvertedColor(invertColor)))
            {
                StaggerSelection(staggerStep);
            }

            GUILayout.Space(EvoEditorGUI.Metrics.LayoutSpacing);
            if (EvoEditorGUI.Button("Align Starts", ButtonOptions.WithTooltip(
                "Start every selected clip with the top one.").WithInvertedColor(invertColor)))
            {
                StaggerSelection(0f);
            }
            GUILayout.EndHorizontal();
        }
    }
}
