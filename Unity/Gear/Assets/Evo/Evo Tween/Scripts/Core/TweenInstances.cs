using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Evo.Tween.Diagnostics;

namespace Evo.Tween
{
    /// <summary>
    /// Scheduler-facing contract implemented by pooled code tweens and component timelines.
    /// </summary>
    internal interface ITweenInstance
    {
        UpdatePhase UpdatePhase { get; }
        bool IsAlive { get; }

        /// <summary>
        /// Slot in the scheduler list of <see cref="ScheduledPhase"/>. Both are -1 while unscheduled.
        /// </summary>
        int SchedulerIndex { get; set; }
        int ScheduledPhase { get; set; }

        bool Tick(float deltaTime, float unscaledDeltaTime);
        TweenDebugInfo GetDebugInfo();
        void OnRemoved();
    }

    /// <summary>
    /// Versioned control surface used by <see cref="TweenHandle"/> without exposing pooled instances.
    /// </summary>
    internal interface ITweenControl
    {
        int Version { get; }
        bool IsActive { get; }
        bool IsPaused { get; }

        void SetDelay(float delay);
        void SetEase(Ease ease, AnimationCurve customCurve);
        void SetLoops(int iterations, LoopMode loop);
        void SetSpeed(float speed);
        void SetTimeMode(TimeMode timeMode);
        void SetUpdatePhase(UpdatePhase updatePhase);
        void SetOnComplete(Action callback);
        void Pause();
        void Resume();
        void Complete();
        void Kill();
    }

    /// <summary>
    /// Identifies a target property so the scheduler can replace a tween already writing to it.
    /// </summary>
    internal interface ITweenPropertyTarget
    {
        UnityEngine.Object Target { get; }
        long PropertyKey { get; }
    }

    /// <summary>
    /// Applies per-binding settings to a reusable module without allocating a configuration object.
    /// </summary>
    internal interface ITweenModuleConfiguration<TModule> where TModule : TweenModule
    {
        void Apply(TModule module);
    }

    internal readonly struct DefaultTweenModuleConfiguration<TModule> : ITweenModuleConfiguration<TModule> where TModule : TweenModule
    {
        public void Apply(TModule module) { }
    }

    /// <summary>
    /// Shared timing and lifecycle implementation used by every pooled code tween.
    /// </summary>
    internal abstract class TweenInstance : ITweenInstance, ITweenControl
    {
        TweenClock clock;
        Action onComplete;
        AnimationCurve customEase;
        Ease ease;
        TimeMode timeMode;
        UpdatePhase updatePhase;
        bool active;
        bool paused;
        bool returnedToPool;
        int evaluationDepth;

        // Property conflict registration, recorded by the scheduler so removal never recomputes a key
        internal UnityEngine.Object RegisteredTarget;
        internal long RegisteredPropertyKey;

        internal bool IsEvaluating => evaluationDepth > 0;
        public int SchedulerIndex { get; set; } = -1;
        public int ScheduledPhase { get; set; } = -1;

        public int Version { get; private set; }
        public bool IsActive => active;
        public bool IsPaused => active && paused;
        public bool IsAlive => active && CanApply;
        public UpdatePhase UpdatePhase => updatePhase;

        protected virtual string DebugName => GetType().Name;
        protected virtual UnityEngine.Object DebugTarget => null;
        protected virtual long DebugPropertyKey => 0L;
        protected float NormalizedTime { get; private set; }

        protected TweenInstance() => ResetSettings();

        /// <summary>
        /// Applies builder settings before <see cref="Begin"/>, so the first written value already uses the final ease.
        /// </summary>
        internal void ApplySettings<T>(in TweenBuilder<T> builder) where T : struct
        {
            clock.SetDelay(builder.Delay);
            clock.SetLoops(builder.Iterations, builder.Loop);
            clock.SetSpeed(builder.Speed);
            ease = builder.Ease;
            customEase = builder.CustomEase;
            timeMode = builder.TimeMode;
            updatePhase = builder.UpdatePhase;
            onComplete = builder.Completion;
        }

        /// <summary>
        /// Starts a run with the current settings. Pooled instances return to default settings when removed.
        /// </summary>
        protected void Begin(float duration)
        {
            Version++;
            paused = false;
            active = true;
            NormalizedTime = 0f;
            clock.Restart(duration);

            evaluationDepth++;
            try
            {
                if (CanApply) { Apply(TweenEase.Evaluate(ease, 0f, customEase)); }
                else { active = false; }
            }
            finally { EndEvaluation(); }
        }

        void ResetSettings()
        {
            clock.Reset(0f, 0f, 1f, 1, LoopMode.None);
            onComplete = null;
            ease = Ease.Linear;
            customEase = null;
            timeMode = TimeMode.Scaled;
            updatePhase = UpdatePhase.Update;
        }

