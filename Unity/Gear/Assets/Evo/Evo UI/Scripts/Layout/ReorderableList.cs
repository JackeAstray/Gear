using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Layout/Reorderable List"), HelpURL(Constants.HelpUrl + "layout/reorderable-list")]
    public class ReorderableList : MonoBehaviour
    {
        // Settings
        public bool instantSnap = false;
        [Tooltip("Used if there is no layout group attached.")]
        public float itemSpacing = 10f;
        [Min(0.05f)] public float animationDuration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Drag Settings
        [Range(0.1f, 1f), Tooltip("Sets the transparency of the object while it's being dragged.")]
        public float dragAlpha = 1f;
        [Range(1f, 2f), Tooltip("Sets the scale of the object while it's being dragged.")]
        public float dragScale = 1.2f;

        // List Transfer Settings
        [Tooltip("Allows items to move between enabled lists in the same group and root canvas.")]
        public bool allowListTransfer = false;
        [Tooltip("Only lists with the same group can exchange items. Leave empty to use the default group.")]
        public string transferGroup = "";
        [Tooltip("Optional drop area, such as a scroll viewport. Defaults to the list container.")]
        public RectTransform dropArea;

        // References
        public RectTransform listContainer;
        [SerializeField] private Canvas canvas;

        // Events
        public UnityEvent<ReorderableListItem, int, int> onOrderChanged = new();
        public UnityEvent<ReorderableListItem, ReorderableList, ReorderableList> onListChanged = new();

        // Cache
        Camera uiCamera;
        LayoutGroup layoutGroup;
        GridLayoutGroup gridLayout;
        ContentSizeFitter contentSizeFitter;
        ReorderableListItem draggedItem;
        WaitForEndOfFrame cachedEndOfFrame;

        readonly List<ReorderableListItem> activeSiblingsCache = new(32);
        readonly List<ReorderableListItem> previousItemsCache = new(32);
        readonly HashSet<ReorderableListItem> newItemsCache = new();
        readonly HashSet<ReorderableListItem> itemSet = new();
        readonly List<ReorderableListItem> previewItemsCache = new(32);
        readonly List<ReorderableListItem> insertionItemsCache = new(32);
        readonly List<Vector3> startPositionsCache = new(32);
        readonly List<ReorderableListItem> items = new();

        // List Transfer Cache
        static readonly List<ReorderableList> activeLists = new();
        readonly List<RaycastResult> raycastResultsCache = new(32);
        ReorderableList dragSourceList;
        ReorderableList dropTarget;
        RectTransform dragLayer;
        PointerEventData dragPointerData;
        Vector2 dragAnchorMin;
        Vector2 dragAnchorMax;
        Vector2 dragSizeDelta;
        Vector2 dragItemSize;
        Vector3 dragOriginalScale;
        Quaternion dragOriginalRotation;
        float dragOriginalAlpha;
        bool dragBlocksRaycasts;
        ReorderableListItem pendingTransferItem;
        int pendingTransferIndex;
        bool pendingTransferActive;

        // Native Layout Cache
        static readonly List<RectTransform> nativeLayoutRoots = new();
        static readonly List<ReorderableList> nativeLists = new();
        static bool updatingNativeLayouts;
        static bool nativeLayoutsDirty;
        readonly List<NativeLayoutState> nativeLayoutCache = new(32);
        readonly Dictionary<ReorderableListItem, NativeLayoutState> nativeLayoutLookup = new();
        readonly Stack<NativeLayoutState> nativeLayoutPool = new();
        readonly Dictionary<ReorderableList, Rect> dropBoundsCache = new();
        readonly Vector3[] dropCornersCache = new Vector3[4];
        RectTransform dragPlaceholder;
        bool nativeLayoutActive;

        class NativeLayoutState
        {
            public ReorderableListItem item;
            public Vector3 position;
            public Vector3 startPosition;
            public Vector3 targetPosition;
        }

        // Trackers
        Coroutine layoutRefreshCoroutine;
        Coroutine dockRefreshCoroutine;
        TweenHandle layoutAnimation;
        TweenHandle dragScaleAnimation;
        TweenHandle dragFadeAnimation;
        Transform scaleTarget;
        CanvasGroup fadeTarget;
        Action<float> layoutProgressSetter;
        Action layoutCompleted;
        float layoutProgress;

        // State Restoration for OnEnable
        bool isStartup = true;
        int pendingResortIndex = -1;
        ReorderableListItem pendingResortItem;

        // Layout group alignment cache
        TextAnchor childAlignment = TextAnchor.MiddleCenter;
        RectOffset layoutPadding;

        // Helpers
        int draggedFromIndex = -1;
        int previewInsertIndex = -1;
        LayoutType layoutType = LayoutType.Horizontal;

        // Enums
        public enum LayoutType { Horizontal, Vertical, Grid }

        // Properties
        public DockMagnifier DockMagnifier { get; set; }
        public bool IsDraggingActive => draggedItem != null;
        public bool IsAnimating { get; private set; }
        public bool IsDragging(ReorderableListItem item) => draggedItem == item;
        public List<ReorderableListItem> Items() => items;

        void Awake()
        {
            cachedEndOfFrame = new WaitForEndOfFrame();
            layoutProgressSetter = SetLayoutProgress;
            layoutCompleted = CompleteLayoutAnimation;

            // Set default refs if not assigned
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (listContainer == null)
                listContainer = GetComponent<RectTransform>();

            // Cache UI camera
            uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // Detect layout group and content size fitter
            layoutGroup = listContainer.GetComponent<LayoutGroup>();
            contentSizeFitter = listContainer.GetComponent<ContentSizeFitter>();
            gridLayout = listContainer.GetComponent<GridLayoutGroup>();

            if (layoutGroup != null)
            {
                if (layoutGroup is GridLayoutGroup)
                {
                    layoutType = LayoutType.Grid;
                    itemSpacing = 0; // Handled by grid cellSize/spacing
                }
                else if (layoutGroup is HorizontalLayoutGroup group)
                {
                    layoutType = LayoutType.Horizontal;
                    itemSpacing = group.spacing;
                }
                else if (layoutGroup is VerticalLayoutGroup verticalGroup)
                {
                    layoutType = LayoutType.Vertical;
                    itemSpacing = verticalGroup.spacing;
                }

                childAlignment = layoutGroup.childAlignment;
                layoutPadding = layoutGroup.padding;
            }

            // Initialize padding
            layoutPadding ??= new RectOffset(0, 0, 0, 0);
        }

        void OnEnable()
        {
            if (!activeLists.Contains(this))
                activeLists.Add(this);

            if (isStartup) { StartCoroutine(StartupInitialization()); }
            else
            {
                RestorePendingResort();
                RefreshItemsList();
            }
        }

        void OnDisable()
        {
            activeLists.Remove(this);

            if (dragSourceList != null)
                dragSourceList.FinishListTransfer(false, false);

            layoutAnimation.Kill();
            dragScaleAnimation.Complete();
            dragFadeAnimation.Complete();
            layoutAnimation = default;
            dragScaleAnimation = default;
            dragFadeAnimation = default;
            scaleTarget = null;
            fadeTarget = null;
            ClearNativeLayout();
            StopAllCoroutines();
            layoutRefreshCoroutine = null;
            dockRefreshCoroutine = null;

            if (draggedItem != null)
            {
                draggedItem.ResetDragState();
                draggedItem.transform.localScale = dragOriginalScale;
                draggedItem.canvasGroup.alpha = dragOriginalAlpha;

                if (draggedFromIndex >= 0)
                {
                    if (gameObject.activeInHierarchy) { SetActiveSiblingIndex(draggedItem, draggedFromIndex); }
                    else
                    {
                        pendingResortItem = draggedItem;
                        pendingResortIndex = draggedFromIndex;
                    }
                }
            }

            ResetState();
            IsAnimating = false;
            SetLayoutEnabled(true);
        }

        void OnDestroy()
        {
            if (dragPlaceholder != null)
                Destroy(dragPlaceholder.gameObject);
            if (dragLayer != null)
                Destroy(dragLayer.gameObject);
        }

        IEnumerator StartupInitialization()
        {
            // Wait one frame to allow Unity's native UI Layout Group to calculate initial sizes and positions.
            // This prevents items from stacking at 0,0 due to 0 width/height.
            yield return null;

            isStartup = false;
            RestorePendingResort();
            RefreshItemsList();
        }

        #region Hierarchy Helpers
        /// <summary>
        /// Sets the absolute sibling index of an item so its visual order matches the desired active index.
        /// Prevents critical desyncs when inactive elements exist in the same hierarchy.
        /// </summary>
        void SetActiveSiblingIndex(ReorderableListItem item, int targetActiveIndex)
        {
            if (item == null)
                return;

            // Gather all active siblings EXCEPT the item we are moving using a cached list to avoid GC
            activeSiblingsCache.Clear();
            for (int i = 0; i < listContainer.childCount; i++)
            {
                Transform child = listContainer.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                    continue;
                if (child.TryGetComponent<ReorderableListItem>(out var listItem) && listItem != item)
                    activeSiblingsCache.Add(listItem);
            }

            if (targetActiveIndex >= activeSiblingsCache.Count) { item.transform.SetAsLastSibling(); }
            else
            {
                targetActiveIndex = Mathf.Max(0, targetActiveIndex);
                int targetSiblingIndex = activeSiblingsCache[targetActiveIndex].transform.GetSiblingIndex();

                // If the item currently sits before the target in the absolute hierarchy,
                // removing it will shift the target's index down by 1.
                if (item.transform.GetSiblingIndex() < targetSiblingIndex)
                    targetSiblingIndex--;

                item.transform.SetSiblingIndex(targetSiblingIndex);
            }
        }

        void RestorePendingResort()
        {
            // If there's a pending restoration that was skipped in OnDisable (due to deactivation), apply it now
            if (pendingResortItem != null && pendingResortIndex >= 0)
            {
                SetActiveSiblingIndex(pendingResortItem, pendingResortIndex);
                pendingResortItem = null;
                pendingResortIndex = -1;
            }
        }

        void DetachItemInternal(RectTransform itemRect)
        {
            if (draggedItem != null && draggedItem.CachedRectTransform == itemRect)
                CancelDrag();

            if (itemRect.TryGetComponent<ReorderableListItem>(out var listItem))
            {
                items.Remove(listItem);
                Destroy(listItem);
            }
            itemRect.SetParent(null, true);
        }

        void QueueDockRefresh()
        {
            if (DockMagnifier == null)
                return;

            if (!isActiveAndEnabled)
                DockMagnifier.RefreshTargets();
            else 
                dockRefreshCoroutine ??= StartCoroutine(DelayedDockRefresh());
        }

        IEnumerator DelayedDockRefresh()
        {
            yield return cachedEndOfFrame;
            dockRefreshCoroutine = null;
            if (DockMagnifier != null)
                DockMagnifier.RefreshTargets();
        }
        #endregion

        #region Layout Geometry
        // Container edges in local space
        float ContainerLeft() => -listContainer.rect.width * listContainer.pivot.x;
        float ContainerRight() => listContainer.rect.width * (1f - listContainer.pivot.x);
        float ContainerBottom() => -listContainer.rect.height * listContainer.pivot.y;
        float ContainerTop() => listContainer.rect.height * (1f - listContainer.pivot.y);

        // What localPosition should an item have so that its left/right/top/bottom edge aligns with a given coordinate?
        float ItemXFromLeft(RectTransform r, float leftEdge) => leftEdge + r.rect.width * r.pivot.x;
        float ItemXFromRight(RectTransform r, float rightEdge) => rightEdge - r.rect.width * (1f - r.pivot.x);
        float ItemYFromTop(RectTransform r, float topEdge) => topEdge - r.rect.height * (1f - r.pivot.y);
        float ItemYFromBottom(RectTransform r, float bottomEdge) => bottomEdge + r.rect.height * r.pivot.y;

        /// <summary>
        /// Explicitly snaps specific items to their designated target position instantly.
        /// </summary>
        void SnapItemsToTarget(HashSet<ReorderableListItem> targetItems)
        {
            float currentPos = (layoutType == LayoutType.Grid) ? 0 : CalculateStartPosition();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                RectTransform itemRect = items[i].CachedRectTransform;
                Vector3 targetPos;

                if (layoutType == LayoutType.Grid) 
                { 
                    targetPos = GetGridPosition(i, items.Count) 
                        + Vector3.Scale(itemRect.rect.size, itemRect.pivot - Vector2.one * 0.5f); 
                }
                else
                {
                    targetPos = GetLinearPosition(currentPos, itemRect);

                    if (layoutType == LayoutType.Horizontal)
                        currentPos += GetItemSize(itemRect) + itemSpacing;
                    else
                        currentPos -= GetItemSize(itemRect) + itemSpacing;
                }

                // Check for target items
                if (items[i] != draggedItem && targetItems.Contains(items[i]))
                    items[i].transform.localPosition = targetPos;
            }
        }

        float CalculateStartPosition(List<ReorderableListItem> layoutItems = null)
        {
            layoutItems ??= items;
            float totalSize = 0f;
            int count = 0;
            for (int i = 0; i < layoutItems.Count; i++)
            {
                if (layoutItems[i] == null)
                    continue;

                totalSize += GetItemSize(layoutItems[i].CachedRectTransform);
                count++;
            }
            totalSize += Mathf.Max(0, count - 1) * itemSpacing;

            if (layoutType == LayoutType.Horizontal)
            {
                float left = ContainerLeft() + layoutPadding.left;
                float width = listContainer.rect.width - layoutPadding.horizontal;
                return left + (width - totalSize) * ((int)childAlignment % 3) * 0.5f;
            }

            float top = ContainerTop() - layoutPadding.top;
            float height = listContainer.rect.height - layoutPadding.vertical;
            return top - (height - totalSize) * ((int)childAlignment / 3) * 0.5f;
        }

        /// <summary>
        /// Checks DockMagnifier for the correct resting size.
        /// </summary>
        float GetItemSize(RectTransform itemRect)
        {
            if (layoutType == LayoutType.Grid && gridLayout != null)
            {
                // Grids enforce cell size, typically
                return (gridLayout.startAxis == GridLayoutGroup.Axis.Horizontal) ? gridLayout.cellSize.x : gridLayout.cellSize.y;
            }

            if (DockMagnifier != null && DockMagnifier.isActiveAndEnabled && IsDraggingActive)
            {
                // Dragging: Prioritize stable baseline size so target slots don't violently shift
                Vector2 restingSize = DockMagnifier.GetRestingSize(itemRect);
                if (layoutType == LayoutType.Horizontal && restingSize.x > 0.1f)
                    return restingSize.x;
                if (layoutType == LayoutType.Vertical && restingSize.y > 0.1f)
                    return restingSize.y;
            }

            return (layoutType == LayoutType.Horizontal) ? itemRect.rect.width : itemRect.rect.height;
        }

        int GetInsertionIndex()
        {
            if (draggedItem == null)
                return 0;

            if (layoutGroup != null && dragSourceList != null)
                return GetNativeInsertionIndex();

            Vector3 draggedPosition = listContainer.InverseTransformPoint(draggedItem.transform.position);
           
            if (layoutType == LayoutType.Grid)
            {
                draggedPosition += (Vector3)draggedItem.CachedRectTransform.rect.center;
                float minDistance = float.MaxValue;
                int bestIndex = 0;
                int totalItems = items.Count + (items.Contains(draggedItem) ? 0 : 1);
                for (int i = 0; i < totalItems; i++)
                {
                    float distance = Vector3.SqrMagnitude(draggedPosition - GetGridPosition(i, totalItems));
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestIndex = i;
                    }
                }
                return bestIndex;
            }

            RectTransform draggedRect = draggedItem.CachedRectTransform;
            float draggedSize = GetItemSize(draggedRect);
            int axis = layoutType == LayoutType.Horizontal ? 0 : 1;
            float direction = axis == 0 ? 1f : -1f;
            float draggedCenter = draggedPosition[axis] + draggedRect.rect.size[axis] * (0.5f - draggedRect.pivot[axis]);
           
            insertionItemsCache.Clear();
            insertionItemsCache.AddRange(items);
            if (!insertionItemsCache.Contains(draggedItem))
                insertionItemsCache.Add(draggedItem);

            float currentPos = CalculateStartPosition(insertionItemsCache);

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;

                float itemSize = GetItemSize(items[i].CachedRectTransform);
                float itemCenter = currentPos + direction * itemSize * 0.5f;
                currentPos += direction * (itemSize + itemSpacing);
              
                if (items[i] == draggedItem)
                    continue;

                float draggedEdge = draggedCenter + direction * draggedSize * 0.5f * (draggedFromIndex < i ? 1f : -1f);
                if ((draggedEdge - itemCenter) * direction < 0f)
                    return draggedFromIndex >= 0 && draggedFromIndex < i ? i - 1 : i;
            }
            return Mathf.Max(0, items.Count - (draggedFromIndex >= 0 ? 1 : 0));
        }

        Vector3 GetLinearPosition(float currentPos, RectTransform itemRect)
        {
            if (layoutType == LayoutType.Horizontal)
                return new Vector3(ItemXFromLeft(itemRect, currentPos), GetAlignedPosition(itemRect, 1), 0);

            return new Vector3(GetAlignedPosition(itemRect, 0), ItemYFromTop(itemRect, currentPos), 0);
        }

        float GetAlignedPosition(RectTransform itemRect, int axis)
        {
            if (axis == 0)
            {
                float left = ContainerLeft() + layoutPadding.left;
                float width = listContainer.rect.width - layoutPadding.horizontal - itemRect.rect.width;
                return ItemXFromLeft(itemRect, left + width * ((int)childAlignment % 3) * 0.5f);
            }

            float top = ContainerTop() - layoutPadding.top;
            float height = listContainer.rect.height - layoutPadding.vertical - itemRect.rect.height;
            return ItemYFromTop(itemRect, top - height * ((int)childAlignment / 3) * 0.5f);
        }

        Vector3 GetGridPosition(int index, int totalCount)
        {
            if (gridLayout == null || totalCount == 0)
                return Vector3.zero;

            Vector2 cellSize = gridLayout.cellSize;
            Vector2 spacing = gridLayout.spacing;
            int columns;
            int rows;
            bool horizontal = gridLayout.startAxis == GridLayoutGroup.Axis.Horizontal;

            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = Mathf.Max(1, gridLayout.constraintCount);
                rows = Mathf.CeilToInt(totalCount / (float)columns);
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                rows = Mathf.Max(1, gridLayout.constraintCount);
                columns = Mathf.CeilToInt(totalCount / (float)rows);
            }
            else
            {
                columns = cellSize.x + spacing.x <= 0f ? int.MaxValue : Mathf.Max(1, Mathf.FloorToInt((listContainer.rect.width - layoutPadding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
                rows = cellSize.y + spacing.y <= 0f ? int.MaxValue : Mathf.Max(1, Mathf.FloorToInt((listContainer.rect.height - layoutPadding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y)));
            }

            // Match Unity's occupied cells so incomplete grids retain their alignment.
            int cellsPerLine = horizontal ? columns : rows;
            columns = horizontal || gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Min(columns, totalCount) : Mathf.Clamp(columns, 1, Mathf.CeilToInt(totalCount / (float)rows));
            rows = !horizontal || gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount
                ? Mathf.Min(rows, totalCount) : Mathf.Clamp(rows, 1, Mathf.CeilToInt(totalCount / (float)cellsPerLine));
            int column = horizontal ? index % cellsPerLine : index / cellsPerLine;
            int row = horizontal ? index / cellsPerLine : index % cellsPerLine;
            // Unity distributes the final cells across constrained rows or columns
            int constraintCount = gridLayout.constraintCount;
            int childrenToMove = 0;

            if (cellsPerLine > 1 && totalCount > constraintCount && Mathf.CeilToInt(totalCount / (float)cellsPerLine) < constraintCount)
            {
                childrenToMove = constraintCount - Mathf.CeilToInt(totalCount / (float)cellsPerLine);
                childrenToMove += Mathf.FloorToInt(childrenToMove / (float)(cellsPerLine - 1));
               
                if (totalCount % cellsPerLine == 1)
                    childrenToMove++;
            }

            if (totalCount - index <= childrenToMove)
            {
                if (horizontal && gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
                {
                    column = 0;
                    row = constraintCount - (totalCount - index);
                }
                else if (!horizontal && gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                {
                    column = constraintCount - (totalCount - index);
                    row = 0;
                }
            }

            int corner = (int)gridLayout.startCorner;
            if (corner % 2 == 1)
                column = columns - 1 - column;
            if (corner / 2 == 1)
                row = rows - 1 - row;

            float width = columns * cellSize.x + (columns - 1) * spacing.x;
            float height = rows * cellSize.y + (rows - 1) * spacing.y;
            float left = ContainerLeft() + layoutPadding.left + (listContainer.rect.width - layoutPadding.horizontal - width) * ((int)childAlignment % 3) * 0.5f;
            float top = ContainerTop() - layoutPadding.top - (listContainer.rect.height - layoutPadding.vertical - height) * ((int)childAlignment / 3) * 0.5f;
            return new Vector3(left + column * (cellSize.x + spacing.x) + cellSize.x * 0.5f, top - row * (cellSize.y + spacing.y) - cellSize.y * 0.5f, 0);
        }

        #endregion

        #region Native Layout
        void RefreshNativeLayout()
        {
            if (!nativeLayoutActive)
            {
                if (nativeLists.Count == 0)
                {
                    // Register after Unity's layout pass so sizes are already resolved.
                    _ = CanvasUpdateRegistry.instance;
                    Canvas.willRenderCanvases += UpdateNativeLayouts;
                }

                nativeLists.Add(this);
                nativeLayoutActive = true;
            }
           
            SyncNativeLayoutItems();
           
            for (int i = 0; i < nativeLayoutCache.Count; i++)
                nativeLayoutCache[i].startPosition = nativeLayoutCache[i].position;

            nativeLayoutsDirty = true;
            SetLayoutEnabled(true);
            StartLayoutAnimation();
            LayoutRebuilder.MarkLayoutForRebuild(listContainer);
        }

        void SyncNativeLayoutItems()
        {
            for (int i = nativeLayoutCache.Count - 1; i >= 0; i--)
            {
                NativeLayoutState state = nativeLayoutCache[i];
                if (state.item != null && state.item != draggedItem && state.item.gameObject.activeInHierarchy && state.item.transform.parent == listContainer)
                    continue;

                nativeLayoutLookup.Remove(state.item);
                nativeLayoutCache.RemoveAt(i);
                state.item = null;
                nativeLayoutPool.Push(state);
            }

            for (int i = 0; i < items.Count; i++)
            {
                ReorderableListItem item = items[i];
                if (item == null || item == draggedItem || item.transform.parent != listContainer || nativeLayoutLookup.ContainsKey(item))
                    continue;

                NativeLayoutState state = nativeLayoutPool.Count > 0 ? nativeLayoutPool.Pop() : new NativeLayoutState();
                state.item = item;
                state.position = item.transform.position;
                state.startPosition = state.position;
                state.targetPosition = state.position;
                nativeLayoutCache.Add(state);
                nativeLayoutLookup.Add(item, state);
            }
        }

        void ClearNativeLayout()
        {
            nativeLayoutActive = false;
            nativeLists.Remove(this);
           
            if (nativeLists.Count == 0)
                Canvas.willRenderCanvases -= UpdateNativeLayouts;

            for (int i = 0; i < nativeLayoutCache.Count; i++)
            {
                NativeLayoutState state = nativeLayoutCache[i];
                state.item = null;
                nativeLayoutPool.Push(state);
            }

            nativeLayoutCache.Clear();
            nativeLayoutLookup.Clear();
        }

        static void UpdateNativeLayouts()
        {
            if (updatingNativeLayouts)
                return;

            updatingNativeLayouts = true;
            nativeLayoutsDirty = false;
         
            try
            {
                // Resolve all layout roots before applying any visual offsets.
                // This also keeps lists inside a shared layout group from overwriting each other.
                nativeLayoutRoots.Clear();
                for (int i = 0; i < nativeLists.Count; i++)
                {
                    ReorderableList list = nativeLists[i];
                    if (!list.nativeLayoutActive || !list.isActiveAndEnabled)
                        continue;

                    list.SyncNativeLayoutItems();
                    RectTransform root = list.listContainer;
                    while (root.parent is RectTransform parent && parent.TryGetComponent<LayoutGroup>(out var parentLayout) && parentLayout.isActiveAndEnabled)
                        root = parent;

                    bool covered = false;
                    for (int j = nativeLayoutRoots.Count - 1; j >= 0; j--)
                    {
                        if (root.IsChildOf(nativeLayoutRoots[j]))
                        {
                            covered = true;
                            break;
                        }

                        if (nativeLayoutRoots[j].IsChildOf(root))
                            nativeLayoutRoots.RemoveAt(j);
                    }

                    if (!covered)
                        nativeLayoutRoots.Add(root);
                }

                for (int i = 0; i < nativeLayoutRoots.Count; i++)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(nativeLayoutRoots[i]);

                for (int i = 0; i < activeLists.Count; i++)
                {
                    ReorderableList source = activeLists[i];
                    if (source.dragSourceList != source || source.draggedItem == null || source.dropTarget == null || source.dragPlaceholder == null)
                        continue;

                    RectTransform item = source.draggedItem.CachedRectTransform;
                    if (source.dropTarget.layoutGroup is GridLayoutGroup || (source.dropTarget.layoutGroup is HorizontalOrVerticalLayoutGroup group && group.childControlHeight))
                        item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, source.dragPlaceholder.rect.height);
                }

                // Keep insertion tests in layout coordinates, independent of moving graphics.
                for (int i = 0; i < nativeLists.Count; i++)
                {
                    ReorderableList list = nativeLists[i];
                    if (!list.nativeLayoutActive)
                        continue;

                    bool changed = false;
                    for (int j = 0; j < list.nativeLayoutCache.Count; j++)
                    {
                        NativeLayoutState state = list.nativeLayoutCache[j];
                        Vector3 targetPosition = state.item.transform.position;
                        changed |= Vector3.SqrMagnitude(targetPosition - state.targetPosition) > 0.001f;
                        state.targetPosition = targetPosition;
                    }

                    if (changed && !list.IsAnimating)
                    {
                        for (int j = 0; j < list.nativeLayoutCache.Count; j++)
                            list.nativeLayoutCache[j].startPosition = list.nativeLayoutCache[j].position;
                      
                        list.StartLayoutAnimation();
                    }
                }

                for (int i = nativeLists.Count - 1; i >= 0; i--)
                {
                    ReorderableList list = nativeLists[i];
                    float t = list.layoutProgress;
                    for (int j = 0; j < list.nativeLayoutCache.Count; j++)
                    {
                        NativeLayoutState state = list.nativeLayoutCache[j];
                        state.position = Vector3.Lerp(state.startPosition, state.targetPosition, t);
                        state.item.transform.position = state.position;
                    }

                    list.IsAnimating = list.layoutAnimation.IsActive;
                    if (!list.IsAnimating && !list.IsDraggingActive)
                        list.ClearNativeLayout();
                }
            }
            finally
            {
                // Do not retain layout roots between updates or across scene loads
                nativeLayoutRoots.Clear();
                updatingNativeLayouts = false;
            }
        }

        int GetNativeInsertionIndex()
        {
            // Pointer events reuse the latest resolved slots until the preview changes.
            if (nativeLayoutsDirty)
                UpdateNativeLayouts();

            RectTransform draggedRect = draggedItem.CachedRectTransform;
            Vector3 draggedPosition = listContainer.InverseTransformPoint(draggedRect.position);
            int axis = layoutType == LayoutType.Horizontal ? 0 : 1;

            // Match the regular drag rule using the resting size, independent of drag scale.
            float draggedSize = draggedRect.rect.size[axis];
            float draggedCenter = draggedPosition[axis] + draggedSize * (0.5f - draggedRect.pivot[axis]);
            int index = 0;
            float closestDistance = float.MaxValue;
            int closestIndex = 0;
            bool reverse = layoutGroup is HorizontalOrVerticalLayoutGroup group && group.reverseArrangement;
            float direction = (layoutType == LayoutType.Horizontal ? 1f : -1f) * (reverse ? -1f : 1f);

            for (int i = 0; i < items.Count; i++)
            {
                ReorderableListItem item = items[i];
                if (item == null || item == draggedItem)
                    continue;

                Vector3 position = item.transform.position;
                if (nativeLayoutLookup.TryGetValue(item, out NativeLayoutState state))
                    position = state.targetPosition;

                Vector3 center = position + item.transform.TransformVector(item.CachedRectTransform.rect.center);
                if (layoutType == LayoutType.Grid)
                {
                    float distance = Vector3.SqrMagnitude(listContainer.InverseTransformPoint(center) - draggedPosition);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = index >= previewInsertIndex ? index + 1 : index;
                    }
                }
                else
                {
                    Vector3 localCenter = listContainer.InverseTransformPoint(center);
                    // Use the edge facing this sibling. The current gap keeps the threshold
                    // stable after neighbors move to make room for the dragged item.
                    float edgeOffset = draggedSize * 0.5f * direction;
                    float draggedEdge = draggedCenter + (index >= previewInsertIndex ? edgeOffset : -edgeOffset);
                    bool before = direction > 0f ? draggedEdge < localCenter[axis] : draggedEdge > localCenter[axis];
                    if (before)
                        return index;
                }

                index++;
            }

            if (layoutType != LayoutType.Grid)
                return index;

            RectTransform placeholder = dragSourceList.dragPlaceholder;
            if (placeholder != null)
            {
                Vector3 center = listContainer.InverseTransformPoint(placeholder.TransformPoint(placeholder.rect.center));
                if (Vector3.SqrMagnitude(center - draggedPosition) < closestDistance)
                    return Mathf.Clamp(previewInsertIndex, 0, index);
            }
            return Mathf.Clamp(closestIndex, 0, index);
        }
        #endregion

        #region List Transfer
        void MoveDragPlaceholder()
        {
            if (dragPlaceholder == null)
                return;

            if (dropTarget == null)
            {
                dragPlaceholder.gameObject.SetActive(false);
                return;
            }

            if (dragPlaceholder.parent != dropTarget.listContainer)
            {
                dragPlaceholder.SetParent(dropTarget.listContainer, false);
                dragPlaceholder.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dragItemSize.x);
                dragPlaceholder.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dragItemSize.y);
                draggedItem.CachedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dragItemSize.x);
                draggedItem.CachedRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dragItemSize.y);
            }

            dragPlaceholder.gameObject.SetActive(true);
            int index = 0;

            for (int i = 0; i < dropTarget.items.Count; i++)
            {
                ReorderableListItem item = dropTarget.items[i];
                if (item == null || item == draggedItem)
                    continue;

                if (index++ == dropTarget.previewInsertIndex)
                {
                    int siblingIndex = item.transform.GetSiblingIndex();
                    if (dragPlaceholder.GetSiblingIndex() < siblingIndex)
                        siblingIndex--;
                   
                    dragPlaceholder.SetSiblingIndex(siblingIndex);
                    return;
                }
            }

            dragPlaceholder.SetAsLastSibling();
        }

        void BeginListTransfer(ReorderableListItem item)
        {
            dragSourceList = this;
            dropTarget = this;
            RectTransform itemRect = item.CachedRectTransform;
            dragAnchorMin = itemRect.anchorMin;
            dragAnchorMax = itemRect.anchorMax;
            dragSizeDelta = itemRect.sizeDelta;
            dragItemSize = itemRect.rect.size;
            dragOriginalScale = itemRect.localScale;
            dragOriginalRotation = itemRect.localRotation;
            dragOriginalAlpha = item.canvasGroup.alpha;
            dragBlocksRaycasts = item.canvasGroup.blocksRaycasts;

            // A real layout element lets Unity allocate preferred and flexible sizes normally
            dragPlaceholder = new GameObject("List Drag Placeholder", typeof(RectTransform), typeof(ReorderableListPlaceholder)).GetComponent<RectTransform>();
            dragPlaceholder.SetParent(listContainer, false);
            dragPlaceholder.pivot = itemRect.pivot;
            dragPlaceholder.sizeDelta = dragItemSize;
            dragPlaceholder.localScale = dragOriginalScale;
            dragPlaceholder.GetComponent<ReorderableListPlaceholder>().item = itemRect;
            MoveDragPlaceholder();

            dropBoundsCache.Clear();

            for (int i = 0; i < activeLists.Count; i++)
            {
                ReorderableList list = activeLists[i];
                if (list.CanReceiveItem(this))
                    dropBoundsCache[list] = list.GetDropBounds();
            }

            // Keep the item visible outside scroll masks without changing list ownership.
            dragLayer = new GameObject("List Drag Layer", typeof(RectTransform), typeof(Canvas), typeof(LayoutElement)).GetComponent<RectTransform>();
            dragLayer.SetParent(canvas.rootCanvas.transform, false);
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.sizeDelta = Vector2.zero;
            dragLayer.GetComponent<LayoutElement>().ignoreLayout = true;
            Canvas dragCanvas = dragLayer.GetComponent<Canvas>();
            dragCanvas.overrideSorting = true;
            dragCanvas.sortingLayerID = canvas.sortingLayerID;
            dragCanvas.sortingOrder = short.MaxValue;

            itemRect.SetParent(dragLayer, true);
            itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dragItemSize.x);
            itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dragItemSize.y);
            item.canvasGroup.blocksRaycasts = false;
            dragPointerData = EventSystem.current != null ? new PointerEventData(EventSystem.current) : null;

            AnimateScale(itemRect, itemRect.localScale * dragScale, 0.1f);
            AnimateFade(item.canvasGroup, dragAlpha, 0.1f);
        }

        bool CanReceiveItem(ReorderableList source)
        {
            return isActiveAndEnabled && allowListTransfer && source.allowListTransfer && transferGroup == source.transferGroup
                && canvas != null && canvas.rootCanvas == source.canvas.rootCanvas && listContainer != null
                && listContainer.gameObject.activeInHierarchy && pendingTransferItem == null
                && !listContainer.IsChildOf(source.draggedItem.transform) && (draggedItem == null || dragSourceList == source);
        }

        Rect GetDropBounds()
        {
            RectTransform area = dropArea != null ? dropArea : listContainer;
            area.GetWorldCorners(dropCornersCache);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, dropCornersCache[0]);
            Vector2 max = min;
            
            for (int i = 1; i < dropCornersCache.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, dropCornersCache[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static bool IsAbove(Transform first, Transform second)
        {
            if (first == second || first.IsChildOf(second))
                return true;
            if (second.IsChildOf(first))
                return false;

            Transform firstParent = first;
            while (firstParent.parent != null && !second.IsChildOf(firstParent.parent))
                firstParent = firstParent.parent;
          
            Transform secondParent = second;
            while (secondParent.parent != null && secondParent.parent != firstParent.parent)
                secondParent = secondParent.parent;
          
            return firstParent.GetSiblingIndex() > secondParent.GetSiblingIndex();
        }

        bool ContainsDropPoint(Vector2 screenPosition, ReorderableList source)
        {
            RectTransform area = dropArea != null ? dropArea : listContainer;
            if (!area.gameObject.activeInHierarchy)
                return false;

            // Removing the dragged item can shrink fitted content underneath a stationary pointer
            // Retain its original area for this drag to avoid enter/leave feedback.
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(area, screenPosition, uiCamera);
            if (!inside && dropArea == null && source.dropBoundsCache.TryGetValue(this, out Rect bounds))
                inside = bounds.Contains(screenPosition);
            if (!inside)
                return false;

            // Respect scroll view clipping and disabled interaction on parent groups.
            for (Transform parent = area; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent<RectMask2D>(out var rectMask) && rectMask.isActiveAndEnabled && !rectMask.IsRaycastLocationValid(screenPosition, uiCamera))
                    return false;
                if (parent.TryGetComponent<Mask>(out var mask) && mask.isActiveAndEnabled && !mask.IsRaycastLocationValid(screenPosition, uiCamera))
                    return false;
                if (parent.TryGetComponent<CanvasGroup>(out var group) && group.enabled && (!group.interactable || !group.blocksRaycasts))
                    return false;
            }

            return true;
        }

        ReorderableList FindDropTarget(Vector2 screenPosition)
        {
            Transform hit = null;
            if (EventSystem.current != null && dragPointerData != null)
            {
                dragPointerData.position = screenPosition;
                raycastResultsCache.Clear();
                EventSystem.current.RaycastAll(dragPointerData, raycastResultsCache);
                if (raycastResultsCache.Count > 0)
                    hit = raycastResultsCache[0].gameObject.transform;
            }

            ReorderableList bestTarget = null;
            int bestDepth = -1;
            for (int i = 0; i < activeLists.Count; i++)
            {
                ReorderableList candidate = activeLists[i];
                if (candidate == null || !candidate.CanReceiveItem(this) || !candidate.ContainsDropPoint(screenPosition, this))
                    continue;

                // A Graphic behind the list must not disqualify gaps between its items
                RectTransform area = candidate.dropArea != null ? candidate.dropArea : candidate.listContainer;
                if (hit != null && !hit.IsChildOf(area) && !area.IsChildOf(hit) && !hit.IsChildOf(candidate.listContainer) && IsAbove(hit, area))
                    continue;

                int depth = 0;
                for (Transform parent = area; parent != null; parent = parent.parent)
                    depth++;

                if (depth > bestDepth || (depth == bestDepth && bestTarget != null && IsAbove(candidate.listContainer, bestTarget.listContainer)))
                {
                    bestTarget = candidate;
                    bestDepth = depth;
                }
            }

            return bestTarget;
        }

        void UpdateDropTarget(Vector2 screenPosition)
        {
            ReorderableList nextTarget = FindDropTarget(screenPosition);
            if (nextTarget != dropTarget)
            {
                ReorderableList previousTarget = dropTarget;
                dropTarget = nextTarget;
                if (previousTarget != null && previousTarget != this)
                {
                    previousTarget.dragSourceList = null;
                    previousTarget.ResetState();
                    previousTarget.RefreshLayout();
                }

                previewInsertIndex = nextTarget == this ? draggedFromIndex : -1;
                if (nextTarget != null && nextTarget != this)
                {
                    nextTarget.RefreshItemsList();
                    nextTarget.dragSourceList = this;
                    nextTarget.draggedItem = draggedItem;
                    nextTarget.draggedFromIndex = -1;
                    nextTarget.previewInsertIndex = 0;
                    MoveDragPlaceholder();
                    nextTarget.RefreshLayout();
                }

                MoveDragPlaceholder();
                RefreshLayout();
            }

            if (dropTarget == null)
                return;

            int newInsertIndex = dropTarget.GetInsertionIndex();
            if (newInsertIndex != dropTarget.previewInsertIndex)
            {
                dropTarget.previewInsertIndex = newInsertIndex;
                MoveDragPlaceholder();
                dropTarget.RefreshLayout();
            }
        }

        void FinishListTransfer(bool commit, bool animate = true)
        {
            if (dragPlaceholder != null)
            {
                dragPlaceholder.gameObject.SetActive(false);
                Destroy(dragPlaceholder.gameObject);
                dragPlaceholder = null;
            }

            dropBoundsCache.Clear();

            ReorderableListItem item = draggedItem;
            ReorderableList previousTarget = dropTarget;
            ReorderableList destination = commit && dropTarget != null && dropTarget.CanReceiveItem(this) ? dropTarget : this;
            int originalIndex = draggedFromIndex;
            int finalIndex = commit && destination == dropTarget ? destination.previewInsertIndex : originalIndex;

            dragScaleAnimation.Kill();
            dragFadeAnimation.Kill();
            dragScaleAnimation = default;
            dragFadeAnimation = default;

            // Clear both previews before refreshing, including cancellation during OnDisable
            dragSourceList = null;
            dropTarget = null;
            ResetState();
            if (previousTarget != null && previousTarget != this)
            {
                previousTarget.dragSourceList = null;
                previousTarget.ResetState();
            }

            if (item != null && (!destination.gameObject.activeInHierarchy || !item.gameObject.activeInHierarchy))
            {
                // Unity rejects reparenting during hierarchy deactivation. Keep the item hidden
                // until the next frame, or until the source list is enabled again.
                pendingTransferItem = item;
                pendingTransferIndex = Mathf.Max(0, finalIndex);
                pendingTransferActive = item.gameObject.activeSelf;
                item.ResetDragState();
                item.canvasGroup.blocksRaycasts = dragBlocksRaycasts;
                item.canvasGroup.alpha = dragOriginalAlpha;
                item.gameObject.SetActive(false);
                dragLayer.gameObject.AddComponent<ReorderableListDragLayer>().Restore(this);
            }
            else if (item != null) { RestoreTransferItem(item, destination, finalIndex, animate); }

            if (dragLayer != null && pendingTransferItem == null)
            {
                Destroy(dragLayer.gameObject);
                dragLayer = null;
            }

            dragPointerData = null;

            RefreshItemsList();
            RefreshLayout();
          
            if (previousTarget != null && previousTarget != this)
            {
                previousTarget.RefreshItemsList();
                previousTarget.RefreshLayout();
            }

            if (item == null || !commit)
                return;

            finalIndex = destination.items.IndexOf(item);

            if (destination != this)
            {
                onListChanged?.Invoke(item, this, destination);
                destination.onListChanged?.Invoke(item, this, destination);
            }
            else if (originalIndex != finalIndex) { onOrderChanged?.Invoke(item, originalIndex, finalIndex); }
        }

        internal void RestorePendingTransfer()
        {
            if (pendingTransferItem == null)
            {
                if (dragLayer != null)
                    Destroy(dragLayer.gameObject); dragLayer = null;

                return;
            }

            ReorderableListItem item = pendingTransferItem;
            pendingTransferItem = null;
            RestoreTransferItem(item, this, pendingTransferIndex, false);
            item.gameObject.SetActive(pendingTransferActive);
            SetActiveSiblingIndex(item, pendingTransferIndex);
          
            if (dragLayer != null)
                Destroy(dragLayer.gameObject); dragLayer = null;

            RefreshItemsList();
            RefreshLayout();
        }

        void RestoreTransferItem(ReorderableListItem item, ReorderableList destination, int finalIndex, bool animate)
        {
            RectTransform itemRect = item.CachedRectTransform;
            Vector3 worldPosition = itemRect.position;
            itemRect.SetParent(destination.listContainer, true);
            Vector3 currentScale = itemRect.localScale;
            itemRect.anchorMin = dragAnchorMin;
            itemRect.anchorMax = dragAnchorMax;
            itemRect.sizeDelta = dragSizeDelta;
            itemRect.localRotation = dragOriginalRotation;
            itemRect.localScale = dragOriginalScale;
            itemRect.position = worldPosition;
            item.canvasGroup.blocksRaycasts = dragBlocksRaycasts;
            item.Initialize(destination);
            item.ResetDragState();
            destination.SetActiveSiblingIndex(item, Mathf.Max(0, finalIndex));

            if (!destination.gameObject.activeInHierarchy)
            {
                destination.pendingResortItem = item;
                destination.pendingResortIndex = Mathf.Max(0, finalIndex);
            }

            if (animate && !destination.instantSnap && destination.isActiveAndEnabled && item.gameObject.activeInHierarchy)
            {
                itemRect.localScale = currentScale;
                destination.AnimateScale(itemRect, dragOriginalScale, 0.2f);
                destination.AnimateFade(item.canvasGroup, dragOriginalAlpha, 0.2f);
            }
            else { item.canvasGroup.alpha = dragOriginalAlpha; }
        }
        #endregion

        #region Animation
        TweenBuilder<T> CreateAnimation<T>(T from, T to, float duration) where T : struct
        {
            bool hasCustomCurve = animationCurve != null && animationCurve.length > 0;
            return EvoTween.Create(from, to, duration)
                .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? animationCurve : null)
                .WithTimeMode(TimeMode.Unscaled);
        }

        void AnimateScale(Transform target, Vector3 targetScale, float duration)
        {
            if (scaleTarget != target)
                dragScaleAnimation.Complete();

            dragScaleAnimation.Kill();
            dragScaleAnimation = default;
            scaleTarget = target;

            if (target == null)
                return;

            if (instantSnap || !isActiveAndEnabled || duration <= 0f || target.localScale == targetScale)
                target.localScale = targetScale;
            else
                dragScaleAnimation = CreateAnimation(target.localScale, targetScale, duration).BindToScale(target);
        }

        void AnimateFade(CanvasGroup target, float targetAlpha, float duration)
        {
            if (fadeTarget != target) { dragFadeAnimation.Complete(); }
            dragFadeAnimation.Kill();
            dragFadeAnimation = default;
            fadeTarget = target;

            if (target == null)
                return;

            if (instantSnap || !isActiveAndEnabled || duration <= 0f || Mathf.Approximately(target.alpha, targetAlpha))
                target.alpha = targetAlpha;
            else
                dragFadeAnimation = CreateAnimation(target.alpha, targetAlpha, duration).BindToAlpha(target);
        }

        void QueueLayoutRefresh()
        {
            if (isActiveAndEnabled && layoutRefreshCoroutine == null)
                layoutRefreshCoroutine = StartCoroutine(DelayedLayoutRefresh());
        }

        IEnumerator DelayedLayoutRefresh()
        {
            yield return cachedEndOfFrame;
            layoutRefreshCoroutine = null;
            RefreshLayout();
        }

        void SetLayoutEnabled(bool value)
        {
            if (layoutGroup != null)
                layoutGroup.enabled = value;
            if (contentSizeFitter != null)
                contentSizeFitter.enabled = value;
        }

        void StartLayoutAnimation()
        {
            layoutAnimation.Kill();
            layoutAnimation = default;
            layoutProgress = 0f;
            IsAnimating = true;
            float duration = animationDuration * (IsDraggingActive ? 0.5f : 1f);
            int count = nativeLayoutActive ? nativeLayoutCache.Count : previewItemsCache.Count;

            if (instantSnap || !Application.isPlaying || duration <= 0f || count == 0)
            {
                CompleteLayoutAnimation();
                return;
            }

            layoutAnimation = CreateAnimation(0f, 1f, duration).OnComplete(layoutCompleted).Bind(layoutProgressSetter);
        }

        void SetLayoutProgress(float value)
        {
            layoutProgress = value;

            if (!nativeLayoutActive)
                ApplyManualLayout(value);
        }

        void CompleteLayoutAnimation()
        {
            layoutProgress = 1f;

            // Native positions are applied after Unity resolves layout for this canvas.
            if (nativeLayoutActive)
                return;

            ApplyManualLayout(1f);

            if (!IsDraggingActive)
                SetLayoutEnabled(true);

            IsAnimating = false;
        }

        void ApplyManualLayout(float progress)
        {
            float currentPos = layoutType == LayoutType.Grid ? 0f : CalculateStartPosition(previewItemsCache);
            for (int i = 0; i < previewItemsCache.Count; i++)
            {
                ReorderableListItem item = previewItemsCache[i];
                if (item == null)
                    continue;

                RectTransform itemRect = item.CachedRectTransform;
                Vector3 targetPosition;
                if (layoutType == LayoutType.Grid)
                {
                    targetPosition = GetGridPosition(i, previewItemsCache.Count)
                        + Vector3.Scale(itemRect.rect.size, itemRect.pivot - Vector2.one * 0.5f);
                }
                else
                {
                    targetPosition = GetLinearPosition(currentPos, itemRect);
                    currentPos += (GetItemSize(itemRect) + itemSpacing) * (layoutType == LayoutType.Horizontal ? 1f : -1f);
                }

                if (item != draggedItem && itemRect.parent == listContainer)
                    itemRect.localPosition = Vector3.Lerp(startPositionsCache[i], targetPosition, progress);
            }
        }
        #endregion

        #region Drag Handlers
        public void OnItemBeginDrag(ReorderableListItem item)
        {
            if (!isActiveAndEnabled || IsDraggingActive || pendingTransferItem != null || item == null || !items.Contains(item))
                return;

            dragScaleAnimation.Complete();
            dragFadeAnimation.Complete();
            draggedItem = item;
            dragOriginalScale = item.transform.localScale;
            dragOriginalAlpha = item.canvasGroup.alpha;
            draggedFromIndex = items.IndexOf(item);
            previewInsertIndex = draggedFromIndex;

            Vector3 currentPosition = item.transform.localPosition;

            // Immediately disable layout group to prepare for manual drag preview animations
            if (layoutGroup != null)
                layoutGroup.enabled = false;

            if (contentSizeFitter != null)
                contentSizeFitter.enabled = false;

            if (allowListTransfer && canvas != null)
            {
                BeginListTransfer(item);
                RefreshLayout();
                return;
            }

            item.transform.SetAsLastSibling(); // Bring dragged item to front
            item.transform.localPosition = currentPosition; // Immediately restore position to prevent flicker

            AnimateScale(item.transform, dragOriginalScale * dragScale, 0.1f);
            AnimateFade(item.canvasGroup, dragAlpha, 0.1f);
            RefreshLayout();
        }

        public void OnItemDrag(ReorderableListItem item, Vector2 screenPosition, Vector2 offset)
        {
            if (item != draggedItem)
                return;

            if (dragSourceList == this)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(listContainer, screenPosition, uiCamera, out Vector2 sourcePoint))
                    item.transform.position = listContainer.TransformPoint(sourcePoint + offset);

                UpdateDropTarget(screenPosition);
                return;
            }

            // Convert screen position to local position
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(listContainer, screenPosition, uiCamera, out Vector2 localPoint))
            {
                localPoint += offset;
                RectTransform itemRect = item.CachedRectTransform;

                if (layoutType == LayoutType.Grid)
                {
                    float minX = ItemXFromLeft(itemRect, ContainerLeft() + layoutPadding.left);
                    float maxX = ItemXFromRight(itemRect, ContainerRight() - layoutPadding.right);
                    float minY = ItemYFromBottom(itemRect, ContainerBottom() + layoutPadding.bottom);
                    float maxY = ItemYFromTop(itemRect, ContainerTop() - layoutPadding.top);
                    float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
                    float clampedY = Mathf.Clamp(localPoint.y, minY, maxY);

                    item.transform.localPosition = new Vector3(clampedX, clampedY, 0);
                }
                else if (layoutType == LayoutType.Horizontal)
                {
                    float yPos = GetAlignedPosition(itemRect, 1);
                    float minPos = ItemXFromLeft(itemRect, ContainerLeft() + layoutPadding.left);
                    float maxPos = ItemXFromRight(itemRect, ContainerRight() - layoutPadding.right);
                    float clampedX = Mathf.Clamp(localPoint.x, minPos, maxPos);

                    item.transform.localPosition = new Vector3(clampedX, yPos, 0);
                }
                else // Vertical
                {
                    float xPos = GetAlignedPosition(itemRect, 0);
                    float maxPos = ItemYFromTop(itemRect, ContainerTop() - layoutPadding.top);
                    float minPos = ItemYFromBottom(itemRect, ContainerBottom() + layoutPadding.bottom);
                    float clampedY = Mathf.Clamp(localPoint.y, minPos, maxPos);
                    item.transform.localPosition = new Vector3(xPos, clampedY, 0);
                }
            }

            // Check for insertion point
            int newInsertIndex = GetInsertionIndex();
            if (newInsertIndex != previewInsertIndex)
            {
                previewInsertIndex = newInsertIndex;
                RefreshLayout();
            }
        }

        public void OnItemEndDrag(ReorderableListItem item)
        {
            if (item != draggedItem)
                return;

            if (dragSourceList == this)
            {
                FinishListTransfer(dropTarget != null);
                return;
            }

            int originalIndex = draggedFromIndex;
            int finalInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);
            items.Remove(item);

            // Insert at final position
            if (finalInsertIndex >= 0 && finalInsertIndex <= items.Count)
            {
                items.Insert(finalInsertIndex, item);
                SetActiveSiblingIndex(item, finalInsertIndex);
            }

            AnimateScale(item.transform, dragOriginalScale, 0.2f);
            AnimateFade(item.canvasGroup, dragOriginalAlpha, 0.2f);
            item.ResetDragState();
            ResetState();
            RefreshItemsList();
            RefreshLayout();

            // Listeners may remove items or begin another drag
            if (originalIndex != finalInsertIndex)
                onOrderChanged?.Invoke(item, originalIndex, finalInsertIndex);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Adds an existing RectTransform items to the list.
        /// </summary>
        public void AddExistingItems(List<RectTransform> itemsList)
        {
            if (itemsList == null)
                return;

            foreach (var item in itemsList)
            {
                if (item == null)
                    continue;

                item.SetParent(listContainer, false);
                item.gameObject.SetActive(true);
            }

            // Refresh internal list
            RefreshItemsList();

        }

        /// <summary>
        /// Adds an existing RectTransform to the list.
        /// </summary>
        public void AddExistingItem(RectTransform item)
        {
            if (item == null)
                return;

            item.SetParent(listContainer, false);
            item.gameObject.SetActive(true);

            // Refresh internal list
            RefreshItemsList();

        }

        /// <summary>
        /// Adds an existing object to the list.
        /// </summary>
        public void AddExistingItem(GameObject item) => AddExistingItem(item.GetComponent<RectTransform>());

        /// <summary>
        /// Removes an item from the list and destroys its GameObject.
        /// </summary>
        public void RemoveItem(ReorderableListItem item)
        {
            if (item == null || !items.Contains(item))
                return;

            if (IsDragging(item))
                CancelDrag();

            // Unparent immediately so layout and DockMagnifier stop tracking this item
            item.transform.SetParent(null);
            Destroy(item.gameObject);
            RefreshItemsList();
            QueueDockRefresh();
        }

        /// <summary>
        /// Removes a specific GameObject from the list and destroys it.
        /// </summary>
        public void RemoveItem(GameObject itemGo)
        {
            if (itemGo != null && itemGo.TryGetComponent<ReorderableListItem>(out var item))
                RemoveItem(item);
        }

        /// <summary>
        /// Detaches an item from the list without destroying it.
        /// The item is unparented and the list re-flows smoothly.
        /// </summary>
        public void DetachItem(RectTransform itemRect)
        {
            if (itemRect == null)
                return;

            DetachItemInternal(itemRect);
            RefreshItemsList();
            QueueDockRefresh();
        }

        /// <summary>
        /// Detaches multiple items from the list without destroying them.
        /// </summary>
        public void DetachItems(List<RectTransform> itemRects)
        {
            if (itemRects == null || itemRects.Count == 0)
                return;

            for (int i = 0; i < itemRects.Count; i++)
            {
                if (itemRects[i] != null)
                    DetachItemInternal(itemRects[i]);
            }

            RefreshItemsList();
            QueueDockRefresh();
        }

        /// <summary>
        /// Clears all items from the list and destroys their GameObjects.
        /// </summary>
        public void ClearItems()
        {
            if (IsDraggingActive)
                CancelDrag();

            for (int i = items.Count - 1; i >= 0; i--)
            {
                ReorderableListItem item = items[i];
                if (item == null)
                    continue;

                item.transform.SetParent(null);
                Destroy(item.gameObject);
            }

            items.Clear();
            RefreshLayout();
            QueueDockRefresh();
        }

        public void ResetState()
        {
            if (dragSourceList != null)
            {
                dragSourceList.FinishListTransfer(false, false);
                return;
            }

            draggedItem = null;
            draggedFromIndex = -1;
            previewInsertIndex = -1;
        }

        public void RefreshItemsList()
        {
            if (listContainer == null)
                return;

            previousItemsCache.Clear();
            previousItemsCache.AddRange(items);
            itemSet.Clear();

            for (int i = 0; i < items.Count; i++)
                itemSet.Add(items[i]);

            items.Clear();
            newItemsCache.Clear();

            // Fetch childs
            for (int i = 0; i < listContainer.childCount; i++)
            {
                Transform child = listContainer.GetChild(i);
                if (!child.gameObject.activeInHierarchy || child.TryGetComponent<ReorderableListPlaceholder>(out _))
                    continue;
                if (!child.TryGetComponent<ReorderableListItem>(out var item))
                    item = child.gameObject.AddComponent<ReorderableListItem>();

                item.Initialize(this);
                items.Add(item);

                if (!itemSet.Contains(item))
                    newItemsCache.Add(item);
            }

            // The source keeps ownership while the item is displayed above the lists
            if (dragSourceList == this && draggedItem != null && draggedItem.gameObject.activeInHierarchy && !items.Contains(draggedItem))
                items.Insert(Mathf.Clamp(draggedFromIndex, 0, items.Count), draggedItem);

            // Keep the logical drag order when the dragged graphic is the last sibling
            if (draggedItem != null && draggedFromIndex >= 0 && items.Remove(draggedItem))
            {
                itemSet.Clear();
                for (int i = 0; i < items.Count; i++)
                    itemSet.Add(items[i]);

                int index = 0;
                for (int i = 0; i < previousItemsCache.Count && previousItemsCache[i] != draggedItem; i++)
                {
                    if (itemSet.Contains(previousItemsCache[i]))
                        index++;
                }

                items.Insert(Mathf.Clamp(index, 0, items.Count), draggedItem);
            }

            bool listChanged = items.Count != previousItemsCache.Count;
            for (int i = 0; !listChanged && i < items.Count; i++)
                listChanged = items[i] != previousItemsCache[i];

            itemSet.Clear();
            for (int i = 0; i < items.Count; i++)
                itemSet.Add(items[i]);

            if (gameObject.activeInHierarchy)
            {
                // Unconditionally refresh targets if composition changed to prevent lingering caches
                if (listChanged && DockMagnifier != null)
                    DockMagnifier.RefreshTargets();

                // Only strictly enforce positions if we are actively rearranging items to maintain drag preview layout
                // (Instantly snap newly created items to their correct target position preventing them from visually sliding from 0,0)
                if (IsDraggingActive && layoutGroup == null && newItemsCache.Count > 0)
                    SnapItemsToTarget(newItemsCache);
            }

            // Update drag indices if we're currently dragging
            if (draggedItem != null && itemSet.Contains(draggedItem))
            {
                draggedFromIndex = items.IndexOf(draggedItem);
                if (previewInsertIndex >= 0)
                    previewInsertIndex = Mathf.Clamp(previewInsertIndex, 0, items.Count - 1);
            }

            // Only hijack layout updates if we are actively rearranging, let native UI handle simple addition/removal
            if (isActiveAndEnabled && IsDraggingActive)
                QueueLayoutRefresh();
            else if (isActiveAndEnabled && listChanged && IsAnimating)
                RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (layoutRefreshCoroutine != null)
            {
                StopCoroutine(layoutRefreshCoroutine);
                layoutRefreshCoroutine = null;
            }

            layoutAnimation.Kill();
            layoutAnimation = default;
            IsAnimating = false;

            if (!isActiveAndEnabled)
                return;

            if (layoutGroup != null && (allowListTransfer || dragSourceList != null || nativeLayoutActive))
            {
                RefreshNativeLayout();
                return;
            }

            SetLayoutEnabled(false);
            previewItemsCache.Clear();
            previewItemsCache.AddRange(items);

            if (draggedItem != null)
            {
                previewItemsCache.Remove(draggedItem);
                if (previewInsertIndex >= 0)
                    previewItemsCache.Insert(Mathf.Min(previewInsertIndex, previewItemsCache.Count), draggedItem);
            }

            startPositionsCache.Clear();

            for (int i = 0; i < previewItemsCache.Count; i++)
                startPositionsCache.Add(previewItemsCache[i] != null ? previewItemsCache[i].transform.localPosition : Vector3.zero);

            StartLayoutAnimation();
        }

        public void CancelDrag()
        {
            if (dragSourceList != null)
            {
                dragSourceList.FinishListTransfer(false);
                return;
            }

            if (draggedItem == null)
                return;

            if (draggedFromIndex >= 0)
                SetActiveSiblingIndex(draggedItem, draggedFromIndex);

            draggedItem.ResetDragState();
            AnimateScale(draggedItem.transform, dragOriginalScale, 0.2f);
            AnimateFade(draggedItem.canvasGroup, dragOriginalAlpha, 0.2f);
            ResetState();
            RefreshItemsList();
            RefreshLayout();
        }
        #endregion
    }

    /// <summary>
    /// Reserves space for a dragged item and exposes its sizing requirements to Unity's layout system.
    /// </summary>
    class ReorderableListPlaceholder : MonoBehaviour, ILayoutElement
    {
        public RectTransform item;
        public int layoutPriority => 1;
        public float minWidth => LayoutUtility.GetMinWidth(item);
        public float preferredWidth => LayoutUtility.GetPreferredWidth(item);
        public float flexibleWidth => LayoutUtility.GetFlexibleWidth(item);
        public float minHeight => LayoutUtility.GetMinHeight(item);
        public float preferredHeight => LayoutUtility.GetPreferredHeight(item);
        public float flexibleHeight => LayoutUtility.GetFlexibleHeight(item);
#if UNITY_6000_6_OR_NEWER
        public float maxWidth => LayoutUtility.GetMaxWidth(item);
        public float maxHeight => LayoutUtility.GetMaxHeight(item);
#endif

        public void CalculateLayoutInputHorizontal()
        {
            if (item != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(item);
        }

        public void CalculateLayoutInputVertical()
        {
            if (item == null)
                return;

            // Text and nested layout groups can report a different preferred height at
            // the receiving list's width. Measure them after its horizontal layout pass.
            if (transform.parent.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out var group) && group.childControlWidth)
                item.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ((RectTransform)transform).rect.width);
            else if (transform.parent.TryGetComponent<GridLayoutGroup>(out _))
                item.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ((RectTransform)transform).rect.width);

            LayoutRebuilder.ForceRebuildLayoutImmediate(item);
        }
    }

    /// <summary>
    /// Restores a pending dragged item when it can be safely reparented after hierarchy deactivation.
    /// </summary>
    class ReorderableListDragLayer : MonoBehaviour
    {
        ReorderableList sourceList;

        public void Restore(ReorderableList source)
        {
            sourceList = source;

            if (isActiveAndEnabled)
                StartCoroutine(RestoreItem());
        }

        void OnEnable()
        {
            if (sourceList != null)
                StartCoroutine(RestoreItem());
        }

        IEnumerator RestoreItem()
        {
            yield return null;

            if (sourceList != null)
                sourceList.RestorePendingTransfer();
            else
                Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handles drag input and forwards it to the owning reorderable list.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class ReorderableListItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public CanvasGroup canvasGroup;
        public RectTransform CachedRectTransform { get; private set; }

        // Helpers
        bool isDragging;
        ReorderableList targetList;
        Vector2 dragOffset;

        void OnEnable()
        {
            if (targetList != null && !targetList.Items().Contains(this))
                targetList.RefreshItemsList();
        }

        void OnDisable()
        {
            isDragging = false;

            if (targetList != null && targetList.gameObject.activeInHierarchy)
            {
                if (targetList.IsDragging(this)) { targetList.CancelDrag(); }
                else if (transform.parent != null && transform.parent.gameObject.activeInHierarchy)
                {
                    transform.SetAsLastSibling();
                    targetList.RefreshItemsList();
                }
            }
        }

        public void Initialize(ReorderableList parentList)
        {
            targetList = parentList;

            if (CachedRectTransform == null)
                CachedRectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void ResetDragState() => isDragging = false;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (targetList == null || !targetList.isActiveAndEnabled || targetList.IsDraggingActive)
                return;

            // Calculate offset between pointer and item position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.parent as RectTransform,
                eventData.position, eventData.pressEventCamera, out Vector2 localPointerPos);

            dragOffset = (Vector2)transform.localPosition - localPointerPos;
            targetList.OnItemBeginDrag(this);
            isDragging = targetList.IsDragging(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            targetList.OnItemDrag(this, eventData.position, dragOffset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || targetList == null)
                return;

            // Resolve the final pointer position even if no OnDrag arrived on the release frame
            targetList.OnItemDrag(this, eventData.position, dragOffset);
            isDragging = false;
            targetList.OnItemEndDrag(this);
        }
    }
}