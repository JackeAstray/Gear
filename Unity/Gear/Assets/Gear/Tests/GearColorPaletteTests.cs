using System.Collections.Generic;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearColorPaletteTests
    {
        private GameObject root;
        private GearColorPalette palette;
        private readonly List<GearColorSwatch> swatches = new List<GearColorSwatch>();

        [SetUp]
        public void SetUp()
        {
            swatches.Clear();
            root = new GameObject("Palette", typeof(RectTransform), typeof(GearColorPalette));
            palette = root.GetComponent<GearColorPalette>();
            Color[] colors = { Color.red, Color.green, Color.blue };
            for (int i = 0; i < colors.Length; i++)
            {
                var go = new GameObject("Swatch" + i,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx),
                    typeof(Button), typeof(GearColorSwatch));
                go.transform.SetParent(root.transform, false);
                var swatch = go.GetComponent<GearColorSwatch>();
                swatch.SetColor(colors[i]);
                swatches.Add(swatch);
            }

            palette.Configure(swatches, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Select_UpdatesColorAndBorder()
        {
            palette.Select(1, false);

            Assert.AreEqual(1, palette.SelectedIndex);
            Assert.AreEqual(Color.green, palette.SelectedColor);
            Assert.AreEqual(2f, swatches[1].Image.OutlineWidth, 0.01f);
            Assert.AreEqual(0f, swatches[0].Image.OutlineWidth, 0.01f);
        }

        [Test]
        public void SetColor_SelectsNearestSwatch()
        {
            palette.SetColor(new Color(0.9f, 0.1f, 0.1f), false);

            Assert.AreEqual(0, palette.SelectedIndex);
            Assert.AreEqual(Color.red, palette.SelectedColor);
        }

        [Test]
        public void Hover_ExtrapolatesScale()
        {
            palette.Select(0, false);
            palette.SetHover(1, true);

            Assert.Greater(swatches[1].transform.localScale.x, 1f);
            palette.SetHover(1, false);
            Assert.AreEqual(1f, swatches[1].transform.localScale.x, 0.01f);
        }
    }
}
