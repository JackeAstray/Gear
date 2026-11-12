using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Rect Dragger"), HelpURL(Constants.HelpUrl + "layout/rect-dragger")]
    public class RectDragger : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // Settings
        public bool isDraggable = true;
        public AxisConstraint axisConstraint = AxisConstraint.Free;

        // Boundary
        public BoundaryType boundaryType = BoundaryType.ScreenBounds;
        public RectTransform boundaryRect;

        // Smooth Return
        public bool allowOutOfBounds = false;
        public bool useUnscaledTime = true;
        [Min(0f)] public float returnDuration = 0.25f;
        public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Multiple Sources
        [Tooltip("Add RectTransforms here to trigger the drag from specific places. If empty, the whole object is draggable.")]
        public List<RectTransform> dragSources = new();

        // Events
        public Action<Vector2> OnDragUpdate;
        public Action OnDragEnd;

        // Enums
        public enum BoundaryType { NoBounds, ScreenBounds, RectBounds }
        public enum AxisConstraint { Free, HorizontalOnly, VerticalOnly }

        // Cache
        RectTransform rectTransform;
        RectTransform canvasRect;
        DragListener[] dragListeners;
        TweenHandle returnTween;
        Action<float> returnUpdate;
        Action returnCompleted;
        readonly Vector3[] cachedCorners = new Vector3[4];

        // State
        bool isDragging;
        int activePointerId;
        DragListener activeSource;
        Vector2 pointerStartPosition;
        Vector2 dragStartPosition;
        Vector2 originalPosition;
        Vector2 returnStartPosition;
        Vector2 returnTargetPosition;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalPosition = rectTransform.anchoredPosition;
            returnUpdate = UpdateReturn;
            returnCompleted = CompleteReturn;
            CacheCanvas();
        }

        void Start()
        {
            originalPosition = rectTransform.anchoredPosition;

            if (dragSources == null || dragSources.Count == 0)
                return;

            dragListeners = new DragListener[dragSources.Count];
            for (int i = 0; i < dragSources.Count; i++)
            {
                RectTransform source = dragSources[i];
                if (source == null || source == rectTransform || dragSources.IndexOf(source) != i)
                    continue;

                DragListener listener = source.gameObject.AddComponent<DragListener>();
                listener.Initialize(this);
                dragListeners[i] = listener;
            }
        }

        void OnEnable() => SetListenersEnabled(true);

        void OnDisable()
        {
            isDragging = false;
            activeSource = null;
            returnTween.Kill();
            SetListenersEnabled(false);
        }

        void OnDestroy()
        {
            if (dragListeners == null)
                return;

            foreach (var listener in dragListeners)
            {
                if (listener == null)
                    continue;

                listener.Initialize(null);
                Destroy(listener);
            }
        }

        void OnTransformParentChanged()
        {
            isDragging = false;
            activeSource = null;
            returnTween.Kill();
            CacheCanvas();
        }

        void CacheCanvas()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }

        void SetListenersEnabled(bool value)
        {
            if (dragListeners == null)
                return;

            foreach (var listener in dragListeners)
            {
                if (listener != null)
                    listener.enabled = value;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Only allow direct dragging when this object is one of the configured sources
            if (dragSources != null && dragSources.Count > 0 && !dragSources.Contains(rectTransform))
                return;

            HandlePointerDown(eventData);
        }

        public void OnDrag(PointerEventData eventData) => HandleDrag(eventData);

        public void OnPointerUp(PointerEventData eventData) => HandlePointerUp(eventData);

        public void HandlePointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !isDraggable || isDragging || eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!GetPointerPosition(eventData, out pointerStartPosition))
                return;

            returnTween.Kill();
            isDragging = true;
            activePointerId = eventData.pointerId;
            activeSource = null;
            dragStartPosition = rectTransform.anchoredPosition;
        }

        public void HandleDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !isDraggable || !isDragging || eventData == null
                || eventData.pointerId != activePointerId || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (!GetPointerPosition(eventData, out Vector2 pointerPosition))
                return;

            // Keep the original grab offset when the pointer moves beyond the boundary.
            Vector2 newPosition = dragStartPosition + ApplyAxisConstraint(pointerPosition - pointerStartPosition);
            if (!allowOutOfBounds)
                newPosition = ConstrainToBounds(newPosition);

            rectTransform.anchoredPosition = newPosition;
            OnDragUpdate?.Invoke(newPosition);
        }

        public void HandlePointerUp(PointerEventData eventData)
        {
            if (!isDragging || (eventData != null && (eventData.pointerId != activePointerId
                || eventData.button != PointerEventData.InputButton.Left)))
                return;

            isDragging = false;
            activeSource = null;
            Vector2 constrainedPosition = ConstrainToBounds(rectTransform.anchoredPosition);

            if (allowOutOfBounds && (rectTransform.anchoredPosition - constrainedPosition).sqrMagnitude > 0.01f)
            {
                ReturnToPosition(constrainedPosition);
                return;
            }

            rectTransform.anchoredPosition = constrainedPosition;
            OnDragEnd?.Invoke();
        }

        bool GetPointerPosition(PointerEventData eventData, out Vector2 position)
        {
            Transform parent = rectTransform.parent;
            RectTransform referenceRect = parent as RectTransform;
            if (referenceRect == null) { referenceRect = rectTransform; }

            bool success = RectTransformUtility.ScreenPointToWorldPointInRectangle(
                referenceRect, eventData.position, eventData.pressEventCamera, out Vector3 worldPosition);

            position = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
            return success;
        }

        Vector2 ApplyAxisConstraint(Vector2 delta)
        {
            return axisConstraint switch
            {
                AxisConstraint.HorizontalOnly => new Vector2(delta.x, 0f),
                AxisConstraint.VerticalOnly => new Vector2(0f, delta.y),
                _ => delta,
            };
        }

        Vector2 ConstrainToBounds(Vector2 position)
        {
            RectTransform bounds = boundaryType switch
            {
                BoundaryType.ScreenBounds => canvasRect,
                BoundaryType.RectBounds => boundaryRect != null ? boundaryRect : rectTransform.parent as RectTransform,
                _ => null,
            };

            if (bounds == null || bounds == rectTransform || bounds.IsChildOf(rectTransform))
                return position;

            Transform parent = rectTransform.parent;
            Vector3 delta = position - rectTransform.anchoredPosition;
            if (parent != null)
                delta = parent.TransformVector(delta);

            delta = bounds.InverseTransformVector(delta);

            // Compare all four corners in boundary space without moving the transform to test it
            rectTransform.GetWorldCorners(cachedCorners);
            Vector2 min = bounds.InverseTransformPoint(cachedCorners[0]) + delta;
            Vector2 max = min;
            for (int i = 1; i < cachedCorners.Length; i++)
            {
                Vector2 corner = bounds.InverseTransformPoint(cachedCorners[i]) + delta;
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }

            Rect boundary = bounds.rect;
            Vector2 minOffset = boundary.min - min;
            Vector2 maxOffset = boundary.max - max;
            Vector3 offset = new(
                minOffset.x <= maxOffset.x ? Mathf.Clamp(0f, minOffset.x, maxOffset.x) : (minOffset.x + maxOffset.x) * 0.5f,
                minOffset.y <= maxOffset.y ? Mathf.Clamp(0f, minOffset.y, maxOffset.y) : (minOffset.y + maxOffset.y) * 0.5f,
                0f
            );

            offset = bounds.TransformVector(offset);
            if (parent != null)
                offset = parent.InverseTransformVector(offset);

            return position + (Vector2)offset;
        }

        void ReturnToPosition(Vector2 targetPosition, bool animate = true)
        {
            returnTween.Kill();
            returnStartPosition = rectTransform.anchoredPosition;
            returnTargetPosition = targetPosition;

            if (!animate || !isActiveAndEnabled || returnDuration <= 0f || returnStartPosition == targetPosition)
            {
                rectTransform.anchoredPosition = targetPosition;
                OnDragUpdate?.Invoke(targetPosition);
                OnDragEnd?.Invoke();
                return;
            }

            returnTween = EvoTween.Create(0f, 1f, returnDuration)
                .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                .OnComplete(returnCompleted)
                .Bind(returnUpdate);
        }

        void UpdateReturn(float progress)
        {
            // Binding applies the initial value before the handle is assigned
            if (progress <= 0f)
                return;

            float curveValue = returnCurve != null ? returnCurve.Evaluate(progress) : progress;
            Vector2 position = progress >= 1f ? returnTargetPosition : Vector2.Lerp(returnStartPosition, returnTargetPosition, curveValue);
            rectTransform.anchoredPosition = position;
            OnDragUpdate?.Invoke(position);
        }

        void CompleteReturn()
        {
            returnTween = default;
            OnDragEnd?.Invoke();
        }

        /// <summary>
        /// Instantly or smoothly resets the transform back to the position it occupied when Start() was called.
        /// </summary>
        public void ResetToOriginalPosition()
        {
            isDragging = false;
            activeSource = null;
            ReturnToPosition(originalPosition, allowOutOfBounds && boundaryType != BoundaryType.NoBounds);
        }

        /// <summary>
        /// Stop dragging process manually.
        /// </summary>
        public void StopDrag() => HandlePointerUp(null);

        /// <summary>
        /// Helper class to forward events.
        /// </summary>
        class DragListener : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            RectDragger dragger;

            public void Initialize(RectDragger srcDragger) => dragger = srcDragger;

            void OnDisable()
            {
                if (dragger != null && dragger.activeSource == this)
                    dragger.StopDrag();
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (dragger == null || dragger.isDragging)
                    return;

                dragger.HandlePointerDown(eventData);

                if (dragger.isDragging)
                    dragger.activeSource = this;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (dragger != null)
                    dragger.HandleDrag(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (dragger != null)
                    dragger.HandlePointerUp(eventData);
            }
        }
    }
}