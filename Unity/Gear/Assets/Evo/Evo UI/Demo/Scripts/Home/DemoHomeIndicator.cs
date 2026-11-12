using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The deck's indicator: the current screen's name over a row of dots. The current dot stretches and fills as the
    /// auto-advance timer. Both change only when the deck swaps cards: the name slides sideways the way the card went, and
    /// the width moves to the new dot. Hovering a dot previews its name, clicking it steps the deck there.
    /// Each change runs as an Evo Tween; per frame only the timer fill moves. The Dots layout group places the dots.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHomeIndicator : MonoBehaviour
    {
        [Serializable]
        public class Dot
        {
            public Interactive button;
            public ProceduralShape fill;
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField, Min(0f)] private float revealDuration = 0.5f;

        [Header("Title")]
        [Tooltip("Two lines that take turns: one leaves while the other comes in.")]
        [SerializeField] private TMP_Text[] lines = new TMP_Text[2];
        [SerializeField] private CanvasGroup[] lineGroups = new CanvasGroup[2];
        [Tooltip("Dims the title while it previews a hovered dot's name.")]
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField, Range(0f, 1f)] private float previewOpacity = 0.6f;
        [SerializeField, Min(0f)] private float slideDistance = 40;

        [Header("Dots")]
        [SerializeField] private Dot[] dots;
        [SerializeField, Min(0f)] private float dotWidth = 10;
        [SerializeField, Min(0f)] private float currentWidth = 40;
        [SerializeField, Min(0f)] private float previewWidth = 18;

        const float OutDuration = 0.2f;
        const float InDuration = 0.4f;
        const float InDelay = 0.05f;
        const float PreviewDuration = 0.08f;
        const float ResizeDuration = 0.3f;
        const float TargetHold = 1.5f;
        const int LineProperty = 31501;
        const int ResizeProperty = 31502;

        DemoHome home;
        string[] names;
        float[] fromWidths, toWidths;
        int count, front, shown = -1, hovered = -1, target = -1, current;
        float targetTime, fill, titleOpacity = 1;
        TweenHandle reveal, resize, preview;

        // The name to show: a hovered dot's, else a clicked dot's while the deck steps there, else the current one
        int Wanted => hovered >= 0 ? hovered : target >= 0 ? target : current;

        public void Setup(DemoHome owner, List<DemoHomeCard> cards)
        {
            home = owner;
            count = Mathf.Min(cards.Count, dots.Length);
            names = new string[count];
            fromWidths = new float[count];
            toWidths = new float[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = cards[i].name;
                int index = i;
                dots[i].button.onClick.AddListener(() => Jump(index));
                dots[i].button.OnStateChanged += state => OnDotState(index, state);
            }

            for (int i = count; i < dots.Length; i++)
                dots[i].button.gameObject.SetActive(false);

            current = owner.Current;
            Show(current, 0);
            Resize(false);
        }

        public void Hide()
        {
            reveal.Kill();
            group.alpha = 0;
        }

        public void Reveal(float delay) => reveal = DemoUtils.Fade(group, 1, revealDuration, delay);

        void OnDisable()
        {
            // A dot hovered as the page closed rests again
            hovered = -1;
            target = -1;
            if (home == null)
                return;

            home.IndicatorHovered = false;
            Resize(false);
            preview.Kill();
            titleGroup.alpha = titleOpacity = 1;
        }

        /// <summary>
        /// Per frame from the deck: the auto-advance timer (0-1) fills the current dot.
        /// </summary>
        public void Tick(float delta, float timer)
        {
            if (names == null)
                return;

            if (target >= 0)
            {
                targetTime -= delta;
                if (targetTime <= 0)
                {
                    target = -1;
                    ShowWanted();
                }
            }

            if (fill != timer && current < count)
            {
                fill = timer;
                SetFill(current, timer);
            }
        }

        /// <summary>
        /// The deck swapped cards; side is the way the card moved (1 right). The name travels with it.
        /// </summary>
        public void Changed(int side)
        {
            if (current < count)
                dots[current].fill.ClipAmount = 0;

            current = home.Current;
            fill = -1;

            if (target == current)
                target = -1;

            if (hovered < 0)
                Show(Wanted, -side);

            Resize(true);
            UpdatePreview();
        }

        #region Title
        void Show(int index, int direction)
        {
            if (index == shown)
                return;

            if (shown < 0)
            {
                // First name: no slide
                front = 0;
                lines[0].text = names[index];
                SetLine(0, 0, 1);
                SetLine(1, 0, 0);
                shown = index;
                return;
            }

            // Direction 1 moves the names left (the new one comes in from the right)
            int leaving = front, coming = 1 - front;
            front = coming;
            lines[coming].text = names[index];
            Slide(leaving, -direction * slideDistance, 0, OutDuration, 0, Ease.OutCubic);
            SetLine(coming, direction * slideDistance, 0);
            Slide(coming, 0, 1, InDuration, InDelay, Ease.OutQuint);
            shown = index;
        }

        void ShowWanted() => Show(Wanted, Wanted > shown ? 1 : -1);

        void SetLine(int line, float x, float alpha)
        {
            var rect = (RectTransform)lines[line].transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            lineGroups[line].alpha = alpha;
        }

        // Moves a line from where it is now; a new slide replaces the line's running one
        void Slide(int line, float x, float alpha, float duration, float delay, Ease ease)
        {
            var rect = (RectTransform)lines[line].transform;
            EvoTween.Create(rect.anchoredPosition.x, x, duration)
                .WithDelay(delay)
                .WithEase(ease)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(rect, static (target, value) => target.anchoredPosition = new Vector2(value, target.anchoredPosition.y), LineProperty);

            EvoTween.Create(lineGroups[line].alpha, alpha, duration)
                .WithDelay(delay)
                .WithEase(ease)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(lineGroups[line]);
        }

        // Dims the title while it shows a hovered dot's name
        void UpdatePreview()
        {
            float opacity = hovered >= 0 && hovered != current ? previewOpacity : 1;
            if (opacity == titleOpacity)
                return;

            titleOpacity = opacity;
            preview = EvoTween.Create(titleGroup.alpha, opacity, PreviewDuration)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(titleGroup);
        }
        #endregion

        #region Dots
        void OnDotState(int index, InteractionState state)
        {
            bool over = state == InteractionState.Highlighted || state == InteractionState.Pressed;
            if (over)
                hovered = index;
            else if (hovered == index)
                hovered = -1;
            else
                return;

            home.IndicatorHovered = hovered >= 0;
            ShowWanted();
            Resize(true);
            UpdatePreview();
        }

        void Jump(int index)
        {
            hovered = -1;
            home.IndicatorHovered = false;

            if (index != current)
            {
                target = index;
                targetTime = TargetHold;
                home.Go(index);
            }

            Show(index, index > shown ? 1 : -1);
            Resize(true);
            UpdatePreview();
        }

        /// <summary>
        /// Moves every dot to its width: the current one stretched, a hovered one a little wider.
        /// </summary>
        void Resize(bool animate)
        {
            bool changed = false;

            for (int i = 0; i < count; i++)
                changed |= toWidths[i] != Width(i);

            if (!changed)
                return;

            for (int i = 0; i < count; i++)
            {
                fromWidths[i] = DotRect(i).sizeDelta.x;
                toWidths[i] = Width(i);
            }

            if (!animate)
            {
                resize.Kill();
                SetWidths(1);
                return;
            }

            resize = EvoTween.Create(0f, 1f, ResizeDuration)
                .WithEase(Ease.OutQuint)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (indicator, t) => indicator.SetWidths(t), ResizeProperty);
        }

        float Width(int index) => index == current ? currentWidth : index == hovered ? Mathf.Max(dotWidth, previewWidth) : dotWidth;

        void SetWidths(float t)
        {
            for (int i = 0; i < count; i++)
                SetWidth(i, Mathf.LerpUnclamped(fromWidths[i], toWidths[i], t));
        }

        // The Dots layout group places the dots by their widths
        void SetWidth(int index, float width)
        {
            RectTransform rect = DotRect(index);
            if (rect.sizeDelta.x != width)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        RectTransform DotRect(int index) => (RectTransform)dots[index].button.transform;

        // The timer fills the dot from its left end. While the fill is narrower than the track is tall, it shrinks to a
        // circle of that width, since the clip alone would leave a thin sliver there
        void SetFill(int index, float amount)
        {
            ProceduralShape shape = dots[index].fill;
            RectTransform rect = shape.rectTransform;
            Vector2 track = ((RectTransform)rect.parent).rect.size;
            float inset = Mathf.Min(amount * track.x - track.y, 0);

            if (rect.sizeDelta.y != inset)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, inset);

            shape.ClipAmount = amount;
        }
        #endregion

#if UNITY_EDITOR
        // Edit mode: the indicator as the page opens on the given card
        public void PreviewStart(int index, string title)
        {
            lines[0].text = title;
            SetLine(0, 0, 1);
            SetLine(1, 0, 0);

            for (int i = 0; i < dots.Length; i++)
            {
                SetWidth(i, i == index ? currentWidth : dotWidth);
                dots[i].fill.ClipAmount = 0;
            }
        }
#endif
    }
}
