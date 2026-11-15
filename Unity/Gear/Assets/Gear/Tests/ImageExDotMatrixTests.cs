using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// GearDotMatrix 与 ImageEx DotMatrix uniform 的 EditMode 测试。
    /// </summary>
    public class ImageExDotMatrixTests
    {
        private GameObject go;
        private ImageEx image;
        private GearDotMatrix dots;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("DotMatrixTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(GearDotMatrix));
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 120f);
            image = go.GetComponent<ImageEx>();
            dots = go.GetComponent<GearDotMatrix>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySettings_WritesImageProperties()
        {
            dots.Pattern = DotMatrixPattern.Radar;
            dots.Shape = DotMatrixShape.Diamond;
            dots.Cell = 24f;
            dots.Radius = 0.4f;
            dots.Intensity = 0.8f;
            dots.Speed = 2f;
            dots.InactiveAlpha = 0.15f;
            dots.InactiveScale = 0.4f;
            dots.EdgeSoftness = 0.2f;
            dots.PhaseOffset = 0.5f;
            dots.TrailLength = 2f;
            dots.ApplySettings();

            Assert.IsTrue(image.EnableDotMatrix);
            Assert.AreEqual(DotMatrixPattern.Radar, image.DotPattern);
            Assert.AreEqual(DotMatrixShape.Diamond, image.DotShape);
            Assert.AreEqual(24f, image.DotCell, 0.01f);
            Assert.AreEqual(0.4f, image.DotRadius, 0.001f);
            Assert.AreEqual(0.8f, image.DotIntensity, 0.001f);
            Assert.AreEqual(2f, image.DotSpeed, 0.001f);
            Assert.AreEqual(0.15f, image.DotInactiveAlpha, 0.001f);
            Assert.AreEqual(0.4f, image.DotInactiveScale, 0.001f);
            Assert.AreEqual(0.2f, image.DotEdgeSoftness, 0.001f);
            Assert.AreEqual(0.5f, image.DotPhaseOffset, 0.001f);
            Assert.AreEqual(2f, image.DotTrailLength, 0.001f);
        }

        [Test]
        public void Material_ReceivesDotMatrixUniforms()
        {
            dots.Pattern = DotMatrixPattern.Spiral;
            dots.Shape = DotMatrixShape.Square;
            dots.Cell = 20f;
            dots.Radius = 0.3f;
            dots.Intensity = 0.75f;
            dots.Speed = 1.5f;
            dots.InactiveAlpha = 0.15f;
            dots.InactiveScale = 0.4f;
            dots.EdgeSoftness = 0.2f;
            dots.PhaseOffset = 0.5f;
            dots.TrailLength = 2f;
            dots.ApplySettings();

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_EnableDotMatrix"));
                Assert.AreEqual((int)DotMatrixPattern.Spiral, modified.GetInt("_DotPattern"));
                Assert.AreEqual((int)DotMatrixShape.Square, modified.GetInt("_DotShape"));
                Assert.AreEqual(20f, modified.GetFloat("_DotCell"), 0.01f);
                Assert.AreEqual(0.3f, modified.GetFloat("_DotRadius"), 0.01f);
                Assert.AreEqual(0.75f, modified.GetFloat("_DotIntensity"), 0.01f);
                Assert.AreEqual(1.5f, modified.GetFloat("_DotSpeed"), 0.01f);
                Assert.AreEqual(0.15f, modified.GetFloat("_DotInactiveAlpha"), 0.01f);
                Assert.AreEqual(0.4f, modified.GetFloat("_DotInactiveScale"), 0.01f);
                Assert.AreEqual(0.2f, modified.GetFloat("_DotEdgeSoftness"), 0.01f);
                Assert.AreEqual(0.5f, modified.GetFloat("_DotPhaseOffset"), 0.01f);
                Assert.AreEqual(2f, modified.GetFloat("_DotTrailLength"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void Values_ClampToSupportedRanges()
        {
            image.DotCell = 1f;
            Assert.AreEqual(4f, image.DotCell, 0.001f);

            image.DotRadius = 0.9f;
            Assert.AreEqual(0.5f, image.DotRadius, 0.001f);

            image.DotIntensity = 3f;
            Assert.AreEqual(1f, image.DotIntensity, 0.001f);

            image.DotRadius = -1f;
            Assert.AreEqual(0f, image.DotRadius, 0.001f);

            image.DotInactiveAlpha = 2f;
            Assert.AreEqual(1f, image.DotInactiveAlpha, 0.001f);
            image.DotTrailLength = -1f;
            Assert.AreEqual(0f, image.DotTrailLength, 0.001f);
        }
    }
}
