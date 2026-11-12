using System;
using UnityEngine;
using Evo.Tween.Modules;

namespace Evo.Tween
{
    /// <summary>
    /// Immutable-style fluent configuration for a value tween. Bind starts playback.
    /// </summary>
    public struct TweenBuilder<T> where T : struct
    {
        internal T From { get; private set; }
        internal T To { get; private set; }
        internal float Duration { get; private set; }
        internal float Delay { get; private set; }
        internal float Speed { get; private set; }
        internal int Iterations { get; private set; }
        internal Ease Ease { get; private set; }
        internal AnimationCurve CustomEase { get; private set; }
        internal LoopMode Loop { get; private set; }
        internal TimeMode TimeMode { get; private set; }
        internal UpdatePhase UpdatePhase { get; private set; }
        internal Action Completion { get; private set; }

        internal TweenBuilder(T from, T to, float duration)
        {
            From = from;
            To = to;
            Duration = TweenMath.SanitizeNonNegative(duration);
            Delay = 0f;
            Speed = 1f;
            Iterations = 1;
            Ease = Ease.Linear;
            CustomEase = null;
            Loop = LoopMode.None;
            TimeMode = TimeMode.Scaled;
            UpdatePhase = UpdatePhase.Update;
            Completion = null;
        }

        public readonly TweenBuilder<T> WithDelay(float delay)
        {
            TweenBuilder<T> copy = this;
            copy.Delay = TweenMath.SanitizeNonNegative(delay);
            return copy;
        }

        public readonly TweenBuilder<T> WithEase(Ease ease, AnimationCurve customCurve = null)
        {
            TweenBuilder<T> copy = this;
            copy.Ease = ease;
            copy.CustomEase = ease == Ease.Custom ? customCurve : null;
            return copy;
        }

        public readonly TweenBuilder<T> WithLoops(int iterations, LoopMode loop = LoopMode.Restart)
        {
            TweenBuilder<T> copy = this;
            copy.Loop = loop;
            copy.Iterations = loop == LoopMode.None ? 1 : Mathf.Max(0, iterations);
            return copy;
        }

        public readonly TweenBuilder<T> WithSpeed(float speed)
        {
            TweenBuilder<T> copy = this;
            copy.Speed = TweenMath.SanitizeNonNegative(speed);
            return copy;
        }

        public readonly TweenBuilder<T> WithTimeMode(TimeMode timeMode)
        {
            TweenBuilder<T> copy = this;
            copy.TimeMode = timeMode;
            return copy;
        }

        public readonly TweenBuilder<T> WithUpdatePhase(UpdatePhase updatePhase)
        {
            TweenBuilder<T> copy = this;
            copy.UpdatePhase = updatePhase;
            return copy;
        }

        public readonly TweenBuilder<T> OnComplete(Action callback)
        {
            TweenBuilder<T> copy = this;
            copy.Completion = callback;
            return copy;
        }

        /// <summary>
        /// Starts playback and sends each evaluated value to a callback.
        /// </summary>
        public readonly TweenHandle Bind(Action<T> setter) => EvoTween.Bind(this, setter);

        /// <summary>
        /// Starts playback against a Unity object with property conflict resolution.
        /// </summary>
        public readonly TweenHandle Bind<TTarget>(TTarget target, Action<TTarget, T> setter, int propertyKey)
            where TTarget : UnityEngine.Object => EvoTween.Bind(this, target, setter, propertyKey);

        /// <summary>
        /// Starts playback through the same module implementation used by serialized clips.
        /// </summary>
        public readonly TweenHandle Bind(Component target, TweenModule module) => EvoTween.Bind(this, target, module);

        /// <summary>
        /// Starts playback with a reusable module instance. Prefer this overload in fluent extension methods
        /// when the module needs no settings beyond the builder's start and end values.
        /// </summary>
        public readonly TweenHandle Bind<TModule>(Component target) where TModule : TweenModule, IValueModule<T>, new()
        {
            DefaultTweenModuleConfiguration<TModule> configuration = default;
            return EvoTween.Bind<T, TModule, DefaultTweenModuleConfiguration<TModule>>(this, target, configuration);
        }

        internal readonly TweenHandle Bind<TModule, TConfiguration>(Component target, in TConfiguration configuration)
            where TModule : TweenModule, IValueModule<T>, new()
            where TConfiguration : struct, ITweenModuleConfiguration<TModule>
        {
            return EvoTween.Bind<T, TModule, TConfiguration>(this, target, configuration);
        }
    }
}