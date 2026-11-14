using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// ImageEx Spotlight（指针聚光）与 ImageExSpotlight 组件的 EditMode 测试。
    /// </summary>
    public class ImageExSpotlightTests
    {
        private GameObject go;
        private ImageEx image;
        private ImageExSpotlight spotlight;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("SpotlightTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(ImageExSpotlight));
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 120f);
            image = go.GetComponent<ImageEx>();
            spotlight = go.GetComponent<ImageExSpotlight>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySettings_WritesImageProperties()
        {
            spotlight.Color = new Color(1f, 0.5f, 0.25f, 1f);
            spotlight.Radius = 80f;
            spotlight.Softness = 0.35f;
            spotlight.Intensity = 0.6f;
            spotlight.ApplySettings();

            Assert.IsTrue(image.EnableSpotlight);
            Assert.AreEqual(new Color(1f, 0.5f, 0.25f, 1f), image.SpotlightColor);
            Assert.AreEqual(80f, image.SpotlightRadius, 0.01f);
            Assert.AreEqual(0.35f, image.SpotlightSoftness, 0.001f);
            Assert.AreEqual(0.6f, image.SpotlightIntensity, 0.001f);
        }

        [Test]
        public void Material_ReceivesSpotlightUniforms()
        {
            image.EnableSpotlight = true;
            image.SpotlightColor = new Color(0.5f, 0.75f, 1f, 1f);
            image.SpotlightRadius = 96f;
            image.SpotlightSoftness = 0.4f;
            image.SpotlightIntensity = 0.55f;
            image.SpotlightPosition = new Vector2(0.25f, 0.75f);
            image.SpotlightHover = 0.8f;

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_EnableSpotlight"));
                Assert.AreEqual(96f, modified.GetFloat("_SpotlightRadius"), 0.01f);
                Assert.AreEqual(0.4f, modified.GetFloat("_SpotlightSoftness"), 0.01f);
                Assert.AreEqual(0.55f, modified.GetFloat("_SpotlightIntensity"), 0.01f);
                Assert.AreEqual(0.8f, modified.GetFloat("_SpotlightHover"), 0.01f);
                Vector4 position = modified.GetVector("_SpotlightPosition");
                Assert.AreEqual(0.25f, position.x, 0.001f);
                Assert.AreEqual(0.75f, position.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void Radius_ClampsToAtLeastOnePixel()
        {
            image.SpotlightRadius = 0.2f;
            Assert.AreEqual(1f, image.SpotlightRadius, 0.001f);

            spotlight.Radius = 0f;
            Assert.AreEqual(1f, spotlight.Radius, 0.001f);
            Assert.AreEqual(1f, image.SpotlightRadius, 0.001f);
        }

        [Test]
        public void SoftnessAndIntensity_ClampToUnitRange()
        {
            image.SpotlightSoftness = 5f;
            image.SpotlightIntensity = -1f;
            Assert.AreEqual(1f, image.SpotlightSoftness, 0.001f);
            Assert.AreEqual(0f, image.SpotlightIntensity, 0.001f);
        }

        [Test]
        public void Position_ClampsToUnitRect()
        {
            image.SpotlightPosition = new Vector2(2f, -1f);
            Assert.AreEqual(1f, image.SpotlightPosition.x, 0.001f);
            Assert.AreEqual(0f, image.SpotlightPosition.y, 0.001f);
        }
    }
}
