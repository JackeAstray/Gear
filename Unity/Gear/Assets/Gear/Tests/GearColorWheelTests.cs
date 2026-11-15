using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearColorWheelTests
    {
        private GameObject go;
        private GearColorWheel wheel;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ColorWheel", typeof(RectTransform), typeof(GearColorWheel));
            wheel = go.GetComponent<GearColorWheel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void EvaluateWheelColor_MapsAngleAndRadius()
        {
            Color center = GearColorWheel.EvaluateWheelColor(0f, 0f, 1f);
            Assert.AreEqual(1f, center.r, 0.01f);
            Assert.AreEqual(1f, center.g, 0.01f);
            Assert.AreEqual(1f, center.b, 0.01f);

            Color right = GearColorWheel.EvaluateWheelColor(1f, 0f, 1f);
            Assert.AreEqual(1f, right.r, 0.01f);
            Assert.AreEqual(0f, right.g, 0.01f);

            Color top = GearColorWheel.EvaluateWheelColor(0f, 1f, 1f);
            Assert.Greater(top.g, 0.9f, "顶部应为 90° 绿");
        }

        [Test]
        public void EvaluateWheelColor_OutsideCircleIsTransparent()
        {
            Color outside = GearColorWheel.EvaluateWheelColor(1.2f, 0f, 1f);
            Assert.AreEqual(0f, outside.a, 0.001f);
        }

        [Test]
        public void SetHSV_ClampsAndWraps()
        {
            wheel.SetHSV(480f, 2f, -1f);

            Assert.AreEqual(120f, wheel.Hue, 0.5f);
            Assert.AreEqual(1f, wheel.Saturation, 0.01f);
            Assert.AreEqual(0f, wheel.Brightness, 0.01f);
        }

        [Test]
        public void SetColor_RaisesChanged()
        {
            Color changed = default;
            wheel.OnColorChanged.AddListener(c => changed = c);

            wheel.SetColor(Color.blue);

            Assert.AreEqual(Color.blue, changed);
            Assert.AreEqual("#0000FF", wheel.Hex);
        }
    }
}
