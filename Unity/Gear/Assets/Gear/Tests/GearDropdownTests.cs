using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>
    /// GearDropdown 的选项/选中/标签/开合测试（EditMode）。
    /// </summary>
    public class GearDropdownTests
    {
        private GameObject go;
        private GameObject labelGo;
        private GearDropdown dropdown;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Dropdown", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Gear.Utilities.UI.ImageExtensions.ImageEx), typeof(Button), typeof(GearDropdown));
            labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);

            dropdown = go.GetComponent<GearDropdown>();
            dropdown.Label = labelGo.GetComponent<TMP_Text>();
            dropdown.Trigger = go.GetComponent<Button>();
        }

        [TearDown]
        public void TearDown()
        {
            if (labelGo != null) Object.DestroyImmediate(labelGo);
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void SetValue_WritesLabelAndNotifies()
        {
            dropdown.SetOptions(new[] { "低", "中", "高" });
            int changed = -1;
            dropdown.OnValueChanged.AddListener(index => changed = index);

            dropdown.SetValue(1, true);

            Assert.AreEqual(1, dropdown.Value);
            Assert.AreEqual("中", dropdown.Label.text);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void SetValueWithoutNotify_DoesNotInvoke()
        {
            dropdown.SetOptions(new[] { "A", "B" });
            int changed = -1;
            dropdown.OnValueChanged.AddListener(index => changed = index);

            dropdown.SetValueWithoutNotify(1);

            Assert.AreEqual(1, dropdown.Value);
            Assert.AreEqual("B", dropdown.Label.text);
            Assert.AreEqual(-1, changed);
        }

        [Test]
        public void SetOptions_ClampsValueAndRefreshesLabel()
        {
            dropdown.SetOptions(new[] { "A", "B", "C" });
            dropdown.SetValueWithoutNotify(2);

            dropdown.SetOptions(new[] { "X" }, true);

            Assert.AreEqual(0, dropdown.Value);
            Assert.AreEqual("X", dropdown.Label.text);
        }

        [Test]
        public void OpenClose_TogglesIsOpen()
        {
            dropdown.SetOptions(new[] { "A", "B", "C" });

            dropdown.Open();
            Assert.IsTrue(dropdown.IsOpen);

            dropdown.Close();
            Assert.IsFalse(dropdown.IsOpen);
        }

        [Test]
        public void Select_ClosesAndNotifies()
        {
            dropdown.SetOptions(new[] { "A", "B", "C" });
            dropdown.Open();

            dropdown.Select(2);

            Assert.AreEqual(2, dropdown.Value);
            Assert.AreEqual("C", dropdown.Label.text);
            Assert.IsFalse(dropdown.IsOpen);
        }
    }
}
