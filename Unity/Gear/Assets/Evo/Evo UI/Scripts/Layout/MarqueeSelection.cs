using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Layout/Marquee Selection"), HelpURL(Constants.HelpUrl)]
    public partial class MarqueeSelection : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        // Selection
        [Tooltip("Items must be below this transform. Defaults to this container. Use a scroll view's content here.")]
        public RectTransform content;
        public bool allowDrag = true;
        public bool allowModifiers = true;
        public HitTest hitTest = HitTest.Overlap;

        // Movement
        [Tooltip("Dragged items draw above the rest of the UI while they move, and masks don't clip them.")]
        public bool bringToFront = true;
        public bool constrainMovement = true;
        [Tooltip("Movement boundary. Defaults to Content. A transferable group may leave it during a drag.")]
        public RectTransform movementBounds;
        public TransferMode transferMode = TransferMode.None;
        public MarqueeSelection[] allowedTargets = Array.Empty<MarqueeSelection>();
        public bool acceptTransfers = true;

        // Marquee Visual
        [Tooltip("Optional dedicated child shape. Its fill, outline, corners and dashes are configured on the shape itself. Created automatically when empty.")]
        [SerializeField] private ProceduralShape marqueeShape;
        [Tooltip("Dash repeats per second. Zero stops the animation; negative values reverse it.")]
        public float dashSpeed = 0.75f;
        [Min(0f)] public float fadeDuration = 0.1f;

        // Events
        public UnityEvent onSelectionChanged = new();
        public UnityEvent onDragStarted = new();
        public UnityEvent onDragEnded = new();
        public event Action<MarqueeSelection> OnTransferred;

        // Enums
        public enum HitTest { Overlap, Contained }
        public enum TransferMode { None, AnyContainer, AllowedTargets }
        public enum SelectionMode { Replace, Add, Toggle }
        enum Gesture { Idle, Pending, Marquee, Drag }

        // Public Properties
        public IReadOnlyList<MarqueeSelectable> Selection => selection;
        public int SelectionCount => selection.Count;
        public bool IsSelecting => gesture == Gesture.Marquee;
        public bool IsDragging => gesture == Gesture.Drag;
        public RectTransform Content => content != null ? content : RectTransform;
        public RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;
        public static MarqueeSelection ActiveContainer { get; private set; }

        // Selection Cache
        readonly List<MarqueeSelectable> selectables = new(64);
        readonly List<MarqueeSelectable> selection = new(64);
        readonly List<MarqueeSelectable> candidates = new(64);
        readonly HashSet<MarqueeSelectable> initialSelection = new(64);
        readonly Vector3[] corners = new Vector3[4];
        readonly List<MonoBehaviour> dragHandlers = new(8);
        bool selectionDirty;

        // Pointer State
        RectTransform rectTransform;
        Canvas rootCanvas;
        Gesture gesture;
        SelectionMode selectionMode;
        PointerEventData pointer;
        MarqueeSelectable pressedItem;
        Vector2 pointerStart;
        bool pressedSelected;
        bool finishing;

        // Visual Cache
        CanvasGroup marqueeGroup;
        bool ownsShape;
        TweenHandle fadeTween;
        TweenHandle dashTween;
        Action<float> updateFade;
        Action<float> updateDash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => ActiveContainer = null;

        void Awake()
        {
            rectTransform = (RectTransform)transform;
            updateFade = UpdateFade;
            updateDash = UpdateDash;
            RaycastArea.ResolveRaycast(gameObject);
            CacheCanvas();
        }

        void Start() => PrepareVisual();

        void OnDisable()
        {
            Cancel();
            ClearSelection();
            HideMarquee(true);
            FlushSelectionChanged();
        }

        void OnDestroy()
        {
            fadeTween.Kill();
            dashTween.Kill();

            if (ownsShape && marqueeShape != null)
                Destroy(marqueeShape.gameObject);
        }

        void OnTransformParentChanged()
        {
            Cancel();
            CacheCanvas();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Cancel();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                Cancel();
        }

        void LateUpdate()
        {
            if (gesture != Gesture.Idle && (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled
                || pointer == null || (pressedItem != null && !pressedItem.Interactable) || Utilities.WasEscapeKeyPressed()))
                Cancel();
           
            FlushSelectionChanged();
        }

        void CacheCanvas()
        {
            Canvas parent = GetComponentInParent<Canvas>();
            rootCanvas = parent != null ? parent.rootCanvas : null;
        }

        #region Selection
        internal void Register(MarqueeSelectable item)
        {
            if (!selectables.Contains(item))
                selectables.Add(item);

            // Grow buffers at registration, never while scanning a marquee or starting a group drag
            int capacity = Mathf.Max(64, selectables.Count);
            if (selection.Capacity < capacity)
                selection.Capacity = capacity * 2;
            if (candidates.Capacity < capacity)
                candidates.Capacity = capacity * 2;
            if (dragItems.Capacity < capacity)
                dragItems.Capacity = capacity * 2;

            initialSelection.EnsureCapacity(capacity);
        }

        internal void Unregister(MarqueeSelectable item)
        {
            if (!finishing && (item == pressedItem || item.IsDragging))
                Cancel();

            Deselect(item);
            selectables.Remove(item);
            initialSelection.Remove(item);
        }

        bool Owns(MarqueeSelectable item) => item != null && item.Container == this
            && item.RectTransform != Content && item.RectTransform.IsChildOf(Content);

        public void Select(MarqueeSelectable item)
        {
            if (!isActiveAndEnabled || !Owns(item) || !item.Interactable || item.IsSelected)
                return;

            item.selectionIndex = selection.Count;
            selection.Add(item);
            selectionDirty = true;
            item.SelectionChanged(true);
        }

        public void Deselect(MarqueeSelectable item)
        {
            if (!finishing && item != null && item.IsDragging)
                Cancel();
            if (item == null || item.Container != this || !item.IsSelected)
                return;

            int index = item.selectionIndex;
            int last = selection.Count - 1;
            selection[index] = selection[last];
            selection[index].selectionIndex = index;
            selection.RemoveAt(last);
            item.selectionIndex = -1;
            selectionDirty = true;
            item.SelectionChanged(false);
        }

        public void ClearSelection()
        {
            if (IsDragging)
                Cancel();

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                if (i < selection.Count)
                    Deselect(selection[i]);
            }
        }

        public void SelectAll()
        {
            for (int i = selectables.Count - 1; i >= 0; i--)
            {
                if (i < selectables.Count)
                    Select(selectables[i]);
            }
        }

        void SetSelected(MarqueeSelectable item, bool selected)
        {
            if (selected) { Select(item); }
            else { Deselect(item); }
        }

        void FlushSelectionChanged()
        {
            if (!selectionDirty)
                return;

            selectionDirty = false;
            onSelectionChanged?.Invoke();
        }
        #endregion

        #region Pointer Events
        public void OnPointerDown(PointerEventData eventData) => BeginPointer(eventData, null);

        internal void BeginPointer(PointerEventData eventData, MarqueeSelectable item)
        {
            if (!isActiveAndEnabled || finishing || gesture != Gesture.Idle || rootCanvas == null
                || (ActiveContainer != null && ActiveContainer != this) || eventData == null
                || eventData.button != PointerEventData.InputButton.Left || (item != null && !Owns(item)))
                return;

            // A child control can own a click/drag even when it has no pointer-down handler
            GameObject hit = eventData.pointerPressRaycast.gameObject;
            GameObject source = item != null ? item.gameObject : gameObject;
            if (HasChildHandler<IPointerClickHandler>(hit, source) || HasChildHandler<IDragHandler>(hit, source))
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, eventData.position,
                eventData.pressEventCamera, out pointerStart))
                return;

            pointerStart = ClampPoint(pointerStart);
            pointer = eventData;
            pressedItem = item;
            pressedSelected = item != null && item.IsSelected;
            selectionMode = allowModifiers ? ReadSelectionMode() : SelectionMode.Replace;
            initialSelection.Clear();
           
            for (int i = 0; i < selection.Count; i++)
                initialSelection.Add(selection[i]);

            candidates.Clear();
            candidates.AddRange(selectables);
            gesture = Gesture.Pending;
            ActiveContainer = this;

            if (item == null)
                return;

            if (selectionMode == SelectionMode.Toggle) { SetSelected(item, !pressedSelected); }
            else
            {
                if (!pressedSelected && selectionMode == SelectionMode.Replace)
                    ClearSelection();

                Select(item);
            }
        }

        static bool HasChildHandler<T>(GameObject hit, GameObject source) where T : IEventSystemHandler
        {
            GameObject handler = ExecuteEvents.GetEventHandler<T>(hit);
            return handler != null && handler != source && handler.transform.IsChildOf(source.transform);
        }

        bool Matches(PointerEventData eventData) => !finishing && gesture != Gesture.Idle && eventData != null
            && pointer != null && eventData.pointerId == pointer.pointerId && eventData.button == PointerEventData.InputButton.Left;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Matches(eventData) || gesture != Gesture.Pending)
                return;

            MonoBehaviour source = pressedItem != null ? pressedItem : this;
            source.GetComponents(dragHandlers);
            bool sharedDrag = false;
          
            for (int i = 0; i < dragHandlers.Count; i++)
            {
                MonoBehaviour handler = dragHandlers[i];
                if (handler != source && handler != null && handler.isActiveAndEnabled && handler is IDragHandler)
                    sharedDrag = true;
            }

            dragHandlers.Clear();

            if (sharedDrag)
                return;

            if (pressedItem == null)
            {
                gesture = Gesture.Marquee;
                eventData.eligibleForClick = false;
                ShowMarquee();
            }
            else if (allowDrag && pressedItem.IsSelected && pressedItem.draggable && CaptureDrag())
            {
                gesture = Gesture.Drag;
                eventData.eligibleForClick = false;
                onDragStarted?.Invoke();
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!Matches(eventData))
                return;

            if (gesture == Gesture.Marquee) { UpdateMarquee(eventData); }
            else if (gesture == Gesture.Drag) { MoveDrag(eventData); }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!Matches(eventData) || gesture != Gesture.Pending)
                return;
           
            if (pressedItem == null && selectionMode == SelectionMode.Replace) { ClearSelection(); }
            else if (pressedItem != null && selectionMode == SelectionMode.Replace && pressedItem.Interactable)
            {
                for (int i = selection.Count - 1; i >= 0; i--)
                {
                    if (i < selection.Count && selection[i] != pressedItem)
                        Deselect(selection[i]);
                }

                Select(pressedItem);
            }

            ReleasePointer();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!Matches(eventData))
                return;

            if (gesture == Gesture.Drag)
            {
                MoveDrag(eventData);
                FinishDrag(eventData);
            }
            else if (gesture == Gesture.Marquee)
                UpdateMarquee(eventData);

            HideMarquee(false);
            ReleasePointer();
        }

        public void OnCancel(BaseEventData eventData) => Cancel();

        public void Cancel()
        {
            if (gesture == Gesture.Idle || finishing)
                return;

            bool wasDragging = IsDragging;
            finishing = true;
            RestoreDrag();
          
            for (int i = 0; i < candidates.Count; i++)
            {
                MarqueeSelectable item = candidates[i];
                if (Owns(item))
                    SetSelected(item, initialSelection.Contains(item));
            }

            HideMarquee(true);
            ReleasePointer();
            finishing = false;

            if (wasDragging)
                onDragEnded?.Invoke();
        }

        void ReleasePointer()
        {
            gesture = Gesture.Idle;
            pointer = null;
            pressedItem = null;
            initialSelection.Clear();
            candidates.Clear();

            if (ActiveContainer == this)
                ActiveContainer = null;
        }

        static SelectionMode ReadSelectionMode()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.ctrlKey.isPressed || keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed)
                    return SelectionMode.Toggle;
                if (keyboard.shiftKey.isPressed)
                    return SelectionMode.Add;
            }
            return SelectionMode.Replace;
        }
        #endregion

        #region Marquee
        Vector2 ClampPoint(Vector2 point)
        {
            Rect rect = RectTransform.rect;
            return new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        void UpdateMarquee(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, eventData.position,
                eventData.pressEventCamera, out Vector2 point))
                return;

            point = ClampPoint(point);
            Vector2 min = Vector2.Min(pointerStart, point);
            Vector2 max = Vector2.Max(pointerStart, point);
            Rect area = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
   
            if (marqueeGroup != null && marqueeShape != null)
            {
                marqueeShape.rectTransform.localPosition = (min + max) * 0.5f;
                marqueeShape.rectTransform.sizeDelta = max - min;
            }

            for (int i = 0; i < candidates.Count && gesture == Gesture.Marquee; i++)
            {
                MarqueeSelectable item = candidates[i];
                if (!Owns(item))
                    continue;

                bool inside = item.Interactable && Intersects(item.RectTransform, area);
                bool initial = initialSelection.Contains(item);
                bool selected = selectionMode == SelectionMode.Add ? initial || inside
                    : selectionMode == SelectionMode.Toggle ? initial != inside : inside;
                SetSelected(item, selected);
            }
        }

        bool Intersects(RectTransform item, Rect area)
        {
            item.GetWorldCorners(corners);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                corners[i] = RectTransform.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, corners[i]);
                max = Vector2.Max(max, corners[i]);
            }
           
            if (hitTest == HitTest.Contained)
                return min.x >= area.xMin && min.y >= area.yMin && max.x <= area.xMax && max.y <= area.yMax;
            if (max.x < area.xMin || min.x > area.xMax || max.y < area.yMin || min.y > area.yMax)
                return false;

            // Separating axes of the item's edges reject empty corners of rotated UI bounds
            for (int i = 0; i < 2; i++)
            {
                Vector2 edge = corners[i + 1] - corners[i];
                Vector2 axis = new(-edge.y, edge.x);
                float low = float.MaxValue;
                float high = float.MinValue;
                for (int j = 0; j < 4; j++)
                {
                    float dot = Vector2.Dot(corners[j], axis);
                    low = Mathf.Min(low, dot);
                    high = Mathf.Max(high, dot);
                }

                float center = Vector2.Dot(area.center, axis);
                float extent = Mathf.Abs(axis.x) * area.width * 0.5f + Mathf.Abs(axis.y) * area.height * 0.5f;
                if (high < center - extent || low > center + extent)
                    return false;
            }
            return true;
        }

        void PrepareVisual()
        {
            if (marqueeGroup != null)
                return;

            if (marqueeShape == null)
            {
                GameObject visual = new("Marquee", typeof(RectTransform));
                visual.transform.SetParent(RectTransform, false);
                visual.AddComponent<LayoutElement>().ignoreLayout = true;
                marqueeShape = visual.AddComponent<ProceduralShape>();
                marqueeShape.fillColorMode = ProceduralShape.ColorMode.Custom;
                marqueeShape.FillColor = new Color(0.22f, 0.56f, 1f, 0.12f);
                marqueeShape.OutlineColor = new Color(0.35f, 0.68f, 1f, 0.95f);
                marqueeShape.OutlineWidth = 1.5f;
                marqueeShape.OutlineDashed = true;
                marqueeShape.OutlineDashLength = 7f;
                marqueeShape.OutlineDashGap = 4f;
                marqueeShape.UniformCornerRadius = 3f;
                ownsShape = true;
            }

            if (marqueeShape.transform.parent != transform || marqueeShape.transform == transform)
                return;

            marqueeShape.raycastTarget = false;
            marqueeShape.rectTransform.anchorMin = marqueeShape.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marqueeShape.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marqueeShape.rectTransform.localRotation = Quaternion.identity;
            marqueeShape.rectTransform.localScale = Vector3.one;
          
            if (!marqueeShape.TryGetComponent(out marqueeGroup))
                marqueeGroup = marqueeShape.gameObject.AddComponent<CanvasGroup>();

            marqueeGroup.blocksRaycasts = false;
            marqueeGroup.alpha = 0f;
        }

        void ShowMarquee()
        {
            PrepareVisual();

            if (marqueeGroup == null)
                return;

            fadeTween.Kill();
            dashTween.Kill();
            marqueeShape.gameObject.SetActive(true);
            marqueeShape.transform.SetAsLastSibling();
            marqueeGroup.alpha = 1f;
           
            if (marqueeShape.OutlineDashed && Mathf.Abs(dashSpeed) > 0.001f && Application.isPlaying)
            {
                dashTween = EvoTween.Create(0f, Mathf.Sign(dashSpeed), 1f / Mathf.Abs(dashSpeed))
                    .WithTimeMode(TimeMode.Unscaled).WithLoops(0).Bind(updateDash);
            }
        }

        void HideMarquee(bool immediate)
        {
            fadeTween.Kill();
            dashTween.Kill();

            if (marqueeGroup == null)
                return;

            if (immediate || fadeDuration <= 0f || !isActiveAndEnabled || !Application.isPlaying)
                UpdateFade(0f);
            else
                fadeTween = EvoTween.Create(marqueeGroup.alpha, 0f, fadeDuration).WithTimeMode(TimeMode.Unscaled).Bind(updateFade);
        }

        void UpdateFade(float alpha)
        {
            if (marqueeGroup != null)
                marqueeGroup.alpha = alpha;
        }

        void UpdateDash(float offset)
        {
            if (marqueeShape != null)
                marqueeShape.OutlineDashOffset = offset;
        }
        #endregion

        #region Drag
        readonly List<DragItem> dragItems = new(64);
        readonly List<RaycastResult> raycasts = new(64);
        static readonly IComparer<DragItem> DragOrder = Comparer<DragItem>.Create(CompareSiblingOrder);

        struct DragItem
        {
            public MarqueeSelectable item;
            public Transform parent;
            public Vector3 position;
            public Vector3 worldPosition;
            public int siblingIndex;
            public Canvas canvas;
            public bool ownsCanvas;
            public bool overrideSorting;
            public int sortingOrder;
            public int sortingLayerID;
        }

        bool CaptureDrag()
        {
            dragItems.Clear();
            for (int i = 0; i < selection.Count; i++)
            {
                MarqueeSelectable item = selection[i];
                if (!Owns(item) || !item.Interactable || !item.draggable)
                    continue;

                // A selected parent carries its descendants; never translate them twice
                Transform parent = item.transform.parent;
                bool carried = false;
                while (parent != null && parent != Content)
                {
                    if (parent.TryGetComponent<MarqueeSelectable>(out var ancestor) && ancestor.IsSelected && ancestor.draggable)
                    {
                        carried = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (carried)
                    continue;

                RectTransform rect = item.RectTransform;
                dragItems.Add(new DragItem
                {
                    item = item,
                    parent = rect.parent,
                    position = rect.anchoredPosition3D,
                    worldPosition = rect.position,
                    siblingIndex = rect.GetSiblingIndex()
                });
                item.dragPosition = rect.position;
                item.IsDragging = true;
            }

            dragItems.Sort(DragOrder);

            // Ascending orders keep the group's own stacking above the rest
            for (int i = 0; i < dragItems.Count && bringToFront; i++)
                dragItems[i] = RaiseItem(dragItems[i], short.MaxValue - dragItems.Count + 1 + i);

            return dragItems.Count > 0;
        }

        // Override sorting draws the item above other UI and scroll masks without touching its layout or hierarchy
        DragItem RaiseItem(DragItem entry, int order)
        {
            entry.ownsCanvas = !entry.item.TryGetComponent(out entry.canvas);
            if (entry.ownsCanvas)
            {
                entry.canvas = entry.item.gameObject.AddComponent<Canvas>();
                entry.canvas.additionalShaderChannels = rootCanvas.additionalShaderChannels;
            }
            else
            {
                // A nested canvas only reads and keeps its own order while it overrides sorting
                entry.overrideSorting = entry.canvas.overrideSorting;
                entry.canvas.overrideSorting = true;
                entry.sortingOrder = entry.canvas.sortingOrder;
                entry.sortingLayerID = entry.canvas.sortingLayerID;
            }

            entry.canvas.overrideSorting = true;
            entry.canvas.sortingLayerID = rootCanvas.sortingLayerID;
            entry.canvas.sortingOrder = order;
            return entry;
        }

        static int CompareSiblingOrder(DragItem a, DragItem b)
        {
            if (a.parent == b.parent)
                return a.siblingIndex.CompareTo(b.siblingIndex);

#if UNITY_6000_3_OR_NEWER
            // Group by parent identity; the order does not represent creation order
            return a.parent.GetEntityId().CompareTo(b.parent.GetEntityId());
#else
            return a.parent.GetInstanceID().CompareTo(b.parent.GetInstanceID());
#endif
        }

        void MoveDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, eventData.position,
                eventData.pressEventCamera, out Vector2 point))
                return;

            Vector3 delta = RectTransform.TransformVector(point - pointerStart);
            for (int i = 0; i < dragItems.Count; i++)
            {
                DragItem entry = dragItems[i];
                if (entry.item == null || !entry.item.Interactable || entry.item.transform.parent != entry.parent)
                {
                    Cancel();
                    return;
                }
                entry.item.RectTransform.position = entry.worldPosition + delta;
            }

            if (transferMode == TransferMode.None && constrainMovement)
                ConstrainDrag(movementBounds != null ? movementBounds : Content);

            for (int i = 0; i < dragItems.Count; i++)
                dragItems[i].item.dragPosition = dragItems[i].item.RectTransform.position;
        }

        void ConstrainDrag(RectTransform bounds)
        {
            if (bounds == null || dragItems.Count == 0)
                return;

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
          
            for (int i = 0; i < dragItems.Count; i++)
            {
                if (dragItems[i].item == null)
                    continue;

                dragItems[i].item.RectTransform.GetWorldCorners(corners);
                for (int j = 0; j < 4; j++)
                {
                    Vector2 corner = bounds.InverseTransformPoint(corners[j]);
                    min = Vector2.Min(min, corner);
                    max = Vector2.Max(max, corner);
                }
            }

            if (min.x > max.x)
                return;

            Vector2 low = bounds.rect.min - min;
            Vector2 high = bounds.rect.max - max;
            // An oversized group is centered on that axis, preserving spacing
            Vector3 offset = bounds.TransformVector(new Vector3(
                low.x <= high.x ? Mathf.Clamp(0f, low.x, high.x) : (low.x + high.x) * 0.5f,
                low.y <= high.y ? Mathf.Clamp(0f, low.y, high.y) : (low.y + high.y) * 0.5f, 0f));
          
            for (int i = 0; i < dragItems.Count; i++)
            {
                if (dragItems[i].item != null)
                    dragItems[i].item.RectTransform.position += offset;
            }
        }

        MarqueeSelection FindDropTarget(PointerEventData eventData)
        {
            if (EventSystem.current == null)
                return null;

            raycasts.Clear();
            EventSystem.current.RaycastAll(eventData, raycasts);
            MarqueeSelection result = null;
           
            for (int i = 0; i < raycasts.Count; i++)
            {
                Transform hit = raycasts[i].gameObject.transform;
                bool dragged = false;

                for (int j = 0; j < dragItems.Count; j++)
                {
                    if (dragItems[j].item != null && hit.IsChildOf(dragItems[j].item.transform))
                    {
                        dragged = true;
                        break;
                    }
                }

                if (dragged)
                    continue;

                result = hit.GetComponentInParent<MarqueeSelection>();
                break; // Never drop through an unrelated UI panel or a rejected container
            }

            raycasts.Clear();
            return result;
        }

        public bool CanTransferTo(MarqueeSelection target)
        {
            if (target == null || target == this || !target.isActiveAndEnabled || !target.acceptTransfers
                || transferMode == TransferMode.None || target.rootCanvas != rootCanvas || rootCanvas == null
                || (target.Content != target.RectTransform && !target.Content.IsChildOf(target.RectTransform)))
                return false;
          
            if (transferMode == TransferMode.AllowedTargets && Array.IndexOf(allowedTargets ?? Array.Empty<MarqueeSelection>(), target) < 0)
                return false;
          
            // Reparenting must never create a transform cycle or move the destination with the selection
            for (int i = 0; i < selection.Count; i++)
            {
                if (target.Content.IsChildOf(selection[i].transform))
                    return false;
            }

            return true;
        }

        void FinishDrag(PointerEventData eventData)
        {
            if (!IsDragging)
                return;

            MarqueeSelection target = transferMode != TransferMode.None ? FindDropTarget(eventData) : this;
            bool transfer = CanTransferTo(target);
            if (!transfer && target != this)
            {
                Cancel();
                return;
            }

            finishing = true;

            if (transfer)
            {
                for (int i = 0; i < dragItems.Count; i++)
                {
                    MarqueeSelectable item = dragItems[i].item;
                    if (item == null)
                        continue;

                    // RectTransform.SetParent(true) preserves the visible size, orientation and group offsets
                    item.RectTransform.SetParent(target.Content, true);
                    if (item == null)
                        continue;
                   
                    item.RefreshContainer();
                    target.Select(item);
                }

                if (target.constrainMovement)
                    ConstrainDrag(target.movementBounds != null ? target.movementBounds : target.Content);
            }
            else if (constrainMovement) { ConstrainDrag(movementBounds != null ? movementBounds : Content); }

            for (int i = 0; i < dragItems.Count; i++)
                EndItemDrag(dragItems[i]);

            dragItems.Clear();
            finishing = false;
            ReleasePointer();
            onDragEnded?.Invoke();

            if (transfer)
                OnTransferred?.Invoke(target);
        }

        void RestoreDrag()
        {
            for (int i = 0; i < dragItems.Count; i++)
            {
                DragItem entry = dragItems[i];
                if (entry.item != null && entry.item.transform.parent == entry.parent)
                    entry.item.RectTransform.anchoredPosition3D = entry.position;
               
                EndItemDrag(entry);
            }
            dragItems.Clear();
        }

        static void EndItemDrag(DragItem entry)
        {
            if (entry.canvas != null)
            {
                if (entry.ownsCanvas) { Destroy(entry.canvas); }
                else
                {
                    entry.canvas.sortingOrder = entry.sortingOrder;
                    entry.canvas.sortingLayerID = entry.sortingLayerID;
                    entry.canvas.overrideSorting = entry.overrideSorting;
                }
            }

            if (entry.item != null)
            {
                entry.item.IsDragging = false;
                if (entry.item.transform.parent is RectTransform parent)
                    LayoutRebuilder.MarkLayoutForRebuild(parent);
            }

            if (entry.parent is RectTransform previous)
                LayoutRebuilder.MarkLayoutForRebuild(previous);
        }
        #endregion
    }
}