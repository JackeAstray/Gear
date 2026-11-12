using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Layout for a single column or row. Sizes are measured along the scroll axis in canvas units.
    /// </summary>
    [Serializable]
    public class VirtualizedListSettings
    {
        [Min(1f)] public float itemSize = 40f;
        [Min(0f)] public float spacing = 0f;
        public RectOffset padding = new();
        public bool enableNavigation = true;
        public bool loopNavigation = false;

        // Optional data-based size lookup.
        // Re-evaluated by SetItemCount or SetLayout, never while scrolling.
        [NonSerialized] public Func<int, float> getItemSize;
    }

    /// <summary>
    /// Owns the content layout and a pool of views for an Evo ScrollRect.
    /// Create through ScrollRect.CreateVirtualizedList. Dispose to restore the original content layout.
    /// </summary>
    public abstract class VirtualizedList : IDisposable
    {
        // References
        readonly ScrollRect scrollRect;
        readonly RectTransform content;
        readonly RectTransform viewport;
        readonly Component prefab;
        readonly bool vertical;
        readonly LayoutGroup layoutGroup;
        readonly ContentSizeFitter contentFitter;
        readonly bool layoutWasEnabled;
        readonly bool fitterWasEnabled;
        readonly RectTransform poolRoot;

        // Original layout
        readonly Vector2 originalAnchorMin;
        readonly Vector2 originalAnchorMax;
        readonly Vector2 originalPivot;
        readonly Vector2 originalSize;
        readonly Vector2 originalPosition;

        // Cache
        readonly List<View> activeViews = new();
        readonly Stack<View> pooledViews = new();
        readonly Dictionary<long, View> viewsByIndex = new();
        Vector2 lastViewportSize;
        Vector2 lastPosition;
        int lastOverscan = -1;
        long lastFirst = -1;
        long lastLast = -1;
        bool lastSnapping;
        bool visibleDirty = true;
        GameObject lastSelection;
        VirtualizedListSettings layout;
        float[] itemStarts;
        float[] itemSizes;
        float contentSize;
        bool refreshing;
        bool layoutDirty = true;
        bool wasVirtualized;
        bool wasInfinite;
        long focusedSlot;

        // Properties
        public int ItemCount { get; private set; }
        public int ActiveCount => activeViews.Count;
        public int PooledCount => pooledViews.Count;
        public bool IsDisposed { get; private set; }
        public float ContentSize => contentSize + LeadingPadding + TrailingPadding;
        internal bool IsInfinite => scrollRect.EnableInfiniteScroll && ItemCount > 0
            && scrollRect.vertical == vertical && scrollRect.horizontal != vertical;
        internal float CycleSize => contentSize + layout.spacing + (vertical ? layout.padding.vertical : layout.padding.horizontal);
        internal bool IsVertical => vertical;
        internal bool LoopNavigation => IsInfinite || layout.loopNavigation;
        internal ScrollRect ScrollRect => scrollRect;
        float ViewportSize => vertical ? viewport.rect.height : viewport.rect.width;
        float LeadingPadding => Mathf.Max(vertical ? layout.padding.top : layout.padding.left,
            !IsInfinite && scrollRect.enableSnapping && ItemCount > 0 ? (ViewportSize - GetItemSize(0)) * 0.5f : 0f);
        float TrailingPadding => Mathf.Max(vertical ? layout.padding.bottom : layout.padding.right,
            !IsInfinite && scrollRect.enableSnapping && ItemCount > 0 ? (ViewportSize - GetItemSize(ItemCount - 1)) * 0.5f : 0f);

        class View
        {
            public Component component;
            public RectTransform rectTransform;
            public VirtualizedListItem interaction;
            public ScrollRect.ItemData itemState;
            public int index = -1;
            public long slot;
            public bool bound;
        }

        internal VirtualizedList(ScrollRect scrollRect, Component prefab, VirtualizedListSettings settings)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Virtualized lists can only be created in play mode.");
            if (prefab == null || prefab.transform is not RectTransform)
                throw new ArgumentException("A UI prefab with a RectTransform is required.", nameof(prefab));
            if (scrollRect.content == null || scrollRect.horizontal == scrollRect.vertical)
                throw new InvalidOperationException("Assign content and enable exactly one ScrollRect axis.");

            this.scrollRect = scrollRect;
            this.prefab = prefab;
            content = scrollRect.content;
            viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            vertical = scrollRect.vertical;
            wasVirtualized = scrollRect.EnableVirtualization;

            if (content.parent != viewport || content.localScale != Vector3.one || content.localRotation != Quaternion.identity)
                throw new InvalidOperationException("Virtualized content must be an unscaled, unrotated direct child of the viewport.");

            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child != prefab.transform || child.gameObject.activeSelf)
                    throw new InvalidOperationException("Use dedicated empty content, optionally containing an inactive item template.");
            }

            layout = CopySettings(settings);
            originalAnchorMin = content.anchorMin;
            originalAnchorMax = content.anchorMax;
            originalPivot = content.pivot;
            originalSize = content.sizeDelta;
            originalPosition = content.anchoredPosition;
            layoutGroup = content.GetComponent<LayoutGroup>();
            contentFitter = content.GetComponent<ContentSizeFitter>();
            layoutWasEnabled = layoutGroup != null && layoutGroup.enabled;
            fitterWasEnabled = contentFitter != null && contentFitter.enabled;

            if (layoutGroup != null)
                layoutGroup.enabled = false;
            if (contentFitter != null)
                contentFitter.enabled = false;

            content.anchorMin = new Vector2(0f, vertical ? 1f : 0f);
            content.anchorMax = new Vector2(vertical ? 1f : 0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            // Instantiate under an inactive parent so OnEnable runs only after the view is bound
            poolRoot = new GameObject("Virtualized List Pool", typeof(RectTransform)).GetComponent<RectTransform>();
            poolRoot.SetParent(content, false);
            poolRoot.gameObject.SetActive(false);
        }

        static VirtualizedListSettings CopySettings(VirtualizedListSettings settings)
        {
            settings ??= new VirtualizedListSettings();
            RectOffset padding = settings.padding ?? new RectOffset();
            return new VirtualizedListSettings
            {
                itemSize = ValidateSize(settings.itemSize),
                spacing = float.IsNaN(settings.spacing) || float.IsInfinity(settings.spacing) ? 0f : Mathf.Max(0f, settings.spacing),
                padding = new RectOffset(Mathf.Max(0, padding.left), Mathf.Max(0, padding.right), Mathf.Max(0, padding.top), Mathf.Max(0, padding.bottom)),
                enableNavigation = settings.enableNavigation,
                loopNavigation = settings.loopNavigation,
                getItemSize = settings.getItemSize
            };
        }

        static float ValidateSize(float size)
        {
            if (float.IsNaN(size) || float.IsInfinity(size) || size < 1f)
                throw new ArgumentOutOfRangeException(nameof(size), "Item sizes must be finite and at least one canvas unit.");

            return size;
        }

        void CheckMutation()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(VirtualizedList));    
            if (refreshing)
                throw new InvalidOperationException("Defer list changes until after create, bind, or unbind callbacks return.");
        }

        internal void InvalidateVisible() => visibleDirty = true;

        /// <summary>
        /// Refreshes the data count, size cache, and visible bindings. Existing views are reused.
        /// Call once after a batch of data changes. Unbind receives the previous data index.
        /// Selection is retained by index. When reordering data, remap selection in the caller.
        /// </summary>
        public void SetItemCount(int count, bool resetPosition = false)
        {
            CheckMutation();

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            // Validate all supplied sizes before changing existing bindings or geometry
            BuildValidatedSizeCache(count, layout, out float[] starts, out float[] sizes, out float total);
            ItemCount = count;
            itemStarts = starts;
            itemSizes = sizes;
            contentSize = total;
            layoutDirty = true;
            scrollRect.StopVirtualizedMovement();
            ApplyContentSize();
            SetScrollOffset(resetPosition ? 0f : GetScrollOffset());
            RebindAll();
            UpdateVisible();
        }

        /// <summary>
        /// Rebuilds geometry after item sizes, spacing, or padding change. Settings are copied.
        /// </summary>
        public void SetLayout(VirtualizedListSettings settings)
        {
            CheckMutation();
            VirtualizedListSettings next = CopySettings(settings);
            BuildValidatedSizeCache(ItemCount, next, out float[] starts, out float[] sizes, out float total);
            layout = next;
            itemStarts = starts;
            itemSizes = sizes;
            contentSize = total;
            layoutDirty = true;
            scrollRect.StopVirtualizedMovement();
            ApplyContentSize();
            SetScrollOffset(GetScrollOffset());
            UpdateVisible();
        }

        void BuildValidatedSizeCache(int count, VirtualizedListSettings settings, out float[] starts, out float[] sizes, out float total)
        {
            refreshing = true;

            try { BuildSizeCache(count, settings, out starts, out sizes, out total); }
            finally { refreshing = false; }
        }

        static void BuildSizeCache(int count, VirtualizedListSettings settings, out float[] starts, out float[] sizes, out float total)
        {
            starts = null;
            sizes = null;
            total = 0f;

            if (settings.getItemSize == null) { total = count * settings.itemSize + Mathf.Max(0, count - 1) * settings.spacing; }
            else
            {
                starts = new float[count];
                sizes = new float[count];

                for (int i = 0; i < count; i++)
                {
                    starts[i] = total;
                    sizes[i] = ValidateSize(settings.getItemSize(i));
                    total += sizes[i] + settings.spacing;
                }

                if (count > 0)
                    total -= settings.spacing;
            }

            if (float.IsInfinity(total) || float.IsNaN(total))
                throw new ArgumentOutOfRangeException(nameof(count), "The combined item sizes exceed the supported content extent.");
        }

        void ApplyContentSize()
        {
            float extent = ContentSize;
            if ((vertical ? content.rect.height : content.rect.width) != extent)
                content.SetSizeWithCurrentAnchors(vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal, extent);
        }

        float GetItemStart(int index)
        {
            return LeadingPadding + (itemStarts != null ? itemStarts[index] : index * (layout.itemSize + layout.spacing));
        }

        float GetItemSize(int index) => itemSizes != null ? itemSizes[index] : layout.itemSize;

        int GetDataIndex(long slot) => IsInfinite ? (int)((slot % ItemCount + ItemCount) % ItemCount) : (int)slot;

        float GetSlotStart(long slot)
        {
            int index = GetDataIndex(slot);
            return GetItemStart(index) + (IsInfinite ? (slot - index) / ItemCount * CycleSize : 0f);
        }

        long GetNearestSlot(int index, float alignment = 0.5f, float referenceOffset = float.NaN)
        {
            if (!IsInfinite)
                return index;

            float target = GetItemStart(index) - (ViewportSize - GetItemSize(index)) * alignment;
            if (float.IsNaN(referenceOffset))
                referenceOffset = GetScrollOffset();
           
            return index + (long)Math.Round((referenceOffset - target) / CycleSize) * ItemCount;
        }

        internal void ShiftInfiniteSlots(long cycles)
        {
            if (!IsInfinite || cycles == 0)
                return;

            long shift = cycles * ItemCount;
            viewsByIndex.Clear();
          
            foreach (View view in activeViews)
            {
                view.slot -= shift;
                viewsByIndex.Add(view.slot, view);
              
                if (view.index < ItemCount)
                    PositionView(view);
            }

            focusedSlot -= shift;
            visibleDirty = true;
        }

        float GetScrollOffset() => vertical ? content.anchoredPosition.y : -content.anchoredPosition.x;

        void SetScrollOffset(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Scroll offsets must be finite.");
           
            if (!IsInfinite)
                value = Mathf.Clamp(value, 0f, Mathf.Max(0f, ContentSize - ViewportSize));
            
            Vector2 position = vertical ? new Vector2(0f, value) : new Vector2(-value, 0f);
            if (content.anchoredPosition != position)
                content.anchoredPosition = position;
        }

        int FindIndex(float offset)
        {
            offset -= LeadingPadding;

            if (itemStarts == null)
                return Mathf.Clamp(Mathf.FloorToInt(offset / (layout.itemSize + layout.spacing)), 0, ItemCount - 1);

            int low = 0;
            int high = ItemCount - 1;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (itemStarts[middle] <= offset) { low = middle; }
                else { high = middle - 1; }
            }
            return low;
        }

        long FindSlot(float offset)
        {
            if (!IsInfinite)
                return FindIndex(offset);

            long cycle = (long)Math.Floor((offset - LeadingPadding) / CycleSize);
            return cycle * ItemCount + FindIndex(offset - cycle * CycleSize);
        }

        internal void UpdateVisible()
        {
            if (IsDisposed || refreshing)
                return;

            if (content == null || viewport == null)
            {
                Dispose();
                return;
            }

            if (!scrollRect.isActiveAndEnabled)
                return;

            // A list owns one geometry configuration. Release it if the host is repurposed.
            RectTransform currentViewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            if (scrollRect.content != content || currentViewport != viewport || scrollRect.vertical != vertical
                || scrollRect.horizontal == vertical || content.parent != viewport)
            {
                Dispose();
                return;
            }

            bool virtualize = scrollRect.EnableVirtualization;
            bool infinite = IsInfinite;
            GameObject selection = VirtualizedListItem.SelectedObject;
            bool selectionChanged = selection != lastSelection;
            bool trimPool = virtualize && !wasVirtualized;
            Vector2 viewportSize = viewport.rect.size;
            int overscan = scrollRect.VirtualizationOverscan;
            bool resize = viewportSize != lastViewportSize || layoutDirty 
                || lastSnapping != scrollRect.enableSnapping || infinite != wasInfinite;
            
            if (!resize && !visibleDirty && !selectionChanged && virtualize == wasVirtualized
                && overscan == lastOverscan && content.anchoredPosition == lastPosition)
                return;

            refreshing = true;

            try
            {
                lastViewportSize = viewportSize;
                lastSnapping = scrollRect.enableSnapping;
                lastOverscan = overscan;
                lastSelection = selection;

                if (resize)
                {
                    if (infinite != wasInfinite)
                        scrollRect.StopVirtualizedMovement();

                    ApplyContentSize();
                    SetScrollOffset(GetScrollOffset());
                }

                // Normalize before calculating the range, including full-generation lists.
                // Large programmatic jumps must never allocate intervening cycles.
                scrollRect.WrapInfinitePosition();

                long first = 0;
                long last = -1;

                if (ItemCount > 0 && !virtualize && !infinite) { last = ItemCount - 1; }
                else if (ItemCount > 0 && ViewportSize > 0f)
                {
                    // Content is an unscaled, unrotated direct child with a leading-edge pivot
                    float start = GetScrollOffset();
                    float end = start + ViewportSize;
                    first = FindSlot(start) - overscan;
                    last = FindSlot(end) + overscan;
                 
                    if (!infinite)
                    {
                        first = Math.Max(0, first);
                        last = Math.Min(ItemCount - 1, last);
                    }
                    else if (!virtualize)
                    {
                        // Keep one complete cycle, plus any copies needed to fill the viewport
                        last = Math.Max(last, first + ItemCount - 1);
                    }
                }

                lastPosition = content.anchoredPosition;

                if (!resize && !visibleDirty && !selectionChanged && virtualize == wasVirtualized
                    && first == lastFirst && last == lastLast)
                    return;

                visibleDirty = false;

                for (int i = activeViews.Count - 1; i >= 0; i--)
                {
                    View view = activeViews[i];
                    if (view.component == null || view.interaction == null || view.index >= ItemCount
                        || (!infinite && view.slot != view.index))
                        ReleaseAt(i);
                    else
                    {
                        bool outside = view.slot < first || view.slot > last;
                        if (outside && !view.interaction.IsInUse) { ReleaseAt(i); }
                        else if (resize)
                        {
                            PositionView(view);
                            view.interaction.Initialize(this, view.index, layout.enableNavigation, view.itemState);
                        }
                    }
                }

                for (long i = first; i <= last; i++)
                {
                    if (!viewsByIndex.ContainsKey(i))
                        Acquire(i);
                }

                layoutDirty = false;
                wasVirtualized = virtualize;
                wasInfinite = infinite;
                lastFirst = first;
                lastLast = last;
                scrollRect.InvalidateItemStates();
            }
            catch
            {
                visibleDirty = true;
                throw;
            }
            finally { refreshing = false; }

            // Leaving full generation should also release the now-unneeded objects
            if (trimPool)
                TrimPool();
        }

        void Acquire(long slot)
        {
            View view = null;
            while (pooledViews.Count > 0 && view == null)
            {
                View candidate = pooledViews.Pop();
                if (candidate.component != null && candidate.interaction != null)
                    view = candidate;
                else
                    DestroyView(candidate);
            }

            if (view == null)
            {
                Component instance = UnityEngine.Object.Instantiate(prefab, poolRoot);
                instance.gameObject.SetActive(false);
                instance.transform.SetParent(content, false);
                view = new View
                {
                    component = instance,
                    rectTransform = (RectTransform)instance.transform,
                    itemState = new ScrollRect.ItemData { rectTransform = (RectTransform)instance.transform }
                };

                try { CreateItem(instance); }
                catch
                {
                    UnityEngine.Object.Destroy(instance.gameObject);
                    throw;
                }

                if (!instance.TryGetComponent(out view.interaction))
                    view.interaction = instance.gameObject.AddComponent<VirtualizedListItem>();
            }

            int index = GetDataIndex(slot);
            view.index = index;
            view.slot = slot;
            PositionView(view);
            activeViews.Add(view);
            viewsByIndex.Add(slot, view);

            try
            {
                view.bound = true;
                BindItem(view.component, index);
                view.interaction.Initialize(this, view.index, layout.enableNavigation, view.itemState);
                view.component.gameObject.SetActive(true);
            }
            catch
            {
                ReleaseAt(activeViews.Count - 1);
                throw;
            }
        }

        internal ScrollRect.ItemData GetItemState(int activeIndex) => activeViews[activeIndex].itemState;

        View GetView(int index)
        {
            if (IsDisposed || index < 0 || index >= ItemCount)
                return null;
            
            if (viewsByIndex.TryGetValue(GetNearestSlot(index), out View view))
                return view;

            if (!IsInfinite)
                return null;

            // A selected view can be retained outside the current visible cycle
            foreach (View candidate in activeViews)
            {
                if (candidate.index == index)
                    return candidate;
            }
            return null;
        }

        internal VirtualizedListItem GetInteraction(int index) => GetView(index)?.interaction;

        void PositionView(View view)
        {
            RectTransform rt = view.rectTransform;
            rt.anchorMin = new Vector2(0f, vertical ? 1f : 0f);
            rt.anchorMax = new Vector2(vertical ? 1f : 0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = vertical ? new Vector2(-layout.padding.horizontal, GetItemSize(view.index))
                : new Vector2(GetItemSize(view.index), -layout.padding.vertical);
            rt.anchoredPosition = vertical ? new Vector2(layout.padding.left, -GetSlotStart(view.slot))
                : new Vector2(GetSlotStart(view.slot), -layout.padding.top);
        }

        void ReleaseAt(int position)
        {
            View view = activeViews[position];
            viewsByIndex.Remove(view.slot);
            activeViews[position] = activeViews[^1];
            activeViews.RemoveAt(activeViews.Count - 1);
            visibleDirty = true;
            scrollRect.InvalidateItemStates();

            try
            {
                if (view.interaction != null)
                    view.interaction.Clear();
              
                view.itemState.RestoreEffects();
                UnbindView(view);
            }
            finally
            {
                if (view.component != null)
                    view.component.gameObject.SetActive(false);

                view.index = -1;

                if (view.component != null && view.interaction != null)
                    pooledViews.Push(view);
                else
                    DestroyView(view);
            }
        }

        void ReleaseAll()
        {
            refreshing = true;

            try
            {
                Exception failure = null;

                for (int i = activeViews.Count - 1; i >= 0; i--)
                {
                    try { ReleaseAt(i); }
                    catch (Exception exception) { failure ??= exception; }
                }

                if (failure != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            finally { refreshing = false; }
        }

        /// <summary>
        /// Rebinds visible rows after their data changes, without rebuilding the size cache.
        /// </summary>
        public void RefreshItems()
        {
            CheckMutation();
            RebindAll();
            UpdateVisible();
        }

        void RebindAll()
        {
            refreshing = true;
            try
            {
                for (int i = activeViews.Count - 1; i >= 0; i--)
                {
                    View view = activeViews[i];
                    if (view.index >= ItemCount || view.component == null || GetDataIndex(view.slot) != view.index)
                        ReleaseAt(i);
                    else
                        Rebind(view);
                }
            }
            finally
            {
                refreshing = false;
                visibleDirty = true;
                scrollRect.InvalidateItemStates();
            }
        }

        void Rebind(View view)
        {
            try
            {
                // Keep selected descendants, but cancel any click belonging to the previous binding.
                if (view.interaction == null)
                    throw new InvalidOperationException("The list's interaction component was destroyed.");
               
                view.interaction.CancelPress();
                view.itemState.RestoreEffects();
                UnbindView(view);
                view.bound = true;
                BindItem(view.component, view.index);
                view.interaction.Initialize(this, view.index, layout.enableNavigation, view.itemState);
            }
            catch
            {
                ReleaseAt(activeViews.IndexOf(view));
                throw;
            }
        }

        void UnbindView(View view)
        {
            if (!view.bound)
                return;

            view.bound = false;

            if (view.component != null)
                UnbindItem(view.component, view.index);
        }

        /// <summary>
        /// Rebinds one visible index after its data changes. Offscreen indices bind when they become visible.
        /// </summary>
        public void RefreshItem(int index)
        {
            CheckMutation();

            if (index < 0 || index >= ItemCount)
                return;

            refreshing = true;

            try
            {
                // Finite lists still use their constant-time index lookup.
                if (!IsInfinite)
                {
                    if (viewsByIndex.TryGetValue(index, out View view))
                        Rebind(view);

                    return;
                }

                for (int i = activeViews.Count - 1; i >= 0; i--)
                {
                    if (activeViews[i].index == index)
                        Rebind(activeViews[i]);
                }
            }
            finally
            {
                refreshing = false;
                scrollRect.InvalidateItemStates();
            }
        }

        /// <summary>
        /// Releases retained spare views. Normal scrolling keeps the pool at its largest viewport capacity.
        /// </summary>
        public void TrimPool()
        {
            CheckMutation();
            refreshing = true;

            try
            {
                Exception failure = null;

                while (pooledViews.Count > 0)
                {
                    try { DestroyView(pooledViews.Pop()); }
                    catch (Exception exception) { failure ??= exception; }
                }

                if (failure != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            finally { refreshing = false; }
        }

        /// <summary>
        /// Scrolls a data index into view. Alignment is 0 for the leading edge, 0.5 for center, and 1 for trailing.
        /// </summary>
        public void ScrollToIndex(int index, float alignment = 0f)
        {
            CheckMutation();

            if (float.IsNaN(alignment) || float.IsInfinity(alignment))
                throw new ArgumentOutOfRangeException(nameof(alignment));

            if (index < 0 || index >= ItemCount)
                return;

            scrollRect.StopVirtualizedMovement();
            scrollRect.CancelStartingSnap();
            alignment = Mathf.Clamp01(alignment);
            SetScrollOffset(GetSlotStart(GetNearestSlot(index, alignment)) - (ViewportSize - GetItemSize(index)) * alignment);
            UpdateVisible();
        }

        /// <summary>
        /// Scrolls only as far as necessary to reveal a data index.
        /// </summary>
        public void EnsureVisible(int index) => EnsureVisible(index, false);

        internal void EnsureVisible(int index, bool navigation)
        {
            CheckMutation();

            if (index < 0 || index >= ItemCount)
                return;

            if (navigation)
            {
                if (scrollRect.EnableSmoothScroll)
                    UpdateVisible();

                if (IsDisposed || index >= ItemCount)
                    return;

                long slot = GetNearestSlot(index);
                float itemStart = GetSlotStart(slot);
                float itemEnd = itemStart + GetItemSize(index);
                float leading = vertical ? layout.padding.top : layout.padding.left;
                float trailing = vertical ? layout.padding.bottom : layout.padding.right;

                // Keep the layout's edge margins when the item and padding fit in the viewport
                if (itemEnd - itemStart + leading + trailing <= ViewportSize)
                {
                    itemStart -= leading;
                    itemEnd += trailing;
                }

                float target = GetScrollOffset();
                if (itemStart < target)
                    target = itemStart;
                else if (itemEnd > target + ViewportSize)
                    target = itemEnd - ViewportSize;

                if (!IsInfinite)
                    target = Mathf.Clamp(target, 0f, Mathf.Max(0f, ContentSize - ViewportSize));

                if (!scrollRect.EnableSmoothScroll)
                {
                    if (target != GetScrollOffset())
                    {
                        scrollRect.StopVirtualizedMovement();
                        SetScrollOffset(target);
                    }

                    UpdateVisible();
                    return;
                }

                // Bind the destination now so navigation can select it before scrolling reaches it
                if (!viewsByIndex.ContainsKey(slot))
                {
                    refreshing = true;
                    try { Acquire(slot); }
                    finally
                    {
                        refreshing = false;
                        InvalidateVisible();
                        scrollRect.InvalidateItemStates();
                    }
                }

                Vector2 pos = scrollRect.enableSnapping ? GetSnapPosition(index, 0f) : vertical ? new Vector2(0f, target) : new Vector2(-target, 0f);
                scrollRect.SmoothScrollTo(pos);
                return;
            }

            float start = GetSlotStart(GetNearestSlot(index));
            float end = start + GetItemSize(index);
            float offset = GetScrollOffset();

            if (start < offset) { ScrollToIndex(index); }
            else if (end > offset + ViewportSize) { ScrollToIndex(index, 1f); }
            else { UpdateVisible(); }
        }

        void DestroyView(View view)
        {
            Component item = view.component;
            if (item == null)
                return;
         
            try { DestroyItem(item); }
            finally
            {
                if (item != null)
                {
                    item.transform.SetParent(null, false);
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
        }

        internal Vector2 GetSnapPosition(int index, float offset, Vector2? fromPosition = null)
        {
            float reference = fromPosition.HasValue ? (vertical ? fromPosition.Value.y : -fromPosition.Value.x) : GetScrollOffset();
            float target = GetSlotStart(GetNearestSlot(index, 0.5f, reference)) - (ViewportSize - GetItemSize(index)) * 0.5f;
            target += vertical ? offset : -offset;
          
            if (!IsInfinite)
                target = Mathf.Clamp(target, 0f, Mathf.Max(0f, ContentSize - ViewportSize));
          
            return vertical ? new Vector2(0f, target) : new Vector2(-target, 0f);
        }

        internal bool IsActiveFocused(int activeIndex) => activeViews[activeIndex].slot == focusedSlot;

        internal float GetActiveDistanceFromCenter(int activeIndex) => GetDistanceFromCenter(activeViews[activeIndex].slot);

        float GetDistanceFromCenter(long slot)
        {
            return Mathf.Abs(GetSlotStart(slot) + GetItemSize(GetDataIndex(slot)) * 0.5f - GetScrollOffset() - ViewportSize * 0.5f);
        }

        internal int GetFocusedIndex()
        {
            if (ItemCount == 0)
                return -1;

            long candidate = FindSlot(GetScrollOffset() + ViewportSize * 0.5f);
            long focused = candidate;
            float distance = GetDistanceFromCenter(candidate);
            long first = IsInfinite ? candidate - 1 : Math.Max(0, candidate - 1);
            long last = IsInfinite ? candidate + 1 : Math.Min(ItemCount - 1, candidate + 1);
           
            for (long i = first; i <= last; i++)
            {
                float nextDistance = GetDistanceFromCenter(i);
                if (nextDistance < distance)
                {
                    distance = nextDistance;
                    focused = i;
                }
            }

            focusedSlot = focused;
            return GetDataIndex(focused);
        }

        protected bool TryGetView(int index, out Component component)
        {
            component = GetView(index)?.component;
            return component != null;
        }

        internal int GetItemIndex(RectTransform rectTransform)
        {
            for (int i = 0; i < activeViews.Count; i++)
            {
                if (activeViews[i].rectTransform == rectTransform)
                    return activeViews[i].index;
            }
            return -1;
        }

        /// <summary>
        /// Destroys owned views and restores the content transform, LayoutGroup, and ContentSizeFitter.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;

            CheckMutation();
            Exception failure = null;
          
            try { ReleaseAll(); }
            catch (Exception exception) { failure = exception; }
          
            try { TrimPool(); } 
            catch (Exception exception) { failure ??= exception; }

            IsDisposed = true;

            if (poolRoot != null)
            {
                poolRoot.SetParent(null, false);
                UnityEngine.Object.Destroy(poolRoot.gameObject);
            }

            if (content != null)
            {
                content.anchorMin = originalAnchorMin;
                content.anchorMax = originalAnchorMax;
                content.pivot = originalPivot;
                content.sizeDelta = originalSize;
                content.anchoredPosition = originalPosition;
            }

            if (layoutGroup != null)
                layoutGroup.enabled = layoutWasEnabled;

            if (contentFitter != null)
                contentFitter.enabled = fitterWasEnabled;

            scrollRect.ReleaseVirtualizedList(this);

            if (failure != null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected abstract void CreateItem(Component item);
        protected abstract void BindItem(Component item, int index);
        protected abstract void UnbindItem(Component item, int index);
        protected abstract void DestroyItem(Component item);
    }

    /// <summary>
    /// Typed callbacks keep component lookup and permanent event subscriptions in createItem.
    /// bindItem must overwrite per-row state. unbindItem should release row-specific subscriptions and references.
    /// </summary>
    public sealed class VirtualizedList<T> : VirtualizedList where T : Component
    {
        readonly Action<T> createItem;
        readonly Action<T> destroyItem;
        readonly Action<T, int> bindItem;
        readonly Action<T, int> unbindItem;

        internal VirtualizedList(ScrollRect scrollRect, T prefab, Action<T, int> bindItem, VirtualizedListSettings settings,
            Action<T, int> unbindItem, Action<T> createItem, Action<T> destroyItem) : base(scrollRect, prefab, settings)
        {
            this.bindItem = bindItem;
            this.unbindItem = unbindItem;
            this.createItem = createItem;
            this.destroyItem = destroyItem;
        }

        protected override void CreateItem(Component item) => createItem?.Invoke((T)item);
        protected override void BindItem(Component item, int index) => bindItem((T)item, index);
        protected override void UnbindItem(Component item, int index) => unbindItem?.Invoke((T)item, index);
        protected override void DestroyItem(Component item) => destroyItem?.Invoke((T)item);

        /// <summary>
        /// Returns a currently bound view. Offscreen data normally has no view.
        /// </summary>
        public bool TryGetItem(int index, out T item)
        {
            bool found = TryGetView(index, out Component component);
            item = found ? (T)component : null;
            return found;
        }
    }
}