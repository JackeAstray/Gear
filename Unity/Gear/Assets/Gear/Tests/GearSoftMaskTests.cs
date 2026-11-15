using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearSoftMaskTests
    {
        private GameObject root;
        private GearSoftMask outer;
        private GearSoftMask inner;
        private ImageEx image;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("OuterMask", typeof(RectTransform), typeof(GearSoftMask));
            outer = root.GetComponent<GearSoftMask>();
            outer.Shape = GearSoftMask.SoftMaskShape.Circle;

            var innerGo = new GameObject("InnerMask", typeof(RectTransform), typeof(GearSoftMask));
            innerGo.transform.SetParent(root.transform, false);
            inner = innerGo.GetComponent<GearSoftMask>();

            var imageGo = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            imageGo.transform.SetParent(innerGo.transform, false);
            image = imageGo.GetComponent<ImageEx>();
            image.rectTransform.sizeDelta = new Vector2(100f, 100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void NestedMasks_WriteTwoLayers()
        {
            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1f, modified.GetFloat("_GearMaskEnabled"), 0.01f);
                Assert.AreEqual(1f, modified.GetFloat("_GearMask2Enabled"), 0.01f);
                Assert.AreEqual(1f, modified.GetFloat("_GearMask2Shape"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void SingleMask_DisablesSecondLayer()
        {
            inner.enabled = false;
            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1f, modified.GetFloat("_GearMaskEnabled"), 0.01f);
                Assert.AreEqual(0f, modified.GetFloat("_GearMask2Enabled"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }
    }
}
