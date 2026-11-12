using System;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Lightweight, versioned control value for a running tween.
    /// </summary>
    public readonly struct TweenHandle
    {
        readonly ITweenControl tween;
        readonly int version;

        internal TweenHandle(ITweenControl tween, int version)
        {
            this.tween = tween;
            this.version = version;
        }

        public bool IsActive => IsValid && tween.IsActive;
        public bool IsPaused => IsValid && tween.IsPaused;
        bool IsValid => tween != null && tween.Version == version;

        /// <summary>
        /// Sets the delay before interpolation begins.
        /// </summary>
        public TweenHandle WithDelay(float delay)
        {
            if (IsValid)
                tween.SetDelay(delay);

            return this;
        }

        /// <summary>
        /// Sets the easing function and optional custom curve.
        /// </summary>
        public TweenHandle WithEase(Ease ease, AnimationCurve customCurve = null)
        {
            if (IsValid)
                tween.SetEase(ease, customCurve);

            return this;
        }

        /// <summary>
        /// Set iterations to zero for an infinite loop. Yoyo counts each direction as one iteration.
        /// </summary>
        public TweenHandle WithLoops(int iterations, LoopMode loop = LoopMode.Restart)
        {
            if (IsValid)
                tween.SetLoops(iterations, loop);

            return this;
        }

        /// <summary>
        /// Sets the playback speed multiplier.
        /// </summary>
        public TweenHandle WithSpeed(float speed)
        {
            if (IsValid)
                tween.SetSpeed(speed);

            return this;
        }

        /// <summary>
        /// Sets whether playback uses scaled or unscaled time.
        /// </summary>
        public TweenHandle WithTimeMode(TimeMode timeMode)
        {
            if (IsValid)
                tween.SetTimeMode(timeMode);

            return this;
        }

        /// <summary>
        /// Sets the Unity player-loop phase used to advance the tween.
        /// </summary>
        public TweenHandle WithUpdatePhase(UpdatePhase updatePhase)
        {
            if (IsValid)
                tween.SetUpdatePhase(updatePhase);

            return this;
        }

        /// <summary>
        /// Registers a callback invoked after successful completion.
        /// </summary>
        public TweenHandle OnComplete(Action callback)
        {
            if (IsValid)
                tween.SetOnComplete(callback);

            return this;
        }

        /// <summary>
        /// Pauses this tween.
        /// </summary>
        public void Pause()
        {
            if (IsValid)
                tween.Pause();
        }

        /// <summary>
        /// Resumes this tween.
        /// </summary>
        public void Resume()
        {
            if (IsValid)
                tween.Resume();
        }

        /// <summary>
        /// Applies the end value and completes this tween immediately.
        /// </summary>
        public void Complete()
        {
            if (IsValid)
                tween.Complete();
        }

        /// <summary>
        /// Cancels this tween without invoking its completion callback.
        /// </summary>
        public void Kill()
        {
            if (IsValid)
                tween.Kill();
        }
    }
}