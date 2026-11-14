using NUnit.Framework;
using TMPro;
using UnityEngine;
using Gear.Utilities.UI.ListView;

namespace Gear.Tests
{
    /// <summary>GearListView 数据层与 GearListRow 列绑定的 EditMode 测试。</summary>
    public class GearListViewTests
    {
        private GameObject root;
        private GearListView list;
        private GameObject emptyState;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ListViewRoot", typeof(RectTransform), typeof(GearListView));
            list = root.GetComponent<GearListView>();
            emptyState = new GameObject("Empty", typeof(RectTransform));
            emptyState.transform.SetParent(root.transform, false);
            list.EmptyState = emptyState;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void SetData_UpdatesRowsAndEmptyState()
        {
            list.SetData(new[]
            {
                new GearListRowData("A", "1"),
                new GearListRowData("B", "2"),
            });

            Assert.AreEqual(2, list.GetItemCount());
            Assert.IsFalse(emptyState.activeSelf);

            list.Clear();
            Assert.AreEqual(0, list.GetItemCount());
            Assert.IsTrue(emptyState.activeSelf);
        }

        [Test]
        public void BindItem_WritesRowCells()
        {
            list.SetData(new[] { new GearListRowData("Alpha", "42") });

            GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(GearListRow));
            TextMeshProUGUI name = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            name.transform.SetParent(rowGo.transform, false);
            TextMeshProUGUI value = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            value.transform.SetParent(rowGo.transform, false);

            GearListRow row = rowGo.GetComponent<GearListRow>();
            row.Configure(new[] { name, value });
            list.BindItem((RectTransform)rowGo.transform, 0);

            Assert.AreEqual(0, row.index);
            Assert.AreEqual("Alpha", name.text);
            Assert.AreEqual("42", value.text);

            Object.DestroyImmediate(rowGo);
        }

        [Test]
        public void Csv_ParsesQuotedFieldsAndSkipsHeader()
        {
            System.Collections.Generic.List<GearListRowData> rows = GearListCsv.Parse(
                "Name,Value\n\"A, B\",1\nC,\"x\ny\"", ',', true);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("A, B", rows[0].cells[0]);
            Assert.AreEqual("1", rows[0].cells[1]);
            Assert.AreEqual("x\ny", rows[1].cells[1]);
        }

        [Test]
        public void ApplyColumnWidths_SetsLayoutElements()
        {
            GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(GearListRow));
            TextMeshProUGUI first = new GameObject("A", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            first.transform.SetParent(rowGo.transform, false);
            TextMeshProUGUI second = new GameObject("B", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            second.transform.SetParent(rowGo.transform, false);

            GearListRow row = rowGo.GetComponent<GearListRow>();
            row.Configure(new[] { first, second });
            row.ApplyColumnWidths(new[] { 120f, 48f });

            Assert.AreEqual(120f, first.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth);
            Assert.AreEqual(48f, second.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth);

            Object.DestroyImmediate(rowGo);
        }
    }
}
