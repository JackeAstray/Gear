using NUnit.Framework;
using TMPro;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearProgressBar / GearCounter 的 EditMode 测试。</summary>
    public class GearWidgetTests
    {
        private GameObject root;
        private RectTransform fill;
        private TextMeshProUGUI label;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WidgetRoot", typeof(RectTransform));
            fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
            fill.SetParent(root.transform, false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = Vector2.one;

            label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void ProgressBar_ImmediateValueUpdatesAnchorsAndLabel()
        {
            GearProgressBar bar = root.AddComponent<GearProgressBar>();
            bar.Bind(fill, label);
            bar.SetFormat("{0:0.00}");
            bar.SetValueImmediate(0.5f);

            Assert.AreEqual(0.5f, fill.anchorMax.x, 0.001f);
            Assert.AreEqual("0.50", label.text);
            Assert.AreEqual(0.5f, bar.Value, 0.001f);
        }

        [Test]
        public void Counter_ImmediateValueFormatsLabel()
        {
            GearCounter counter = root.AddComponent<GearCounter>();
            counter.Bind(label);
            counter.SetFormat("{0:0}");
            counter.SetValueImmediate(42f);

            Assert.AreEqual("42", label.text);
            Assert.AreEqual(42f, counter.Value, 0.001f);
        }
    }
}
