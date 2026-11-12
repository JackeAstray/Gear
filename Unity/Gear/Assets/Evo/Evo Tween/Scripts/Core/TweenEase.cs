using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Evaluates EvoTween easing functions without creating temporary objects.
    /// </summary>
    public static class TweenEase
    {
        const float BackConstant = 1.70158f;
        const float ElasticConstant = (2f * Mathf.PI) / 3f;
        const float ElasticInOutConstant = (2f * Mathf.PI) / 4.5f;

        /// <summary>
        /// Evaluates an easing curve at a normalized time.
        /// </summary>
        public static float Evaluate(Ease ease, float time, AnimationCurve customCurve = null)
        {
            float t = Mathf.Clamp01(time);

            switch (ease)
            {
                case Ease.InSine:
                    return 1f - Mathf.Cos((t * Mathf.PI) * 0.5f);
                case Ease.OutSine:
                    return Mathf.Sin((t * Mathf.PI) * 0.5f);
                case Ease.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

                case Ease.InQuad:
                    return t * t;
                case Ease.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Square(-2f * t + 2f) * 0.5f;

                case Ease.InCubic:
                    return t * t * t;
                case Ease.OutCubic:
                    return 1f - Cube(1f - t);
                case Ease.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Cube(-2f * t + 2f) * 0.5f;

                case Ease.InQuart:
                    return t * t * t * t;
                case Ease.OutQuart:
                    return 1f - Quart(1f - t);
                case Ease.InOutQuart:
                    return t < 0.5f ? 8f * t * t * t * t : 1f - Quart(-2f * t + 2f) * 0.5f;

                case Ease.InQuint:
                    return t * t * t * t * t;
                case Ease.OutQuint:
                    return 1f - Quint(1f - t);
                case Ease.InOutQuint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Quint(-2f * t + 2f) * 0.5f;

                case Ease.InExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case Ease.OutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case Ease.InOutExpo:
                    if (t <= 0f) { return 0f; }
                    if (t >= 1f) { return 1f; }
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;

                case Ease.InCirc:
                    return 1f - Mathf.Sqrt(1f - t * t);
                case Ease.OutCirc:
                    return Mathf.Sqrt(1f - (t - 1f) * (t - 1f));
                case Ease.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Square(2f * t))) * 0.5f
                        : (Mathf.Sqrt(1f - Square(-2f * t + 2f)) + 1f) * 0.5f;

                case Ease.InBack:
                    {
                        float c3 = BackConstant + 1f;
                        return c3 * t * t * t - BackConstant * t * t;
                    }

                case Ease.OutBack:
                    {
                        float c3 = BackConstant + 1f;
                        return 1f + c3 * Cube(t - 1f) + BackConstant * Square(t - 1f);
                    }

                case Ease.InOutBack:
                    {
                        float c2 = BackConstant * 1.525f;
                        return t < 0.5f
                            ? Square(2f * t) * ((c2 + 1f) * 2f * t - c2) * 0.5f
                            : (Square(2f * t - 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) * 0.5f;
                    }

                case Ease.InElastic:
                    if (t <= 0f || t >= 1f) { return t; }
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * ElasticConstant);

                case Ease.OutElastic:
                    if (t <= 0f || t >= 1f) { return t; }
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * ElasticConstant) + 1f;

                case Ease.InOutElastic:
                    if (t <= 0f || t >= 1f) { return t; }
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * ElasticInOutConstant)) * 0.5f
                        : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * ElasticInOutConstant) * 0.5f + 1f;

                case Ease.InBounce:
                    return 1f - OutBounce(1f - t);
                case Ease.OutBounce:
                    return OutBounce(t);
                case Ease.InOutBounce:
                    return t < 0.5f ? (1f - OutBounce(1f - 2f * t)) * 0.5f : (1f + OutBounce(2f * t - 1f)) * 0.5f;

                case Ease.Custom:
                    return customCurve != null ? customCurve.Evaluate(t) : t;
                default:
                    return t;
            }
        }

        static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;

            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        static float Square(float value) => value * value;
        static float Cube(float value) => value * value * value;
        static float Quart(float value) => value * value * value * value;
        static float Quint(float value) => value * value * value * value * value;
    }

    /// <summary>
    /// Built-in interpolation curves available to tween clips and code-driven tweens.
    /// </summary>
    public enum Ease
    {
        Custom = 0,
        Linear = 1,
        InSine = 2,
        OutSine = 3,
        InOutSine = 4,
        InQuad = 5,
        OutQuad = 6,
        InOutQuad = 7,
        InCubic = 8,
        OutCubic = 9,
        InOutCubic = 10,
        InQuart = 11,
        OutQuart = 12,
        InOutQuart = 13,
        InQuint = 14,
        OutQuint = 15,
        InOutQuint = 16,
        InExpo = 17,
        OutExpo = 18,
        InOutExpo = 19,
        InCirc = 20,
        OutCirc = 21,
        InOutCirc = 22,
        InBack = 23,
        OutBack = 24,
        InOutBack = 25,
        InElastic = 26,
        OutElastic = 27,
        InOutElastic = 28,
        InBounce = 29,
        OutBounce = 30,
        InOutBounce = 31
    }
}