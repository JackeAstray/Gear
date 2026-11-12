using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    /// <summary>
    /// iOS-style time picker that combines hour, minute and AM/PM wheels.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Time Picker"), HelpURL(Constants.HelpUrl + "ui-elements/time-picker")]
    public class TimePicker : MonoBehaviour
    {
        // Initial Time
        [SerializeField, Tooltip("Current reads the device clock on start. Edit Mode previews it as 9:41.")]
        private InitialTimeMode initialTimeMode = InitialTimeMode.Current;
        [SerializeField, Range(0, 23)] private int customHour = 9;
        [SerializeField, Range(0, 59)] private int customMinute = 41;

        // Settings
        [SerializeField, Tooltip("Auto follows the device's regional time format.")]
        private HourFormat hourFormat = HourFormat.TwelveHour;
        [SerializeField, Tooltip("Step between selectable minutes. Times in between round down.")]
        private MinuteInterval minuteInterval = MinuteInterval.One;
        [SerializeField, Min(1f), Tooltip("Row height of every wheel. The selection indicator follows it.")]
        private float rowHeight = 44f;
        [SerializeField] private string amLabel = "AM";
        [SerializeField] private string pmLabel = "PM";

#if EVO_LOCALIZATION
        // Localization
        [Tooltip("Resolves the AM and PM labels from their table keys on enable and when the language changes.")]
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        [SerializeField] private string amKey = "";
        [SerializeField] private string pmKey = "";
#endif

        // Styling
        [Tooltip("The provider that will manage the tick sound.")]
        public StyleProvider styleProvider = StyleProvider.Styler;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        public StylerPreset stylerPreset;

        // Sound Effects
        [Tooltip("Plays a tick as rows pass the center while the user turns a wheel.")]
        public bool soundEffects = true;
        public AudioMapping tickSFX = new() { stylerID = "Click SFX" };

        // References
        public PickerWheel hourWheel;
        public PickerWheel minuteWheel;
        [Tooltip("Hidden while the picker uses the 24-hour format.")]
        public PickerWheel periodWheel;
        [Tooltip("Optional highlight behind the selected row, anchored to the vertical middle. Its height follows Row Height.")]
        public RectTransform selectionIndicator;

        // Events
        [Tooltip("Invoked with the time of day when the user settles on a new time, or when a notifying SetTime changes it.")]
        public UnityEvent<TimeSpan> onTimeChanged = new();

        // Enums
        public enum InitialTimeMode { Current, Custom }

        public enum HourFormat
        {
            [InspectorName("12-Hour")] TwelveHour = 0,
            [InspectorName("24-Hour")] TwentyFourHour = 1,
            Auto = 2
        }

        public enum MinuteInterval
        {
            [InspectorName("1 Minute")] One = 1,
            [InspectorName("2 Minutes")] Two = 2,
            [InspectorName("3 Minutes")] Three = 3,
            [InspectorName("4 Minutes")] Four = 4,
            [InspectorName("5 Minutes")] Five = 5,
            [InspectorName("6 Minutes")] Six = 6,
            [InspectorName("10 Minutes")] Ten = 10,
            [InspectorName("12 Minutes")] Twelve = 12,
            [InspectorName("15 Minutes")] Fifteen = 15,
            [InspectorName("20 Minutes")] Twenty = 20,
            [InspectorName("30 Minutes")] Thirty = 30
        }

        // Constants
        const int PreviewHour = 9;
        const int PreviewMinute = 41;
        const float MinTickInterval = 0.04f;
        static readonly string[] TwoDigitLabels = CreateLabels(60, "00");
        static readonly string[] Hour24Labels = CreateLabels(24, "00");
        static readonly string[] Hour12Labels = CreateHour12Labels();
        static readonly string[][] MinuteLabels = new string[31][]; // By step, made on first use and shared

        // State
        int hour = PreviewHour;
        int minute = PreviewMinute;
        bool isInitialized;
        bool hasNotified;
        TimeSpan lastNotifiedTime;
        float lastTickTime = float.NegativeInfinity;

        // Cache
        readonly string[] periodLabels = new string[2];
        DrivenRectTransformTracker tracker;
        RectTransform drivenIndicator;
        PickerWheel boundHour;
        PickerWheel boundMinute;
        PickerWheel boundPeriod;
        Action<PickerWheel, int, int> hourCommitted;
        Action<PickerWheel, int, int> minuteCommitted;
        Action<PickerWheel, int, int> periodCommitted;
        Action<PickerWheel> wheelTicked;
#if EVO_LOCALIZATION
        Action<Localization.LocalizationLanguage> languageChanged;
#endif

        int Step
        {
            get
            {
                int step = (int)minuteInterval;
                return step >= 1 && step <= 30 && 60 % step == 0 ? step : 1;
            }
        }

        void Start()
        {
            if (Application.IsPlaying(gameObject))
                Initialize();
        }

        void OnEnable()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                // Plain Instantiate copies, such as Catalog previews, get no OnValidate and can be captured
                // before any delayed call, so Edit Mode previews right away. The delayed refresh still follows.
                if (CanPreviewNow())
                    ApplyEditorPreview();

                RequestEditorRefresh();
                return;
            }
