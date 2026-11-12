using System;
using System.Text;
using UnityEngine;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Off Screen Indicator"), HelpURL(Constants.HelpUrl + "ui-elements/off-screen-indicator")]
    public class OffScreenIndicator : MonoBehaviour
    {
        // Customization
        [SerializeField] private GameObject indicatorPreset;
        public Sprite indicatorIcon;

        // Settings
        [Tooltip("Hide the indicator and distance text when the target is on screen.")]
        public bool hideWhenOnScreen;
        [Tooltip("Maximum distance from the distance source, in meters.")]
        public float checkDistance = 100f;
        public float borderOffset = 40f;
        [Range(0.01f, 2f)] public float fadeDuration = 0.1f;
        [Range(0f, 1f)] public float transitionEase = 0f;

        // Distance Settings
        public DistanceUnit distanceUnit = DistanceUnit.None;
        [Tooltip("{0} = distance, {1} = unit")]
        public string distanceFormat = "{0}{1}";
        [Tooltip("Used for the distance limit, fade, and text. Defaults to the target camera.")]
        public Transform distanceSource;
        public bool enableDistanceFade = true;
        public float fadeStartDistance = 5;
        public float fadeEndDistance = 2;

        // References
        [Tooltip("Target to track. RectTransform targets are detected automatically. Defaults to this transform.")]
        public Transform targetTransform;
        [Tooltip("Camera used for target projection. Defaults to the main camera.")]
        public Camera targetCamera;
        [SerializeField, Tooltip("Canvas used to display the indicator. A shared canvas is created if unassigned.")]
        private Canvas targetCanvas;
        [Tooltip("Optional screen-space bounds taken from this RectTransform.")]
        public RectTransform rectBoundary;

        // Constants
        const float MetersToFeet = 3.28084f;
        const float MetersToKilometers = 0.001f;
        const float FeetToMiles = 1f / 5280f;
        const string NumberMarker = "\u001f";
        const string UnitMarker = "\u001e";

        // Enums
        public enum DistanceUnit { None, Metric, Imperial }

        // Cache
        OffScreenIndicatorObject indicatorObject;
        RectTransform indicatorRect;
        RectTransform targetCanvasRect;
        Camera canvasCamera;
        Rect screenBoundary;
        Vector2 canvasCenter;
        Vector2 currentPosition;
        Vector2 positionVelocity;
        float cachedDistanceToTarget;
        float targetObjectVisibilityAlpha;
        bool hasPosition;
        bool isDisabling;
        readonly Vector3[] rectCorners = new Vector3[4];

        // Distance Formatting Cache
        readonly StringBuilder distanceBuilder = new(64);
        readonly char[] numberBuffer = new char[64];
        string cachedDistanceFormat;
        string distanceTemplate;

        void Awake()
        {
            if (targetTransform == null)
                targetTransform = transform;
        }

        void Start()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
            {
                Debug.LogError("[Off Screen Indicator] No camera assigned and no main camera found.", this);
                enabled = false;
                return;
            }

            if (indicatorPreset == null || !indicatorPreset.TryGetComponent<OffScreenIndicatorObject>(out _)
                || !indicatorPreset.TryGetComponent<RectTransform>(out _))
            {
                Debug.LogError("[Off Screen Indicator] Assign a preset with OffScreenIndicatorObject and RectTransform components.", this);
                enabled = false;
                return;
            }

            if (targetCanvas == null)
                targetCanvas = Utilities.ResolveCanvas();

            targetCanvasRect = targetCanvas.GetComponent<RectTransform>();

            if (distanceSource == null)
                distanceSource = targetCamera.transform;

            CreateIndicatorObject();
            UpdateDistanceTemplate();
        }

        void OnEnable()
        {
            isDisabling = false;
            hasPosition = false;
            positionVelocity = Vector2.zero;
        }

        void OnDisable()
        {
            isDisabling = false;

            if (indicatorObject != null)
            {
                indicatorObject.VisibilityAlpha = 0f;
                indicatorObject.SetAlpha(0f);
                indicatorObject.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (indicatorObject != null)
                Destroy(indicatorObject.gameObject);
        }

        void LateUpdate()
        {
            if (indicatorObject == null)
                return;

            if (isDisabling)
            {
                UpdateAlphaValues();
                return;
            }

            if (targetTransform == null || targetCamera == null || targetCanvas == null || targetCanvasRect == null)
            {
                indicatorObject.SetAlpha(0f);
                indicatorObject.VisibilityAlpha = 0f;
                indicatorObject.gameObject.SetActive(false);
                hasPosition = false;
                positionVelocity = Vector2.zero;
                return;
            }

            UpdateIndicator();
        }

        void CreateIndicatorObject()
        {
            GameObject indicatorInstance = Instantiate(indicatorPreset, targetCanvas.transform);
            indicatorInstance.name = $"Off Screen Indicator ({gameObject.name})";
            indicatorObject = indicatorInstance.GetComponent<OffScreenIndicatorObject>();
            indicatorRect = indicatorInstance.GetComponent<RectTransform>();

            // Center anchors allow screen positions to map directly to canvas space
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);

            indicatorObject.Initialize();
            indicatorObject.SetIcon(indicatorIcon);
            indicatorObject.VisibilityAlpha = 0f;
            indicatorObject.SetAlpha(0f);
            indicatorInstance.SetActive(false);
        }

        void UpdateIndicator()
        {
            RectTransform targetRect = targetTransform as RectTransform;
            Vector3 targetPosition = targetTransform.position;
            Vector3 worldCenter = targetRect != null ? targetRect.TransformPoint(targetRect.rect.center) : targetPosition;
            Camera projectionCamera = targetRect != null ? GetCanvasCamera(targetRect.GetComponentInParent<Canvas>()) : targetCamera;
            canvasCamera = GetCanvasCamera(targetCanvas);
            UpdateBoundaryValues(projectionCamera);

            Vector3 targetScreenPosition = projectionCamera != null ? projectionCamera.WorldToScreenPoint(worldCenter) : worldCenter;
            bool isBehindCamera = projectionCamera != null && targetScreenPosition.z <= 0f;

            if (distanceSource == null)
                distanceSource = targetCamera.transform;

            cachedDistanceToTarget = Vector3.Distance(distanceSource.position, targetPosition);
            bool isOnScreen = !isBehindCamera && screenBoundary.Contains(targetScreenPosition);
            targetObjectVisibilityAlpha = cachedDistanceToTarget <= checkDistance && (!isOnScreen || !hideWhenOnScreen) ? 1f : 0f;

            if (!indicatorObject.gameObject.activeSelf)
                indicatorObject.gameObject.SetActive(true);

            indicatorObject.SetArrowVisible(!isOnScreen);
            UpdateDistanceDisplay(isOnScreen);
            UpdateAlphaValues();

            Vector2 calculatedPosition = CalculateIndicatorPosition(targetScreenPosition, isBehindCamera, isOnScreen, projectionCamera);

            if (!hasPosition || transitionEase <= 0f)
            {
                currentPosition = calculatedPosition;
                positionVelocity = Vector2.zero;
                hasPosition = true;
            }
            else if (Time.deltaTime > 0f)
                currentPosition = Vector2.SmoothDamp(currentPosition, calculatedPosition, ref positionVelocity, transitionEase);

            indicatorObject.SetPosition(currentPosition);
        }

        Camera GetCanvasCamera(Canvas canvas)
        {
            if (canvas == null)
                return targetCamera;

            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : targetCamera;
        }

        void UpdateBoundaryValues(Camera projectionCamera)
        {
            if (rectBoundary != null)
                screenBoundary = GetScreenRect(rectBoundary, GetCanvasCamera(rectBoundary.GetComponentInParent<Canvas>()));
            else
                screenBoundary = projectionCamera != null ? projectionCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
        }

        Rect GetScreenRect(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(rectCorners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, rectCorners[0]);
            Vector2 max = min;

            for (int i = 1; i < rectCorners.Length; i++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(camera, rectCorners[i]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        void UpdateAlphaValues()
        {
            float targetAlpha = isDisabling ? 0f : targetObjectVisibilityAlpha;
            float alpha = fadeDuration > 0f
                ? Mathf.MoveTowards(indicatorObject.VisibilityAlpha, targetAlpha, Time.deltaTime / fadeDuration)
                : targetAlpha;
            indicatorObject.VisibilityAlpha = alpha;

            if (!isDisabling && enableDistanceFade)
            {
                float distanceAlpha = 1f;
                if (cachedDistanceToTarget <= fadeEndDistance)
                    distanceAlpha = 0f;
                else if (cachedDistanceToTarget < fadeStartDistance)
                    distanceAlpha = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, cachedDistanceToTarget);

                alpha *= distanceAlpha;
            }

            indicatorObject.SetAlpha(alpha);

            if (isDisabling && alpha <= 0f)
                enabled = false;
        }

        void UpdateDistanceDisplay(bool isOnScreen)
        {
            bool shouldShowDistance = !hideWhenOnScreen && isOnScreen && distanceUnit != DistanceUnit.None;
            indicatorObject.SetDistanceVisible(shouldShowDistance);

            if (!shouldShowDistance)
                return;

            float distance = cachedDistanceToTarget;
            string unit;
            string format;

            if (distanceUnit == DistanceUnit.Imperial)
            {
                distance *= MetersToFeet;
                bool useMiles = distance >= 5280f;
                if (useMiles)
                    distance *= FeetToMiles;

                unit = useMiles ? "mi" : "ft";
                format = useMiles ? "F1" : "F0";
            }
            else
            {
                bool useKilometers = distance >= 1000f;
                if (useKilometers)
                    distance *= MetersToKilometers;

                unit = useKilometers ? "km" : "m";
                format = useKilometers ? "F1" : "F0";
            }

            UpdateDistanceTemplate();

            if (!distance.TryFormat(numberBuffer, out int length, format))
                return;

            distanceBuilder.Clear();

            for (int i = 0; i < distanceTemplate.Length; i++)
            {
                char character = distanceTemplate[i];
                if (character == NumberMarker[0])
                    distanceBuilder.Append(numberBuffer, 0, length);
                else if (character == UnitMarker[0])
                    distanceBuilder.Append(unit);
                else
                    distanceBuilder.Append(character);
            }

            indicatorObject.SetDistanceTextBuffer(distanceBuilder);
        }

        void UpdateDistanceTemplate()
        {
            if (distanceTemplate != null && cachedDistanceFormat == distanceFormat)
                return;

            cachedDistanceFormat = distanceFormat;
            string format = distanceFormat ?? "{0}{1}";

            // Parse only when the format changes, matching the slider text-buffer pattern
            try { distanceTemplate = string.Format(format, NumberMarker, UnitMarker); }
            catch (FormatException) { distanceTemplate = NumberMarker + UnitMarker; }
        }

        Vector2 CalculateIndicatorPosition(Vector3 targetScreenPosition, bool isBehindCamera, bool isOnScreen, Camera projectionCamera)
        {
            // A root canvas rect read costs more the larger its hierarchy, and a centered pivot keeps the center at zero
            Vector2 canvasPivot = targetCanvasRect.pivot;
            canvasCenter = canvasPivot.x == 0.5f && canvasPivot.y == 0.5f ? Vector2.zero : targetCanvasRect.rect.center;

            if (isOnScreen)
                return GetCanvasPosition(targetScreenPosition);

            // Perspective projection reverses points behind the camera. Orthographic projection does not.
            if (isBehindCamera && !projectionCamera.orthographic)
            {
                Vector2 cameraCenter = projectionCamera.pixelRect.center;
                targetScreenPosition.x = 2f * cameraCenter.x - targetScreenPosition.x;
                targetScreenPosition.y = 2f * cameraCenter.y - targetScreenPosition.y;
            }

            Vector2 center = screenBoundary.center;
            Vector2 direction = ((Vector2)targetScreenPosition - center).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.down;

            Rect indicatorScreenRect = GetScreenRect(indicatorRect, canvasCamera);
            float offset = Mathf.Max(0f, borderOffset) + Mathf.Max(indicatorScreenRect.width, indicatorScreenRect.height) * 0.5f;
            Vector2 borderPosition = GetBorderPosition(center, direction, offset);
            Vector2 localDirection = GetCanvasPosition(center + direction) - GetCanvasPosition(center);
            float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            indicatorObject.SetArrowRotation(angle - 90f);

            return GetCanvasPosition(borderPosition);
        }

        Vector2 GetCanvasPosition(Vector2 screenPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetCanvasRect, screenPosition, canvasCamera, out Vector2 position))
                return position - canvasCenter;

            return currentPosition;
        }

        Vector2 GetBorderPosition(Vector2 center, Vector2 direction, float offset)
        {
            // Collapse oversized padding to the center instead of producing inverted bounds
            Vector2 halfSize = screenBoundary.size * 0.5f;
            halfSize.x = Mathf.Max(0f, halfSize.x - offset);
            halfSize.y = Mathf.Max(0f, halfSize.y - offset);

            float horizontal = Mathf.Abs(direction.x) > 0.0001f ? halfSize.x / Mathf.Abs(direction.x) : float.MaxValue;
            float vertical = Mathf.Abs(direction.y) > 0.0001f ? halfSize.y / Mathf.Abs(direction.y) : float.MaxValue;
            return center + direction * Mathf.Min(horizontal, vertical);
        }

        public void SetTarget(Transform newTarget)
        {
            targetTransform = newTarget != null ? newTarget : transform;
            isDisabling = false;
            hasPosition = false;
            positionVelocity = Vector2.zero;

            if (newTarget != null && !enabled)
                enabled = true;
        }

        public void SetDistanceUnit(DistanceUnit unit) => distanceUnit = unit;

        public void SetRectBoundary(RectTransform boundary) => rectBoundary = boundary;

        public void Disable()
        {
            if (isDisabling)
                return;

            if (indicatorObject == null || !indicatorObject.gameObject.activeInHierarchy)
            {
                enabled = false;
                return;
            }

            // Fade from the displayed alpha, including any distance fade already applied
            indicatorObject.StopFade();
            indicatorObject.VisibilityAlpha = indicatorObject.Alpha;
            isDisabling = true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            checkDistance = Mathf.Max(0f, checkDistance);
            borderOffset = Mathf.Max(0f, borderOffset);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
            fadeStartDistance = Mathf.Max(0f, fadeStartDistance);
            fadeEndDistance = Mathf.Clamp(fadeEndDistance, 0f, fadeStartDistance);
        }
#endif
    }
}