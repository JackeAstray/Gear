using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearTimer 的 EditMode 用例：暂停/继续、倒计时完成、循环、格式化。</summary>
    public class GearTimerWidgetTests
    {
        private GameObject go;
        private GearTimer timer;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Timer", typeof(RectTransform), typeof(GearTimer));
            timer = go.GetComponent<GearTimer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Countdown_CompletesOnceAndStops()
        {
            timer.Configure(GearTimerMode.Countdown, 5f, false);
            int completed = 0;
            timer.OnCompleted.AddListener(() => completed++);

            timer.Play();
            timer.Tick(4f);
            Assert.IsTrue(timer.IsRunning);
            Assert.AreEqual(1f, timer.Remaining, 0.001f);

            timer.Tick(2f);
            Assert.IsFalse(timer.IsRunning, "到点后应自动停止");
            Assert.AreEqual(0f, timer.Remaining, 0.001f);
            Assert.AreEqual(1, completed);

            timer.Tick(1f);
            Assert.AreEqual(1, completed, "完成事件只派发一次");
        }

        [Test]
        public void PauseAndResume_KeepProgress()
        {
            timer.Configure(GearTimerMode.Stopwatch, 0f, false);
            timer.Play();
            timer.Tick(2f);
            timer.Pause();
            timer.Tick(5f);

            Assert.AreEqual(2f, timer.Elapsed, 0.001f, "暂停期间不累计");
            Assert.IsFalse(timer.IsRunning);

            timer.Play();
            timer.Tick(1f);
            Assert.AreEqual(3f, timer.Elapsed, 0.001f);
        }

        [Test]
        public void Stopwatch_CountsUpAndReportsProgress()
        {
            timer.Configure(GearTimerMode.Stopwatch, 10f, false);
            timer.Play();
            timer.Tick(2.5f);

            Assert.AreEqual(2.5f, timer.Elapsed, 0.001f);
            Assert.AreEqual(2.5f, timer.Remaining, 0.001f, "秒表的 Remaining 即已用时间");
            Assert.AreEqual(0.25f, timer.Progress, 0.001f);
        }

        [Test]
        public void ResetTimer_ReturnsToStart()
        {
            timer.Configure(GearTimerMode.Countdown, 30f, true);
            timer.Tick(10f);
            Assert.AreEqual(20f, timer.Remaining, 0.001f);

            timer.ResetTimer();
            Assert.IsFalse(timer.IsRunning);
            Assert.AreEqual(30f, timer.Remaining, 0.001f);
        }

        [Test]
        public void Loop_RestartsAfterCompletion()
        {
            timer.Configure(GearTimerMode.Countdown, 2f, false);
            timer.Loop = true;
            int completed = 0;
            timer.OnCompleted.AddListener(() => completed++);

            timer.Play();
            timer.Tick(2f);

            Assert.AreEqual(1, completed);
            Assert.IsTrue(timer.IsRunning, "循环模式应自动继续");
            Assert.AreEqual(0f, timer.Elapsed, 0.001f);
        }

        [Test]
        public void FormatTime_SupportsMinutesSecondsAndHundredths()
        {
            timer.Format = "mm:ss";
            Assert.AreEqual("01:05", timer.FormatTime(65f));

            timer.Format = "HH:mm:ss";
            Assert.AreEqual("00:01:05", timer.FormatTime(65f));

            timer.Format = "m:ss.ff";
            Assert.AreEqual("1:05.50", timer.FormatTime(65.5f));

            timer.Format = "ss";
            Assert.AreEqual("05", timer.FormatTime(65f));
        }

        [Test]
        public void SetSeconds_ClampsToDuration()
        {
            timer.Configure(GearTimerMode.Countdown, 10f, false);
            timer.SetSeconds(4f);
            Assert.AreEqual(4f, timer.Remaining, 0.001f);

            timer.SetSeconds(-3f);
            Assert.AreEqual(0f, timer.Remaining, 0.001f, "负数按 0 秒剩余处理");

            timer.SetSeconds(99f);
            Assert.AreEqual(10f, timer.Remaining, 0.001f, "超过时长按满时长处理");
        }
    }
}
