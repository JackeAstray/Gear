using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// GearTopographic 与 ImageEx 等值线 uniform 的 EditMode 测试。
    /// </summary>
    public class ImageExTopographicTests
    {
        private GameObject go;
        private ImageEx image;
        private GearTopographic topo;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("TopographicTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(GearTopographic));
            ((RectTransform)go.transform).sizeDelta = new Vector2(240f, 140f);
            image = go.GetComponent<ImageEx>();
            topo = go.GetComponent<GearTopographic>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySettings_WritesUniforms()
        {
            topo.Scale = 4f;
            topo.Density = 8f;
            topo.LineWidth = 0.1f;
            topo.MajorEvery = 4f;
            topo.Intensity = 0.7f;
            topo.Speed = 0.5f;
            topo.Falloff = 0.6f;
            topo.ApplySettings();

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_EnableTopographic"));
                Assert.AreEqual(4f, modified.GetFloat("_TopoScale"), 0.01f);
                Assert.AreEqual(8f, modified.GetFloat("_TopoDensity"), 0.01f);
                Assert.AreEqual(0.1f, modified.GetFloat("_TopoLineWidth"), 0.01f);
                Assert.AreEqual(4f, modified.GetFloat("_TopoMajorEvery"), 0.01f);
                Assert.AreEqual(0.7f, modified.GetFloat("_TopoIntensity"), 0.01f);
                Assert.AreEqual(0.5f, modified.GetFloat("_TopoSpeed"), 0.01f);
                Assert.AreEqual(0.6f, modified.GetFloat("_TopoFalloff"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void Values_ClampToSupportedRanges()
        {
            image.TopoScale = 0f;
            Assert.AreEqual(0.1f, image.TopoScale, 0.001f);

            image.TopoDensity = 0f;
            Assert.AreEqual(1f, image.TopoDensity, 0.001f);

            image.TopoLineWidth = 2f;
            Assert.AreEqual(0.5f, image.TopoLineWidth, 0.001f);

            image.TopoMajorEvery = 0f;
            Assert.AreEqual(1f, image.TopoMajorEvery, 0.001f);

            image.TopoIntensity = 5f;
            Assert.AreEqual(1f, image.TopoIntensity, 0.001f);

            image.TopoFalloff = -1f;
            Assert.AreEqual(0f, image.TopoFalloff, 0.001f);
        }
    }
}
