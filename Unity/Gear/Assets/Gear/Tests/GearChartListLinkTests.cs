using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Charts;
using Gear.Utilities.UI.ListView;

namespace Gear.Tests
{
    /// <summary>图表 → ListView 过滤联动的 EditMode 测试。</summary>
    public class GearChartListLinkTests
    {
        private GameObject root;
        private GameObject pieGo;
        private GearListView list;
        private GearChartListLink link;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("LinkRoot", typeof(RectTransform));
            GameObject listGo = new GameObject("List", typeof(RectTransform), typeof(GearListView));
            listGo.transform.SetParent(root.transform, false);
            list = listGo.GetComponent<GearListView>();
            GameObject empty = new GameObject("Empty", typeof(RectTransform));
            empty.transform.SetParent(listGo.transform, false);
            list.EmptyState = empty;

            pieGo = new GameObject("Pie", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearPieChart));
            pieGo.transform.SetParent(root.transform, false);
            GearPieChart pie = pieGo.GetComponent<GearPieChart>();
            pie.SetData(new[] { 0.5f, 0.3f, 0.2f });

            link = root.AddComponent<GearChartListLink>();
            link.Bind(pie, list, new[]
            {
                new GearListRowData("A", "1"),
                new GearListRowData("A", "2"),
                new GearListRowData("B", "3"),
            }, new[] { "A", "B", "C" }, 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void ApplyFilter_FiltersRowsByCategoryColumn()
        {
            link.ApplyFilter(0);
            Assert.AreEqual(2, link.VisibleRowCount);

            link.ApplyFilter(1);
            Assert.AreEqual(1, link.VisibleRowCount);

            link.ClearFilter();
            Assert.AreEqual(3, link.VisibleRowCount);
            Assert.AreEqual(-1, link.SelectedSlice);
        }
    }
}
