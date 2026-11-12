using System;
using UnityEngine;
using UnityEngine.Events;
using Evo.Tween;
using Evo.Tween.Modules.Transforms;
using Evo.Tween.Modules.UGUI;

namespace Evo.UI
{
    /// <summary>
    /// Handles the logic and animations for showing/hiding a UI popover.
    /// </summary>
    [AddComponentMenu("Evo/UI/Animation/Popover"), HelpURL(Constants.HelpUrl)]
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class Popover : MonoBehaviour
    {
        // Settings
        public bool isOpen = false;
        [Tooltip("Disable or enable GameObject based on the popover state.")]
        public bool manageGameObjectState = true;
        public bool closeOnOutsideClick = true;
        public bool closeOnEscapeKey = true;

        // Animations
        public TweenSequence openAnimation = CreateAnimation(true, Vector3.one);
        public TweenSequence closeAnimation = CreateAnimation(false, Vector3.one);

        // Events
        public UnityEvent<bool> onStateChanged = new();

        // Cache
        CanvasGroup canvasGroup;
        RectTransform rectTransform;
        Canvas parentCanvas;
        Action openComplete;
        Action closeComplete;

        // Runtime State
        bool isInitialized;
        int openedFrame;
        TweenHandle currentAnimation;
        TweenSequenceSampler stateSampler;

        void Awake() => Initialize();

        void OnEnable() => openedFrame = Time.frameCount;

        void OnDisable()
        {
            bool wasAnimating = currentAnimation.IsActive;
            StopAnimation();

            if (!isInitialized || (!isOpen && !wasAnimating))
                return;

            isOpen = false;
            ApplyState(false);
        }

        void OnDestroy()
        {
            StopAnimation();
            stateSampler?.Dispose();
        }

        void OnTransformParentChanged() => parentCanvas = GetComponentInParent<Canvas>();

        void LateUpdate()
        {
            if (!isOpen || Time.frameCount == openedFrame)
                return;

            if (closeOnEscapeKey && Utilities.WasEscapeKeyPressed())
            {
                Close();
                return;
            }

            // Process release after UI callbacks so the trigger can toggle without reopening on the same click
            if (closeOnOutsideClick && Utilities.WasPointerReleased() && !IsPointerOverPopover())
                Close();
        }

        void Initialize()
        {
            if (isInitialized)
                return;

            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            openComplete = CompleteOpen;
            closeComplete = CompleteClose;
            stateSampler = new TweenSequenceSampler(gameObject, null);
            isInitialized = true;
            SetStateImmediate(isOpen);
        }

        bool IsPointerOverPopover()
        {
            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();

            Canvas rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
            Camera eventCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;

            if (rootCanvas != null && rootCanvas.renderMode == RenderMode.WorldSpace && eventCam == null)
                eventCam = Camera.main;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Utilities.GetPointerPosition(), eventCam);
        }

        bool AnimatesAlpha(TweenSequence animation)
        {
            if (animation == null)
                return false;

            for (int i = 0; i < animation.Count; i++)
            {
                if (animation.Clips[i]?.Settings.Module is CanvasGroupAlphaModule 
                    && (animation.GetTarget(i) == null || animation.GetTarget(i) == canvasGroup))
                    return true;
            }

            return false;
        }

        void SetInteraction(bool value)
        {
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        void StopAnimation()
        {
            currentAnimation.Kill();
            currentAnimation = default;
        }

        void ApplyState(bool open)
        {
            TweenSequence animation = open ? openAnimation : closeAnimation;
            stateSampler.SetSource(gameObject, animation);

            // Apply modular clip endpoints without scheduling a tween or retaining captured targets
            try { stateSampler.Sample(stateSampler.Duration); }
            finally { stateSampler.Release(); }

            canvasGroup.alpha = open ? 1f : 0f;
            SetInteraction(open);
        }

        void CompleteOpen()
        {
            currentAnimation = default;
            canvasGroup.alpha = 1f;
            SetInteraction(true);
        }

        void CompleteClose()
        {
            currentAnimation = default;
            canvasGroup.alpha = 0f;
            SetInteraction(false);

            if (manageGameObjectState)
                gameObject.SetActive(false);
        }

        static TweenSequence CreateAnimation(bool open, Vector3 scale)
        {
            TweenSequence animation = new() { TimeMode = TimeMode.Unscaled };
            TweenClip fadeClip = new();
            fadeClip.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = true, to = open ? 1f : 0f };
            fadeClip.Settings.Duration = 0.1f;
            fadeClip.Settings.Ease = Ease.OutCubic;
            animation.InlineClips.Add(fadeClip);

            TweenClip scaleClip = new();
            scaleClip.Settings.Module = new TransformScaleModule { fromCurrent = true, to = open ? scale : scale * 0.9f };
            scaleClip.Settings.Duration = open ? 0.15f : 0.1f;
            scaleClip.Settings.Ease = Ease.OutCubic;
            animation.InlineClips.Add(scaleClip);
            return animation;
        }

        /// <summary>
        /// Toggles the popover visibility based on its current state.
        /// </summary>
        public void Toggle()
        {
            if (isOpen) { Close(); }
            else { Open(); }
        }

        /// <summary>
        /// Opens the popover with the configured animation.
        /// </summary>
        public void Open()
        {
            Initialize();

            if (isOpen || !enabled)
                return;

            StopAnimation();
            isOpen = true;
            openedFrame = Time.frameCount;

            if (manageGameObjectState)
                gameObject.SetActive(true);

            // Activation callbacks may have closed or destroyed the popover
            if (this == null || !isOpen)
                return;

            SetInteraction(true);

            if (!isActiveAndEnabled) { ApplyState(true); }
            else
            {
                if (!AnimatesAlpha(openAnimation))
                    canvasGroup.alpha = 1f;

                currentAnimation = openAnimation != null ? openAnimation.Trigger(gameObject).OnComplete(openComplete) : default;
                if (!currentAnimation.IsActive)
                    CompleteOpen();
            }

            onStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Closes the popover with the configured animation.
        /// </summary>
        public void Close()
        {
            Initialize();

            if (!isOpen)
                return;

            StopAnimation();
            isOpen = false;
            SetInteraction(false);

            if (!isActiveAndEnabled) { SetStateImmediate(false); }
            else
            {
                currentAnimation = closeAnimation != null ? closeAnimation.Trigger(gameObject).OnComplete(closeComplete) : default;
                if (!currentAnimation.IsActive)
                    CompleteClose();
            }

            onStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Forces the popover to immediately snap open or closed without animations or events.
        /// </summary>
        public void SetStateImmediate(bool open)
        {
            Initialize();
            StopAnimation();
            isOpen = open;
            openedFrame = Time.frameCount;
            ApplyState(open);

            if (manageGameObjectState)
                gameObject.SetActive(open);
        }

#if UNITY_EDITOR
        void Reset()
        {
            openAnimation = CreateAnimation(true, transform.localScale);
            closeAnimation = CreateAnimation(false, transform.localScale);
        }
#endif
    }
}