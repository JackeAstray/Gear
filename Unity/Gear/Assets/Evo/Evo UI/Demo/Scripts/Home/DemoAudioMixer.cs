using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample audio mixer screen. Presets ease the Bass, Voice and Air boosts, the boosts shape the EQ curve,
    /// and the fader meters dance to fake program material. Knob fills, values and keycaps are their components' own.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoAudioMixer : DemoScreenContent
    {
        [Header("Enhance")]
        [SerializeField] private RadioButtonGroup presets;
        [Tooltip("Bass, Voice and Air boosts of each preset, in the group's order.")]
        [SerializeField] private Vector3[] presetBoosts = { new(0, 0, 0), new(9, 1, 2), new(1, 8, 3), new(2, 2, 9) };
        [SerializeField, Min(0f)] private float presetDuration = 0.45f;
        [Tooltip("Bass, Voice and Air knobs.")]
        [SerializeField] private RadialSlider[] boosts;
        [Tooltip("Five points: Bass, between, Voice, between, Air.")]
        [SerializeField] private LineChart curve;

        [Header("Mix")]
        [SerializeField] private UnityEngine.UI.Slider[] faders;
        [Tooltip("Lit segment column of each fader's meter, clipped from the bottom.")]
        [SerializeField] private ProceduralShape[] meters;
        [SerializeField, Min(1)] private int meterSegments = 24;
        [SerializeField] private Button testTone;
        [SerializeField, Min(0f)] private float toneDuration = 1.4f;

        const int PresetProperty = 32101;

        float[] level;
        int[] shown;
        Vector3 presetFrom, presetTo;
        TweenHandle presetTween;
        float toneLeft;

        void Awake()
        {
            for (int i = 0; i < boosts.Length; i++)
                boosts[i].onValueChanged.AddListener(OnBoostChanged);

            level = new float[faders.Length];
            shown = new int[faders.Length];
            for (int i = 0; i < shown.Length; i++)
                shown[i] = -1;

            presets.onSelectionChanged.AddListener(ApplyPreset);
            testTone.onClick.AddListener(PlayTone);
            UpdateCurve();
        }

        void OnDisable()
        {
            toneLeft = 0;

            // A deck cut lands the preset at once
            if (presetTween.IsActive)
            {
                presetTween.Kill();
                SetBoosts(1);
            }
        }

        // The meters follow fake program material, fast attack and slow release; the test tone holds them just under their faders
        protected override void Tick(float delta)
        {
            float time = Elapsed;
            toneLeft = Mathf.Max(0, toneLeft - delta);

            for (int i = 0; i < faders.Length; i++)
            {
                float value = faders[i].normalizedValue;
                float phase = i * 1.7f;
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 2.1f + phase)), 3);
                float wobble = 0.55f + 0.25f * Mathf.Sin(time * 7.3f + phase * 2) * Mathf.Sin(time * 3.1f + i);
                float target = toneLeft > 0 ? value * 0.94f : value * Mathf.Min(1, wobble + 0.35f * beat);
                level[i] += (target - level[i]) * Mathf.Min(delta * (target > level[i] ? 30 : 6), 1);

                int segments = Mathf.RoundToInt(Mathf.Clamp01(level[i]) * meterSegments);
                if (segments == shown[i])
                    continue;

                shown[i] = segments;
                meters[i].ClipAmount = (float)segments / meterSegments;
            }
        }

        void PlayTone() => toneLeft = toneDuration;

        void ApplyPreset(int index)
        {
            presetFrom = new Vector3(boosts[0].Value, boosts[1].Value, boosts[2].Value);
            presetTo = presetBoosts[index];
            presetTween.Kill();

            if (presetDuration <= 0)
            {
                SetBoosts(1);
                return;
            }

            presetTween = EvoTween.Create(0f, 1f, presetDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (mixer, t) => mixer.SetBoosts(t), PresetProperty);
        }

        void SetBoosts(float t)
        {
            Vector3 value = Vector3.LerpUnclamped(presetFrom, presetTo, t);
            for (int i = 0; i < boosts.Length; i++)
                boosts[i].Value = value[i];
        }

        void OnBoostChanged(float value) => UpdateCurve();

        void UpdateCurve()
        {
            float bass = boosts[0].Value, voice = boosts[1].Value, air = boosts[2].Value;
            curve.dataPoints[0].value = bass;
            curve.dataPoints[1].value = bass * 0.45f + voice * 0.3f;
            curve.dataPoints[2].value = voice;
            curve.dataPoints[3].value = voice * 0.3f + air * 0.45f;
            curve.dataPoints[4].value = air;
            curve.MarkDirty();
        }
    }
}
