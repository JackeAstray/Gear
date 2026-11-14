using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearCalendar 的月网格、周偏移、翻月与选中逻辑。</summary>
    public class GearCalendarTests
    {
        private GameObject go;
        private GearCalendar calendar;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Calendar", typeof(RectTransform), typeof(GearCalendar));
            calendar = go.GetComponent<GearCalendar>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BuildMonthGrid_SundayStart_November2026()
        {
            calendar.SetVisibleMonth(2026, 11);
            List<GearCalendarDay> days = calendar.BuildMonthGrid(new DateTime(2026, 11, 14));

            Assert.AreEqual(35, days.Count, "30 天且 1 号是周日 → 5 行");
            Assert.AreEqual(new DateTime(2026, 11, 1), days[0].Date);
            Assert.IsTrue(days[0].InMonth);
            Assert.AreEqual(new DateTime(2026, 11, 30), days[29].Date);
            Assert.AreEqual(new DateTime(2026, 12, 1), days[30].Date);
            Assert.IsFalse(days[30].InMonth, "尾部补的是下个月");
            Assert.IsTrue(days[13].IsToday, "11-14 应标记今天");
        }

        [Test]
        public void BuildMonthGrid_MondayStart_OffsetsLeadingDays()
        {
            calendar.WeekStart = GearWeekStart.Monday;
            calendar.SetVisibleMonth(2026, 11);
            List<GearCalendarDay> days = calendar.BuildMonthGrid(new DateTime(2026, 11, 14));

            Assert.AreEqual(42, days.Count, "周一开头需要 6 行");
            Assert.AreEqual(new DateTime(2026, 10, 26), days[0].Date, "首格回退到周一");
            Assert.AreEqual(6, days.FindIndex(d => d.Date.Day == 1 && d.InMonth));
        }

        [Test]
        public void MonthNavigation_RollsOverYear()
        {
            calendar.SetVisibleMonth(2026, 1);
            calendar.PreviousMonth();
            Assert.AreEqual(2025, calendar.Year);
            Assert.AreEqual(12, calendar.Month);

            calendar.NextMonth();
            Assert.AreEqual(2026, calendar.Year);
            Assert.AreEqual(1, calendar.Month);
        }

        [Test]
        public void SelectDate_SyncsMonthAndRaisesIso()
        {
            string captured = null;
            calendar.OnDateSelected.AddListener(value => captured = value);

            Assert.IsTrue(calendar.SelectDate(new DateTime(2027, 3, 15)));

            Assert.AreEqual(2027, calendar.Year);
            Assert.AreEqual(3, calendar.Month);
            Assert.AreEqual(new DateTime(2027, 3, 15), calendar.SelectedDate);
            Assert.AreEqual("2027-03-15", captured);
        }

        [Test]
        public void SelectDay_RejectsOutOfRange()
        {
            calendar.SetVisibleMonth(2026, 11);

            Assert.IsFalse(calendar.SelectDay(31), "11 月只有 30 天");
            Assert.IsTrue(calendar.SelectDay(30));
            Assert.AreEqual(30, calendar.SelectedDate.Value.Day);
        }

        [Test]
        public void GridMarksSelectedAndToday()
        {
            calendar.SetVisibleMonth(2026, 11);
            calendar.SelectDate(new DateTime(2026, 11, 14), false);
            List<GearCalendarDay> days = calendar.BuildMonthGrid(new DateTime(2026, 11, 14));

            GearCalendarDay cell = days.Find(d => d.Date.Day == 14);
            Assert.IsTrue(cell.IsToday);
            Assert.IsTrue(cell.IsSelected);
            Assert.IsFalse(days.Find(d => d.Date.Day == 15).IsSelected);
        }

        [Test]
        public void ClearSelection_RemovesFlag()
        {
            calendar.SelectDate(new DateTime(2026, 11, 14), false);
            calendar.ClearSelection();
            Assert.IsNull(calendar.SelectedDate);
        }
    }
}