        internal void OnRented() => returnedToPool = false;

        public bool Tick(float deltaTime, float unscaledDeltaTime)
        {
            evaluationDepth++;
            try { return TickInternal(deltaTime, unscaledDeltaTime); }
            finally { EndEvaluation(); }
        }

        bool TickInternal(float deltaTime, float unscaledDeltaTime)
        {
            if (!active || !CanApply)
            {
                active = false;
                return false;
            }

            if (paused)
                return true;

            float delta = timeMode == TimeMode.Unscaled ? unscaledDeltaTime : deltaTime;
            bool completed = clock.Tick(delta, out float normalizedTime);
            NormalizedTime = normalizedTime;
            Apply(TweenEase.Evaluate(ease, normalizedTime, customEase));

            // A setter is allowed to kill or complete its own tween while it is being evaluated
            if (!active)
                return false;

            if (!completed)
                return true;

            active = false;
            Action callback = onComplete;
            onComplete = null;
            callback?.Invoke();
            return active;
        }

        public TweenDebugInfo GetDebugInfo()
        {
            return new TweenDebugInfo(DebugName, DebugTarget, IsPaused, updatePhase, timeMode, clock.Position, clock.Duration, 
                clock.DelayRemaining, clock.Speed, clock.CompletedIterations, clock.Iterations, clock.Loop, DebugPropertyKey);
        }

        public void SetDelay(float delay) => clock.SetDelay(delay);

        public virtual void SetEase(Ease value, AnimationCurve curve)
        {
            ease = value;
            customEase = curve;
        }

        public void SetLoops(int iterations, LoopMode loop) => clock.SetLoops(iterations, loop);
        public void SetSpeed(float speed) => clock.SetSpeed(speed);
        public void SetTimeMode(TimeMode value) => timeMode = value;

        public void SetUpdatePhase(UpdatePhase value)
        {
            if (updatePhase == value)
                return;

            updatePhase = value;
            if (ScheduledPhase >= 0)
                TweenScheduler.NotifyPhaseChanged(this);
        }

        public void SetOnComplete(Action callback) => onComplete = callback;
        public void Pause() => paused = active;
        public void Resume() => paused = false;

        public void Complete()
        {
            if (!active)
                return;

            // Mark inactive before applying so a setter cannot recursively complete this instance
            active = false;

            Action callback = onComplete;
            onComplete = null;
            evaluationDepth++;

            try
            {
                if (CanApply)
                {
                    // Same value as natural completion: the start after an even Yoyo count, and curve(1) for custom eases
                    float finalTime = clock.FinalNormalizedTime;
                    NormalizedTime = finalTime;
                    Apply(TweenEase.Evaluate(ease, finalTime, customEase));
                }

                callback?.Invoke();
            }
            finally { EndEvaluation(); }
        }

        void EndEvaluation()
        {
            evaluationDepth--;
            TweenScheduler.FlushDeferredClear();
        }

        public void Kill() => active = false;

        public void OnRemoved()
        {
            if (returnedToPool)
                return;

            returnedToPool = true;
            active = false;
            paused = false;
            NormalizedTime = 0f;
            ResetSettings();

            // Invalidate outstanding handles before this instance becomes visible to its pool
            Version++;
            SchedulerIndex = -1;
            ScheduledPhase = -1;
            RegisteredTarget = null;
            RegisteredPropertyKey = 0L;

            try { ResetState(); }
            finally { ReturnToPool(); }
        }

        protected virtual bool CanApply => true;
        protected abstract void Apply(float easedTime);
        protected abstract void ResetState();
        protected abstract void ReturnToPool();
    }

