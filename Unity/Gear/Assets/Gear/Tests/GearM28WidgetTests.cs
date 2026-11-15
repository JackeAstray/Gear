using System.Collections.Generic;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearSelectorTests
    {
        private GameObject root;
        private GearSelector selector;
        private RectTransform indicator;
        private readonly List<GearSelector.Option> options = new List<GearSelector.Option>();

        [SetUp]
        public void SetUp()
        {
            options.Clear();
            root = new GameObject("Selector", typeof(RectTransform), typeof(GearSelector));
            selector = root.GetComponent<GearSelector>();
            indicator = new GameObject("Indicator", typeof(RectTransform)).GetComponent<RectTransform>();
            indicator.SetParent(root.transform, false);
            indicator.sizeDelta = new Vector2(20f, 20f);

            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Option" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(root.transform, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(80f, 30f);
                rect.anchoredPosition = new Vector2(i * 100f, 0f);
                var image = go.GetComponent<Image>();
                options.Add(new GearSelector.Option
                {
                    button = go.GetComponent<Button>(),
                    visual = image,
                    indicatorTarget = rect,
                });
            }

            selector.Configure(options, 0);
            selector.Indicator = indicator;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Select_UpdatesColorAndIndicator()
        {
            selector.Select(2);
            selector.Tick(0.2f);

            Assert.AreEqual(2, selector.SelectedIndex);
            Assert.AreEqual(new Vector2(200f, 0f), indicator.anchoredPosition);
            Assert.AreEqual(selector.Options[2].visual.color, new Color(0.49f, 0.36f, 0.99f, 1f));
        }

        [Test]
        public void Select_RaisesEvent()
        {
            int changed = -1;
            selector.OnValueChanged.AddListener(index => changed = index);
            selector.Select(1);

            Assert.AreEqual(1, changed);
        }
    }

    public sealed class GearRectDraggerTests
    {
        private GameObject root;
        private RectTransform target;
        private GearRectDragger dragger;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Dragger", typeof(RectTransform));
            var bounds = new GameObject("Bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            bounds.SetParent(root.transform, false);
            bounds.sizeDelta = new Vector2(200f, 200f);

            target = new GameObject("Target", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(bounds, false);
            target.sizeDelta = new Vector2(40f, 40f);
            dragger = target.gameObject.AddComponent<GearRectDragger>();
            dragger.Target = target;
            dragger.Bounds = bounds;
            dragger.Axis = GearDragAxis.Horizontal;
            dragger.SpringBack = true;
            dragger.ClampToBounds = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Drag_HonorsAxisAndClamps()
        {
            dragger.OnBeginDrag(new UnityEngine.EventSystems.PointerEventData(null) { delta = Vector2.zero });
            dragger.OnDrag(new UnityEngine.EventSystems.PointerEventData(null) { delta = new Vector2(500f, 80f) });

            Assert.AreEqual(80f, target.anchoredPosition.x, 0.01f);
            Assert.AreEqual(0f, target.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void SpringBack_ReturnsToRest()
        {
            dragger.OnBeginDrag(new UnityEngine.EventSystems.PointerEventData(null));
            dragger.OnDrag(new UnityEngine.EventSystems.PointerEventData(null) { delta = new Vector2(50f, 0f) });
            dragger.OnEndDrag(new UnityEngine.EventSystems.PointerEventData(null));
            dragger.Tick(0.3f);

            Assert.AreEqual(Vector2.zero, target.anchoredPosition);
        }
    }

    public sealed class GearButtonGroupDimmerTests
    {
        private GameObject root;
        private GearButtonGroupDimmer dimmer;
        private readonly List<Graphic> images = new List<Graphic>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Dimmer", typeof(RectTransform), typeof(GearButtonGroupDimmer));
            dimmer = root.GetComponent<GearButtonGroupDimmer>();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Member" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root.transform, false);
                images.Add(go.GetComponent<Image>());
            }

            dimmer.Configure(images);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Hover_DimsOtherMembers()
        {
            dimmer.SetHover(1, true);
            dimmer.Tick(1f);

            Assert.AreEqual(1f, images[1].color.a, 0.01f);
            Assert.Less(images[0].color.a, 1f);
            Assert.Less(images[2].color.a, 1f);
        }
    }
}
