using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearRippleTests
    {
        private GameObject go;
        private ImageEx image;
        private GearRipple ripple;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Ripple", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(GearRipple));
            image = go.GetComponent<ImageEx>();
            ripple = go.GetComponent<GearRipple>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Emit_WritesRingBufferToMaterial()
        {
            ripple.Emit(new Vector2(0.5f, 0.5f));

            Assert.AreEqual(1, ripple.WaveCount);
            Material material = image.material;
            Assert.IsNotNull(material);
            Assert.AreEqual(1f, material.GetFloat("_RippleCount"), 0.001f);
        }

        [Test]
        public void Emit_CapsAtEightWaves()
        {
            for (int i = 0; i < 10; i++) ripple.Emit(new Vector2(i / 10f, 0.5f));

            Assert.AreEqual(8, ripple.WaveCount);
            Assert.AreEqual(8f, image.material.GetFloat("_RippleCount"), 0.001f);
        }

        [Test]
        public void Clear_ResetsWaveCount()
        {
            ripple.Emit(Vector2.one);
            ripple.Clear();

            Assert.AreEqual(0, ripple.WaveCount);
        }
    }
}
