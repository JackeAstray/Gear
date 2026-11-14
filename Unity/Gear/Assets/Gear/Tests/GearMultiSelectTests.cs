using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>GearMultiSelect 的勾选、掩码、上限与确认事件。</summary>
    public class GearMultiSelectTests
    {
        private GameObject go;
        private GearMultiSelect select;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("MultiSelect", typeof(RectTransform), typeof(GearMultiSelect));
            select = go.GetComponent<GearMultiSelect>();
            select.SetOptions(new[] { "A", "B", "C", "D" });
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Toggle_UpdatesMaskAndLabel()
        {
            int changed = -1;
            select.OnSelectionChanged.AddListener(mask => changed = mask);

            select.Toggle(0);
            select.Toggle(2);

            Assert.AreEqual(0b101, select.Mask);
            Assert.AreEqual(0b101, changed);
            Assert.AreEqual(2, select.SelectedCount);
            Assert.AreEqual("2 项已选", select.LabelText);

            select.Toggle(2);
            Assert.AreEqual(0b001, select.Mask, "再次点击应取消");
        }

        [Test]
        public void SetSelected_IgnoresOutOfRange()
        {
            Assert.IsFalse(select.SetSelected(-1, true));
            Assert.IsFalse(select.SetSelected(4, true));
            Assert.AreEqual(0, select.SelectedCount);
        }

        [Test]
        public void MaxSelection_BlocksExtraChecks()
        {
            select.MaxSelection = 2;
            Assert.IsTrue(select.SetSelected(0, true));
            Assert.IsTrue(select.SetSelected(1, true));
            Assert.IsFalse(select.SetSelected(2, true), "超过上限应拒绝");
            Assert.AreEqual(0b011, select.Mask);
        }

        [Test]
        public void SelectAllAndClear_RespectLimit()
        {
            select.MaxSelection = 3;
            select.SelectAll();
            Assert.AreEqual(3, select.SelectedCount);

            select.MaxSelection = 0;
            select.SelectAll();
            Assert.AreEqual(4, select.SelectedCount);

            select.ClearSelection();
            Assert.AreEqual(0, select.Mask);
            Assert.AreEqual("请选择", select.LabelText);
        }

        [Test]
        public void Confirm_RaisesMask()
        {
            select.SetSelected(1, true);
            select.SetSelected(3, true);

            int confirmed = -1;
            select.OnConfirmed.AddListener(mask => confirmed = mask);

            select.Confirm();

            Assert.AreEqual(0b1010, confirmed);
        }

        [Test]
        public void SetOptions_TrimsStaleSelection()
        {
            select.SetSelected(0, true);
            select.SetSelected(3, true);

            select.SetOptions(new[] { "A", "B" }, false);

            Assert.AreEqual(1, select.SelectedCount, "越界项应被裁掉");
            Assert.IsTrue(select.IsSelected(0));
            Assert.AreEqual(0b01, select.Mask);
        }

        [Test]
        public void Mask_SaturatesAtThirtyOneItems()
        {
            var many = new string[40];
            for (int i = 0; i < many.Length; i++) many[i] = "Item " + i;
            select.SetOptions(many);

            select.SelectAll();
            Assert.AreEqual(int.MaxValue, select.Mask, "31 位以上不进入掩码");
            Assert.AreEqual(40, select.SelectedCount);
        }
    }
}
