using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Knob tab: the amp's level meter. Gain and Level set how high it bounces, Tone how fast.
    /// The fill is one bar over its track, never thinner than it is tall.
    /// The bounce follows the knobs on every frame, so a looping Evo Tween is its clock.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoLevelMeter : MonoBehaviour
    {
        [SerializeField] private RadialSlider gain;
        [SerializeField] private RadialSlider tone;
        [SerializeField] private RadialSlider level;
        [SerializeField] private RectTransform fill;

        [Header("Motion")]
        [SerializeField] private float minSpeed = 0.6f;
        [SerializeField] private float maxSpeed = 2.2f;
        [SerializeField] private float release = 6f;

        const int ClockKey = 1;
        const float ClockPeriod = 60f;

        float time, current, clock;
        TweenHandle ticker;

        void OnEnable()
        {
            current = 0f;
            clock = 0f;
            Apply();

            // Seconds within the minute; each tick takes the time since the last one
            ticker = EvoTween.Create(0f, ClockPeriod, ClockPeriod)
                .WithLoops(0)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (meter, seconds) => meter.Tick(seconds), ClockKey);
        }

        void OnDisable() => ticker.Kill();

        // Rises at once to each peak and falls back smoothly, like a VU meter
        void Tick(float seconds)
        {
            float dt = Mathf.Repeat(seconds - clock, ClockPeriod);
            clock = seconds;
            time += dt * Mathf.Lerp(minSpeed, maxSpeed, tone.NormalizedValue);
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 5.3f) * Mathf.Sin(time * 1.7f + 1f));
            float target = gain.NormalizedValue * level.NormalizedValue * pulse;
            current = target > current ? target : Mathf.Lerp(current, target, Mathf.Min(1f, dt * release));
            Apply();
        }

        void Apply()
        {
            float height = fill.rect.height;
            float width = ((RectTransform)fill.parent).rect.width;
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Lerp(height, width, current));
        }
    }
}
