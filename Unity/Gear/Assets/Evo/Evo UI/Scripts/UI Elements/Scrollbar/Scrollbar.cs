using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Scrollbar")]
    [HelpURL(Constants.HelpUrl + "ui-elements/scrollbar")]
    public class Scrollbar : UnityEngine.UI.Scrollbar
    {
        // Animation
        [Range(0.1f, 3f)] public float smoothDuration = 0.5f;
        public AnimationCurve smoothCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Auto Hide
        [Tooltip("Auto hide scrollbar in case of no interaction.")]
        public bool autoHide = false;
        [Range(0f, 1f)] public float hideAlpha = 0f;
        [Range(0.05f, 2f)] public float hideDuration = 0.2f;
        [Range(0.1f, 10f)] public float hideTimer = 1f;

        // References
        [SerializeField] private Button minArrow;
        [SerializeField] private Button maxArrow;

        // Runtime Cache
        CanvasGroup canvasGroup;

        // Runtime State
        bool isHovered;
        bool isPointerDown;
        bool autoHideActive;
        double hideTime = -1d;
        float fadeTarget;
        float lastActivityValue;
        float scrollStart;
        float scrollTarget;
        TweenHandle scrollAnimation;
        TweenHandle fadeAnimation;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying)
                return;

            lastActivityValue = value;

            if (minArrow != null)
                minArrow.onClick.AddListener(ScrollToMin);
            if (maxArrow != null)
                maxArrow.onClick.AddListener(ScrollToMax);

            onValueChanged.AddListener(OnValueChanged);

            UpdateAutoHide();
            HandleArrows(value);
        }

        protected override void OnDisable()
        {
            StopScrolling();
            fadeAnimation.Kill();
            fadeAnimation = default;

            // Unity's pointer-down repeat coroutine also needs to stop when only this component is disabled
            StopAllCoroutines();

            if (minArrow != null)
                minArrow.onClick.RemoveListener(ScrollToMin);
            if (maxArrow != null)
                maxArrow.onClick.RemoveListener(ScrollToMax);

            onValueChanged.RemoveListener(OnValueChanged);

            if (autoHideActive && canvasGroup != null)
                canvasGroup.alpha = 1f;

            isHovered = false;
            isPointerDown = false;
            autoHideActive = false;
            hideTime = -1d;

            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();

            if (!Application.isPlaying)
                return;

            if (autoHideActive != autoHide)
                UpdateAutoHide();

            if (minArrow != null || maxArrow != null)
                HandleArrows(value);

            if (!autoHideActive || hideTime < 0d)
                return;

            // Keep the full idle delay after interaction and fade-in, without requesting another fade each frame
            if (isHovered || isPointerDown || scrollAnimation.IsActive || (fadeTarget == 1f && fadeAnimation.IsActive))
                hideTime = Time.unscaledTimeAsDouble + Mathf.Max(0f, hideTimer);
            else if (Time.unscaledTimeAsDouble >= hideTime)
            {
                hideTime = -1d;
                lastActivityValue = value;
                FadeTo(hideAlpha);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (handleRect == null)
                return;

            StopScrolling();
            isPointerDown = true;
            ShowScrollbar();
            base.OnPointerDown(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnPointerUp(eventData);
            isPointerDown = false;
            ShowScrollbar();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            isHovered = true;
            ShowScrollbar();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            isHovered = false;
            ShowScrollbar();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (IsActive() && IsInteractable())
                StopScrolling();

            base.OnMove(eventData);
        }

        public override void SetValueWithoutNotify(float input)
        {
            float previousValue = value;
            base.SetValueWithoutNotify(input);

            if (Application.isPlaying && IsActive() && previousValue != value)
                OnValueChanged(value);
        }

        void OnValueChanged(float val)
        {
            HandleArrows(val);

            // Require more movement to reveal a hiding scrollbar, so settling values cannot reverse its fade
            float threshold = hideTime < 0d ? 0.005f : 0.001f;
            if (Mathf.Abs(val - lastActivityValue) < threshold)
                return;

            lastActivityValue = val;
            ShowScrollbar();
        }

        void HandleArrows(float val)
        {
            if (minArrow == null && maxArrow == null)
                return;

            bool forward = direction == Direction.BottomToTop || direction == Direction.LeftToRight;
            bool canScroll = IsActive() && IsInteractable() && size < 1f;
            bool canMoveToZero = val > 0.001f;
            bool canMoveToOne = val < 1f - 0.001f;

            if (minArrow != null)
                minArrow.SetInteractable(canScroll && (forward ? canMoveToZero : canMoveToOne));
            if (maxArrow != null)
                maxArrow.SetInteractable(canScroll && (forward ? canMoveToOne : canMoveToZero));
        }

        void UpdateAutoHide()
        {
            if (autoHideActive == autoHide)
                return;

            autoHideActive = autoHide;
            fadeAnimation.Kill();
            fadeAnimation = default;
            hideTime = -1d;

            if (autoHideActive && canvasGroup == null && !TryGetComponent(out canvasGroup))
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = autoHideActive ? hideAlpha : 1f;
        }

        void ShowScrollbar()
        {
            if (!autoHide || !Application.isPlaying || !IsActive())
                return;

            UpdateAutoHide();
            hideTime = Time.unscaledTimeAsDouble + Mathf.Max(0f, hideTimer);
            FadeTo(1f);
        }

        void FadeTo(float alpha)
        {
            if (canvasGroup == null)
                return;

            alpha = Mathf.Clamp01(alpha);
            if (fadeAnimation.IsActive && fadeTarget == alpha)
                return;

            fadeAnimation.Kill();
            fadeAnimation = default;
            fadeTarget = alpha;

            if (hideDuration <= 0f || Mathf.Approximately(canvasGroup.alpha, alpha))
            {
                canvasGroup.alpha = alpha;
                return;
            }

            fadeAnimation = EvoTween.Create(canvasGroup.alpha, alpha, hideDuration)
                .WithEase(Ease.OutQuad)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(canvasGroup);
        }

        void StopScrolling()
        {
            scrollAnimation.Kill();
            scrollAnimation = default;
        }

        void ApplyScroll(float progress)
        {
            // Preserve clamped curve interpolation and always finish at the requested endpoint
            float t = progress >= 1f ? 1f : smoothCurve != null && smoothCurve.length > 0 ? smoothCurve.Evaluate(progress) : progress;
            value = Mathf.Lerp(scrollStart, scrollTarget, t);
        }

        /// <summary>
        /// Smoothly scroll to the min position.
        /// </summary>
        public void ScrollToMin()
        {
            float target = (direction == Direction.BottomToTop || direction == Direction.LeftToRight) ? 0 : 1;
            ScrollTo(target, false, smoothDuration);
        }

        /// <summary>
        /// Smoothly scroll to the max position.
        /// </summary>
        public void ScrollToMax()
        {
            float target = (direction == Direction.BottomToTop || direction == Direction.LeftToRight) ? 1 : 0;
            ScrollTo(target, false, smoothDuration);
        }

        /// <summary>
        /// Smoothly scroll to a specific position with custom duration.
        /// </summary>
        public void ScrollTo(float targetValue, bool clamp, float duration)
        {
            if (float.IsNaN(targetValue) || float.IsInfinity(targetValue))
                return;

            targetValue = clamp ? Mathf.Clamp01(targetValue) : targetValue;
            StopScrolling();

            if (!Application.isPlaying || !IsActive() || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                value = targetValue;
                return;
            }

            if (value == targetValue)
                return;

            scrollStart = value;
            scrollTarget = targetValue;
            ShowScrollbar();

            scrollAnimation = EvoTween.Create(0f, 1f, duration)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (target, progress) => target.ApplyScroll(progress), 0x45565342);
        }

        /// <summary>
        /// Smoothly scroll to a normalized value. The physical position depends on direction.
        /// </summary>
        public void ScrollTo(float targetValue) => ScrollTo(targetValue, false, smoothDuration);

        /// <summary>
        /// Smoothly scroll by a relative amount.
        /// </summary>
        public void ScrollBy(float amount) => ScrollTo(value + amount);

        /// <summary>
        /// Smoothly scroll by a relative amount with custom duration.
        /// </summary>
        public void ScrollBy(float amount, bool clamp, float duration) => ScrollTo(value + amount, clamp, duration);
    }
}