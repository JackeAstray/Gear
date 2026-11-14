using System;

namespace Gear.Utilities.UI.ListView
{
    /// <summary>ListView 的一行数据；cells 对应行视图里的列文本。</summary>
    [Serializable]
    public sealed class GearListRowData
    {
        public string[] cells;
        public bool enabled = true;

        public GearListRowData() { }

        public GearListRowData(params string[] values)
        {
            cells = values ?? Array.Empty<string>();
        }
    }
}
