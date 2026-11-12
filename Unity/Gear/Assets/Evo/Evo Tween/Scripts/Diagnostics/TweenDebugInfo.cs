using UnityEngine;

namespace Evo.Tween.Diagnostics
{
    /// <summary>
    /// Read-only information about one tween currently owned by the scheduler.
    /// </summary>
    public readonly struct TweenDebugInfo
    {
        public readonly bool IsValid;
        public readonly string Name;
        public readonly Object Target;
        public readonly bool IsPaused;
        public readonly UpdatePhase UpdatePhase;
        public readonly TimeMode TimeMode;
        public readonly float Position;
        public readonly float Duration;
        public readonly float DelayRemaining;
        public readonly float Speed;
        public readonly int CompletedIterations;
        public readonly int Iterations;
        public readonly LoopMode Loop;
        public readonly long PropertyKey;

        /// <summary>
        /// Current progress normalized from zero to one.
        /// </summary>
        public float NormalizedTime => Duration > 0f ? Mathf.Clamp01(Position / Duration) : 0f;

        internal TweenDebugInfo(string name, Object target, bool isPaused, UpdatePhase updatePhase, TimeMode timeMode, float position,
            float duration, float delayRemaining, float speed, int completedIterations, int iterations, LoopMode loop, long propertyKey)
        {
            IsValid = true;
            Name = name;
            Target = target;
            IsPaused = isPaused;
            UpdatePhase = updatePhase;
            TimeMode = timeMode;
            Position = position;
            Duration = duration;
            DelayRemaining = delayRemaining;
            Speed = speed;
            CompletedIterations = completedIterations;
            Iterations = iterations;
            Loop = loop;
            PropertyKey = propertyKey;
        }
    }
}