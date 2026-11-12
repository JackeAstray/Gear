using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Scene intro. The timeline lives in the Tween Animator; this component covers what a timeline can't:
    /// it shows the first frame at once but runs it only after loading hitches pass, then steps it itself so a later freeze
    /// holds the intro for a moment instead of skipping part of it, holds Home's own intro until its cue,
    /// spreads the dock background over the screen so the timeline can fold it back into the dock,
    /// keeps it as opaque as the intro until the fold brings it to its own alpha,
    /// starts the dock's Animated Container and turns the intro off once everything has settled.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoIntro : MonoBehaviour
    {
        [SerializeField] private TweenAnimator timeline;

        [Header("Home")]
        [SerializeField] private TweenAnimator homeIntro;
        [SerializeField, Min(0f)] private float homeDelay = 2f;

        [Header("Dock")]
        [SerializeField] private ProceduralShape dockBackground;
        [SerializeField] private AnimatedContainer dockItems;

        // Frames slower than this are loading hitches, which would jump the start of the timeline ahead
        const float SteadyDeltaTime = 0.1f;
        const float MaxWait = 1f;
        // Longest step the timeline takes in one frame. Frames up to this length play in real time
        const float MaxStep = 0.05f;

        readonly Vector3[] corners = new Vector3[4];
        ProceduralShape.ColorMode savedFillMode;
        Color savedFillColor, dockFill;
        float spreadHeight, dockHeight;
        float startTime, time;
        bool running, homeReleased, dockFading;

        void OnEnable()
        {
            if (homeIntro != null)
                homeIntro.Started += HoldHome;
        }

        void OnDisable()
        {
            if (homeIntro != null)
                homeIntro.Started -= HoldHome;

            // Never leave Home frozen if the intro stops early
            ReleaseHome();
            RestoreDockFill();
        }

        void Start()
        {
            timeline.Play();
            timeline.Sample(0f);
            timeline.Pause();
            startTime = Time.time;

            // Home may have started before this component subscribed
            if (homeIntro != null && homeIntro.IsPlaying)
                HoldHome(homeIntro);
        }

        void Update()
        {
            if (!running)
            {
                if (Time.deltaTime > SteadyDeltaTime && Time.time - startTime < MaxWait)
                    return;

                running = true;
                SpreadDockBackground();

                if (dockItems != null)
                    dockItems.enabled = true;

                return;
            }

            // The timeline stays paused and steps here, so one slow frame can't move it further than MaxStep
            if (timeline.IsPaused)
            {
                time += Mathf.Min(Time.deltaTime, MaxStep);
                if (time < timeline.Duration)
                    timeline.Sample(time);
                else
                    timeline.Complete();
            }

            if (dockFading)
                FadeDockFill();

            if (!homeReleased && timeline.CurrentTime >= homeDelay)
                ReleaseHome();

            if (timeline.State == PlaybackState.Completed && (dockItems == null || !dockItems.IsAnimating))
                gameObject.SetActive(false);
        }

        void HoldHome(TweenAnimator animator)
        {
            if (homeReleased)
                return;

            animator.Sample(0f);
            animator.Pause();
        }

        void ReleaseHome()
        {
            if (homeReleased)
                return;

            homeReleased = true;

            if (homeIntro != null && homeIntro.IsPaused)
                homeIntro.Resume();
        }

        /// <summary>
        /// Stretches the dock background over the whole canvas. Top and bottom go on the anchors and the sides on the offsets,
        /// so the timeline folds them on separate clips that end on the saved rect.
        /// </summary>
        void SpreadDockBackground()
        {
            if (dockBackground == null)
                return;

            RectTransform background = dockBackground.rectTransform;
            var dock = (RectTransform)background.parent;
            if (dock.rect.height <= 0f)
                LayoutRebuilder.ForceRebuildLayoutImmediate(dock);

            Rect rect = dock.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var canvas = (RectTransform)dockBackground.canvas.rootCanvas.transform;
            canvas.GetWorldCorners(corners);
            Vector2 min = dock.InverseTransformPoint(corners[0]);
            Vector2 max = dock.InverseTransformPoint(corners[2]);

            Vector2 anchorMin = background.anchorMin, anchorMax = background.anchorMax;
            Vector2 offsetMin = background.offsetMin, offsetMax = background.offsetMax;
            dockHeight = background.rect.height;

            background.anchorMin = new Vector2(anchorMin.x, (min.y - rect.yMin - offsetMin.y) / rect.height);
            background.anchorMax = new Vector2(anchorMax.x, (max.y - rect.yMin - offsetMax.y) / rect.height);
            background.offsetMin = new Vector2(min.x - rect.xMin - anchorMin.x * rect.width, offsetMin.y);
            background.offsetMax = new Vector2(max.x - rect.xMin - anchorMax.x * rect.width, offsetMax.y);

            spreadHeight = background.rect.height;
            MakeDockFillOpaque();
        }

        /// <summary>
        /// The intro under the spread background is opaque, so the background takes over at full alpha.
        /// Its resolved fill moves to a custom color whose alpha follows the fold, and the original fill returns when the intro ends.
        /// </summary>
        void MakeDockFillOpaque()
        {
            savedFillMode = dockBackground.fillColorMode;
            if (savedFillMode != ProceduralShape.ColorMode.Styler && savedFillMode != ProceduralShape.ColorMode.Custom)
                return;

            savedFillColor = dockBackground.FillColor;
            dockFill = savedFillMode == ProceduralShape.ColorMode.Styler
                ? dockBackground.fillStyler.ResolveColor(dockBackground.Preset, InteractionState.Normal, false) : savedFillColor;

            dockBackground.fillColorMode = ProceduralShape.ColorMode.Custom;
            dockBackground.SetMaterialDirty();
            dockFading = true;
            FadeDockFill();
        }

        void FadeDockFill()
        {
            float fold = Mathf.InverseLerp(spreadHeight, dockHeight, dockBackground.rectTransform.rect.height);
            Color fill = dockFill;
            fill.a = Mathf.Lerp(1f, dockFill.a, fold);
            dockBackground.FillColor = fill;
        }

        void RestoreDockFill()
        {
            if (!dockFading)
                return;

            dockFading = false;
            if (dockBackground == null)
                return;

            dockBackground.fillColorMode = savedFillMode;
            dockBackground.FillColor = savedFillColor;
            dockBackground.UpdateStyler();
        }
    }
}