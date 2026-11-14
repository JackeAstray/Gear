using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearOffScreenIndicator 的边缘数学与朝向。</summary>
    public class GearOffScreenIndicatorTests
    {
        private static readonly Vector2 HalfSize = new Vector2(400f, 300f);
        private static readonly Vector2 Padding = new Vector2(40f, 40f);

        [Test]
        public void InsideRect_IsNotClamped()
        {
            Vector2 point = new Vector2(100f, -80f);
            Vector2 result = GearOffScreenIndicator.ClampToEdge(point, HalfSize, Padding, out bool outside);

            Assert.IsFalse(outside);
            Assert.AreEqual(point, result);
        }

        [Test]
        public void OutsideRight_ProjectsToRightEdge()
        {
            Vector2 result = GearOffScreenIndicator.ClampToEdge(new Vector2(1000f, 0f), HalfSize, Padding,
                out bool outside);

            Assert.IsTrue(outside);
            Assert.AreEqual(360f, result.x, 0.01f, "半宽 400 - padding 40");
            Assert.AreEqual(0f, result.y, 0.01f);
        }

        [Test]
        public void Diagonal_IsClampedToNearestEdge()
        {
            Vector2 result = GearOffScreenIndicator.ClampToEdge(new Vector2(900f, 200f), HalfSize, Padding,
                out bool outside);

            Assert.IsTrue(outside);
            Assert.LessOrEqual(Mathf.Abs(result.x), 360.01f);
            Assert.LessOrEqual(Mathf.Abs(result.y), 260.01f);
            // 对角线方向保持：x/y 比例不变
            Assert.AreEqual(900f / 200f, result.x / result.y, 0.05f);
        }

        [Test]
        public void PaddingIsClampedToPositive()
        {
            Vector2 result = GearOffScreenIndicator.ClampToEdge(new Vector2(0f, -500f), HalfSize,
                new Vector2(-50f, -50f), out bool outside);

            Assert.IsTrue(outside);
            Assert.AreEqual(-300f, result.y, 0.01f, "负 padding 按 0 处理");
        }

        [Test]
        public void AngleFor_UpIsZero()
        {
            Assert.AreEqual(0f, GearOffScreenIndicator.AngleFor(Vector2.up), 0.01f);
            Assert.AreEqual(-90f, GearOffScreenIndicator.AngleFor(Vector2.right), 0.01f);
            Assert.AreEqual(-180f, GearOffScreenIndicator.AngleFor(Vector2.down), 0.01f);
            Assert.AreEqual(90f, GearOffScreenIndicator.AngleFor(Vector2.left), 0.01f);
        }
    }
}
