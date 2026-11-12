using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Shared numeric guards used by tween settings and runtime clocks.
    /// </summary>
    internal static class TweenMath
    {
        public static float SanitizeNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
        }

        public static float GetIterationsDuration(float duration, LoopMode loop, int iterations)
        {
            int effectiveIterations = loop == LoopMode.None ? 1 : Mathf.Max(1, iterations);
            double total = (double)SanitizeNonNegative(duration) * effectiveIterations;
            return total >= float.MaxValue ? float.MaxValue : (float)total;
        }

        public static float AddDurations(float first, float second)
        {
            double total = (double)SanitizeNonNegative(first) + SanitizeNonNegative(second);
            return total >= float.MaxValue ? float.MaxValue : (float)total;
        }
    }
}