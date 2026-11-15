using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class ImageExTransientEffectTests
    {
        private GameObject go;
        private ImageEx image;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Transient", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            image = go.GetComponent<ImageEx>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void TransientReferences_ControlPooledBypass()
        {
            Assert.IsFalse(image.HasTransientEffects);

            image.AddTransientEffect();
            image.AddTransientEffect();
            Assert.IsTrue(image.HasTransientEffects);
            Assert.AreEqual(2, image.TransientEffectReferences);

            image.RemoveTransientEffect();
            image.RemoveTransientEffect();
            Assert.IsFalse(image.HasTransientEffects);
        }
    }
}
