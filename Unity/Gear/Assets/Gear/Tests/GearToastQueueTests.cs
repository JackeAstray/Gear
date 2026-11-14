using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Tests
{
    /// <summary>
    /// GearToast 队列的 EditMode 测试：超过 MaxVisible 的 Toast 先隐藏，空出槽位后再显示。
    /// </summary>
    public class GearToastQueueTests
    {
        private int previousMax;

        [SetUp]
        public void SetUp()
        {
            previousMax = GearToast.MaxVisible;
            GearToast.MaxVisible = 2;
        }

        [TearDown]
        public void TearDown()
        {
            GearToast.ClearAll();
            GearToast.MaxVisible = previousMax;

            GameObject canvas = GameObject.Find("GearOverlayCanvas");
            if (canvas != null)
                Object.DestroyImmediate(canvas);
        }

        [Test]
        public void ThirdToast_WaitsUntilSlotFrees()
        {
            GearToastView first = GearToast.Show("1", duration: 10f);
            GearToastView second = GearToast.Show("2", duration: 10f);
            GearToastView third = GearToast.Show("3", duration: 10f);

            Assert.IsTrue(first.gameObject.activeSelf);
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.IsFalse(third.gameObject.activeSelf);

            first.Close();
            Assert.IsTrue(third.gameObject.activeSelf);
        }
    }
}
