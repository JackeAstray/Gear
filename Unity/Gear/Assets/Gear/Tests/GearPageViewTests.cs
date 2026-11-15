using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.Navigation;

namespace Gear.Tests
{
    /// <summary>
    /// GearPageView 的 EditMode 测试：排版、切页、边界与事件。
    /// </summary>
    public class GearPageViewTests
    {
        private GameObject root;
        private RectTransform viewport;
        private RectTransform content;
        private GearPageView view;
        private RectTransform page0;
        private RectTransform page1;
        private RectTransform page2;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PageViewRoot", typeof(RectTransform), typeof(GearPageView));
            view = root.GetComponent<GearPageView>();

            viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);

            page0 = new GameObject("Page0", typeof(RectTransform)).GetComponent<RectTransform>();
            page0.SetParent(content, false);
            page1 = new GameObject("Page1", typeof(RectTransform)).GetComponent<RectTransform>();
            page1.SetParent(content, false);
            page2 = new GameObject("Page2", typeof(RectTransform)).GetComponent<RectTransform>();
            page2.SetParent(content, false);

            view.PageSizeOverride = new Vector2(100f, 50f);
            view.Configure(viewport, content, new List<RectTransform> { page0, page1, page2 });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Configure_LaysOutPagesAndSelectsFirst()
        {
            Assert.AreEqual(0, view.CurrentIndex);
            Assert.AreEqual(new Vector2(0f, 0f), page0.anchoredPosition);
            Assert.AreEqual(new Vector2(100f, 0f), page1.anchoredPosition);
            Assert.AreEqual(new Vector2(200f, 0f), page2.anchoredPosition);
            Assert.AreEqual(new Vector2(0f, 0f), content.anchoredPosition);
        }

        [Test]
        public void GoTo_MovesContentAndRaisesEvent()
        {
            int changes = 0;
            view.PageChanged += _ => changes++;
            view.GoTo(1, true);
            Assert.AreEqual(1, view.CurrentIndex);
            Assert.AreEqual(-100f, content.anchoredPosition.x, 0.01f);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void NextAndPrev_ClampAtEnds()
        {
            view.GoTo(2, true);
            view.Next();
            Assert.AreEqual(2, view.CurrentIndex);

            view.GoTo(0, true);
            view.Prev();
            Assert.AreEqual(0, view.CurrentIndex);
        }

        [Test]
        public void GoTo_OutOfRange_ClampsToLast()
        {
            view.GoTo(99, true);
            Assert.AreEqual(2, view.CurrentIndex);
            Assert.AreEqual(-200f, content.anchoredPosition.x, 0.01f);
        }

        [Test]
        public void PagePadding_OffsetsPagesForNeighborPeek()
        {
            view.PagePadding = 20f;

            Assert.AreEqual(new Vector2(20f, 0f), page0.anchoredPosition);
            Assert.AreEqual(new Vector2(120f, 0f), page1.anchoredPosition);
        }

        [Test]
        public void Indicators_UpdateColorAndClickJumps()
        {
            List<Button> indicators = new List<Button>();
            for (int i = 0; i < 3; i++)
            {
                Button button = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image), typeof(Button))
                    .GetComponent<Button>();
                button.transform.SetParent(root.transform, false);
                button.targetGraphic = button.GetComponent<Image>();
                indicators.Add(button);
            }

            view.SetIndicators(indicators);
            view.GoTo(1, true);
            Assert.AreEqual(Color.white, indicators[1].targetGraphic.color);

            indicators[2].onClick.Invoke();
            Assert.AreEqual(2, view.CurrentIndex);
        }

        [Test]
        public void Drag_EndsOnNearestPage()
        {
            PointerEventData eventData = new PointerEventData(null)
            {
                delta = new Vector2(-60f, 0f),
            };

            view.OnBeginDrag(eventData);
            view.OnDrag(eventData);
            view.OnEndDrag(eventData);

            Assert.AreEqual(1, view.CurrentIndex);
        }
    }
}
