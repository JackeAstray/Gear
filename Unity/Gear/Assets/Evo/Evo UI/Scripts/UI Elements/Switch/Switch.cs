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
    [AddComponentMenu("Evo/UI/UI Elements/Switch"), HelpURL(Constants.HelpUrl + "ui-elements/switch")]
    public class Switch : Interactive
    {
        // Settings
        [SerializeField, Tooltip("Sets whether the switch is on or off.")]
        private bool isOn;

        // Animation
        [Range(0f, 3f)] public float handleDuration = 0.2f;
        public AnimationCurve handleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [SerializeField] private RectTransform switchHandle;
        public CanvasGroup[] offCG;
        public CanvasGroup[] onCG;

        // Events
        public UnityEvent<bool> onValueChanged = new();

        // Cache
        readonly List<TweenHandle> currentStateAnimations = new();

        // Helpers
        TweenHandle currentHandleAnimation;
        float handleProgress;
        static readonly Action<Switch, float> SetHandleProgressCallback = (target, progress) => target.SetHandleProgress(progress);

        public bool IsOn
        {
            get => isOn;
            set => SetValue(value);
        }

        protected override void Awake()
        {
            base.Awake();

            if (switchHandle != null)
                SetHandlePositionInstant();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateStates();
            UpdateState(false);
        }

        protected override void OnDisable()
        {
            StopAnimations();
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            // Layout groups and stretched anchors resize the switch after Awake and OnEnable
            if (currentHandleAnimation.IsActive)
                UpdateHandlePosition();
            else
                SetHandlePositionInstant();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnPointerClick(eventData);

            if (IsInteractable())
                Toggle();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if (IsInteractable())
                UpdateStates();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);

            if (IsInteractable())
                Toggle();
        }

        void UpdateState(bool animate)
        {
            currentHandleAnimation.Kill();
            currentHandleAnimation = default;

            if (switchHandle == null)
                return;

            if (!animate || !Application.isPlaying || handleDuration <= 0f)
            {
                SetHandlePositionInstant();
                return;
            }

            // Animating progress instead of a position keeps the handle on its edge if the switch resizes mid-animation
            bool hasCustomCurve = handleCurve != null && handleCurve.length > 0;
            currentHandleAnimation = EvoTween.Create(handleProgress, isOn ? 1f : 0f, handleDuration)
                .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? handleCurve : null)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, SetHandleProgressCallback, 0x535748);
        }

        void UpdateStates()
        {
            if (!gameObject.activeInHierarchy)
                return;

            StopStateAnimations();

            float duration = Mathf.Max(0f, transitionDuration);
            bool animate = Application.isPlaying && duration > 0f;

            UpdateCanvasGroups(offCG, isOn ? 0f : 1f, duration, animate);
            UpdateCanvasGroups(onCG, isOn ? 1f : 0f, duration, animate);
        }

        void SetHandlePositionInstant() => SetHandleProgress(isOn ? 1f : 0f);

        void SetHandleProgress(float progress)
        {
            handleProgress = progress;
            UpdateHandlePosition();
        }

        void UpdateHandlePosition()
        {
            if (switchHandle == null)
                return;

            var parentRect = switchHandle.parent as RectTransform;
            if (parentRect == null)
                return;

            Rect parent = parentRect.rect;
            float handleWidth = switchHandle.rect.width;
            float handlePivotX = switchHandle.pivot.x;

            // Special case: full-width handles normally won't move (xMin and xMax math cancels out).
            // By capping the calculation width to half the parent's width, it perfectly slides
            // exactly 50% of the parent width while keeping the exact same edge-snapping workflow.
            float effectiveWidth = handleWidth >= parent.width - 0.1f ? parent.width * 0.5f : handleWidth;
            float offPosition = parent.xMin + (effectiveWidth * handlePivotX);
            float onPosition = parent.xMax - (effectiveWidth * (1f - handlePivotX));

            // Weighted sum lands exactly on either edge at 0 and 1
            Vector2 position = switchHandle.anchoredPosition;
            position.x = (offPosition * (1f - handleProgress)) + (onPosition * handleProgress);
            switchHandle.anchoredPosition = position;
        }

        void UpdateCanvasGroups(CanvasGroup[] groups, float targetAlpha, float duration, bool animate)
        {
            if (groups == null)
                return;

            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup canvasGroup = groups[i];
                if (canvasGroup == null)
                    continue;

                if (!animate || Mathf.Approximately(canvasGroup.alpha, targetAlpha))
                {
                    canvasGroup.alpha = targetAlpha;
                    continue;
                }

                TweenHandle animation = EvoTween.Create(canvasGroup.alpha, targetAlpha, duration)
                    .WithEase(Ease.OutQuad)
                    .WithTimeMode(TimeMode.Unscaled)
                    .BindToAlpha(canvasGroup);

                if (animation.IsActive)
                    currentStateAnimations.Add(animation);
            }
        }

        void StopStateAnimations()
        {
            for (int i = 0; i < currentStateAnimations.Count; i++)
            {
                TweenHandle animation = currentStateAnimations[i];
                animation.Kill();
            }

            currentStateAnimations.Clear();
        }

        void StopAnimations()
        {
            currentHandleAnimation.Kill();
            currentHandleAnimation = default;
            StopStateAnimations();
        }

        public void SetValue(bool value) => SetValue(value, true);

        public void SetValue(bool value, bool invokeEvents)
        {
            if (isOn == value)
                return;

            isOn = value;

            // Inactive switches catch up on enable, but listeners still hear the change
            if (gameObject.activeInHierarchy)
            {
                UpdateState(true);
                UpdateStates();

                if (isOn)
                    AudioManager.PlayClip(Styler.GetAudio(styleProvider, selectedSFX, stylerPreset));
            }

            if (invokeEvents)
                onValueChanged?.Invoke(isOn);
        }

        public void Toggle() => SetValue(!isOn);

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
                UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
            }
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            SetHandlePositionInstant();
            UpdateCanvasGroups(offCG, isOn ? 0f : 1f, 0f, false);
            UpdateCanvasGroups(onCG, isOn ? 1f : 0f, 0f, false);
        }
#endif
    }
}