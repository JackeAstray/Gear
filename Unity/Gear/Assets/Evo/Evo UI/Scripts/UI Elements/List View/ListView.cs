using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Renders tabular UI data with reusable procedural cells and incremental editor/runtime updates.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/List View"), HelpURL(Constants.HelpUrl + "ui-elements/list-view")]
    public partial class ListView : MonoBehaviour, IStylerHandler
    {
        // Content
        [Tooltip("Column definitions that control headers, widths, and cell alignment.")]
        public List<ListViewColumn> columns = new();
        [Tooltip("Row data displayed by the ListView.")]
        public List<ListViewRow> rows = new();

        // Style Settings
        [Tooltip("Visual and layout settings used to render the ListView.")]
        public ListViewStyle style = new();
        [Tooltip("Determines whether colors and fonts use local values or a Styler preset.")]
        public StyleProvider styleProvider = StyleProvider.Styler;
        [Tooltip("Maps ListView style values to items in the assigned Styler preset.")]
        public List<ListViewStyle.Mapping> styleMapping = new();
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        [Tooltip("Styler preset used when Styling Source is set to Styler Preset.")]
        public StylerPreset stylerPreset;

        // Scroll View
        [UnityEngine.Serialization.FormerlySerializedAs("enableVerticalScroll")]
        [UnityEngine.Serialization.FormerlySerializedAs("enableScrolling")]
        [Tooltip("Automatic shares the List View's height between the rows, and nothing scrolls. Fixed keeps Row Height " +
            "and column widths, and scrolls the rows and columns that don't fit.")]
        public ScaleMode scaleMode = ScaleMode.Fixed;
        [Tooltip("Gives cells only to the rows in view and passes them on while scrolling, so long lists stay light. " +
            "Rows out of view have no cell objects.")]
        public bool virtualizeRows = false;
        [Min(0), Tooltip("Scroll-wheel and trackpad sensitivity.")]
        public float scrollSensitivity = 10;
        public ScrollRect.MovementType movementType = ScrollRect.MovementType.Clamped;
        [Min(0)] public float elasticity = 0.1f;
        public bool inertia = false;
        [Min(0)] public float decelerationRate = 0.135f;
        [Tooltip("Smooths mouse wheel, trackpad, and navigation scrolling. Dragging stays immediate.")]
        public bool enableSmoothScroll = true;
        [Range(0.01f, 0.5f), Tooltip("Scroll response time in unscaled seconds. Lower values respond faster.")]
        public float smoothScrollTime = 0.08f;
        [Tooltip("Optional external scrollbar for the rows.")]
        public Scrollbar verticalScrollbar;
        [Tooltip("Optional external scrollbar for the columns.")]
        public Scrollbar horizontalScrollbar;
        [Tooltip("Shows each scrollbar only while its axis has something to scroll.")]
        public bool autoHideScrollbars = false;

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Events
        [Tooltip("Invoked when a generated header is clicked. Header clicks use row index -1.")]
        public UnityEvent<int, int> onCellClicked = new();
        [Tooltip("Invoked when a row gets cells it didn't have: on first draw, for new rows, and with Virtualize Rows " +
            "each time a row scrolls into view. Use it to set per-row state on custom objects.")]
        public UnityEvent<int> onRowShown = new();

        // Serialized generated references let an editor-built view be reused in play mode
        [SerializeField, HideInInspector] string textProvider;
        [SerializeField, HideInInspector] RectTransform generatedRoot;
        [SerializeField, HideInInspector] RectTransform headerContainer;
        [SerializeField, HideInInspector] RectTransform rowContainer;
        [SerializeField, HideInInspector] GameObject scrollViewObject;
        [SerializeField, HideInInspector] RectTransform headerViewport;
        [SerializeField, HideInInspector] RectTransform viewport;
        [SerializeField, HideInInspector] ScrollRect scrollRect;

        // Constants
        const string GeneratedRootName = "Content [Generated]";
        const string HeaderName = "Header [Generated]";
        const string RowsName = "Rows [Generated]";
        const int CellRenderingVersion = 3;
        const int HashSeed = unchecked((int)2166136261);

        // Enums
        public enum ScaleMode
        {
            Automatic = 0,
            Fixed = 1
        }

        // State
        bool refreshing;
        bool refreshQueued;
        float[] columnWidths = Array.Empty<float>();
        float[] columnLefts = Array.Empty<float>();
        float columnsWidth;
        float automaticRowHeight;
        readonly Dictionary<string, ListViewCell> cellCache = new();
        readonly List<ListViewCell> staleCells = new();
        readonly HashSet<ListViewCell> usedCells = new();
        readonly HashSet<string> ids = new();
        readonly List<int> shownRows = new();
        ListViewCell[] rowCells = Array.Empty<ListViewCell>();
        string[] rowKeys = Array.Empty<string>();
        string[] headerKeys = Array.Empty<string>();
        string[] headerKeyIds = Array.Empty<string>();
        bool notifyingRows;
        [HideInInspector] public bool isDirty = false;

        // Properties
        public int RowCount => rows?.Count ?? 0;
        public int ColumnCount => columns?.Count ?? 0;

        // ExecuteAlways also sends messages in edit mode, and while the Editor plays, to objects open in Prefab Mode.
        // Play logic runs only in a playing scene. Edit mode refreshes from OnValidate and size changes instead.
        bool Playing => Application.IsPlaying(gameObject);

        void Awake()
        {
            if (Playing)
                Initialize();
        }

        void Start()
        {
            if (Playing)
                Refresh();
        }

        void OnEnable()
        {
            if (!Playing)
            {
#if UNITY_EDITOR
                CaptureUndoGroup();

                // OnValidate skips inactive lists, and Unity can validate one while an undo has it inactive
                if (isDirty && autoRefresh && !Application.isPlaying)
                    RequestRefresh();
#endif
                return;
            }

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization, refreshImmediately: false);
#endif

            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            Styler.Register(this);
            Refresh();
        }

        void OnDisable()
        {
            if (!Playing)
                return;

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif

            Styler.Unregister(this);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedRefresh;
            pendingRedraw = false;
#endif
        }

