using UnityEngine;
using TMPro;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample ship console. The mode Selector reroutes power, a hull alert comes up now and then and clears through
    /// the Progress Button (on its own while the screen sits in the deck), and new comms messages decode into the feed.
    /// Bars ease through their own smoothing, a Tween Animator switches the alert look and Counters roll the readouts.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShipConsole : DemoScreenContent
    {
        [Header("Power")]
        [SerializeField] private Selector modeSelector;
        [SerializeField] private TextEffects modeEffects;
        [SerializeField] private ProgressBar output;
        [Tooltip("Shield, engines, weapons and life support.")]
        [SerializeField] private ProgressBar[] power;
        [SerializeField, Min(0f)] private float driftInterval = 1.6f;

        [Header("Comms")]
        [Tooltip("The rows' Vertical Layout Group. It slides up one row for every new message.")]
        [SerializeField] private RectTransform feed;
        [Tooltip("Top to bottom. Each new message moves the oldest row to the bottom.")]
        [SerializeField] private FeedRow[] rows;
        [SerializeField] private Counter uplink;
        [SerializeField, Min(0f)] private float messageInterval = 4.2f;
        [SerializeField] private Message[] messages =
        {
            new("NAV", "NAV LOCK ACQUIRED"), new("CARGO", "CARGO SEALS NOMINAL"), new("RELAY", "RELAY 3 ONLINE"),
            new("BCN", "PING FROM SECTOR 7"), new("PWR", "MAIN BUS AT 98%"), new("SENS", "SOLAR FLUX RISING"),
            new("CREW", "ROTATION IN 14 MIN"), new("SENS", "CONTACT AT BRG 212"), new("DOCK", "REQUEST FOR PORT B"),
            new("NAV", "COURSE CORRECTION 0.4")
        };

        [Header("Alert")]
        [SerializeField] private CanvasGroup alert;
        [Tooltip("Plays Alert and Clear: the alert fades over the nominal status while the band, reactor arc and glow change.")]
        [SerializeField] private TweenAnimator alertAnimator;
        [SerializeField] private ProgressButton acknowledge;
        [SerializeField] private Counter hull;
        [SerializeField] private string alertMessage = "STRESS IN SECTOR 4";
        [SerializeField] private string clearMessage = "ALERT CLEARED";
        [SerializeField, Min(0f)] private float alertInterval = 15;
        [Tooltip("In the deck, the alert acknowledges itself after this many seconds.")]
        [SerializeField, Min(0f)] private float autoAcknowledgeDelay = 2.2f;

        [Header("Colors")]
        [SerializeField] private Color accent = new Color32(255, 179, 64, 255);
        [SerializeField] private Color alertColor = new Color32(255, 83, 56, 255);

        [System.Serializable]
        struct FeedRow
        {
            public CanvasGroup group;
            public TMP_Text time;
            public ProceduralShape chip;
            public TMP_Text source;
            public TextEffects message;
        }

        [System.Serializable]
        struct Message
        {
            public string source;
            public string text;

            public Message(string source, string text)
            {
                this.source = source;
                this.text = text;
            }
        }

        // Output, then shield, engines, weapons and life support, in the Selector's order
        static readonly float[][] modes =
        {
            new float[] { 72, 40, 86, 18, 72 },
            new float[] { 96, 92, 58, 96, 61 },
            new float[] { 38, 14, 24, 0, 46 }
        };

        // Row alpha by age, newest first; the canvas blends in linear space, so low values still read
        static readonly float[] ageAlphas = { 1, 0.62f, 0.42f, 0.3f, 0.21f, 0.15f, 0.11f };
        const int StartClock = 4 * 3600 + 7 * 60 + 33;
        const float HullFloor = 91, HullDrain = 1.2f, HullRepair = 0.3125f;
        const int HullProperty = 31301;
        const string ModeSource = "SYS", HullSource = "HULL";

        readonly char[] stamp = new char[5];
        string[] modeLines;
        string alertLine;
        int first, mode, nextMessageIndex;
        bool alerting, initialized;
        float nextDrift, nextMessage = 2, nextAlert, alertStart, hullValue = 100, rowHeight;
        Vector2 feedPosition;
        Color chipColor;
        TweenHandle slide, fadeIn, hullTween;

        void Awake()
        {
            // Tagged once, so pushing the alert line never allocates
            alertLine = "<glitch>" + alertMessage + "</glitch>";
            modeLines = new string[modeSelector.items.Count];
            for (int i = 0; i < modeLines.Length; i++)
                modeLines[i] = "MODE / " + modeSelector.items[i].label;

            feedPosition = feed.anchoredPosition;
            rowHeight = ((RectTransform)rows[0].group.transform).rect.height;
            chipColor = rows[0].chip.OutlineColor;
            nextMessageIndex = rows.Length;
            nextAlert = alertInterval;
            modeSelector.onSelectionChanged.AddListener(SetMode);
            acknowledge.onComplete.AddListener(OnAcknowledged);
        }

        void Start()
        {
            // Seed the feed, oldest on top, a couple of minutes apart
            for (int i = 0; i < rows.Length; i++)
            {
                Message message = messages[i % messages.Length];
                int age = rows.Length - 1 - i;
                WriteRow(rows[i], message.source, message.text, false, (StartClock - age * 100 - 30) / 60);
                rows[i].group.alpha = ageAlphas[Mathf.Min(age, ageAlphas.Length - 1)];
            }

            initialized = true;
            mode = modeSelector.SelectedIndex;
            ApplyMode(true);
            uplink.SetValueInstant(92);
        }

        void OnEnable() => hullTween.Resume();

        void OnDisable()
        {
            slide.Complete();
            fadeIn.Complete();
            hullTween.Pause();
        }

        protected override void Tick(float delta)
        {
            float time = Elapsed;

            // Turning the screen off stops the animator, even mid-transition; the alert look lands where it belongs
            if (alertAnimator.State == PlaybackState.Stopped)
            {
                alertAnimator.Play(alerting ? "Alert" : "Clear");
                alertAnimator.Complete();
            }

            if (time >= nextDrift)
            {
                nextDrift = time + driftInterval;
                ApplyMode(false);
                uplink.SetValue(Random.Range(84, 97));
            }

            if (time >= nextMessage)
            {
                nextMessage = time + messageInterval;
                Message message = messages[nextMessageIndex++ % messages.Length];
                PushLine(message.source, message.text, false, time);
            }

            if (!alerting)
            {
                // A faded-out alert leaves its graphics; switch them off until the next one
                if (alert.gameObject.activeSelf && alert.alpha == 0)
                    alert.gameObject.SetActive(false);

                if (time >= nextAlert)
                    SetAlert(true, time);
            }
            else
            {
                // In the deck nobody can hold the button, so it holds itself
                if (!Focused && time - alertStart >= autoAcknowledgeDelay && !acknowledge.IsHolding
                    && acknowledge.CurrentProgressState == ProgressButton.ProgressState.Normal)
                    acknowledge.StartProgress();
            }
        }

        void SetMode(int index)
        {
            mode = index;
            modeEffects.PlayScramble();
            ApplyMode(false);

            if (initialized)
                PushLine(ModeSource, modeLines[index], false, Elapsed);
        }

        /// <summary>
        /// Moves every bar to the mode's level with a little drift; an alert pulls power into the shield.
        /// </summary>
        void ApplyMode(bool instant)
        {
            float[] levels = modes[mode % modes.Length];
            SetBar(output, levels[0] + (alerting ? -14 : 0) + (instant ? 0 : Random.Range(-2f, 2f)), instant);

            for (int i = 0; i < power.Length; i++)
            {
                float level = levels[i + 1];
                if (level > 0 && !instant)
                    level += Random.Range(-3f, 3f);
                if (alerting && i == 0)
                    level += 30;

                SetBar(power[i], level, instant);
            }
        }

        static void SetBar(ProgressBar bar, float value, bool instant)
        {
            if (instant) { bar.SetValueInstant(value); }
            else { bar.Value = value; }
        }

        void SetAlert(bool value, float time)
        {
            alerting = value;
            alertStart = time;

            if (value)
                alert.gameObject.SetActive(true);
            else
                nextAlert = time + alertInterval;

            alert.blocksRaycasts = value;
            alertAnimator.Play(value ? "Alert" : "Clear");

            // The hull wears down while the alert is up and slowly recovers after it clears
            float target = value ? HullFloor : 100;
            hullTween = EvoTween.Create(hullValue, target, Mathf.Abs(target - hullValue) / (value ? HullDrain : HullRepair))
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (console, integrity) => console.SetHull(integrity), HullProperty);

            PushLine(HullSource, value ? alertLine : clearMessage, value, time);
            ApplyMode(false);
        }

        void SetHull(float integrity)
        {
            hullValue = integrity;
            hull.SetValue(Mathf.FloorToInt(integrity));
        }

        void OnAcknowledged()
        {
            if (alerting)
                SetAlert(false, Elapsed);
        }

        #region Feed
        void PushLine(string source, string text, bool isAlert, float time)
        {
            slide.Kill();
            fadeIn.Kill();

            // The oldest row becomes the newest: the layout group moves it to the bottom and every other row up a slot
            FeedRow row = rows[first];
            first = (first + 1) % rows.Length;
            row.group.transform.SetAsLastSibling();
            WriteRow(row, source, text, isAlert, (StartClock + (int)time) / 60);
            row.message.PlayScramble();

            for (int i = 0; i < rows.Length - 1; i++)
                rows[(first + i) % rows.Length].group.alpha = ageAlphas[Mathf.Min(rows.Length - 1 - i, ageAlphas.Length - 1)];

            // Same picture as before the push, then the feed slides up one row while the new one fades in
            feed.anchoredPosition = feedPosition - new Vector2(0, rowHeight);
            slide = EvoTween.Create(feed.anchoredPosition, feedPosition, 0.4f)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAnchoredPosition(feed);
            fadeIn = EvoTween.Create(0f, 1f, 0.3f)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(row.group);
        }

        void WriteRow(FeedRow row, string source, string text, bool isAlert, int minute)
        {
            int hours = minute / 60 % 24;
            int minutes = minute % 60;
            stamp[0] = (char)('0' + hours / 10);
            stamp[1] = (char)('0' + hours % 10);
            stamp[2] = ':';
            stamp[3] = (char)('0' + minutes / 10);
            stamp[4] = (char)('0' + minutes % 10);
            row.time.SetCharArray(stamp, 0, stamp.Length);
            row.source.SetText(source);
            row.message.SetText(text);

            Color color = isAlert ? alertColor : accent;
            row.source.color = color;
            row.message.TextComponent.color = color;
            row.chip.OutlineColor = isAlert ? alertColor : chipColor;
        }
        #endregion
    }
}