    /// <summary>
    /// Pooled TweenInstance implementation for code-triggered clip timelines.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal sealed class TweenTimelineInstance : TweenInstance
    {
        const int InitialPoolCapacity = 64;
        const string DebugLabel = "Tween Timeline";

        static readonly TweenObjectPool<TweenTimelineInstance> Pool = new(InitialPoolCapacity);

        readonly List<TweenClip> clips = new(4);
        readonly List<float> clipStartTimes = new(4);
        readonly List<float> evaluationBoundaries = new(4);
        UnityEngine.Object debugTarget;
        float currentTime;
        float runtimeDuration;
        bool hasCapturedClip;

        protected override bool CanApply
        {
            get
            {
                if (!hasCapturedClip)
                    return false;

                for (int i = 0; i < clips.Count; i++)
                {
                    if (clips[i]?.IsCaptured == true)
                        return true;
                }

                return false;
            }
        }
        protected override string DebugName => DebugLabel;
        protected override UnityEngine.Object DebugTarget => debugTarget;

        public static TweenTimelineInstance Get() => Pool.Get();

        public bool Configure(TweenClip source, GameObject owner, bool cloneClip, TimeMode timeMode)
        {
            Prepare(owner);
            clips.Add(cloneClip ? TweenClip.RentRuntime(source) : source);
            return CaptureAndBegin(owner, timeMode);
        }

        public bool Configure(IReadOnlyList<TweenClip> source, GameObject owner, TimeMode timeMode)
        {
            Prepare(owner);

            if (clips.Capacity < source.Count)
                clips.Capacity = source.Count;

            for (int i = 0; i < source.Count; i++)
                clips.Add(TweenClip.RentRuntime(source[i]));

            return CaptureAndBegin(owner, timeMode);
        }

        public bool Configure(TweenSequence source, GameObject owner)
        {
            Prepare(owner);
            source.CopyResolvedClips(clips, runtimeCopies: true);
            return CaptureAndBegin(owner, source.TimeMode);
        }

        public override void SetEase(Ease ease, AnimationCurve customCurve)
        {
            for (int i = 0; i < clips.Count; i++)
                clips[i]?.SetEase(ease, customCurve);
        }

        public void Abort()
        {
            for (int i = clips.Count - 1; i >= 0; i--)
                clips[i]?.Restore();

            hasCapturedClip = false;
            Kill();
        }

        protected override void Apply(float normalizedTime)
        {
            TweenSequenceSampler.EvaluateTimeline(clips, clipStartTimes, evaluationBoundaries, runtimeDuration,
                ref currentTime, runtimeDuration * normalizedTime);
        }

        protected override void ResetState()
        {
            for (int i = clips.Count - 1; i >= 0; i--)
                clips[i]?.ReturnRuntime();

            clips.Clear();
            clipStartTimes.Clear();
            evaluationBoundaries.Clear();
            debugTarget = null;
            currentTime = 0f;
            runtimeDuration = 0f;
            hasCapturedClip = false;
        }

        protected override void ReturnToPool() => Pool.Release(this);

        void Prepare(GameObject owner)
        {
            clips.Clear();
            clipStartTimes.Clear();
            evaluationBoundaries.Clear();
            debugTarget = owner;
            currentTime = 0f;
            runtimeDuration = 0f;
            hasCapturedClip = false;
        }

        bool CaptureAndBegin(GameObject owner, TimeMode timeMode)
        {
            try
            {
                runtimeDuration = TweenSequence.BuildTimeline(clips, clipStartTimes);
                TweenSequenceSampler.BuildEvaluationBoundaries(clipStartTimes, runtimeDuration, evaluationBoundaries);

                for (int i = 0; i < clips.Count; i++)
                {
                    TweenClip clip = clips[i];
                    if (clip == null)
                        continue;

                    GameObject clipOwner = owner;
                    if (clipOwner == null)
                    {
                        if (clip.Target == null)
                            throw new InvalidOperationException($"Clip at index {i} requires an explicit Target " +
                                "when the timeline is triggered without an owner.");

                        clipOwner = clip.Target.gameObject;
                    }

                    debugTarget = debugTarget != null ? debugTarget : clip.Target;
                    clip.Capture(clipOwner);
                    hasCapturedClip |= clip.IsCaptured;
                }

                if (!hasCapturedClip)
                    return false;

                TweenClip.ResolveHolds(clips, clipStartTimes);
                Begin(runtimeDuration);
                SetTimeMode(timeMode);
                return IsActive;
            }
            catch
            {
                Abort();
                throw;
            }
        }
    }

    /// <summary>
    /// Pooled code tween that evaluates the same module implementation used by serialized clips.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal sealed class TweenModuleInstance : TweenInstance, ITweenPropertyTarget
    {
        const int InitialPoolCapacity = 64;
        static readonly TweenObjectPool<TweenModuleInstance> Pool = new(InitialPoolCapacity);

        Component target;
        TweenModule module;

        public UnityEngine.Object Target => target;
        public long PropertyKey => module?.PropertyKey ?? 0L;
        protected override bool CanApply => target != null && module != null;
        protected override string DebugName => module?.GetType().Name ?? nameof(TweenModuleInstance);
        protected override UnityEngine.Object DebugTarget => target;
        protected override long DebugPropertyKey => PropertyKey;

        public static TweenModuleInstance Get() => Pool.Get();

        public bool Configure(Component valueTarget, TweenModule valueModule, float duration)
        {
            target = valueTarget;
            module = valueModule;

            try
            {
                // The owner only resolves a missing target, so an explicit target needs no GameObject lookup
                if (!module.Capture(target, null))
                    return false;

                module.Start(duration);
                Begin(duration);
                return IsActive;
            }
            catch
            {
                module.RestoreAfterFailure(target);
                throw;
            }
        }

        protected override void Apply(float easedTime) => module.Evaluate(easedTime, NormalizedTime);

        protected override void ResetState()
        {
            try { module?.ReturnRuntime(); }
            finally
            {
                target = null;
                module = null;
            }
        }

        protected override void ReturnToPool() => Pool.Release(this);
    }

