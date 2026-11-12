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
    [AddComponentMenu("Evo/UI/Layout/Page Manager"), HelpURL(Constants.HelpUrl + "layout/page-manager")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Evo.UI", null, "Pages")]
    public class PageManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // Pages
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("defaultPageIndex")]
        private int currentPageIndex = 0;
        public List<Page> pages = new();

        // References
        [Tooltip("Parent RectTransform used to size and position pages. Uses this object's RectTransform when unassigned.")]
        public RectTransform container;
        [Tooltip("Optional indicator that follows page buttons. Hidden when a page has no button.")]
        public RectTransform indicator;

        // Settings
        public bool useUnscaledTime = false;
        [Tooltip("Deactivate offscreen pages. Pages entering or leaving the view stay active during transitions and swipes.")]
        public bool disableInvisiblePages = true;
        [Tooltip("Allow new animated page selections and swipes to interrupt an active transition.")]
        public bool interruptTransitions = true;
        [SerializeField, Tooltip("Add runtime helpers to nested ScrollRects so perpendicular drags can swipe pages.")]
        [UnityEngine.Serialization.FormerlySerializedAs("autoHandleNestedScrolling")]
        private bool handleNestedScrolling = false;

        // Swipe Settings
        [Tooltip("Minimum drag distance, as a fraction of the container size, to change pages without a quick swipe.")]
        [Range(0.1f, 0.9f)] public float swipeThreshold = 0.3f;
        [Tooltip("Fraction of drag movement retained beyond the first or last page. Lower values feel firmer.")]
        [Range(0.1f, 1f)] public float elasticResistance = 0.3f;
        [Tooltip("Minimum swipe speed in local UI units per unscaled second to change pages before reaching the distance threshold.")]
        [Min(0f)] public float velocityThreshold = 500f;
        [Tooltip("Axis used to arrange pages and detect swipe gestures.")]
        public SwipeDirection swipeDirection = SwipeDirection.Horizontal;

        // Animation Settings
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f)] public float transitionDuration = 0.3f;
        [Tooltip("Extra space between pages in local UI units.")]
        [Min(0f)] public float pageSpacing = 20f;

        // Events
        public UnityEvent<int> onPageChanged = new();

        // Enums
        public enum SwipeDirection { Horizontal, Vertical }

        // Public Properties
        public int CurrentPageIndex => currentPageIndex;
        public Page CurrentPage => IsValidPage(currentPageIndex) ? pages[currentPageIndex] : null;
        public bool IsTransitioning => transitionTween.IsActive;
        public bool HandleNestedScrolling
        {
            get => handleNestedScrolling;
            set
            {
                if (handleNestedScrolling == value)
                    return;

                handleNestedScrolling = value;
                RefreshNestedScrolling();
            }
        }

        // Properties
        float ContainerSize => container == null ? 0f : swipeDirection == SwipeDirection.Horizontal ? container.rect.width : container.rect.height;
        float PageSize => Mathf.Max(0.001f, ContainerSize + Mathf.Max(0f, pageSpacing));

        // Runtime State
        bool isInitialized;
        bool isDragging;
        float currentPosition;
        TweenHandle transitionTween;
        PointerEventData dragEvent;
        int dragPageIndex;
        int dragSourcePageIndex;
        float dragStartPosition;
        float dragStartPointer;
        float dragLastPointer;
        float dragLastTime;
        float dragVelocity;

        // Cache
        Action completeTransition;
        readonly List<Page> boundPages = new();
        List<UnityEngine.UI.ScrollRect> nestedScrollRects;
        List<NestedScrollManager> nestedScrollManagers;
        Vector2 lastContainerSize;
        float lastPageSpacing;
        SwipeDirection lastSwipeDirection;
        readonly Vector3[] indicatorCorners = new Vector3[4];
        static readonly Action<PageManager, float> PositionSetter = (target, value) => target.ApplyPosition(value);
        const int PositionPropertyKey = 0x50414745;

        void Awake() => completeTransition = CompleteTransition;

        void Start()
        {
            isInitialized = true;
            PositionPages();
            ApplyPosition(currentPosition);
            onPageChanged?.Invoke(currentPageIndex);
        }

        void OnEnable()
        {
            if (isInitialized)
            {
                ApplySelection();
                RefreshNestedScrolling();
            }
        }

        void OnDisable()
        {
            if (isInitialized)
                ApplySelection();
            else
                StopTransition();

            EnableNestedScrolling(false);
        }

        void OnDestroy()
        {
            StopTransition();
            UnbindPages();
            if (nestedScrollManagers != null)
            {
                for (int i = 0; i < nestedScrollManagers.Count; i++)
                {
                    if (nestedScrollManagers[i] == null || nestedScrollManagers[i].Owner != this)
                        continue;

                    nestedScrollManagers[i].Release();
                    Destroy(nestedScrollManagers[i]);
                }
                nestedScrollManagers.Clear();
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= UpdateEditorPreview;
#endif
        }

        void LateUpdate()
        {
            if (container == null)
                return;

            if (lastContainerSize != container.rect.size || lastPageSpacing != pageSpacing || lastSwipeDirection != swipeDirection)
            {
                lastContainerSize = container.rect.size;
                lastPageSpacing = pageSpacing;
                lastSwipeDirection = swipeDirection;

                // A resize changes the pointer-to-page ratio. Finish the current gesture at its selection.
                if (isDragging)
                    ApplySelection();
                else
                    ApplyPosition(currentPosition);
            }

            if (!isDragging && !IsTransitioning)
                RefreshIndicator();
        }

        #region Swipe Gestures
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanBeginDrag(eventData) || !TryGetPointer(eventData, eventData.pressPosition, out Vector2 position))
                return;

            StopTransition();
            isDragging = true;
            dragEvent = eventData;
            dragStartPosition = currentPosition;
            dragPageIndex = Mathf.Clamp(Mathf.RoundToInt(currentPosition), 0, pages.Count - 1);

            if (!IsValidPage(dragPageIndex))
                dragPageIndex = currentPageIndex;

            dragSourcePageIndex = -1;
            if (eventData.pointerDrag != null)
            {
                for (int i = 0; i < pages.Count; i++)
                {
                    if (IsValidPage(i) && eventData.pointerDrag.transform.IsChildOf(pages[i].pageObject))
                    {
                        dragSourcePageIndex = i;
                        break;
                    }
                }
            }

            dragStartPointer = GetPointerAxis(position);
            dragLastPointer = dragStartPointer;
            dragLastTime = Time.unscaledTime;
            dragVelocity = 0f;
            ApplyPosition(currentPosition);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || dragEvent != eventData || !TryGetPointer(eventData, eventData.position, out Vector2 position))
                return;

            float pointer = GetPointerAxis(position);
            float elapsed = Time.unscaledTime - dragLastTime;
            if (elapsed > 0f)
            {
                dragVelocity = (pointer - dragLastPointer) / elapsed;
                dragLastPointer = pointer;
                dragLastTime = Time.unscaledTime;
            }

            float offset = dragStartPosition + Mathf.Clamp((pointer - dragStartPointer) / PageSize, -1f, 1f);
            float min = FindPage(0, 1);
            float max = FindPage(pages.Count - 1, -1);

            if (offset < min)
                offset = min + (offset - min) * Mathf.Clamp01(elasticResistance);
            else if (offset > max)
                offset = max + (offset - max) * Mathf.Clamp01(elasticResistance);

            ApplyPosition(offset);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || dragEvent != eventData)
                return;

            isDragging = false;
            dragEvent = null;
            int targetPage = dragPageIndex;
            if (TryGetPointer(eventData, eventData.position, out Vector2 position))
            {
                float delta = GetPointerAxis(position) - dragStartPointer;
                bool quickSwipe = Time.unscaledTime - dragLastTime <= 0.1f && Mathf.Abs(dragVelocity) > velocityThreshold;
                if (quickSwipe || Mathf.Abs(delta) >= ContainerSize * Mathf.Clamp01(swipeThreshold))
                {
                    int direction = (quickSwipe ? dragVelocity : delta) > 0f ? 1 : -1;
                    int next = FindPage(dragPageIndex + direction, direction);
                    if (next >= 0)
                        targetPage = next;
                }
            }

            OpenPage(targetPage);
        }

        bool TryGetPointer(PointerEventData eventData, Vector2 screenPosition, out Vector2 position)
        {
            position = default;
            return container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container,
                screenPosition, eventData.pressEventCamera, out position);
        }

        float GetPointerAxis(Vector2 position) => swipeDirection == SwipeDirection.Horizontal ? -position.x : position.y;

        internal bool CanBeginDrag(PointerEventData eventData)
        {
            if (!isInitialized || !isActiveAndEnabled || isDragging || !IsValidPage(currentPageIndex)
                || eventData.button != PointerEventData.InputButton.Left || ContainerSize <= 0f
                || IsTransitioning && !interruptTransitions)
                return false;

            if (!TryGetPointer(eventData, eventData.position, out Vector2 position)
                || !TryGetPointer(eventData, eventData.pressPosition, out Vector2 start))
                return false;

            Vector2 delta = position - start;
            return swipeDirection == SwipeDirection.Horizontal
                ? Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                : Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
        }
        #endregion

        #region Page Layout
        bool IsValidPage(int index) => index >= 0 && index < pages.Count && pages[index]?.pageObject != null;

        int FindPage(int start, int direction)
        {
            for (int i = start; i >= 0 && i < pages.Count; i += direction)
            {
                if (IsValidPage(i))
                    return i;
            }
            return -1;
        }

        void StopTransition()
        {
            transitionTween.Kill();
            transitionTween = default;
        }

        void UnbindPages()
        {
            for (int i = 0; i < boundPages.Count; i++)
                boundPages[i].Unbind();

            boundPages.Clear();
        }

        void ApplySelection()
        {
            StopTransition();
            isDragging = false;
            dragEvent = null;

            if (!IsValidPage(currentPageIndex))
                currentPageIndex = FindPage(0, 1);

            ApplyPosition(currentPageIndex >= 0 ? currentPageIndex : 0f);
            UpdateButtonStates();
        }

        void ApplyPosition(float position)
        {
            currentPosition = position;

            if (container == null)
                return;

            float size = PageSize;
            for (int i = 0; i < pages.Count; i++)
            {
                RectTransform page = pages[i]?.pageObject;
                if (page == null)
                    continue;

                float offset = (i - position) * size;
                page.anchoredPosition = swipeDirection == SwipeDirection.Horizontal ? new Vector2(offset, 0f) : new Vector2(0f, -offset);
                bool visible = !disableInvisiblePages || Mathf.Abs(i - position) < 1f || isDragging && Mathf.Abs(i - position) <= 1f;
                // Keep the event receiver alive until the gesture ends, including interrupted transitions.
                visible |= isDragging && i == dragSourcePageIndex;
                if (page.gameObject.activeSelf != visible)
                    page.gameObject.SetActive(visible);
            }

            RefreshIndicator();
        }

        void UpdateButtonStates()
        {
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i]?.pageButton != null)
                    pages[i].pageButton.SetState(i == currentPageIndex ? InteractionState.Selected : InteractionState.Normal);
            }
        }

        void CompleteTransition()
        {
            transitionTween = default;
            ApplyPosition(currentPageIndex >= 0 ? currentPageIndex : 0f);
        }
        #endregion

        #region Nested Scrolling
        void EnableNestedScrolling(bool value)
        {
            if (nestedScrollManagers == null)
                return;

            for (int i = 0; i < nestedScrollManagers.Count; i++)
            {
                if (nestedScrollManagers[i] != null && nestedScrollManagers[i].Owner == this)
                    nestedScrollManagers[i].enabled = value;
            }
        }

        void RefreshNestedScrolling()
        {
            if (!Application.isPlaying || !isInitialized)
                return;

            EnableNestedScrolling(false);
          
            if (!handleNestedScrolling || !isActiveAndEnabled)
                return;

            nestedScrollRects ??= new();
            nestedScrollManagers ??= new();
            GetComponentsInChildren(true, nestedScrollRects);

            for (int i = 0; i < nestedScrollRects.Count; i++)
            {
                UnityEngine.UI.ScrollRect scrollRect = nestedScrollRects[i];
                if (scrollRect.transform == transform || scrollRect.GetComponentInParent<PageManager>(true) != this)
                    continue;

                if (!scrollRect.TryGetComponent<NestedScrollManager>(out var manager))
                {
                    manager = scrollRect.gameObject.AddComponent<NestedScrollManager>();
                    manager.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                }

                manager.Initialize(this, scrollRect);

                if (!nestedScrollManagers.Contains(manager))
                    nestedScrollManagers.Add(manager);
            }

            nestedScrollRects.Clear();

            for (int i = nestedScrollManagers.Count - 1; i >= 0; i--)
            {
                NestedScrollManager manager = nestedScrollManagers[i];
                if (manager != null && manager.Owner == this && manager.enabled)
                    continue;

                if (manager != null && manager.Owner == this)
                {
                    manager.Release();
                    Destroy(manager);
                }

                nestedScrollManagers.RemoveAt(i);
            }
        }

        internal void CancelNestedDrag(PointerEventData eventData)
        {
            if (isDragging && dragEvent == eventData)
                ApplySelection();
        }
        #endregion

        #region Indicator
        bool GetIndicatorBounds(int index, out Vector2 position, out Vector2 size)
        {
            position = default;
            size = default;
            RectTransform button = IsValidPage(index) && pages[index].pageButton != null
                ? pages[index].pageButton.transform as RectTransform
                : null;

            if (button == null)
                return false;

            button.GetWorldCorners(indicatorCorners);
            Transform parent = indicator.parent;
            Vector3 min = parent != null ? parent.InverseTransformPoint(indicatorCorners[0]) : indicatorCorners[0];
            Vector3 max = min;
      
            for (int i = 1; i < indicatorCorners.Length; i++)
            {
                Vector3 corner = parent != null ? parent.InverseTransformPoint(indicatorCorners[i]) : indicatorCorners[i];
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            Vector2 boundsSize = max - min;
            Vector2 pivotPosition = (Vector2)min + Vector2.Scale(boundsSize, indicator.pivot);
            position = indicator.anchoredPosition + pivotPosition - (Vector2)indicator.localPosition;
            size = indicator.sizeDelta + boundsSize - indicator.rect.size;
            return true;
        }

        public void RefreshIndicator()
        {
            if (indicator == null)
                return;

            float position = Mathf.Clamp(currentPosition, 0f, Mathf.Max(0, pages.Count - 1));
            int from = Mathf.FloorToInt(position);
            int to = Mathf.CeilToInt(position);
            bool visible = GetIndicatorBounds(from, out Vector2 fromPosition, out Vector2 fromSize);
            Vector2 toPosition = fromPosition;
            Vector2 toSize = fromSize;
          
            if (visible && from != to)
                visible = GetIndicatorBounds(to, out toPosition, out toSize);

            if (indicator.gameObject.activeSelf != visible)
                indicator.gameObject.SetActive(visible);
           
            if (!visible)
                return;

            indicator.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, position - from);
            indicator.sizeDelta = Vector2.LerpUnclamped(fromSize, toSize, position - from);
        }
        #endregion

        #region Public API
        public void OpenPage(int index, bool animate = true)
        {
            if (!IsValidPage(index))
                return;

            if (!isInitialized && Application.isPlaying)
            {
                currentPageIndex = index;
                return;
            }

            if (animate && IsTransitioning && (!interruptTransitions || currentPageIndex == index))
                return;

            bool changed = currentPageIndex != index;
            StopTransition();
            isDragging = false;
            dragEvent = null;
            currentPageIndex = index;
            UpdateButtonStates();

            float duration = transitionDuration * Mathf.Clamp01(Mathf.Abs(index - currentPosition));
            if (!animate || !Application.isPlaying || !isActiveAndEnabled || !(duration > 0f)) { ApplyPosition(index); }
            else
            {
                bool customCurve = transitionCurve != null && transitionCurve.length > 0;
                transitionTween = EvoTween.Create(currentPosition, (float)index, duration)
                    .WithEase(customCurve ? Ease.Custom : Ease.Linear, customCurve ? transitionCurve : null)
                    .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                    .OnComplete(completeTransition)
                    .Bind(this, PositionSetter, PositionPropertyKey);
            }

            // Complete internal state before invoking user code, including reentrant OpenPage calls
            if (changed)
                onPageChanged?.Invoke(currentPageIndex);
        }

        public void OpenPage(int index) => OpenPage(index, true);

        public void OpenNextPage() => OpenPage(FindPage(currentPageIndex + 1, 1));

        public void OpenPreviousPage() => OpenPage(FindPage(currentPageIndex - 1, -1));

        /// <summary>
        /// Rebuilds page layout and button bindings after changing the page list or references.
        /// </summary>
        public void PositionPages()
        {
            if (container == null)
                container = transform as RectTransform;

            UnbindPages();

            for (int i = 0; i < pages.Count; i++)
            {
                Page page = pages[i];
                if (page == null)
                    continue;

                if (Application.isPlaying && isInitialized)
                {
                    page.Bind(this);
                    boundPages.Add(page);
                }

                if (page.pageObject == null)
                    continue;

                page.pageObject.anchorMin = Vector2.zero;
                page.pageObject.anchorMax = Vector2.one;
                page.pageObject.sizeDelta = Vector2.zero;
            }

            lastContainerSize = container != null ? container.rect.size : Vector2.zero;
            lastPageSpacing = pageSpacing;
            lastSwipeDirection = swipeDirection;
            ApplySelection();
            RefreshNestedScrolling();
        }
        #endregion

