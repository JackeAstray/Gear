using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearMarqueeTests
    {
        private GameObject root;
        private GearMarquee marquee;
        private RectTransform content;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Marquee", typeof(RectTransform), typeof(GearMarquee));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = new Vector2(100f, 30f);
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.sizeDelta = new Vector2(240f, 30f);
            marquee = root.GetComponent<GearMarquee>();
            marquee.Viewport = viewport;
            marquee.Content = content;
            marquee.Speed = 40f;
            marquee.Spacing = 20f;
            marquee.Play();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Tick_MovesContentLeft()
        {
            marquee.Tick(1f);
            Assert.AreEqual(-40f, content.anchoredPosition.x, 0.01f);
        }

        [Test]
        public void Tick_WrapsAfterOneCycle()
        {
            marquee.Tick(10f);
            Assert.Greater(content.anchoredPosition.x, -260f);
        }
    }

    public sealed class GearComparisonSliderTests
    {
        private GameObject root;
        private GearComparisonSlider slider;
        private RectTransform track;
        private RectTransform before;
        private RectTransform after;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Comparison", typeof(RectTransform), typeof(GearComparisonSlider));
            slider = root.GetComponent<GearComparisonSlider>();
            track = new GameObject("Track", typeof(RectTransform)).GetComponent<RectTransform>();
            track.SetParent(root.transform, false);
            track.sizeDelta = new Vector2(200f, 80f);
            before = new GameObject("Before", typeof(RectTransform)).GetComponent<RectTransform>();
            before.SetParent(track, false);
            after = new GameObject("After", typeof(RectTransform)).GetComponent<RectTransform>();
            after.SetParent(track, false);
            slider.Track = track;
            slider.Before = before;
            slider.After = after;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void SetValue_SplitsBeforeAndAfter()
        {
            slider.SetValue(0.25f, false);

            Assert.AreEqual(0.25f, slider.Value, 0.001f);
            Assert.AreEqual(50f, before.sizeDelta.x, 0.01f);
            Assert.AreEqual(150f, after.sizeDelta.x, 0.01f);
        }

        [Test]
        public void SetValue_ClampsAndNotifies()
        {
            float changed = -1f;
            slider.OnValueChanged.AddListener(value => changed = value);
            slider.SetValue(1.5f);

            Assert.AreEqual(1f, slider.Value, 0.001f);
            Assert.AreEqual(1f, changed, 0.001f);
        }
    }
}
