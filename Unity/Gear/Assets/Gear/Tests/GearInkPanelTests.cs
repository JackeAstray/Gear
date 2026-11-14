using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// GearInkPanel 与 ImageEx 墨团 uniform 的 EditMode 测试。
    /// </summary>
    public class GearInkPanelTests
    {
        private GameObject go;
        private ImageEx image;
        private GearInkPanel ink;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("InkPanelTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(GearInkPanel));
            ((RectTransform)go.transform).sizeDelta = new Vector2(240f, 140f);
            image = go.GetComponent<ImageEx>();
            ink = go.GetComponent<GearInkPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySettings_WritesUniforms()
        {
            ink.Origin = new Vector2(0.25f, 0.75f);
            ink.Softness = 0.2f;
            ink.Roughness = 0.6f;
            ink.SetProgress(0.35f);

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_EnableInkPanel"));
                Assert.AreEqual(0.2f, modified.GetFloat("_InkSoftness"), 0.01f);
                Assert.AreEqual(0.6f, modified.GetFloat("_InkRoughness"), 0.01f);
                Assert.AreEqual(0.35f, modified.GetFloat("_InkProgress"), 0.01f);
                Vector4 origin = modified.GetVector("_InkOrigin");
                Assert.AreEqual(0.25f, origin.x, 0.001f);
                Assert.AreEqual(0.75f, origin.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void SetProgress_ClampsToUnitRange()
        {
            ink.SetProgress(3f);
            Assert.AreEqual(1f, ink.Progress, 0.001f);

            ink.SetProgress(-2f);
            Assert.AreEqual(0f, ink.Progress, 0.001f);
        }

        [Test]
        public void ShowAndStep_AdvancesTowardsVisible()
        {
            ink.Duration = 0.6f;
            ink.SetProgress(0f);
            ink.Show();

            ink.Step(0.3f);
            Assert.AreEqual(0.5f, ink.Progress, 0.02f);

            ink.Step(0.6f);
            Assert.AreEqual(1f, ink.Progress, 0.001f);
        }

        [Test]
        public void Hide_StepsBackToHidden()
        {
            ink.Duration = 0.5f;
            ink.SetProgress(1f);
            ink.Hide();

            ink.Step(0.25f);
            Assert.AreEqual(0.5f, ink.Progress, 0.02f);

            ink.Step(1f);
            Assert.AreEqual(0f, ink.Progress, 0.001f);
        }
    }
}
