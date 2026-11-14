using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// GearPixelCurtain 与 ImageEx 幕帘 uniform 的 EditMode 测试。
    /// </summary>
    public class GearPixelCurtainTests
    {
        private GameObject go;
        private ImageEx image;
        private GearPixelCurtain curtain;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("CurtainTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(GearPixelCurtain));
            ((RectTransform)go.transform).sizeDelta = new Vector2(240f, 140f);
            image = go.GetComponent<ImageEx>();
            curtain = go.GetComponent<GearPixelCurtain>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplySettings_WritesUniforms()
        {
            curtain.Cell = 32f;
            curtain.Randomness = 0.5f;
            curtain.Softness = 0.2f;
            curtain.Direction = GearCurtainDirection.BottomToTop;
            curtain.SetProgress(0.4f);

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1, modified.GetInt("_EnablePixelCurtain"));
                Assert.AreEqual(32f, modified.GetFloat("_CurtainCell"), 0.01f);
                Assert.AreEqual(0.5f, modified.GetFloat("_CurtainRandomness"), 0.01f);
                Assert.AreEqual(0.2f, modified.GetFloat("_CurtainSoftness"), 0.01f);
                Assert.AreEqual((int)GearCurtainDirection.BottomToTop, modified.GetInt("_CurtainDirection"));
                Assert.AreEqual(0.4f, modified.GetFloat("_CurtainProgress"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void SetProgress_ClampsToUnitRange()
        {
            curtain.SetProgress(3f);
            Assert.AreEqual(1f, curtain.Progress, 0.001f);

            curtain.SetProgress(-2f);
            Assert.AreEqual(0f, curtain.Progress, 0.001f);
        }

        [Test]
        public void ShowAndStep_AdvancesTowardsVisible()
        {
            curtain.Duration = 0.6f;
            curtain.SetProgress(0f);
            curtain.Show();

            curtain.Step(0.3f);
            Assert.AreEqual(0.5f, curtain.Progress, 0.02f);

            curtain.Step(0.6f);
            Assert.AreEqual(1f, curtain.Progress, 0.001f);
        }

        [Test]
        public void Hide_StepsBackToHidden()
        {
            curtain.Duration = 0.5f;
            curtain.SetProgress(1f);
            curtain.Hide();

            curtain.Step(0.25f);
            Assert.AreEqual(0.5f, curtain.Progress, 0.02f);

            curtain.Step(1f);
            Assert.AreEqual(0f, curtain.Progress, 0.001f);
        }
    }
}
