using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Determines how a row cell renders and handles input.
    /// </summary>
    public enum ListViewCellType
    {
        None = 0,
        GameObject = 1
    }

    /// <summary>
    /// A convenient value object for reading and writing cell data.
    /// </summary>
    public readonly struct ListViewCellData
    {
        /// <summary>
        /// The rendering and interaction preset used by the cell.
        /// </summary>
        public readonly ListViewCellType type;

        /// <summary>
        /// The label displayed by built-in cell presets.
        /// </summary>
        public readonly string label;

        /// <summary>
        /// The icon displayed by built-in cell presets.
        /// </summary>
        public readonly Sprite icon;

        /// <summary>
        /// The source object instantiated by the <see cref="ListViewCellType.GameObject"/> preset.
        /// </summary>
        public readonly GameObject gameObject;

        /// <summary>
        /// Whether the ListView background is rendered behind GameObject cell content.
        /// </summary>
        public readonly bool drawBackground;

        public ListViewCellData(ListViewCellType type, string label = "", Sprite icon = null, GameObject gameObject = null, bool drawBackground = true)
        {
            this.type = type;
            this.label = label ?? string.Empty;
            this.icon = icon;
            this.gameObject = gameObject;
            this.drawBackground = drawBackground;
        }
    }

    /// <summary>
    /// Defines the content and layout of one ListView column.
    /// </summary>
    [Serializable]
    public class ListViewColumn
    {
        [Tooltip("Text displayed in the column header.")]
        public string columnName = "Column";

#if EVO_LOCALIZATION
        [Tooltip("Localization key for the column header. Leave empty to use Name.")]
        public string tableKey;
#endif

        [Tooltip("Optional icon displayed in the column header.")]
        public Sprite columnIcon;

        [Tooltip("Column width in pixels. A flexible column uses it as its minimum while Horizontal Scroll is on.")]
        public float width = 100;

        [Tooltip("Shares the width remaining after fixed-width columns are allocated.")]
        public bool useFlexibleWidth = true;

        [Tooltip("Alignment used by the header and every built-in cell in this column.")]
        public TextAnchor alignment = TextAnchor.MiddleCenter;

        [Tooltip("Stable identity used to preserve generated cells across reordering.")]
        [SerializeField, HideInInspector] string id;

        internal string GetOrCreateId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");

            return id;
        }

        internal void RegenerateId() => id = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Stores the serialized data for one ListView row.
    /// </summary>
    [Serializable]
    public class ListViewRow
    {
        [Tooltip("Built-in label text for each column.")]
        public List<string> values = new();

#if EVO_LOCALIZATION
        [Tooltip("Localization key for each built-in cell. Empty keys use the original label.")]
        public List<string> tableKeys = new();
#endif

        [Tooltip("Built-in icon for each column.")]
        public List<Sprite> icons = new();

        [Tooltip("Source object instantiated for each GameObject cell.")]
        public List<GameObject> customObjects = new();

        [Tooltip("Whether the ListView background is rendered behind each GameObject cell.")]
        public List<bool> drawBackgrounds = new();

        [Tooltip("Rendering and interaction preset for each cell.")]
        public List<ListViewCellType> cellTypes = new();

        [Tooltip("Stable identity used to preserve generated cells across reordering.")]
        [SerializeField, HideInInspector] string id;

        public ListViewRow() { }

        /// <summary>
        /// Creates a row of built-in, non-interactive cells from label values.
        /// </summary>
        public ListViewRow(params string[] rowValues)
        {
            int count = rowValues?.Length ?? 0;
            values = rowValues != null ? new List<string>(rowValues) : new List<string>();
            icons = new List<Sprite>(new Sprite[count]);
            customObjects = new List<GameObject>(new GameObject[count]);
            cellTypes = new List<ListViewCellType>(count);
            drawBackgrounds = new List<bool>(count);

            for (int i = 0; i < count; i++)
            {
                cellTypes.Add(ListViewCellType.None);
                drawBackgrounds.Add(true);
            }
        }

        /// <summary>
        /// Gets a cell-data snapshot at the specified column index.
        /// </summary>
        public ListViewCellData GetCell(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return new ListViewCellData(
                GetCellType(index),
                index < values.Count ? values[index] : string.Empty,
                GetIcon(index),
                GetCustomObject(index),
                GetDrawBackground(index));
        }

        /// <summary>
        /// Sets all data for a cell at the specified column index.
        /// </summary>
        public void SetCell(int index, ListViewCellData data)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureCapacity(index + 1);
            values[index] = data.label ?? string.Empty;
            icons[index] = data.icon;
            customObjects[index] = data.gameObject;
            cellTypes[index] = data.type == ListViewCellType.GameObject ? ListViewCellType.GameObject : ListViewCellType.None;
            drawBackgrounds[index] = data.drawBackground;
        }

        /// <summary>
        /// Changes a cell preset without replacing its stored label, icon, or source object.
        /// </summary>
        public void SetCellType(int index, ListViewCellType type)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureCapacity(index + 1);
            cellTypes[index] = type == ListViewCellType.GameObject ? ListViewCellType.GameObject : ListViewCellType.None;
        }

        /// <summary>
        /// Shows or hides the ListView background behind the specified GameObject cell.
        /// </summary>
        public void SetDrawBackground(int index, bool drawBackground)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureCapacity(index + 1);
            drawBackgrounds[index] = drawBackground;
        }

        /// <summary>
        /// Sets legacy cell fields and selects GameObject when <paramref name="obj"/> is assigned; otherwise None.
        /// </summary>
        public void SetCell(int index, string value, Sprite icon = null, GameObject obj = null)
        {
            SetCell(index, new ListViewCellData(obj != null ? ListViewCellType.GameObject : ListViewCellType.None,
                value, icon, obj));
        }

        /// <summary>
        /// Gets the cell preset at the specified column index.
        /// </summary>
        public ListViewCellType GetCellType(int index)
        {
            if (index >= 0 && index < cellTypes.Count)
                return cellTypes[index] == ListViewCellType.GameObject ? ListViewCellType.GameObject : ListViewCellType.None;

            return GetCustomObject(index) != null ? ListViewCellType.GameObject : ListViewCellType.None;
        }

        /// <summary>
        /// Gets the icon at the specified column index, or null when unavailable.
        /// </summary>
        public Sprite GetIcon(int index) => index >= 0 && index < icons.Count ? icons[index] : null;

        /// <summary>
        /// Gets the source object at the specified column index, or null when unavailable.
        /// </summary>
        public GameObject GetCustomObject(int index) => index >= 0 && index < customObjects.Count ? customObjects[index] : null;

        /// <summary>
        /// Gets whether the ListView background is rendered for the specified cell.
        /// </summary>
        public bool GetDrawBackground(int index) => drawBackgrounds == null || index < 0 || index >= drawBackgrounds.Count || drawBackgrounds[index];

        internal string GetOrCreateId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");

            return id;
        }

        internal void RegenerateId() => id = Guid.NewGuid().ToString("N");

        internal void Normalize(int count)
        {
#if EVO_LOCALIZATION
            tableKeys ??= new List<string>();
            while (tableKeys.Count < count)
                tableKeys.Add(string.Empty);
#endif
            values ??= new List<string>();
            icons ??= new List<Sprite>();
            customObjects ??= new List<GameObject>();
            cellTypes ??= new List<ListViewCellType>();
            drawBackgrounds ??= new List<bool>();

            while (values.Count < count)
                values.Add(string.Empty);

            while (icons.Count < count)
                icons.Add(null);

            while (customObjects.Count < count)
                customObjects.Add(null);

            while (drawBackgrounds.Count < count)
                drawBackgrounds.Add(true);

            while (cellTypes.Count < count)
            {
                int index = cellTypes.Count;
                cellTypes.Add(index < customObjects.Count && customObjects[index] != null
                    ? ListViewCellType.GameObject
                    : ListViewCellType.None);
            }

            for (int i = 0; i < cellTypes.Count; i++)
            {
                if (cellTypes[i] != ListViewCellType.GameObject)
                    cellTypes[i] = ListViewCellType.None;
            }

#if EVO_LOCALIZATION
            Trim(tableKeys, count);
#endif
            Trim(values, count);
            Trim(icons, count);
            Trim(customObjects, count);
            Trim(cellTypes, count);
            Trim(drawBackgrounds, count);
        }

        void EnsureCapacity(int count)
        {
#if EVO_LOCALIZATION
            tableKeys ??= new List<string>();
            while (tableKeys.Count < count)
                tableKeys.Add(string.Empty);
#endif
            values ??= new List<string>();
            icons ??= new List<Sprite>();
            customObjects ??= new List<GameObject>();
            cellTypes ??= new List<ListViewCellType>();
            drawBackgrounds ??= new List<bool>();

            while (values.Count < count)
                values.Add(string.Empty);

            while (icons.Count < count)
                icons.Add(null);

            while (customObjects.Count < count)
                customObjects.Add(null);

            while (cellTypes.Count < count)
                cellTypes.Add(ListViewCellType.None);

            while (drawBackgrounds.Count < count)
                drawBackgrounds.Add(true);
        }

        static void Trim<T>(List<T> list, int count)
        {
            if (list.Count > count)
                list.RemoveRange(count, list.Count - count);
        }
    }
}