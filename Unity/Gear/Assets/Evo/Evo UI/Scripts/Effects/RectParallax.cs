using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Applies a subtle parallax depth effect to a UI element based on the pointer's location.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Effects/Rect Parallax"), HelpURL(Constants.HelpUrl + "effects/rect-parallax")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Evo.UI", null, "RectDepth")]
    public class RectParallax : MonoBehaviour
    {
        [Header("Settings")]
        public bool useUnscaledTime = true;
        [Range(0.01f, 0.5f), Tooltip("Lower values = faster response. Higher values = smoother/slower following.")]
        public float followSpeed = 0.1f;
        [Range(0.01f, 1f), Tooltip("Distance multiplier for the parallax effect.")]
        public float depthMultiplier = 0.1f;

        [Header("References")]
        [UnityEngine.Serialization.FormerlySerializedAs("targetRect")]
        public RectTransform depthObject;
        [UnityEngine.Serialization.FormerlySerializedAs("targetCanvas")]
        [SerializeField] private Canvas canvas;

        // Cache
        RectTransform canvasRect;
        Camera targetCam;

        // State
        Vector2 currentVelocity;

        void Start()
        {
            if (depthObject == null)
                depthObject = GetComponent<RectTransform>();
           
            if (canvas == null)
                canvas = depthObject.GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                canvasRect = canvas.transform as RectTransform;
                if (canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    targetCam = canvas.rootCanvas.worldCamera;
                    if (targetCam == null)
                        targetCam = Camera.main;
                }
            }
        }

        void OnEnable() => currentVelocity = Vector2.zero;

        void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (canvas == null || depthObject == null || deltaTime <= 0f)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                Utilities.GetPointerPosition(), targetCam, out Vector2 mousePos))
            {
                Vector2 targetPosition = mousePos * depthMultiplier;
                Vector2 currentPosition = depthObject.anchoredPosition;
                Vector2 nextPosition = Vector2.SmoothDamp(currentPosition, targetPosition,
                    ref currentVelocity, followSpeed, Mathf.Infinity, deltaTime);

                // Avoid dirtying the UI transform when the position hasn't changed
                if (!nextPosition.Equals(currentPosition))
                    depthObject.anchoredPosition = nextPosition;
            }
        }
    }
}