#if UNITY_EDITOR
        // Removing the component in edit mode also removes its generated content, in the same undo step when the user
        // removed it. Hierarchy changes inside OnDestroy break deletion and undo, so this runs on the next editor tick.
        void OnDestroy()
        {
            if (Playing || UnityEditor.EditorUtility.IsPersistent(this) || !IsOwnedTransform(generatedRoot))
                return;

            GameObject content = generatedRoot.gameObject;
            ListView owner = this;
            int group = UnityEditor.Undo.isProcessing ? -1 : UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.EditorApplication.delayCall += Cleanup;

            void Cleanup()
            {
                if (UnityEditor.Undo.isProcessing)
                {
                    UnityEditor.EditorApplication.delayCall += Cleanup;
                    return;
                }

                // Deleted objects and closed scenes take the content with them
                if (owner != null || content == null || !content.scene.IsValid() || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                if (group >= 0 && group == UnityEditor.Undo.GetCurrentGroup() && !string.IsNullOrEmpty(UnityEditor.Undo.GetCurrentGroupName()))
                    UnityEditor.Undo.DestroyObjectImmediate(content);
                else
                    DestroyImmediate(content);
            }
        }
#endif

#if EVO_LOCALIZATION
        string LocalizeLabel(string key, string fallback) => Application.IsPlaying(gameObject) && enableLocalization
            && localizedObject != null ? localizedObject.GetString(key) ?? fallback : fallback;

        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (enableLocalization && localizedObject != null)
                Refresh();
        }
