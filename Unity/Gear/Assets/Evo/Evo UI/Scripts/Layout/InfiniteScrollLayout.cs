using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Repeats an authored layout along the ScrollRect's active axis, reusing original children
    /// and pooling additional occurrences needed by the viewport or retained input.
    /// </summary>
    internal sealed class InfiniteScrollLayout : IDisposable
    {
        readonly ScrollRect scrollRect;
        readonly RectTransform content;
        readonly RectTransform viewport;
        readonly bool vertical;
        readonly LayoutGroup layoutGroup;
        readonly ContentSizeFitter contentFitter;
        readonly bool layoutWasEnabled;
        readonly bool fitterWasEnabled;
        RectTransform poolRoot;
        readonly List<Source> sources = new();
        readonly List<Copy> copies = new();
        readonly Action invalidateVisible;
        RectState contentState;
        Vector2 lastViewportSize;
        Vector2 lastPosition;
        bool dirty = true;
        bool layoutDirty = true;
        GameObject lastSelection;
        readonly Vector3[] corners = new Vector3[4];

        internal float CycleSize { get; private set; }
        internal int CopyCount => copies.Count;

        struct RectState
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 size;
            public Vector2 position;

            public RectState(RectTransform rt)
            {
                anchorMin = rt.anchorMin;
                anchorMax = rt.anchorMax;
                pivot = rt.pivot;
                size = rt.sizeDelta;
                position = rt.anchoredPosition;
            }

            public readonly void Apply(RectTransform rt)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = pivot;
                rt.sizeDelta = size;
                rt.anchoredPosition = position;
            }
        }

        sealed class Source
        {
            public ScrollRect.ItemData state;
            public RectState rect;
            public float position;
            public float size;
            public float min;
            public float max;
            public long cycle;
            public VirtualizedListItem interaction;
            public Dictionary<long, Copy> activeCopies;
            public Stack<Copy> pooledCopies;
        }

        sealed class Copy
        {
            public ScrollRect.ItemData state;
            public int index;
            public long cycle;
            public bool active;
            public VirtualizedListItem interaction;
        }

        internal InfiniteScrollLayout(ScrollRect scrollRect, List<ScrollRect.ItemData> items)
        {
            this.scrollRect = scrollRect;
            invalidateVisible = InvalidateVisible;
            content = scrollRect.content;
            viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            vertical = scrollRect.vertical;
            contentState = new RectState(content);
            layoutGroup = content.GetComponent<LayoutGroup>();
            contentFitter = content.GetComponent<ContentSizeFitter>();
            layoutWasEnabled = layoutGroup != null && layoutGroup.enabled;
            fitterWasEnabled = contentFitter != null && contentFitter.enabled;

            foreach (ScrollRect.ItemData item in items)
            {
                RectTransform rt = item.rectTransform;
                if (!rt.TryGetComponent(out VirtualizedListItem interaction))
                    interaction = rt.gameObject.AddComponent<VirtualizedListItem>();
                
                interaction.Initialize(null, sources.Count, false, item, invalidateVisible);
                sources.Add(new Source { state = item, rect = new RectState(rt), interaction = interaction });
            }

            Measure();
        }

        internal bool MatchesContent => scrollRect.content == content && scrollRect.vertical == vertical
            && scrollRect.horizontal != vertical && content != null && content.parent == viewport
            && content.localScale == Vector3.one && content.localRotation == Quaternion.identity
            && (scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform) == viewport;

        internal ScrollRect.ItemData GetCopyState(int index) => copies[index].state;
       
        internal int GetCopyIndex(int index) => copies[index].index;
       
        internal bool IsCopyActive(int index) => copies[index].active && copies[index].state.rectTransform != null;

        void InvalidateVisible() => dirty = true;

        internal Vector2 GetSnapPosition(int index, float offset, Vector2 from)
        {
            Source source = sources[index];
            float size = vertical ? viewport.rect.height : viewport.rect.width;
            float target = (source.min + source.max - size) * 0.5f;
            float reference = vertical ? from.y : -from.x;
            target += (float)Math.Round((reference - target) / CycleSize) * CycleSize;
            Vector2 position = content.anchoredPosition;
           
            if (vertical)
                position.y = target + offset;
            else
                position.x = -target + offset;

            return position;
        }

        void RestoreGeometry()
        {
            contentState.Apply(content);

            foreach (Source source in sources)
            {
                if (source.state.rectTransform != null)
                {
                    source.state.RestoreEffects();
                    source.rect.Apply(source.state.rectTransform);
                }
            }

            if (layoutGroup != null)
                layoutGroup.enabled = layoutWasEnabled;

            if (contentFitter != null)
                contentFitter.enabled = fitterWasEnabled;
        }

        void Measure()
        {
            float offset = CycleSize > 0f ? (vertical ? content.anchoredPosition.y : -content.anchoredPosition.x) : 0f;
            RestoreGeometry();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            contentState = new RectState(content);
            Rect rect = content.rect;
            Vector2 leading = new(content.localPosition.x + rect.xMin - viewport.rect.xMin,
                content.localPosition.y + rect.yMax - viewport.rect.yMax);
           
            if (CycleSize <= 0f)
                offset = vertical ? leading.y : -leading.x;
          
            float spacing = layoutGroup switch
            {
                HorizontalOrVerticalLayoutGroup linear => linear.spacing,
                GridLayoutGroup grid => vertical ? grid.spacing.y : grid.spacing.x,
                FlexibleGridLayout grid => vertical ? grid.Spacing.y : grid.Spacing.x,
                _ => 0f
            };

            CycleSize = Mathf.Max(1f, (vertical ? rect.height : rect.width) + spacing);

            foreach (Source source in sources)
            {
                RectTransform rt = source.state.rectTransform;
                if (rt == null)
                    continue;

                source.rect = new RectState(rt);
                source.position = vertical ? rt.localPosition.y - rect.yMax : rt.localPosition.x - rect.xMin;
                source.size = vertical ? rt.rect.height : rt.rect.width;
                rt.GetWorldCorners(corners);
                source.min = float.MaxValue;
                source.max = float.MinValue;

                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = content.InverseTransformPoint(corners[i]);
                    float position = vertical ? rect.yMax - point.y : point.x - rect.xMin;
                    source.min = Mathf.Min(source.min, position);
                    source.max = Mathf.Max(source.max, position);
                }
            }

            if (layoutGroup != null)
                layoutGroup.enabled = false;

            if (contentFitter != null)
                contentFitter.enabled = false;

            content.anchorMin = new Vector2(0f, vertical ? 1f : 0f);
            content.anchorMax = new Vector2(vertical ? 1f : 0f, 1f);
            content.pivot = new Vector2(0f, 1f);

            // Retain the authored cross-axis size and position
            content.sizeDelta = vertical ? new Vector2(rect.width - viewport.rect.width, 0f)
                : new Vector2(0f, rect.height - viewport.rect.height);
            content.SetSizeWithCurrentAnchors(vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal,
                vertical ? rect.height : rect.width);
            content.anchoredPosition = vertical ? new Vector2(leading.x, offset) : new Vector2(-offset, leading.y);
            lastViewportSize = viewport.rect.size;

            layoutDirty = true;
            dirty = true;
        }

        void Position(RectTransform rt, Source source, long cycle, bool applyLayout)
        {
            if (applyLayout)
            {
                Vector2 min = source.rect.anchorMin;
                Vector2 max = source.rect.anchorMax;
               
                if (vertical)
                    min.y = max.y = 1f;
                else
                    min.x = max.x = 0f;

                rt.anchorMin = min;
                rt.anchorMax = max;
                rt.pivot = source.rect.pivot;
                rt.sizeDelta = source.rect.size;
                rt.SetSizeWithCurrentAnchors(vertical ? RectTransform.Axis.Vertical : RectTransform.Axis.Horizontal, source.size);
            }

            Vector2 position = source.rect.position;
            
            if (vertical)
                position.y = source.position - cycle * CycleSize;
            else
                position.x = source.position + cycle * CycleSize;

            if (rt.anchoredPosition != position)
                rt.anchoredPosition = position;
        }

        internal void UpdateVisible()
        {
            if (!MatchesContent)
                return;
           
            if (viewport.rect.size != lastViewportSize)
                Measure();
           
            GameObject selection = VirtualizedListItem.SelectedObject;
            if (!dirty && selection == lastSelection && content.anchoredPosition == lastPosition)
                return;

            float start = vertical ? content.anchoredPosition.y : -content.anchoredPosition.x;
            float size = vertical ? viewport.rect.height : viewport.rect.width;
            float end = start + size;
          
            lastSelection = selection;
            dirty = false;

            // Release occurrences first so changing the visible window reuses existing copies
            for (int i = 0; i < copies.Count; i++)
            {
                Copy copy = copies[i];
                if (!copy.active)
                    continue;

                Source source = sources[copy.index];
                bool active = copy.cycle >= Math.Ceiling((start - source.max) / CycleSize)
                    && copy.cycle <= Math.Floor((end - source.min) / CycleSize);
                active &= source.state.rectTransform != null && source.state.rectTransform.gameObject.activeSelf;
                bool inUse = copy.interaction != null && copy.interaction.IsInUse;
                
                if (!active && !inUse)
                    ReleaseCopy(copy, source);
                else if (!active && copy.state.rectTransform != null)
                    Position(copy.state.rectTransform, source, copy.cycle, layoutDirty);
            }

            for (int i = 0; i < sources.Count; i++)
            {
                Source source = sources[i];
                RectTransform rt = source.state.rectTransform;
                if (rt == null || !rt.gameObject.activeSelf)
                    continue;

                long first = (long)Math.Ceiling((start - source.max) / CycleSize);
                long last = (long)Math.Floor((end - source.min) / CycleSize);
                long nearest = (long)Math.Round((start + size * 0.5f - (source.min + source.max) * 0.5f) / CycleSize);
                // Only a press pins an original. A selected original moves with its item, so the
                // visible occurrence stays the live object that owns its listeners and selection state.
                bool pressed = source.interaction != null && source.interaction.IsPressed;

                if ((source.cycle < first || source.cycle > last) && !pressed)
                {
                    // Do not replace a copy that currently owns pointer input or selection.
                    bool occupied = source.activeCopies != null
                        && source.activeCopies.TryGetValue(nearest, out Copy copy)
                        && copy.interaction != null && copy.interaction.IsInUse;
                    if (!occupied)
                        source.cycle = nearest;
                }
              
                Position(rt, source, source.cycle, layoutDirty);

                if (source.activeCopies != null && source.activeCopies.TryGetValue(source.cycle, out Copy replaced))
                    ReleaseCopy(replaced, source);

                for (long cycle = first; cycle <= last; cycle++)
                {
                    if (cycle == source.cycle)
                        continue;

                    source.activeCopies ??= new Dictionary<long, Copy>();
                    source.pooledCopies ??= new Stack<Copy>();
                    source.activeCopies.TryGetValue(cycle, out Copy copy);
                   
                    while (copy == null && source.pooledCopies.Count > 0)
                    {
                        Copy candidate = source.pooledCopies.Pop();
                        if (candidate.state.rectTransform != null)
                            copy = candidate;
                    }

                    if (copy == null)
                    {
                        if (poolRoot == null)
                        {
                            poolRoot = new GameObject("Infinite Scroll Pool", typeof(RectTransform)).GetComponent<RectTransform>();
                            poolRoot.SetParent(viewport, false);
                            poolRoot.gameObject.SetActive(false);
                        }

                        source.state.RestoreEffects();
                        RectTransform instance = UnityEngine.Object.Instantiate(rt, poolRoot);
                        instance.gameObject.SetActive(false);

                        if (!instance.TryGetComponent(out LayoutElement element))
                            element = instance.gameObject.AddComponent<LayoutElement>();
                       
                        element.ignoreLayout = true;
                        copy = new Copy { index = i, state = new ScrollRect.ItemData { rectTransform = instance } };
                       
                        if (!instance.TryGetComponent(out copy.interaction))
                            copy.interaction = instance.gameObject.AddComponent<VirtualizedListItem>();
                       
                        copy.interaction.Initialize(null, i, false, copy.state, invalidateVisible);
                        copies.Add(copy);
                    }

                    copy.cycle = cycle;
                    source.activeCopies[cycle] = copy;
                  
                    RectTransform target = copy.state.rectTransform;
                    if (target.parent != content)
                        target.SetParent(content, false);
                   
                    Position(target, source, cycle, layoutDirty || !copy.active);
                    copy.active = true;
                   
                    if (!target.gameObject.activeSelf)
                        target.gameObject.SetActive(true);
                }
            }

            lastPosition = content.anchoredPosition;
            layoutDirty = false;
            scrollRect.InvalidateItemStates();
        }

        void ReleaseCopy(Copy copy, Source source)
        {
            copy.state.RestoreEffects();

            if (copy.state.rectTransform != null)
                copy.state.rectTransform.gameObject.SetActive(false);
            
            copy.active = false;
            source.activeCopies.Remove(copy.cycle);
            source.pooledCopies.Push(copy);
        }

        internal void ShiftCycles(long cycles)
        {
            foreach (Source source in sources)
            {
                source.cycle -= cycles;
                source.activeCopies?.Clear();
            }

            foreach (Copy copy in copies)
            {
                copy.cycle -= cycles;
                if (copy.active)
                    sources[copy.index].activeCopies.Add(copy.cycle, copy);
            }

            dirty = true;
        }

        public void Dispose()
        {
            foreach (Source source in sources)
            {
                if (source.interaction != null)
                {
                    source.interaction.CancelPress();
                    source.interaction.Initialize(null, -1, false, source.state);
                }
            }

            // Unity rejects reparenting while the hierarchy is being deactivated, which would
            // leave every copy under the content. Destroy those copies directly instead.
            bool canReparent = poolRoot != null && content != null && content.gameObject.activeInHierarchy;
            foreach (Copy copy in copies)
            {
                if (copy.state.rectTransform == null)
                    continue;
               
                copy.state.rectTransform.gameObject.SetActive(false);

                if (canReparent) { copy.state.rectTransform.SetParent(poolRoot, false); }
                else { UnityEngine.Object.Destroy(copy.state.rectTransform.gameObject); }
            }

            if (content != null)
            {
                float offset = vertical ? content.anchoredPosition.y : -content.anchoredPosition.x;
                RestoreGeometry();
              
                if (viewport != null)
                {
                    Rect rect = content.rect;
                    float size = vertical ? viewport.rect.height : viewport.rect.width;
                    float maximum = Mathf.Max(0f, (vertical ? rect.height : rect.width) - size);
                    // Refreshing the same loop must not clamp its phase to the finite endpoint.
                    if (!scrollRect.EnableInfiniteScroll || !scrollRect.isActiveAndEnabled || !MatchesContent)
                        offset = Mathf.Clamp(offset, 0f, maximum);
                   
                    Vector2 position = content.anchoredPosition;
                   
                    if (vertical)
                        position.y += offset - (content.localPosition.y + rect.yMax - viewport.rect.yMax);
                    else
                        position.x -= offset - (viewport.rect.xMin - content.localPosition.x - rect.xMin);
                 
                    content.anchoredPosition = position;
                }
            }

            if (poolRoot != null)
                UnityEngine.Object.Destroy(poolRoot.gameObject);
        }
    }
}