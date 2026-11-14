using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 时间选择：时 / 分（可选秒、AM/PM）多列滚轮，支持 12/24 小时制与格式化文本。
    /// <para>
    /// 事件统一给「当天秒数」（hour * 3600 + minute * 60 + second），
    /// 需要时再拆成 <see cref="Hour"/> / <see cref="Minute"/> / <see cref="Second"/>。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Time Picker")]
    [DisallowMultipleComponent]
    public sealed class GearTimePicker : MonoBehaviour
    {
        private static readonly string[] MeridiemLabels = { "AM", "PM" };

        [SerializeField] private GearPickerWheel hourWheel;
        [SerializeField] private GearPickerWheel minuteWheel;
        [SerializeField] private GearPickerWheel secondWheel;
        [SerializeField] private GearPickerWheel meridiemWheel;
        [SerializeField] private TMP_Text label;
        [SerializeField] private bool use24Hour = true;
        [SerializeField] private bool showSeconds;
        [SerializeField, Range(0, 23)] private int hour = 12;
        [SerializeField, Range(0, 59)] private int minute;
        [SerializeField, Range(0, 59)] private int second;
        [SerializeField] private string format = "HH:mm";
        [SerializeField] private UnityEvent<int> onTimeChanged = new UnityEvent<int>();

        private bool bound;
        private bool refreshing;

        public UnityEvent<int> OnTimeChanged => onTimeChanged;
        public event Action<int, int, int> TimeChanged;

        public int Hour => hour;
        public int Minute => minute;
        public int Second => second;
        public bool IsPm => hour >= 12;
        public int TotalSeconds => hour * 3600 + minute * 60 + second;

        /// <summary>按 <see cref="Format"/> 渲染出的文本。</summary>
        public string Text => FormatTime();

        public TMP_Text Label
        {
            get => label;
            set { label = value; RefreshLabel(); }
        }

        public bool Use24Hour
        {
            get => use24Hour;
            set
            {
                if (use24Hour == value) return;
                use24Hour = value;
                RefreshWheels();
            }
        }

        public bool ShowSeconds
        {
            get => showSeconds;
            set
            {
                if (showSeconds == value) return;
                showSeconds = value;
                RefreshWheels();
            }
        }

        /// <summary>格式串：HH/H 时、hh/h 12 小时、mm/m 分、ss/s 秒、tt/t AM/PM。</summary>
        public string Format
        {
            get => format;
            set { format = string.IsNullOrEmpty(value) ? "HH:mm" : value; RefreshLabel(); }
        }

        public GearPickerWheel HourWheel
        {
            get => hourWheel;
            set { Unbind(); hourWheel = value; Bind(); RefreshWheels(); }
        }

        public GearPickerWheel MinuteWheel
        {
            get => minuteWheel;
            set { Unbind(); minuteWheel = value; Bind(); RefreshWheels(); }
        }

        public GearPickerWheel SecondWheel
        {
            get => secondWheel;
            set { Unbind(); secondWheel = value; Bind(); RefreshWheels(); }
        }

        public GearPickerWheel MeridiemWheel
        {
            get => meridiemWheel;
            set { Unbind(); meridiemWheel = value; Bind(); RefreshWheels(); }
        }

        private void OnEnable()
        {
            Bind();
            RefreshWheels();
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>写入时间并同步滚轮；notify=false 时不派发事件。</summary>
        public void SetTime(int newHour, int newMinute, int newSecond = 0, bool notify = true)
        {
            ApplyTime(newHour, newMinute, newSecond, notify);
        }

        public void SetHour(int value) => ApplyTime(value, minute, second, true);
        public void SetMinute(int value) => ApplyTime(hour, value, second, true);
        public void SetSecond(int value) => ApplyTime(hour, minute, value, true);
        public void SetTimeWithoutNotify(int h, int m, int s = 0) => ApplyTime(h, m, s, false);

        /// <summary>当前时间按格式串渲染。</summary>
        public string FormatTime()
        {
            if (string.IsNullOrEmpty(format)) return string.Empty;

            var builder = new System.Text.StringBuilder(format.Length + 4);
            for (int i = 0; i < format.Length; i++)
            {
                char current = format[i];
                char next = i + 1 < format.Length ? format[i + 1] : '\0';
                switch (current)
                {
                    case 'H':
                        if (next == 'H') { builder.Append(hour.ToString("00")); i++; }
                        else builder.Append(hour);
                        break;
                    case 'h':
                        int display = DisplayHour();
                        if (next == 'h') { builder.Append(display.ToString("00")); i++; }
                        else builder.Append(display);
                        break;
                    case 'm':
                        if (next == 'm') { builder.Append(minute.ToString("00")); i++; }
                        else builder.Append(minute);
                        break;
                    case 's':
                        if (next == 's') { builder.Append(second.ToString("00")); i++; }
                        else builder.Append(second);
                        break;
                    case 't':
                        if (next == 't') { builder.Append(IsPm ? "PM" : "AM"); i++; }
                        else builder.Append(IsPm ? 'P' : 'A');
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }

            return builder.ToString();
        }

        // ---------------- 内部 ----------------

        private int DisplayHour()
        {
            if (use24Hour) return hour;
            int display = hour % 12;
            return display == 0 ? 12 : display;
        }

        private int DisplayHourIndex()
        {
            return use24Hour ? Mathf.Clamp(hour, 0, 23) : DisplayHour() - 1;
        }

        private void ApplyTime(int h, int m, int s, bool notify)
        {
            h = Mathf.Clamp(h, 0, 23);
            m = Mathf.Clamp(m, 0, 59);
            s = Mathf.Clamp(s, 0, 59);

            bool changed = h != hour || m != minute || s != second;
            hour = h;
            minute = m;
            second = s;

            SyncWheels();
            RefreshLabel();

            if (!changed || !notify) return;
            onTimeChanged?.Invoke(TotalSeconds);
            TimeChanged?.Invoke(hour, minute, second);
        }

        private void Bind()
        {
            if (bound) return;
            if (hourWheel != null) hourWheel.ValueChanged += OnHourWheelChanged;
            if (minuteWheel != null) minuteWheel.ValueChanged += OnMinuteWheelChanged;
            if (secondWheel != null) secondWheel.ValueChanged += OnSecondWheelChanged;
            if (meridiemWheel != null) meridiemWheel.ValueChanged += OnMeridiemWheelChanged;
            bound = true;
        }

        private void Unbind()
        {
            if (!bound) return;
            if (hourWheel != null) hourWheel.ValueChanged -= OnHourWheelChanged;
            if (minuteWheel != null) minuteWheel.ValueChanged -= OnMinuteWheelChanged;
            if (secondWheel != null) secondWheel.ValueChanged -= OnSecondWheelChanged;
            if (meridiemWheel != null) meridiemWheel.ValueChanged -= OnMeridiemWheelChanged;
            bound = false;
        }

        private void OnHourWheelChanged(int index)
        {
            if (refreshing) return;
            int h = use24Hour
                ? Mathf.Clamp(index, 0, 23)
                : (Mathf.Clamp(index, 0, 11) + 1) % 12 + (IsPm ? 12 : 0);
            ApplyTime(h, minute, second, true);
        }

        private void OnMinuteWheelChanged(int index) => ApplyTime(hour, index, second, true);
        private void OnSecondWheelChanged(int index) => ApplyTime(hour, minute, index, true);

        private void OnMeridiemWheelChanged(int index)
        {
            if (refreshing) return;
            int meridiem = Mathf.Clamp(index, 0, 1);
            ApplyTime(hour % 12 + (meridiem == 1 ? 12 : 0), minute, second, true);
        }

        private void RefreshWheels()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (hourWheel != null)
                {
                    hourWheel.SetOptions(BuildHourOptions(), false);
                    hourWheel.SnapTo(DisplayHourIndex());
                }

                if (minuteWheel != null)
                {
                    minuteWheel.SetOptions(BuildPadded(60), false);
                    minuteWheel.SnapTo(minute);
                }

                if (secondWheel != null)
                {
                    secondWheel.gameObject.SetActive(showSeconds);
                    secondWheel.SetOptions(BuildPadded(60), false);
                    secondWheel.SnapTo(second);
                }

                if (meridiemWheel != null)
                {
                    meridiemWheel.gameObject.SetActive(!use24Hour);
                    meridiemWheel.SetOptions(MeridiemLabels, false);
                    meridiemWheel.SnapTo(IsPm ? 1 : 0);
                }

                RefreshLabel();
            }
            finally
            {
                refreshing = false;
            }
        }

        private void SyncWheels()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (hourWheel != null && hourWheel.Count > 0) hourWheel.SnapTo(DisplayHourIndex());
                if (minuteWheel != null && minuteWheel.Count > 0) minuteWheel.SnapTo(minute);
                if (secondWheel != null && secondWheel.Count > 0) secondWheel.SnapTo(second);
                if (meridiemWheel != null && meridiemWheel.Count > 0) meridiemWheel.SnapTo(IsPm ? 1 : 0);
            }
            finally
            {
                refreshing = false;
            }
        }

        private void RefreshLabel()
        {
            if (label != null) label.text = FormatTime();
        }

        private string[] BuildHourOptions()
        {
            int count = use24Hour ? 24 : 12;
            var items = new string[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = use24Hour ? i.ToString("00") : (i + 1).ToString("00");
            }

            return items;
        }

        private static string[] BuildPadded(int count)
        {
            var items = new string[count];
            for (int i = 0; i < count; i++) items[i] = i.ToString("00");
            return items;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            hour = Mathf.Clamp(hour, 0, 23);
            minute = Mathf.Clamp(minute, 0, 59);
            second = Mathf.Clamp(second, 0, 59);
            if (string.IsNullOrEmpty(format)) format = "HH:mm";
            RefreshLabel();
        }
#endif
    }
}