#endif

        void OnRectTransformDimensionsChange()
        {
            if (refreshing || !isActiveAndEnabled)
                return;

#if UNITY_EDITOR
            // Edit mode follows on the next editor tick. Prefab Mode while playing stays as it is.
            if (!Playing)
            {
                if (autoRefresh && !Application.isPlaying)
                    RequestRefresh();

                return;
            }
#endif
            // The cells there are follow the new size right away, which a parent's layout pass allows too. A refresh
            // follows only when rows come into view or a scroll axis turns on or off.
            if (isDirty || !LayoutCells())
                RequestRefresh();
        }

        bool EnsureGeneratedHierarchy()
        {
            bool changed = false;

            if (!IsOwnedTransform(generatedRoot))
                generatedRoot = FindDirectChild((RectTransform)transform, GeneratedRootName);

            if (generatedRoot == null)
            {
                RemoveLegacyPreviewHierarchy();
                generatedRoot = CreateRect(GeneratedRootName, transform);
                generatedRoot.gameObject.hideFlags = HideFlags.HideInHierarchy;
                changed = true;
            }

            Stretch(generatedRoot);
            changed |= EnsureContainer(ref headerContainer, HeaderName);
            changed |= EnsureContainer(ref rowContainer, RowsName);
            changed |= EnsureScrollView();
            return changed;
        }

        bool EnsureContainer(ref RectTransform container, string name)
        {
            if (!IsDescendant(container, generatedRoot))
                container = FindDescendant(generatedRoot, name);

            if (container != null)
                return false;

            container = CreateRect(name, generatedRoot);
            return true;
        }

        bool ReconcileCells()
        {
            bool changed = false;
            cellCache.Clear();
            staleCells.Clear();
            usedCells.Clear();
            CollectExistingCells(headerContainer);

            // Virtualized rows are drawn after the scroll view is sized. The other mode's row map is dropped here.
            if (!VirtualizesRows)
            {
                ResetRowMap();
                CollectExistingCells(rowContainer);
            }

            CalculateColumnWidths();
            CalculateRowHeight();
            Color borderColor = GetStyleColor(ListViewStyle.Type.BorderColor, style.borderColor);

            if (headerKeys.Length != columns.Count)
            {
                headerKeys = new string[columns.Count];
                headerKeyIds = new string[columns.Count];
            }

            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                ListViewColumn column = columns[columnIndex];
                string label = column.columnName;
#if EVO_LOCALIZATION
                label = LocalizeLabel(column.tableKey, label);
#endif
                string key = HeaderKey(columnIndex);
                ListViewCell cell = GetOrCreateCell(key, headerContainer, -1, columnIndex, ref changed);
                cell.Bind(this, key, -1, columnIndex);
                PlaceCell(cell, columnIndex);

                Color backgroundColor = GetStyleColor(ListViewStyle.Type.HeaderBackgroundColor, style.headerBackgroundColor);
                Color textColor = GetStyleColor(ListViewStyle.Type.HeaderTextColor, style.headerTextColor);
                UnityEngine.Object font = GetStyleFont(ListViewStyle.Type.HeaderFont, style.headerFont);
                Vector4 cornerRadii = GetCellCornerRadii(-1, columnIndex);
                int contentHash = CalculateContentHash(ListViewCellType.None, label, column.alignment,
                    textColor, style.headerFontSize, style.headerFontStyle);
                int visualHash = CalculateVisualHash(backgroundColor, borderColor, cornerRadii, true, true);

                cell.Apply(ListViewCellType.None, label, column.columnIcon, null, true, true, column.alignment, backgroundColor,
                    borderColor, cornerRadii, textColor, font, style.headerFontSize, style.headerFontStyle, style, contentHash, visualHash);
                usedCells.Add(cell);
            }

            if (!VirtualizesRows)
            {
                if (rowCells.Length != columns.Count)
                {
                    rowCells = new ListViewCell[columns.Count];
                    rowKeys = new string[columns.Count];
                }

                ids.Clear();
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    ListViewRow row = CheckRow(rowIndex);
                    string rowId = GetUniqueId(row);
                    for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        rowKeys[columnIndex] = RowKey(rowId, columns[columnIndex].GetOrCreateId());
                        rowCells[columnIndex] = GetOrCreateCell(rowKeys[columnIndex], rowContainer, rowIndex, columnIndex, ref changed);
                        usedCells.Add(rowCells[columnIndex]);
                    }

                    DrawRow(row, rowIndex, rowCells, rowKeys, rowIndex * columns.Count);
                }

                ids.Clear();
            }

            foreach (ListViewCell cell in staleCells)
            {
                if (cell != null && !usedCells.Contains(cell))
                {
                    cell.PrepareForRemoval();
                    DestroyGeneratedObject(cell.gameObject);
                    changed = true;
                }
            }

            foreach (var pair in cellCache)
            {
                if (pair.Value != null && !usedCells.Contains(pair.Value))
                {
                    pair.Value.PrepareForRemoval();
                    DestroyGeneratedObject(pair.Value.gameObject);
                    changed = true;
                }
            }

            cellCache.Clear();

            foreach (ListViewCell cell in usedCells)
                cellCache[cell.BindingKey] = cell;

            usedCells.Clear();
            return changed;
        }

        // Draws one row checked by CheckRow. Keyed cells follow their row's id, and virtualized cells (empty keys) follow
        // the row index. A negative sibling index leaves the cell order alone.
        void DrawRow(ListViewRow row, int rowIndex, ListViewCell[] cells, string[] keys, int siblingIndex)
        {
            Color backgroundColor = GetRowBackgroundColor(rowIndex);
            Color textColor = GetStyleColor(ListViewStyle.Type.RowTextColor, style.rowTextColor);
            Color borderColor = GetStyleColor(ListViewStyle.Type.BorderColor, style.borderColor);
            UnityEngine.Object font = GetStyleFont(ListViewStyle.Type.RowFont, style.rowFont);
            bool playing = Application.isPlaying;
            bool shown = false;

            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                ListViewCell cell = cells[columnIndex];
                ListViewColumn column = columns[columnIndex];
                ListViewCellData data = row.GetCell(columnIndex);
                string label = data.label;
#if EVO_LOCALIZATION
                if (data.type != ListViewCellType.GameObject)
                    label = LocalizeLabel(row.tableKeys[columnIndex], label);
#endif
                cell.Bind(this, keys != null ? keys[columnIndex] : string.Empty, rowIndex, columnIndex);
                PlaceCell(cell, siblingIndex < 0 ? -1 : siblingIndex + columnIndex);

                Vector4 cornerRadii = GetCellCornerRadii(rowIndex, columnIndex);
                int contentHash = CalculateContentHash(data.type, label, column.alignment,
                    textColor, style.rowFontSize, style.rowFontStyle);
                bool drawBackground = data.type != ListViewCellType.GameObject || data.drawBackground;
                int visualHash = CalculateVisualHash(backgroundColor, borderColor, cornerRadii, false, drawBackground);

                cell.Apply(data.type, label, data.icon, data.gameObject, false, drawBackground, column.alignment, backgroundColor,
                    borderColor, cornerRadii, textColor, font, style.rowFontSize, style.rowFontStyle, style, contentHash, visualHash);

                // Tracked in play mode only, so cells drawn in edit mode still count as new when play mode keeps them
                if (playing)
                {
                    shown |= cell.ShownRow != row;
                    cell.ShownRow = row;
                }
            }

            if (shown)
                shownRows.Add(rowIndex);
        }

        // Puts a bound cell at its row and column. Header cells use row -1.
        void PlaceCell(ListViewCell cell, int siblingIndex)
        {
            int rowIndex = cell.RowIndex;
            int columnIndex = cell.ColumnIndex;
            cell.SetRect(columnLefts[columnIndex], rowIndex < 0 ? 0 : RowTop(rowIndex), columnWidths[columnIndex],
                rowIndex < 0 ? style.headerHeight : RowHeight, siblingIndex);
        }

        // Moves the cells there are to the list's size without adding, removing, or redrawing any, so it can run inside
        // a parent's layout pass. Returns false when a refresh is needed: rows came into view or a scroll axis changed.
        bool LayoutCells()
        {
            if (rowContainer == null || headerContainer == null || scrollRect == null || (VirtualizesRows && mappedColumns != columns.Count))
                return false;

            refreshing = true;
            try
            {
                CalculateColumnWidths();
                CalculateRowHeight();
                foreach (ListViewCell cell in cellCache.Values)
                    PlaceLiveCell(cell);

                foreach (ListViewCell[] cells in liveRows.Values)
                {
                    for (int i = 0; i < cells.Length; i++)
                        PlaceLiveCell(cells[i]);
                }

                LayoutScrollView(out bool scrollX, out bool scrollY);
                SyncHeader();

                if (scrollX != scrollRect.horizontal || scrollY != scrollRect.vertical)
                    return false;

                if (!VirtualizesRows)
                    return true;

                GetLiveRange(out int first, out int last);
                return first == firstLiveRow && last == lastLiveRow;
            }
            finally
            {
                refreshing = false;
            }

            void PlaceLiveCell(ListViewCell cell)
            {
                if (cell != null && cell.ColumnIndex < columnWidths.Length)
                    PlaceCell(cell, -1);
            }
        }

        void NotifyShownRows()
        {
            // Listeners may refresh the list. Rows they add are sent by this loop, not by a nested one.
            if (notifyingRows)
                return;

            notifyingRows = true;
            try
            {
                for (int i = 0; i < shownRows.Count; i++)
                    onRowShown.Invoke(shownRows[i]);
            }
            finally
            {
                shownRows.Clear();
                notifyingRows = false;
            }
        }

        void CollectExistingCells(RectTransform container)
        {
            if (container == null)
                return;

            for (int i = 0; i < container.childCount; i++)
            {
                if (!container.GetChild(i).TryGetComponent(out ListViewCell cell))
                    continue;

                string key = cell.BindingKey;
                if (string.IsNullOrEmpty(key) || cellCache.ContainsKey(key))
                    staleCells.Add(cell);
                else
                    cellCache.Add(key, cell);
            }
        }

        ListViewCell GetOrCreateCell(string key, RectTransform parent, int rowIndex, int columnIndex, ref bool changed)
        {
            if (cellCache.TryGetValue(key, out ListViewCell cell) && cell != null)
            {
                if (cell.transform.parent != parent)
                {
                    cell.transform.SetParent(parent, false);
                    changed = true;
                }

                return cell;
            }

            changed = true;

            // Cells without a key, such as those left by Virtualize Rows, are reused before new ones are made
            int stale = FindStaleCell(parent, rowIndex, columnIndex);
            if (stale < 0)
                return CreateCell(parent, rowIndex < 0);

            cell = staleCells[stale];
            staleCells.RemoveAt(stale);
            if (!cell.gameObject.activeSelf)
                cell.gameObject.SetActive(true);

            return cell;
        }

        // Prefers the cell that showed the same row and column, so a prefab instance gets its own cells back
        int FindStaleCell(RectTransform parent, int rowIndex, int columnIndex)
        {
            int match = -1;
            for (int i = staleCells.Count - 1; i >= 0; i--)
            {
                ListViewCell cell = staleCells[i];
                if (cell == null || cell.transform.parent != parent)
                    continue;

                if (cell.RowIndex == rowIndex && cell.ColumnIndex == columnIndex)
                    return i;

                if (match < 0)
                    match = i;
            }

            return match;
        }

        static ListViewCell CreateCell(RectTransform parent, bool header)
        {
            var cellObject = new GameObject(header ? "Header Cell [ListView]" : "Cell [ListView]", typeof(RectTransform));
            cellObject.transform.SetParent(parent, false);
            cellObject.AddComponent<ProceduralShape>();
            ListViewCell cell = cellObject.AddComponent<ListViewCell>();
            cell.Invalidate();
            return cell;
        }

        // Fills the width and left edge of each column
        void CalculateColumnWidths()
        {
            if (columnWidths.Length != columns.Count)
            {
                columnWidths = new float[columns.Count];
                columnLefts = new float[columns.Count];
            }

            float[] widths = columnWidths;
            columnsWidth = 0;
            if (widths.Length == 0)
                return;

            float containerWidth = generatedRoot != null ? generatedRoot.rect.width : ((RectTransform)transform).rect.width;
            float totalSpacing = style.columnSpacing * (widths.Length - 1);
            float remaining = containerWidth - totalSpacing;
            int flexibleCount = 0;

            // Flexible columns are marked with -1 until they get their share
            for (int i = 0; i < widths.Length; i++)
            {
                if (columns[i].useFlexibleWidth)
                {
                    widths[i] = -1;
                    flexibleCount++;
                }
                else
                {
                    widths[i] = Mathf.Max(0, columns[i].width);
                    remaining -= widths[i];
                }
            }

            // In Fixed mode, a flexible column's width is its minimum. Columns whose minimum
            // is above an equal share keep it, and the others split what's left.
            if (IsScrollable)
            {
                bool locked = true;
                while (locked && flexibleCount > 0)
                {
                    locked = false;
                    float share = remaining / flexibleCount;

                    for (int i = 0; i < widths.Length; i++)
                    {
                        float minimum = Mathf.Max(0, columns[i].width);
                        if (widths[i] >= 0 || minimum <= share)
                            continue;

                        widths[i] = minimum;
                        remaining -= minimum;
                        flexibleCount--;
                        locked = true;
                    }
                }
            }

            float flexibleWidth = flexibleCount > 0 ? Mathf.Max(0, remaining) / flexibleCount : 0;
            float x = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (widths[i] < 0)
                    widths[i] = flexibleWidth;

                columnsWidth += widths[i];
                columnLefts[i] = x;
                x += widths[i] + style.columnSpacing;
            }

            columnsWidth += totalSpacing;
        }

        // Fixed rows keep Row Height. Automatic rows share the height under the header, set by CalculateRowHeight.
        float RowHeight => scaleMode == ScaleMode.Fixed ? Mathf.Max(0, style.rowHeight) : automaticRowHeight;

        // Each row and the spacing above it gets an equal share, so the last row ends at the List View's bottom edge.
        // The List View's size comes from its anchors or a parent layout, and it never changes it.
        void CalculateRowHeight()
        {
            if (scaleMode == ScaleMode.Fixed || rows.Count == 0)
                return;

            float height = generatedRoot != null ? generatedRoot.rect.height : ((RectTransform)transform).rect.height;
            automaticRowHeight = Mathf.Max(0, (height - Mathf.Max(0, style.headerHeight)) / rows.Count - style.rowSpacing);
        }

        float CalculateRowsHeight()
        {
            if (rows.Count == 0)
                return 0;

            return Mathf.Max(0, rows.Count * RowHeight + (rows.Count - 1) * style.rowSpacing);
        }

        // Cells save these hashes, so they must match across sessions. System.HashCode is seeded per domain and
        // object hashes are instance IDs, so this uses FNV-1a over values. Cells compare icons, fonts, and objects directly.
        int CalculateContentHash(ListViewCellType type, string text, TextAnchor alignment, Color textColor, int fontSize, FontStyles fontStyle)
        {
            int hash = Hash(HashSeed, LabelBackend.Id);
            hash = Hash(hash, (int)type);
            hash = Hash(hash, text);
            hash = Hash(hash, (int)alignment);
            hash = Hash(hash, textColor);
            hash = Hash(hash, fontSize);
            hash = Hash(hash, (int)fontStyle);
            hash = Hash(hash, style.iconSize);
            hash = Hash(hash, style.contentSpacing);
            hash = Hash(hash, style.contentPadding?.left ?? 0);
            hash = Hash(hash, style.contentPadding?.right ?? 0);
            hash = Hash(hash, style.contentPadding?.top ?? 0);
            return Hash(hash, style.contentPadding?.bottom ?? 0);
        }

        int CalculateVisualHash(Color backgroundColor, Color borderColor, Vector4 cornerRadii, bool interactive, bool drawBackground)
        {
            int hash = Hash(HashSeed, backgroundColor);
            hash = Hash(hash, borderColor);
            hash = Hash(hash, cornerRadii);
            hash = Hash(hash, style.independentCorners);
            hash = Hash(hash, (int)style.radiusMode);
            hash = Hash(hash, style.squircleCorners);
            hash = Hash(hash, style.shapeSoftness);
            hash = Hash(hash, style.showBorder);
            hash = Hash(hash, style.borderWidth);
            hash = Hash(hash, interactive);
            hash = Hash(hash, drawBackground);
            return Hash(hash, CellRenderingVersion);
        }

        static int Hash(int hash, int value) => unchecked((hash ^ value) * 16777619);
        static int Hash(int hash, float value) => Hash(hash, value.GetHashCode());
        static int Hash(int hash, bool value) => Hash(hash, value ? 1 : 0);
        static int Hash(int hash, Color value) => Hash(Hash(Hash(Hash(hash, value.r), value.g), value.b), value.a);
        static int Hash(int hash, Vector4 value) => Hash(Hash(Hash(Hash(hash, value.x), value.y), value.z), value.w);

        static int Hash(int hash, string value)
        {
            if (value == null)
                return Hash(hash, -1);

            hash = Hash(hash, value.Length);
            for (int i = 0; i < value.Length; i++)
                hash = Hash(hash, (int)value[i]);

            return hash;
        }

        // Checks every row. A long list would make each change slow if this ran per refresh, so it runs only where data
        // can come in unchecked: inspector edits and undo (OnValidate), loading (Initialize), and column changes.
        void EnsureDataConsistency()
        {
            EnsureColumnConsistency();

            ids.Clear();
            for (int i = 0; i < rows.Count; i++)
                GetUniqueId(CheckRow(i));

            ids.Clear();
        }

        // Runs on every refresh, so it covers only what doesn't grow with the rows. Rows are checked as they're drawn.
        void EnsureColumnConsistency()
        {
            columns ??= new List<ListViewColumn>();
            rows ??= new List<ListViewRow>();
            style ??= new ListViewStyle();
            styleMapping ??= new List<ListViewStyle.Mapping>();
            InitializeStyleMappings();

            // The id set is reused, so a refresh doesn't allocate one per call
            ids.Clear();
            for (int i = 0; i < columns.Count; i++)
            {
                columns[i] ??= new ListViewColumn();
                string id = columns[i].GetOrCreateId();
                if (!ids.Add(id))
                {
                    columns[i].RegenerateId();
                    ids.Add(columns[i].GetOrCreateId());
                }
            }

            ids.Clear();
        }

        // Rows are checked when they're drawn, so with Virtualize Rows a change costs the same on any list length.
        // A null row (from code) is replaced, and the row's lists are matched to the columns.
        ListViewRow CheckRow(int rowIndex)
        {
            ListViewRow row = rows[rowIndex] ??= new ListViewRow();
            row.Normalize(columns.Count);
            return row;
        }

        // Cells without Virtualize Rows are keyed by row id, so a row copied with its id (duplicated in the inspector
        // or cloned in code) gets a new one. Callers clear ids first.
        string GetUniqueId(ListViewRow row)
        {
            string id = row.GetOrCreateId();
            if (ids.Add(id))
                return id;

            row.RegenerateId();
            id = row.GetOrCreateId();
            ids.Add(id);
            return id;
        }

        void InitializeStyleMappings()
        {
            styleMapping ??= new List<ListViewStyle.Mapping>();
            if (styleMapping.Count != 0)
                return;

            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.HeaderFont });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.RowFont });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.HeaderTextColor });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.RowTextColor });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.HeaderBackgroundColor });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.RowBackgroundColor });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.AlternatingRowColor });
            styleMapping.Add(new ListViewStyle.Mapping { type = ListViewStyle.Type.BorderColor });
        }

        void Changed()
        {
            isDirty = true;
#if UNITY_EDITOR
            if (!Application.isPlaying && !autoRefresh)
                return;
#endif
            RequestRefresh();
        }

        Color GetRowBackgroundColor(int rowIndex)
        {
            if (style.useAlternatingRowColor && rowIndex % 2 == 1)
                return GetStyleColor(ListViewStyle.Type.AlternatingRowColor, style.alternatingRowColor);

            return GetStyleColor(ListViewStyle.Type.RowBackgroundColor, style.rowBackgroundColor);
        }

        Vector4 GetCellCornerRadii(int rowIndex, int columnIndex)
        {
            Vector4 radii = style.GetCornerRadii();
            if (!style.overrideOuterCorners)
                return radii;

            // Header uses row index -1; a zero-height header leaves the first row on top
            int topRow = style.headerHeight > 0 || rows.Count == 0 ? -1 : 0;
            int bottomRow = rows.Count - 1;
            bool firstColumn = columnIndex == 0;
            bool lastColumn = columnIndex == columns.Count - 1;
            float outerRadius = Mathf.Max(0, style.outerCornerRadius);

            // Vector4 order: top left, top right, bottom right, bottom left
            if (rowIndex == topRow && firstColumn) { radii.x = outerRadius; }
            if (rowIndex == topRow && lastColumn) { radii.y = outerRadius; }
            if (rowIndex == bottomRow && lastColumn) { radii.z = outerRadius; }
            if (rowIndex == bottomRow && firstColumn) { radii.w = outerRadius; }
            return radii;
        }

        Color GetStyleColor(ListViewStyle.Type type, Color fallback)
        {
            if (styleProvider != StyleProvider.Styler || stylerPreset == null)
                return fallback;

            ListViewStyle.Mapping mapping = GetStyleMapping(type);
            if (mapping != null && !string.IsNullOrEmpty(mapping.colorID) &&
                stylerPreset.TryGetColor(mapping.colorID, out Color mappedColor))
            {
                return Styler.ApplyAlphaOverride(mappedColor, mapping.overrideAlpha, mapping.alphaOverride);
            }

            return Color.clear;
        }

        /// <summary>
        /// Text provider of generated labels, saved so builds match the editor.
        /// </summary>
        internal TextBackend LabelBackend => TextProvider.Resolve(ref textProvider, this);

        UnityEngine.Object GetStyleFont(ListViewStyle.Type type, UnityEngine.Object fallback)
        {
            if (styleProvider != StyleProvider.Styler || stylerPreset == null)
                return fallback;

            ListViewStyle.Mapping mapping = GetStyleMapping(type);
            return mapping != null && !string.IsNullOrEmpty(mapping.fontID) ? stylerPreset.GetFont(mapping.fontID, LabelBackend) : fallback;
        }

        ListViewStyle.Mapping GetStyleMapping(ListViewStyle.Type type)
        {
            for (int i = 0; i < styleMapping.Count; i++)
            {
                if (styleMapping[i] != null && styleMapping[i].type == type)
                    return styleMapping[i];
            }
            return null;
        }

        bool CanGeneratePreview()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                return false;
