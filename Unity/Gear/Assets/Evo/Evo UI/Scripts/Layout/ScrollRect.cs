using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Layout/Scroll Rect"), HelpURL(Constants.HelpUrl + "layout/scroll-rect")]
    public class ScrollRect : UnityEngine.UI.ScrollRect
    {
        // Smooth Scrolling
        [SerializeField, Tooltip("Smooths mouse wheel, trackpad, and list navigation scrolling. Dragging and explicit positioning remain immediate.")]
        private bool enableSmoothScroll = true;
        [SerializeField, Range(0.01f, 0.5f), Tooltip("Scroll response time in unscaled seconds. Lower values respond faster.")]
        private float smoothScrollTime = 0.08f;

        // Infinite Scrolling
        [SerializeField, Tooltip("Repeats the content along one scroll axis. Scrollbars retain the original list endpoints. " +
            "The viewport determines how many repeated views are needed.")]
        private bool enableInfiniteScroll = false;

        // Virtualization
        [SerializeField, Tooltip("Recycles offscreen views in a bound list. Disable to create a view for every data item. " +
            "Existing content is unaffected until a list is bound.")]
        private bool enableVirtualization = false;
        [SerializeField, Range(0, 100), Tooltip("Extra rows kept before and after the visible area of a virtualized list.")]
        private int virtualizationOverscan = 2;

        // Snapping
        public bool enableSnapping = false;
        [Min(0), Tooltip("Item centered when the scroll rect starts. Indices past the end use the last item.")]
        public int startingIndex = 0;
        [Range(0.05f, 2f)] public float snapDuration = 0.3f;
        public AnimationCurve snapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool disableUnfocused = false;

        // Scaling
        public bool enableScaling = false;
        [Range(0f, 1f)] public float minScale = 0.7f;
        public float scaleDistance = 200f;

        // Fading
        public bool enableFading = false;
        [Range(0f, 1f)] public float minAlpha = 0.3f;
        public float fadeDistance = 150f;

        // Events
        public UnityEvent<int> onItemFocused = new();

        // Constants
        const int SnapPropertyKey = 0x5343524C;
        const float WheelGestureTime = 0.1f;

        // Set by owners that lay out generated content, such as List View. Its children aren't items
        // to snap to or fade, and tracking them would cost a pass over every child per scrolled frame.
        internal bool ignoreContentItems;

        // Helpers
        readonly List<ItemData> items = new();
        int focusedIndex = -1;
        int lastFocusedIndex = -1;
        bool isDragging;
        PointerEventData dragEvent;
        RectTransform itemsContent;
        int contentChildCount;
        bool pendingItemRefresh;
        bool itemsDirty = true;
        bool pendingStartingSnap;
        bool pendingDragSnap;
        Vector2 snapTarget;
        Vector2 snapStartPosition;
        int snapIndex = -1;
        float snapOffset;
        float wheelTicks;
        float wheelTime;
        int wheelDirection;

        // Infinite Scrolling
        InfiniteScrollLayout infiniteLayout;
        Vector2 infinitePreviousPosition;
        bool updatingScrollbars;

        // Smooth Scrolling
        bool smoothScrolling;
        bool scrollLayoutReady;
        RectTransform smoothScrollContent;
        Vector2 smoothScrollTarget;
        Vector2 smoothScrollLastPosition;

        // Cache
        RectTransform snapContent;
        Bounds lastContentBounds;
        Rect lastViewportRect;
        Vector2 lastContentPosition;
        int lastEffectFlags;
        Vector4 lastEffectValues;
        TweenHandle snapAnimation;
        Action snapCompleted;
        static readonly Action<ScrollRect, float> SnapProgressSetter = (target, value) => target.ApplySnapProgress(value);

        protected override void Awake()
        {
            base.Awake();
            snapCompleted = CompleteSnap;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            scrollLayoutReady = false;
            StopSmoothScroll();
            itemsDirty = true;
            pendingItemRefresh = true;
            infinitePreviousPosition = content != null ? content.anchoredPosition : Vector2.zero;
        }

        protected override void Start()
        {
            base.Start();

            if (!Application.isPlaying)
                return;
            if (TracksItems)
                RefreshItems();

            UpdateInfiniteLayout();

            // Applied in the first LateUpdate, after every Start has had a chance to add items
            pendingStartingSnap = enableSnapping && focusedIndex < 0;
        }

        protected override void OnDisable()
        {
            StopSmoothScroll();
            StopSnapAnimation();
            isDragging = false;
            dragEvent = null;
            RestoreItemStates();
            ReleaseInfiniteLayout();

            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            StopSmoothScroll();
            StopSnapAnimation();
            ReleaseInfiniteLayout();

            try { VirtualizedList?.Dispose(); }
            finally { base.OnDestroy(); }
        }

        protected override void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                base.LateUpdate();
                return;
            }

            // Item refreshes below must not cancel the starting snap
            bool startingSnap = pendingStartingSnap;
            pendingStartingSnap = false;

            if (TracksItems && (pendingItemRefresh || itemsContent != content
                || (content != null && content.childCount != contentChildCount)))
                RefreshItems();

            UpdateInfiniteLayout();
            UpdateInfiniteItems();

            if (snapAnimation.IsActive)
                StopMovement();

            // Resolve viewport changes before ScrollRect updates bounds and scrollbar sizes
            bool hasList = HasVirtualizedList;
            Vector2 previousPosition = default;
            Rect previousViewport = default;
            if (hasList)
            {
                VirtualizedList.UpdateVisible();
                previousPosition = content != null ? content.anchoredPosition : Vector2.zero;
                previousViewport = viewRect.rect;
            }

            RefreshSnapTarget();
            WrapInfinitePosition();
            bool appliedSmoothScroll = UpdateSmoothScroll(Time.unscaledDeltaTime);
            MovementType movement = OverrideMovement();

            // The wheel target already respects finite bounds. Let one motion model own
            // this frame, including a smooth return from an interrupted elastic drag.
            if (appliedSmoothScroll)
                movementType = MovementType.Unrestricted;
           
            bool wasUpdatingScrollbars = updatingScrollbars;
            updatingScrollbars = true;
         
            try { base.LateUpdate(); }
            finally
            {
                updatingScrollbars = wasUpdatingScrollbars;
                movementType = movement;
            }

            infinitePreviousPosition = content != null ? content.anchoredPosition : Vector2.zero;

            if (content == null)
                return;

            // Inertia, elasticity, or expanding scrollbars may change the visible range
            if (HasVirtualizedList && (!hasList || !previousPosition.Equals(content.anchoredPosition) || previousViewport != viewRect.rect))
                VirtualizedList.UpdateVisible();

            UpdateInfiniteItems();

            if (startingSnap && enableSnapping && ItemCount > 0)
            {
                // Items added by Start have not been laid out yet
                EnsureLayout();
                SnapToElementInstant(Mathf.Clamp(startingIndex, 0, ItemCount - 1));
                return;
            }

            if (pendingDragSnap)
            {
                pendingDragSnap = false;
                UpdateItemStates();

                if (!isDragging && snapIndex < 0)
                    SnapToElement(focusedIndex);

                return;
            }

            if (ignoreContentItems)
                return;

            if (itemsDirty || !lastContentBounds.Equals(m_ContentBounds) || lastViewportRect != viewRect.rect
                || lastContentPosition != content.anchoredPosition || lastEffectFlags != EffectFlags
                || !lastEffectValues.Equals(EffectValues))
                UpdateItemStates();
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive())
                return;

            base.OnBeginDrag(eventData);

            if (Application.isPlaying)
            {
                StopSmoothScroll();
                base.StopMovement();
                isDragging = true;
                dragEvent = eventData;
                pendingStartingSnap = false;
                StopSnapAnimation();
            }
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnEndDrag(eventData);

            if (Application.isPlaying && isDragging)
            {
                isDragging = false;
                dragEvent = null;
                pendingDragSnap = enableSnapping && IsActive();
                itemsDirty = true;
            }
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (!IsActive())
                return;

            pendingStartingSnap = false;

            // Snapped content moves by whole items instead of by scroll distance
            if (Application.isPlaying && enableSnapping && ItemCount > 0)
            {
                if (!isDragging)
                    QueueSnapScroll(eventData);

                return;
            }

            if (Application.isPlaying && enableSmoothScroll)
            {
                if (!isDragging)
                    QueueSmoothScroll(eventData);

                return;
            }

            StopSmoothScroll();
            StopSnapAnimation();
            MovementType movement = OverrideMovement();

            try { base.OnScroll(eventData); }
            finally { movementType = movement; }
        }

        public override void OnDrag(PointerEventData eventData)
        {
            MovementType movement = OverrideMovement();

            try { base.OnDrag(eventData); }
            finally { movementType = movement; }
        }

        protected override void SetContentAnchoredPosition(Vector2 position)
        {
            base.SetContentAnchoredPosition(position);
            WrapInfinitePosition();
        }

        protected override void SetNormalizedPosition(float value, int axis)
        {
            // Ignore the scrollbars echoing our current position, including Unity's
            // clamped thumb value while the viewport crosses an infinite loop seam.
            if (updatingScrollbars && value == Mathf.Clamp01(axis == 0 ? horizontalNormalizedPosition : verticalNormalizedPosition))
                return;

            // Scrollbars and programmatic scrolling take over from an in-flight snap
            if (Application.isPlaying)
            {
                pendingStartingSnap = false;
                StopSmoothScroll();
                StopSnapAnimation();
            }

            base.SetNormalizedPosition(value, axis);

            if (IsInfinite)
            {
                WrapInfinitePosition();

                if (isDragging && dragEvent != null)
                    base.OnBeginDrag(dragEvent);

                itemsDirty = true;
            }
        }

        public override void Rebuild(UnityEngine.UI.CanvasUpdate executing)
        {
            bool wasUpdatingScrollbars = updatingScrollbars;
            updatingScrollbars = true;
          
            try { base.Rebuild(executing); }
            finally { updatingScrollbars = wasUpdatingScrollbars; }

            if (executing == UnityEngine.UI.CanvasUpdate.PostLayout)
            {
                scrollLayoutReady = true;
                itemsDirty = true;
            }
        }

        #region Smooth Scrolling
        public bool EnableSmoothScroll
        {
            get => enableSmoothScroll;
            set
            {
                enableSmoothScroll = value;
                if (!value)
                    StopSmoothScroll();
            }
        }

        public float SmoothScrollTime
        {
            get => float.IsNaN(smoothScrollTime) || float.IsInfinity(smoothScrollTime) ? 0.08f : Mathf.Clamp(smoothScrollTime, 0.01f, 0.5f);
            set => smoothScrollTime = float.IsNaN(value) || float.IsInfinity(value) ? 0.08f : Mathf.Clamp(value, 0.01f, 0.5f);
        }

        public override void StopMovement()
        {
            StopSmoothScroll();
            base.StopMovement();
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // A press catches free scrolling, but a snap keeps going unless a drag begins
            if (eventData.button == PointerEventData.InputButton.Left && snapIndex < 0)
                StopSmoothScroll();

            base.OnInitializePotentialDrag(eventData);
        }

        void StopSmoothScroll()
        {
            smoothScrolling = false;
            smoothScrollContent = null;

            // A wheel step's glide ends with its smooth scroll
            if (!snapAnimation.IsActive)
                snapIndex = -1;
        }

        // Matches Unity's wheel direction and dominant-axis mapping
        Vector2 MapScrollDelta(Vector2 delta)
        {
            delta.y = -delta.y;
            if (vertical && !horizontal && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                delta.y = delta.x;
            else if (horizontal && !vertical && Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                delta.x = delta.y;

            if (!horizontal)
                delta.x = 0f;
            if (!vertical)
                delta.y = 0f;

            return delta;
        }

        // As in Unity's scroll handler, flush the initial layout only when needed
        void EnsureLayout()
        {
            if (!scrollLayoutReady && !UnityEngine.UI.CanvasUpdateRegistry.IsRebuildingLayout())
                Canvas.ForceUpdateCanvases();
        }

        void QueueSmoothScroll(PointerEventData eventData)
        {
            // Preserve fractional trackpad input and the existing Scroll Sensitivity units
            Vector2 delta = MapScrollDelta(eventData.scrollDelta) * scrollSensitivity;
            if (delta == Vector2.zero || float.IsNaN(delta.x) || float.IsNaN(delta.y)
                || float.IsInfinity(delta.x) || float.IsInfinity(delta.y))
                return;

            EnsureLayout();

            if (!IsActive())
                return;

            StopSnapAnimation();

            Vector2 current = content.anchoredPosition;
            if (!smoothScrolling || smoothScrollContent != content || current != smoothScrollLastPosition)
                smoothScrollTarget = current;

            // Discard the pending distance on a reversing axis so direction changes
            // respond immediately, even after a burst of wheel events.
            Vector2 remaining = smoothScrollTarget - current;
            if (delta.x > 0f && remaining.x < 0f || delta.x < 0f && remaining.x > 0f)
                smoothScrollTarget.x = current.x;
            if (delta.y > 0f && remaining.y < 0f || delta.y < 0f && remaining.y > 0f)
                smoothScrollTarget.y = current.y;

            BeginSmoothScroll(smoothScrollTarget + delta);
            base.StopMovement();
        }

        internal void SmoothScrollTo(Vector2 position)
        {
            StopVirtualizedMovement();
            BeginSmoothScroll(position);
        }

        void BeginSmoothScroll(Vector2 target)
        {
            UpdateBounds();
            smoothScrollTarget = ConstrainSmoothScroll(target);
            smoothScrollLastPosition = content.anchoredPosition;
            smoothScrollContent = content;
            smoothScrolling = !smoothScrollTarget.Equals(smoothScrollLastPosition);
        }

        Vector2 ConstrainSmoothScroll(Vector2 target)
        {
            Vector2 current = content.anchoredPosition;
            if (!horizontal)
                target.x = current.x;
            if (!vertical)
                target.y = current.y;
            if (IsUnrestricted || movementType == MovementType.Unrestricted)
                return target;

            // UpdateBounds pads small content according to its pivot, just as Unity does.
            // Bound the destination as well as the motion to avoid edge backlog.
            Rect view = viewRect.rect;
            Vector2 delta = target - current;
            if (horizontal)
                delta.x = Mathf.Clamp(delta.x, view.xMax - m_ContentBounds.max.x, view.xMin - m_ContentBounds.min.x);
            if (vertical)
                delta.y = Mathf.Clamp(delta.y, view.yMax - m_ContentBounds.max.y, view.yMin - m_ContentBounds.min.y);
           
            return current + delta;
        }

        bool UpdateSmoothScroll(float deltaTime)
        {
            if (!smoothScrolling)
                return false;
        
            if (!enableSmoothScroll || !IsActive() || isDragging || snapAnimation.IsActive
                || content != smoothScrollContent || content.anchoredPosition != smoothScrollLastPosition)
            {
                StopSmoothScroll();
                return false;
            }

            UpdateBounds();
            smoothScrollTarget = ConstrainSmoothScroll(smoothScrollTarget);
            base.StopMovement();
          
            if (deltaTime <= 0f)
                return true;

            Vector2 current = content.anchoredPosition;
            // Exponential decay is independent of frame rate and needs no coroutine,
            // tween, or managed allocation. Input updates the destination in place.
            float blend = 1f - Mathf.Exp(-deltaTime / SmoothScrollTime);
            Vector2 position = Vector2.LerpUnclamped(current, smoothScrollTarget, blend);
            bool complete = (position - smoothScrollTarget).sqrMagnitude <= 0.0001f || position.Equals(current);
            if (complete)
                position = smoothScrollTarget;

            SetContentAnchoredPosition(position);
            smoothScrollLastPosition = content.anchoredPosition;
          
            if (complete)
                StopSmoothScroll();
           
            return true;
        }
        #endregion

        #region Types
        internal sealed class ItemData
        {
            public RectTransform rectTransform;
            public float distance;
            CanvasGroup canvasGroup;
            Vector3 baseScale;
            float baseAlpha;
            bool baseInteractable;
            int effectFlags;

            public void ApplyEffects(int flags, float distance, float minScale, float invScaleDistance,
                float minAlpha, float invFadeDistance, bool focused)
            {
                if (rectTransform == null)
                    return;

                if (((flags | effectFlags) & 1) != 0)
                {
                    if ((effectFlags & 1) == 0)
                        baseScale = rectTransform.localScale;

                    Vector3 scale = (flags & 1) != 0
                        ? baseScale * Mathf.Lerp(1f, minScale, Mathf.Clamp01(distance * invScaleDistance))
                        : baseScale;

                    if (rectTransform.localScale != scale)
                        rectTransform.localScale = scale;
                }

                if ((flags & 6) != 0 && canvasGroup == null)
                {
                    if (!rectTransform.TryGetComponent(out canvasGroup))
                        canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

                    // A replacement group has its own baseline
                    effectFlags &= ~6;
                }

                if (canvasGroup != null)
                {
                    if (((flags | effectFlags) & 2) != 0)
                    {
                        if ((effectFlags & 2) == 0)
                            baseAlpha = canvasGroup.alpha;

                        float alpha = (flags & 2) != 0
                            ? baseAlpha * Mathf.Lerp(1f, minAlpha, Mathf.Clamp01(distance * invFadeDistance))
                            : baseAlpha;

                        if (canvasGroup.alpha != alpha)
                            canvasGroup.alpha = alpha;
                    }
                    if (((flags | effectFlags) & 4) != 0)
                    {
                        if ((effectFlags & 4) == 0)
                            baseInteractable = canvasGroup.interactable;

                        bool interactable = baseInteractable && ((flags & 4) == 0 || focused);
                        if (canvasGroup.interactable != interactable)
                            canvasGroup.interactable = interactable;
                    }
                }

                effectFlags = flags;
            }

            public void RestoreEffects() => ApplyEffects(0, 0f, 1f, 0f, 1f, 0f, true);

            // Navigation ignores our temporary focus restriction, while respecting the bound data and parent groups
            public UnityEngine.UI.Selectable GetNavigationTarget(UnityEngine.UI.Selectable[] selectables)
            {
                bool restricted = (effectFlags & 4) != 0 && canvasGroup != null;
                bool previous = restricted && canvasGroup.interactable;
                if (restricted)
                    canvasGroup.interactable = baseInteractable;

                try
                {
                    foreach (var selectable in selectables)
                    {
                        if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
                            return selectable;
                    }
                    return null;
                }
                finally
                {
                    if (restricted && canvasGroup != null)
                        canvasGroup.interactable = previous;
                }
            }
        }
        #endregion

        #region Scrolling and Snapping
        enum SnapMotion { Instant, Animate, Glide }

        // Snapping centers the first and last items, which finite bounds would pull back.
        // Virtualized lists pad their content for this instead.
        bool IsUnrestricted => IsInfinite || (Application.isPlaying && enableSnapping && !HasVirtualizedList);

        bool CanSnap => enableSnapping && Application.isPlaying && isActiveAndEnabled && !isDragging;

        // Lifts finite bounds for a base call; the caller restores the returned value
        MovementType OverrideMovement()
        {
            MovementType movement = movementType;
            if (IsUnrestricted)
                movementType = MovementType.Unrestricted;

            return movement;
        }

        void StopSnapAnimation()
        {
            snapAnimation.Kill();
            snapAnimation = default;
            snapContent = null;
            snapIndex = -1;
            pendingDragSnap = false;
            itemsDirty = true;
        }

        void ApplySnapProgress(float value)
        {
            if (!isActiveAndEnabled || !enableSnapping || snapContent == null || content != snapContent)
            {
                StopSnapAnimation();
                return;
            }

            // Keep custom curves clamped, matching the original snap interpolation
            snapContent.anchoredPosition = Vector2.Lerp(snapStartPosition, snapTarget, value);
            itemsDirty = true;
        }

        void CompleteSnap()
        {
            if (snapContent != null && content == snapContent)
                snapContent.anchoredPosition = snapTarget;

            snapAnimation = default;
            snapContent = null;
            snapIndex = -1;
            itemsDirty = true;
        }

        // Infinite content uses the loop nearest to 'from', moved past it when a direction is given
        bool TryGetSnapPosition(int index, float offset, Vector2 from, int direction, out Vector2 position)
        {
            position = default;

            if (content == null || index < 0 || index >= ItemCount || float.IsNaN(offset) || float.IsInfinity(offset))
                return false;

            UpdateInfiniteLayout();

            if (HasVirtualizedList)
            {
                VirtualizedList list = VirtualizedList;
                list.UpdateVisible();
                if (!HasVirtualizedList || VirtualizedList != list || index >= list.ItemCount)
                    return false;

                position = list.GetSnapPosition(index, offset, from);
            }
            else
            {
                RectTransform item = items[index].rectTransform;
                if (item == null)
                    return false;

                if (IsInfinite) { position = infiniteLayout.GetSnapPosition(index, offset, from); }
                else
                {
                    Vector3 difference = viewRect.TransformPoint(viewRect.rect.center) - item.TransformPoint(item.rect.center);

                    // Anchored positions use the parent's axes, including scaled or rotated content
                    if (content.parent != null)
                        difference = content.parent.InverseTransformVector(difference);

                    position = content.anchoredPosition;

                    if (vertical) { position.y += difference.y + offset; }
                    else { position.x += difference.x + offset; }
                }
            }

            AdjustInfiniteSnap(ref position, from, direction);
            return true;
        }

        void RefreshSnapTarget()
        {
            // Virtualized and repeated layouts can re-measure while a snap is in flight
            if (snapIndex < 0 || (!HasVirtualizedList && infiniteLayout == null)
                || !TryGetSnapPosition(snapIndex, snapOffset, snapTarget, 0, out Vector2 position))
                return;

            snapTarget = position;

            if (smoothScrolling)
                smoothScrollTarget = position;
        }

        void QueueSnapScroll(PointerEventData eventData)
        {
            // Count wheel ticks rather than distance, so one notch moves one item with any input module
            EventSystem eventSystem = EventSystem.current;
            Vector2 ticks = eventSystem != null && eventSystem.currentInputModule != null
                ? eventSystem.currentInputModule.ConvertPointerEventScrollDeltaToTicks(eventData.scrollDelta)
                : eventData.scrollDelta;

            // Positive steps advance to later items, and a negative sensitivity inverts the wheel as usual
            ticks = MapScrollDelta(ticks);
            float amount = (vertical ? ticks.y : -ticks.x) * Mathf.Sign(scrollSensitivity);
            if (scrollSensitivity == 0f || amount == 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return;

            // A new gesture or reversal moves at least one item, so fractional input responds at once.
            // Continuous input then moves one item per tick, which keeps trackpads from racing ahead.
            int direction = amount > 0f ? 1 : -1;
            float time = Time.unscaledTime;
            if (direction != wheelDirection || time - wheelTime > WheelGestureTime)
                wheelTicks = direction * Mathf.Max(Mathf.Abs(amount), 1f);
            else
                wheelTicks += amount;

            wheelDirection = direction;
            wheelTime = time;

            // Tolerate rounding error in whole ticks
            int steps = (int)(wheelTicks + direction * 0.01f);
            if (steps == 0)
                return;

            wheelTicks -= steps;
            EnsureLayout();

            if (IsActive())
                SnapBy(steps, enableSmoothScroll ? SnapMotion.Glide : SnapMotion.Animate);
        }

        void SnapBy(int steps, SnapMotion motion)
        {
            int count = ItemCount;
            if (!CanSnap || count == 0)
                return;

            // The focus is held on the destination of an in-flight snap, so repeated steps queue up
            if (focusedIndex < 0 || focusedIndex >= count)
                UpdateItemStates();
            if (focusedIndex < 0 || focusedIndex >= count)
                return;

            int index = focusedIndex + steps;

            // Infinite content wraps around and keeps moving in the step's direction
            if (IsInfinite)
            {
                SnapTo((index % count + count) % count, 0f, Math.Sign(steps), motion);
                return;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            if (index != focusedIndex)
                SnapTo(index, 0f, 0, motion);
        }

        void SnapTo(int index, float offset, int direction, SnapMotion motion)
        {
            if (content == null)
                return;

            // Continue from an in-flight target, so queued steps pick the right loop of infinite content
            Vector2 from = snapIndex >= 0 ? snapTarget : content.anchoredPosition;
            if (!TryGetSnapPosition(index, offset, from, direction, out Vector2 position))
                return;

            StopVirtualizedMovement();
            pendingStartingSnap = false;
            focusedIndex = index;
            snapIndex = index;
            snapOffset = offset;
            snapTarget = position;

            // Wheel steps glide like smooth scrolling, so quick input retargets without restarting an ease
            if (motion == SnapMotion.Glide) { BeginSmoothScroll(position); }
            else if (motion == SnapMotion.Animate && snapDuration > 0f && !float.IsInfinity(snapDuration)
                && !content.anchoredPosition.Equals(position))
            {
                snapContent = content;
                snapStartPosition = content.anchoredPosition;
                bool hasCustomCurve = snapCurve != null && snapCurve.length > 0;
                snapAnimation = EvoTween.Create(0f, 1f, snapDuration)
                    .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? snapCurve : null)
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(snapCompleted)
                    .Bind(this, SnapProgressSetter, SnapPropertyKey);
            }

            if (smoothScrolling || snapAnimation.IsActive)
                return;

            // Nothing to animate, so land at once
            snapIndex = -1;
            content.anchoredPosition = position;

            if (HasVirtualizedList)
                VirtualizedList.UpdateVisible();

            WrapInfinitePosition();
            UpdateInfiniteItems();
            UpdateItemStates();
        }

        void AdjustInfiniteSnap(ref Vector2 position, Vector2 from, int direction)
        {
            if (!IsInfinite || direction == 0)
                return;

            int axis = vertical ? 1 : 0;
            float sign = vertical ? 1f : -1f;
            float distance = (position[axis] - from[axis]) * sign;
            if (direction > 0 && distance <= 0f || direction < 0 && distance >= 0f)
                position[axis] += direction * sign * InfiniteCycleSize;
        }
        #endregion

        #region Infinite Scrolling
        public bool EnableInfiniteScroll
        {
            get => enableInfiniteScroll;
            set
            {
                if (enableInfiniteScroll == value)
                    return;

                enableInfiniteScroll = value;
                StopVirtualizedMovement();

                if (Application.isPlaying && isActiveAndEnabled)
                {
                    UpdateInfiniteLayout();
                    VirtualizedList?.UpdateVisible();
                }
            }
        }

        public bool IsInfinite => Application.isPlaying && enableInfiniteScroll && horizontal != vertical
            && content != null && (HasVirtualizedList ? VirtualizedList.IsInfinite : infiniteLayout != null);
      
        float InfiniteCycleSize => HasVirtualizedList ? VirtualizedList.CycleSize : infiniteLayout.CycleSize;

        void UpdateInfiniteLayout()
        {
            if (infiniteLayout != null && (!enableInfiniteScroll || HasVirtualizedList || !infiniteLayout.MatchesContent))
                ReleaseInfiniteLayout();

            if (!Application.isPlaying || !isActiveAndEnabled || !enableInfiniteScroll || HasVirtualizedList
                || infiniteLayout != null || content == null || items.Count == 0
                || horizontal == vertical || content.parent != viewRect || content.localScale != Vector3.one
                || content.localRotation != Quaternion.identity)
                return;

            StopVirtualizedMovement();
            infiniteLayout = new InfiniteScrollLayout(this, items);
            infinitePreviousPosition = content.anchoredPosition;
            UpdateBounds();
            UpdatePrevData();
            itemsDirty = true;
        }

        void UpdateInfiniteItems()
        {
            if (infiniteLayout == null)
                return;

            infiniteLayout.UpdateVisible();
            contentChildCount = content.childCount;
        }

        void ReleaseInfiniteLayout()
        {
            if (infiniteLayout == null)
                return;

            StopVirtualizedMovement();
            infiniteLayout.Dispose();
            infiniteLayout = null;
            pendingItemRefresh = true;
            itemsDirty = true;
        }

        internal void WrapInfinitePosition()
        {
            if (!IsInfinite)
                return;

            float cycle = InfiniteCycleSize;
            int axis = vertical ? 1 : 0;
            Vector2 position = content.anchoredPosition;
            float offset = vertical ? position.y : -position.x;
          
            if (cycle <= 0f || (offset >= 0f && offset <= cycle) || float.IsNaN(offset) || float.IsInfinity(offset))
                return;

            long cycles = (long)Math.Floor((double)offset / cycle);
            float wrapped = (float)(((double)offset % cycle + cycle) % cycle);
            Vector2 shift = Vector2.zero;
            shift[axis] = (vertical ? -1f : 1f) * cycles * cycle;
            position[axis] = vertical ? wrapped : -wrapped;
            snapStartPosition += shift;
            snapTarget += shift;
            smoothScrollTarget += shift;
            smoothScrollLastPosition += shift;
            infinitePreviousPosition += shift;

            // Shift Unity's previous sample and drag origin together,
            // preserving momentum and the pointer delta when content crosses the seam.
            content.anchoredPosition = infinitePreviousPosition;
            UpdateBounds();
            UpdatePrevData();
            content.anchoredPosition = position;
            UpdateBounds();

            if (HasVirtualizedList)
                VirtualizedList.ShiftInfiniteSlots(cycles);
            else
                infiniteLayout.ShiftCycles(cycles);
          
            m_ContentStartPosition += shift;
            itemsDirty = true;
        }
        #endregion

        #region Item Visuals
        int EffectFlags => (enableScaling ? 1 : 0) | (enableFading ? 2 : 0) | (enableSnapping && disableUnfocused ? 4 : 0);
        Vector4 EffectValues => new(minScale, scaleDistance, minAlpha, fadeDistance);

        void RestoreItemStates()
        {
            if (HasVirtualizedList)
            {
                for (int i = 0; i < VirtualizedList.ActiveCount; i++)
                    VirtualizedList.GetItemState(i).RestoreEffects();
            }
            else
            {
                foreach (ItemData item in items)
                    item.RestoreEffects();
            }

            if (infiniteLayout != null)
            {
                for (int i = 0; i < infiniteLayout.CopyCount; i++)
                    infiniteLayout.GetCopyState(i).RestoreEffects();
            }

            lastEffectFlags = 0;
            itemsDirty = true;
        }

        void UpdateItemStates()
        {
            if (content == null)
                return;

            itemsDirty = false;
            lastContentBounds = m_ContentBounds;
            lastViewportRect = viewRect.rect;
            lastContentPosition = content.anchoredPosition;
            int flags = EffectFlags;
            bool applyEffects = (flags | lastEffectFlags) != 0;
            lastEffectFlags = flags;
            lastEffectValues = EffectValues;
            float invScaleDistance = scaleDistance > 0f ? 1f / scaleDistance : 0f;
            float invFadeDistance = fadeDistance > 0f ? 1f / fadeDistance : 0f;

            if (HasVirtualizedList)
            {
                int index = VirtualizedList.GetFocusedIndex();
                if (applyEffects)
                {
                    for (int i = 0; i < VirtualizedList.ActiveCount; i++)
                    {
                        VirtualizedList.GetItemState(i).ApplyEffects(flags, VirtualizedList.GetActiveDistanceFromCenter(i),
                            minScale, invScaleDistance, minAlpha, invFadeDistance, VirtualizedList.IsActiveFocused(i));
                    }
                }
                UpdateFocusedIndex(index);
                return;
            }

            Matrix4x4 worldToContent = content.worldToLocalMatrix;
            Vector3 viewportCenter = worldToContent.MultiplyPoint3x4(viewRect.TransformPoint(lastViewportRect.center));
            float minDistance = float.MaxValue;
            int focusedView = -1;
            int count = items.Count + (infiniteLayout?.CopyCount ?? 0);

            // Measure each view once. Effects need the focused view, so they follow in a second pass.
            for (int i = 0; i < count; i++)
            {
                ItemData state = GetSceneItemState(i);
                RectTransform rt = state.rectTransform;
                state.distance = -1f;

                // Pooled copies keep their restored state until reused
                if (rt == null || i >= items.Count && !infiniteLayout.IsCopyActive(i - items.Count))
                    continue;

                Vector3 center = worldToContent.MultiplyPoint3x4(rt.TransformPoint(rt.rect.center));
                state.distance = Mathf.Abs(vertical ? viewportCenter.y - center.y : viewportCenter.x - center.x);
                if (state.distance < minDistance && rt.gameObject.activeInHierarchy)
                {
                    minDistance = state.distance;
                    focusedView = i;
                }
            }

            if (applyEffects)
            {
                for (int i = 0; i < count; i++)
                {
                    ItemData state = GetSceneItemState(i);
                    if (state.distance >= 0f)
                        state.ApplyEffects(flags, state.distance, minScale, invScaleDistance, minAlpha, invFadeDistance, i == focusedView);
                }
            }

            UpdateFocusedIndex(focusedView < items.Count ? focusedView : infiniteLayout.GetCopyIndex(focusedView - items.Count));
        }

        ItemData GetSceneItemState(int index) => index < items.Count ? items[index] : infiniteLayout.GetCopyState(index - items.Count);

        void UpdateFocusedIndex(int index)
        {
            // An in-flight snap keeps its destination focused
            if (snapIndex < 0 || focusedIndex >= ItemCount)
                focusedIndex = index;

            if (focusedIndex < 0) { lastFocusedIndex = -1; }
            else if (!isDragging && focusedIndex != lastFocusedIndex)
            {
                lastFocusedIndex = focusedIndex;
                onItemFocused?.Invoke(focusedIndex);
            }
        }

        void ResetFocus()
        {
            focusedIndex = -1;
            lastFocusedIndex = -1;
        }

        ItemData CreateItemData(RectTransform target) => new() { rectTransform = target };
        #endregion

        #region Virtualization
        public VirtualizedList VirtualizedList { get; private set; }
        public bool HasVirtualizedList => VirtualizedList != null && !VirtualizedList.IsDisposed;
        public bool IsVirtualized => HasVirtualizedList && enableVirtualization;
        public int ItemCount => HasVirtualizedList ? VirtualizedList.ItemCount : items.Count;
        bool TracksItems => !HasVirtualizedList && !ignoreContentItems;

        internal void InvalidateItemStates() => itemsDirty = true;
        internal void CancelStartingSnap() => pendingStartingSnap = false;

        public bool EnableVirtualization
        {
            get => enableVirtualization;
            set
            {
                if (enableVirtualization == value)
                    return;

                enableVirtualization = value;
                VirtualizedList?.UpdateVisible();
                itemsDirty = true;
            }
        }

        public int VirtualizationOverscan
        {
            get => Mathf.Clamp(virtualizationOverscan, 0, 100);
            set
            {
                value = Mathf.Clamp(value, 0, 100);
                if (virtualizationOverscan == value)
                    return;

                virtualizationOverscan = value;
                VirtualizedList?.UpdateVisible();
                itemsDirty = true;
            }
        }

        /// <summary>
        /// Creates a reusable list on this scroll rect's content. Call SetItemCount after providing the data.
        /// EnableVirtualization controls whether views are recycled or created for all data items.
        /// One list can own this scroll rect at a time.
        /// </summary>
        public VirtualizedList<T> CreateVirtualizedList<T>(T prefab, Action<T, int> bindItem,
            VirtualizedListSettings settings = null, Action<T, int> unbindItem = null, Action<T> createItem = null,
            Action<T> destroyItem = null) where T : Component
        {
            if (HasVirtualizedList)
                throw new InvalidOperationException("Dispose the current virtualized list before creating another one.");
            if (bindItem == null)
                throw new ArgumentNullException(nameof(bindItem));

            ReleaseInfiniteLayout();
            RestoreItemStates();
            var list = new VirtualizedList<T>(this, prefab, bindItem, settings, unbindItem, createItem, destroyItem);
            VirtualizedList = list;
            StopVirtualizedMovement();
            items.Clear();
            ResetFocus();
            return list;
        }

        internal void StopVirtualizedMovement()
        {
            // A data/layout change invalidates Unity's cached drag origin
            if (isDragging && dragEvent != null)
                base.OnEndDrag(dragEvent);

            isDragging = false;
            dragEvent = null;
            StopMovement();
            StopSnapAnimation();
        }

        internal void ReleaseVirtualizedList(VirtualizedList list)
        {
            if (VirtualizedList != list)
                return;

            StopVirtualizedMovement();
            VirtualizedList = null;
            ResetFocus();
        }
        #endregion

        #region Public API
        public void RefreshItems()
        {
            StopSmoothScroll();
            StopSnapAnimation();
            ReleaseInfiniteLayout();
            pendingItemRefresh = false;

            if (HasVirtualizedList)
            {
                VirtualizedList.RefreshItems();
                return;
            }

            if (content != itemsContent)
            {
                RestoreItemStates();
                items.Clear();
                itemsContent = content;
                ResetFocus();
            }

            if (content == null)
            {
                items.Clear();
                ResetFocus();
                return;
            }

            int childCount = content.childCount;
            contentChildCount = childCount;
            if (items.Capacity < childCount)
                items.Capacity = childCount;

            int itemCount = 0;
            bool changed = false;
            for (int i = 0; i < childCount; i++)
            {
                if (content.GetChild(i) is not RectTransform child || !child.gameObject.activeInHierarchy)
                    continue;

                if (itemCount >= items.Count)
                {
                    items.Add(CreateItemData(child));
                    changed = true;
                }
                else if (items[itemCount].rectTransform != child)
                {
                    items[itemCount].RestoreEffects();
                    items[itemCount] = CreateItemData(child);
                    changed = true;
                }

                itemCount++;
            }

            if (itemCount < items.Count)
            {
                for (int i = itemCount; i < items.Count; i++)
                    items[i].RestoreEffects();

                items.RemoveRange(itemCount, items.Count - itemCount);
                changed = true;
            }

            if (changed)
                ResetFocus();
        }

        public void AddTransform(RectTransform rectTransform)
        {
            if (rectTransform == null || HasVirtualizedList)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].rectTransform == rectTransform)
                    return;
            }

            ReleaseInfiniteLayout();
            items.Add(CreateItemData(rectTransform));
            itemsDirty = true;
        }

        public void RemoveTransform(RectTransform rectTransform)
        {
            if (rectTransform == null || HasVirtualizedList)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].rectTransform != rectTransform)
                    continue;

                StopSnapAnimation();
                ReleaseInfiniteLayout();
                items[i].RestoreEffects();
                items.RemoveAt(i);
                ResetFocus();
                return;
            }
        }

        public void InstantiateObject(GameObject rectObject, Transform parent)
        {
            if (rectObject == null || HasVirtualizedList)
                return;

            GameObject createdObj = Instantiate(rectObject, parent);
            if (createdObj.transform is RectTransform rectTransform)
                AddTransform(rectTransform);
        }

        public void SnapToElement(int index, float offset = 0)
        {
            if (CanSnap)
                SnapTo(index, offset, 0, SnapMotion.Animate);
        }

        public void SnapToElementInstant(int index, float offset = 0) => SnapTo(index, offset, 0, SnapMotion.Instant);

        public void SnapToElement(RectTransform targetRect)
        {
            if (targetRect == null)
                return;

            if (HasVirtualizedList)
            {
                SnapToElement(VirtualizedList.GetItemIndex(targetRect));
                return;
            }

            for (int i = 0; i < items.Count + (infiniteLayout?.CopyCount ?? 0); i++)
            {
                if (GetSceneItemState(i).rectTransform == targetRect)
                {
                    SnapToElement(i < items.Count ? i : infiniteLayout.GetCopyIndex(i - items.Count));
                    return;
                }
            }
        }

        public void ScrollToNext() => SnapBy(1, SnapMotion.Animate);

        public void ScrollToPrevious() => SnapBy(-1, SnapMotion.Animate);
        #endregion
    }
}