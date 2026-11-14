using System;
using System.Collections.Generic;
using UnityEngine;
using Gear.Utilities.UI.ListView;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>
    /// 图表 → ListView 联动：饼图扇区点击后，按 categories[sliceIndex] 过滤行的某一列。
    /// <para>
    /// 只做数据层联动，不绑定具体业务；行数据仍是 <see cref="GearListRowData"/>。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Charts/Chart List Link")]
    public sealed class GearChartListLink : MonoBehaviour
    {
        [SerializeField] private GearPieChart chart;
        [SerializeField] private GearListView list;
        [SerializeField] private int categoryColumn;
        [SerializeField] private string[] categories;

        private readonly List<GearListRowData> allRows = new List<GearListRowData>();

        public int SelectedSlice { get; private set; } = -1;
        public int VisibleRowCount => list != null ? list.GetItemCount() : 0;
        public IReadOnlyList<GearListRowData> AllRows => allRows;

        public event Action<int> FilterChanged;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (chart == null) return;
            chart.SliceClicked -= ApplyFilter;
            chart.SliceClicked += ApplyFilter;
        }

        private void Unsubscribe()
        {
            if (chart != null) chart.SliceClicked -= ApplyFilter;
        }

        /// <summary>运行时绑定数据；rows 会复制一份用于“全部”视图。</summary>
        public void Bind(GearPieChart pie, GearListView listView, IList<GearListRowData> rows,
            IList<string> sliceCategories, int column = 0)
        {
            Unsubscribe();
            chart = pie;
            list = listView;
            categoryColumn = Mathf.Max(0, column);

            categories = sliceCategories == null ? Array.Empty<string>() : new string[sliceCategories.Count];
            if (sliceCategories != null)
            {
                for (int i = 0; i < sliceCategories.Count; i++) categories[i] = sliceCategories[i];
            }

            allRows.Clear();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i] != null) allRows.Add(rows[i]);
                }
            }

            Subscribe();
            ApplyFilter(-1);
        }

        /// <summary>应用过滤；sliceIndex<0 或没有分类时恢复全部行。</summary>
        public void ApplyFilter(int sliceIndex)
        {
            SelectedSlice = sliceIndex;
            if (list == null) return;

            if (sliceIndex < 0 || categories == null || sliceIndex >= categories.Length)
            {
                list.SetData(allRows);
                FilterChanged?.Invoke(-1);
                return;
            }

            string category = categories[sliceIndex];
            List<GearListRowData> filtered = new List<GearListRowData>();
            for (int i = 0; i < allRows.Count; i++)
            {
                GearListRowData row = allRows[i];
                if (row == null || row.cells == null || categoryColumn >= row.cells.Length) continue;
                if (string.Equals(row.cells[categoryColumn], category, StringComparison.Ordinal))
                    filtered.Add(row);
            }

            list.SetData(filtered);
            FilterChanged?.Invoke(sliceIndex);
        }

        public void ClearFilter() => ApplyFilter(-1);
    }
}
