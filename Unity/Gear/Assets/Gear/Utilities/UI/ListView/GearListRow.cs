using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ListView
{
    /// <summary>
    /// ListView 行视图：把 <see cref="GearListRowData.cells"/> 写进若干 TMP 列，并维护选中底色。
    /// 挂到 LoopScrollRect 的 itemPrefab 上使用。
    /// </summary>
    [AddComponentMenu("UI/Gear/List Row")]
    public sealed class GearListRow : global::Gear.Utilities.LoopItemBase
    {
        [SerializeField] private TMP_Text[] cells;
        [SerializeField] private Graphic background;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color selectedColor = new Color(0.49f, 0.36f, 0.99f, 0.22f);

        public GearListRowData Data { get; private set; }

        /// <summary>运行时/编辑器配置列与背景；演示卡与测试用。</summary>
        public void Configure(TMP_Text[] columns, Graphic rowBackground = null)
        {
            cells = columns;
            if (rowBackground != null) background = rowBackground;
        }

        public override void Set(int index, string name)
        {
            this.index = index;
            if (cells != null && cells.Length > 0 && cells[0] != null)
                cells[0].text = name ?? string.Empty;
        }

        public void Bind(int dataIndex, GearListRowData data)
        {
            index = dataIndex;
            Data = data;
            if (cells == null) return;

            int count = data != null && data.cells != null ? data.cells.Length : 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                cells[i].text = i < count ? data.cells[i] ?? string.Empty : string.Empty;
            }
        }

        public override void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? selectedColor : normalColor;
        }

        /// <summary>按列宽设置每个 cell 的 LayoutElement.preferredWidth；需配合 HorizontalLayoutGroup。</summary>
        public void ApplyColumnWidths(float[] widths)
        {
            if (cells == null || widths == null) return;

            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                if (cells[i] == null) continue;
                LayoutElement layout = cells[i].GetComponent<LayoutElement>();
                if (layout == null) layout = cells[i].gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = Mathf.Max(0f, widths[i]);
                layout.flexibleWidth = 0f;
            }
        }
    }
}