#endif
            if (isInitialized)
                Activate();
        }

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(languageChanged);
#endif
            tracker.Clear();
            drivenIndicator = null;

#if UNITY_EDITOR
            CancelEditorRefresh();
#endif
        }

        // Wheels stay bound while disabled, as one settling during deactivation still commits its value
        void OnDestroy() => UnbindWheels();

        void Initialize()
        {
            if (isInitialized || !Application.IsPlaying(gameObject))
                return;

            isInitialized = true;
            TimeSpan time = initialTimeMode == InitialTimeMode.Current ? DateTime.Now.TimeOfDay
                : new TimeSpan(Mathf.Clamp(customHour, 0, 23), Mathf.Clamp(customMinute, 0, 59), 0);

            hour = time.Hours;
            minute = FloorToStep(time.Minutes);
            Activate();

            hasNotified = true;
            lastNotifiedTime = SelectedTime;
            onTimeChanged?.Invoke(lastNotifiedTime);
        }

        // Runs once started and on every later enable
        void Activate()
        {
            BindWheels();
            ApplyAll();

#if EVO_LOCALIZATION
            languageChanged ??= UpdateLocalization;
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, languageChanged);
#endif
        }

        #region Wheels
        void BindWheels()
        {
            if (boundHour == hourWheel && boundMinute == minuteWheel && boundPeriod == periodWheel)
                return;

            UnbindWheels();
            hourCommitted ??= OnHourCommitted;
            minuteCommitted ??= OnMinuteCommitted;
            periodCommitted ??= OnPeriodCommitted;
            wheelTicked ??= OnWheelTicked;

            boundHour = hourWheel;
            boundMinute = minuteWheel;
            boundPeriod = periodWheel;
            Bind(boundHour, hourCommitted, true);
            Bind(boundMinute, minuteCommitted, true);
            Bind(boundPeriod, periodCommitted, true);
        }

        void UnbindWheels()
        {
            Bind(boundHour, hourCommitted, false);
            Bind(boundMinute, minuteCommitted, false);
            Bind(boundPeriod, periodCommitted, false);

            boundHour = null;
            boundMinute = null;
            boundPeriod = null;
        }

        void Bind(PickerWheel wheel, Action<PickerWheel, int, int> committed, bool bind)
        {
            if (wheel == null)
                return;

            if (bind)
            {
                wheel.RawIndexCommitted += committed;
                wheel.CenterTicked += wheelTicked;
            }
            else
            {
                wheel.RawIndexCommitted -= committed;
                wheel.CenterTicked -= wheelTicked;
            }
        }

        void ApplyAll()
        {
            ApplyRowHeight();
            ApplyFormat();
            SyncWheels(false);
        }

        void ApplyRowHeight()
        {
            float height = Mathf.Max(1f, rowHeight);
            if (hourWheel != null)
                hourWheel.ItemHeight = height;
            if (minuteWheel != null)
                minuteWheel.ItemHeight = height;
            if (periodWheel != null)
                periodWheel.ItemHeight = height;

            // Driven again only when it changes, as tracking it anew in an open undo group records its height as an override
            if (drivenIndicator != selectionIndicator)
            {
                tracker.Clear();
                drivenIndicator = selectionIndicator;
                if (drivenIndicator != null)
                    tracker.Add(this, drivenIndicator, DrivenTransformProperties.SizeDeltaY);
            }

            if (selectionIndicator == null)
                return;

            Vector2 size = selectionIndicator.sizeDelta;
            if (size.y != height)
            {
                size.y = height;
                selectionIndicator.sizeDelta = size;
            }
        }

        void ApplyFormat()
        {
            bool is24Hour = Is24Hour;

            if (hourWheel != null)
            {
                hourWheel.Loop = true;
                SetItems(hourWheel, is24Hour ? Hour24Labels : Hour12Labels, false);
            }

            if (minuteWheel != null)
            {
                minuteWheel.Loop = true;
                SetItems(minuteWheel, GetMinuteLabels(Step), false);
            }

            if (periodWheel != null)
            {
                string am = GetPeriodLabel(false);
                string pm = GetPeriodLabel(true);
                bool changed = periodLabels[0] != am || periodLabels[1] != pm;
                periodLabels[0] = am;
                periodLabels[1] = pm;
                periodWheel.Loop = false;
                SetItems(periodWheel, periodLabels, changed);
                SetActive(periodWheel.gameObject, !is24Hour);
            }
        }

        // Wheels read their items while drawing, so an unchanged list needs no redraw
        static void SetItems(PickerWheel wheel, string[] labels, bool changed)
        {
            if (changed || !ReferenceEquals(wheel.Items, labels))
                wheel.SetItems(labels);
        }

        void SyncWheels(bool animated)
        {
            if (hourWheel != null)
                hourWheel.SetIndex(Is24Hour ? hour : hour % 12, animated, false);
            if (minuteWheel != null)
                minuteWheel.SetIndex(minute / Step, animated, false);
            if (periodWheel != null)
                periodWheel.SetIndex(hour >= 12 ? 1 : 0, animated, false);
        }

        void OnHourCommitted(PickerWheel wheel, int previousRaw, int raw)
        {
            if (Is24Hour)
            {
                hour = wheel.SelectedIndex;
            }
            else
            {
                // As on iOS, turning the hours past 11 and 12 also turns AM/PM
                bool isPM = hour >= 12;
                if (((FloorDivide(raw, 12) - FloorDivide(previousRaw, 12)) & 1) != 0)
                {
                    isPM = !isPM;
                    if (periodWheel != null)
                        periodWheel.SetIndex(isPM ? 1 : 0, true, false);
                }

                hour = wheel.SelectedIndex + (isPM ? 12 : 0);
            }

            NotifyTimeChanged();
        }

        void OnMinuteCommitted(PickerWheel wheel, int previousRaw, int raw)
        {
            minute = Mathf.Clamp(wheel.SelectedIndex * Step, 0, 59);
            NotifyTimeChanged();
        }

        void OnPeriodCommitted(PickerWheel wheel, int previousRaw, int raw)
        {
            hour = hour % 12 + (wheel.SelectedIndex == 1 ? 12 : 0);
            NotifyTimeChanged();
        }

        void OnWheelTicked(PickerWheel wheel)
        {
            if (!soundEffects)
                return;

            // Fast spins would otherwise stack clicks
            float now = Time.unscaledTime;
            if (now - lastTickTime < MinTickInterval)
                return;

            lastTickTime = now;
            AudioManager.PlayClip(Styler.GetAudio(styleProvider, tickSFX, stylerPreset));
        }

        void NotifyTimeChanged()
        {
            TimeSpan time = SelectedTime;
            if (hasNotified && time == lastNotifiedTime)
                return;

            hasNotified = true;
            lastNotifiedTime = time;
            onTimeChanged?.Invoke(time);
        }
        #endregion

        #region Helpers
        static string[] GetMinuteLabels(int step)
        {
            if (step == 1)
                return TwoDigitLabels;

            string[] labels = MinuteLabels[step];
            if (labels != null)
                return labels;

            labels = MinuteLabels[step] = new string[60 / step];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = TwoDigitLabels[i * step];

            return labels;
        }

        string GetPeriodLabel(bool isPM)
        {
            string label = isPM ? pmLabel : amLabel;
#if EVO_LOCALIZATION
            string key = isPM ? pmKey : amKey;
            if (Application.IsPlaying(gameObject) && enableLocalization && localizedObject != null && !string.IsNullOrEmpty(key))
                label = localizedObject.GetString(key) ?? label;
#endif
            return label ?? string.Empty;
        }

        int FloorToStep(int value) => value - value % Step;

        static int FloorDivide(int value, int divisor) => (int)Math.Floor((double)value / divisor);

        static bool UsesSystem24Hour()
        {
            string pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            return pattern.IndexOf('H') >= 0;
        }

        static string[] CreateLabels(int count, string format)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++)
                labels[i] = i.ToString(format, CultureInfo.InvariantCulture);

            return labels;
        }

        static string[] CreateHour12Labels()
        {
            var labels = new string[12];
            labels[0] = "12";
            for (int i = 1; i < labels.Length; i++)
                labels[i] = i.ToString(CultureInfo.InvariantCulture);

            return labels;
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf == active)
                return;

            target.SetActive(active);
