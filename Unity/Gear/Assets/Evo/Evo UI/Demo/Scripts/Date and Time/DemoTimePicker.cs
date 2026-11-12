using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Time Picker tab: hour format, minute steps and tick sounds, plus a Set to now tile that turns itself off when the wheels stop.
    /// Row height and the wheel bars set the picker's and wheels' properties directly.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoTimePicker : MonoBehaviour
    {
        [SerializeField] private TimePicker picker;

        [Header("Options")]
        [SerializeField] private Switch nowOption;
        [SerializeField] private TimePicker.MinuteInterval[] intervals =
        {
            TimePicker.MinuteInterval.One, TimePicker.MinuteInterval.Five, TimePicker.MinuteInterval.Fifteen, TimePicker.MinuteInterval.Thirty
        };

        float nowOffAt = -1;

        void Update()
        {
            if (nowOffAt < 0 || Time.unscaledTime < nowOffAt || picker.hourWheel.IsMoving || picker.minuteWheel.IsMoving || picker.periodWheel.IsMoving)
                return;

            nowOffAt = -1;
            DemoUtils.Show(nowOption, false);
        }

        // 12-hour or 24-hour
        public void SetFormat(int index) => picker.Format = index == 0 ? TimePicker.HourFormat.TwelveHour : TimePicker.HourFormat.TwentyFourHour;

        public void SetInterval(int index) => picker.Interval = intervals[index];

        public void SetSounds(bool value) => picker.soundEffects = value;

        // An action rather than a setting: the tile stays on while the wheels turn, then it's ready for the next click
        public void SetToNow(bool value)
        {
            if (!value)
                return;

            picker.SetToCurrentTime();
            nowOffAt = Time.unscaledTime + 0.25f;
        }
    }
}
