using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public static partial class EvoEditorGUI
    {
        const float DefaultListMaxHeight = 400f;

        // One control per drawn list, released together with its target object
        static readonly ConditionalWeakTable<UnityEngine.Object,
            Dictionary<(string propertyPath, string controlName), ReorderableListControl>> reorderableLists = new();

        /// <summary>
        /// Draws <paramref name="items"/> as a foldout list with drag reordering, add and delete buttons and a context menu
        /// to copy, paste, expand or collapse the rows. Alt+click on the header opens or closes every row along with the list.
        /// Once the rows are taller than <paramref name="maxHeight"/> (0 never scrolls), they scroll and only the visible ones
        /// are drawn. Row identity is tracked by the editor, so runtime data needs no editor-only IDs.
        /// </summary>
        /// <returns>True while a row is being dragged.</returns>
        public static bool ReorderableList(SerializedObject owner, SerializedProperty items,
            Func<SerializedProperty, int, string> getItemTitle, Action<SerializedProperty> drawItem,
            ReorderableListOptions options = default, bool compactItems = true, bool compactHeader = false,
            bool invertColors = false, bool boldHeader = true, float maxHeight = DefaultListMaxHeight)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (owner.targetObject == null)
                throw new ArgumentException("The serialized object must have a target object.", nameof(owner));
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (!items.isArray)
                throw new ArgumentException("The supplied property must be an array or list.", nameof(items));
            if (getItemTitle == null)
                throw new ArgumentNullException(nameof(getItemTitle));

            var lists = reorderableLists.GetOrCreateValue(owner.targetObject);
            var key = (items.propertyPath, options.ControlName);
            if (!lists.TryGetValue(key, out ReorderableListControl list))
                lists.Add(key, list = new ReorderableListControl(key.GetHashCode()));

            return list.Draw(owner, items, getItemTitle, drawItem, options, compactItems, compactHeader, invertColors, boldHeader, maxHeight);
        }

        [NoAutoStaticsCleanup]
        sealed class ReorderableListControl
        {
            const float Epsilon = 0.01f;
            const float VirtualizationOverscan = 32f;
            const float ScrollbarPadding = 5f;
            const float ScrollLimitTolerance = 0.5f;
            const float DragScrollMargin = 12f;
            const float DragScrollSpeed = 12f;
            const float CursorRange = 100000f;
            const int AnimatedDepth = -500;
            const int DraggedDepth = -1000;
            const int ButtonIconSize = 8;
            const int RetiredItemCapacity = 32;

            static int undoVersion;

            readonly struct RetiredItem
            {
                public readonly long Key;
                public readonly uint ContentHash;
                public readonly bool? Expanded;

                public RetiredItem(long key, uint contentHash, bool? expanded)
                {
                    Key = key;
                    ContentHash = contentHash;
                    Expanded = expanded;
                }
            }

            // Row identity: one key per array slot that travels with its item through reorders, inserts and removals
            readonly List<long> itemKeys = new();
            readonly Dictionary<long, bool> foldoutStates = new();
            readonly Dictionary<long, uint> contentHashes = new();
            readonly List<RetiredItem> retiredItems = new();

            // Row layout in content space (the scroll view's content once the list scrolls)
            readonly Dictionary<long, Rect> itemRects = new();
            readonly HashSet<long> estimatedKeys = new();
            readonly Dictionary<long, Transition> moveAnimations = new();
            readonly Dictionary<long, ExpandAnimation> rowAnimations = new();
            readonly List<float> dragItemCenters = new();

            // Scratch collections, reused to keep key matching free of allocations
            readonly List<long> previousKeys = new();
            readonly List<uint> currentHashes = new();
            readonly Dictionary<uint, long> firstKeyByHash = new();
            readonly Dictionary<long, long> nextKeyWithHash = new();
            readonly HashSet<long> claimedKeys = new();
            readonly List<long> staleKeys = new();

            readonly int controlHint;
            readonly ReorderableListAddContext addContext;
            readonly Action<Rect> itemPreviewDrawer;

            SerializedObject listOwner;
            string listPath;
            SerializedObject clipboardOwner;
            string clipboardPath;
            ReorderableListOptions options;
            float maxHeight;
            bool listInverted;
            bool listVisible;
            ExpandAnimation listAnimation;
            Rect listHeaderRect;
            Rect listContextRect;
            int controlId;
            int pendingDeleteIndex = -1;
            bool pendingAdd;
            bool? pendingItemsExpanded;

            long nextKey;
            bool keysInitialized;
            bool keysDirty;
            int syncedDirtyCount;
            int observedUndoVersion;

            float collapsedHeight;
            float expandedHeightEstimate;
            float itemSpacing = Metrics.PropertySpacing;
            float headerOffset;
            float headerHeight;
            bool metricsDirty = true;
            bool estimatesDirty;
            bool hasListBounds;
            Rect listBounds;
            bool hasDrawnBounds;
            Rect drawnBounds;
            bool hasScrollView;
            Vector2 scrollPosition;
            bool virtualize;
            int firstVisibleIndex;
            int lastVisibleIndex;
            bool hasPreviousRowRect;
            Rect previousRowRect;

            int draggedIndex = -1;
            long draggedKey;
            float dragOffsetY;
            int dragStartIndex = -1;
            float dragStartY;
            float dragStartHeight;
            int pendingReorderIndex = -1;
            bool finishDragPending;
            int activeControlId;
            bool hasDraggedLayoutRect;
            Rect draggedLayoutRect;

            long drawingKey;
            SerializedProperty drawingItem;
            ExpandAnimation rowAnimation;
            Rect rowHeaderRect;
            Matrix4x4 previousMatrix;
            int previousDepth;
            bool rowDragged;
            bool rowTranslated;
            bool rowClipped;

            static ReorderableListControl() => Undo.undoRedoPerformed += () => undoVersion++;

            public ReorderableListControl(int controlHint)
            {
                this.controlHint = controlHint;
                addContext = new ReorderableListAddContext(AddItem);
                itemPreviewDrawer = DrawItemPreview;
            }

            bool IsDragging => draggedIndex >= 0;

            public bool Draw(SerializedObject owner, SerializedProperty items, Func<SerializedProperty, int, string> getItemTitle,
                Action<SerializedProperty> drawItem, ReorderableListOptions listOptions, bool compactItems, bool compactHeader,
                bool invertColors, bool boldHeader, float listMaxHeight)
            {
                collapsedHeight = compactItems ? Metrics.FoldoutHeightCompact : Metrics.FoldoutHeight;

                if (BeginList(owner, items, listOptions, compactHeader, invertColors, boldHeader, listMaxHeight))
                    DrawItems(items, getItemTitle, drawItem, compactItems, !invertColors);

                EndList(items);
                return IsDragging;
            }

            #region List
            bool BeginList(SerializedObject owner, SerializedProperty items, ReorderableListOptions listOptions, bool compactHeader,
                bool invertColors, bool boldHeader, float listMaxHeight)
            {
                listOwner = owner;
                listPath = items.propertyPath;
                options = listOptions;
                listInverted = invertColors;
                maxHeight = Mathf.Max(0f, listMaxHeight);
                pendingDeleteIndex = -1;
                pendingAdd = false;
                hasScrollView = false;
                hasDrawnBounds = false;

                SerializedProperty clipboardRoot = options.ListClipboardRoot ?? items;
                clipboardOwner = clipboardRoot.serializedObject;
                clipboardPath = clipboardRoot.propertyPath;

                if (IsDragging && !options.AllowItemChanges)
                    CancelDrag();

                BeginColumn(LayoutOptions.WithInvertedColor(invertColors));

                bool wasExpanded = items.isExpanded;
                bool expanded = wasExpanded;
                string title = string.IsNullOrEmpty(options.Title) ? items.displayName : options.Title;
                listAnimation ??= new ExpandAnimation(wasExpanded, Metrics.FoldoutAnimationDuration);
                listHeaderRect = DrawFoldoutHeader(ref expanded, $"{title} ({items.arraySize})",
                    FoldoutOptions.WithIcon(options.Icon).WithoutBackground().WithBoldText(boldHeader), compactHeader);
                items.isExpanded = expanded;

                // Alt+click also opens or closes every row, like Unity's own foldouts
                bool clicked = expanded != wasExpanded;
                if (clicked && Event.current.alt)
                    pendingItemsExpanded = expanded;

                listVisible = BeginFoldoutContent(expanded, clicked, listAnimation);
                if (!listVisible)
                    return false;

                BeginContainer();
                options.ContentHeader?.Invoke();
                SyncItemKeys(items);

                if ((metricsDirty || estimatesDirty) && !IsDragging && Event.current.type == EventType.Layout)
                    UpdateListMetrics(items);

                hasScrollView = ShouldUseScrollView();
                if (hasScrollView)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false,
                        GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, GUILayout.Height(maxHeight));
                    scrollPosition.x = 0f;
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical();
                }
                else
                {
                    scrollPosition = Vector2.zero;
                }

                controlId = GUIUtility.GetControlID(controlHint, FocusType.Passive);
                if (IsDragging)
                {
                    hasDraggedLayoutRect = itemRects.TryGetValue(draggedKey, out draggedLayoutRect);
                    HandleDrag(items);
                }

                UpdateVisibleRange();

                if (items.arraySize == 0 && !string.IsNullOrEmpty(options.EmptyMessage))
                    Callout(options.EmptyMessage, invertColor: !listInverted, spaceBefore: 0, spaceAfter: 0);

                return true;
            }

            void EndList(SerializedProperty items)
            {
                if (listVisible)
                {
                    if (hasScrollView)
                    {
                        GUILayout.EndVertical();
                        GUILayout.Space(ScrollbarPadding);
                        GUILayout.EndHorizontal();

                        // The scroll view takes every wheel event over it, so it is hidden there once the rows can't
                        // scroll any further that way, and the view around the list keeps scrolling instead
                        Event currentEvent = Event.current;
                        bool passWheel = currentEvent.type == EventType.ScrollWheel
                            && !CanScroll(currentEvent.delta.y, GUILayoutUtility.GetLastRect().height);

                        if (passWheel)
                            currentEvent.type = EventType.Ignore;

                        EditorGUILayout.EndScrollView();

                        if (passWheel)
                            currentEvent.type = EventType.ScrollWheel;
                    }

                    UpdateDrawnBounds();
                    AddPropertySpace();

                    if (!string.IsNullOrEmpty(options.AddLabel) && Button(options.AddLabel, options.AddIcon,
                        ButtonOptions.WithIconSize(ButtonIconSize).WithInvertedColor(!listInverted)))
                        pendingAdd = true;

                    EndContainer();
                }

                listAnimation.EndContent();
                EndColumn();

                if (Event.current.type == EventType.Repaint || listContextRect.width <= 0f)
                    listContextRect = Encapsulate(listHeaderRect, GUILayoutUtility.GetLastRect());

                if (HandleContextClick(listContextRect))
                    ShowListMenu(items);

                DrawPrefabOverride(items, listHeaderRect);

                if (IsDragging)
                {
                    var cursorRect = new Rect(-CursorRange, -CursorRange, CursorRange * 2f, CursorRange * 2f);
                    EditorGUIUtility.AddCursorRect(cursorRect, MouseCursor.Pan);
                }

                if (UpdateMoveAnimations())
                    RequestAnimationFrame();

                // Structural edits wait until the list is drawn, so its layout stays consistent for this event
                if (pendingDeleteIndex >= 0)
                {
                    int index = pendingDeleteIndex;
                    pendingDeleteIndex = -1;
                    DeleteListItem(index);
                }

                if (pendingAdd)
                {
                    pendingAdd = false;
                    if (options.AddAction != null) { options.AddAction(addContext); }
                    else { AddItem(options.InitializeNewItem); }
                }

                // Rows close once the list has, so its closing animation keeps their height
                if (pendingItemsExpanded.HasValue && (items.isExpanded || !listVisible))
                    SetItemsExpanded(items, pendingItemsExpanded.Value);
            }

            bool ShouldUseScrollView() => maxHeight > 0f && hasListBounds && listBounds.height > maxHeight;

            bool CanScroll(float wheelDelta, float contentHeight)
            {
                if (wheelDelta > 0f) { return scrollPosition.y < contentHeight - maxHeight - ScrollLimitTolerance; }
                if (wheelDelta < 0f) { return scrollPosition.y > ScrollLimitTolerance; }
                return false;
            }

            void UpdateDrawnBounds()
            {
                if (Event.current.type != EventType.Repaint)
                    return;

                hasListBounds = hasDrawnBounds;
                if (hasListBounds)
                    listBounds = drawnBounds;

                if (ShouldUseScrollView() != hasScrollView)
                    RepaintView();
            }
            #endregion

            #region Rows
            void DrawItems(SerializedProperty items, Func<SerializedProperty, int, string> getItemTitle,
                Action<SerializedProperty> drawItem, bool compact, bool invertColors)
            {
                int itemCount = Mathf.Min(items.arraySize, itemKeys.Count);
                int firstIndex = virtualize ? Mathf.Clamp(firstVisibleIndex, 0, itemCount) : 0;
                int lastIndex = virtualize ? Mathf.Clamp(lastVisibleIndex, -1, itemCount - 1) : itemCount - 1;
                RefreshContentHashes(items, firstIndex, lastIndex);
                hasPreviousRowRect = false;

                if (firstIndex > lastIndex)
                {
                    ReserveRows(0, itemCount - 1, itemCount);
                    return;
                }

                ReserveRows(0, firstIndex - 1, itemCount);

                Func<SerializedProperty, Texture2D> getItemIcon = options.ItemIconGetter;
                for (int i = firstIndex; i <= lastIndex && i < items.arraySize; i++)
                {
                    SerializedProperty item = items.GetArrayElementAtIndex(GetDataIndex(i));
                    if (BeginRow(item, i, getItemTitle(item, i), getItemIcon?.Invoke(item), compact, invertColors))
                        drawItem?.Invoke(item);

                    EndRow(item, i, i < itemCount - 1);
                }

                ReserveRows(lastIndex + 1, itemCount - 1, itemCount);
            }

            bool BeginRow(SerializedProperty item, int index, string title, Texture2D icon, bool compact, bool invertColors)
            {
                drawingKey = itemKeys[index];
                drawingItem = item;
                BeginRowTranslation();
                BeginColumn(LayoutOptions.WithInvertedColor(invertColors));
                GUILayout.BeginHorizontal();

                if (!foldoutStates.TryGetValue(drawingKey, out bool wasExpanded))
                    wasExpanded = item.isExpanded;

                bool expanded = wasExpanded;
                float buttonSize = compact ? Metrics.FoldoutHeightCompact : Metrics.FoldoutHeight;
                DrawFoldoutHeader(ref expanded, title, FoldoutOptions.WithIcon(icon).WithoutBackground().WithoutBoldText(), compact,
                    options.ItemPreviewSize, options.DrawItemPreview != null ? itemPreviewDrawer : null);
                item.isExpanded = expanded;
                foldoutStates[drawingKey] = expanded;

                if (options.AllowItemChanges)
                {
                    DrawHandle(index, buttonSize);
                    if (Button(string.Empty, GetIcon(IconLibrary.Delete), ButtonOptions.WithTooltip(options.DeleteTooltip)
                        .WithIconSize(ButtonIconSize).WithSize(buttonSize, buttonSize).WithoutIdleBackground()))
                        pendingDeleteIndex = index;
                }

                GUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint)
                    rowHeaderRect = GUILayoutUtility.GetLastRect();

                // Rows only keep an animation while they open or close
                bool clicked = expanded != wasExpanded;
                if (!rowAnimations.TryGetValue(drawingKey, out rowAnimation) && clicked)
                    rowAnimations.Add(drawingKey, rowAnimation = new ExpandAnimation(wasExpanded, Metrics.FoldoutAnimationDuration));

                return BeginFoldoutContent(expanded, clicked, rowAnimation);
            }

            void EndRow(SerializedProperty item, int index, bool addSpace)
            {
                if (rowAnimation != null)
                {
                    rowAnimation.EndContent();
                    if (rowAnimation.IsSettled)
                        rowAnimations.Remove(drawingKey);
                }

                EndColumn();
                Rect rowRect = GUILayoutUtility.GetLastRect();

                if (Event.current.type == EventType.Repaint)
                    CacheRowRect(drawingKey, rowRect);

                EndRowTranslation(rowRect);

                if (HandleContextClick(rowRect))
                    ShowItemMenu(item, index);

                if (addSpace)
                    AddPropertySpace();
            }

            void DrawItemPreview(Rect rect) => options.DrawItemPreview?.Invoke(drawingItem, rect);

            void BeginRowTranslation()
            {
                rowDragged = false;
                rowTranslated = false;
                rowClipped = false;

                if (Event.current.type != EventType.Repaint)
                    return;

                if (IsDragging && drawingKey == draggedKey && (hasDraggedLayoutRect || itemRects.ContainsKey(drawingKey)))
                {
                    Rect layoutRect = hasDraggedLayoutRect ? draggedLayoutRect : itemRects[drawingKey];
                    rowDragged = true;
                    TranslateRow(GetClampedDragY(layoutRect) - layoutRect.y, DraggedDepth);
                }
                else if (TryGetMoveOffset(drawingKey, out float offset))
                {
                    TranslateRow(offset, AnimatedDepth);
                }
            }

            void TranslateRow(float yOffset, int depth)
            {
                previousMatrix = GUI.matrix;
                previousDepth = GUI.depth;

                // Inside the scroll view a clip keeps the moved row within the viewport; a matrix would not
                if (TryGetScrollViewportRect(out Rect viewportRect))
                {
                    GUI.BeginClip(viewportRect, new Vector2(-viewportRect.x, yOffset - viewportRect.y), Vector2.zero, false);
                    rowClipped = true;
                }
                else
                {
                    GUI.matrix = Matrix4x4.Translate(new Vector3(0f, yOffset, 0f)) * previousMatrix;
                }

                GUI.depth = depth;
                rowTranslated = true;
            }

            void EndRowTranslation(Rect rowRect)
            {
                if (!rowTranslated)
                    return;

                if (rowDragged)
                    DrawDragBorder(rowRect);

                if (rowClipped) { GUI.EndClip(); }
                else { GUI.matrix = previousMatrix; }

                GUI.depth = previousDepth;
                rowDragged = false;
                rowTranslated = false;
                rowClipped = false;
            }

            bool TryGetScrollViewportRect(out Rect viewportRect)
            {
                if (!hasScrollView || !hasListBounds || listBounds.width <= 0f)
                {
                    viewportRect = default;
                    return false;
                }

                viewportRect = new Rect(listBounds.xMin, scrollPosition.y, listBounds.width, maxHeight);
                return true;
            }

            void DrawHandle(int index, float size)
            {
                Event currentEvent = Event.current;
                bool enabled = GUI.enabled;
                bool active = IsDragging && drawingKey == draggedKey;

                // The button only draws; it stays disabled outside Repaint so the press below can start the drag
                GUI.enabled = enabled && currentEvent.type == EventType.Repaint;
                Button(string.Empty, GetIcon(IconLibrary.Reorder), ButtonOptions.WithTooltip("Drag to reorder")
                    .WithIconSize(ButtonIconSize).WithSize(size, size).WithNormalColor(active ? Palette.GetBackgroundColor(pressed: true) : Color.clear));
                GUI.enabled = enabled;

                // A press outside the visible part of the scroll view arrives as Ignore, so hidden overscan rows can't take it
                Rect rect = GUILayoutUtility.GetLastRect();
                if (enabled && !IsDragging && currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
                    StartDrag(index, rect);
            }

            void ReserveRows(int firstIndex, int lastIndex, int itemCount)
            {
                // Rows outside the viewport take their measured (or estimated) space without being drawn
                if (firstIndex > lastIndex)
                    return;

                bool hasRange = itemRects.TryGetValue(itemKeys[firstIndex], out Rect firstRect)
                    & itemRects.TryGetValue(itemKeys[lastIndex], out Rect lastRect);
                float height = hasRange ? lastRect.yMax - firstRect.yMin + (lastIndex < itemCount - 1 ? itemSpacing : 0f) : 0f;
                Rect rangeRect = GUILayoutUtility.GetRect(0f, Mathf.Max(0f, height), GUILayout.ExpandWidth(true));

                if (Event.current.type != EventType.Repaint)
                    return;

                if (hasRange)
                    ShiftRows(firstIndex, lastIndex, rangeRect.y - firstRect.y);

                IncludeRect(ref drawnBounds, ref hasDrawnBounds, rangeRect);
                hasPreviousRowRect = false;
            }

            void ShiftRows(int firstIndex, int lastIndex, float yOffset)
            {
                if (Mathf.Abs(yOffset) <= Epsilon)
                    return;

                for (int i = firstIndex; i <= lastIndex; i++)
                {
                    long key = itemKeys[i];
                    if (!itemRects.TryGetValue(key, out Rect rect))
                        continue;

                    rect.y += yOffset;
                    itemRects[key] = rect;
                }
            }

            void CacheRowRect(long key, Rect rect)
            {
                // A row measured for the first time can change which rows fit the viewport, and refines the estimate of the rest
                if (estimatedKeys.Remove(key) && itemRects.TryGetValue(key, out Rect estimate) && Mathf.Abs(estimate.height - rect.height) > Epsilon)
                {
                    estimatesDirty = true;
                    RepaintView();
                }
                // A row that changed height (a closed foldout) moved the rows below it, and the visible range was found
                // with the old heights; without a repaint, rows that moved into view stay undrawn until the next event
                else if (virtualize && itemRects.TryGetValue(key, out Rect previous) && Mathf.Abs(previous.height - rect.height) > Epsilon)
                {
                    RepaintView();
                }

                itemRects[key] = rect;
                headerOffset = rowHeaderRect.y - rect.y;
                headerHeight = rowHeaderRect.height;

                if (foldoutStates.TryGetValue(key, out bool expanded) && expanded && !rowAnimations.ContainsKey(key))
                    expandedHeightEstimate = rect.height;

                if (hasPreviousRowRect && rect.yMin >= previousRowRect.yMax)
                    itemSpacing = rect.yMin - previousRowRect.yMax;

                previousRowRect = rect;
                hasPreviousRowRect = true;
                IncludeRect(ref drawnBounds, ref hasDrawnBounds, rect);
            }

            void UpdateListMetrics(SerializedProperty items)
            {
                // Every row needs a rect for virtualization. Rows that were never drawn get an estimate, corrected once they are,
                // so a long list never has to draw all of its rows just to measure them.
                int itemCount = Mathf.Min(items.arraySize, itemKeys.Count);
                float x = hasListBounds ? listBounds.x : 0f;
                float width = hasListBounds ? listBounds.width : 1f;
                float y = hasListBounds ? listBounds.yMin : 0f;
                hasListBounds = false;

                for (int i = 0; i < itemCount; i++)
                {
                    long key = itemKeys[i];
                    if (!itemRects.TryGetValue(key, out Rect rect) || estimatedKeys.Contains(key))
                    {
                        // The foldout state is kept so later estimates don't have to read the element again
                        if (!foldoutStates.TryGetValue(key, out bool expanded))
                            foldoutStates[key] = expanded = items.GetArrayElementAtIndex(i).isExpanded;

                        float height = expanded && expandedHeightEstimate > 0f ? expandedHeightEstimate : collapsedHeight;
                        rect = new Rect(x, y, width, height);
                        estimatedKeys.Add(key);
                    }

                    rect.y = y;
                    itemRects[key] = rect;
                    IncludeRect(ref listBounds, ref hasListBounds, rect);
                    y = rect.yMax + itemSpacing;
                }

                metricsDirty = false;
                estimatesDirty = false;
            }

            void UpdateVisibleRange()
            {
                if (Event.current.type != EventType.Layout)
                    return;

                ReleaseFinishedRowAnimations();

                int itemCount = itemKeys.Count;
                virtualize = hasScrollView && !metricsDirty && itemCount > 0;
                if (!virtualize)
                    return;

                // Rows around an opening or closing row move every frame, so draw as far as they can slide into view
                float overscan = VirtualizationOverscan;
                foreach (long key in rowAnimations.Keys)
                {
                    if (itemRects.TryGetValue(key, out Rect rect))
                        overscan += Mathf.Max(0f, rect.height - collapsedHeight);
                }

                // Measured from the first row, since rects are still in the previous space right after the list starts scrolling
                float top = GetRowRect(0).yMin + scrollPosition.y;
                firstVisibleIndex = FindFirstRowEndingAfter(top - overscan);
                lastVisibleIndex = FindLastRowStartingBefore(top + maxHeight + overscan);

                // The dragged row is always drawn, so it can follow the mouse
                if (IsDragging && draggedIndex < itemCount)
                {
                    firstVisibleIndex = Mathf.Min(firstVisibleIndex, draggedIndex);
                    lastVisibleIndex = Mathf.Max(lastVisibleIndex, draggedIndex);
                }
            }

            void ReleaseFinishedRowAnimations()
            {
                // Drawn rows let go of their animation in EndRow. A row that finished out of view is released here,
                // or the overscan would keep making room for it on every frame
                if (rowAnimations.Count == 0)
                    return;

                double time = AnimationClock.Time;
                staleKeys.Clear();
                foreach (KeyValuePair<long, ExpandAnimation> entry in rowAnimations)
                {
                    if (entry.Value.IsFinished(time))
                        staleKeys.Add(entry.Key);
                }

                for (int i = 0; i < staleKeys.Count; i++)
                    rowAnimations.Remove(staleKeys[i]);
            }

            int FindFirstRowEndingAfter(float y)
            {
                int lower = 0;
                int upper = itemKeys.Count;

                while (lower < upper)
                {
                    int middle = lower + (upper - lower) / 2;
                    if (GetRowRect(middle).yMax < y) { lower = middle + 1; }
                    else { upper = middle; }
                }

                return lower;
            }

            int FindLastRowStartingBefore(float y)
            {
                int lower = 0;
                int upper = itemKeys.Count;

                while (lower < upper)
                {
                    int middle = lower + (upper - lower) / 2;
                    if (GetRowRect(middle).yMin <= y) { lower = middle + 1; }
                    else { upper = middle; }
                }

                return lower - 1;
            }

            Rect GetRowRect(int index) => itemRects.TryGetValue(itemKeys[index], out Rect rect) ? rect : default;

            static void IncludeRect(ref Rect bounds, ref bool hasBounds, Rect rect)
            {
                bounds = hasBounds ? Encapsulate(bounds, rect) : rect;
                hasBounds = true;
            }

            static Rect Encapsulate(Rect first, Rect second)
            {
                return Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                    Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
            }

            static void DrawDragBorder(Rect rect)
            {
                Styles.Ensure();

                Color cachedColor = GUI.color;
                Color borderColor = Palette.Selection;
                borderColor.a = 1f;
                GUI.color = borderColor;
                GUI.Box(rect, GUIContent.none, Styles.DragBorder);
                GUI.color = cachedColor;
            }
            #endregion

            #region Item Keys
            void SyncItemKeys(SerializedProperty items)
            {
                if (observedUndoVersion != undoVersion)
                {
                    // An undo elsewhere leaves this list alone; only match rows again when its object changed
                    observedUndoVersion = undoVersion;
                    if (EditorUtility.GetDirtyCount(listOwner.targetObject) != syncedDirtyCount)
                        keysDirty = true;
                }

                if (keysInitialized && !keysDirty && itemKeys.Count == items.arraySize)
                    return;

                if (IsDragging)
                    CancelDrag();

                RebuildItemKeys(items);
            }

            void RebuildItemKeys(SerializedProperty items)
            {
                // Undo, redo, paste and external edits replace the array wholesale. Matching rows to their previous keys by
                // content keeps foldout states and measured heights with the items instead of leaving them on the slots.
                ReadContentHashes(items, currentHashes);
                int itemCount = currentHashes.Count;
                previousKeys.Clear();
                previousKeys.AddRange(itemKeys);
                itemKeys.Clear();

                if (keysInitialized) { MatchItemKeys(itemCount); }
                else
                {
                    for (int i = 0; i < itemCount; i++)
                        itemKeys.Add(NextKey());
                }

                // Rows whose content changed are measured again; keys that are gone are remembered for an undo
                claimedKeys.Clear();
                for (int i = 0; i < itemCount; i++)
                {
                    long key = itemKeys[i];
                    claimedKeys.Add(key);
                    if (!contentHashes.TryGetValue(key, out uint hash) || hash != currentHashes[i])
                    {
                        itemRects.Remove(key);
                        estimatedKeys.Remove(key);
                    }
                }

                for (int i = 0; i < previousKeys.Count; i++)
                {
                    if (!claimedKeys.Contains(previousKeys[i]))
                        RemoveKeyState(previousKeys[i], retire: true);
                }

                contentHashes.Clear();
                for (int i = 0; i < itemCount; i++)
                    contentHashes[itemKeys[i]] = currentHashes[i];

                moveAnimations.Clear();
                syncedDirtyCount = EditorUtility.GetDirtyCount(listOwner.targetObject);
                keysInitialized = true;
                keysDirty = false;
                metricsDirty = true;
            }

            void MatchItemKeys(int itemCount)
            {
                // Previous keys per content hash, as linked lists in their original order
                firstKeyByHash.Clear();
                nextKeyWithHash.Clear();
                for (int i = previousKeys.Count - 1; i >= 0; i--)
                {
                    long key = previousKeys[i];
                    if (!contentHashes.TryGetValue(key, out uint hash))
                        continue;

                    if (firstKeyByHash.TryGetValue(hash, out long next))
                        nextKeyWithHash[key] = next;

                    firstKeyByHash[hash] = key;
                }

                claimedKeys.Clear();
                for (int i = 0; i < itemCount; i++)
                {
                    uint hash = currentHashes[i];
                    if (!firstKeyByHash.TryGetValue(hash, out long key))
                    {
                        itemKeys.Add(0L);
                        continue;
                    }

                    if (nextKeyWithHash.TryGetValue(key, out long next)) { firstKeyByHash[hash] = next; }
                    else { firstKeyByHash.Remove(hash); }

                    claimedKeys.Add(key);
                    itemKeys.Add(key);
                }

                // Edited rows keep the key of their slot, and restored rows take back the key they had before removal
                for (int i = 0; i < itemCount; i++)
                {
                    if (itemKeys[i] != 0L)
                        continue;

                    long key = i < previousKeys.Count && claimedKeys.Add(previousKeys[i]) ? previousKeys[i] : ReviveKey(currentHashes[i]);
                    itemKeys[i] = key != 0L ? key : NextKey();
                }
            }

            void RefreshContentHashes(SerializedProperty items, int firstIndex, int lastIndex)
            {
                // Hashes must describe the rows as they were right before an undo. Rows are edited while they're drawn, so only
                // the drawn range is hashed again after a change, which keeps long lists cheap.
                if (Event.current.type != EventType.Layout || IsDragging)
                    return;

                int dirtyCount = EditorUtility.GetDirtyCount(listOwner.targetObject);
                if (dirtyCount == syncedDirtyCount)
                    return;

                syncedDirtyCount = dirtyCount;
                for (int i = Mathf.Max(0, firstIndex); i <= lastIndex && i < itemKeys.Count; i++)
                    contentHashes[itemKeys[i]] = items.GetArrayElementAtIndex(i).contentHash;
            }

            static void ReadContentHashes(SerializedProperty items, List<uint> hashes)
            {
                hashes.Clear();
                int itemCount = items.arraySize;
                if (itemCount == 0)
                    return;

                // Walking the siblings with one iterator avoids a property per element on long lists
                SerializedProperty item = items.GetArrayElementAtIndex(0);
                for (int i = 0; i < itemCount; i++)
                {
                    hashes.Add(item.contentHash);
                    if (i < itemCount - 1)
                        item.Next(false);
                }
            }

            long NextKey()
            {
                nextKey = unchecked(nextKey + 1L);
                if (nextKey == 0L)
                    nextKey = 1L;

                return nextKey;
            }

            long ReviveKey(uint contentHash)
            {
                for (int i = retiredItems.Count - 1; i >= 0; i--)
                {
                    RetiredItem item = retiredItems[i];
                    if (item.ContentHash != contentHash)
                        continue;

                    retiredItems.RemoveAt(i);
                    if (item.Expanded.HasValue)
                        foldoutStates[item.Key] = item.Expanded.Value;

                    return item.Key;
                }

                return 0L;
            }

            void RemoveKeyState(long key, bool retire)
            {
                // A removed row is remembered by content, so undoing the removal restores its key and foldout state
                if (retire && contentHashes.TryGetValue(key, out uint hash))
                {
                    if (retiredItems.Count >= RetiredItemCapacity)
                        retiredItems.RemoveAt(0);

                    bool? expanded = foldoutStates.TryGetValue(key, out bool state) ? state : null;
                    retiredItems.Add(new RetiredItem(key, hash, expanded));
                }

                contentHashes.Remove(key);
                foldoutStates.Remove(key);
                itemRects.Remove(key);
                estimatedKeys.Remove(key);
                moveAnimations.Remove(key);
                rowAnimations.Remove(key);
            }

            bool CanEditKeys => keysInitialized && !keysDirty;

            void InsertItemKey(int index, SerializedProperty item, long? copyStateFrom = null)
            {
                if (CanEditKeys && index >= 0 && index <= itemKeys.Count)
                {
                    long key = NextKey();
                    itemKeys.Insert(index, key);
                    contentHashes[key] = item.contentHash;
                    if (copyStateFrom.HasValue && foldoutStates.TryGetValue(copyStateFrom.Value, out bool expanded))
                        foldoutStates[key] = expanded;
                }
                else
                {
                    keysDirty = true;
                }

                metricsDirty = true;
                InvalidateSiblingLists();
            }

            void RemoveItemKey(int index)
            {
                if (CanEditKeys && index >= 0 && index < itemKeys.Count)
                {
                    RemoveKeyState(itemKeys[index], retire: true);
                    itemKeys.RemoveAt(index);
                }
                else
                {
                    keysDirty = true;
                }

                metricsDirty = true;
                InvalidateSiblingLists();
            }

            void InvalidateItemKeys()
            {
                keysDirty = true;
                metricsDirty = true;
                InvalidateSiblingLists();
            }

            void InvalidateSiblingLists()
            {
                // Lists drawing the same array under another control name match their rows again on their next draw
                UnityEngine.Object target = listOwner?.targetObject;
                if (target == null || !reorderableLists.TryGetValue(target, out var lists))
                    return;

                foreach (var entry in lists)
                {
                    if (entry.Value == this || entry.Key.propertyPath != listPath)
                        continue;

                    entry.Value.keysDirty = true;
                    entry.Value.metricsDirty = true;
                }
            }
            #endregion

            #region Drag
            void StartDrag(int index, Rect handleRect)
            {
                Event currentEvent = Event.current;
                Rect rowRect = itemRects.TryGetValue(drawingKey, out Rect cachedRect) ? cachedRect : handleRect;

                draggedIndex = index;
                draggedKey = drawingKey;
                dragOffsetY = currentEvent.mousePosition.y - rowRect.y;
                dragStartIndex = index;
                dragStartY = rowRect.y;
                dragStartHeight = rowRect.height;
                pendingReorderIndex = -1;
                finishDragPending = false;

                // Targets are found against the rows' positions when the drag started, which keeps them stable while rows move
                dragItemCenters.Clear();
                for (int i = 0; i < itemKeys.Count; i++)
                    dragItemCenters.Add(itemRects.TryGetValue(itemKeys[i], out Rect rect) ? rect.center.y : rowRect.center.y);

                activeControlId = controlId;
                GUIUtility.hotControl = activeControlId;
                currentEvent.Use();
            }

            void HandleDrag(SerializedProperty items)
            {
                Event currentEvent = Event.current;
                switch (currentEvent.type)
                {
                    case EventType.Layout:
                        ApplyPendingReorder(items.arraySize);
                        if (finishDragPending)
                            FinishDrag(items);
                        return;

                    case EventType.Ignore:
                        EndDrag(items);
                        return;

                    // Escape ends the drag where the row is
                    case EventType.KeyDown when currentEvent.keyCode == KeyCode.Escape:
                        EndDrag(items);
                        currentEvent.Use();
                        return;

                    // A plain mouse move means the button was released where this view couldn't see it
                    case EventType.MouseUp when currentEvent.button == 0:
                    case EventType.MouseMove:
                        GUIUtility.hotControl = 0;
                        if (pendingReorderIndex >= 0)
                        {
                            // The last move is applied on the next layout, then the drag ends
                            finishDragPending = true;
                            RepaintView();
                        }
                        else
                        {
                            FinishDrag(items);
                        }

                        if (currentEvent.type == EventType.MouseUp)
                            currentEvent.Use();
                        return;
                }

                if (finishDragPending)
                    return;

                if (GUIUtility.hotControl != activeControlId)
                    GUIUtility.hotControl = activeControlId;

                bool mouseDrag = currentEvent.type == EventType.MouseDrag && currentEvent.button == 0;
                bool scrollRepaint = hasScrollView && currentEvent.type == EventType.Repaint;
                if (!mouseDrag && !scrollRepaint)
                    return;

                int currentIndex = itemKeys.IndexOf(draggedKey);
                if (currentIndex < 0)
                {
                    CancelDrag();
                    if (mouseDrag)
                        currentEvent.Use();
                    return;
                }

                float draggedY = hasDraggedLayoutRect ? GetClampedDragY(draggedLayoutRect) : currentEvent.mousePosition.y - dragOffsetY;
                if (hasScrollView && ScrollDraggedItemIntoView(draggedY))
                    RepaintView();

                int targetIndex = GetDragTargetIndex(items.arraySize, currentIndex, draggedY);
                pendingReorderIndex = targetIndex != currentIndex ? targetIndex : -1;
                if (pendingReorderIndex >= 0)
                    RepaintView();

                if (mouseDrag)
                    currentEvent.Use();
            }

            void ApplyPendingReorder(int itemCount)
            {
                // Rows only move during Layout, so every event of a frame sees the same order. Only the rows on screen move;
                // the array keeps its order until the drop, so a long drag doesn't pay for an apply per row it crosses.
                int targetIndex = pendingReorderIndex;
                pendingReorderIndex = -1;
                if (targetIndex < 0 || itemCount == 0)
                    return;

                int currentIndex = itemKeys.IndexOf(draggedKey);
                targetIndex = Mathf.Clamp(targetIndex, 0, itemCount - 1);
                if (currentIndex < 0 || targetIndex == currentIndex)
                    return;

                MoveDraggedKey(currentIndex, targetIndex);
                PrepareReorderAnimation(currentIndex, targetIndex);
            }

            void MoveDraggedKey(int currentIndex, int targetIndex)
            {
                itemKeys.RemoveAt(currentIndex);
                itemKeys.Insert(targetIndex, draggedKey);
                draggedIndex = targetIndex;
            }

            // Array index of the row drawn at a position; the two differ until a drag is applied
            int GetDataIndex(int index)
            {
                if (!IsDragging)
                    return index;

                if (index == draggedIndex)
                    return dragStartIndex;

                // Rows between the two slots shift one step toward the dragged row's old slot
                if (index >= dragStartIndex && index < draggedIndex)
                    return index + 1;

                if (index <= dragStartIndex && index > draggedIndex)
                    return index - 1;

                return index;
            }

            int GetDragTargetIndex(int itemCount, int currentIndex, float draggedY)
            {
                if (dragStartIndex < 0 || dragItemCenters.Count != itemCount)
                    return currentIndex;

                int targetIndex = dragStartIndex;

                if (draggedY > dragStartY)
                {
                    // Moving down: the row passes every row whose center its bottom edge has crossed
                    float draggedBottom = draggedY + dragStartHeight;
                    int lower = dragStartIndex + 1;
                    int upper = itemCount;

                    while (lower < upper)
                    {
                        int middle = lower + (upper - lower) / 2;
                        if (dragItemCenters[middle] <= draggedBottom) { lower = middle + 1; }
                        else { upper = middle; }
                    }

                    targetIndex = Mathf.Max(dragStartIndex, lower - 1);
                }
                else if (draggedY < dragStartY)
                {
                    // Moving up: the row passes every row whose center its top edge has crossed
                    int lower = 0;
                    int upper = dragStartIndex;

                    while (lower < upper)
                    {
                        int middle = lower + (upper - lower) / 2;
                        if (dragItemCenters[middle] < draggedY) { lower = middle + 1; }
                        else { upper = middle; }
                    }

                    targetIndex = lower;
                }

                return Mathf.Clamp(targetIndex, 0, itemCount - 1);
            }

            float GetClampedDragY(Rect itemRect)
            {
                float dragY = Event.current.mousePosition.y - dragOffsetY;
                if (!hasListBounds)
                    return dragY;

                // The header never leaves the top of the list, and the row stops at its bottom
                float itemHeight = dragStartHeight > 0f ? dragStartHeight : itemRect.height;
                float minY = listBounds.yMin - headerOffset;
                float maxY = Mathf.Max(minY, listBounds.yMax - itemHeight);
                return Mathf.Clamp(dragY, minY, maxY);
            }

            bool ScrollDraggedItemIntoView(float draggedY)
            {
                Rect itemRect = hasDraggedLayoutRect ? draggedLayoutRect : itemRects.TryGetValue(draggedKey, out Rect rect) ? rect : default;
                if (itemRect.width <= 0f)
                    return false;

                // Rows taller than the viewport keep just their header in view
                Rect visibleRect;
                if (itemRect.height <= Mathf.Max(1f, maxHeight - DragScrollMargin * 2f) || headerHeight <= 0f)
                    visibleRect = new Rect(itemRect.x, draggedY, itemRect.width, itemRect.height);
                else
                    visibleRect = new Rect(itemRect.x, draggedY + headerOffset, itemRect.width, headerHeight);

                visibleRect.yMin -= DragScrollMargin;
                visibleRect.yMax += DragScrollMargin;
                return GUI.ScrollTowards(visibleRect, DragScrollSpeed);
            }

            // Puts the rows back without changing the array
            void CancelDrag() => EndDrag(null);

            void EndDrag(SerializedProperty items)
            {
                GUIUtility.hotControl = 0;
                FinishDrag(items);
            }

            void FinishDrag(SerializedProperty items)
            {
                bool moved = draggedIndex >= 0 && draggedIndex != dragStartIndex;
                bool applied = moved && items != null && ApplyDrag(items);
                if (moved && !applied && draggedIndex < itemKeys.Count && itemKeys[draggedIndex] == draggedKey)
                    MoveDraggedKey(draggedIndex, dragStartIndex);

                // The released row eases from where it was dropped into its slot
                if (hasDraggedLayoutRect && (applied || !moved))
                {
                    float moveOffset = GetClampedDragY(draggedLayoutRect) - draggedLayoutRect.y;
                    if (Mathf.Abs(moveOffset) > Epsilon)
                        moveAnimations[draggedKey] = CreateMoveAnimation(moveOffset, AnimationClock.Time);
                }

                draggedIndex = -1;
                draggedKey = 0L;
                dragOffsetY = 0f;
                dragStartIndex = -1;
                dragStartY = 0f;
                dragStartHeight = 0f;
                pendingReorderIndex = -1;
                finishDragPending = false;
                activeControlId = 0;
                hasDraggedLayoutRect = false;
                dragItemCenters.Clear();

                if (applied)
                    options.AfterChange?.Invoke();
            }

            bool ApplyDrag(SerializedProperty items)
            {
                if (dragStartIndex >= items.arraySize || draggedIndex >= items.arraySize)
                    return false;

                // The whole drag is one change and one undo step
                SerializedObject owner = items.serializedObject;
                options.BeforeChange?.Invoke();
                Undo.RegisterCompleteObjectUndo(owner.targetObjects, "Reorder Items");
                items.MoveArrayElement(dragStartIndex, draggedIndex);
                owner.ApplyModifiedPropertiesWithoutUndo();
                owner.UpdateIfRequiredOrScript();
                InvalidateSiblingLists();
                return true;
            }

            void PrepareReorderAnimation(int previousIndex, int newIndex)
            {
                if (!hasListBounds)
                    return;

                // Rows between the two slots slide from where they were drawn to their new place
                double currentTime = AnimationClock.Time;
                int firstIndex = Mathf.Min(previousIndex, newIndex);
                int lastIndex = Mathf.Max(previousIndex, newIndex);
                float y = listBounds.yMin;

                if (firstIndex > 0)
                {
                    if (!itemRects.TryGetValue(itemKeys[firstIndex - 1], out Rect previousRect))
                        return;

                    y = previousRect.yMax + itemSpacing;
                }

                for (int i = firstIndex; i <= lastIndex; i++)
                {
                    long key = itemKeys[i];
                    if (!itemRects.TryGetValue(key, out Rect rect))
                        return;

                    float drawnY = rect.y + GetMoveOffset(key, currentTime);
                    rect.y = y;
                    itemRects[key] = rect;

                    if (key == draggedKey)
                    {
                        moveAnimations.Remove(key);
                        draggedLayoutRect = rect;
                        hasDraggedLayoutRect = true;
                    }
                    else if (Mathf.Abs(drawnY - y) > Epsilon)
                    {
                        moveAnimations[key] = CreateMoveAnimation(drawnY - y, currentTime);
                    }
                    else
                    {
                        moveAnimations.Remove(key);
                    }

                    y = rect.yMax + itemSpacing;
                }
            }

            bool UpdateMoveAnimations()
            {
                if (moveAnimations.Count == 0)
                    return false;

                double currentTime = AnimationClock.Time;
                staleKeys.Clear();
                foreach (KeyValuePair<long, Transition> entry in moveAnimations)
                {
                    if (!entry.Value.IsRunning(currentTime))
                        staleKeys.Add(entry.Key);
                }

                for (int i = 0; i < staleKeys.Count; i++)
                    moveAnimations.Remove(staleKeys[i]);

                return moveAnimations.Count > 0;
            }

            bool TryGetMoveOffset(long key, out float offset)
            {
                if (moveAnimations.TryGetValue(key, out Transition animation))
                {
                    double currentTime = AnimationClock.Time;
                    if (animation.IsRunning(currentTime))
                    {
                        offset = animation.Evaluate(currentTime);
                        return true;
                    }
                }

                offset = 0f;
                return false;
            }

            float GetMoveOffset(long key, double currentTime)
            {
                return moveAnimations.TryGetValue(key, out Transition animation) ? animation.Evaluate(currentTime) : 0f;
            }

            // A row drawn away from its slot eases back into it
            static Transition CreateMoveAnimation(float offset, double startTime)
            {
                return new Transition(offset, 0f, startTime, Metrics.ReorderAnimationDuration);
            }
            #endregion

            #region Menus
            bool HandleContextClick(Rect rect)
            {
                Event currentEvent = Event.current;
                if (IsDragging || currentEvent.type != EventType.ContextClick || !rect.Contains(currentEvent.mousePosition))
                    return false;

                currentEvent.Use();
                return true;
            }

            void ShowItemMenu(SerializedProperty item, int index) => CreateItemMenu(item, index).ShowAsContext();

            GenericMenu CreateItemMenu(SerializedProperty item, int index)
            {
                // A disabled GUI (read-only asset, locked section) still allows copying
                bool canEdit = options.AllowItemChanges && GUI.enabled;
                bool clipboard = options.AllowClipboard;
                bool customClipboard = options.CopyItem != null && options.CanPasteItem != null && options.PasteItem != null;
                bool canCopy = clipboard && (customClipboard || SerializedPropertyClipboard.CanCopy(item));
                bool canPaste = clipboard && canEdit && (customClipboard ? options.CanPasteItem(item) : SerializedPropertyClipboard.CanPaste(item));

                var menu = new GenericMenu();
                AddMenuItem(menu, "Copy", canCopy ? () => CopyListItem(index, customClipboard) : null);
                AddMenuItem(menu, "Paste", canPaste ? () => PasteListItem(index, customClipboard) : null);
                menu.AddSeparator(string.Empty);
                AddMenuItem(menu, "Duplicate", clipboard && canEdit ? () => DuplicateListItem(index) : null);
                AddMenuItem(menu, "Delete", canEdit ? () => DeleteListItem(index) : null);
                return menu;
            }

            void ShowListMenu(SerializedProperty items) => CreateListMenu(items).ShowAsContext();

            GenericMenu CreateListMenu(SerializedProperty items)
            {
                bool canEdit = GUI.enabled;
                SerializedProperty root = options.ListClipboardRoot ?? items;
                bool canCopy = options.AllowClipboard && SerializedPropertyClipboard.CanCopy(root);
                bool canPaste = options.AllowClipboard && options.AllowListPaste && canEdit && SerializedPropertyClipboard.CanPaste(root);

                var menu = new GenericMenu();
                AddMenuItem(menu, "Copy", canCopy ? CopyListClipboard : null);
                AddMenuItem(menu, "Paste", canPaste ? PasteListClipboard : null);
                menu.AddSeparator(string.Empty);
                AddExpandMenuItems(menu, items);

                if (canEdit && HasPrefabOverride(items))
                {
                    menu.AddSeparator(string.Empty);
                    foreach (string assetPath in GetPrefabApplyTargets(items))
                    {
                        UnityEngine.Object prefab = AssetDatabase.LoadMainAssetAtPath(assetPath);
                        AddMenuItem(menu, $"Apply to Prefab '{(prefab != null ? prefab.name : assetPath)}'", () => ApplyListOverride(assetPath));
                    }

                    AddMenuItem(menu, "Revert", RevertListOverride);
                }

                return menu;
            }

            void AddExpandMenuItems(GenericMenu menu, SerializedProperty items)
            {
                // Expanding also opens a closed list. A row is only known to be open or closed once it was matched to
                // its key and drawn or estimated; until then both stay available
                int itemCount = items.arraySize;
                bool canExpand = !items.isExpanded;
                bool canCollapse = false;
                bool keysKnown = CanEditKeys && itemKeys.Count == itemCount;

                for (int i = 0; i < itemCount; i++)
                {
                    if (!keysKnown || !foldoutStates.TryGetValue(itemKeys[i], out bool expanded)) { canExpand = canCollapse = true; }
                    else if (expanded) { canCollapse = true; }
                    else { canExpand = true; }

                    if (canExpand && canCollapse)
                        break;
                }

                AddMenuItem(menu, "Expand All", canExpand ? () => SetItemsExpanded(true) : null);
                AddMenuItem(menu, "Collapse All", canCollapse ? () => SetItemsExpanded(false) : null);
            }

            static void AddMenuItem(GenericMenu menu, string label, Action action)
            {
                if (action != null) { menu.AddItem(new GUIContent(label), false, action.Invoke); }
                else { menu.AddDisabledItem(new GUIContent(label)); }
            }
            #endregion

            #region Item Actions
            void AddItem(Action<SerializedProperty> initialize)
            {
                if (!TryGetList(out SerializedProperty items))
                    return;

                options.BeforeChange?.Invoke();
                Undo.SetCurrentGroupName("Add List Item");

                // Growing the array copies the last element, including its managed references
                int index = items.arraySize;
                items.arraySize++;
                SerializedProperty item = items.GetArrayElementAtIndex(index);
                if (index > 0)
                    ManagedReferences.MakeUnique(items.GetArrayElementAtIndex(index - 1), item);

                initialize?.Invoke(item);
                InsertItemKey(index, item);
                ApplyChange(listOwner);
            }

            void DuplicateListItem(int index)
            {
                if (!TryGetListItem(index, out SerializedProperty items, out SerializedProperty item))
                    return;

                options.BeforeChange?.Invoke();
                Undo.SetCurrentGroupName("Duplicate List Item");
                int previousCount = items.arraySize;
                item.DuplicateCommand();

                if (items.arraySize > previousCount)
                {
                    // The copy would otherwise share the original's managed reference objects
                    SerializedProperty copy = items.GetArrayElementAtIndex(index + 1);
                    ManagedReferences.MakeUnique(items.GetArrayElementAtIndex(index), copy);
                    InsertItemKey(index + 1, copy, index < itemKeys.Count ? itemKeys[index] : (long?)null);
                }
                else
                {
                    InvalidateItemKeys();
                }

                ApplyChange(listOwner);
            }

            void DeleteListItem(int index)
            {
                if (!TryGetListItem(index, out SerializedProperty items, out SerializedProperty item))
                    return;

                options.BeforeChange?.Invoke();
                if (options.DeleteItem != null)
                {
                    if (!options.DeleteItem(item) || !TryGetListItem(index, out items, out item))
                        return;
                }

                Undo.SetCurrentGroupName("Delete List Item");
                int previousCount = items.arraySize;
                item.DeleteCommand();

                if (items.arraySize < previousCount) { RemoveItemKey(index); }
                else { InvalidateItemKeys(); }

                ApplyChange(listOwner);
            }

            void SetItemsExpanded(bool expanded)
            {
                if (TryGetList(out SerializedProperty items))
                    SetItemsExpanded(items, expanded);
            }

            void SetItemsExpanded(SerializedProperty items, bool expanded)
            {
                pendingItemsExpanded = null;

                // A drag finds its targets with the row heights it started with
                if (IsDragging)
                    return;

                if (expanded)
                    items.isExpanded = true;

                int itemCount = items.arraySize;
                SerializedProperty item = itemCount > 0 ? items.GetArrayElementAtIndex(0) : null;

                // Like other changes from outside a row's header, rows snap to the new state. Rows that change are estimated
                // again, so the scroll range is right before they're drawn
                for (int i = 0; i < itemCount; i++)
                {
                    item.isExpanded = expanded;
                    if (i < itemKeys.Count)
                    {
                        long key = itemKeys[i];
                        if (!foldoutStates.TryGetValue(key, out bool wasExpanded) || wasExpanded != expanded)
                            estimatedKeys.Add(key);

                        foldoutStates[key] = expanded;
                        rowAnimations.Remove(key);
                    }

                    if (i < itemCount - 1)
                        item.Next(false);
                }

                metricsDirty = true;
                RepaintView();
            }

            void CopyListItem(int index, bool customClipboard)
            {
                if (!TryGetListItem(index, out _, out SerializedProperty item))
                    return;

                if (customClipboard) { options.CopyItem(item); }
                else { SerializedPropertyClipboard.Copy(item); }
            }

            void PasteListItem(int index, bool customClipboard)
            {
                if (!TryGetListItem(index, out _, out SerializedProperty item))
                    return;

                if (!(customClipboard ? options.CanPasteItem(item) : SerializedPropertyClipboard.CanPaste(item)))
                    return;

                options.BeforeChange?.Invoke();
                Undo.SetCurrentGroupName("Paste List Item");

                if (customClipboard)
                {
                    options.PasteItem(item);
                    ApplyChange(listOwner);
                }
                else if (SerializedPropertyClipboard.Paste(item))
                {
                    ApplyChange(listOwner);
                }
            }

            void CopyListClipboard()
            {
                if (TryGetClipboardRoot(out SerializedProperty root))
                    SerializedPropertyClipboard.Copy(root);
            }

            void PasteListClipboard()
            {
                if (!options.AllowClipboard || !options.AllowListPaste || !TryGetClipboardRoot(out SerializedProperty root)
                    || !SerializedPropertyClipboard.CanPaste(root))
                    return;

                options.BeforeChange?.Invoke();
                Undo.SetCurrentGroupName("Paste List");

                if (SerializedPropertyClipboard.Paste(root))
                {
                    InvalidateItemKeys();
                    ApplyChange(clipboardOwner);
                }
            }

            void ApplyListOverride(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath) || !TryGetList(out SerializedProperty items) || !HasPrefabOverride(items))
                    return;

                options.BeforeChange?.Invoke();
                PrefabUtility.ApplyPropertyOverride(items, assetPath, InteractionMode.UserAction);
                RefreshAfterPrefabChange();
            }

            void RevertListOverride()
            {
                if (!TryGetList(out SerializedProperty items) || !HasPrefabOverride(items))
                    return;

                options.BeforeChange?.Invoke();
                PrefabUtility.RevertPropertyOverride(items, InteractionMode.UserAction);
                RefreshAfterPrefabChange();
            }

            void RefreshAfterPrefabChange()
            {
                listOwner.UpdateIfRequiredOrScript();
                InvalidateItemKeys();
                options.AfterChange?.Invoke();
                RepaintView();
            }

            void ApplyChange(SerializedObject owner)
            {
                owner.ApplyModifiedProperties();
                metricsDirty = true;
                options.AfterChange?.Invoke();
                RepaintView();
            }

            static void RepaintView()
            {
                // Same window as EvoEditorGUI's hover repaints; HandleUtility.Repaint would log errors from menu callbacks
                EditorWindow window = EditorWindow.mouseOverWindow != null ? EditorWindow.mouseOverWindow : EditorWindow.focusedWindow;
                if (window != null)
                    window.Repaint();
            }

            bool TryGetListItem(int index, out SerializedProperty items, out SerializedProperty item)
            {
                item = null;
                if (!TryGetList(out items) || index < 0 || index >= items.arraySize)
                    return false;

                item = items.GetArrayElementAtIndex(index);
                return true;
            }

            bool TryGetList(out SerializedProperty items) => TryGetProperty(listOwner, listPath, out items) && items.isArray;

            bool TryGetClipboardRoot(out SerializedProperty root) => TryGetProperty(clipboardOwner, clipboardPath, out root);

            static bool TryGetProperty(SerializedObject owner, string propertyPath, out SerializedProperty property)
            {
                // Actions can run from a menu after the inspector changed, so start from the object's current state
                property = null;
                if (owner == null || owner.targetObject == null || string.IsNullOrEmpty(propertyPath))
                    return false;

                owner.ApplyModifiedProperties();
                owner.UpdateIfRequiredOrScript();
                property = owner.FindProperty(propertyPath);
                return property != null;
            }
            #endregion

            #region Prefab Overrides
            static bool HasPrefabOverride(SerializedProperty property)
            {
                return property != null && property.isInstantiatedPrefab && property.prefabOverride && !property.isDefaultOverride;
            }

            static void DrawPrefabOverride(SerializedProperty property, Rect rect)
            {
                if (Event.current.type != EventType.Repaint || !HasPrefabOverride(property))
                    return;

                // An empty property scope draws Unity's override margin without touching the header's label style
                EditorGUI.BeginProperty(rect, GUIContent.none, property);
                EditorGUI.EndProperty();
            }

            static List<string> GetPrefabApplyTargets(SerializedProperty property)
            {
                var paths = new List<string>();
                UnityEngine.Object target = property?.serializedObject.targetObject;
                if (target == null || !PrefabUtility.IsPartOfNonAssetPrefabInstance(target))
                    return paths;

                GameObject targetObject = target is Component component ? component.gameObject : target as GameObject;
                for (Transform current = targetObject != null ? targetObject.transform : null; current != null; current = current.parent)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(current.gameObject))
                        AddPrefabApplyTarget(paths, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject));
                }

                UnityEngine.Object source = target;
                while ((source = PrefabUtility.GetCorrespondingObjectFromSource(source)) != null)
                    AddPrefabApplyTarget(paths, AssetDatabase.GetAssetPath(source));

                return paths;
            }

            static void AddPrefabApplyTarget(List<string> paths, string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath) || paths.Contains(assetPath))
                    return;

                UnityEngine.Object prefab = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (prefab != null && PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(prefab))
                    paths.Add(assetPath);
            }
            #endregion

            #region Managed References
            sealed class ManagedReferenceSnapshot : ScriptableObject
            {
                [SerializeReference] public List<object> Values = new();
            }

            static class ManagedReferences
            {
                // Visits every managed reference under property, skipping the inside of one (its object graph is copied with it)
                public static void Visit(SerializedProperty property, Action<SerializedProperty, string> visit)
                {
                    if (property.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        visit(property, string.Empty);
                        return;
                    }

                    SerializedProperty iterator = property.Copy();
                    SerializedProperty end = iterator.GetEndProperty();
                    int prefixLength = property.propertyPath.Length + 1;
                    bool enterChildren = true;

                    while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        enterChildren = iterator.propertyType != SerializedPropertyType.ManagedReference;
                        if (!enterChildren)
                            visit(iterator, iterator.propertyPath[prefixLength..]);
                    }
                }

                public static ManagedReferenceSnapshot CreateSnapshot()
                {
                    var snapshot = ScriptableObject.CreateInstance<ManagedReferenceSnapshot>();
                    snapshot.hideFlags = HideFlags.HideAndDontSave;
                    return snapshot;
                }

                /// <summary>
                /// Gives <paramref name="copy"/> its own copies of the managed reference objects it shares with <paramref name="source"/>.
                /// </summary>
                public static void MakeUnique(SerializedProperty source, SerializedProperty copy)
                {
                    List<string> sharedPaths = null;
                    Visit(copy, (reference, relativePath) =>
                    {
                        SerializedProperty original = relativePath.Length == 0 ? source : source.FindPropertyRelative(relativePath);
                        if (original != null && original.propertyType == SerializedPropertyType.ManagedReference
                            && reference.managedReferenceValue != null && original.managedReferenceId == reference.managedReferenceId)
                            (sharedPaths ??= new List<string>()).Add(relativePath);
                    });

                    if (sharedPaths == null)
                        return;

                    // Cloning the snapshot copies all shared objects together, so references between them stay intact
                    ManagedReferenceSnapshot snapshot = CreateSnapshot();
                    ManagedReferenceSnapshot clone = null;
                    try
                    {
                        foreach (string relativePath in sharedPaths)
                            snapshot.Values.Add(GetRelative(copy, relativePath).managedReferenceValue);

                        clone = UnityEngine.Object.Instantiate(snapshot);
                        for (int i = 0; i < sharedPaths.Count; i++)
                            GetRelative(copy, sharedPaths[i]).managedReferenceValue = clone.Values[i];
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(snapshot);
                        if (clone != null)
                            UnityEngine.Object.DestroyImmediate(clone);
                    }
                }

                public static SerializedProperty GetRelative(SerializedProperty property, string relativePath)
                {
                    return relativePath.Length == 0 ? property : property.FindPropertyRelative(relativePath);
                }
            }
            #endregion

            #region Clipboard
            [NoAutoStaticsCleanup]
            static class SerializedPropertyClipboard
            {
                const BindingFlags MethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                const string ManagedReferenceMarkerPrefix = "EvoManagedReference:";

                readonly struct ManagedReferenceInfo
                {
                    public readonly string RelativePath;
                    public readonly string FieldType;

                    public ManagedReferenceInfo(string relativePath, string fieldType)
                    {
                        RelativePath = relativePath;
                        FieldType = fieldType;
                    }
                }

                // Unity's own property clipboard (internal), the one behind the inspector's Copy/Paste Property
                static readonly Type ClipboardType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Clipboard");
                static readonly MethodInfo HasSerializedProperty = ClipboardType?.GetMethod(
                    "HasSerializedProperty", MethodFlags, null, Type.EmptyTypes, null);
                static readonly MethodInfo SetSerializedProperty = ClipboardType?.GetMethod(
                    "SetSerializedProperty", MethodFlags, null, new[] { typeof(SerializedProperty) }, null);
                static readonly MethodInfo GetSerializedProperty = ClipboardType?.GetMethod(
                    "GetSerializedProperty", MethodFlags, null, new[] { typeof(SerializedProperty) }, null);

                // It doesn't carry managed references, so those are kept here alongside it
                static readonly List<ManagedReferenceInfo> managedReferences = new();
                static ManagedReferenceSnapshot snapshot;
                static string clipboardMarker;
                static string rootType;
                static string rootArrayElementType;
                static string rootManagedReferenceFieldType;
                static SerializedPropertyType rootPropertyType;
                static bool rootIsArray;

                static bool IsNativeSupported => HasSerializedProperty != null && SetSerializedProperty != null && GetSerializedProperty != null;

                static SerializedPropertyClipboard() => AssemblyReloadEvents.beforeAssemblyReload += Clear;

                public static bool CanCopy(SerializedProperty property)
                {
                    return property != null && (property.propertyType == SerializedPropertyType.ManagedReference || IsNativeSupported);
                }

                public static bool CanPaste(SerializedProperty property)
                {
                    if (property == null)
                        return false;

                    if (HasSnapshot)
                        return IsCompatible(property);

                    return property.propertyType != SerializedPropertyType.ManagedReference && HasNativeValue;
                }

                public static bool Copy(SerializedProperty property)
                {
                    if (property == null)
                        return false;

                    Clear();
                    bool isReference = property.propertyType == SerializedPropertyType.ManagedReference;
                    if (!isReference && !Invoke(SetSerializedProperty, property))
                        return false;

                    // A managed reference has no native clipboard text, so a marker tells whether the system clipboard still holds it
                    clipboardMarker = isReference ? ManagedReferenceMarkerPrefix + Guid.NewGuid().ToString("N") : EditorGUIUtility.systemCopyBuffer;
                    if (!CaptureManagedReferences(property))
                        return false;

                    if (isReference)
                        EditorGUIUtility.systemCopyBuffer = clipboardMarker;

                    return true;
                }

                public static bool Paste(SerializedProperty property)
                {
                    if (property == null || !CanPaste(property))
                        return false;

                    if (property.propertyType != SerializedPropertyType.ManagedReference && !Invoke(GetSerializedProperty, property))
                        return false;

                    return RestoreManagedReferences(property);
                }

                static bool HasNativeValue
                {
                    get
                    {
                        if (!IsNativeSupported)
                            return false;

                        try { return (bool)HasSerializedProperty.Invoke(null, null); }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            return false;
                        }
                    }
                }

                static bool HasSnapshot
                {
                    get
                    {
                        if (snapshot == null || managedReferences.Count == 0)
                            return false;

                        if (!string.IsNullOrEmpty(clipboardMarker) && EditorGUIUtility.systemCopyBuffer == clipboardMarker)
                            return true;

                        // Something else was copied since
                        Clear();
                        return false;
                    }
                }

                static bool CaptureManagedReferences(SerializedProperty property)
                {
                    ManagedReferenceSnapshot source = null;

                    try
                    {
                        ManagedReferences.Visit(property, (reference, relativePath) =>
                        {
                            source = source != null ? source : ManagedReferences.CreateSnapshot();
                            managedReferences.Add(new ManagedReferenceInfo(relativePath, reference.managedReferenceFieldTypename));
                            source.Values.Add(reference.managedReferenceValue);
                        });

                        if (source == null)
                            return true;

                        // Cloned now, so later edits to the copied item don't change what gets pasted
                        snapshot = UnityEngine.Object.Instantiate(source);
                        snapshot.hideFlags = HideFlags.HideAndDontSave;
                        rootType = property.type;
                        rootPropertyType = property.propertyType;
                        rootIsArray = property.isArray;
                        rootArrayElementType = property.isArray ? property.arrayElementType : null;
                        rootManagedReferenceFieldType = property.propertyType == SerializedPropertyType.ManagedReference
                            ? property.managedReferenceFieldTypename
                            : null;
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Clear();
                        Debug.LogException(exception);
                        return false;
                    }
                    finally
                    {
                        if (source != null)
                            UnityEngine.Object.DestroyImmediate(source);
                    }
                }

                static bool RestoreManagedReferences(SerializedProperty property)
                {
                    if (!HasSnapshot)
                        return true;

                    ManagedReferenceSnapshot clone = null;
                    try
                    {
                        // Each paste gets its own objects
                        clone = UnityEngine.Object.Instantiate(snapshot);
                        if (clone.Values.Count != managedReferences.Count)
                            return false;

                        for (int i = 0; i < managedReferences.Count; i++)
                        {
                            ManagedReferenceInfo info = managedReferences[i];
                            SerializedProperty destination = ManagedReferences.GetRelative(property, info.RelativePath);
                            if (destination == null || destination.propertyType != SerializedPropertyType.ManagedReference
                                || destination.managedReferenceFieldTypename != info.FieldType)
                                return false;

                            destination.managedReferenceValue = clone.Values[i];
                        }

                        return true;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return false;
                    }
                    finally
                    {
                        if (clone != null)
                            UnityEngine.Object.DestroyImmediate(clone);
                    }
                }

                static bool IsCompatible(SerializedProperty property)
                {
                    return property.propertyType == rootPropertyType && property.type == rootType && property.isArray == rootIsArray
                        && (!property.isArray || property.arrayElementType == rootArrayElementType)
                        && (property.propertyType != SerializedPropertyType.ManagedReference
                        || property.managedReferenceFieldTypename == rootManagedReferenceFieldType);
                }

                static void Clear()
                {
                    if (snapshot != null)
                        UnityEngine.Object.DestroyImmediate(snapshot);

                    snapshot = null;
                    managedReferences.Clear();
                    clipboardMarker = null;
                    rootType = null;
                    rootArrayElementType = null;
                    rootManagedReferenceFieldType = null;
                    rootPropertyType = default;
                    rootIsArray = false;
                }

                static bool Invoke(MethodInfo method, SerializedProperty property)
                {
                    if (method == null || property == null)
                        return false;

                    try
                    {
                        method.Invoke(null, new object[] { property });
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        return false;
                    }
                }
            }
            #endregion
        }
    }
}