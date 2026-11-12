using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    /// <summary>
    /// Applies a smooth, continuous swaying effect to a UI element based on cursor positioning.
    /// </summary>
    [AddComponentMenu("Evo/UI/Effects/Rect Sway")]
    [HelpURL(Constants.HelpUrl + "effects/rect-sway")]
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public class RectSway : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Settings")]
        public bool useUnscaledTime = true;
        public bool enableXAxis = true;
        public bool enableYAxis = true;
        [Range(1f, 30f)] public float smoothness = 10f;
        [Range(0.1f, 5f)] public float swayIntensity = 1f;

        [Header("References")]
        public RectTransform swayObject;
        [SerializeField] private Canvas canvas;

        // Properties
        public bool AllowSway { get; set; }

        // Cache
        Camera targetCam;
        Vector3 defaultPos;

        // State
        bool isInitialized;
        Vector3 targetPos;
        Vector3 currentVelocity;

        void Start()
        {
            if (swayObject == null)
                swayObject = GetComponent<RectTransform>();
          
            if (canvas == null)
                canvas = swayObject.GetComponentInParent<Canvas>();

            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                targetCam = canvas.rootCanvas.worldCamera;
                if (targetCam == null)
                    targetCam = Camera.main;
            }

            defaultPos = swayObject.anchoredPosition3D;
            targetPos = defaultPos;
            isInitialized = true;
        }

        void OnEnable() => ResetSway();

        void Update()
        {
            if (!isInitialized || swayObject == null)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;          
            if (deltaTime <= 0f)
                return;

            UpdateInput();
            UpdateSwayPosition(deltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData) => AllowSway = true;

        public void OnPointerExit(PointerEventData eventData) => AllowSway = false;

        void UpdateInput()
        {
            targetPos = defaultPos;

            if (!AllowSway || canvas == null)
                return;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(swayObject,
                Utilities.GetPointerPosition(), targetCam, out Vector3 worldPos))
            {
                Vector3 offset = worldPos - swayObject.position;
                Transform parent = swayObject.parent;
                if (parent != null)
                    offset = parent.InverseTransformVector(offset);

                Vector3 currentPos = swayObject.anchoredPosition3D;
                if (enableXAxis)
                    targetPos.x = currentPos.x + offset.x * swayIntensity;
                if (enableYAxis)
                    targetPos.y = currentPos.y + offset.y * swayIntensity;
            }
        }

        void UpdateSwayPosition(float deltaTime)
        {
            Vector3 currentPos = swayObject.anchoredPosition3D;
            Vector3 nextPos = Vector3.SmoothDamp(currentPos, targetPos, ref currentVelocity,
                1f / Mathf.Max(0.0001f, smoothness), Mathf.Infinity, deltaTime);

            // Snap the return to rest so idle elements stop dirtying the canvas
            if (!AllowSway && (nextPos - defaultPos).sqrMagnitude < 0.000001f && currentVelocity.sqrMagnitude < 0.000001f)
            {
                nextPos = defaultPos;
                currentVelocity = Vector3.zero;
            }

            if (!nextPos.Equals(currentPos))
                swayObject.anchoredPosition3D = nextPos;
        }

        public void ResetSway()
        {
            // OnEnable runs before Start has captured the initial position.
            if (!isInitialized || swayObject == null)
                return;

            swayObject.anchoredPosition3D = defaultPos;
            targetPos = defaultPos;
            currentVelocity = Vector3.zero;
        }

        #region Obsolete API
        [System.Obsolete("Use AllowSway instead.")]
        public void SetSwayState(bool enabled) => AllowSway = enabled;
        #endregion
    }
}