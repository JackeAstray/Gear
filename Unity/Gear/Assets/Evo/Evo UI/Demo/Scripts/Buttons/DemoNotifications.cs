using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Button (Icon Only) tab: a stack of notifications, each with a close button. A closed card slides out
    /// and the ones below move up; once all are gone, they come back after a short pause.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoNotifications : DemoButtonPage
    {
        [Tooltip("Top-anchored cards, in stack order from the top.")]
        [SerializeField] private RectTransform[] cards;
        [SerializeField] private CanvasGroup[] cardGroups;
        [SerializeField] private float spacing = 114f;
        [SerializeField] private float slide = 80f;
        [SerializeField, Min(0f)] private float duration = 0.3f;
        [SerializeField, Min(0f)] private float refreshDelay = 0.5f;

        const int LayoutKey = 1;

        bool[] open;
        Vector2 top;
        Vector2[] fromPosition, toPosition;
        float[] fromAlpha, toAlpha;
        TweenHandle tween;
        Action refresh;

        protected override void Awake()
        {
            base.Awake();

            open = new bool[cards.Length];
            fromPosition = new Vector2[cards.Length];
            toPosition = new Vector2[cards.Length];
            fromAlpha = new float[cards.Length];
            toAlpha = new float[cards.Length];
            top = cards[0].anchoredPosition;
            refresh = () => Refresh(true);
            Refresh(false);
        }

        public void Close(int index)
        {
            open[index] = false;
            Layout(true, 0f);
        }

        void Refresh(bool animate)
        {
            for (int i = 0; i < open.Length; i++)
                open[i] = true;

            Layout(animate, refreshDelay);
        }

        void Layout(bool animate, float delay)
        {
            int row = 0;

            for (int i = 0; i < cards.Length; i++)
            {
                fromPosition[i] = cards[i].anchoredPosition;
                fromAlpha[i] = cardGroups[i].alpha;
                toAlpha[i] = open[i] ? 1f : 0f;
                toPosition[i] = open[i] ? top + new Vector2(0f, -spacing * row++) : new Vector2(top.x + slide, fromPosition[i].y);
                cardGroups[i].blocksRaycasts = open[i];
            }

            // Once the last card has slid out, all of them come back
            tween.Kill();
            if (!animate) { Apply(1f); }
            else
            {
                tween = EvoTween.Create(0f, 1f, duration)
                    .WithDelay(delay)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(row == 0 ? refresh : null)
                    .Bind(this, static (demo, t) => demo.Apply(t), LayoutKey);
            }
        }

        void Apply(float t)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].anchoredPosition = Vector2.LerpUnclamped(fromPosition[i], toPosition[i], t);
                cardGroups[i].alpha = Mathf.Lerp(fromAlpha[i], toAlpha[i], t);
            }
        }
    }
}
