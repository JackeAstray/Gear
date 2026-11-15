using System.Collections.Generic;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Navigation;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearShowcasePanelTests
    {
        private GameObject root;
        private GearPageView pageView;
        private GearShowcasePanel panel;
        private TMP_Text title;
        private TMP_Text description;
        private ImageEx background;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ShowcasePanel", typeof(RectTransform), typeof(GearShowcasePanel));
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(root.transform, false);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            var pages = new List<RectTransform>();
            for (int i = 0; i < 3; i++)
            {
                var page = new GameObject("Page" + i, typeof(RectTransform));
                page.transform.SetParent(content.transform, false);
                pages.Add((RectTransform)page.transform);
            }

            pageView = root.AddComponent<GearPageView>();
            pageView.Configure((RectTransform)viewport.transform, (RectTransform)content.transform, pages);
            pageView.PageSizeOverride = new Vector2(100f, 100f);

            title = CreateText("Title");
            description = CreateText("Description");
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            bgGo.transform.SetParent(root.transform, false);
            background = bgGo.GetComponent<ImageEx>();
            var group = root.AddComponent<CanvasGroup>();

            panel = root.GetComponent<GearShowcasePanel>();
            panel.Configure(pageView, title, description, background, group,
                new List<GearShowcasePanel.Item>
                {
                    new GearShowcasePanel.Item { title = "A", description = "a", color = Color.red },
                    new GearShowcasePanel.Item { title = "B", description = "b", color = Color.green },
                    new GearShowcasePanel.Item { title = "C", description = "c", color = Color.blue },
                });
        }

        private TMP_Text CreateText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(root.transform, false);
            return go.GetComponent<TMP_Text>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void GoTo_UpdatesTextAndBackground()
        {
            panel.GoTo(1);

            Assert.AreEqual("B", title.text);
            Assert.AreEqual("b", description.text);
            Assert.AreEqual(Color.green, background.color);
        }

        [Test]
        public void Tick_AutoAdvancesAndLoops()
        {
            panel.AutoPlay = true;
            panel.AutoInterval = 0.1f;
            panel.GoTo(0);

            panel.Tick(0.11f);
            Assert.AreEqual(1, panel.CurrentIndex);
            Assert.AreEqual("B", title.text);

            panel.Tick(0.11f);
            panel.Tick(0.11f);
            Assert.AreEqual(0, panel.CurrentIndex, "自动轮播应循环");
        }

        [Test]
        public void PageChanged_RaisesOnce()
        {
            int changes = 0;
            panel.OnPageChanged.AddListener(_ => changes++);

            panel.GoTo(2);
            Assert.AreEqual(1, changes);
        }
    }
}
