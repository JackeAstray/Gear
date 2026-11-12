using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Applies a macOS dock-style magnification effect to child elements when hovered.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Effects/Dock Magnifier"), HelpURL(Constants.HelpUrl + "effects/dock-magnifier")]
    public class DockMagnifier : UIBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        // Scale
        [Min(1f)] public float maxScale = 1.5f;
        [Min(0.01f)] public float baseScale = 1f;
        [Tooltip("Determines which axes are scaled during magnification.")]
        public Orientation scaleDirection = Orientation.Free;

        // Magnification
        [Tooltip("Pixels in parent local space.")]
        [Min(1f)] public float influenceRadius = 120f;
        [Tooltip("Widens influence along the main axis.")]
        [Min(0.01f)] public float axisBias = 2f;
        [Range(0.001f, 1f)] public float smoothTime = 0.05f;

        // Behavior
        [Tooltip("While hovering, refresh the current pointer position every frame, including during scrolling.")]
        public bool alwaysUpdateOnHover = true;
        [Tooltip("Only refresh the pointer while it is inside this RectTransform.")]
        public bool requirePointerInsideForRefresh = true;
        public Orientation orientation = Orientation.Horizontal;

        // Restriction
        [Tooltip("Excludes these children from magnification. Call RefreshTargets after changing this list at runtime.")]
        public List<RectTransform> ignoredRects = new();

        // Cache
        RectTransform parentRect;
        Camera eventCamera;
        ReorderableList reorderableList;

        // State
        bool isHovering;
        bool targetsDirty;
        Vector2 lastPointerLocal;
        Orientation appliedScaleDirection;
        readonly List<ChildState> states = new();

        struct ChildState
        {
            public RectTransform rt;
            public Vector2 baseSize;
            public Vector2 vel;
        }

        public enum Orientation
        {
            Horizontal,
            Vertical,
            Free
        }

        protected override void Awake()
        {
            base.Awake();

            parentRect = transform as RectTransform;
            appliedScaleDirection = scaleDirection;

            if (TryGetComponent(out reorderableList))
                reorderableList.DockMagnifier = this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            CacheEventCamera();
            RefreshTargets();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            isHovering = false;
            ResetSizes(Mathf.Max(0.01f, baseScale));
        }

        protected override void OnDestroy()
        {
            if (reorderableList != null && reorderableList.DockMagnifier == this)
                reorderableList.DockMagnifier = null;
           
            states.Clear();
            base.OnDestroy();
        }

        void Update()
        {
            if (targetsDirty)
                RefreshTargets();

            UpdateScaleDirection();

            float deltaTime = Time.deltaTime;
            if (states.Count == 0 || deltaTime <= 0f)
                return;

            bool isReordering = reorderableList != null && (reorderableList.IsDraggingActive || reorderableList.IsAnimating);
            bool magnify = isHovering && !isReordering;
            if (magnify && alwaysUpdateOnHover)
                TryRefreshPointer();

            bool scaleW = scaleDirection != Orientation.Vertical;
            bool scaleH = scaleDirection != Orientation.Horizontal;
            float restingScale = Mathf.Max(0.01f, baseScale);
            float peakScale = Mathf.Max(restingScale, maxScale);
            float sigma = Mathf.Max(1f, influenceRadius * 0.5f);
            float falloff = 1f / (2f * sigma * sigma);
            float inverseBias = 1f / Mathf.Max(0.01f, axisBias);
            bool changed = false;

            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];

                // Detached drag items keep their baseline, but are controlled by ReorderableList
                if (!st.rt || st.rt.parent != parentRect || !st.rt.gameObject.activeInHierarchy)
                    continue;

                Rect rect = st.rt.rect;
                float targetScale = restingScale;
             
                if (magnify)
                {
                    Vector2 center = parentRect.InverseTransformPoint(st.rt.TransformPoint(rect.center));
                    Vector2 delta = center - lastPointerLocal;
                 
                    if (orientation == Orientation.Horizontal)
                        delta.x *= inverseBias;
                    else if (orientation == Orientation.Vertical)
                        delta.y *= inverseBias;

                    float weight = Mathf.Exp(-delta.sqrMagnitude * falloff);
                    targetScale = Mathf.Lerp(restingScale, peakScale, weight);
                }

                Vector2 current = rect.size;
               
                if (scaleW)
                {
                    changed |= SmoothSize(st.rt, current.x, st.baseSize.x * targetScale, 
                        ref st.vel.x, RectTransform.Axis.Horizontal, deltaTime);
                }

                if (scaleH)
                {
                    changed |= SmoothSize(st.rt, current.y, st.baseSize.y * targetScale, 
                        ref st.vel.y, RectTransform.Axis.Vertical, deltaTime);
                }
               
                states[i] = st;
            }

            if (changed)
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            CacheEventCamera();
        }

        void OnTransformChildrenChanged() => targetsDirty = true;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsActive())
                return;

            eventCamera = eventData.enterEventCamera;
            isHovering = RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, 
                eventCamera, out lastPointerLocal);
        }

        public void OnPointerExit(PointerEventData eventData) => isHovering = false;

        public void OnPointerMove(PointerEventData eventData)
        {
            if (IsActive() && isHovering && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out var local))
            {
                lastPointerLocal = local;
            }
        }

        void CacheEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas = canvas.rootCanvas;

            eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        bool SmoothSize(RectTransform rt, float current, float target, ref float velocity, RectTransform.Axis axis, float deltaTime)
        {
            target = Mathf.Max(0.0001f, target);
            float next = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, deltaTime);
            if (Mathf.Abs(next - target) < 0.001f)
            {
                next = target;
                velocity = 0f;
            }

            if (next == current)
                return false;

            rt.SetSizeWithCurrentAnchors(axis, next);
            return true;
        }

        void UpdateScaleDirection()
        {
            if (appliedScaleDirection == scaleDirection)
                return;

            ResetSizes(1f);
            appliedScaleDirection = scaleDirection;
        }

        void ResetSizes(float scale)
        {
            bool changed = false;

            for (int i = 0; i < states.Count; i++)
            {
                var st = states[i];
                if (st.rt && st.rt.parent == parentRect)
                    changed |= SetStateSize(st, scale);

                st.vel = Vector2.zero;
                states[i] = st;
            }

            if (changed)
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }

        bool SetStateSize(ChildState st, float scale)
        {
            Vector2 current = st.rt.rect.size;
            float width = Mathf.Max(0.0001f, st.baseSize.x * scale);
            float height = Mathf.Max(0.0001f, st.baseSize.y * scale);
            bool changeW = appliedScaleDirection != Orientation.Vertical && current.x != width;
            bool changeH = appliedScaleDirection != Orientation.Horizontal && current.y != height;
           
            if (changeW)
                st.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (changeH)
                st.rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
          
            return changeW || changeH;
        }

        void TryRefreshPointer()
        {
            Vector2 screenPos = Utilities.GetPointerPosition();
           
            if (requirePointerInsideForRefresh && !RectTransformUtility.RectangleContainsScreenPoint(parentRect, screenPos, eventCamera))
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, eventCamera, out var local))
                lastPointerLocal = local;
        }

        int FindState(RectTransform rt)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].rt == rt)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Rescans children, preserving existing baselines. Call after changing ignoredRects at runtime.
        /// </summary>
        public void RefreshTargets()
        {
            if (!parentRect)
                return;

            targetsDirty = false;
            UpdateScaleDirection();
            bool changed = false;

            for (int i = states.Count - 1; i >= 0; i--)
            {
                var st = states[i];
                bool ignored = st.rt && ignoredRects != null && ignoredRects.Contains(st.rt);
                if (st.rt && !ignored)
                {
                    if (st.rt.parent == parentRect)
                        continue;
                    if (reorderableList != null && st.rt.TryGetComponent<ReorderableListItem>(out var item) 
                        && reorderableList.IsDragging(item))
                        continue;
                }

                // Restore the unscaled baseline before forgetting an item to avoid cumulative scaling on re-add
                if (st.rt)
                    changed |= SetStateSize(st, 1f);

                states.RemoveAt(i);
            }

            for (int i = 0; i < parentRect.childCount; i++)
            {
                if (parentRect.GetChild(i) is not RectTransform child || (ignoredRects != null && ignoredRects.Contains(child))
                    || FindState(child) >= 0 || child.TryGetComponent<ReorderableListPlaceholder>(out _))
                    continue;

                var st = new ChildState { rt = child, baseSize = child.rect.size };
                states.Add(st);
                changed |= SetStateSize(st, Mathf.Max(0.01f, baseScale));
            }

            if (changed)
                LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        }

        /// <summary>
        /// Returns the resting size on the scaled axes and the current size on untouched axes.
        /// Used by ReorderableList to calculate slots while items are magnified.
        /// </summary>
        public Vector2 GetRestingSize(RectTransform rt)
        {
            if (!rt)
                return Vector2.zero;

            Vector2 size = rt.rect.size;
            int index = FindState(rt);
            if (index < 0)
                return size;

            Vector2 resting = states[index].baseSize * Mathf.Max(0.01f, baseScale);
            if (scaleDirection != Orientation.Vertical)
                size.x = Mathf.Max(0.0001f, resting.x);
            if (scaleDirection != Orientation.Horizontal)
                size.y = Mathf.Max(0.0001f, resting.y);
        
            return size;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            baseScale = Mathf.Max(0.01f, baseScale);
            maxScale = Mathf.Max(baseScale, maxScale);
            influenceRadius = Mathf.Max(1f, influenceRadius);
            axisBias = Mathf.Max(0.01f, axisBias);
            smoothTime = Mathf.Clamp(smoothTime, 0.001f, 1f);
            targetsDirty = true;
        }
#endif
    }
}