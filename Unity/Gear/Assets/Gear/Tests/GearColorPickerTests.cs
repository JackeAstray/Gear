using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearColorPicker 的 Hex 解析、HSV 换算与事件。</summary>
    public class GearColorPickerTests
    {
        private GameObject go;
        private GearColorPicker picker;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ColorPicker", typeof(RectTransform), typeof(GearColorPicker));
            picker = go.GetComponent<GearColorPicker>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void TryParseHex_SupportsShortLongAndAlpha()
        {
            Assert.IsTrue(GearColorPicker.TryParseHex("#F00", out Color shortHex));
            Assert.AreEqual(Color.red.r, shortHex.r, 0.01f);

            Assert.IsTrue(GearColorPicker.TryParseHex("00ff00", out Color green));
            Assert.AreEqual(1f, green.g, 0.01f);
            Assert.AreEqual(0f, green.r, 0.01f);

            Assert.IsTrue(GearColorPicker.TryParseHex("#808080CC", out Color alpha));
            Assert.AreEqual(0.5f, alpha.r, 0.01f);
            Assert.AreEqual(204f / 255f, alpha.a, 0.01f);

            Assert.IsFalse(GearColorPicker.TryParseHex("#12", out _));
            Assert.IsFalse(GearColorPicker.TryParseHex("zzzzzz", out _));
            Assert.IsFalse(GearColorPicker.TryParseHex(null, out _));
        }

        [Test]
        public void ColorToHex_FormatsUppercaseRgb()
        {
            Assert.AreEqual("#FF0000", GearColorPicker.ColorToHex(Color.red));
            Assert.AreEqual("#808080", GearColorPicker.ColorToHex(new Color(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void SetColor_UpdatesHexAndRaisesEvents()
        {
            string hex = null;
            Color changed = default;
            picker.OnHexChanged.AddListener(value => hex = value);
            picker.OnColorChanged.AddListener(value => changed = value);

            picker.SetColor(new Color(1f, 0f, 0f));

            Assert.AreEqual("#FF0000", picker.Hex);
            Assert.AreEqual("#FF0000", hex);
            Assert.AreEqual(1f, changed.r, 0.01f);
            Assert.AreEqual(0f, picker.Hue, 0.5f);
            Assert.AreEqual(1f, picker.Saturation, 0.01f);
        }

        [Test]
        public void SetHSV_ClampsAndWrapsHue()
        {
            picker.SetHSV(480f, 2f, -1f);

            Assert.AreEqual(120f, picker.Hue, 0.5f, "480 应绕回 120");
            Assert.AreEqual(1f, picker.Saturation, 0.01f);
            Assert.AreEqual(0f, picker.Brightness, 0.01f);
            Assert.AreEqual(0f, picker.Value.g, 0.01f);
        }

        [Test]
        public void SetHex_InvalidKeepsColor()
        {
            picker.SetColor(Color.blue);
            Color before = picker.Value;

            Assert.IsFalse(picker.SetHex("not-a-color"));
            Assert.AreEqual(before, picker.Value);
        }

        [Test]
        public void SetHex_WithoutNotifySilent()
        {
            int calls = 0;
            picker.OnColorChanged.AddListener(_ => calls++);

            Assert.IsTrue(picker.SetHex("#00FF00", false));
            Assert.AreEqual(0, calls);
            Assert.AreEqual("#00FF00", picker.Hex);
        }

        [Test]
        public void SetColor_KeepsAlpha()
        {
            picker.SetColor(new Color(1f, 0f, 0f, 0.25f));
            Assert.AreEqual(0.25f, picker.Value.a, 0.01f);

            picker.SetHSV(240f, 1f, 1f);
            Assert.AreEqual(0.25f, picker.Value.a, 0.01f, "改 HSV 不应丢掉 alpha");
        }
    }
}
