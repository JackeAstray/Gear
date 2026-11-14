using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>GearInkMask 的覆盖度数学（与 shader 同式）。</summary>
    public class GearInkMaskTests
    {
        private const float Aspect = 1f;

        [Test]
        public void ProgressZero_HidesFarCorner()
        {
            float alpha = GearInkMask.ComputeCoverage(new Vector2(0.95f, 0.95f), 0f,
                new Vector2(0.5f, 0.5f), 0.08f, 0.35f, Aspect);

            Assert.Less(alpha, 0.05f);
        }

        [Test]
        public void ProgressOne_RevealsCorners()
        {
            float alpha = GearInkMask.ComputeCoverage(new Vector2(0f, 1f), 1f,
                new Vector2(0f, 0f), 0.08f, 0f, Aspect);

            Assert.AreEqual(1f, alpha, 0.01f, "进度 1 时四角也应完全显示");
        }

        [Test]
        public void OriginMovesBlob()
        {
            Vector2 origin = new Vector2(0f, 0f);
            float nearOrigin = GearInkMask.ComputeCoverage(new Vector2(0.02f, 0.02f), 0.4f, origin, 0.08f, 0f, Aspect);
            float farCorner = GearInkMask.ComputeCoverage(new Vector2(1f, 1f), 0.4f, origin, 0.08f, 0f, Aspect);

            Assert.AreEqual(1f, nearOrigin, 0.01f);
            Assert.Less(farCorner, 0.05f);
        }

        [Test]
        public void CoverageGrowsMonotonicallyWithProgress()
        {
            Vector2 point = new Vector2(0.8f, 0.5f);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            float previous = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float alpha = GearInkMask.ComputeCoverage(point, i / 10f, origin, 0.05f, 0f, Aspect);
                Assert.GreaterOrEqual(alpha, previous - 0.0001f);
                previous = alpha;
            }

            Assert.AreEqual(1f, previous, 0.01f);
        }

        [Test]
        public void WiderAspectStretchesDistanceHorizontally()
        {
            Vector2 point = new Vector2(0.9f, 0.5f);
            Vector2 origin = new Vector2(0.5f, 0.5f);
            float square = GearInkMask.ComputeCoverage(point, 0.3f, origin, 0.05f, 0f, 1f);
            float wide = GearInkMask.ComputeCoverage(point, 0.3f, origin, 0.05f, 0f, 2f);

            Assert.Less(wide, square, "宽画布下同一 uv 的水平距离更远，覆盖更少");
        }
    }
}
