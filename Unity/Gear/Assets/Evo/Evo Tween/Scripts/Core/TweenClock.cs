using System;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Stateful timeline cursor shared by code tweens and <see cref="TweenAnimator"/>.
    /// It consumes delay, applies playback speed, tracks iterations and yoyo direction,
    /// and produces normalized progress plus a completion signal for each scheduler tick.
    /// </summary>
    internal struct TweenClock
    {
        const float MinimumDuration = 0.000001f;

        float delayRemaining;
        float duration;
        float position;
        float speed;
        int iterations;
        int completedIterations;
        LoopMode loop;
        bool forward;
        bool completed;

        public readonly bool IsForward => forward;
        public readonly float DelayRemaining => delayRemaining;
        public readonly float Duration => duration;
        public readonly float Position => position;
        public readonly float Speed => speed;
        public readonly int Iterations => iterations;
        public readonly int CompletedIterations => completedIterations;
        public readonly LoopMode Loop => loop;

        /// <summary>
        /// Normalized progress at which finite playback ends: the start after an even number of Yoyo iterations, otherwise the end.
        /// </summary>
        public readonly float FinalNormalizedTime => loop == LoopMode.Yoyo && iterations > 0 && (iterations & 1) == 0 ? 0f : 1f;

        /// <summary>
        /// Initializes the clock at the beginning of a new playback run.
        /// </summary>
        public void Reset(float delay, float tweenDuration, float playbackSpeed, int tweenIterations, LoopMode loopMode)
        {
            delayRemaining = TweenMath.SanitizeNonNegative(delay);
            duration = TweenMath.SanitizeNonNegative(tweenDuration);
            position = 0f;
            speed = TweenMath.SanitizeNonNegative(playbackSpeed);
            iterations = Mathf.Max(0, tweenIterations);
            completedIterations = 0;
            loop = loopMode;
            forward = true;
            completed = false;
        }

        /// <summary>
        /// Starts a new playback run while keeping the configured delay, speed, and loop settings.
        /// </summary>
        public void Restart(float tweenDuration)
        {
            duration = TweenMath.SanitizeNonNegative(tweenDuration);
            position = 0f;
            completedIterations = 0;
            forward = true;
            completed = false;
        }

        /// <summary>
        /// Replaces the delay that remains before playback begins.
        /// </summary>
        public void SetDelay(float value) => delayRemaining = TweenMath.SanitizeNonNegative(value);

        /// <summary>
        /// Replaces the playback speed multiplier.
        /// </summary>
        public void SetSpeed(float value) => speed = TweenMath.SanitizeNonNegative(value);

        /// <summary>
        /// Replaces the iteration count and endpoint behavior.
        /// </summary>
        public void SetLoops(int value, LoopMode mode)
        {
            iterations = Mathf.Max(0, value);
            loop = mode;
        }

        /// <summary>
        /// Moves to normalized progress on the first forward iteration.
        /// </summary>
        public void Seek(float normalizedTime) => Seek(normalizedTime, true, 0);

        /// <summary>
        /// Moves to normalized progress while restoring loop direction and completed-iteration state.
        /// </summary>
        public void Seek(float normalizedTime, bool forwardDirection, int completedIterationCount)
        {
            delayRemaining = 0f;
            position = duration * Mathf.Clamp01(normalizedTime);
            forward = forwardDirection;
            completedIterations = Mathf.Max(0, completedIterationCount);
            completed = false;
        }

        /// <summary>
        /// Advances the timeline and returns true when all finite iterations have completed.
        /// </summary>
        public bool Tick(float deltaTime, out float normalizedTime)
        {
            double remaining = (double)TweenMath.SanitizeNonNegative(deltaTime) * speed;

            if (delayRemaining > 0f)
            {
                if (remaining <= delayRemaining)
                {
                    delayRemaining -= (float)remaining;
                    normalizedTime = 0f;
                    return false;
                }

                remaining -= delayRemaining;
                delayRemaining = 0f;
            }

            if (completed)
            {
                normalizedTime = duration > MinimumDuration ? Mathf.Clamp01(position / duration) : 1f;
                return true;
            }

            if (duration <= MinimumDuration)
            {
                position = duration;
                forward = true;
                completed = true;
                normalizedTime = 1f;
                return true;
            }

            if (remaining <= 0d)
            {
                normalizedTime = Mathf.Clamp01(position / duration);
                return false;
            }

            float distance = forward ? duration - position : position;
            if (remaining < distance)
            {
                position += forward ? (float)remaining : -(float)remaining;
                normalizedTime = Mathf.Clamp01(position / duration);
                return false;
            }

            position = forward ? duration : 0f;
            remaining -= distance;
            IncrementCompletedIterations(1d);

            bool finite = loop == LoopMode.None || iterations > 0;
            int targetIterations = loop == LoopMode.None ? 1 : iterations;
            if (finite && completedIterations >= targetIterations)
            {
                completed = true;
                normalizedTime = forward ? 1f : 0f;
                return true;
            }

            if (loop == LoopMode.Yoyo) { forward = !forward; }
            else
            {
                forward = true;
                position = 0f;
            }

            if (finite)
            {
                int iterationsUntilCompletion = targetIterations - completedIterations;
                double timeUntilCompletion = (double)iterationsUntilCompletion * duration;
                if (remaining >= timeUntilCompletion)
                {
                    bool finalDirection = loop != LoopMode.Yoyo || (iterationsUntilCompletion & 1) != 0
                        ? forward
                        : !forward;

                    completedIterations = targetIterations;
                    forward = finalDirection;
                    position = forward ? duration : 0f;
                    completed = true;
                    normalizedTime = forward ? 1f : 0f;
                    return true;
                }

                int fullIterations = (int)(remaining / duration);
                remaining -= (double)fullIterations * duration;
                completedIterations += fullIterations;

                if (loop == LoopMode.Yoyo && (fullIterations & 1) != 0)
                    forward = !forward;
            }
            else
            {
                double fullIterations = Math.Floor(remaining / duration);
                IncrementCompletedIterations(fullIterations);

                if (loop == LoopMode.Yoyo)
                {
                    double cyclePosition = remaining % ((double)duration * 2d);
                    bool oddIterations = cyclePosition >= duration;
                    remaining = oddIterations ? cyclePosition - duration : cyclePosition;

                    if (oddIterations)
                        forward = !forward;
                }
                else
                {
                    remaining %= duration;
                }
            }

            position = loop == LoopMode.Yoyo && !forward ? duration : 0f;
            position += forward ? (float)remaining : -(float)remaining;
            normalizedTime = Mathf.Clamp01(position / duration);
            return false;
        }

        void IncrementCompletedIterations(double value)
        {
            if (value >= int.MaxValue - completedIterations)
            {
                completedIterations = int.MaxValue;
                return;
            }

            completedIterations += (int)value;
        }
    }
}