#endif
            return true;
        }

        void MarkHierarchyDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        void RequestRefresh()
        {
            if (this == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (pendingRedraw || !CanGeneratePreview())
                    return;

                pendingRedraw = true;
                UnityEditor.EditorApplication.delayCall += DelayedRefresh;
                return;
            }
#endif
            // Graphics can't be turned on or off while the canvas rebuilds, so the next LateUpdate refreshes instead
            if (IsCanvasRebuilding)
                refreshQueued = true;
            else
                Refresh();
        }

        static bool IsCanvasRebuilding => CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics();

        bool IsOwnedTransform(Transform candidate) => candidate != null && candidate.parent == transform;

        bool IsValidCellIndex(int rowIndex, int columnIndex)
        {
            return rowIndex >= 0 && rowIndex < rows.Count && columnIndex >= 0 && columnIndex < columns.Count;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        static RectTransform FindDirectChild(RectTransform parent, string name)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
            }

            return null;
        }

        static RectTransform FindDescendant(RectTransform parent, string name)
        {
            if (parent == null)
                return null;

            RectTransform[] descendants = parent.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != parent && descendants[i].name == name)
                    return descendants[i];
            }

            return null;
        }

        static bool IsDescendant(Transform child, Transform parent) => child != null && parent != null && child.IsChildOf(parent);

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(target); }
            else
            {
                if (target is GameObject gameObject)
                {
                    gameObject.SetActive(false);
                    gameObject.transform.SetParent(null, false);
                }

                Destroy(target);
            }
        }

        // Kept per column, so a refresh doesn't build a key string per column. A replaced id (undo, inspector edits,
        // RegenerateId) is a new string and rebuilds its key.
        string HeaderKey(int columnIndex)
        {
            string id = columns[columnIndex].GetOrCreateId();
            if (!ReferenceEquals(headerKeyIds[columnIndex], id))
            {
                headerKeyIds[columnIndex] = id;
                headerKeys[columnIndex] = "H:" + id;
            }

            return headerKeys[columnIndex];
        }

        static string RowKey(string rowId, string columnId) => "R:" + rowId + ":" + columnId;

        internal void NotifyCellClicked(int rowIndex, int columnIndex) => onCellClicked?.Invoke(rowIndex, columnIndex);

        ListViewCell FindRenderedCell(int rowIndex, int columnIndex)
        {
            if (VirtualizesRows)
                return liveRows.TryGetValue(rowIndex, out ListViewCell[] cells) && columnIndex < cells.Length ? cells[columnIndex] : null;

            string key = RowKey(rows[rowIndex].GetOrCreateId(), columns[columnIndex].GetOrCreateId());
            return cellCache.TryGetValue(key, out ListViewCell cell) ? cell : null;
        }

        ListViewCell GetRenderedCell(int rowIndex, int columnIndex)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return null;

            ListViewCell cell = FindRenderedCell(rowIndex, columnIndex);
            if (cell != null)
                return cell;

            // A pending refresh creates missing cells. Virtualized rows out of view have none.
            if (!VirtualizesRows || isDirty)
            {
                Refresh();
                cell = FindRenderedCell(rowIndex, columnIndex);
            }

            return cell;
        }

        static void RemoveAt<T>(List<T> list, int index)
        {
            if (list != null && index >= 0 && index < list.Count)
                list.RemoveAt(index);
        }

        #region Public API
        /// <summary>
        /// Ensures that the generated hierarchy exists. Existing cells and custom instances are adopted, not rebuilt.
        /// </summary>
        public void Initialize()
        {
            if (!CanGeneratePreview())
                return;

            EnsureDataConsistency();
            EnsureGeneratedHierarchy();
            isDirty = true;
        }

        /// <summary>
        /// Explicitly discards and recreates all generated objects.
        /// </summary>
        public void Rebuild()
        {
            if (!CanGeneratePreview())
                return;

            if (generatedRoot != null)
                DestroyGeneratedObject(generatedRoot.gameObject);

            generatedRoot = null;
            headerContainer = null;
            rowContainer = null;
            scrollViewObject = null;
            headerViewport = null;
            viewport = null;
            scrollRect = null;
            cellCache.Clear();
            ResetRowMap();
            Initialize();
            Refresh();
        }

        /// <summary>
        /// Reconciles the hierarchy with the current data. Only missing, removed, or changed cells are mutated.
        /// </summary>
        public void Refresh()
        {
            if (refreshing || this == null || gameObject == null || !CanGeneratePreview())
                return;

            if (Application.isPlaying && !gameObject.activeInHierarchy)
                return;

            if (IsCanvasRebuilding)
            {
                RequestRefresh();
                return;
            }

            refreshing = true;
            refreshQueued = false;
#if UNITY_EDITOR
            bool recordUndo = RecordsUndo();
            if (recordUndo)
                CollectHierarchy(existingObjects);
#endif
            try
            {
                EnsureColumnConsistency();
                bool hierarchyChanged = EnsureGeneratedHierarchy();
                hierarchyChanged |= ReconcileCells();
                UpdateScrollView();

                // Virtualized rows need the viewport and scroll position set above
                if (VirtualizesRows)
                    hierarchyChanged |= ReconcileLiveRows();

                isDirty = false;

                if (hierarchyChanged)
                    MarkHierarchyDirty();

#if UNITY_EDITOR
                if (recordUndo)
                    RecordCreatedObjects();
#endif
            }
            finally
            {
                refreshing = false;
            }

            NotifyShownRows();
        }

        /// <summary>
        /// Adds a column, normalizes existing rows, and refreshes the view.
        /// </summary>
        public void AddColumn(ListViewColumn column)
        {
            columns.Add(column ?? new ListViewColumn());
            EnsureDataConsistency();
            Changed();
        }

        /// <summary>
        /// Removes a column and its corresponding data from every row.
        /// </summary>
        public void RemoveColumn(int index)
        {
            if (index < 0 || index >= columns.Count)
                return;

            columns.RemoveAt(index);
            foreach (ListViewRow row in rows)
            {
                RemoveAt(row.values, index);
                RemoveAt(row.icons, index);
                RemoveAt(row.customObjects, index);
                RemoveAt(row.cellTypes, index);
                RemoveAt(row.drawBackgrounds, index);
#if EVO_LOCALIZATION
                RemoveAt(row.tableKeys, index);
#endif
            }

            Changed();
        }

        /// <summary>
        /// Adds a row, normalizes it to the current column count, and refreshes the view.
        /// </summary>
        public void AddRow(ListViewRow row)
        {
            row ??= new ListViewRow();
            row.Normalize(columns.Count);
            rows.Add(row);
            Changed();
        }

        /// <summary>
        /// Adds a row of cells from label values.
        /// </summary>
        public void AddRow(params string[] values) => AddRow(new ListViewRow(values));

        /// <summary
        /// >Removes the row at the specified index.
        /// </summary>
        public void RemoveRow(int index)
        {
            if (index < 0 || index >= rows.Count)
                return;

            rows.RemoveAt(index);
            Changed();
        }

        /// <summary>
        /// Removes all data rows while preserving column definitions.
        /// </summary>
        public void ClearRows()
        {
            if (rows.Count == 0)
                return;

            rows.Clear();
            Changed();
        }

        /// <summary>
        /// Changes a cell label without changing its preset, icon, or source object.
        /// </summary>
        public void SetCellValue(int rowIndex, int columnIndex, string value)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            ListViewCellData current = rows[rowIndex].GetCell(columnIndex);
            rows[rowIndex].SetCell(columnIndex, new ListViewCellData(current.type, value, current.icon, current.gameObject, current.drawBackground));
            Changed();
        }

        /// <summary>
        /// Sets the complete data for a cell.
        /// </summary>
        public void SetCellData(int rowIndex, int columnIndex, ListViewCellData data)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            rows[rowIndex].SetCell(columnIndex, data);
            Changed();
        }

        /// <summary>
        /// Changes a cell preset without replacing its stored content.
        /// </summary>
        public void SetCellType(int rowIndex, int columnIndex, ListViewCellType type)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            rows[rowIndex].SetCellType(columnIndex, type);
            Changed();
        }

        /// <summary>
        /// Shows or hides the ListView background behind a GameObject cell.
        /// </summary>
        public void SetCellDrawBackground(int rowIndex, int columnIndex, bool drawBackground)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            rows[rowIndex].SetDrawBackground(columnIndex, drawBackground);
            Changed();
        }

        /// <summary>
        /// Gets a cell label, or null when the indices are invalid.
        /// </summary>
        public string GetCellValue(int rowIndex, int columnIndex)
        {
            return IsValidCellIndex(rowIndex, columnIndex) ? rows[rowIndex].GetCell(columnIndex).label : null;
        }

        /// <summary>
        /// Gets a cell-data snapshot.
        /// </summary>
        public ListViewCellData GetCellData(int rowIndex, int columnIndex)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                throw new ArgumentOutOfRangeException();

            return rows[rowIndex].GetCell(columnIndex);
        }

        /// <summary>
        /// Attempts to get a cell-data snapshot without throwing for invalid indices.
        /// </summary>
        public bool TryGetCellData(int rowIndex, int columnIndex, out ListViewCellData data)
        {
            if (IsValidCellIndex(rowIndex, columnIndex))
            {
                data = rows[rowIndex].GetCell(columnIndex);
                return true;
            }

            data = default;
            return false;
        }

        /// <summary>
        /// Invalidates one rendered cell and schedules an incremental refresh.
        /// </summary>
        public void RefreshCell(int rowIndex, int columnIndex)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            ListViewCell cell = FindRenderedCell(rowIndex, columnIndex);
            if (cell != null)
                cell.Invalidate();

            Changed();
        }

        /// <summary>
        /// Gets the instantiated custom object rendered by a GameObject cell, or null for a row out of view while Virtualize Rows is on.
        /// </summary>
        public GameObject GetCellCustomInstance(int rowIndex, int columnIndex)
        {
            ListViewCell cell = GetRenderedCell(rowIndex, columnIndex);
            return cell != null ? cell.CustomObjectInstance : null;
        }

        /// <summary>
        /// Gets a component from the instantiated custom object rendered by a GameObject cell, or null for a row out of view
        /// while Virtualize Rows is on.
        /// </summary>
        public T GetCellCustomInstance<T>(int rowIndex, int columnIndex) where T : Component
        {
            GameObject instance = GetCellCustomInstance(rowIndex, columnIndex);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        /// <summary>
        /// Gets the generated root object for a rendered cell, or null for a row out of view while Virtualize Rows is on.
        /// </summary>
        public GameObject GetCellObject(int rowIndex, int columnIndex)
        {
            ListViewCell cell = GetRenderedCell(rowIndex, columnIndex);
            return cell != null ? cell.gameObject : null;
        }

        /// <summary>
        /// Enables or disables alternating row backgrounds and refreshes affected cells.
        /// </summary>
        public void UseAlternatingRowColor(bool value)
        {
            if (style.useAlternatingRowColor == value)
                return;

            style.useAlternatingRowColor = value;
            Changed();
        }
        #endregion

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            isDirty = true;
            RequestRefresh();
        }
        #endregion

        #region Backwards Compatibility
        /// <summary>
        /// Sets legacy cell fields and selects GameObject when a source object is assigned; otherwise None.
        /// </summary>
        public void SetCellData(int rowIndex, int columnIndex, string value, Sprite icon = null, GameObject customObject = null)
        {
            if (!IsValidCellIndex(rowIndex, columnIndex))
                return;

            rows[rowIndex].SetCell(columnIndex, value, icon, customObject);
            Changed();
        }

        void RemoveLegacyPreviewHierarchy()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                bool legacyName = child.name == "Content [Generated]" || child.name == "Header" ||
                    child.name == "Rows" || child.name == "Scroll View";
                bool wasTemporary = (child.gameObject.hideFlags & HideFlags.DontSave) != 0;

                if (legacyName && wasTemporary)
                    DestroyGeneratedObject(child.gameObject);
            }
        }
        #endregion

