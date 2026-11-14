using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.ListView
{
    /// <summary>
    /// ListView 数据层：复用 <see cref="LoopScrollRect"/> 的虚拟化/分页/选中，
    /// 只负责数据、行绑定与空态。行视图用 <see cref="GearListRow"/>。
    /// </summary>
    [AddComponentMenu("UI/Gear/List View")]
    public sealed class GearListView : MonoBehaviour, global::Gear.Utilities.LoopScrollRect.IDataSource
    {
        [SerializeField] private global::Gear.Utilities.LoopScrollRect loop;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private float[] columnWidths;

        private readonly List<GearListRowData> rows = new List<GearListRowData>();

        public IReadOnlyList<GearListRowData> Rows => rows;
        public int SelectedIndex => loop != null ? loop.GetSelectedIndex() : -1;

        /// <summary>列宽（像素）；为空时由行内布局自行决定。</summary>
        public float[] ColumnWidths
        {
            get => columnWidths;
            set
            {
                columnWidths = value;
                if (loop != null) loop.RefreshItems();
            }
        }

        public global::Gear.Utilities.LoopScrollRect Loop
        {
            get => loop;
            set => loop = value;
        }

        /// <summary>空列表时显示的对象；可在 Inspector 或运行时绑定。</summary>
        public GameObject EmptyState
        {
            get => emptyState;
            set => emptyState = value;
        }

        public event Action<int> RowClicked;
        public event Action<int> SelectionChanged;

        private void Awake()
        {
            if (loop == null) loop = GetComponent<global::Gear.Utilities.LoopScrollRect>();
        }

        private void OnEnable()
        {
            if (loop == null) return;
            loop.ItemClicked -= HandleItemClicked;
            loop.ItemClicked += HandleItemClicked;
            loop.SelectionChanged -= HandleSelectionChanged;
            loop.SelectionChanged += HandleSelectionChanged;
        }

        private void OnDestroy()
        {
            if (loop == null) return;
            loop.ItemClicked -= HandleItemClicked;
            loop.SelectionChanged -= HandleSelectionChanged;
        }

        /// <summary>替换全部数据并刷新列表。</summary>
        public void SetData(IList<GearListRowData> data)
        {
            rows.Clear();
            if (data != null)
            {
                for (int i = 0; i < data.Count; i++)
                {
                    if (data[i] != null) rows.Add(data[i]);
                }
            }

            Refresh();
        }

        public void Clear() => SetData(null);

        /// <summary>重新初始化 LoopScrollRect 并更新空态。</summary>
        public void Refresh()
        {
            if (loop != null)
                loop.Initialize(this);
            UpdateEmptyState();
        }

        public int GetItemCount() => rows.Count;

        public void BindItem(RectTransform item, int dataIndex)
        {
            if (item == null || dataIndex < 0 || dataIndex >= rows.Count) return;
            GearListRow row = item.GetComponent<GearListRow>();
            if (row == null) return;

            row.Bind(dataIndex, rows[dataIndex]);
            row.ApplyColumnWidths(columnWidths);
        }

        /// <summary>从简单 CSV 导入；hasHeader=true 时跳过第一行。</summary>
        public void SetCsv(string csv, bool hasHeader = true, char delimiter = ',')
        {
            SetData(GearListCsv.Parse(csv, delimiter, hasHeader));
        }

        public void Select(int dataIndex, bool ensureVisible = false, bool animated = false)
        {
            loop?.SelectDataIndex(dataIndex, ensureVisible, animated);
        }

        public void ClearSelection() => loop?.ClearSelection();

        private void UpdateEmptyState()
        {
            if (emptyState != null)
                emptyState.SetActive(rows.Count == 0);
        }

        private void HandleItemClicked(int index) => RowClicked?.Invoke(index);
        private void HandleSelectionChanged(int index) => SelectionChanged?.Invoke(index);
    }
}
