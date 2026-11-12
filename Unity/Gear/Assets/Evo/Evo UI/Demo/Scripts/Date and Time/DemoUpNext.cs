using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Timer (Radial) tab: an "up next" row. The ring counts down to the next episode; at zero, or when the ring is clicked,
    /// the episode's title swaps in and the ring refills through SetCurrentTime, so the bar's smoothing glides it back up.
    /// Cancel stops the countdown and offers Play now. Without Loop the refilled ring waits on Start.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoUpNext : DemoTimer
    {
        const int SwapKey = 1;
        const float SwapOut = 0.18f;
        const float SwapIn = 0.3f;
        const float SwapDistance = 14;
        const float DimAlpha = 0.4f;
        const float FadeDuration = 0.2f;

        [SerializeField] private CanvasGroup ring;
        [SerializeField] private CanvasGroup details;
        [SerializeField] private RectTransform[] titleBars;
        [SerializeField] private Vector2[] titleWidths;

        [Header("Button")]
        [SerializeField] private Button button;
        [SerializeField] private string cancelText = "Cancel";
        [SerializeField] private Sprite cancelIcon;
        [SerializeField] private string playText = "Play now";
        [SerializeField] private string startText = "Start";
        [SerializeField] private Sprite playIcon;

        enum Mode { Counting, Cancelled, Waiting }

        Mode mode;
        bool swapping, swapNow;
        Vector2 detailsBase;
        TweenHandle swap;
        System.Action swappedOut, swappedIn;

        protected override void Awake()
        {
            base.Awake();
            detailsBase = ((RectTransform)details.transform).anchoredPosition;
            swappedOut = RiseIn;
            swappedIn = () => Swapped(swapNow);
        }

        void OnDisable()
        {
            swap.Kill();
            swapping = false;
            details.alpha = 1;
            ((RectTransform)details.transform).anchoredPosition = detailsBase;
        }

        public override void Restart()
        {
            swap.Kill();
            swapping = false;
            details.alpha = 1;
            ((RectTransform)details.transform).anchoredPosition = detailsBase;
            timer.ResetTimer();
            Count(false);
        }

        public override void SetRunning(bool value)
        {
            if (swapping)
                return;

            if (value) { Count(false); }
            else { timer.PauseTimer(); }
        }

        public override void SetLoop(bool value)
        {
            base.SetLoop(value);
            if (value && mode == Mode.Waiting)
                Count(true);
        }

        // Cancel while counting, Play now once cancelled, Start when the refilled ring waits
        public void PressButton()
        {
            if (swapping)
                return;

            if (mode == Mode.Counting)
            {
                mode = Mode.Cancelled;
                timer.StopTimer();
                ShowRunning(false);
                DemoUtils.Fade(ring, DimAlpha, FadeDuration);
                SetButton(playText, playIcon);
            }
            else if (mode == Mode.Cancelled) { Advance(true); }
            else { Count(true); }
        }

        // The ring's click plays the next episode now
        public void PlayNow() => Advance(true);

        protected override void OnTimerComplete() => Advance(false);

        void Count(bool step)
        {
            if (step)
                ShowRunning(true);

            mode = Mode.Counting;
            DemoUtils.Fade(ring, 1, FadeDuration);
            SetButton(cancelText, cancelIcon);
            Run();
        }

        void Advance(bool now)
        {
            if (swapping)
                return;

            swapping = true;
            timer.PauseTimer();
            DemoUtils.Fade(ring, 1, FadeDuration);

            // The old title leaves upward, the new one rises in. The callbacks are made once, so a swap doesn't allocate
            swapNow = now;
            swap.Kill();
            swap = EvoTween.Create(0f, 1f, SwapOut)
                .WithEase(Ease.InOutQuad)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(swappedOut)
                .Bind(this, static (u, t) => u.ApplySwap(t, 1), SwapKey);
        }

        void RiseIn()
        {
            for (int i = 0; i < titleBars.Length; i++)
                titleBars[i].sizeDelta = new Vector2(Random.Range(titleWidths[i].x, titleWidths[i].y), titleBars[i].sizeDelta.y);

            swap = EvoTween.Create(1f, 0f, SwapIn)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(swappedIn)
                .Bind(this, static (u, t) => u.ApplySwap(t, -1), SwapKey);
        }

        // t is how far the title is from its place: up on the way out, down on the way in
        void ApplySwap(float t, float side)
        {
            ((RectTransform)details.transform).anchoredPosition = detailsBase + new Vector2(0, SwapDistance * t * side);
            details.alpha = 1 - t;
        }

        void Swapped(bool now)
        {
            swapping = false;
            timer.SetCurrentTime(timer.countDown ? timer.duration : 0);

            if (now || Loop) { Count(true); }
            else
            {
                mode = Mode.Waiting;
                ShowRunning(false);
                SetButton(startText, playIcon);
            }
        }

        void SetButton(string text, Sprite icon)
        {
            button.Text = text;
            button.Icon = icon;
        }
    }
}
