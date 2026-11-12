using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Progress Button tab: three settings, some moved off their defaults. Holding the button, Regular or Alt by the Type option,
    /// glides every slider back to its default. A dot marks each changed setting.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoResetSettings : DemoButtonPage
    {
        [SerializeField] private ProgressButton[] buttons;
        [SerializeField] private Slider[] sliders;
        [SerializeField] private CanvasGroup[] changedDots;
        [SerializeField] private float defaultValue = 50f;
        [SerializeField, Min(0f)] private float glideDuration = 0.6f;

        const float DotFadeDuration = 0.2f;
        const int GlideKey = 1;

        float[] glideFrom;
        bool[] changed;
        bool gliding;
        TweenHandle glide;

        protected override void Awake()
        {
            base.Awake();

            glideFrom = new float[sliders.Length];
            changed = new bool[sliders.Length];

            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i].onValueChanged.AddListener(OnValueChanged);
                ShowChanged(i, false);
            }
        }

        // Called by both buttons' On Complete
        public void ResetValues()
        {
            for (int i = 0; i < sliders.Length; i++)
                glideFrom[i] = sliders[i].value;

            glide.Kill();
            gliding = true;
            glide = EvoTween.Create(0f, 1f, glideDuration)
                .WithEase(Ease.InOutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, t) => demo.Glide(t), GlideKey);
        }

        void Glide(float t)
        {
            for (int i = 0; i < sliders.Length; i++)
                sliders[i].value = Mathf.Lerp(glideFrom[i], defaultValue, t);

            gliding = t < 1f;
        }

        // A new change takes a button out of a kept Complete state
        void OnValueChanged(float value)
        {
            for (int i = 0; i < sliders.Length; i++)
                ShowChanged(i, true);

            if (gliding)
                return;

            foreach (ProgressButton button in buttons)
            {
                if (button.CurrentProgressState == ProgressButton.ProgressState.Complete)
                    button.ResetProgress(false);
            }
        }

        void ShowChanged(int index, bool animate)
        {
            bool value = Mathf.Abs(sliders[index].value - defaultValue) > 0.5f;
            if (animate && value == changed[index])
                return;

            changed[index] = value;
            if (!animate) { changedDots[index].alpha = value ? 1f : 0f; }
            else { DemoUtils.Fade(changedDots[index], value ? 1f : 0f, DotFadeDuration); }
        }

        // Hold
        public void SetHoldTime(float value)
        {
            foreach (ProgressButton button in buttons)
                button.holdDuration = value;
        }

        public void SetResetTime(float value)
        {
            foreach (ProgressButton button in buttons)
                button.resetDuration = value;
        }
    }
}
