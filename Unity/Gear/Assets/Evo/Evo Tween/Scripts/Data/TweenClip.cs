using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Reusable, inline-serializable clip unit.
    /// </summary>
    [Serializable, NoAutoStaticsCleanup]
    public sealed class TweenClip
    {
        static TweenClip pooled;

        [SerializeField, Tooltip("Component to animate. A matching component is resolved from the owner when empty.")]
        private Component target;

        [SerializeField, Tooltip("Parallel shares the current group start. Sequential begins after the group finishes.")]
        private ClipFlow flow = ClipFlow.Parallel;

        [SerializeField, Tooltip("Type-specific values and playback settings for this clip.")]
        private TweenClipSettings settings;

        [NonSerialized] TweenClip nextPooled;
        [NonSerialized] bool runtimeCopy;
        [NonSerialized] TweenModule activeModule;
        [NonSerialized] GameObject owner;
        [NonSerialized] AnimationCurve activeCustomEase;
        [NonSerialized] Ease activeEase;
        [NonSerialized] float activeDuration;
        [NonSerialized] float activeInverseDuration;
        [NonSerialized] float activeTotalDuration;
        [NonSerialized] int activeIterations;
        [NonSerialized] LoopMode activeLoop;
        [NonSerialized] bool captured;
        [NonSerialized] bool started;
        [NonSerialized] bool holdsBeforeStart;
        [NonSerialized] bool holding;

        /// <summary>
        /// Explicit component animated by this entry. When null, the module resolves a compatible component from its owner.
        /// </summary>
        public Component Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>
        /// How this entry is placed relative to the current timeline group.
        /// </summary>
        public ClipFlow Flow
        {
            get => flow;
            set => flow = value;
        }

        /// <summary>
        /// Settings used for playback.
        /// </summary>
        public TweenClipSettings Settings => settings ??= new TweenClipSettings();

        /// <summary>
        /// Alias retained for callers that explicitly distinguish inline settings.
        /// </summary>
        public TweenClipSettings InlineSettings => Settings;

        /// <summary>
        /// Configured delay before this clip begins.
        /// </summary>
        public float Delay => Settings.Delay;

        /// <summary>
        /// Configured playback duration.
        /// </summary>
        public float Duration => Settings.Duration;

        /// <summary>
        /// Configured playback behavior after one iteration.
        /// </summary>
        public LoopMode Loop => Settings.Loop;

        /// <summary>
        /// Configured number of clip iterations. None always evaluates once.
        /// </summary>
        public int Iterations => Settings.Iterations;

        /// <summary>
        /// Effective playback duration including every configured iteration.
        /// </summary>
        public float TotalDuration => TweenMath.GetIterationsDuration(Duration, Loop, Iterations);

        /// <summary>
        /// Whether this instance currently owns captured runtime target state.
        /// </summary>
        public bool IsCaptured => captured && activeModule != null && activeModule.HasTarget;

        /// <summary>
        /// Creates a clip with default inline settings.
        /// </summary>
        public TweenClip() => settings = new TweenClipSettings();

        internal TweenClip(TweenClipSettings source, Component clipTarget)
        {
            target = clipTarget;
            settings = source?.Clone() ?? new TweenClipSettings();
        }

        /// <summary>
        /// Creates an independent clip copy while retaining the referenced target object.
        /// </summary>
        public TweenClip(TweenClip source)
        {
            if (source == null)
                return;

            target = source.target;
            flow = source.flow;
            settings = source.Settings.Clone();
        }

        /// <summary>
        /// Returns an independent serializable copy. The target reference is retained.
        /// </summary>
        public TweenClip Clone() => new(this);

        internal static TweenClip RentRuntime(TweenClip source)
        {
            if (source == null)
                return null;

            TweenClip copy = RentRuntime(source.Settings, source.target);
            copy.flow = source.flow;
            return copy;
        }

        static TweenClip RentRuntime(TweenClipSettings source, Component clipTarget)
        {
            TweenClip copy = pooled;
            if (copy == null) { copy = new TweenClip(); }
            else
            {
                pooled = copy.nextPooled;
                copy.nextPooled = null;
            }

            copy.runtimeCopy = true;
            copy.target = clipTarget;
            copy.flow = ClipFlow.Parallel;

            try { copy.settings.CopyRuntimeFrom(source); }
            catch
            {
                copy.ReturnRuntime();
                throw;
            }

            return copy;
        }

        internal void ReturnRuntime()
        {
            Release();

            if (!runtimeCopy)
                return;

            runtimeCopy = false;

            try { settings.ReleaseRuntime(); }
            finally
            {
                target = null;
                nextPooled = pooled;
                pooled = this;
            }
        }

        /// <summary>
        /// Triggers this clip using its explicit target as the clip owner.
        /// </summary>
        public TweenHandle Trigger(TimeMode timeMode = TimeMode.Scaled)
        {
            if (target == null)
                throw new InvalidOperationException("The clip requires an explicit Target. " +
                    "Use Trigger(owner) when the module should resolve its target from a GameObject.");

            return TriggerInternal(this, target.gameObject, true, timeMode);
        }

        /// <summary>
        /// Triggers this clip with an owner used to resolve its target when no explicit component is assigned.
        /// </summary>
        public TweenHandle Trigger(GameObject clipOwner, TimeMode timeMode = TimeMode.Scaled)
        {
            if (clipOwner == null)
                throw new ArgumentNullException(nameof(clipOwner));

            return TriggerInternal(this, clipOwner, true, timeMode);
        }

        /// <summary>
        /// Triggers a sequence of clips. Every clip requires an explicit target.
        /// Parallel and Sequential flow values are evaluated in list order.
        /// The supplied time mode advances the complete timeline.
        /// </summary>
        public static TweenHandle Trigger(IReadOnlyList<TweenClip> clips, TimeMode timeMode = TimeMode.Scaled)
        {
            if (clips == null)
                throw new ArgumentNullException(nameof(clips));

            return TriggerInternal(clips, null, timeMode);
        }

        /// <summary>
        /// Triggers a sequence of clips. The owner resolves compatible components for entries without an explicit target.
        /// Parallel and Sequential flow values are evaluated in list order.
        /// The supplied time mode advances the complete timeline.
        /// </summary>
        public static TweenHandle Trigger(GameObject owner, IReadOnlyList<TweenClip> clips, TimeMode timeMode = TimeMode.Scaled)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (clips == null)
                throw new ArgumentNullException(nameof(clips));

            return TriggerInternal(clips, owner, timeMode);
        }

        /// <summary>
        /// Copies settings into the inline payload without changing the target or flow.
        /// </summary>
        public void CopySettingsFrom(TweenClipSettings source) => settings = source?.Clone() ?? new TweenClipSettings();

        internal static TweenHandle TriggerSettings(Component target, TweenClipSettings settings, TimeMode timeMode)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!Application.isPlaying)
                return default;

            TweenClip clip = RentRuntime(settings, target);
            return TriggerInternal(clip, target.gameObject, false, timeMode);
        }

        internal static TweenHandle TriggerSettings(GameObject target, TweenClipSettings settings, TimeMode timeMode)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (!Application.isPlaying)
                return default;

            TweenClip clip = RentRuntime(settings, null);
            return TriggerInternal(clip, target, false, timeMode);
        }

        internal void Capture(GameObject clipOwner)
        {
            if (captured)
                Restore();

            owner = clipOwner;
            started = false;

            try
            {
                TweenClipSettings activeSettings = Settings;
                activeSettings.Validate();
                activeDuration = activeSettings.Duration;
                activeInverseDuration = activeDuration > 0f ? 1f / activeDuration : 0f;
                activeLoop = activeSettings.Loop;
                activeIterations = activeSettings.Iterations;
                activeTotalDuration = TweenMath.GetIterationsDuration(activeDuration, activeLoop, activeIterations);
                activeEase = activeSettings.Ease;
                activeCustomEase = activeSettings.CustomEase;
                activeModule = activeSettings.Module;

                if (activeModule == null)
                {
                    Release();
                    return;
                }

                captured = activeModule.Capture(target, clipOwner);
                if (!captured)
                    TryReleaseAfterFailure();
                else
                    holdsBeforeStart = activeSettings.HoldBeforeStart && activeModule.CanHoldBeforeStart;
            }
            catch (Exception exception)
            {
                captured = false;
                Debug.LogException(exception, clipOwner);
                TryReleaseAfterFailure();
            }
        }

        internal void Evaluate(float timelineTime, float startTime)
        {
            if (captured && !activeModule.HasTarget)
            {
                Release();
                return;
            }

            // A held From value never changes, so it is written once each time the timeline moves before the clip
            bool beforeStart = timelineTime < startTime;
            if (!captured || (beforeStart && (!holdsBeforeStart || holding)))
                return;

            try
            {
                if (!started)
                {
                    activeModule.Start(activeDuration);
                    started = true;
                }

                float normalizedTime = beforeStart ? 0f : GetNormalizedTime(timelineTime - startTime);
                activeModule.Evaluate(TweenEase.Evaluate(activeEase, normalizedTime, activeCustomEase), normalizedTime);
                holding = beforeStart;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, owner);
                captured = false;
                started = false;
                TryReleaseAfterFailure();
            }
        }

        internal void SetEase(Ease ease, AnimationCurve customEase)
        {
            activeEase = ease;
            activeCustomEase = customEase;
        }

        float GetNormalizedTime(float elapsed)
        {
            if (activeDuration <= 0f)
                return GetFinalNormalizedTime();

            if (elapsed >= activeTotalDuration)
                return GetFinalNormalizedTime();

            if (activeLoop == LoopMode.None)
                return Mathf.Clamp01(elapsed * activeInverseDuration);

            int completedIterations = Mathf.FloorToInt(elapsed * activeInverseDuration);
            float normalizedTime = Mathf.Clamp01((elapsed % activeDuration) * activeInverseDuration);
            return activeLoop == LoopMode.Yoyo && (completedIterations & 1) != 0
                ? 1f - normalizedTime
                : normalizedTime;
        }

        float GetFinalNormalizedTime() => activeLoop == LoopMode.Yoyo && (activeIterations & 1) == 0 ? 0f : 1f;

        /// <summary>
        /// Keeps Hold Before Start only on the clip that writes each property first. Later clips follow the value
        /// left by earlier ones, and at equal start times the clip evaluated last decides the value, as in playback.
        /// Call after capture, before the first evaluation.
        /// </summary>
        internal static void ResolveHolds(List<TweenClip> clips, List<float> startTimes)
        {
            int count = Mathf.Min(clips.Count, startTimes.Count);
            for (int i = 0; i < count; i++)
            {
                TweenClip clip = clips[i];
                if (clip == null || !clip.holdsBeforeStart)
                    continue;

                float startTime = startTimes[i];
                if (startTime <= 0f)
                {
                    clip.holdsBeforeStart = false;
                    continue;
                }

                long propertyKey = clip.activeModule.PropertyKey;
                for (int j = 0; j < count; j++)
                {
                    TweenClip other = clips[j];
                    if (j == i || other == null || !other.captured || startTimes[j] > startTime || (startTimes[j] == startTime && j < i))
                        continue;

                    if (clip.activeModule.WritesSameProperty(other.activeModule, propertyKey))
                    {
                        clip.holdsBeforeStart = false;
                        break;
                    }
                }
            }
        }

        internal void Restore()
        {
            if (!captured && activeModule == null)
                return;

            try { activeModule?.Restore(); }
            catch (Exception exception) { Debug.LogException(exception, owner); }
            finally { Release(); }
        }

        internal void Validate()
        {
            settings ??= new TweenClipSettings();
            settings.Validate();
        }

        static TweenHandle TriggerInternal(TweenClip clip, GameObject clipOwner, bool cloneClip, TimeMode timeMode)
        {
            if (!Application.isPlaying)
                return default;

            TweenTimelineInstance tween = TweenTimelineInstance.Get();

            try
            {
                if (!tween.Configure(clip, clipOwner, cloneClip, timeMode))
                {
                    tween.OnRemoved();
                    return default;
                }

                if (!TweenScheduler.Add(tween))
                {
                    tween.Abort();
                    tween.OnRemoved();
                    return default;
                }

                return new TweenHandle(tween, tween.Version);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        static TweenHandle TriggerInternal(IReadOnlyList<TweenClip> clips, GameObject owner, TimeMode timeMode)
        {
            if (!Application.isPlaying || clips.Count == 0)
                return default;

            TweenTimelineInstance tween = TweenTimelineInstance.Get();

            try
            {
                if (!tween.Configure(clips, owner, timeMode))
                {
                    tween.OnRemoved();
                    return default;
                }

                if (!TweenScheduler.Add(tween))
                {
                    tween.Abort();
                    tween.OnRemoved();
                    return default;
                }

                return new TweenHandle(tween, tween.Version);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        internal static TweenHandle TriggerSequence(TweenSequence sequence, GameObject owner)
        {
            if (sequence == null)
                throw new ArgumentNullException(nameof(sequence));

            if (!Application.isPlaying || sequence.Count == 0)
                return default;

            TweenTimelineInstance tween = TweenTimelineInstance.Get();

            try
            {
                if (!tween.Configure(sequence, owner))
                {
                    tween.OnRemoved();
                    return default;
                }

                if (!TweenScheduler.Add(tween))
                {
                    tween.Abort();
                    tween.OnRemoved();
                    return default;
                }

                return new TweenHandle(tween, tween.Version);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        void TryReleaseAfterFailure()
        {
            try { activeModule?.Restore(); }
            catch { }

            Release();
        }

        internal void Release()
        {
            try { activeModule?.Release(); }
            catch (Exception exception) { Debug.LogException(exception, owner); }

            owner = null;
            activeModule = null;
            activeCustomEase = null;
            activeDuration = 0f;
            activeInverseDuration = 0f;
            activeTotalDuration = 0f;
            activeIterations = 0;
            activeLoop = LoopMode.None;
            captured = false;
            started = false;
            holdsBeforeStart = false;
            holding = false;
        }
    }
}