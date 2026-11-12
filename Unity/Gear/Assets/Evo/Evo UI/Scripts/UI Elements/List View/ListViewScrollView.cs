using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    public partial class ListView
    {
        // The header and rows always sit in their own viewports under one scroll view. Turning scrolling
        // on or off only switches components, so nothing is reparented, which prefab instances wouldn't allow.
        const string ScrollViewName = "Scroll View [Generated]";
        const string HeaderViewportName = "Header Viewport [Generated]";
        const string ViewportName = "Viewport [Generated]";

        bool scrollActive;
        ScrollRect boundScrollRect;
        UnityAction<Vector2> scrollChanged;
        GameObject lastSelected;

        /// <summary>
        /// The generated Scroll Rect that moves the rows. It's disabled while there's nothing to scroll.
        /// </summary>
        public ScrollRect ScrollRect => scrollRect;

        bool IsScrollable => scaleMode == ScaleMode.Fixed;

        bool CanScroll => IsScrollable && scrollRect != null && viewport != null && rowContainer != null;

        float HeaderGap => rows.Count > 0 ? style.rowSpacing : 0;

        // A negative header gap tucks the first row under the header instead of raising the viewport over it,
        // so scrolled rows never cover the header
        float RowTop(int rowIndex) => Mathf.Min(0, HeaderGap) + rowIndex * (RowHeight + style.rowSpacing);

        void LateUpdate()
        {
            // Set only in play mode, while the canvas was rebuilding
            if (refreshQueued)
                Refresh();

            if (!scrollActive || !Playing)
                return;

            // Keeps the selected cell in view, so keyboard and gamepad navigation can reach hidden rows
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (ReferenceEquals(selected, lastSelected))
                return;

            lastSelected = selected;
            ListViewCell cell = FindCell(selected);
            if (cell != null)
                EnsureVisible(cell.RowIndex, cell.ColumnIndex);
        }

        bool EnsureScrollView()
        {
            bool changed = false;

            if (scrollViewObject == null || scrollViewObject.transform.parent != generatedRoot)
            {
                RectTransform existing = FindDirectChild(generatedRoot, ScrollViewName);
                scrollViewObject = existing != null ? existing.gameObject : null;
            }

            if (scrollViewObject == null)
            {
                scrollViewObject = CreateRect(ScrollViewName, generatedRoot).gameObject;
                changed = true;
            }

            // Earlier versions caught scroll input with a transparent shape
            if (scrollViewObject.TryGetComponent(out ProceduralShape legacyShape))
            {
                DestroyImmediate(legacyShape);
                changed = true;
            }

            if (!scrollViewObject.TryGetComponent(out RaycastArea _))
            {
                scrollViewObject.AddComponent<RaycastArea>();
                changed = true;
            }

            if (!scrollViewObject.TryGetComponent(out scrollRect))
            {
                scrollRect = scrollViewObject.AddComponent<ScrollRect>();
                changed = true;
            }

            var scrollTransform = (RectTransform)scrollViewObject.transform;
            changed |= EnsureViewport(ref headerViewport, HeaderViewportName, scrollTransform, 0);
            changed |= EnsureViewport(ref viewport, ViewportName, scrollTransform, 1);
            changed |= MoveTo(headerContainer, headerViewport);
            changed |= MoveTo(rowContainer, viewport);
            return changed;
        }

        static bool EnsureViewport(ref RectTransform target, string name, RectTransform parent, int siblingIndex)
        {
            bool changed = false;

            if (target == null || target.parent != parent)
                target = FindDirectChild(parent, name);

            if (target == null)
            {
                target = CreateRect(name, parent);
                changed = true;
            }

            if (!target.TryGetComponent(out RectMask2D _))
            {
                target.gameObject.AddComponent<RectMask2D>();
                changed = true;
            }

            if (target.GetSiblingIndex() != siblingIndex)
                target.SetSiblingIndex(siblingIndex);

            return changed;
        }

        static bool MoveTo(Transform child, Transform parent)
        {
            if (child.parent == parent)
                return false;

            child.SetParent(parent, false);
            return true;
        }

        void UpdateScrollView()
        {
            LayoutScrollView(out bool scrollX, out bool scrollY);
            ConfigureScrollRect(scrollX, scrollY);
            SyncHeader();
        }

        // Sizes the viewports and containers, and gives the axes that can scroll. It turns nothing on or off, so it can
        // run inside a parent's layout pass.
        void LayoutScrollView(out bool scrollX, out bool scrollY)
        {
            float headerHeight = Mathf.Max(0, style.headerHeight);
            Stretch((RectTransform)scrollViewObject.transform);
            SetEdges(headerViewport, new Vector2(0, 1), new Vector2(0, -headerHeight), Vector2.zero);
            SetEdges(viewport, Vector2.zero, Vector2.zero, new Vector2(0, -headerHeight - Mathf.Max(0, HeaderGap)));

            Rect view = viewport.rect;
            bool scrollable = IsScrollable;
            float width = scrollable ? Mathf.Max(columnsWidth, view.width) : view.width;
            float height = Mathf.Max(0, CalculateRowsHeight() + Mathf.Min(0, HeaderGap));
            SetTopLeft(rowContainer, new Vector2(width, height));
            SetTopLeft(headerContainer, new Vector2(width, headerHeight));

            // An axis scrolls only while it overflows or keeps a permanent scrollbar. Then the wheel drives the
            // axis that can move, and a List View with nothing to scroll leaves input to its parents.
            scrollX = scrollable && (width > view.width + 0.01f || (!autoHideScrollbars && horizontalScrollbar != null));
            scrollY = scrollable && (height > view.height + 0.01f || (!autoHideScrollbars && verticalScrollbar != null));

            Vector2 position = rowContainer.anchoredPosition;
            if (!scrollX)
                position.x = 0;
            if (!scrollY)
                position.y = 0;

            rowContainer.anchoredPosition = position;
        }

        void ConfigureScrollRect(bool scrollX, bool scrollY)
        {
            bool scrollable = IsScrollable;
            SetMaskEnabled(headerViewport, scrollable);
            SetMaskEnabled(viewport, scrollable);

            // Cells aren't items to snap to or fade, so the Scroll Rect doesn't track them every frame
            scrollRect.ignoreContentItems = true;

            if (scrollRect.content != rowContainer)
                scrollRect.content = rowContainer;
            if (scrollRect.viewport != viewport)
                scrollRect.viewport = viewport;

            scrollRect.horizontal = scrollX;
            scrollRect.vertical = scrollY;
            scrollRect.movementType = movementType;
            scrollRect.elasticity = elasticity;
            scrollRect.inertia = inertia;
            scrollRect.decelerationRate = decelerationRate;
            scrollRect.scrollSensitivity = Mathf.Max(0, scrollSensitivity);
            scrollRect.EnableSmoothScroll = enableSmoothScroll;
            scrollRect.SmoothScrollTime = smoothScrollTime;

            Scrollbar horizontalBar = scrollable ? horizontalScrollbar : null;
            Scrollbar verticalBar = scrollable ? verticalScrollbar : null;
            ScrollRect.ScrollbarVisibility visibility = autoHideScrollbars
                ? ScrollRect.ScrollbarVisibility.AutoHide
                : ScrollRect.ScrollbarVisibility.Permanent;

            if (scrollRect.horizontalScrollbar != horizontalBar)
                scrollRect.horizontalScrollbar = horizontalBar;
            if (scrollRect.verticalScrollbar != verticalBar)
                scrollRect.verticalScrollbar = verticalBar;
            if (scrollRect.horizontalScrollbarVisibility != visibility)
                scrollRect.horizontalScrollbarVisibility = visibility;
            if (scrollRect.verticalScrollbarVisibility != visibility)
                scrollRect.verticalScrollbarVisibility = visibility;

            // In play mode the Scroll Rect runs only while an axis can move. The editor keeps it on for the preview.
            scrollActive = Application.isPlaying ? scrollX || scrollY : scrollable;
            if (scrollRect.enabled != scrollActive)
                scrollRect.enabled = scrollActive;

            if (scrollViewObject.TryGetComponent(out RaycastArea raycastArea))
                raycastArea.raycastTarget = scrollActive;

            // A disabled Scroll Rect stops updating its scrollbars, and Automatic mode has nothing to scroll
            if (!scrollActive && autoHideScrollbars && Application.isPlaying)
            {
                HideScrollbar(horizontalScrollbar);
                HideScrollbar(verticalScrollbar);
            }

            BindScrollEvents();
        }

        void BindScrollEvents()
        {
            if (ReferenceEquals(boundScrollRect, scrollRect))
                return;

            scrollChanged ??= _ =>
            {
                SyncHeader();
                UpdateLiveRows();
            };

            if (boundScrollRect != null)
                boundScrollRect.onValueChanged.RemoveListener(scrollChanged);

            scrollRect.onValueChanged.AddListener(scrollChanged);
            boundScrollRect = scrollRect;
        }

        // The header follows the rows sideways
        void SyncHeader()
        {
            if (headerContainer == null || rowContainer == null)
                return;

            var position = new Vector2(rowContainer.anchoredPosition.x, 0);
            if (headerContainer.anchoredPosition != position)
                headerContainer.anchoredPosition = position;
        }

        // The cell of this list that holds an object, such as the selection
        ListViewCell FindCell(GameObject target)
        {
            if (target == null || scrollViewObject == null)
                return null;

            Transform root = scrollViewObject.transform;
            if (!target.transform.IsChildOf(root))
                return null;

            for (Transform current = target.transform; current != root; current = current.parent)
            {
                if (current.TryGetComponent(out ListViewCell cell) && cell.Owner == this)
                    return cell;
            }

            return null;
        }

        void ScrollTo(Vector2 position, bool instant)
        {
            // Clamped like the Scroll Rect, so a smooth move doesn't stall at an edge
            Rect view = viewport.rect;
            Vector2 size = rowContainer.rect.size;
            position.x = Mathf.Clamp(position.x, Mathf.Min(0, view.width - size.x), 0);
            position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, size.y - view.height));

            if (!instant && enableSmoothScroll && Application.isPlaying && scrollRect.isActiveAndEnabled)
            {
                scrollRect.SmoothScrollTo(position);
                return;
            }

            scrollRect.StopMovement();
            rowContainer.anchoredPosition = position;
            SyncHeader();
            UpdateLiveRows();
        }

        // Moves a scroll offset as little as possible to show [start, start + size]
        static float Reveal(float offset, float start, float size, float viewSize)
        {
            if (start < offset || size > viewSize)
                return start;

            return Mathf.Max(offset, start + size - viewSize);
        }

        // Writes final values only, so a refresh never moves the viewport through a stretched state
        static void SetEdges(RectTransform rect, Vector2 anchorMin, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void SetTopLeft(RectTransform rect, Vector2 size)
        {
            var topLeft = new Vector2(0, 1);
            if (rect.anchorMin != topLeft || rect.anchorMax != topLeft || rect.pivot != topLeft)
            {
                rect.anchorMin = topLeft;
                rect.anchorMax = topLeft;
                rect.pivot = topLeft;
                rect.anchoredPosition = Vector2.zero;
            }

            rect.sizeDelta = size;
        }

        static void SetMaskEnabled(RectTransform target, bool value)
        {
            if (target.TryGetComponent(out RectMask2D mask) && mask.enabled != value)
                mask.enabled = value;
        }

        static void HideScrollbar(Scrollbar scrollbar)
        {
            if (scrollbar != null && scrollbar.gameObject.activeSelf)
                scrollbar.gameObject.SetActive(false);
        }

        #region Virtualized Rows
        // With Virtualize Rows, only rows near the viewport own cells. Rows that scroll out of range hand theirs to rows
        // that scroll in, so the object count stays fixed and a scrolled frame redraws only the rows that came in.
        const int OverscanRows = 1;

        readonly Dictionary<int, ListViewCell[]> liveRows = new();
        readonly Stack<ListViewCell[]> spareRows = new();
        readonly List<ListViewCell> looseCells = new();
        int mappedColumns = -1;
        int firstLiveRow;
        int lastLiveRow = -1;
        int heldRow = -1;
        int looseIndex;

        bool VirtualizesRows => virtualizeRows && IsScrollable;

        // Rows that overlap the viewport, plus overscan. The count stays the same while scrolling, so cells are only passed on.
        void GetLiveRange(out int first, out int last)
        {
            float rowHeight = RowHeight;
            float pitch = rowHeight + style.rowSpacing;
            if (pitch < 0.01f)
            {
                // Rows stacked on top of each other can't be culled
                first = 0;
                last = rows.Count - 1;
                return;
            }

            int capacity = Mathf.FloorToInt((Mathf.Max(0, viewport.rect.height) + rowHeight) / pitch) + 1 + OverscanRows * 2;
            first = Mathf.FloorToInt((rowContainer.anchoredPosition.y - RowTop(0) - rowHeight) / pitch) + 1 - OverscanRows;
            first = Mathf.Clamp(first, 0, Mathf.Max(0, rows.Count - capacity));
            last = Mathf.Min(rows.Count, first + capacity) - 1;
        }

        // The row holding the selection stays alive out of range, so its selected state and navigation stay with it
        int FindHeldRow(int first, int last)
        {
            EventSystem eventSystem = EventSystem.current;
            ListViewCell cell = FindCell(eventSystem != null ? eventSystem.currentSelectedGameObject : null);
            int row = cell != null ? cell.RowIndex : -1;
            return row >= 0 && row < rows.Count && (row < first || row > last) ? row : -1;
        }

        // The full pass, after data, style, or size changes. Rows that stay are drawn again, and their cells skip what didn't change.
        bool ReconcileLiveRows()
        {
            GetLiveRange(out int first, out int last);
            int held = FindHeldRow(first, last);

            if (mappedColumns != columns.Count)
                MapExistingCells(first, last, held);

            bool changed = ShowRange(first, last, held, true);

            // Cells the new map didn't need
            for (int i = 0; i < looseCells.Count; i++)
            {
                if (looseCells[i] != null)
                {
                    looseCells[i].PrepareForRemoval();
                    DestroyGeneratedObject(looseCells[i].gameObject);
                    changed = true;
                }
            }

            looseCells.Clear();
            return changed;
        }

        // Runs when the rows scroll. Rows that scrolled out of range hand their cells to rows that scrolled in.
        void UpdateLiveRows()
        {
            if (refreshing || !VirtualizesRows || mappedColumns != columns.Count || viewport == null || rowContainer == null)
                return;

            GetLiveRange(out int first, out int last);
            if (first == firstLiveRow && last == lastLiveRow)
                return;

            // Edit mode changes go through the full pass, which saves them. Cells can't be turned on or off while the
            // canvas rebuilds, so the pass waits for the next frame then.
            if (!Application.isPlaying || IsCanvasRebuilding)
            {
                RequestRefresh();
                return;
            }

            refreshing = true;
            try { ShowRange(first, last, FindHeldRow(first, last), false); }
            finally { refreshing = false; }

            NotifyShownRows();
        }

        // Builds the row map from the cells that exist: saved in a scene, left by the other mode, or from before a column
        // change. Cells keep a row that's still in range, so a saved scene opens unchanged, and the rest are handed out.
        void MapExistingCells(int first, int last, int held)
        {
            ResetRowMap();
            looseCells.Clear();
            looseIndex = 0;

            int columnCount = columns.Count;
            for (int i = 0; i < rowContainer.childCount; i++)
            {
                if (!rowContainer.GetChild(i).TryGetComponent(out ListViewCell cell))
                    continue;

                int row = cell.RowIndex;
                int column = cell.ColumnIndex;
                if (row >= 0 && (row >= first && row <= last || row == held) && column >= 0 && column < columnCount)
                {
                    if (!liveRows.TryGetValue(row, out ListViewCell[] cells))
                    {
                        cells = new ListViewCell[columnCount];
                        liveRows.Add(row, cells);
                    }

                    if (cells[column] == null)
                    {
                        cells[column] = cell;
                        continue;
                    }
                }

                looseCells.Add(cell);
            }

            mappedColumns = columnCount;
        }

        // Moves the live range: rows that left hand their cells to rows that came in. With redraw, rows that stay are drawn again.
        bool ShowRange(int first, int last, int held, bool redraw)
        {
            bool changed = false;
            bool fromTop = first < firstLiveRow;

            for (int row = firstLiveRow; row <= lastLiveRow; row++)
                ReleaseRow(row, first, last, held);

            if (heldRow >= 0)
                ReleaseRow(heldRow, first, last, held);

            for (int row = first; row <= last; row++)
            {
                if (redraw || !liveRows.ContainsKey(row))
                    changed |= ShowRow(row);
            }

            if (held >= 0 && (redraw || !liveRows.ContainsKey(held)))
                changed |= ShowRow(held);

            // Spare cells wait inactive for rows that scroll in. Nothing scrolls in edit mode, so they're removed there.
            foreach (ListViewCell[] cells in spareRows)
            {
                foreach (ListViewCell cell in cells)
                {
                    if (cell == null)
                        continue;

                    if (!Application.isPlaying)
                    {
                        cell.PrepareForRemoval();
                        DestroyGeneratedObject(cell.gameObject);
                        changed = true;
                    }
                    else if (cell.gameObject.activeSelf)
                    {
                        cell.gameObject.SetActive(false);
                    }
                }
            }

            if (!Application.isPlaying)
                spareRows.Clear();

            OrderLiveRows(first, last, fromTop);
            firstLiveRow = first;
            lastLiveRow = last;
            heldRow = held;
            return changed;
        }

        void ReleaseRow(int row, int first, int last, int held)
        {
            if ((row < first || row > last) && row != held && liveRows.Remove(row, out ListViewCell[] cells))
                spareRows.Push(cells);
        }

        // Gives a row a full set of cells and draws it. Returns whether cells were created.
        bool ShowRow(int row)
        {
            bool created = false;
            if (!liveRows.TryGetValue(row, out ListViewCell[] cells))
            {
                cells = spareRows.Count > 0 ? spareRows.Pop() : new ListViewCell[columns.Count];
                liveRows.Add(row, cells);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null)
                    continue;

                cells[i] = TakeLooseCell();
                if (cells[i] == null)
                {
                    cells[i] = CreateCell(rowContainer, false);
                    created = true;
                }
            }

            // Drawn before it's shown, so a spare builds its meshes once
            DrawRow(CheckRow(row), row, cells, null, -1);

            foreach (ListViewCell cell in cells)
            {
                if (!cell.gameObject.activeSelf)
                    cell.gameObject.SetActive(true);
            }

            return created;
        }

        ListViewCell TakeLooseCell()
        {
            while (looseIndex < looseCells.Count)
            {
                ListViewCell cell = looseCells[looseIndex];
                looseCells[looseIndex++] = null;

                if (cell != null)
                    return cell;
            }

            return null;
        }

        // Later rows draw over earlier ones, as without virtualization. Cells move only when out of order: rows that came in
        // at the top move to the front, and the others to the back.
        void OrderLiveRows(int first, int last, bool fromTop)
        {
            int columnCount = columns.Count;
            int previous = fromTop ? int.MaxValue : -1;

            for (int i = first; i <= last; i++)
            {
                ListViewCell[] cells = liveRows[fromTop ? first + last - i : i];
                for (int j = 0; j < columnCount; j++)
                {
                    Transform cell = cells[fromTop ? columnCount - 1 - j : j].transform;
                    int index = cell.GetSiblingIndex();

                    if (fromTop && index > previous)
                    {
                        cell.SetAsFirstSibling();
                        index = 0;
                    }
                    else if (!fromTop && index < previous)
                    {
                        cell.SetAsLastSibling();
                        index = rowContainer.childCount - 1;
                    }

                    previous = index;
                }
            }
        }

        void ResetRowMap()
        {
            liveRows.Clear();
            spareRows.Clear();
            mappedColumns = -1;
            firstLiveRow = 0;
            lastLiveRow = -1;
            heldRow = -1;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Scrolls so a row sits at an alignment in the viewport: 0 top, 0.5 center, 1 bottom.
        /// With Smooth Scroll on, the move animates unless instant is true.
        /// </summary>
        public void ScrollToRow(int rowIndex, float alignment = 0, bool instant = false)
        {
            if (!CanScroll || rowIndex < 0 || rowIndex >= rows.Count || float.IsNaN(alignment))
                return;

            Vector2 position = rowContainer.anchoredPosition;
            position.y = RowTop(rowIndex) - (viewport.rect.height - RowHeight) * Mathf.Clamp01(alignment);
            ScrollTo(position, instant);
        }

        /// <summary>
        /// Scrolls only as far as needed to show a cell. Row -1 is the header, which only scrolls sideways,
        /// and column -1 keeps the horizontal position.
        /// </summary>
        public void EnsureVisible(int rowIndex, int columnIndex = -1, bool instant = false)
        {
            if (!CanScroll)
                return;

            Vector2 position = rowContainer.anchoredPosition;
            Rect view = viewport.rect;

            if (rowIndex >= 0 && rowIndex < rows.Count)
                position.y = Reveal(position.y, RowTop(rowIndex), RowHeight, view.height);

            if (columnIndex >= 0 && columnIndex < columnWidths.Length)
                position.x = -Reveal(-position.x, columnLefts[columnIndex], columnWidths[columnIndex], view.width);

            ScrollTo(position, instant);
        }
        #endregion
    }
}