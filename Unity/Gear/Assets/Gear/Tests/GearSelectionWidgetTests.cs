using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>
    /// GearRadioGroup / GearQuantitySelector 的 EditMode 测试。
    /// </summary>
    public class GearSelectionWidgetTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SelectionRoot", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private (Button button, ImageEx visual) CreateOption(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            go.transform.SetParent(root.transform, false);
            return (go.GetComponent<Button>(), go.GetComponent<ImageEx>());
        }

        [Test]
        public void RadioGroup_SelectsOneAndColorsVisuals()
        {
            var a = CreateOption("A");
            var b = CreateOption("B");
            var c = CreateOption("C");

            var group = root.AddComponent<GearRadioGroup>();
            var options = new List<GearRadioGroup.Option>
            {
                new GearRadioGroup.Option { button = a.button, visual = a.visual },
                new GearRadioGroup.Option { button = b.button, visual = b.visual },
                new GearRadioGroup.Option { button = c.button, visual = c.visual },
            };

            int changed = -1;
            group.OnValueChanged.AddListener(index => changed = index);
            group.Configure(options, 0);

            group.Select(2);

            Assert.AreEqual(2, group.SelectedIndex);
            Assert.AreEqual(2, changed);
            Assert.AreNotEqual(a.visual.color, c.visual.color);
        }

        [Test]
        public void RadioGroup_ButtonClickSelects()
        {
            var a = CreateOption("A");
            var b = CreateOption("B");

            var group = root.AddComponent<GearRadioGroup>();
            group.Configure(new List<GearRadioGroup.Option>
            {
                new GearRadioGroup.Option { button = a.button, visual = a.visual },
                new GearRadioGroup.Option { button = b.button, visual = b.visual },
            }, 0);

            b.button.onClick.Invoke();

            Assert.AreEqual(1, group.SelectedIndex);
        }

        [Test]
        public void Quantity_ClampsAndUpdatesLabel()
        {
            var rootGo = new GameObject("Quantity", typeof(RectTransform), typeof(GearQuantitySelector));
            rootGo.transform.SetParent(root.transform, false);

            var labelGo = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(rootGo.transform, false);

            var selector = rootGo.GetComponent<GearQuantitySelector>();
            selector.Configure(0, 3, 1);
            selector.Label = labelGo.GetComponent<TMP_Text>();

            int changed = -1;
            selector.OnValueChanged.AddListener(v => changed = v);

            selector.Increment();
            Assert.AreEqual(2, selector.Value);
            Assert.AreEqual("2", labelGo.GetComponent<TMP_Text>().text);

            selector.SetValue(99);
            Assert.AreEqual(3, selector.Value);
            Assert.AreEqual(3, changed);
        }

        [Test]
        public void Quantity_WrapsAndKeepsButtonsInteractable()
        {
            var rootGo = new GameObject("Quantity", typeof(RectTransform), typeof(GearQuantitySelector));
            rootGo.transform.SetParent(root.transform, false);

            var minus = new GameObject("Minus", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            minus.transform.SetParent(rootGo.transform, false);
            var plus = new GameObject("Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            plus.transform.SetParent(rootGo.transform, false);

            var selector = rootGo.GetComponent<GearQuantitySelector>();
            selector.Configure(0, 2, 2);
            selector.MinusButton = minus.GetComponent<Button>();
            selector.PlusButton = plus.GetComponent<Button>();
            selector.Wrap = true;

            selector.Increment();
            Assert.AreEqual(0, selector.Value);
            Assert.IsTrue(plus.GetComponent<Button>().interactable);

            selector.Decrement();
            Assert.AreEqual(2, selector.Value);
        }
    }
}
