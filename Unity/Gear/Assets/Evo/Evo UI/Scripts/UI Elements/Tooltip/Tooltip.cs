using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Tooltip"), HelpURL(Constants.HelpUrl + "ui-elements/tooltip")]
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>
        /// Closed tooltips kept for reuse, shared by all tooltips. Set to 0 to destroy them instead.
        /// </summary>
        public static int MaxPoolSize { get; set; } = 3;

        // Content
        public GameObject tooltipPreset;
        [SerializeField] private Sprite icon;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 4)] private string description;

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        // Settings
        public bool followCursor = true;
        public bool is3DObject = false;
        [Tooltip("Sets the maximum width of the tooltip.")]
        public float maxWidth = 400f;
        [Range(0f, 10f), Tooltip("Delay in seconds before the tooltip is displayed.")]
        public float showDelay = 0f;
        [Range(0f, 40f), Tooltip("Smooths position changes. Set to 0 for immediate movement.")]
        public float movementSmoothing = 0f;

        // Position & Offset
        public OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        public Vector2 customOffset = new(10f, 10f);
        public float offsetDistance = 30f;
        [Tooltip("Keeps the tooltip away from the canvas edges.")]
        public float screenEdgePadding = 10f;

        // References
        [SerializeField, Tooltip("Canvas used to render the tooltip. A default canvas is used when unset.")]
        private Canvas tooltipCanvas;
        [Tooltip("Instantiates a custom prefab instead of the preset content.")]
        public GameObject customContent;

        // Events
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        // State
        bool isCustomLogGiven;
        bool isUIElement;
        bool offsetNeedsUpdate;

        // Cache
        Canvas cachedCanvas;
        Camera cachedCamera;
        Camera followCamera;
        RectTransform rectTransform;
        RectTransform cachedCanvasRect;
        TooltipPreset tooltipInstance;
        RectTransform positionRect;
        TweenHandle showDelayTween;
        System.Action showAction;
        Vector2 cachedTooltipSize;
        readonly Vector3[] targetWorldCorners = new Vector3[4];

        // Properties
        Canvas ActiveCanvas
        {
            get
            {
                if (cachedCanvas != null)
                    return cachedCanvas;

                if (tooltipCanvas != null) { cachedCanvas = tooltipCanvas; }
                else
                {
                    cachedCanvas = GetComponentInParent<Canvas>();
                    if (cachedCanvas == null)
                        cachedCanvas = Utilities.ResolveCanvas();
                }

                if (cachedCanvas != null)
                    cachedCanvasRect = cachedCanvas.GetComponent<RectTransform>();

                return cachedCanvas;
            }
        }

        public Sprite Icon
        {
            get => icon;
            set
            {
                if (icon == value)
                    return;

                icon = value;
                RefreshVisibleContent();
            }
        }

        public string Title
        {
            get => title;
            set
            {
                if (title == value)
                    return;

                title = value;
                RefreshVisibleContent();
            }
        }

        public string Description
        {
            get => description;
            set
            {
                if (description == value)
                    return;

                description = value;
                RefreshVisibleContent();
            }
        }

        /// <summary>
        /// Rect the tooltip is placed against when Follow Cursor is off. Null uses this object.
        /// </summary>
        public RectTransform Target { get; set; }

        public TooltipPreset Instance => tooltipInstance;

        public bool IsVisible => tooltipInstance != null;

        void Awake() => isUIElement = TryGetComponent(out rectTransform);

#if EVO_LOCALIZATION
        void OnEnable()
        {
            Localization.LocalizationManager.Subscribe(this, enableLocalization && customContent == null, ref localizedObject, UpdateLocalization);
        }
