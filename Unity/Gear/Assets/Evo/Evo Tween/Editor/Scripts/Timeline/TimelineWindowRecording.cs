using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    public sealed partial class TimelineWindow
    {
        const float RecordingTimeTolerance = 0.0001f;
        static List<TweenRecordingBinding> recordingBindings;
        static TimelineWindow recordingWindow;

        sealed class RecordingWatch
        {
            public TweenRecordingBinding Binding;
            public Component Target;
            public TweenModule Module;
            public object Value;
            public int Index;
        }

        sealed class RecordingEdit
        {
            public RecordingWatch Watch;
            public object Value;
            public object From;
            public object To;
            public float Start;
            public float End;
            public float GroupStart;
            public int InsertIndex;
        }

        sealed class RecordingPrefabState
        {
            public PropertyModification[] Original;
            public readonly List<RecordingWatch> Recorded = new();
        }

        readonly List<RecordingWatch> recordingWatches = new();
        readonly Dictionary<GameObject, RecordingPrefabState> recordingPrefabs = new();
        bool recording;
        bool processingRecording;
        float recordingTime;
        Texture2D recordIcon;

        void DrawRecordButton(Rect rect)
        {
            recordIcon = recordIcon != null ? recordIcon : EditorGUIUtility.IconContent("Animation.Record").image as Texture2D;
            ButtonOptions options = ButtonOptions.WithIconSize(16).WithInvertedColor().WithTooltip(recording
                ? "Stop recording and restore scene values"
                : "Record supported property changes");
        
            if (recording)
                options = options.WithColors(new Color(1f, 0.2f, 0.2f, 0.4f), new Color(1f, 0.2f, 0.2f, 0.55f), new Color(1, 0.1f, 0.1f, 0.45f));

            using (new EditorGUI.DisabledScope(!recording && (Application.isPlaying || IsPlayModeTransitioning
                || previewGUI == null || EditorUtility.IsPersistent(owner) || AnimationMode.InAnimationMode())))
            {
                if (DrawEvoButton(rect, string.Empty, recordIcon, options))
                {
                    if (recording) { StopRecording(); }
                    else { StartRecording(); }
                }
            }
        }

        static void EnsureRecordingBindings()
        {
            if (recordingBindings != null)
                return;

            recordingBindings = new List<TweenRecordingBinding>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ITweenRecordingProvider>())
            {
                if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                    continue;

                try
                {
                    ITweenRecordingProvider provider = (ITweenRecordingProvider)Activator.CreateInstance(type);
                    foreach (TweenRecordingBinding binding in provider.GetBindings())
                    {
                        if (binding != null)
                            recordingBindings.Add(binding);
                    }
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }

            recordingBindings.Sort((a, b) =>
            {
                int priority = b.Priority.CompareTo(a.Priority);
                return priority != 0 ? priority : string.CompareOrdinal(a.ModuleType.FullName, b.ModuleType.FullName);
            });
        }

        void StartRecording()
        {
            if (owner == null || previewGUI == null || EditorUtility.IsPersistent(owner)
                || Application.isPlaying || IsPlayModeTransitioning || AnimationMode.InAnimationMode())
                return;

            if (recordingWindow != null)
                recordingWindow.StopRecording();

            EnsureRecordingBindings();
            recordingTime = CurrentTimelineTime;
            recording = true;
            recordingWindow = this;
            Undo.postprocessModifications += RecordModifications;
            EditorSceneManager.sceneSaving += OnRecordingSceneSaving;
            EditorApplication.update += ValidateRecording;
            ScrubTimeline(recordingTime);
        }

        void StopRecording()
        {
            if (!recording)
                return;

            // Commit an Inspector field or Scene handle before removing the Undo callback
            if (!processingRecording)
                Undo.FlushUndoRecordObjects();

            recording = false;

            if (recordingWindow == this)
                recordingWindow = null;

            Undo.postprocessModifications -= RecordModifications;
            EditorApplication.delayCall -= RefreshRecordingPreview;
            EditorSceneManager.sceneSaving -= OnRecordingSceneSaving;
            EditorApplication.update -= ValidateRecording;
            recordingWatches.Clear();
            previewGUI?.StopPreview();
            RestoreRecordingPrefabOverrides();
            recordingPrefabs.Clear();
            Repaint();
        }

        void ValidateRecording()
        {
            if (owner == null || definitionOwner?.targetObject == null || Application.isPlaying || IsPlayModeTransitioning
                || AnimationMode.InAnimationMode() || sequencePreset?.objectReferenceValue != activePreset)
                StopRecording();
        }

        void OnRecordingSceneSaving(Scene scene, string path) => StopRecording();

        void ScrubTimeline(float time)
        {
            if (recording && !processingRecording)
                Undo.FlushUndoRecordObjects();

            recordingTime = SanitizeNonNegative(time);
            previewGUI?.ScrubPreview(recordingTime);
            RefreshRecordingWatches();
        }

        void ToggleTimelinePreview()
        {
            StopRecording();
            previewGUI?.TogglePreview();
        }

        void StopTimelinePreview()
        {
            StopRecording();
            previewGUI?.StopPreview();
        }

        void OnHierarchyChange() => RefreshRecordingWatches();

        void RefreshRecordingPreview()
        {
            if (!recording || processingRecording || owner == null)
                return;

            sequenceOwner.Update();

            if (sequencePreset?.objectReferenceValue != activePreset)
            {
                StopRecording();
                return;
            }

            previewGUI.StopPreview();
            BindDefinitionSource(force: true);
            previewGUI.ScrubPreview(recordingTime);
            RefreshRecordingWatches();
        }

        void RefreshRecordingWatches()
        {
            if (!recording || processingRecording || owner == null)
                return;

            recordingWatches.Clear();
            UpdateSerializedSources();

            // Inspect only the owner, its preview root, selected objects, and existing bindings.
            // Large UI hierarchies should not be scanned and cloned on every Inspector drag.
            HashSet<Component> targets = new(owner.GetComponents<Component>());
            GameObject previewRoot = TweenPreviewRoot.Resolve(owner);
            if (previewRoot != null && previewRoot != owner.gameObject)
                targets.UnionWith(previewRoot.GetComponents<Component>());

            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected.scene == owner.gameObject.scene)
                    targets.UnionWith(selected.GetComponents<Component>());
            }

            for (int i = 0; i < clips.arraySize; i++)
            {
                TweenModule module = GetClipModule(i);
                Component target = GetTargetProperty(i)?.objectReferenceValue as Component;
                target = target != null ? target : ResolveDefaultTarget(module);
                if (target == null || module == null)
                    continue;

                targets.Add(target);
                foreach (TweenRecordingBinding binding in recordingBindings)
                {
                    if (binding.ModuleType == module.GetType() && binding.TargetType.IsInstanceOfType(target))
                        AddRecordingWatch(binding, target, module, i);
                }
            }

            foreach (Component target in targets)
            {
                if (target == null || EditorUtility.IsPersistent(target))
                    continue;
               
                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                if (prefabRoot != null && !recordingPrefabs.ContainsKey(prefabRoot))
                    recordingPrefabs.Add(prefabRoot, new RecordingPrefabState { Original = PrefabUtility.GetPropertyModifications(prefabRoot) });
              
                foreach (TweenRecordingBinding binding in recordingBindings)
                {
                    if (binding.TargetType.IsInstanceOfType(target) && binding.CanCreate(target))
                        AddRecordingWatch(binding, target, binding.CreateModule(), -1);
                }
            }
        }

        void AddRecordingWatch(TweenRecordingBinding binding, Component target, TweenModule module, int index)
        {
            try
            {
                recordingWatches.Add(new RecordingWatch
                {
                    Binding = binding,
                    Target = target,
                    Module = module.Clone(),
                    Value = binding.Read(module, target),
                    Index = index
                });
            }
            catch (Exception exception) { Debug.LogException(exception, target); }
        }

        UndoPropertyModification[] RecordModifications(UndoPropertyModification[] modifications)
        {
            if (!recording || processingRecording || owner == null || definitionOwner?.targetObject == null
                || Application.isPlaying || IsPlayModeTransitioning || AnimationMode.InAnimationMode())
                return modifications;

            List<RecordingEdit> edits = new();
            HashSet<RecordingWatch> chosen = new();

            foreach (UndoPropertyModification modification in modifications)
            {
                PropertyModification property = modification.currentValue;
                if (property?.target is not Component target)
                    continue;

                RecordingWatch best = null;
                int bestScore = int.MinValue;
                float bestDistance = float.MaxValue;
              
                foreach (RecordingWatch watch in recordingWatches)
                {
                    if (watch.Target != target || !watch.Binding.Matches(property.propertyPath))
                        continue;
                  
                    int score = watch.Binding.Priority;
                    float distance = float.MaxValue;
                
                    if (watch.Index >= 0)
                    {
                        ClipTiming timing = clipTimings[watch.Index];
                        distance = Mathf.Max(timing.Start - recordingTime, recordingTime - timing.End, 0f);
                        bool active = recordingTime >= timing.Start - RecordingTimeTolerance && recordingTime <= timing.End + RecordingTimeTolerance;
                        score += active ? 10000 : 1000;
                        if (active && IsSelected(watch.Index))
                            score += 10000;
                    }

                    if (score > bestScore || score == bestScore && distance < bestDistance)
                    {
                        best = watch;
                        bestScore = score;
                        bestDistance = distance;
                    }
                }

                if (best == null || !chosen.Add(best))
                    continue;

                try
                {
                    object value = best.Binding.Read(best.Module, target);
                    if (!Equals(value, best.Value))
                        edits.Add(new RecordingEdit { Watch = best, Value = value });
                }
                catch (Exception exception) { Debug.LogException(exception, target); }
            }

            if (edits.Count == 0)
            {
                foreach (UndoPropertyModification modification in modifications)
                {
                    if (modification.currentValue.target == owner || modification.currentValue.target == definitionOwner.targetObject)
                    {
                        EditorApplication.delayCall -= RefreshRecordingPreview;
                        EditorApplication.delayCall += RefreshRecordingPreview;
                        break;
                    }
                }
                return modifications;
            }

            // A shared boundary is one authored pose. Update both adjoining segments so the later
            // segment cannot immediately overwrite a newly recorded end value.
            int changedCount = edits.Count;
            for (int i = 0; i < changedCount; i++)
            {
                RecordingWatch source = edits[i].Watch;
                foreach (RecordingWatch watch in recordingWatches)
                {
                    if (watch.Index < 0 || watch.Target != source.Target || watch.Binding.Channel != source.Binding.Channel || chosen.Contains(watch))
                        continue;
                   
                    ClipTiming timing = clipTimings[watch.Index];
                    if (Mathf.Abs(timing.Start - recordingTime) > RecordingTimeTolerance && Mathf.Abs(timing.End - recordingTime) > RecordingTimeTolerance)
                        continue;
                    
                    chosen.Add(watch);
                    edits.Add(new RecordingEdit { Watch = watch, Value = watch.Binding.Read(watch.Module, watch.Target) });
                }
            }

            // Validate the whole gesture before touching either the scene or the definitions
            foreach (RecordingEdit edit in edits)
            {
                if (!CanRecordEdit(edit))
                    return modifications;
            }

            processingRecording = true;
            UnityEngine.Object definitionTarget = definitionOwner.targetObject;
            string definitionBefore = null;
            string ownerBefore = null;
           
            try
            {
                foreach (RecordingEdit edit in edits)
                    edit.Watch.Binding.Restore(edit.Watch.Module, edit.Watch.Target, edit.Watch.Value);
         
                previewGUI.StopPreview();

                foreach (RecordingEdit edit in edits)
                    PrepareRecordingEdit(edit);
               
                previewGUI.StopPreview();

                // Providers are extension code. Retain a rollback image so a failed conversion
                // cannot leave half of a multi-object gesture in the sequence.
                definitionBefore = EditorJsonUtility.ToJson(definitionTarget);
                if (definitionTarget != owner)
                    ownerBefore = EditorJsonUtility.ToJson(owner);

                // Complete snapshots are needed here: delayed RecordObject diffs run after this callback.
                Undo.RegisterCompleteObjectUndo(definitionOwner.targetObject, "Record Tween Values");
                if (definitionOwner != sequenceOwner)
                    Undo.RegisterCompleteObjectUndo(owner, "Record Tween Values");

                if (activePreset != null && edits.Exists(edit => edit.Watch.Index < 0
                    || recordingTime > edit.Start + RecordingTimeTolerance && recordingTime < edit.End - RecordingTimeTolerance))
                {
                    // Preset targets are indexed per instance. Inserting into a shared asset would
                    // silently reassign targets in its other consumers, including unloaded scenes.
                    GetBoundSequence().DetachPreset();
                    sequenceOwner.Update();
                    BindDefinitionSource(force: true);
                    ShowNotification(new GUIContent("Detached this sequence to add recorded segments. The shared preset is unchanged."));
                }

                // Insertions after higher indices cannot invalidate the lower original indices.
                edits.Sort((a, b) => b.InsertIndex.CompareTo(a.InsertIndex));
                foreach (RecordingEdit edit in edits)
                    ApplyRecordingEdit(edit);

                ApplySerializedSources();
                foreach (RecordingEdit edit in edits)
                {
                    GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(edit.Watch.Target);
                    if (prefabRoot != null && recordingPrefabs.TryGetValue(prefabRoot, out RecordingPrefabState state))
                    {
                        if (!state.Recorded.Exists(watch => watch.Target == edit.Watch.Target && watch.Binding == edit.Watch.Binding))
                            state.Recorded.Add(edit.Watch);
                    }
                }

                BuildClipTimings();
                previewGUI.ScrubPreview(recordingTime);
                SceneView.RepaintAll();
                Repaint();
                List<UndoPropertyModification> remaining = new();

                foreach (UndoPropertyModification modification in modifications)
                {
                    bool recorded = false;

                    foreach (RecordingEdit edit in edits)
                    {
                        if (modification.currentValue.target == edit.Watch.Target && edit.Watch.Binding.Matches(modification.currentValue.propertyPath))
                        {
                            recorded = true;
                            break;
                        }
                    }

                    if (!recorded)
                        remaining.Add(modification);
                }
                return remaining.ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, owner);
                StopRecording();
              
                if (definitionTarget != null && definitionBefore != null)
                    EditorJsonUtility.FromJsonOverwrite(definitionBefore, definitionTarget);
                if (owner != null && ownerBefore != null)
                    EditorJsonUtility.FromJsonOverwrite(ownerBefore, owner);
              
                sequenceOwner?.Update();
                definitionOwner?.Update();
                BindDefinitionSource(force: true);
              
                foreach (RecordingEdit edit in edits)
                {
                    try
                    {
                        if (edit.Watch.Target != null)
                            edit.Watch.Binding.Restore(edit.Watch.Module, edit.Watch.Target, edit.Value);
                    }
                    catch (Exception restoreException) { Debug.LogException(restoreException, owner); }
                }

                ShowNotification(new GUIContent("Recording stopped after a provider error. The gesture was left as a normal scene edit."));
                return modifications;
            }
            finally
            {
                processingRecording = false;
                RefreshRecordingWatches();
            }
        }

        bool CanRecordEdit(RecordingEdit edit)
        {
            RecordingWatch watch = edit.Watch;
            if (!string.IsNullOrEmpty(watch.Binding.UnavailableReason))
            {
                ShowNotification(new GUIContent(watch.Binding.UnavailableReason));
                return false;
            }

            foreach (RecordingWatch other in recordingWatches)
            {
                if (other.Index < 0 || other.Index == watch.Index || other.Target != watch.Target || other.Binding.Channel != watch.Binding.Channel)
                    continue;

                ClipTiming otherTiming = clipTimings[other.Index];
                if (recordingTime > otherTiming.Start + RecordingTimeTolerance && recordingTime < otherTiming.End - RecordingTimeTolerance)
                {
                    ShowNotification(new GUIContent("Overlapping clips write this property. Resolve their overlap before recording."));
                    return false;
                }
            }

            if (watch.Index < 0)
                return true;

            ClipTiming timing = clipTimings[watch.Index];
            SerializedProperty settings = clips.GetArrayElementAtIndex(watch.Index).FindPropertyRelative("settings");
            Ease ease = (Ease)settings.FindPropertyRelative("ease").enumValueIndex;
            AnimationCurve curve = settings.FindPropertyRelative("customEase").animationCurveValue;
            if (Mathf.Abs(TweenEase.Evaluate(ease, 0f, curve)) > RecordingTimeTolerance
                || Mathf.Abs(TweenEase.Evaluate(ease, 1f, curve) - 1f) > RecordingTimeTolerance)
            {
                ShowNotification(new GUIContent("Recording requires an ease curve that starts at 0 and ends at 1."));
                return false;
            }

            float localTime = recordingTime - timing.Start;
            if (timing.Loop != LoopMode.None && localTime > RecordingTimeTolerance
                && recordingTime < timing.End - RecordingTimeTolerance)
            {
                ShowNotification(new GUIContent("Record loop endpoints, or disable looping before adding a waypoint."));
                return false;
            }

            return true;
        }

        void RestoreRecordingPrefabOverrides()
        {
            foreach (KeyValuePair<GameObject, RecordingPrefabState> entry in recordingPrefabs)
            {
                if (entry.Key == null || entry.Value.Recorded.Count == 0)
                    continue;

                List<PropertyModification> retained = new();
                PropertyModification[] current = PrefabUtility.GetPropertyModifications(entry.Key);
                if (current != null)
                {
                    foreach (PropertyModification modification in current)
                    {
                        if (!IsRecordedPrefabProperty(entry.Value, modification))
                            retained.Add(modification);
                    }
                }

                if (entry.Value.Original != null)
                {
                    foreach (PropertyModification modification in entry.Value.Original)
                    {
                        if (IsRecordedPrefabProperty(entry.Value, modification))
                            retained.Add(modification);
                    }
                }

                PrefabUtility.SetPropertyModifications(entry.Key, retained.ToArray());
            }
        }

        static bool IsRecordedPrefabProperty(RecordingPrefabState state, PropertyModification modification)
        {
            foreach (RecordingWatch watch in state.Recorded)
            {
                if (watch.Target != null && watch.Binding.Matches(modification.propertyPath)
                    && (modification.target == PrefabUtility.GetCorrespondingObjectFromSource(watch.Target)
                        || modification.target == PrefabUtility.GetCorrespondingObjectFromOriginalSource(watch.Target)))
                    return true;
            }
            return false;
        }

        void PrepareRecordingEdit(RecordingEdit edit)
        {
            RecordingWatch watch = edit.Watch;
            bool active = watch.Index >= 0 && recordingTime >= clipTimings[watch.Index].Start - RecordingTimeTolerance
                && recordingTime <= clipTimings[watch.Index].End + RecordingTimeTolerance;
            if (active)
            {
                ClipTiming timing = clipTimings[watch.Index];
                edit.Start = timing.Start;
                edit.End = timing.End;
                edit.GroupStart = timing.GroupStart;
                edit.InsertIndex = watch.Index;
                previewGUI.ScrubPreview(timing.Start);
                edit.From = watch.Binding.Read(watch.Module, watch.Target);
                TweenClip sampleClip = new() { Target = watch.Target };
                sampleClip.Settings.CopyFrom(GetBoundSequence().Clips[watch.Index].Settings);
                sampleClip.Settings.Module = watch.Module.Clone();
                sampleClip.Settings.Delay = 0f;
                sampleClip.Settings.Loop = LoopMode.None;
                TweenSequence sampleSequence = new();
                sampleSequence.InlineClips.Add(sampleClip);
                using TweenSequenceSampler endpointSampler = new(owner.gameObject, sampleSequence);
                endpointSampler.Sample(timing.Duration);
                edit.To = watch.Binding.Read(watch.Module, watch.Target);
                return;
            }

            // A new channel starts in the group under the playhead, preserving all earlier flow groups
            edit.InsertIndex = FindGroupSlot(recordingTime + RecordingTimeTolerance, out edit.GroupStart);

            edit.Start = edit.GroupStart;
            foreach (RecordingWatch other in recordingWatches)
            {
                if (other.Index < 0 || other.Target != watch.Target || other.Binding.Channel != watch.Binding.Channel)
                    continue;
             
                ClipTiming timing = clipTimings[other.Index];
                if (timing.End <= recordingTime && timing.End >= edit.Start)
                    edit.Start = timing.End;
            }

            edit.End = recordingTime;
            previewGUI.ScrubPreview(edit.Start);
            edit.From = watch.Binding.Read(watch.Module, watch.Target);
            edit.To = edit.Value;
            // Negative index distinguishes a new segment from an existing definition
            edit.Watch = new RecordingWatch 
            { 
                Binding = watch.Binding,
                Target = watch.Target, 
                Module = watch.Module, 
                Index = -1 
            };
        }

        void ApplyRecordingEdit(RecordingEdit edit)
        {
            RecordingWatch watch = edit.Watch;
            TweenModule module = watch.Module.Clone();
            if (watch.Index < 0)
            {
                watch.Binding.SetValues(module, edit.Start == edit.End ? edit.Value : edit.From, edit.Value);
                InsertClip(edit.InsertIndex, watch.Target, module, ClipFlow.Parallel, edit.Start - edit.GroupStart, edit.End - edit.Start, Ease.Linear);
                return;
            }

            int index = watch.Index;
            SerializedProperty settings = clips.GetArrayElementAtIndex(index).FindPropertyRelative("settings");
      
            if (recordingTime <= edit.Start + RecordingTimeTolerance)
                watch.Binding.SetValues(module, edit.Value, edit.End == edit.Start ? edit.Value : edit.To);
            else if (recordingTime >= edit.End - RecordingTimeTolerance)
            {
                ClipTiming timing = clipTimings[index];
                bool returnsToStart = timing.Loop == LoopMode.Yoyo && timing.Iterations % 2 == 0;
                watch.Binding.SetValues(module, returnsToStart ? edit.Value : edit.From, returnsToStart ? edit.To : edit.Value);
            }
            else
            {
                TweenModule continuation = module.Clone();
                watch.Binding.SetValues(module, edit.From, edit.Value);
                watch.Binding.SetValues(continuation, edit.Value, edit.To);
                Ease ease = (Ease)settings.FindPropertyRelative("ease").enumValueIndex;
                AnimationCurve curve = settings.FindPropertyRelative("customEase").animationCurveValue;
                settings.FindPropertyRelative("module").managedReferenceValue = module;
                settings.FindPropertyRelative("duration").floatValue = recordingTime - edit.Start;
                InsertClip(index + 1, watch.Target, continuation, ClipFlow.Parallel, recordingTime - edit.GroupStart, edit.End - recordingTime, ease, curve);
                return;
            }

            settings.FindPropertyRelative("module").managedReferenceValue = module;
            SelectTrack(index);
        }
    }
}