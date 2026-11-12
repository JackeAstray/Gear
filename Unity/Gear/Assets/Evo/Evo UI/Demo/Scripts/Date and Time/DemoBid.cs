using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Timer tab: an auction lot that sells when its countdown runs out. Every bid adds time with AddTime, so the rolling
    /// digits turn back up, and the other bidders join in by themselves a few times per lot. The leader moves to the front
    /// of the bidder stack. After the sale the button lists a new lot; Loop does that on its own.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoBid : DemoTimer
    {
        const int SlideKey = 1;
        const int RiseKey = 2;
        const float SlideDuration = 0.35f;
        const float FadeDuration = 0.25f;
        const float RiseDuration = 0.9f;
        const float RiseDistance = 34;
        const float BotChance = 0.8f;

        [SerializeField] private float bidTime = 10;
        [SerializeField] private Button bidButton;
        [SerializeField] private string bidText = "Place bid";
        [SerializeField] private Sprite bidIcon;
        [SerializeField] private string relistText = "New lot";
        [SerializeField] private Sprite relistIcon;
        [SerializeField] private float relistDelay = 3;

        [Header("Bidders")]
        [SerializeField] private RectTransform[] bidders;
        [SerializeField] private CanvasGroup[] leadRings;
        [SerializeField] private Vector2 backSlot = new(-32, -82);
        [SerializeField] private float spacing = 30;
        [SerializeField] private CanvasGroup winner;
        [SerializeField] private Vector2 winnerOffset = new(10, 19);
        [SerializeField] private Vector2 botDelay = new(3, 8);
        [SerializeField] private int botBids = 3;

        [Header("Bid Label")]
        [SerializeField] private TMP_Text bidLabel;
        [SerializeField] private StylerObject bidLabelStyler;
        [SerializeField] private CanvasGroup bidLabelGroup;
        [SerializeField] private float labelGap = 14;

        // Bidder indexes front to back; 0 is you
        readonly List<int> order = new();
        Vector2 labelBase;
        TweenHandle rise;
        int leader = -1;
        int botCount;
        bool sold;
        float nextBot;
        float relistAt;

        void OnDisable()
        {
            rise.Kill();
            bidLabelGroup.alpha = 0;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (sold)
            {
                if (relistAt > 0 && now >= relistAt)
                    Relist(true);

                return;
            }

            if (now < nextBot)
                return;

            // Now and then a bidder lets the moment pass
            if (timer.IsRunning && botCount < botBids && Random.value < BotChance)
            {
                botCount++;
                Bid(Random.Range(1, bidders.Length));
            }

            nextBot = now + Random.Range(botDelay.x, botDelay.y);
        }

        public override void Restart() => Relist(false);

        // The button's click: a bid while the lot is open, a new lot once it sold
        public void PressButton()
        {
            if (sold) { Relist(true); }
            else { Bid(0); }
        }

        public override void SetRunning(bool value)
        {
            if (!sold) { base.SetRunning(value); }
            else if (value) { Relist(false); }
        }

        public override void SetLoop(bool value)
        {
            base.SetLoop(value);
            if (sold)
                relistAt = value ? Time.unscaledTime + relistDelay * 0.5f : 0;
        }

        protected override void OnTimerComplete()
        {
            sold = true;
            if (leader < 0)
                Lead(Random.Range(1, bidders.Length));

            ((RectTransform)winner.transform).anchoredPosition = Slot(0) + winnerOffset;
            Fade(winner, true);
            SetButton(relistText, relistIcon);
            ShowRunning(false);
            relistAt = Loop ? Time.unscaledTime + relistDelay : 0;
        }

        void Relist(bool step)
        {
            if (step)
                ShowRunning(true);

            sold = false;
            relistAt = 0;
            botCount = 0;
            leader = -1;
            nextBot = Time.unscaledTime + Random.Range(botDelay.x, botDelay.y);

            // You start at the back of the stack
            order.Clear();
            for (int i = 1; i <= bidders.Length; i++)
                order.Add(i % bidders.Length);

            Arrange(step);
            Fade(winner, false, step);
            SetButton(bidText, bidIcon);
            timer.ResetTimer();
            Run();
        }

        void Bid(int bidder)
        {
            Lead(bidder);
            timer.AddTime(timer.countDown ? bidTime : -bidTime);
            ShowLabel(bidder == 0);
        }

        void Lead(int bidder)
        {
            leader = bidder;
            order.Remove(bidder);
            order.Insert(0, bidder);
            Arrange(true);
        }

        Vector2 Slot(int index) => backSlot - new Vector2((order.Count - 1 - index) * spacing, 0);

        // The front bidder draws last, so it overlaps the ones behind it
        void Arrange(bool animate)
        {
            for (int i = order.Count - 1; i >= 0; i--)
            {
                RectTransform bidder = bidders[order[i]];
                bidder.SetAsLastSibling();

                // A zero-length slide still replaces one that is running
                if (!animate)
                    bidder.anchoredPosition = Slot(i);

                EvoTween.Create(bidder.anchoredPosition.x, Slot(i).x, animate ? SlideDuration : 0)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(bidder, static (r, v) => r.anchoredPosition = new Vector2(v, r.anchoredPosition.y), SlideKey);
            }

            for (int i = 0; i < leadRings.Length; i++)
                Fade(leadRings[i], i == leader, animate);
        }

        // "+10 s" rises from the end of the digits: in the accent for your bids, dim for the others
        void ShowLabel(bool yours)
        {
            bidLabelStyler.colorID = yours ? "Accent" : "Primary (Dim)";
            bidLabelStyler.UpdateStyler();

            var digits = (RectTransform)timer.transform;
            float width = timer.valueText is TMP_Text text ? text.preferredWidth : 0;
            labelBase = new Vector2(digits.anchoredPosition.x + width + labelGap, digits.anchoredPosition.y);

            rise.Kill();
            rise = EvoTween.Create(0f, 1f, RiseDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (bid, t) => bid.ApplyRise(t), RiseKey);
        }

        void ApplyRise(float t)
        {
            bidLabel.rectTransform.anchoredPosition = labelBase + new Vector2(0, RiseDistance * t);
            bidLabelGroup.alpha = t < 0.6f ? 1 : 1 - (t - 0.6f) / 0.4f;
        }

        void SetButton(string text, Sprite icon)
        {
            bidButton.Text = text;
            bidButton.Icon = icon;
        }

        static void Fade(CanvasGroup group, bool visible, bool animate = true)
        {
            if (!animate)
                group.alpha = visible ? 1 : 0;

            DemoUtils.Fade(group, visible ? 1f : 0f, animate ? FadeDuration : 0);
        }
    }
}
