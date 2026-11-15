using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>
    /// Pack C 滚轮类控件：GearPickerWheel 的吸附/循环/边界，GearTimePicker 的 12/24 小时换算与格式。
    /// EditMode 下没有生命周期，统一显式调用 SetOptions / SnapTo / Tick。
    /// </summary>
    public class GearPickerWidgetTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PickerRoot", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private GearPickerWheel CreateWheel(string name, string[] labels, bool wrap = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(GearPickerWheel));
            go.transform.SetParent(root.transform, false);
            GearPickerWheel wheel = go.GetComponent<GearPickerWheel>();
            wheel.Wrap = wrap;
            wheel.SetOptions(labels, false);
            return wheel;
        }

        private static string[] Range(int count)
        {
            var items = new string[count];
            for (int i = 0; i < count; i++) items[i] = "Item " + i;
            return items;
        }

        // ---------------- PickerWheel ----------------

        [Test]
        public void PickerWheel_SetValueClampsAndNotifies()
        {
            GearPickerWheel wheel = CreateWheel("Wheel", new[] { "A", "B", "C" });
            int calls = 0;
            wheel.OnValueChanged.AddListener(_ => calls++);

            wheel.SetValue(2);
            Assert.AreEqual(2, wheel.Value);
            Assert.AreEqual(1, calls);

            wheel.SetValue(99);
            Assert.AreEqual(2, wheel.Value, "越界应钳到末项");
            Assert.AreEqual(1, calls, "值没变不应重复派发");

            wheel.SetValueWithoutNotify(0);
            Assert.AreEqual(0, wheel.Value);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void PickerWheel_DragUpdatesNearestValue()
        {
            GearPickerWheel wheel = CreateWheel("Wheel", Range(5));

            wheel.ApplyScrollDelta(45f);
            Assert.AreEqual(1, wheel.Value, "45px / 40px 应吸附到第 1 项");

            wheel.ApplyScrollDelta(-1000f);
            Assert.AreEqual(0, wheel.Value, "向上拖过头应停在首项");
            Assert.AreEqual(0f, wheel.Scroll, 0.001f);

            wheel.ApplyScrollDelta(1000f);
            Assert.AreEqual(4, wheel.Value, "向下拖过头应停在末项");
            Assert.AreEqual(160f, wheel.Scroll, 0.001f);
        }

        [Test]
        public void PickerWheel_ReleaseSnapsAndFiresSettledOnce()
        {
            GearPickerWheel wheel = CreateWheel("Wheel", Range(5));
            wheel.ApplyScrollDelta(52f);
            Assert.AreEqual(1, wheel.Value);

            int settledValue = -1;
            int settledCalls = 0;
            wheel.OnSettled.AddListener(v => { settledValue = v; settledCalls++; });

            wheel.ReleaseDrag(0f);
            for (int i = 0; i < 60 && !wheel.IsSettled; i++) wheel.Tick(0.05f);

            Assert.IsTrue(wheel.IsSettled);
            Assert.AreEqual(40f, wheel.Scroll, 0.01f);
            Assert.AreEqual(1, wheel.Value);
            Assert.AreEqual(1, settledValue);
            Assert.AreEqual(1, settledCalls);

            wheel.Tick(0.05f);
            Assert.AreEqual(1, settledCalls, "吸附完成后不应重复派发");
        }

        [Test]
        public void PickerWheel_WrapCyclesBothDirections()
        {
            GearPickerWheel wheel = CreateWheel("Wheel", Range(5), true);

            wheel.SetValue(7);
            Assert.AreEqual(2, wheel.Value);

            wheel.SetValue(0);
            wheel.Step(-1);
            Assert.AreEqual(4, wheel.Value);

            wheel.Step(1);
            Assert.AreEqual(0, wheel.Value);

            wheel.ApplyScrollDelta(-50f);
            Assert.AreEqual(4, wheel.Value, "循环模式下负偏移应绕回末项");
        }

        [Test]
        public void PickerWheel_WorksWithoutContentOrLabel()
        {
            GearPickerWheel wheel = CreateWheel("Wheel", new[] { "A", "B" });
            Assert.IsNull(wheel.Content);
            Assert.IsNull(wheel.SelectedLabel);

            wheel.SetValue(1);
            for (int i = 0; i < 30; i++) wheel.Tick(0.1f);

            Assert.AreEqual(1, wheel.Value);
            Assert.IsTrue(wheel.IsSettled);
            Assert.AreEqual(wheel.TargetScroll, wheel.Scroll, 0.01f);
        }

        // ---------------- TimePicker ----------------

        private GearTimePicker CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
            out GearPickerWheel meridiem)
        {
            GameObject go = new GameObject("TimePicker", typeof(RectTransform), typeof(GearTimePicker));
            go.transform.SetParent(root.transform, false);
            GearTimePicker picker = go.GetComponent<GearTimePicker>();

            hour = CreateWheel("Hour", Range(24));
            minute = CreateWheel("Minute", Range(60));
            meridiem = CreateWheel("Meridiem", new[] { "AM", "PM" });

            picker.HourWheel = hour;
            picker.MinuteWheel = minute;
            picker.MeridiemWheel = meridiem;
            return picker;
        }

        [Test]
        public void TimePicker_24HourSyncsWheelsAndFormats()
        {
            GearTimePicker picker = CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
                out GearPickerWheel meridiem);

            picker.SetTime(9, 5, 3);

            Assert.AreEqual(9, picker.Hour);
            Assert.AreEqual(9, hour.Value);
            Assert.AreEqual(5, minute.Value);
            Assert.AreEqual(0, meridiem.Value);
            Assert.AreEqual("09:05", picker.Text);

            picker.Format = "HH:mm:ss";
            Assert.AreEqual("09:05:03", picker.Text);
        }

        [Test]
        public void TimePicker_12HourMeridiemRoundTrip()
        {
            GearTimePicker picker = CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
                out GearPickerWheel meridiem);

            picker.Format = "hh:mm tt";
            picker.Use24Hour = false;

            picker.SetTime(15, 30, 0);
            Assert.IsTrue(picker.IsPm);
            Assert.AreEqual(2, hour.Value, "15 点应显示为 03（序号 2）");
            Assert.AreEqual(1, meridiem.Value);
            Assert.IsTrue(picker.Text.Contains("PM"), picker.Text);

            meridiem.SetValue(0, true);
            Assert.AreEqual(3, picker.Hour, "AM 应把 15 点回退到 3 点");
            Assert.IsFalse(picker.IsPm);

            hour.SetValue(4, true);
            Assert.AreEqual(5, picker.Hour, "12 小时制第 5 项 = 05 点");

            meridiem.SetValue(1, true);
            Assert.AreEqual(17, picker.Hour, "切到 PM 应为 17 点");
        }

        [Test]
        public void TimePicker_MinuteIntervalSnapsAndSteps()
        {
            GearTimePicker picker = CreateTimePicker(out _, out GearPickerWheel minute, out _);

            picker.MinuteInterval = 5;
            picker.SetTime(10, 17);

            Assert.AreEqual(15, picker.Minute);
            Assert.AreEqual("15", minute.Options[minute.Value]);

            minute.Step(1);
            Assert.AreEqual(20, picker.Minute);
        }

        [Test]
        public void TimePicker_CustomLabelsAndRowHeight()
        {
            GearTimePicker picker = CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
                out GearPickerWheel meridiem);
            picker.Use24Hour = false;
            picker.Format = "hh:mm tt";
            picker.AmLabel = "AM";
            picker.PmLabel = "PM";
            picker.RowHeight = 44f;

            picker.SetTime(15, 30);

            Assert.IsTrue(picker.Text.Contains("PM"), picker.Text);
            Assert.AreEqual(44f, hour.ItemHeight, 0.01f);
            Assert.AreEqual(44f, minute.ItemHeight, 0.01f);
            Assert.AreEqual(44f, meridiem.ItemHeight, 0.01f);
        }

        [Test]
        public void TimePicker_WrapsHourMinuteAndMeridiem()
        {
            GearTimePicker picker = CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
                out GearPickerWheel meridiem);
            picker.Use24Hour = false;
            picker.SetTime(23, 59, 0, false);

            Assert.AreEqual(10, hour.Value, "23 点应显示为 11 PM（序号 10）");
            Assert.AreEqual(1, meridiem.Value);

            minute.Step(1);
            Assert.AreEqual(0, picker.Minute, "59 分下一步应绕回 00");

            hour.Step(1);
            Assert.AreEqual(0, picker.Hour, "11 PM 下一步应绕到 12 AM");
            Assert.IsFalse(picker.IsPm);

            hour.Step(1);
            Assert.AreEqual(1, picker.Hour, "12 AM 下一步应到 1 AM");

            hour.Step(-1);
            Assert.AreEqual(0, picker.Hour);

            hour.Step(-1);
            Assert.AreEqual(23, picker.Hour, "12 AM 上一步应回到 11 PM");
            Assert.IsTrue(picker.IsPm);
            Assert.AreEqual(10, hour.Value);
        }

        [Test]
        public void TimePicker_LoopFalseStopsAtBoundary()
        {
            GearTimePicker picker = CreateTimePicker(out GearPickerWheel hour, out GearPickerWheel minute,
                out _);
            picker.Loop = false;
            picker.Use24Hour = true;
            picker.SetTime(23, 59, 0, false);

            hour.Step(1);
            minute.Step(1);

            Assert.AreEqual(23, picker.Hour);
            Assert.AreEqual(59, picker.Minute);
        }

        [Test]
        public void TimePicker_ClampsInputsAndDeduplicatesEvents()
        {
            GearTimePicker picker = CreateTimePicker(out _, out _, out _);

            int calls = 0;
            int total = -1;
            picker.OnTimeChanged.AddListener(value => { calls++; total = value; });

            picker.SetTime(10, 20, 30);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(10 * 3600 + 20 * 60 + 30, total);

            picker.SetTime(10, 20, 30);
            Assert.AreEqual(1, calls, "相同时间不应重复派发");

            picker.SetTime(25, 70, 99);
            Assert.AreEqual(23, picker.Hour);
            Assert.AreEqual(59, picker.Minute);
            Assert.AreEqual(59, picker.Second);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void TimePicker_FormatTokensHandleNoonAndMidnight()
        {
            GearTimePicker picker = CreateTimePicker(out _, out _, out _);
            picker.Use24Hour = false;
            picker.Format = "h:mm tt";

            picker.SetTime(0, 7, 0);
            Assert.AreEqual("12:07 AM", picker.Text);

            picker.SetTime(12, 0, 0);
            Assert.AreEqual("12:00 PM", picker.Text);

            picker.Format = "HH:mm";
            picker.SetTime(23, 59, 0);
            Assert.AreEqual("23:59", picker.Text);
        }
    }
}
