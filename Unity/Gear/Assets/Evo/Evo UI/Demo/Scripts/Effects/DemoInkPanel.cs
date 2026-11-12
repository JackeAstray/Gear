using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Ink Panel tab: the reveal plays in, holds, plays out and rests, on a loop; a click on the stage plays it again.
    /// Mask art switches the Soft Mask: on, the ink clips the art; off, the ink shows in its own color.
    /// </summary>
    [AddComponentMenu(""), NoAutoStaticsCleanup]
    public class DemoInkPanel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private InkPanel ink;
        [SerializeField] private SoftMask mask;

        [Header("Timing")]
        [SerializeField] private float revealDuration = 1.1f;
        [SerializeField] private float holdDuration = 2.4f;
        [SerializeField] private float hideDuration = 0.8f;
        [SerializeField] private float restDuration = 0.7f;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup origin;
        [SerializeField] private Switch loop;
        [SerializeField] private Switch maskArt;
        [SerializeField] private Slider roundness;
        [SerializeField] private Slider roughness;
        [SerializeField] private Slider droplets;
        [SerializeField] private Slider speed;

        static readonly Vector2[] Origins = { new(0.5f, 0.5f), new(0.05f, 0.5f), new(0.5f, 0.05f), new(0.05f, 0.05f) };
        const int RevealKey = 1;
        static readonly Action<InkPanel, float> SetReveal = (target, value) => target.Reveal = value;

        bool looping = true;
        TweenHandle reveal;
        Action onRevealed, onHidden;

        void Awake()
        {
            onRevealed = Hide;
            onHidden = () => Reveal(restDuration);
            DemoUtils.Show(origin, Array.IndexOf(Origins, ink.Origin));
            DemoUtils.Show(loop, looping);
            DemoUtils.Show(maskArt, mask.enabled);
            DemoUtils.Show(roundness, ink.Roundness * 100f);
            DemoUtils.Show(roughness, ink.Roughness * 100f);
            DemoUtils.Show(droplets, ink.Droplets * 100f);
            DemoUtils.Show(speed, ink.Speed);
        }

        void OnEnable() => Play();

        void OnDisable()
        {
            reveal.Kill();
            ink.Reveal = 0f;
        }

        public void OnPointerClick(PointerEventData eventData) => Play();

        void Play()
        {
            reveal.Kill();
            ink.Reveal = 0f;
            Reveal(0f);
        }

        void Reveal(float delay)
        {
            reveal = EvoTween.Create(0f, 1f, revealDuration)
                .WithDelay(delay)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(onRevealed)
                .Bind(ink, SetReveal, RevealKey);
        }

        void Hide()
        {
            if (!looping)
                return;

            reveal = EvoTween.Create(1f, 0f, hideDuration)
                .WithDelay(holdDuration)
                .WithEase(Ease.InCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(onHidden)
                .Bind(ink, SetReveal, RevealKey);
        }

        public void SetOrigin(int index) => ink.Origin = Origins[index];

        public void SetLoop(bool value)
        {
            looping = value;
            // Turning it back on picks up from a reveal that already finished
            if (value && ink.Reveal >= 1f)
                Hide();
        }

        // Roundness, roughness and droplets are percent bars
        public void SetRoundness(float value) => ink.Roundness = value / 100f;
        public void SetRoughness(float value) => ink.Roughness = value / 100f;
        public void SetDroplets(float value) => ink.Droplets = value / 100f;
    }
}