#if UNITY_EDITOR
        bool isEditingPages;
        int editedPageIndex;
        RectTransform editedPageObject;

        public void BeginPagesEdit()
        {
            if (!Application.isPlaying || !isInitialized || isEditingPages)
                return;

            isEditingPages = true;
            editedPageIndex = currentPageIndex;
            editedPageObject = CurrentPage?.pageObject;
            StopTransition();
            isDragging = false;
            dragEvent = null;
            UnbindPages();

            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i]?.pageObject != null && disableInvisiblePages)
                    pages[i].pageObject.gameObject.SetActive(false);
            }
        }

        public void EndPagesEdit()
        {
            if (!isEditingPages)
                return;

            isEditingPages = false;
            PositionPages();
            bool changed = editedPageIndex != currentPageIndex || editedPageObject != CurrentPage?.pageObject;
            editedPageObject = null;
           
            if (changed)
                onPageChanged?.Invoke(currentPageIndex);
        }

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            currentPageIndex = pages.Count > 0 ? Mathf.Clamp(currentPageIndex, 0, pages.Count - 1) : -1;
            UnityEditor.EditorApplication.delayCall -= UpdateEditorPreview;
            UnityEditor.EditorApplication.delayCall += UpdateEditorPreview;
        }

        void UpdateEditorPreview()
        {
            if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this))
                return;

            PositionPages();
        }
