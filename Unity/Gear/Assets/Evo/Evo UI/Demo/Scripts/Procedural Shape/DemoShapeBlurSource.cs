using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Backdrop Blur samples what the camera renders, and the demo canvases are all overlays.
    /// This keeps camera-rendered copies of its graphics, at the same screen rects and clipped to the same area,
    /// so a blurred panel on top shows them. The copies only exist while this object is enabled.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeBlurSource : MonoBehaviour
    {
        [Tooltip("The graphics to copy.")]
        [SerializeField] private Graphic[] graphics;
        [Tooltip("The copies are clipped to this rect, like the originals under their mask.")]
        [SerializeField] private RectTransform clipArea;
        [Tooltip("The root canvas of the graphics, for its event camera.")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private float planeDistance = 5f;

        GameObject copyRoot;
        Canvas copyCanvas;
        CanvasGroup copyGroup;
        RectTransform copyClip;
        readonly List<RectTransform> copies = new();
        readonly Vector3[] corners = new Vector3[4];

        void OnEnable()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (copyRoot == null)
                Build();

            copyCanvas.worldCamera = camera;
            copyCanvas.planeDistance = planeDistance;
            copyRoot.SetActive(true);
            Follow();
        }

        void OnDisable()
        {
            if (copyRoot != null)
                copyRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (copyRoot != null)
                Destroy(copyRoot);
        }

        void LateUpdate() => Follow();

        void Build()
        {
            copyRoot = new GameObject("Blur Source (Runtime)", typeof(RectTransform)) { hideFlags = HideFlags.DontSave };
            copyCanvas = copyRoot.AddComponent<Canvas>();
            copyCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            copyGroup = copyRoot.AddComponent<CanvasGroup>();
            copyGroup.blocksRaycasts = false;

            copyClip = (RectTransform)new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D)) { hideFlags = HideFlags.DontSave }.transform;
            copyClip.SetParent(copyRoot.transform, false);

            // Each graphic is copied on its own, without children or other components, and placed in screen space
            foreach (Graphic graphic in graphics)
            {
                GameObject copy = Instantiate(graphic.gameObject, copyClip);
                copy.hideFlags = HideFlags.DontSave;
                for (int i = copy.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(copy.transform.GetChild(i).gameObject);
                foreach (Component component in copy.GetComponents<Component>())
                    if (component is not Transform && component is not CanvasRenderer && component is not Graphic)
                        Destroy(component);

                var rect = (RectTransform)copy.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                copies.Add(rect);
            }
        }

        void Follow()
        {
            if (copyRoot == null || !copyRoot.activeSelf)
                return;

            Camera sourceCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Place(copyClip, clipArea, sourceCamera);
            for (int i = 0; i < copies.Count; i++)
            {
                copies[i].gameObject.SetActive(graphics[i].isActiveAndEnabled);
                Place(copies[i], graphics[i].rectTransform, sourceCamera);
            }

            // Fade with the page, so the blur fades with what it shows
            copyGroup.alpha = graphics.Length > 0 ? graphics[0].canvasRenderer.GetInheritedAlpha() : 1f;
        }

        // Screen rect of the original, as a centered rect in the copy's parent
        void Place(RectTransform copy, RectTransform original, Camera sourceCamera)
        {
            original.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[2]);

            var parent = (RectTransform)copy.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, min, copyCanvas.worldCamera, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, max, copyCanvas.worldCamera, out Vector2 localMax);
            copy.anchoredPosition = (localMin + localMax) * 0.5f;
            copy.sizeDelta = localMax - localMin;
        }
    }
}