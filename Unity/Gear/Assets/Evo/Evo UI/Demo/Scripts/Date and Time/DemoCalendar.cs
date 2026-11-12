using System;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Calendar tab: the view segment follows the calendar, whose header and grids change the view too.
    /// Future only limits the dates, and Go to today turns itself off once the calendar gets there.
    /// The other options set the calendar's properties directly.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCalendar : MonoBehaviour
    {
        [SerializeField] private Calendar calendar;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup viewOption;
        [SerializeField] private Switch todayOption;

        static readonly DateTime FirstDate = new(1900, 1, 1);

        Calendar.DisplayMode shownMode;
        float todayOffAt = -1;

        // A picked date apart from today, in the same month, so both the selection and the today highlight show
        void Start()
        {
            DateTime today = DateTime.Today;
            int offset = today.Day + 3 <= DateTime.DaysInMonth(today.Year, today.Month) ? 3 : -3;
            calendar.SelectDate(today.AddDays(offset), false);
        }

        void Update()
        {
            if (calendar.CurrentDisplayMode != shownMode)
            {
                shownMode = calendar.CurrentDisplayMode;
                DemoUtils.Show(viewOption, (int)shownMode);
            }

            if (todayOffAt >= 0 && Time.unscaledTime >= todayOffAt)
            {
                todayOffAt = -1;
                DemoUtils.Show(todayOption, false);
            }
        }

        // Days, Months or Years
        public void SetView(int index) => calendar.SetDisplayMode((Calendar.DisplayMode)index);

        public void SetFutureOnly(bool value)
        {
            calendar.SetMinDate(value ? DateTime.Today : FirstDate);

            if (value && calendar.GetSelectedDate() < DateTime.Today)
                calendar.SelectDate(DateTime.Today);
        }

        // An action rather than a setting: the tile stays on while the calendar travels, then it's ready for the next click
        public void GoToToday(bool value)
        {
            if (!value)
                return;

            calendar.GoToToday();
            todayOffAt = Time.unscaledTime + Mathf.Max(calendar.TransitionDuration, 0.25f);
        }
    }
}
