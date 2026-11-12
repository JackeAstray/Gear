using Evo.Tween;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Off Screen Indicator tab: three targets drift in and out of a small map, with you at its center.
    /// The targets are plain transforms measured in meters, seen through a camera that never renders: it only projects
    /// the map onto the viewport, so distances read the same at any resolution. The markers on the map follow the targets.
    /// </summary>
    [AddComponentMenu(""), DefaultExecutionOrder(-100)]
    public class DemoOffScreenIndicator : MonoBehaviour
    {
        const int MoveXKey = 1;
        const int MoveYKey = 2;

        [System.Serializable]
        public struct Target
        {
            public OffScreenIndicator indicator;
            public RectTransform marker;
            [Tooltip("Meters each axis travels to either side. A negative value starts on the other side.")]
            public Vector2 range;
            public Vector2 period;
        }

        [SerializeField] private GameObject world;
        [SerializeField] private Camera view;
        [SerializeField] private RectTransform viewport;
        [Tooltip("The root canvas, for its event camera.")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Target[] targets;
        [SerializeField] private float pixelsPerMeter = 10;

        readonly Vector3[] corners = new Vector3[4];
        TweenHandle[] moves;

        void OnEnable()
        {
            world.SetActive(true);

            if (moves == null)
            {
                StartMoves();
                return;
            }

            foreach (TweenHandle move in moves)
                move.Resume();
        }

        void OnDisable()
        {
            // The map's targets live outside the canvas, so they leave with the tab
            if (world != null)
                world.SetActive(false);

            if (moves == null)
                return;

            foreach (TweenHandle move in moves)
                move.Pause();
        }

        void StartMoves()
        {
            moves = new TweenHandle[targets.Length * 2];

            for (int i = 0; i < targets.Length; i++)
            {
                Transform target = targets[i].indicator.transform;
                Vector2 range = targets[i].range;
                Vector2 period = targets[i].period;

                moves[i * 2] = EvoTween.Create(-range.x, range.x, period.x)
                    .WithEase(Ease.InOutSine)
                    .WithLoops(0, LoopMode.Yoyo)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(target, static (t, x) => t.localPosition = new Vector3(x, t.localPosition.y, 0), MoveXKey);
                moves[i * 2 + 1] = EvoTween.Create(-range.y, range.y, period.y)
                    .WithEase(Ease.InOutSine)
                    .WithLoops(0, LoopMode.Yoyo)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(target, static (t, y) => t.localPosition = new Vector3(t.localPosition.x, y, 0), MoveYKey);
            }
        }

        // Before the indicators' LateUpdate, so they project through this frame's viewport
        void LateUpdate()
        {
            viewport.GetWorldCorners(corners);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            view.pixelRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            view.orthographicSize = viewport.rect.height / pixelsPerMeter / 2;

            for (int i = 0; i < targets.Length; i++)
                targets[i].marker.anchoredPosition = (Vector2)targets[i].indicator.transform.localPosition * pixelsPerMeter;
        }

        // None, Metric or Imperial
        public void SetUnit(int index)
        {
            foreach (Target target in targets)
                target.indicator.SetDistanceUnit((OffScreenIndicator.DistanceUnit)index);
        }

        public void SetHideOnScreen(bool value)
        {
            foreach (Target target in targets)
                target.indicator.hideWhenOnScreen = value;
        }

        public void SetDistanceFade(bool value)
        {
            foreach (Target target in targets)
                target.indicator.enableDistanceFade = value;
        }

        public void SetEdgePadding(float value)
        {
            foreach (Target target in targets)
                target.indicator.borderOffset = value;
        }

        public void SetSmoothing(float seconds)
        {
            foreach (Target target in targets)
                target.indicator.transitionEase = seconds;
        }

        public void SetRange(float meters)
        {
            foreach (Target target in targets)
                target.indicator.checkDistance = meters;
        }
    }
}