#endif

        [Serializable]
        public class Page
        {
            [Tooltip("Name used to identify this page in the Inspector.")]
            public string pageID = "";
            [Tooltip("Page content to stretch to the container and move during swipes and transitions.")]
            public RectTransform pageObject;
            [Tooltip("Optional button that opens this page and displays its selection state.")]
            public Button pageButton;

            // Cache
            [NonSerialized] PageManager owner;
            [NonSerialized] Button boundButton;
            [NonSerialized] UnityAction clickAction;

            internal void Bind(PageManager manager)
            {
                Unbind();
                owner = manager;
                boundButton = pageButton;
                clickAction ??= OnClick;

                if (boundButton != null)
                    boundButton.onClick.AddListener(clickAction);
            }

            internal void Unbind()
            {
                if (boundButton != null && clickAction != null)
                    boundButton.onClick.RemoveListener(clickAction);

                boundButton = null;
                owner = null;
            }

            void OnClick()
            {
                if (owner != null && owner.isActiveAndEnabled)
                    owner.OpenPage(owner.pages.IndexOf(this));
            }
        }
    }

    /// <summary>
    /// Created only at runtime by PageManager. Keeps ordinary ScrollRects free of page-specific input handling.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    internal sealed class NestedScrollManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal PageManager Owner { get; private set; }
        UnityEngine.UI.ScrollRect scrollRect;
        RectTransform rectTransform;
        PointerEventData dragEvent;

        internal void Initialize(PageManager owner, UnityEngine.UI.ScrollRect target)
        {
            CancelDrag();
            Owner = owner;
            scrollRect = target;
            rectTransform = target.transform as RectTransform;
            enabled = true;
        }

        internal void Release()
        {
            enabled = false;
            CancelDrag();
            Owner = null;
            scrollRect = null;
        }

        void OnDisable() => CancelDrag();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragEvent != null || Owner == null || !Owner.HandleNestedScrolling || scrollRect == null
                || !scrollRect.isActiveAndEnabled || !Owner.CanBeginDrag(eventData))
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 position)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.pressPosition, eventData.pressEventCamera, out Vector2 start))
                return;

            Vector2 delta = position - start;
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            if (horizontal ? scrollRect.horizontal : scrollRect.vertical)
                return;

            // Both components receive BeginDrag. Suspend the child for this gesture regardless of callback order.
            dragEvent = eventData;
            scrollRect.StopMovement();
            scrollRect.enabled = false;
            Owner.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragEvent == eventData && Owner != null)
                Owner.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragEvent != eventData)
                return;

            RestoreScrollRect();
            if (Owner != null)
                Owner.OnEndDrag(eventData);
        }

        void RestoreScrollRect()
        {
            if (dragEvent == null)
                return;

            dragEvent = null;

            if (scrollRect != null)
                scrollRect.enabled = true;
        }

        void CancelDrag()
        {
            PointerEventData previousEvent = dragEvent;
            RestoreScrollRect();

            if (previousEvent != null && Owner != null)
                Owner.CancelNestedDrag(previousEvent);
        }
    }
}