#if UNITY_EDITOR
            if (!Application.IsPlaying(target) && !UnityEditor.EditorUtility.IsPersistent(target))
            {
                UnityEditor.EditorUtility.SetDirty(target);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
#endif
        }

        void OnSettingsChanged()
        {
            if (Application.IsPlaying(gameObject))
            {
                if (!isInitialized)
                    return;

                minute = FloorToStep(minute);
                BindWheels();
                ApplyAll();
                NotifyTimeChanged();
            }
#if UNITY_EDITOR
            else { RequestEditorRefresh(); }
#endif
        }
        #endregion

        #region Public API
        /// <summary>
        /// Selected time of day. Setting it animates the wheels and invokes events on change.
        /// </summary>
        public TimeSpan SelectedTime
        {
            get => new(hour, minute, 0);
            set => SetTime(value);
        }

        /// <summary>
        /// Selected hour from 0 to 23.
        /// </summary>
        public int Hour => hour;

        public int Minute => minute;

        public bool Is24Hour => hourFormat == HourFormat.TwentyFourHour || (hourFormat == HourFormat.Auto && UsesSystem24Hour());

        public HourFormat Format
        {
            get => hourFormat;
            set
            {
                if (hourFormat == value)
                    return;

                hourFormat = value;
                OnSettingsChanged();
            }
        }

        public MinuteInterval Interval
        {
            get => minuteInterval;
            set
            {
                if (minuteInterval == value)
                    return;

                minuteInterval = value;
                OnSettingsChanged();
            }
        }

        public float RowHeight
        {
            get => rowHeight;
            set
            {
                rowHeight = Mathf.Max(1f, value);
                OnSettingsChanged();
            }
        }

        /// <summary>
        /// Sets the time from an hour of 0 to 23 and a minute, which rounds down to the minute interval.
        /// Events run only when the time changes.
        /// </summary>
        public void SetTime(int hour, int minute, bool animated = true, bool invokeEvents = true)
        {
            bool playing = Application.IsPlaying(gameObject);
            if (!isInitialized && playing)
                Initialize();

            this.hour = (hour % 24 + 24) % 24;
            this.minute = FloorToStep(Mathf.Clamp(minute, 0, 59));
            SyncWheels(animated && playing && isActiveAndEnabled);

            if (invokeEvents && playing) { NotifyTimeChanged(); }
            else
            {
                hasNotified = true;
                lastNotifiedTime = SelectedTime;
            }
        }

        /// <summary>
        /// Sets the time from a time of day. Days and seconds are ignored.
        /// </summary>
        public void SetTime(TimeSpan time, bool animated = true, bool invokeEvents = true)
        {
            long ticks = time.Ticks % TimeSpan.TicksPerDay;
            var day = new TimeSpan(ticks < 0 ? ticks + TimeSpan.TicksPerDay : ticks);
            SetTime(day.Hours, day.Minutes, animated, invokeEvents);
        }

        public void SetTime(DateTime dateTime, bool animated = true, bool invokeEvents = true)
        {
            SetTime(dateTime.Hour, dateTime.Minute, animated, invokeEvents);
        }

        public void SetToCurrentTime(bool animated = true, bool invokeEvents = true) => SetTime(DateTime.Now, animated, invokeEvents);

        public void SetPeriodLabels(string am, string pm)
        {
            amLabel = am;
            pmLabel = pm;
            OnSettingsChanged();
        }

        /// <summary>
        /// Rebinds the wheels and redraws them. Call after replacing wheel references at runtime.
        /// </summary>
        public void Refresh() => OnSettingsChanged();
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!Application.IsPlaying(gameObject) || !isInitialized)
                return;

            ApplyFormat();
        }
