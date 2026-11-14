using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// ImageEx 形状命中与圆角百分比的纯几何测试（EditMode，不需要 Canvas / 渲染）。
    /// </summary>
    public class ImageExShapeHitTests
    {
        private GameObject go;
        private ImageEx image;
        private RectTransform rect;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ImageExHitTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200f, 100f);
            image = go.GetComponent<ImageEx>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void Rectangle_PercentageRadius_100_IsHalfShortEdge()
        {
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg
            {
                CornerRadius = new Vector4(100f, 100f, 100f, 100f),
                RadiusMode = CornerRadiusMode.Percentage
            };

            Vector4 radius = image.Rectangle.ResolveCornerRadius(new Vector2(200f, 100f));
            Assert.AreEqual(50f, radius.x, 0.01f);
            Assert.AreEqual(50f, radius.y, 0.01f);
            Assert.AreEqual(50f, radius.z, 0.01f);
            Assert.AreEqual(50f, radius.w, 0.01f);
        }

        [Test]
        public void Rectangle_ShapeHit_RejectsRoundedCorner()
        {
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg
            {
                CornerRadius = new Vector4(100f, 100f, 100f, 100f),
                RadiusMode = CornerRadiusMode.Percentage
            };

            Vector2 size = new Vector2(200f, 100f);
            Assert.IsTrue(ImageExShapeHitTest.Contains(image, new Vector2(100f, 50f), size), "中心应命中");
            Assert.IsFalse(ImageExShapeHitTest.Contains(image, new Vector2(1f, 1f), size), "左下圆角外不应命中");
            Assert.IsTrue(ImageExShapeHitTest.Contains(image, new Vector2(100f, 1f), size), "底边中部应命中");
        }

        [Test]
        public void Circle_ShapeHit_RejectsCorners()
        {
            image.DrawShape = DrawShape.Circle;
            image.Circle = new CircleImg { FitToRect = true };

            Vector2 size = new Vector2(200f, 100f);
            Assert.IsTrue(ImageExShapeHitTest.Contains(image, new Vector2(100f, 50f), size), "圆心应命中");
            Assert.IsFalse(ImageExShapeHitTest.Contains(image, new Vector2(1f, 1f), size), "圆外角不应命中");
        }

        [Test]
        public void RaycastMode_None_RejectsEverything()
        {
            image.RaycastMode = RaycastDetectionMode.None;
            Assert.IsFalse(image.IsRaycastLocationValid(Vector2.zero, null), "None 模式不应参与命中");
        }

        [Test]
        public void GradientScale_ClampsToShaderRange()
        {
            image.GradientScale = 10f;
            Assert.AreEqual(5f, image.GradientScale, 0.001f);
            image.GradientScale = 0f;
            Assert.AreEqual(0.1f, image.GradientScale, 0.001f);
        }

        [Test]
        public void ShapeSoftness_ClampsToShaderRange()
        {
            image.ShapeSoftness = 0f;
            Assert.AreEqual(0.001f, image.ShapeSoftness, 0.0001f);
            image.ShapeSoftness = 99f;
            Assert.AreEqual(8f, image.ShapeSoftness, 0.0001f);
        }

        [Test]
        public void Dither_Toggles()
        {
            image.Dither = true;
            Assert.IsTrue(image.Dither);
            image.Dither = false;
            Assert.IsFalse(image.Dither);
        }

        [Test]
        public void SoftMask_WritesMaskUniformsToChildImageEx()
        {
            GameObject root = new GameObject("SoftMaskRoot", typeof(RectTransform), typeof(GearSoftMask));
            GameObject child = new GameObject("SoftMaskChild", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            Material baseMat = null;
            try
            {
                child.transform.SetParent(root.transform, false);
                ImageEx masked = child.GetComponent<ImageEx>();
                masked.rectTransform.sizeDelta = new Vector2(100f, 50f);

                GearSoftMask mask = root.GetComponent<GearSoftMask>();
                mask.Softness = 12f;
                mask.Radius = 8f;

                baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
                Material modified = masked.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_GearMaskEnabled"));
                Assert.AreEqual(12f, modified.GetFloat("_GearMaskSoftness"), 0.01f);
            }
            finally
            {
                if (baseMat != null) Object.DestroyImmediate(baseMat);
                Object.DestroyImmediate(root);
            }
        }
    }
}
