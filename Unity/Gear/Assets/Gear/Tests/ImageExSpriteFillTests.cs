using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// ImageEx Sprite Fill 的纯 UV 换算测试（EditMode，不依赖 TMP/Canvas）。
    /// </summary>
    public class ImageExSpriteFillTests
    {
        private static void AssertVector(Vector4 expected, Vector4 actual, float tolerance = 0.0001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance);
            Assert.AreEqual(expected.y, actual.y, tolerance);
            Assert.AreEqual(expected.z, actual.z, tolerance);
            Assert.AreEqual(expected.w, actual.w, tolerance);
        }

        [Test]
        public void Cover_WideRect_CropsVertically()
        {
            // 精灵 100×100，矩形 200×100（更宽）→ 上下各裁 1/4
            Vector4 uv = ImageHelper.ApplySpriteFill(
                new Vector4(0f, 0f, 1f, 1f), new Vector2(100f, 100f), new Vector2(200f, 100f),
                SpriteFillMode.Cover);

            AssertVector(new Vector4(0f, 0.25f, 1f, 0.75f), uv);
        }

        [Test]
        public void Cover_TallRect_CropsHorizontally()
        {
            // 矩形 100×200（更高）→ 左右各裁 1/4
            Vector4 uv = ImageHelper.ApplySpriteFill(
                new Vector4(0f, 0f, 1f, 1f), new Vector2(100f, 100f), new Vector2(100f, 200f),
                SpriteFillMode.Cover);

            AssertVector(new Vector4(0.25f, 0f, 0.75f, 1f), uv);
        }

        [Test]
        public void Cover_KeepsAtlasOffsetCentered()
        {
            // 图集子区域 (0.2,0.4)-(0.6,0.8)，矩形更高 → 宽度各裁 1/4，中心不变
            Vector4 uv = ImageHelper.ApplySpriteFill(
                new Vector4(0.2f, 0.4f, 0.6f, 0.8f), new Vector2(100f, 100f), new Vector2(100f, 200f),
                SpriteFillMode.Cover);

            AssertVector(new Vector4(0.3f, 0.4f, 0.5f, 0.8f), uv);
        }

        [Test]
        public void StretchAndFit_LeaveUvUntouched()
        {
            Vector4 source = new Vector4(0.1f, 0.2f, 0.7f, 0.9f);

            AssertVector(source, ImageHelper.ApplySpriteFill(
                source, new Vector2(100f, 50f), new Vector2(300f, 100f), SpriteFillMode.Stretch));
            AssertVector(source, ImageHelper.ApplySpriteFill(
                source, new Vector2(100f, 50f), new Vector2(300f, 100f), SpriteFillMode.Fit));
        }

        [Test]
        public void Cover_SameAspect_LeavesUvUntouched()
        {
            Vector4 source = new Vector4(0f, 0f, 1f, 1f);
            AssertVector(source, ImageHelper.ApplySpriteFill(
                source, new Vector2(200f, 100f), new Vector2(400f, 200f), SpriteFillMode.Cover));
        }
    }
}
