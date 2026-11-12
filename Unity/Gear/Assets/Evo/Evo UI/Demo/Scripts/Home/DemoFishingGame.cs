using System;
using UnityEngine;
using TMPro;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample fishing trip planner. The Dropdown's items are the spots and the Radio Button Group's buttons the days;
    /// each spot holds its tide, catch odds and sonar readings for every day. Tween Animators run the sonar's sweep, the
    /// blips' flashes, the ping and the wake after setting sail. This swaps the picked data in, eases the tide point by
    /// point with its marks riding along, and moves the blips along their bearings.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFishingGame : DemoScreenContent
    {
        [Serializable]
        public class Spot
        {
            [Tooltip("One name per catch odds row.")]
            public string[] species = new string[4];
            [Range(0f, 100f)] public float logbook = 50;
            public int caught;
            public int bestDay;
            [Tooltip("Notification title after setting sail.")]
            public string sailed;
            [Tooltip("One per day, in the same order as the Radio Button Group's buttons.")]
            public Tide[] tides = new Tide[0];
        }

        [Serializable]
        public class Tide
        {
            [Tooltip("Day, depth and sea state above the spot's name.")]
            public string caption;
            [TextArea] public string summary;
            [Tooltip("One share per Distribution Bar segment.")]
            public float[] odds = { 50, 30, 15, 5 };
            [Tooltip("One height per Line Chart point, in meters.")]
            public float[] heights = new float[5];
            [Tooltip("One per mark view, in the same order as Marks.")]
            public Mark[] marks = new Mark[0];
            [Tooltip("One per blip: distance from the sonar's center as a share of Sonar Radius. Zero hides the blip.")]
            public float[] blips = new float[0];
        }

        [Serializable]
        public class Mark
        {
            [Tooltip("Index of the Line Chart point the mark sits on.")]
            public int point;
            public string label;
            public string time;
        }

        [Serializable]
        public class MarkView
        {
            public RectTransform rect;
            [Tooltip("Fades the texts out and back in while they change.")]
            public CanvasGroup texts;
            public TMP_Text label;
            public TMP_Text time;
            public Counter height;
        }

        [Serializable]
        public class Blip
        {
            public RectTransform rect;
            [Tooltip("Shows and hides the blip. Its flash runs on a Tween Animator of the sweep's length.")]
            public CanvasGroup group;
            [Tooltip("Degrees clockwise from up.")]
            public float bearing;
        }

        [Header("Spot and Day")]
        [Tooltip("Its items are the spots, in the same order as Spots.")]
        [SerializeField] private Dropdown spot;
        [SerializeField] private Spot[] spots;
        [Tooltip("One button per day, in the same order as each spot's tides.")]
        [SerializeField] private RadioButtonGroup day;
        [Tooltip("Picks another spot and another day.")]
        [SerializeField] private Button shuffle;

        [Header("Board")]
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text summary;
        [Tooltip("Show state: the spot's name and tide fade in after a change.")]
        [SerializeField] private TweenAnimator details;
        [SerializeField] private DistributionBar odds;
        [Tooltip("One per catch odds row.")]
        [SerializeField] private TMP_Text[] species;
        [Tooltip("One per catch odds row.")]
        [SerializeField] private Counter[] shares;
        [SerializeField] private ProgressBar logbookRing;
        [SerializeField] private Counter logbook;
        [SerializeField] private Counter caught;
        [SerializeField] private Counter bestDay;

        [Header("Tide")]
        [SerializeField] private LineChart tide;
        [Tooltip("Anchored inside the chart's plot area.")]
        [SerializeField] private MarkView[] marks;
        [SerializeField, Min(0.01f)] private float changeDuration = 1.1f;
        [Tooltip("Share of the change each point starts after the one on its left.")]
        [SerializeField, Range(0f, 0.2f)] private float pointLag = 0.1f;

        [Header("Sonar")]
        [SerializeField] private Blip[] blips;
        [SerializeField, Min(0f)] private float sonarRadius = 114;
        [Tooltip("Ping state after a change, Sail state after setting sail.")]
        [SerializeField] private TweenAnimator ping;

        [Header("Set Sail")]
        [SerializeField] private ProgressButton setSail;
        [SerializeField] private Ripple ripple;
        [SerializeField] private Selector boat;
        [SerializeField] private Notification notification;

        const string ShowState = "Show", PingState = "Ping", SailState = "Sail";
        const int ChangeProperty = 32102;

        int spotIndex, dayIndex;
        bool textsChanged;
        float[] fromHeights, radii, fromRadii, fromAlphas;
        TweenHandle changeTween;

        Tide Current => spots[spotIndex].tides[dayIndex];

        void Awake()
        {
            fromHeights = new float[tide.dataPoints.Count];
            radii = new float[blips.Length];
            fromRadii = new float[blips.Length];
            fromAlphas = new float[blips.Length];

            spot.onItemSelected.AddListener(PickSpot);
            day.onSelectionChanged.AddListener(PickDay);
            shuffle.onClick.AddListener(Shuffle);
            setSail.onComplete.AddListener(Sail);
        }

        void Start()
        {
            spotIndex = Mathf.Max(0, spot.items.IndexOf(spot.SelectedItem));
            dayIndex = Mathf.Max(0, day.selectedIndex);
            Show(false);
        }

        // The deck turns hidden screens off; a change cut short lands on its end
        void OnDisable()
        {
            if (!changeTween.IsActive)
                return;

            changeTween.Kill();
            Apply(1);
        }

        protected override void OnFocusChanged(bool value)
        {
            if (!value && spot.IsOpen)
                spot.Close();
        }

        #region Picks
        void PickSpot(int index)
        {
            if (index == spotIndex)
                return;

            spotIndex = index;
            Show(true);
        }

        void PickDay(int index)
        {
            if (index == dayIndex)
                return;

            dayIndex = index;
            Show(true);
        }

        // Another spot and another day, picked quietly so the board changes once
        void Shuffle()
        {
            spotIndex = (spotIndex + UnityEngine.Random.Range(1, spots.Length)) % spots.Length;
            int days = spots[spotIndex].tides.Length;
            dayIndex = (dayIndex + UnityEngine.Random.Range(1, days)) % days;
            spot.SelectItem(spot.items[spotIndex], false);
            day.SetButton(dayIndex, true, false);
            Show(true);
        }

        void Sail()
        {
            ripple.Create();
            ping.Play(SailState);
            notification.SetContent(null, spots[spotIndex].sailed, boat.items[boat.SelectedIndex].label);
            notification.Open();
        }
        #endregion

        #region Board
        void Show(bool animated)
        {
            Spot place = spots[spotIndex];
            Tide water = Current;

            caption.SetText(water.caption);
            title.SetText(spot.items[spotIndex].label);
            summary.SetText(water.summary);

            odds.SetValues(water.odds, false, !animated);
            for (int i = 0; i < shares.Length; i++)
            {
                species[i].SetText(place.species[i]);
                SetCounter(shares[i], water.odds[i], animated);
            }

            SetCounter(logbook, place.logbook, animated);
            SetCounter(caught, place.caught, animated);
            SetCounter(bestDay, place.bestDay, animated);
            if (animated)
                logbookRing.Value = place.logbook;
            else
                logbookRing.SetValueInstant(place.logbook);

            for (int i = 0; i < marks.Length; i++)
                SetCounter(marks[i].height, water.heights[water.marks[i].point], animated);

            // The change starts from wherever the tide and the blips are now
            for (int i = 0; i < fromHeights.Length; i++)
                fromHeights[i] = tide.dataPoints[i].value;

            for (int i = 0; i < blips.Length; i++)
            {
                fromRadii[i] = blips[i].group.alpha > 0 ? radii[i] : water.blips[i];
                fromAlphas[i] = blips[i].group.alpha;
            }

            changeTween.Kill();
            textsChanged = false;
            if (!animated)
            {
                Apply(1);
                return;
            }

            details.Play(ShowState);
            ping.Play(PingState);
            changeTween = EvoTween.Create(0f, 1f, changeDuration)
                .WithEase(Ease.Linear)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (board, t) => board.Apply(t), ChangeProperty);
        }

        static void SetCounter(Counter counter, float value, bool animated)
        {
            if (animated)
                counter.SetValue(value);
            else
                counter.SetValueInstant(value);
        }

        // Each point eases on its own, a little after the one on its left, so the change travels across the water
        void Apply(float t)
        {
            Tide water = Current;
            int count = fromHeights.Length;
            float span = 1 - pointLag * (count - 1);

            for (int i = 0; i < count; i++)
            {
                float k = TweenEase.Evaluate(Ease.InOutCubic, Mathf.Clamp01((t - i * pointLag) / span));
                tide.dataPoints[i].value = Mathf.LerpUnclamped(fromHeights[i], water.heights[i], k);
            }

            tide.MarkDirty();

            // Mark texts fade out, change, then fade back in once their points have moved
            float textAlpha = t < 0.18f ? 1 - t / 0.18f : Mathf.Clamp01((t - 0.4f) / 0.3f);
            if (!textsChanged && t >= 0.18f)
            {
                textsChanged = true;
                for (int i = 0; i < marks.Length; i++)
                {
                    marks[i].label.SetText(water.marks[i].label);
                    marks[i].time.SetText(water.marks[i].time);
                }
            }

            for (int i = 0; i < marks.Length; i++)
            {
                int point = water.marks[i].point;
                PlaceMark(marks[i].rect, point, tide.dataPoints[point].value);
                marks[i].texts.alpha = textAlpha;
            }

            float e = TweenEase.Evaluate(Ease.InOutCubic, Mathf.Clamp01(t / 0.75f));
            for (int i = 0; i < blips.Length; i++)
            {
                float target = water.blips[i];
                radii[i] = Mathf.Lerp(fromRadii[i], target > 0 ? target : fromRadii[i], e);
                blips[i].group.alpha = Mathf.Lerp(fromAlphas[i], target > 0 ? 1 : 0, e);

                float bearing = blips[i].bearing * Mathf.Deg2Rad;
                blips[i].rect.anchoredPosition = new Vector2(Mathf.Sin(bearing), Mathf.Cos(bearing)) * (radii[i] * sonarRadius);
            }
        }

        void PlaceMark(RectTransform mark, int point, float value)
        {
            Vector2 range = tide.valueRange;
            var anchor = new Vector2(point / (float)(fromHeights.Length - 1), Mathf.InverseLerp(range.x, range.y, value));
            mark.anchorMin = mark.anchorMax = anchor;
        }
        #endregion
    }
}
