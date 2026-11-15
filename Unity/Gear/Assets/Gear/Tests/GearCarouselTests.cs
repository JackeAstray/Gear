using System.Collections.Generic;
using Gear.Utilities.UI.Navigation;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearCarouselTests
    {
        private GameObject root;
        private GearPageView pageView;
        private GearCarousel carousel;
        private readonly List<RectTransform> pages = new List<RectTransform>();
        private readonly List<Button> indicators = new List<Button>();

        [SetUp]
        public void SetUp()
        {
            pages.Clear();
            indicators.Clear();
            root = new GameObject("Carousel", typeof(RectTransform), typeof(GearPageView), typeof(GearCarousel));
            pageView = root.GetComponent<GearPageView>();
            carousel = root.GetComponent<GearCarousel>();
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);

            for (int i = 0; i < 3; i++)
            {
                var page = new GameObject("Page" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                page.SetParent(content, false);
                pages.Add(page);
            }

            pageView.PageSizeOverride = new Vector2(100f, 50f);
            pageView.Configure(viewport, content, pages);
            for (int i = 0; i < 3; i++)
            {
                Button button = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image), typeof(Button))
                    .GetComponent<Button>();
                button.transform.SetParent(root.transform, false);
                button.targetGraphic = button.GetComponent<Image>();
                indicators.Add(button);
            }

            carousel.Configure(pageView, indicators);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Tick_AutoAdvancesAndLoops()
        {
            carousel.AutoSlide = true;
            carousel.AutoInterval = 0.1f;

            carousel.Tick(0.11f);
            Assert.AreEqual(1, carousel.CurrentIndex);

            carousel.Next();
            carousel.Next();
            Assert.AreEqual(0, carousel.CurrentIndex, "末尾应循环到第一页");
        }

        [Test]
        public void Indicators_ShrinkSelected()
        {
            carousel.Next();
            RectTransform selected = (RectTransform)indicators[1].transform;
            RectTransform idle = (RectTransform)indicators[0].transform;

            Assert.Greater(selected.sizeDelta.x, idle.sizeDelta.x);
        }

        [Test]
        public void NeighborVisuals_DimAndScale()
        {
            var current = pages[0].GetComponent<CanvasGroup>();
            var neighbor = pages[1].GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, current.alpha, 0.01f);
            Assert.Less(neighbor.alpha, 1f);
            Assert.Less(neighbor.transform.localScale.x, 1f);
        }
    }
}
