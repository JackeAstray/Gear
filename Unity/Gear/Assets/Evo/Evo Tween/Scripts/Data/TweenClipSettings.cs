using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Portable module and timing data shared by inline clips and preset assets.
    /// </summary>
    [Serializable]
    public sealed class TweenClipSettings
    {
        [SerializeReference, Tooltip("Module-specific values and settings.")]
        private TweenModule module;

        [SerializeField, Min(0f), Tooltip("Time added before this clip starts within its flow group.")]
        private float delay;

        [SerializeField, Tooltip("Apply the From value when the sequence starts, instead of leaving the target unchanged until this clip begins. " +
            "Has no effect when the clip starts at 0s, or when an earlier clip animates the same property.")]
        private bool holdBeforeStart;

        [SerializeField, Min(0f), Tooltip("Length of one clip iteration in seconds.")]
        private float duration = 0.3f;

        [SerializeField, Tooltip("Playback behavior after a clip iteration finishes.")]
        private LoopMode loop = LoopMode.None;

        [SerializeField, Min(1), Tooltip("Total clip iterations when looping.")]
        private int iterations = 1;

        [SerializeField, Tooltip("Interpolation curve used between the start and target values.")]
        private Ease ease = Ease.OutCubic;

        [SerializeField, Tooltip("Curve evaluated when Ease is set to Custom.")]
        private AnimationCurve customEase = new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        /// <summary>
        /// Module-specific configuration. Replacing it changes the clip type.
        /// </summary>
        public TweenModule Module
        {
            get => module;
            set => module = value;
        }

        public float Delay
        {
            get => delay;
            set => delay = TweenMath.SanitizeNonNegative(value);
        }

        /// <summary>
        /// Applies the From value from the start of the sequence until this clip begins. Ignored when the module
        /// reads its start value when the clip begins (From Current), and when an earlier clip in the same
        /// sequence animates the same property, since that clip already decides the value before this one.
        /// </summary>
        public bool HoldBeforeStart
        {
            get => holdBeforeStart;
            set => holdBeforeStart = value;
        }

        public float Duration
        {
            get => duration;
            set => duration = TweenMath.SanitizeNonNegative(value);
        }

        public LoopMode Loop
        {
            get => loop;
            set => loop = value;
        }

        public int Iterations
        {
            get => Mathf.Max(1, iterations);
            set => iterations = Mathf.Max(1, value);
        }

        public Ease Ease
        {
            get => ease;
            set => ease = value;
        }

        public AnimationCurve CustomEase
        {
            get => customEase;
            set => customEase = value ?? LinearCurve();
        }

        public TweenClipSettings() { }

        public TweenClipSettings(TweenClipSettings source) => CopyFrom(source);

        public TweenClipSettings Clone() => new(this);

        internal void CopyRuntimeFrom(TweenClipSettings source)
        {
            source.Validate();
            module = source.module?.RentRuntime();
            delay = source.Delay;
            holdBeforeStart = source.holdBeforeStart;
            duration = source.Duration;
            loop = source.Loop;
            iterations = source.Iterations;
            ease = source.Ease;

            if (ease == Ease.Custom && source.customEase != null)
                customEase.CopyFrom(source.customEase);
        }

        internal void ReleaseRuntime()
        {
            TweenModule runtimeModule = module;
            module = null;
            runtimeModule?.ReturnRuntime();
        }

        public TweenHandle Trigger(Component target, TimeMode timeMode = TimeMode.Scaled)
        {
            return TweenClip.TriggerSettings(target, this, timeMode);
        }

        public TweenHandle Trigger(GameObject target, TimeMode timeMode = TimeMode.Scaled)
        {
            return TweenClip.TriggerSettings(target, this, timeMode);
        }

        public void CopyFrom(TweenClipSettings source)
        {
            if (source == null)
            {
                module = null;
                delay = 0f;
                holdBeforeStart = false;
                duration = 0.3f;
                loop = LoopMode.None;
                iterations = 1;
                ease = Ease.OutCubic;
                customEase = LinearCurve();
                return;
            }

            module = source.module?.Clone();
            delay = source.delay;
            holdBeforeStart = source.holdBeforeStart;
            duration = source.duration;
            loop = source.loop;
            iterations = source.iterations;
            ease = source.ease;
            customEase = source.customEase != null ? new AnimationCurve(source.customEase.keys) : LinearCurve();
            Validate();
        }

        internal void Validate()
        {
            delay = TweenMath.SanitizeNonNegative(delay);
            duration = TweenMath.SanitizeNonNegative(duration);
            iterations = Mathf.Max(1, iterations);
            customEase ??= LinearCurve();
        }

        static AnimationCurve LinearCurve() => new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    }

    /// <summary>
    /// Serializable settings for a code-driven tween of one supported value type.
    /// </summary>
    [Serializable]
    public sealed class TweenClipSettings<T> where T : struct
    {
        [SerializeField, Tooltip("Value applied at the beginning of the tween.")]
        private T from = TweenClipSettingsDefaultValue<T>.Value;

        [SerializeField, Tooltip("Value applied at the end of the tween.")]
        private T to = TweenClipSettingsDefaultValue<T>.Value;

        [SerializeField, Min(0f), Tooltip("Time added before this tween starts.")]
        private float delay;

        [SerializeField, Min(0f), Tooltip("Length of one tween iteration in seconds.")]
        private float duration = 0.3f;

        [SerializeField, Tooltip("Playback behavior after a tween iteration finishes.")]
        private LoopMode loop = LoopMode.None;

        [SerializeField, Min(1), Tooltip("Total iterations when looping.")]
        private int iterations = 1;

        [SerializeField, Tooltip("Clock used to advance this tween.")]
        private TimeMode timeMode = Evo.Tween.TimeMode.Scaled;

        [SerializeField, Tooltip("Interpolation curve used between the start and target values.")]
        private Ease ease = Ease.OutCubic;

        [SerializeField, Tooltip("Curve evaluated when Ease is set to Custom.")]
        private AnimationCurve customEase = new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        public T From { get => from; set => from = value; }
        public T To { get => to; set => to = value; }
        public float Delay { get => delay; set => delay = TweenMath.SanitizeNonNegative(value); }
        public float Duration { get => duration; set => duration = TweenMath.SanitizeNonNegative(value); }
        public LoopMode Loop { get => loop; set => loop = value; }
        public int Iterations { get => Mathf.Max(1, iterations); set => iterations = Mathf.Max(1, value); }
        public TimeMode TimeMode { get => timeMode; set => timeMode = value; }
        public Ease Ease { get => ease; set => ease = value; }
        public AnimationCurve CustomEase { get => customEase; set => customEase = value ?? LinearCurve(); }

        public TweenClipSettings() { }

        public TweenClipSettings(T from, T to)
        {
            this.from = from;
            this.to = to;
        }

        public TweenClipSettings(TweenClipSettings<T> source) => CopyFrom(source);

        public TweenClipSettings<T> Clone() => new(this);

        public TweenHandle Trigger(Action<T> setter)
        {
            Validate();
            return CreateBuilder().Bind(setter);
        }

        public TweenHandle Trigger<TTarget>(TTarget target, Action<TTarget, T> setter, int propertyKey) where TTarget : UnityEngine.Object
        {
            Validate();
            return CreateBuilder().Bind(target, setter, propertyKey);
        }

        public void CopyFrom(TweenClipSettings<T> source)
        {
            if (source == null)
            {
                from = TweenClipSettingsDefaultValue<T>.Value;
                to = TweenClipSettingsDefaultValue<T>.Value;
                delay = 0f;
                duration = 0.3f;
                loop = LoopMode.None;
                iterations = 1;
                timeMode = Evo.Tween.TimeMode.Scaled;
                ease = Ease.OutCubic;
                customEase = LinearCurve();
                return;
            }

            from = source.from;
            to = source.to;
            delay = source.delay;
            duration = source.duration;
            loop = source.loop;
            iterations = source.iterations;
            timeMode = source.timeMode;
            ease = source.ease;
            customEase = source.customEase != null ? new AnimationCurve(source.customEase.keys) : LinearCurve();
            Validate();
        }

        TweenBuilder<T> CreateBuilder()
        {
            return EvoTween.Create(from, to, duration)
                .WithDelay(delay)
                .WithEase(ease, ease == Ease.Custom ? customEase : null)
                .WithLoops(iterations, loop)
                .WithTimeMode(timeMode);
        }

        void Validate()
        {
            delay = TweenMath.SanitizeNonNegative(delay);
            duration = TweenMath.SanitizeNonNegative(duration);
            iterations = Mathf.Max(1, iterations);
            customEase ??= LinearCurve();
        }

        static AnimationCurve LinearCurve() => new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    }

    [NoAutoStaticsCleanup]
    static class TweenClipSettingsDefaultValue<T> where T : struct
    {
        public static readonly T Value = typeof(T) == typeof(Quaternion) ? (T)(object)Quaternion.identity : default;
    }
}