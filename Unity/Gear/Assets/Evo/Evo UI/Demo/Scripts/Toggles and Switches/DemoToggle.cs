using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Toggle tab: a checklist. Checked rows fade their lines and the bar above fills with the share of checked rows.
    /// Every option applies to all toggles; the effects are locked while the toggles aren't interactable.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoToggle : MonoBehaviour
    {
        [SerializeField] private Toggle[] targets;
        [Tooltip("Each toggle's lines, in the same order; they fade while it's checked.")]
        [SerializeField] private CanvasGroup[] lines;
        [Tooltip("The progress bar's fill; its right anchor follows the share of checked rows.")]
        [SerializeField] private RectTransform progress;

        [Header("Locks")]
        [SerializeField] private CanvasGroup[] interactionOptions;

        const float CheckedAlpha = 0.4f;
        const float FadeDuration = 0.2f;
        const float ProgressDuration = 0.35f;
        const int ProgressKey = 1;

        TweenHandle progressTween;
        float shownProgress;

        void Awake()
        {
            foreach (Toggle target in targets)
                target.onValueChanged.AddListener(OnValueChanged);

            Refresh(false);
            DemoUtils.SetLocked(interactionOptions, targets.Length > 0 && !targets[0].IsInteractable(), false);
        }

        void OnValueChanged(bool value) => Refresh(true);

        void Refresh(bool animate)
        {
            int checkedCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                bool isOn = targets[i].IsOn;
                if (isOn)
                    checkedCount++;

                if (i < lines.Length)
                    Fade(lines[i], isOn ? CheckedAlpha : 1f, animate);
            }

            float share = targets.Length == 0 ? 0f : (float)checkedCount / targets.Length;
            progressTween.Kill();

            if (!animate || !Application.isPlaying) { SetProgress(share); }
            else
            {
                progressTween = EvoTween.Create(shownProgress, share, ProgressDuration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (demo, value) => demo.SetProgress(value), ProgressKey);
            }
        }

        void SetProgress(float value)
        {
            shownProgress = value;
            progress.anchorMax = new Vector2(value, progress.anchorMax.y);
        }

        static void Fade(CanvasGroup group, float alpha, bool animate)
        {
            if (!animate || !Application.isPlaying) { group.alpha = alpha; }
            else { DemoUtils.Fade(group, alpha, FadeDuration); }
        }

        // Check
        public void SetDuration(float value)
        {
            foreach (Toggle target in targets)
                target.checkDuration = value;
        }

        // Effects
        public void SetRipple(bool value)
        {
            foreach (Toggle target in targets)
                DemoUtils.SetEffect(target.ripple, value);
        }

        public void SetBorderGlow(bool value)
        {
            foreach (Toggle target in targets)
                DemoUtils.SetEffect(target.borderGlow, value);
        }

        // Behavior
        public void SetSounds(bool value)
        {
            foreach (Toggle target in targets)
                target.soundEffects = value;
        }

        public void SetInteractable(bool value)
        {
            foreach (Toggle target in targets)
                target.SetInteractable(value);

            DemoUtils.SetLocked(interactionOptions, !value);
        }
    }
}