#if UNITY_EDITOR
        [Tooltip("Automatically updates the generated editor preview when serialized settings or the List View's size change.")]
        [HideInInspector] public bool autoRefresh = true;
        bool pendingRedraw;

        // Undoing the step that added the list, such as Add Component or Paste Component As New, restores its children
        // from that step and reports the ones it doesn't know as dangling. So objects a refresh creates in that step join it.
        // Data edits don't restore children, so their refreshes stay unrecorded.
        int undoGroup = -1;
        readonly HashSet<Transform> existingObjects = new();
        readonly List<Transform> hierarchyBuffer = new();

        void CaptureUndoGroup() => undoGroup = UnityEditor.Undo.isProcessing ? -1 : UnityEditor.Undo.GetCurrentGroup();

        // Only a group holding a user edit has a name; loads, undo and redo stay unrecorded so they add no undo step
        bool RecordsUndo() => !Application.isPlaying && !UnityEditor.Undo.isProcessing && undoGroup >= 0
            && undoGroup == UnityEditor.Undo.GetCurrentGroup() && !string.IsNullOrEmpty(UnityEditor.Undo.GetCurrentGroupName());

        void CollectHierarchy(HashSet<Transform> target)
        {
            target.Clear();
            GetComponentsInChildren(true, hierarchyBuffer);

            foreach (Transform item in hierarchyBuffer)
                target.Add(item);

            hierarchyBuffer.Clear();
        }

        // Registers the top-most new object of each branch, which records its children with it
        void RecordCreatedObjects()
        {
            // Text adds sub-mesh children when it first draws, so draw now to record those with their cells
            Canvas.ForceUpdateCanvases();

            string groupName = UnityEditor.Undo.GetCurrentGroupName();
            GetComponentsInChildren(true, hierarchyBuffer);

            foreach (Transform item in hierarchyBuffer)
            {
                if (!existingObjects.Contains(item) && existingObjects.Contains(item.parent))
                    UnityEditor.Undo.RegisterCreatedObjectUndo(item.gameObject, groupName);
            }

            hierarchyBuffer.Clear();
            existingObjects.Clear();
        }

        void OnValidate()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            InitializeStyleMappings();
            EnsureDataConsistency();
            isDirty = true;

            if (autoRefresh && gameObject.activeInHierarchy && !Application.isPlaying)
                RequestRefresh();
        }

        void Reset()
        {
            InitializeStyleMappings();
            inertia = true;
            autoHideScrollbars = true;
            useDefaultStylerPreset = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }

        void DelayedRefresh()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedRefresh;
            pendingRedraw = false;

            if (this != null)
                Refresh();
        }
#endif
    }
}