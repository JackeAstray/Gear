using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Serializable clip timeline that can use inline definitions or a shared preset.
    /// Preset definitions stay asset-owned while targets remain local to each sequence instance.
    /// </summary>
    [Serializable]
    public sealed class TweenSequence
    {
        [SerializeField, Tooltip("Optional shared clip timeline. Targets remain local to this sequence.")]
        private TweenSequencePreset preset;

        [SerializeField, Tooltip("Clock used to advance the complete clip timeline.")]
        private TimeMode timeMode = TimeMode.Scaled;

        [SerializeField, Tooltip("Ordered clips used when no preset is assigned.")]
        private List<TweenClip> clips = new();

        [SerializeField, HideInInspector]
        private List<Component> presetTargets = new();

        /// <summary>
        /// Clock used to advance the complete clip timeline.
        /// </summary>
        public TimeMode TimeMode
        {
            get => timeMode;
            set => timeMode = value;
        }

        /// <summary>
        /// Optional shared timeline asset. Assigning a preset replaces the effective inline definitions, but not their data.
        /// </summary>
        public TweenSequencePreset Preset
        {
            get => preset;
            set
            {
                preset = value;
                SynchronizeTargetBindings();
            }
        }

        /// <summary>
        /// Locally serialized definitions retained while a preset is attached.
        /// </summary>
        public List<TweenClip> InlineClips => clips ??= new List<TweenClip>();

        /// <summary>
        /// Effective ordered definitions supplied by the preset or the inline list.
        /// Targets for preset definitions are retrieved with <see cref="GetTarget"/>.
        /// </summary>
        public IReadOnlyList<TweenClip> Clips => preset != null ? preset.Clips : InlineClips;

        /// <summary>
        /// Number of effective clip definitions in this sequence.
        /// </summary>
        public int Count => Clips.Count;

        /// <summary>
        /// Calculated duration of the effective timeline in seconds.
        /// </summary>
        public float Duration => BuildTimeline(Clips, null);

        /// <summary>
        /// Creates an empty inline sequence.
        /// </summary>
        public TweenSequence() { }

        /// <summary>
        /// Creates an independent sequence copy. Asset and component references are retained.
        /// </summary>
        public TweenSequence(TweenSequence source)
        {
            if (source == null)
                return;

            timeMode = source.timeMode;
            preset = source.preset;

            IReadOnlyList<TweenClip> sourceClips = source.InlineClips;
            clips = new List<TweenClip>(sourceClips.Count);
            for (int i = 0; i < sourceClips.Count; i++)
                clips.Add(sourceClips[i]?.Clone());

            presetTargets = new List<Component>(source.presetTargets ?? new List<Component>());
            SynchronizeTargetBindings();
        }

        /// <summary>
        /// Returns an independent serializable copy. Referenced preset and target objects are retained.
        /// </summary>
        public TweenSequence Clone() => new(this);

        /// <summary>
        /// Gets the instance-owned target for an effective clip.
        /// </summary>
        public Component GetTarget(int index)
        {
            ValidateIndex(index);

            if (preset == null)
                return InlineClips[index]?.Target;

            presetTargets ??= new List<Component>();
            return index < presetTargets.Count ? presetTargets[index] : null;
        }

        /// <summary>
        /// Sets the instance-owned target for an effective clip.
        /// </summary>
        public void SetTarget(int index, Component target)
        {
            ValidateIndex(index);

            if (preset == null)
            {
                InlineClips[index] ??= new TweenClip();
                InlineClips[index].Target = target;
                return;
            }

            SynchronizeTargetBindings();
            presetTargets[index] = target;
        }

        /// <summary>
        /// Removes the preset. By default, the effective preset definitions and local targets are copied inline.
        /// </summary>
        public void DetachPreset(bool copyClips = true)
        {
            if (preset == null)
                return;

            if (copyClips)
            {
                List<TweenClip> resolved = new(Count);
                CopyResolvedClips(resolved);
                clips = resolved;
            }

            preset = null;
            presetTargets ??= new List<Component>();
            presetTargets.Clear();
        }

        /// <summary>
        /// Triggers the effective timeline. Every clip requires an explicit local target.
        /// </summary>
        public TweenHandle Trigger() => TweenClip.TriggerSequence(this, null);

        /// <summary>
        /// Triggers the effective timeline. The owner resolves compatible components for entries without a local target.
        /// </summary>
        public TweenHandle Trigger(GameObject owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            return TweenClip.TriggerSequence(this, owner);
        }

        internal void CopyResolvedClips(List<TweenClip> destination, bool cloneInline = true, bool runtimeCopies = false)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            destination.Clear();
            IReadOnlyList<TweenClip> definitions = Clips;
            presetTargets ??= new List<Component>();

            if (destination.Capacity < definitions.Count)
                destination.Capacity = definitions.Count;

            bool usesPreset = preset != null;
            for (int i = 0; i < definitions.Count; i++)
            {
                TweenClip definition = definitions[i];
                TweenClip clip = usesPreset || cloneInline
                    ? runtimeCopies ? TweenClip.RentRuntime(definition) : definition?.Clone()
                    : definition;

                if (clip != null && usesPreset)
                    clip.Target = i < presetTargets.Count ? presetTargets[i] : null;

                destination.Add(clip);
            }
        }

        internal void Validate()
        {
            clips ??= new List<TweenClip>();
            presetTargets ??= new List<Component>();

            for (int i = 0; i < clips.Count; i++)
                clips[i]?.Validate();

            SynchronizeTargetBindings();
        }

        internal void SynchronizeTargetBindings()
        {
            presetTargets ??= new List<Component>();

            if (preset == null)
                return;

            int targetCount = preset.Count;
            if (presetTargets.Count > targetCount)
            {
                presetTargets.RemoveRange(targetCount, presetTargets.Count - targetCount);
                return;
            }

            while (presetTargets.Count < targetCount)
            {
                int index = presetTargets.Count;
                Component inlineTarget = index < InlineClips.Count ? InlineClips[index]?.Target : null;
                presetTargets.Add(inlineTarget);
            }
        }

        internal static float BuildTimeline(IReadOnlyList<TweenClip> source, List<float> startTimes)
        {
            startTimes?.Clear();

            if (source == null)
                return 0f;

            float groupStart = 0f;
            float groupEnd = 0f;
            bool hasGroup = false;

            for (int i = 0; i < source.Count; i++)
            {
                TweenClip clip = source[i];
                float startTime = 0f;

                if (clip != null)
                {
                    if (!hasGroup || clip.Flow == ClipFlow.Sequential)
                    {
                        groupStart = groupEnd;
                        hasGroup = true;
                    }

                    startTime = TweenMath.AddDurations(groupStart, clip.Delay);
                    groupEnd = Mathf.Max(groupEnd, TweenMath.AddDurations(startTime, clip.TotalDuration));
                }

                startTimes?.Add(startTime);
            }

            return groupEnd;
        }

        void ValidateIndex(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The clip index is outside the effective sequence.");
        }
    }
}