#endif

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
            Hide();
        }

        void OnDestroy()
        {
            showDelayTween.Kill();

            // The copy may be going away with this object, so it isn't pooled
            if (tooltipInstance != null)
                tooltipInstance.Discard();
            else if (positionRect != null)
                Destroy(positionRect.gameObject);

            tooltipInstance = null;
            positionRect = null;
        }

        void OnMouseEnter()
        {
            if (is3DObject && enabled)
                Show();
        }

        void OnMouseExit()
        {
            if (is3DObject && enabled)
                Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!is3DObject)
                Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!is3DObject)
                Hide();
        }

        public void OnSelect(BaseEventData eventData)
        {
            // Clicking a hovered control selects it; keep the tooltip that is shown or about to show
            bool shownOrPending = showDelayTween.IsActive || (tooltipInstance != null && !tooltipInstance.IsClosing);
            if (!is3DObject && !shownOrPending)
                Show();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!is3DObject)
                Hide();
        }

        void UpdateCachedTooltipSize()
        {
            if (tooltipInstance == null || tooltipInstance.tooltipRect == null)
            {
                cachedTooltipSize = Vector2.zero;
                return;
            }

            cachedTooltipSize = tooltipInstance.GetResolvedSize();

            if (positionRect != null)
                positionRect.sizeDelta = cachedTooltipSize;
        }

        void RefreshVisibleContent()
        {
            if (tooltipInstance == null)
                return;

            tooltipInstance.Setup(title, description, icon, maxWidth, customContent != null);
            offsetNeedsUpdate = true;
        }

        Camera GetCanvasCamera(Canvas canvas, Camera fallback)
        {
            if (canvas == null)
                return fallback != null ? fallback : Camera.main;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera != null ? canvas.worldCamera : fallback != null ? fallback : Camera.main;
        }

        Camera GetTargetCamera()
        {
            if (cachedCamera != null)
                return cachedCamera;

            Canvas canvas = ActiveCanvas;
            if (canvas != null && canvas.worldCamera != null)
            {
                cachedCamera = canvas.worldCamera;
                return cachedCamera;
            }

            if (isUIElement)
            {
                // ActiveCanvas lookup already checked parent structure, use it safely
                if (canvas != null && canvas.worldCamera != null)
                    return canvas.worldCamera;
            }

            cachedCamera = Camera.main;
            return cachedCamera;
        }

        Vector2 CalculateOffset(Vector2 targetSize)
        {
            if (offsetPosition == OffsetPosition.Custom)
                return customOffset;

            float horizontalDistance = (targetSize.x + cachedTooltipSize.x) * 0.5f + offsetDistance;
            float verticalDistance = (targetSize.y + cachedTooltipSize.y) * 0.5f + offsetDistance;

            return offsetPosition switch
            {
                OffsetPosition.TopLeft => new Vector2(-horizontalDistance, verticalDistance),
                OffsetPosition.TopRight => new Vector2(horizontalDistance, verticalDistance),
                OffsetPosition.BottomLeft => new Vector2(-horizontalDistance, -verticalDistance),
                OffsetPosition.BottomRight => new Vector2(horizontalDistance, -verticalDistance),
                OffsetPosition.Top => new Vector2(0f, verticalDistance),
                OffsetPosition.Bottom => new Vector2(0f, -verticalDistance),
                OffsetPosition.Left => new Vector2(-horizontalDistance, 0f),
                OffsetPosition.Right => new Vector2(horizontalDistance, 0f),
                _ => Vector2.zero,
            };
        }

        Vector2 CalculateTargetPosition(Camera targetCamera, out Vector2 targetSize)
        {
            targetSize = Vector2.zero;
            Canvas canvas = ActiveCanvas;

            if (canvas == null || cachedCanvasRect == null)
                return Vector2.zero;

            Camera canvasCamera = GetCanvasCamera(canvas, targetCamera);

            if (followCursor)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedCanvasRect, Utilities.GetPointerPosition(),
                    canvasCamera, out Vector2 pointerPosition);
                return pointerPosition;
            }

            RectTransform target = Target != null ? Target : isUIElement ? rectTransform : null;
            if (target != null)
            {
                Canvas targetCanvas = target.GetComponentInParent<Canvas>();
                Camera sourceCamera = GetCanvasCamera(targetCanvas, targetCamera);
                target.GetWorldCorners(targetWorldCorners);

                Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

                for (int i = 0; i < targetWorldCorners.Length; i++)
                {
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, targetWorldCorners[i]);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedCanvasRect, screenPoint, canvasCamera,
                        out Vector2 localCorner);
                    min = Vector2.Min(min, localCorner);
                    max = Vector2.Max(max, localCorner);
                }

                targetSize = max - min;
                return (min + max) * 0.5f;
            }

            targetCamera = targetCamera != null ? targetCamera : Camera.main;
            Vector2 targetScreenPoint = targetCamera != null ? targetCamera.WorldToScreenPoint(transform.position) : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cachedCanvasRect, targetScreenPoint, canvasCamera,
                out Vector2 targetPosition);

            return targetPosition;
        }

        Vector2 ClampToCanvasBounds(Vector2 anchoredPosition)
        {
            Canvas canvas = ActiveCanvas;

            if (canvas == null || cachedCanvasRect == null)
                return anchoredPosition;

            float width = cachedTooltipSize.x;
            float height = cachedTooltipSize.y;

            Rect canvasRect = cachedCanvasRect.rect;
            float minX = canvasRect.xMin + width * 0.5f + screenEdgePadding;
            float maxX = canvasRect.xMax - width * 0.5f - screenEdgePadding;
            float minY = canvasRect.yMin + height * 0.5f + screenEdgePadding;
            float maxY = canvasRect.yMax - height * 0.5f - screenEdgePadding;

            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
            return anchoredPosition;
        }

        void ShowNow()
        {
            if (this == null || !gameObject.activeInHierarchy)
                return;

            GameObject prefabToUse = customContent != null ? customContent : tooltipPreset;
            if (prefabToUse == null)
            {
                Debug.LogWarning($"No Tooltip Preset or Custom Content assigned to {gameObject.name}.", this);
                return;
            }

            Canvas canvas = ActiveCanvas;
            if (canvas == null)
            {
                Debug.LogWarning($"No canvas is available for the tooltip on {gameObject.name}.", this);
                return;
            }

            // Reuse a closed copy of the same prefab before instantiating a new one.
            // Positioning and visual animation use separate transforms so a preset can slide while following its target.
            tooltipInstance = TooltipPreset.Rent(prefabToUse);
            positionRect = tooltipInstance != null
                ? tooltipInstance.RuntimeRoot
                : (RectTransform)new GameObject("Tooltip Position", typeof(RectTransform)).transform;

            if (positionRect.parent != canvas.transform) { positionRect.SetParent(canvas.transform, false); }
            else { positionRect.SetAsLastSibling(); }

            Vector2 canvasPivot = cachedCanvasRect != null ? cachedCanvasRect.pivot : Vector2.one * 0.5f;
            positionRect.anchorMin = canvasPivot;
            positionRect.anchorMax = canvasPivot;
            positionRect.pivot = Vector2.one * 0.5f;
            positionRect.sizeDelta = Vector2.zero;

            if (tooltipInstance == null)
            {
                GameObject toGo = Instantiate(prefabToUse, positionRect, false);

                // Try to get existing TooltipPreset, or add one if missing
                bool addedAtRuntime = !toGo.TryGetComponent(out tooltipInstance);
                if (addedAtRuntime)
                    tooltipInstance = toGo.AddComponent<TooltipPreset>();

                tooltipInstance.SetRuntimeRoot(positionRect, prefabToUse, addedAtRuntime);
            }

            bool isCustom = customContent != null;
            if (tooltipInstance.IsAddedAtRuntime)
            {
                if (!isCustomLogGiven)
                {
                    Debug.LogWarning($"<b>Tooltip Preset</b> component is not attached to <b>{gameObject.name}</b>, " +
                        $"but it's assigned as Tooltip Preset." +
                        $"\nThis tooltip will be treated as Custom Content, and the default content will be skipped.\n", this);
                    isCustomLogGiven = true;
                }
                isCustom = true;
            }

            tooltipInstance.Activate(this);
            tooltipInstance.Setup(title, description, icon, maxWidth, isCustom);

            // Force layout update once so placement uses the tooltip's resolved size
            UpdateCachedTooltipSize();
            offsetNeedsUpdate = false;

            followCamera = GetTargetCamera();
            Vector2 initialPosition = CalculateTargetPosition(followCamera, out Vector2 targetSize);
            initialPosition += CalculateOffset(targetSize);
            positionRect.anchoredPosition = ClampToCanvasBounds(initialPosition);
            tooltipInstance.Open();
        }

        /// <summary>
        /// Called every frame by the shown tooltip while it's open.
        /// </summary>
        internal void FollowTarget()
        {
            if (positionRect == null)
                return;

            // Recalculate layout offset only if text/content changed mid-display
            if (offsetNeedsUpdate)
            {
                Canvas.ForceUpdateCanvases();
                UpdateCachedTooltipSize();
                offsetNeedsUpdate = false;
            }

            Vector2 newTargetPosition = CalculateTargetPosition(followCamera, out Vector2 targetSize);
            newTargetPosition += CalculateOffset(targetSize);
            newTargetPosition = ClampToCanvasBounds(newTargetPosition);

            if (movementSmoothing == 0) { positionRect.anchoredPosition = newTargetPosition; }
            else
            {
                Vector2 currentPosition = positionRect.anchoredPosition;
                Vector2 smoothedPosition = Vector2.Lerp(currentPosition, newTargetPosition, movementSmoothing * Time.unscaledDeltaTime);
                positionRect.anchoredPosition = smoothedPosition;
            }
        }

        /// <summary>
        /// Called by the shown tooltip once it's back in the pool or destroyed.
        /// </summary>
        internal void ReleaseInstance(TooltipPreset instance)
        {
            if (tooltipInstance != instance)
                return;

            tooltipInstance = null;
            positionRect = null;
        }

        public void Show()
        {
            // Check if we have either a tooltip preset or custom content
            if (tooltipPreset == null && customContent == null)
            {
                Debug.LogWarning($"No tooltip preset or custom content assigned to {gameObject.name}. " +
                    $"Please assign a prefab with 'Tooltip Preset' component or a custom content GameObject.", this);
                return;
            }

            showDelayTween.Kill();
            HideImmediate();

            if (showDelay > 0)
            {
                // A zero-length tween works as a delayed call on unscaled time. Evo Tween pools it, and the value is unused.
                showAction ??= ShowNow;
                showDelayTween = EvoTween.Create(0f, 0f, 0f)
                    .WithDelay(showDelay)
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(showAction)
                    .Bind(_ => { });
            }
            else { ShowNow(); }

            onShow?.Invoke();
        }

        public void Hide()
        {
            showDelayTween.Kill();

            if (!gameObject.activeInHierarchy)
                HideImmediate();
            else if (tooltipInstance != null)
                tooltipInstance.Close();

            onHide?.Invoke();
        }

        public void HideImmediate()
        {
            if (tooltipInstance != null)
                tooltipInstance.CloseImmediate();
            else if (positionRect != null)
                Destroy(positionRect.gameObject);

            tooltipInstance = null;
            positionRect = null;
        }

        public void SetContent(string newTitle, string newDescription, Sprite newIcon = null, GameObject newCustomContent = null)
        {
            if (newCustomContent != null)
            {
                customContent = newCustomContent;
                if (tooltipInstance != null)
                {
                    HideImmediate();
                    Show();
                }
                return;
            }

            title = newTitle;
            description = newDescription;
            icon = newIcon;
            customContent = newCustomContent;

            RefreshVisibleContent();
        }

        public void SetContent(GameObject newCustomContent) => SetContent(null, null, null, newCustomContent);

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || customContent != null)
                return;

            bool changed = false;
            if (!string.IsNullOrEmpty(titleKey)) 
            { 
                title = localizedObject.GetString(titleKey) ?? title;
                changed = true;
            }

            if (!string.IsNullOrEmpty(descriptionKey)) 
            { 
                description = localizedObject.GetString(descriptionKey) ?? description;
                changed = true;
            }

            if (changed)
                RefreshVisibleContent();
        }
#endif

        #region Obsolete API
        [System.Obsolete("Use Title instead.")]
        public void SetTitle(string newTitle) => Title = newTitle;

        [System.Obsolete("Use Description instead.")]
        public void SetDescription(string newDescription) => Description = newDescription;

        [System.Obsolete("Use Icon instead.")]
        public void SetIcon(Sprite newIcon) => Icon = newIcon;
        #endregion
    }
}