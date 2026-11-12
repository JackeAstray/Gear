using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Captures and samples a <see cref="TweenSequence"/> without scheduling playback.
    /// Useful for editor previews and components that provide their own playback clock.
    /// </summary>
    public sealed class TweenSequenceSampler : IDisposable
    {
        readonly List<TweenClip> runtimeClips = new();
        readonly List<float> clipStartTimes = new();
        readonly List<float> evaluationBoundaries = new();
        readonly bool cloneInlineClips;
        readonly bool cacheClips;
        bool prepared;

        GameObject owner;
        TweenSequence sequence;
        float currentTime;
        float runtimeDuration;
        bool captured;

        /// <summary>
        /// GameObject used to resolve entries without an explicit target.
        /// </summary>
        public GameObject Owner => owner;

        /// <summary>
        /// Sequence currently sampled by this instance.
        /// </summary>
        public TweenSequence Sequence => sequence;

        /// <summary>
        /// Whether target values are currently captured.
        /// </summary>
        public bool HasCapturedValues => captured;

        internal bool HasLiveTargets
        {
            get
            {
                for (int i = 0; i < runtimeClips.Count; i++)
                {
                    if (runtimeClips[i]?.IsCaptured == true)
                        return true;
                }

                return runtimeClips.Count == 0;
            }
        }

        /// <summary>
        /// Current absolute sample position in seconds.
        /// </summary>
        public float CurrentTime => currentTime;

        /// <summary>
        /// Captured duration, or the configured sequence duration before capture.
        /// </summary>
        public float Duration => captured ? runtimeDuration : sequence?.Duration ?? 0f;

        /// <param name="cloneInlineClips">
        /// Clone inline definitions before capture. Disable only when this sampler exclusively owns the sequence's
        /// runtime state, as <see cref="TweenAnimator"/> does.
        /// </param>
        public TweenSequenceSampler(GameObject owner, TweenSequence sequence, bool cloneInlineClips = true, bool cacheClips = false)
        {
            this.cloneInlineClips = cloneInlineClips;
            this.cacheClips = cacheClips;
            SetSource(owner, sequence);
        }

        /// <summary>
        /// Changes the sampled owner and sequence. Captured values are restored before changing source.
        /// </summary>
        public void SetSource(GameObject sequenceOwner, TweenSequence source)
        {
            if (owner == sequenceOwner && ReferenceEquals(sequence, source))
                return;

            Restore();
            ReleasePreparedClips();
            owner = sequenceOwner;
            sequence = source;
        }

        /// <summary>
        /// Captures every effective clip target and samples the start of the timeline.
        /// </summary>
        public void Capture()
        {
            if (captured || owner == null || sequence == null)
                return;

            Prepare();
            int count = runtimeClips.Count;
            captured = true;

            for (int i = 0; i < count; i++)
                runtimeClips[i]?.Capture(owner);

            TweenClip.ResolveHolds(runtimeClips, clipStartTimes);
            currentTime = 0f;
            EvaluateTimeline(runtimeClips, clipStartTimes, evaluationBoundaries, runtimeDuration, ref currentTime, 0f);
        }

        /// <summary>
        /// Resolves clips and reserves sampling storage without reading or writing target values.
        /// Cached samplers retain this configuration until Refresh, SetSource, or Dispose.
        /// </summary>
        public void Prepare(bool rebuild = false)
        {
            if (rebuild)
            {
                Restore();
                ReleasePreparedClips();
            }

            if (prepared || sequence == null)
                return;

            try { sequence.CopyResolvedClips(runtimeClips, cloneInlineClips, runtimeCopies: true); }
            catch
            {
                for (int i = runtimeClips.Count - 1; i >= 0; i--)
                    runtimeClips[i]?.ReturnRuntime();

                runtimeClips.Clear();
                throw;
            }

            int count = runtimeClips.Count;
            if (clipStartTimes.Capacity < count)
                clipStartTimes.Capacity = count;
            if (evaluationBoundaries.Capacity < count)
                evaluationBoundaries.Capacity = count;

            runtimeDuration = TweenSequence.BuildTimeline(runtimeClips, clipStartTimes);
            BuildEvaluationBoundaries(clipStartTimes, runtimeDuration, evaluationBoundaries);
            prepared = true;
        }

        /// <summary>
        /// Samples the timeline at an absolute time, capturing first when required.
        /// </summary>
        public void Sample(float time)
        {
            if (!captured)
                Capture();
            if (!captured)
                return;

            EvaluateTimeline(runtimeClips, clipStartTimes, evaluationBoundaries, runtimeDuration, ref currentTime, time);
        }

        /// <summary>
        /// Rebuilds edited sequence data while preserving a normalized timeline position.
        /// </summary>
        public void Refresh(float normalizedTime)
        {
            Restore();
            ReleasePreparedClips();
            Capture();
            Sample(Duration * Mathf.Clamp01(normalizedTime));
        }

        /// <summary>
        /// Restores every captured target to its value before capture.
        /// </summary>
        public void Restore() => ClearCapture(restoreValues: true);

        /// <summary>
        /// Releases captured targets and runtime module state while preserving the currently sampled values.
        /// </summary>
        public void Release() => ClearCapture(restoreValues: false);

        public void Dispose()
        {
            Restore();
            ReleasePreparedClips();
            owner = null;
            sequence = null;
        }

        void ClearCapture(bool restoreValues)
        {
            if (!captured)
                return;

            for (int i = runtimeClips.Count - 1; i >= 0; i--)
            {
                if (restoreValues)
                    runtimeClips[i]?.Restore();

                runtimeClips[i]?.Release();
            }

            captured = false;
            currentTime = 0f;

            if (!cacheClips)
                ReleasePreparedClips();
        }

        void ReleasePreparedClips()
        {
            for (int i = runtimeClips.Count - 1; i >= 0; i--)
                runtimeClips[i]?.ReturnRuntime();

            prepared = false;
            runtimeDuration = 0f;
            runtimeClips.Clear();
            clipStartTimes.Clear();
            evaluationBoundaries.Clear();
        }

        internal static void BuildEvaluationBoundaries(List<float> startTimes, float duration, List<float> boundaries)
        {
            boundaries.Clear();

            // Sorted insertion instead of List.Sort, which allocates on every call in Unity's Mono
            for (int i = 0; i < startTimes.Count; i++)
            {
                float startTime = startTimes[i];
                if (startTime <= 0f || startTime >= duration)
                    continue;

                int index = boundaries.Count;
                while (index > 0 && boundaries[index - 1] > startTime)
                    index--;

                if ((index > 0 && Mathf.Approximately(boundaries[index - 1], startTime))
                    || (index < boundaries.Count && Mathf.Approximately(boundaries[index], startTime)))
                    continue;

                boundaries.Insert(index, startTime);
            }
        }

        internal static void EvaluateTimeline(List<TweenClip> source, List<float> startTimes,
            List<float> boundaries, float duration, ref float currentTime, float time)
        {
            float targetTime = Mathf.Clamp(time, 0f, duration);
            if (targetTime > currentTime)
            {
                for (int i = 0; i < boundaries.Count; i++)
                {
                    float boundary = boundaries[i];
                    if (boundary <= currentTime)
                        continue;
                    if (boundary >= targetTime)
                        break;

                    EvaluateClips(source, startTimes, boundary);
                }
            }
            else if (targetTime < currentTime)
            {
                for (int i = boundaries.Count - 1; i >= 0; i--)
                {
                    float boundary = boundaries[i];
                    if (boundary >= currentTime)
                        continue;
                    if (boundary <= targetTime)
                        break;

                    EvaluateClips(source, startTimes, boundary);
                }
            }

            currentTime = targetTime;
            EvaluateClips(source, startTimes, currentTime);
        }

        static void EvaluateClips(List<TweenClip> source, List<float> startTimes, float time)
        {
            int count = Mathf.Min(source.Count, startTimes.Count);
            for (int i = 0; i < count; i++)
                source[i]?.Evaluate(time, startTimes[i]);
        }
    }
}