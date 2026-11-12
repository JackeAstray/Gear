using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Distribution Bar tab: a map vote. Others vote every second until the round ends, and a row click puts your votes on that map
    /// (or takes them back). Each change is one SetValues call, so the bar eases every share together. The winner gets a trophy.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoDistributionBar : MonoBehaviour
    {
        [SerializeField] private DistributionBar bar;
        [SerializeField] private Interactive[] rows;
        [SerializeField] private TMP_Text[] voteTexts;
        [SerializeField] private Graphic[] thumbnails;
        [SerializeField] private GameObject[] voters;
        [SerializeField] private Image[] marks;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Image timerIcon;
        [SerializeField] private int[] startVotes = { 8, 5, 4 };
        [SerializeField] private float[] othersShare = { 0.45f, 0.33f, 0.22f };
        [SerializeField] private int yourVotes = 5;
        [SerializeField] private int roundLength = 30;
        [SerializeField] private float nextRoundDelay = 3;

        [Header("Icons")]
        [SerializeField] private Sprite clockIcon;
        [SerializeField] private Sprite trophyIcon;
        [SerializeField] private Sprite checkIcon;

        const float PopScale = 0.6f;
        const float PopDuration = 0.35f;

        int[] votes;
        float[] values;
        int mine = -1;
        int winner = -1;
        int secondsLeft;
        float elapsed;
        TweenHandle pop;
        RectTransform popped;

        void Awake()
        {
            votes = new int[rows.Length];
            values = new float[rows.Length];

            // The map art wears the segment colors
            for (int i = 0; i < thumbnails.Length; i++)
                thumbnails[i].color = bar.segments[i].fillShape.color;
        }

        void OnEnable() => StartRound(true);

        void OnDisable() => StopPop();

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (secondsLeft == 0)
            {
                if (elapsed >= nextRoundDelay)
                    StartRound(false);

                return;
            }

            if (elapsed < 1)
                return;

            elapsed = 0;
            secondsLeft--;
            votes[PickMap()] += Random.Range(1, 5);
            DemoUtils.SetClock(timer, string.Empty, secondsLeft);

            if (secondsLeft == 0)
            {
                winner = Leader();
                timerIcon.sprite = trophyIcon;
                Pop(marks[winner].rectTransform);
            }

            Apply(false);
        }

        void StartRound(bool instant)
        {
            for (int i = 0; i < votes.Length; i++)
                votes[i] = startVotes[i] + (instant ? 0 : Random.Range(0, 7));

            mine = -1;
            winner = -1;
            secondsLeft = roundLength;
            elapsed = 0;
            timerIcon.sprite = clockIcon;
            DemoUtils.SetClock(timer, string.Empty, secondsLeft);
            Apply(instant);
        }

        // Clicking your map again takes your votes back. The vote is closed once the round ends.
        public void Vote(int index)
        {
            if (secondsLeft == 0)
                return;

            if (mine >= 0)
                votes[mine] -= yourVotes;

            mine = mine == index ? -1 : index;
            if (mine >= 0)
            {
                votes[mine] += yourVotes;
                Pop(marks[mine].rectTransform);
            }

            Apply(false);
        }

        void Apply(bool instant)
        {
            for (int i = 0; i < votes.Length; i++)
            {
                values[i] = votes[i];
                voteTexts[i].SetText("{0}", votes[i]);

                // Your map shows a check, the winner a trophy, the rest who voted
                bool marked = i == mine || i == winner;
                voters[i].SetActive(!marked);
                marks[i].gameObject.SetActive(marked);
                marks[i].sprite = i == winner ? trophyIcon : checkIcon;

                var state = i == mine ? InteractionState.Selected : InteractionState.Normal;
                if (rows[i].InteractionState != state && (state == InteractionState.Selected || rows[i].InteractionState == InteractionState.Selected))
                    rows[i].SetState(state);
            }

            bar.SetValues(values, false, instant);
        }

        void Pop(RectTransform target)
        {
            StopPop();
            popped = target;
            pop = EvoTween.Create(Vector3.one * PopScale, Vector3.one, PopDuration)
                .WithEase(Ease.OutBack)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToScale(target);
        }

        // A new pop on another mark would leave the last one mid-scale
        void StopPop()
        {
            pop.Kill();
            if (popped != null)
                popped.localScale = Vector3.one;
        }

        int PickMap()
        {
            float pick = Random.value;

            for (int i = 0; i < othersShare.Length - 1; i++)
            {
                if (pick < othersShare[i])
                    return i;

                pick -= othersShare[i];
            }

            return othersShare.Length - 1;
        }

        int Leader()
        {
            int leader = 0;

            for (int i = 1; i < votes.Length; i++)
            {
                if (votes[i] > votes[leader])
                    leader = i;
            }

            return leader;
        }

        // Percent or Votes
        public void SetTextDisplay(int index)
        {
            bar.textDisplay = (DistributionBar.TextDisplay)index;
            bar.Refresh();
        }

        public void SetHideBelow(float percent)
        {
            bar.minimumTextPercentage = percent;
            bar.Refresh();
        }

        // From left or From right
        public void SetDirection(int index)
        {
            bar.direction = index == 0 ? DistributionBar.Direction.LeftToRight : DistributionBar.Direction.RightToLeft;
            bar.Refresh();
        }

        public void SetSmoothing(bool value) => bar.enableSmoothing = value;
    }
}