#endif

#if UNITY_EDITOR
        bool refreshPending;

        void OnValidate()
        {
            rowHeight = Mathf.Max(1f, rowHeight);
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            RequestEditorRefresh();
        }

        void Reset()
        {
            useDefaultStylerPreset = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }

        void RequestEditorRefresh()
        {
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshEditor;
        }

        void CancelEditorRefresh()
        {
            if (!refreshPending)
                return;

            refreshPending = false;
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
        }

        void RefreshEditor()
        {
            refreshPending = false;

            if (this == null || !isActiveAndEnabled || !CanPreview())
                return;

            if (IsEditorBusy)
            {
                RequestEditorRefresh();
                return;
            }

            if (Application.IsPlaying(gameObject))
            {
                OnSettingsChanged();
                return;
            }

            ApplyEditorPreview();
        }

        bool CanPreview() => !UnityEditor.EditorUtility.IsPersistent(this) && gameObject.scene.IsValid();

        bool CanPreviewNow() => CanPreview() && !IsEditorBusy;

        static bool IsEditorBusy => UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;

        void ApplyEditorPreview()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Preview without reading the clock, so opening a scene never modifies it
            bool custom = initialTimeMode == InitialTimeMode.Custom;
            hour = custom ? Mathf.Clamp(customHour, 0, 23) : PreviewHour;
            minute = FloorToStep(custom ? Mathf.Clamp(customMinute, 0, 59) : PreviewMinute);
            ApplyAll();
        }
#endif
    }
}