    /// <summary>
    /// Pooled module binding used by built-in fluent extensions. Both the scheduler wrapper and
    /// its module are reused, avoiding reflection, cloning, and per-play managed allocations.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal sealed class TweenModuleValueInstance<TValue, TModule> : TweenInstance, ITweenPropertyTarget
        where TValue : struct
        where TModule : TweenModule, Modules.IValueModule<TValue>, new()
    {
        const int InitialPoolCapacity = 128;
        static readonly TweenObjectPool<TweenModuleValueInstance<TValue, TModule>> Pool = new(InitialPoolCapacity);

        readonly TModule module = new();
        Component target;

        public UnityEngine.Object Target => target;
        public long PropertyKey => module.PropertyKey;
        protected override bool CanApply => target != null;
        protected override string DebugName => typeof(TModule).Name;
        protected override UnityEngine.Object DebugTarget => target;
        protected override long DebugPropertyKey => PropertyKey;

        public static TweenModuleValueInstance<TValue, TModule> Get() => Pool.Get();

        public bool Configure<TConfiguration>(Component valueTarget, TValue from, TValue to, float duration,in TConfiguration configuration)
            where TConfiguration : struct, ITweenModuleConfiguration<TModule>
        {
            target = valueTarget;

            try
            {
                configuration.Apply(module);
                module.SetValues(from, to);

                // The owner only resolves a missing target, so an explicit target needs no GameObject lookup
                if (!module.Capture(target, null))
                    return false;

                module.Start(duration);
                Begin(duration);
                return IsActive;
            }
            catch
            {
                module.RestoreAfterFailure(target);
                throw;
            }
        }

        protected override void Apply(float easedTime) => module.Evaluate(easedTime, NormalizedTime);

        protected override void ResetState()
        {
            try { module.Release(); }
            finally
            {
                module.ResetPooledConfiguration();
                target = null;
            }
        }

        protected override void ReturnToPool() => Pool.Release(this);
    }

    /// <summary>
    /// Pooled tween that interpolates a value and forwards it to an arbitrary setter.
    /// </summary>
    internal sealed class TweenValue<T> : TweenInstance
    {
        static readonly string DebugLabel = $"Value<{typeof(T).Name}>";

        T from;
        T to;
        Action<T> setter;
        Func<T, T, float, T> interpolator;

        protected override string DebugName => DebugLabel;

        public void Configure(T startValue, T endValue, float duration, Action<T> valueSetter, Func<T, T, float, T> valueInterpolator)
        {
            from = startValue;
            to = endValue;
            setter = valueSetter;
            interpolator = valueInterpolator;
            Begin(duration);
        }

        protected override void Apply(float easedTime) => setter(interpolator(from, to, easedTime));

        protected override void ResetState()
        {
            from = default;
            to = default;
            setter = null;
            interpolator = null;
        }

        protected override void ReturnToPool() => TweenPool<T>.Release(this);
    }

    /// <summary>
    /// Pooled tween that writes an interpolated value to a Unity object and participates in property conflict resolution.
    /// </summary>
    internal sealed class TweenTargetValue<TTarget, TValue> : TweenInstance, ITweenPropertyTarget where TTarget : UnityEngine.Object
    {
        static readonly string DebugLabel = $"{typeof(TTarget).Name} / {typeof(TValue).Name}";

        TTarget target;
        TValue from;
        TValue to;
        Action<TTarget, TValue> setter;
        Func<TValue, TValue, float, TValue> interpolator;
        int propertyKey;

        public UnityEngine.Object Target => target;
        public long PropertyKey => propertyKey;
        protected override bool CanApply => target != null;
        protected override string DebugName => DebugLabel;
        protected override UnityEngine.Object DebugTarget => target;
        protected override long DebugPropertyKey => propertyKey;

        public void Configure(TTarget valueTarget, TValue startValue, TValue endValue, float duration,
            Action<TTarget, TValue> valueSetter, Func<TValue, TValue, float, TValue> valueInterpolator, int valuePropertyKey)
        {
            target = valueTarget;
            from = startValue;
            to = endValue;
            setter = valueSetter;
            interpolator = valueInterpolator;
            propertyKey = valuePropertyKey;
            Begin(duration);
        }

        protected override void Apply(float easedTime) => setter(target, interpolator(from, to, easedTime));

        protected override void ResetState()
        {
            target = null;
            from = default;
            to = default;
            setter = null;
            interpolator = null;
            propertyKey = 0;
        }

        protected override void ReturnToPool() => TweenTargetPool<TTarget, TValue>.Release(this);
    }
}