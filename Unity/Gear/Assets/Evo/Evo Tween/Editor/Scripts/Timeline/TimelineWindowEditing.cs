using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    public sealed partial class TimelineWindow
    {
        /// <summary>
        /// Undo-aware clip mutations and the timing model shared by drawing and interaction code.
        /// </summary>
        void ShowAddMenu()
        {
            GenericMenu menu = new();
            AddModuleMenuItems(menu, string.Empty);
            menu.ShowAsContext();
        }

        // Right-click on empty timeline space, where there is no clip to act on
        void ShowEmptyAreaContextMenu()
        {
            GenericMenu menu = new();
            AddModuleMenuItems(menu, "Add Clip/");
            menu.AddSeparator(string.Empty);
            AddPasteAsNewMenuItem(menu);
            menu.ShowAsContext();
        }

        void AddModuleMenuItems(GenericMenu menu, string prefix)
        {
            IReadOnlyList<TweenClipGUI.ModuleTypeInfo> types = TweenClipGUI.GetModuleTypes();
            for (int i = 0; i < types.Count; i++)
            {
                TweenClipGUI.ModuleTypeInfo info = types[i];
                Type moduleType = info.Type;
                menu.AddItem(new GUIContent(prefix + info.MenuPath), false, () => AddClip(moduleType));
            }

            if (types.Count == 0)
                menu.AddDisabledItem(new GUIContent(prefix + "No TweenModule types found"));
        }

        void AddPasteAsNewMenuItem(GenericMenu menu)
        {
            GUIContent content = new(TweenClipGUI.ClipboardCount > 1 ? "Paste as New Clips" : "Paste as New Clip");
            if (TweenClipGUI.CanPasteClip)
                menu.AddItem(content, false, PasteAsNewClip);
            else
                menu.AddDisabledItem(content);
        }

        void AddClip(Type moduleType)
        {
            PerformEdit("Add Tween Clip", () =>
            {
                int index = clips.arraySize;
                TweenModule module = Activator.CreateInstance(moduleType) as TweenModule;
                InsertClip(index, GetNewClipTarget(module), module, index == 0 ? ClipFlow.Parallel : ClipFlow.Sequential, 0f, DefaultClipDuration);
            }, structural: true);
        }

        /// <summary>
        /// Inserts a clip with fresh settings and selects it. Preset-backed sequences store the target in this
        /// instance's bindings instead of the shared definition. Callers run this inside <see cref="PerformEdit"/>.
        /// </summary>
        void InsertClip(int index, Component target, TweenModule module, ClipFlow flow, float delay, float duration,
            Ease ease = Ease.OutCubic, AnimationCurve customEase = null)
        {
            target = GetStoredTarget(target);
            if (activePreset != null)
                SynchronizeTargetBindings();

            clips.InsertArrayElementAtIndex(index);
            SerializedProperty clip = clips.GetArrayElementAtIndex(index);
            clip.FindPropertyRelative("target").objectReferenceValue = activePreset == null ? target : null;
            clip.FindPropertyRelative("flow").enumValueIndex = (int)flow;
            SerializedProperty settings = clip.FindPropertyRelative("settings");
            settings.FindPropertyRelative("module").managedReferenceValue = module;
            TweenClipGUI.InitializeClipSettings(settings, delay, duration, ease, customEase);

            if (activePreset != null)
            {
                targetBindings.InsertArrayElementAtIndex(index);
                targetBindings.GetArrayElementAtIndex(index).objectReferenceValue = target;
            }

            SelectTrack(index);
            trackToReveal = index;
        }

        // A target on a redirected owner's preview root stays implicit, so the clip applies wherever the sequence plays
        Component GetStoredTarget(Component target)
        {
            return target != null && TweenPreviewRoot.IsRedirected(owner) && target.gameObject == TweenPreviewRoot.Resolve(owner)
                ? null
                : target;
        }

        void DeleteSelected()
        {
            if (!HasSelection)
                return;

            List<int> indices = new(GetSortedSelection());
            PerformEdit(indices.Count > 1 ? "Delete Tween Clips" : "Delete Tween Clip", () =>
            {
                for (int i = indices.Count - 1; i >= 0; i--)
                {
                    DeleteArrayElement(clips, indices[i]);
                    if (activePreset != null && targetBindings != null && indices[i] < targetBindings.arraySize)
                        DeleteArrayElement(targetBindings, indices[i]);
                }

                SelectTrack(Mathf.Min(indices[0], clips.arraySize - 1));
            }, structural: true);
        }

        void DuplicateSelected()
        {
            if (!HasSelection)
                return;

            CopySelected();
            AppendClipboard(SelectedCount > 1 ? "Duplicate Tween Clips" : "Duplicate Tween Clip");
        }

        // Copies the effective targets, so preset clips keep the targets this sequence binds them to
        void CopySelected()
        {
            if (!HasSelection)
                return;

            List<int> indices = GetSortedSelection();
            List<SerializedProperty> copied = new(indices.Count);
            List<Component> targets = new(indices.Count);
            for (int i = 0; i < indices.Count; i++)
            {
                copied.Add(clips.GetArrayElementAtIndex(indices[i]));
                targets.Add(GetTargetProperty(indices[i])?.objectReferenceValue as Component);
            }

            TweenClipGUI.CopyClips(copied, targets);
        }

        // One copied clip replaces every selected clip, like Paste in the Inspector list
        void PasteSelected()
        {
            if (!HasSelection || TweenClipGUI.ClipboardCount != 1)
                return;

            List<int> indices = new(GetSortedSelection());
            PerformEdit("Paste Tween Clip", () =>
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    SerializedProperty destination = clips.GetArrayElementAtIndex(indices[i]);
                    TweenClipGUI.PasteClip(destination);

                    if (activePreset != null)
                        destination.FindPropertyRelative("target").objectReferenceValue = null;
                }
            });
        }

        void PasteAsNewClip() => AppendClipboard(TweenClipGUI.ClipboardCount > 1 ? "Paste Tween Clips" : "Paste Tween Clip");

        // Appended like Add Clip, so the timing of existing flow groups stays the same
        void AppendClipboard(string undoName)
        {
            if (clips == null || !TweenClipGUI.CanPasteClip)
                return;

            PerformEdit(undoName, () =>
            {
                selectedTracks.Clear();
                for (int i = 0; i < TweenClipGUI.ClipboardCount; i++)
                {
                    int index = clips.arraySize;
                    clips.arraySize++;
                    SerializedProperty destination = clips.GetArrayElementAtIndex(index);
                    TweenClipGUI.PasteClip(destination, i);

                    if (activePreset != null)
                    {
                        destination.FindPropertyRelative("target").objectReferenceValue = null;
                        SynchronizeTargetBindings();
                        targetBindings.GetArrayElementAtIndex(index).objectReferenceValue = TweenClipGUI.GetClipboardTarget(i);
                    }

                    selectedTracks.Add(index);
                }

                selectionAnchor = selectedTracks.Count > 0 ? selectedTracks[0] : -1;
                trackToReveal = clips.arraySize - 1;
            }, structural: true);
        }

        void MoveTrack(int source, int destination)
        {
            if (source < 0 || destination < 0 || source >= clips.arraySize || destination >= clips.arraySize)
                return;

            PerformEdit("Reorder Tween Clips", () =>
            {
                clips.MoveArrayElement(source, destination);

                if (activePreset != null && targetBindings != null && source < targetBindings.arraySize && destination < targetBindings.arraySize)
                    targetBindings.MoveArrayElement(source, destination);

                SelectTrack(destination);
            }, structural: true);
        }

        void SetFlow(IReadOnlyList<int> indices, ClipFlow flow)
        {
            if (clips == null || indices.Count == 0)
                return;

            PerformEdit("Change Tween Flow", () =>
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    if (indices[i] >= 0 && indices[i] < clips.arraySize)
                        clips.GetArrayElementAtIndex(indices[i]).FindPropertyRelative("flow").enumValueIndex = (int)flow;
                }
            });
        }

        // Acts on the whole selection when the clicked clip is part of it
        void ShowClipContextMenu(int index)
        {
            if (!IsSelected(index))
                SelectTrack(index);

            List<int> indices = new(GetSortedSelection());
            bool several = indices.Count > 1;
            bool allParallel = true;
            bool allSequential = true;
            for (int i = 0; i < indices.Count; i++)
            {
                bool sequential = indices[i] < clipTimings.Count && clipTimings[indices[i]].Flow == ClipFlow.Sequential;
                allParallel &= !sequential;
                allSequential &= sequential;
            }

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Flow/Parallel"), allParallel, () => SetFlow(indices, ClipFlow.Parallel));
            menu.AddItem(new GUIContent("Flow/Sequential"), allSequential, () => SetFlow(indices, ClipFlow.Sequential));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy"), false, CopySelected);

            if (TweenClipGUI.ClipboardCount == 1)
                menu.AddItem(new GUIContent("Paste"), false, PasteSelected);
            else
                menu.AddDisabledItem(new GUIContent("Paste"));

            AddPasteAsNewMenuItem(menu);
            menu.AddItem(new GUIContent("Duplicate"), false, DuplicateSelected);

            if (several)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent($"Stagger by {staggerStep:0.###}s"), false, () => StaggerSelection(staggerStep));
                menu.AddItem(new GUIContent("Align Starts"), false, () => StaggerSelection(0f));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(several ? "Frame Selection" : "Frame Clip"), false, FrameSelection);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, DeleteSelected);
            menu.ShowAsContext();
        }

        void DetachPreset()
        {
            StopRecording();
            TweenSequence boundSequence = GetBoundSequence();
            if (owner == null || boundSequence == null || activePreset == null)
                return;

            previewGUI?.BeginLiveEdit();
            ApplySerializedSources();
            Undo.RecordObject(owner, "Detach Tween Preset");
            boundSequence.DetachPreset(copyClips: true);
            EditorUtility.SetDirty(owner);
            BindDefinitionSource(force: true);
            previewGUI?.EndLiveEdit();
            BuildClipTimings();
            Repaint();
        }

        void PerformEdit(string undoName, Action edit, bool structural = false)
        {
            if (sequenceOwner?.targetObject == null || definitionOwner?.targetObject == null || edit == null)
                return;

            previewGUI?.BeginLiveEdit();
            RecordUndo(undoName);
            UpdateSerializedSources();
            edit();
            ApplySerializedSources();

            if (structural)
                BindDefinitionSource(force: true);
            else
                UpdateSerializedSources();

            EditorUtility.SetDirty(definitionOwner.targetObject);
            EditorUtility.SetDirty(owner);
            previewGUI?.EndLiveEdit();
            BuildClipTimings();
            ClampSelection();
            if (recording)
                ScrubTimeline(recordingTime);
            Repaint();
        }

        void RecordUndo(string undoName)
        {
            if (definitionOwner?.targetObject == null || sequenceOwner?.targetObject == null)
                return;

            UnityEngine.Object definitionTarget = definitionOwner.targetObject;
            UnityEngine.Object sequenceTarget = sequenceOwner.targetObject;

            if (definitionTarget == sequenceTarget)
                Undo.RecordObject(sequenceTarget, undoName);
            else
                Undo.RecordObjects(new[] { definitionTarget, sequenceTarget }, undoName);
        }

        void CommitPropertyChanges()
        {
            previewGUI?.BeginLiveEdit();
            ApplySerializedSources();
            EditorUtility.SetDirty(owner);

            if (definitionOwner?.targetObject != null)
                EditorUtility.SetDirty(definitionOwner.targetObject);

            BindDefinitionSource(force: true);
            previewGUI?.EndLiveEdit();
            BuildClipTimings();
            if (recording)
                ScrubTimeline(recordingTime);
        }

        void ApplySerializedSources()
        {
            if (definitionOwner?.targetObject != null && definitionOwner != sequenceOwner)
                definitionOwner.ApplyModifiedProperties();
            if (sequenceOwner?.targetObject != null)
                sequenceOwner.ApplyModifiedProperties();
        }

        void BuildClipTimings()
        {
            clipTimingsDirty = false;
            clipTimings.Clear();
            clipVisuals.Clear();
            flowGroups.Clear();
            flowLabelGroup = -1;
            timelineDuration = 0f;

            if (clips == null || !clips.isArray)
                return;

            float groupStart = 0f;
            float groupEnd = 0f;
            int lastEnding = -1;

            for (int i = 0; i < clips.arraySize; i++)
            {
                SerializedProperty clip = clips.GetArrayElementAtIndex(i);
                SerializedProperty flowProperty = clip.FindPropertyRelative("flow");
                SerializedProperty settings = clip.FindPropertyRelative("settings");
                SerializedProperty delayProperty = settings?.FindPropertyRelative("delay");
                ClipFlow flow = flowProperty != null ? (ClipFlow)flowProperty.enumValueIndex : ClipFlow.Parallel;
                float delay = SanitizeNonNegative(delayProperty?.floatValue ?? 0f);
                float duration = SanitizeNonNegative(settings?.FindPropertyRelative("duration")?.floatValue ?? 0f);
                LoopMode loop = (LoopMode)(settings?.FindPropertyRelative("loop")?.enumValueIndex ?? (int)LoopMode.None);
                int iterations = loop == LoopMode.None
                    ? 1
                    : Mathf.Max(1, settings?.FindPropertyRelative("iterations")?.intValue ?? 1);

                // A group waits for the clip that ends last so far, which is what the flow context shows
                if (flowGroups.Count == 0 || flow == ClipFlow.Sequential)
                {
                    groupStart = groupEnd;
                    flowGroups.Add(new FlowGroup(groupStart, i, i, lastEnding));
                }
                else
                {
                    FlowGroup group = flowGroups[^1];
                    flowGroups[^1] = new FlowGroup(group.Start, group.First, i, group.After);
                }

                int placement = placedClips.IndexOf(i);
                if (placement >= 0 && delayProperty != null)
                {
                    delay = Mathf.Max(0f, placedStarts[placement] - groupStart);
                    delayProperty.floatValue = delay;
                }

                float start = SafeAdd(groupStart, delay);
                float totalDuration = SanitizeNonNegative(duration * iterations);
                float end = SafeAdd(start, totalDuration);
                if (end >= groupEnd)
                    lastEnding = i;

                groupEnd = Mathf.Max(groupEnd, end);
                clipTimings.Add(new ClipTiming(groupStart, start, end, duration, delay, iterations, flowGroups.Count - 1, loop, flow));

                Ease ease = (Ease)(settings?.FindPropertyRelative("ease")?.enumValueIndex ?? (int)Ease.Linear);
                AnimationCurve customEase = ease == Ease.Custom ? settings?.FindPropertyRelative("customEase")?.animationCurveValue : null;
                TweenModule module = TweenClipGUI.GetEffectiveModule(clip);
                TweenClipGUI.TryGetValueRange(settings, module?.GetType(), out SerializedProperty from, out SerializedProperty to);
                clipVisuals.Add(new ClipVisual(GetTrackTitle(settings, module), duration, iterations, ease, customEase, from, to));
            }

            timelineDuration = groupEnd;
        }

        /// <summary>
        /// Moves each clip in <see cref="placedClips"/> to start at its <see cref="placedStarts"/> time, as close as its
        /// flow group allows. Clips are placed in track order during one timing pass, so each lands against the group
        /// start that the clips before it produce.
        /// </summary>
        void PlaceClips()
        {
            try { BuildClipTimings(); }
            finally
            {
                placedClips.Clear();
                placedStarts.Clear();
            }
        }

        /// <summary>
        /// Where a clip added at <paramref name="time"/> goes: into the last flow group that has started by then, after
        /// that group's last clip, so earlier groups keep their timing.
        /// </summary>
        int FindGroupSlot(float time, out float groupStart)
        {
            groupStart = 0f;
            if (flowGroups.Count == 0)
                return clips?.arraySize ?? 0;

            FlowGroup slot = flowGroups[0];
            for (int i = 1; i < flowGroups.Count && flowGroups[i].Start <= time; i++)
                slot = flowGroups[i];

            groupStart = slot.Start;
            return slot.Last + 1;
        }

        TweenModule GetClipModule(int index) => TweenClipGUI.GetEffectiveModule(clips.GetArrayElementAtIndex(index));

        SerializedProperty GetTargetProperty(int index)
        {
            if (clips == null || index < 0 || index >= clips.arraySize)
                return null;

            if (activePreset == null)
                return clips.GetArrayElementAtIndex(index).FindPropertyRelative("target");

            return targetBindings != null && index < targetBindings.arraySize ? targetBindings.GetArrayElementAtIndex(index) : null;
        }

        Component ResolveDefaultTarget(TweenModule module)
        {
            if (owner == null || module?.TargetType == null)
                return null;

            GameObject root = TweenPreviewRoot.Resolve(owner);
            return root != null ? root.GetComponent(module.TargetType) : null;
        }

        // Redirected owners keep new clips implicit, so they follow whichever GameObject the sequence plays on
        Component GetNewClipTarget(TweenModule module) => TweenPreviewRoot.IsRedirected(owner) ? null : ResolveDefaultTarget(module);

        static void DeleteArrayElement(SerializedProperty array, int index)
        {
            if (array == null || index < 0 || index >= array.arraySize)
                return;

            int previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
                array.DeleteArrayElementAtIndex(index);
        }

        static float SanitizeNonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

        static float SafeAdd(float a, float b)
        {
            float value = a + b;
            return float.IsNaN(value) || float.IsInfinity(value) ? float.MaxValue : Mathf.Max(0f, value);
        }

        static string FormatRulerTime(float time)
        {
            if (time >= 60f)
                return $"{Mathf.FloorToInt(time / 60f)}:{time % 60f:00.0}";
            if (time >= 10f)
                return $"{time:0.0}s";

            return $"{time:0.00}s";
        }

        /// <summary>
        /// Returns the transport time label, in seconds like the ruler. The string is rebuilt only when a displayed
        /// value changes, so repainting a still playhead does not allocate.
        /// </summary>
        string GetTimeLabel(float currentTime, float duration)
        {
            int current = Mathf.RoundToInt(SanitizeNonNegative(currentTime) * 100f);
            int total = Mathf.RoundToInt(SanitizeNonNegative(duration) * 100f);
            if (cachedTimeLabel == null || total != cachedTimeLabelDuration)
                cachedTimeButtonWidth = MeasureTimeButton(total);

            if (cachedTimeLabel == null || current != cachedTimeLabelCurrent || total != cachedTimeLabelDuration)
            {
                cachedTimeLabelCurrent = current;
                cachedTimeLabelDuration = total;
                cachedTimeLabel = $"{FormatTime(current)} / {FormatTime(total)}";
            }

            return cachedTimeLabel;
        }

        // Fits the longest label for this sequence length. The current time never has more digits than the length,
        // and zeroed digits keep the width from shifting the transport while the time changes.
        float MeasureTimeButton(int totalHundredths)
        {
            char[] sample = FormatTime(totalHundredths).ToCharArray();
            for (int i = 0; i < sample.Length; i++)
            {
                if (char.IsDigit(sample[i]))
                    sample[i] = '0';
            }

            string text = new(sample);
            timeMeasureContent.text = $"{text} / {text}";
            return Mathf.Ceil(EditorStyles.label.CalcSize(timeMeasureContent).x)
                + EvoEditorGUI.Metrics.ButtonPaddingLeft + EvoEditorGUI.Metrics.ButtonPaddingRight + 8f;
        }

        static string FormatTime(int hundredths)
        {
            int seconds = hundredths / 100;
            return seconds >= 60
                ? $"{seconds / 60}:{seconds % 60:00}.{hundredths % 100:00}"
                : $"{seconds}.{hundredths % 100:00}s";
        }
    }
}