using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Input Field Enhancer"), HelpURL(Constants.HelpUrl + "ui-elements/input-field")]
    public class InputFieldEnhancer : MonoBehaviour, IStylerHandler
    {
        // Settings
        public bool clearAfterSubmit = false;
        [Tooltip("Deselects the input field from the EventSystem when editing ends.")]
        public bool deselectOnEndEdit = false;
        [Tooltip("Shift+Enter inserts a line break at the caret. Enter submits. TextMesh Pro only; UniText fields do this with a Submit Key behavior set to Enter.")]
        public bool handleShiftEnter = false;
        [Tooltip("Offset while selected or containing text. Zero disables sliding.")]
        public Vector2 slideOffset = new(0, 20);
        [Range(0.1f, 3f), Tooltip("Scale while selected or containing text. One disables scaling.")]
        public float scaleMultiplier = 0.8f;
        [Range(0f, 1f), Tooltip("Opacity multiplier while selected or containing text.")]
        public float fadeAlpha = 1f;
        [Range(0f, 5f)] public float animationDuration = 0.15f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [TextInput] public MonoBehaviour source;
        public Interactive interactableObject;

        // Events
        public UnityEvent<string> onSubmit = new();

        // Cache
        TextBackend backend;
        TextInputListener sourceListener;
        TMP_InputField tmpSource;
        InputAction shiftEnterAction;
        Graphic placeholderGraphic;
        StylerObject placeholderStyler;
        RectTransform placeholderRect;
        CanvasGroup placeholderGroup;
        TMP_InputField.LineType originalLineType;
        Event newlineEvent;

        // Runtime State
        bool originalSelectAll;
        bool isSelected;

        // Animation State
        TweenHandle currentAnimation;
        static readonly Action<InputFieldEnhancer, float> TweenUpdateCallback = ApplyPlaceholder;
        Vector2 originalPlaceholderPosition;
        Vector3 originalPlaceholderScale;
        float originalPlaceholderAlpha;
        Vector2 startPosition;
        Vector2 targetPosition;
        Vector3 startScale;
        Vector3 targetScale;
        float startAlpha;
        float targetAlpha;
        bool placeholderActive;
        bool animateSlide;
        bool animateScale;
        bool animateAlpha;

        bool HasText => !backend.ValueEquals(source, ReadOnlySpan<char>.Empty);

        void Awake()
        {
            if (source == null)
                TextProvider.TryGetInput(this, out source);

            backend = TextProvider.FindInput(source);
            if (backend == null)
            {
                Debug.LogError("[Input Field Enhancer] Input Field is missing!", this);
                return;
            }

            tmpSource = source as TMP_InputField;
            sourceListener = new TextInputListener
            {
                onValueChanged = OnSourceValueChanged,
                onSubmit = OnSubmit,
                onEndEdit = OnEndEdit,
                onSelect = OnSourceSelect,
                onDeselect = OnSourceDeselect,

                // Values set without notification still change whether the placeholder floats
                onTextUpdated = RefreshPlaceholder
            };
            sourceListener.Bind(source);
            placeholderGraphic = backend.GetPlaceholder(source);

            if (placeholderGraphic != null)
            {
                placeholderGraphic.TryGetComponent(out placeholderStyler);
                placeholderRect = placeholderGraphic.rectTransform;

                // Visibility multiplies the styled color without taking ownership of it
                if (!placeholderGraphic.TryGetComponent(out placeholderGroup))
                    placeholderGroup = placeholderGraphic.gameObject.AddComponent<CanvasGroup>();
            }

            if (interactableObject != null)
            {
                interactableObject.OnStateChanged += OnInteractableStateChanged;
                interactableObject.onSelect.AddListener(OnInteractableSelect);
            }
        }

        void OnEnable()
        {
            if (backend == null)
                return;

            isSelected = backend.IsFocused(source) || Utilities.GetSelectedObject() == source.gameObject;

            if (handleShiftEnter && tmpSource != null)
            {
                originalLineType = tmpSource.lineType;
                originalSelectAll = tmpSource.onFocusSelectAll;
                tmpSource.lineType = Utilities.WasShiftKeyPressed()
                    ? TMP_InputField.LineType.MultiLineNewline
                    : TMP_InputField.LineType.MultiLineSubmit;
                tmpSource.onFocusSelectAll = false;

                if (shiftEnterAction == null)
                {
                    shiftEnterAction = new InputAction(type: InputActionType.PassThrough);
                    shiftEnterAction.AddBinding("<Keyboard>/shift");
                    shiftEnterAction.AddBinding("<Keyboard>/enter");
                    shiftEnterAction.AddBinding("<Keyboard>/numpadEnter");
                    shiftEnterAction.performed += OnShiftEnter;
                }

                shiftEnterAction.Enable();
            }

            if (placeholderGraphic != null)
            {
                originalPlaceholderPosition = placeholderRect.anchoredPosition;
                originalPlaceholderScale = placeholderRect.localScale;
                originalPlaceholderAlpha = placeholderGroup.alpha;

                // The field would hide the placeholder while it has text, so it is detached while it animates
                backend.SetPlaceholder(source, null);
                AnimatePlaceholder(isSelected || HasText, true);
            }
        }

        void OnDisable()
        {
            currentAnimation.Kill();
            currentAnimation = default;
            Canvas.preWillRenderCanvases -= FocusPending;
            shiftEnterAction?.Disable();
            isSelected = false;

            if (placeholderRect != null)
            {
                placeholderRect.anchoredPosition = originalPlaceholderPosition;
                placeholderRect.localScale = originalPlaceholderScale;
            }

            if (placeholderGroup != null)
                placeholderGroup.alpha = originalPlaceholderAlpha;

            if (source == null || backend == null)
                return;

            if (handleShiftEnter && tmpSource != null)
            {
                tmpSource.lineType = originalLineType;
                tmpSource.onFocusSelectAll = originalSelectAll;
            }

            if (placeholderGraphic != null && backend.GetPlaceholder(source) == null)
                backend.SetPlaceholder(source, placeholderGraphic);
        }

        void OnDestroy()
        {
            shiftEnterAction?.Dispose();
            sourceListener?.Unbind();

            if (interactableObject != null)
            {
                interactableObject.OnStateChanged -= OnInteractableStateChanged;
                interactableObject.onSelect.RemoveListener(OnInteractableSelect);
            }
        }

        void OnShiftEnter(InputAction.CallbackContext context)
        {
            if (!isActiveAndEnabled || !handleShiftEnter || tmpSource == null)
                return;

            var keyboard = (Keyboard)context.control.device;
            var lineType = keyboard.shiftKey.isPressed ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.MultiLineSubmit;
            if (tmpSource.lineType != lineType)
                tmpSource.lineType = lineType;

            // TMP converts Shift+Enter to a vertical tab, then rejects that control character
            if (lineType == TMP_InputField.LineType.MultiLineNewline && tmpSource.isFocused && context.ReadValueAsButton()
                && (context.control == keyboard.enterKey || context.control == keyboard.numpadEnterKey))
            {
                newlineEvent ??= new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\n' };
                tmpSource.ProcessEvent(newlineEvent);
                tmpSource.ForceLabelUpdate();
            }
        }

        void OnSourceValueChanged(string _) => RefreshPlaceholder();

        void RefreshPlaceholder()
        {
            if (!isActiveAndEnabled || source == null)
                return;

            AnimatePlaceholder(isSelected || backend.IsFocused(source) || HasText, false);
        }

        void OnSubmit(string value)
        {
            if (!isActiveAndEnabled || source == null)
                return;

            onSubmit?.Invoke(value);

            if (clearAfterSubmit && source != null && HasText)
                backend.SetValue(source, string.Empty, true);
        }

        void OnEndEdit(string _)
        {
            if (isActiveAndEnabled && source != null && deselectOnEndEdit && Utilities.GetSelectedObject() == source.gameObject)
                Utilities.SetSelectedObject(null);
        }

        void OnSourceSelect()
        {
            if (!isActiveAndEnabled)
                return;

            isSelected = true;
            RefreshPlaceholder();

            if (interactableObject != null)
                interactableObject.SetState(InteractionState.Selected);
        }

        void OnSourceDeselect()
        {
            if (!isActiveAndEnabled || source == null)
                return;

            isSelected = false;
            RefreshPlaceholder();

            if (interactableObject != null)
                interactableObject.SetState(backend.IsInteractable(source) ? InteractionState.Normal : InteractionState.Disabled);
        }

        void OnInteractableSelect()
        {
            if (!isActiveAndEnabled)
                return;

            // Defer focus until the EventSystem has finished selecting the interactable
            Canvas.preWillRenderCanvases -= FocusPending;
            Canvas.preWillRenderCanvases += FocusPending;
        }

        void FocusPending()
        {
            Canvas.preWillRenderCanvases -= FocusPending;
            if (interactableObject != null && Utilities.GetSelectedObject() == interactableObject.gameObject)
                Focus();
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (!isActiveAndEnabled || source == null)
                return;

            backend.SetInteractable(source, newState != InteractionState.Disabled);
        }

        void AnimatePlaceholder(bool active, bool instant = false)
        {
            if (placeholderRect == null || placeholderGroup == null || (!instant && placeholderActive == active))
                return;

            placeholderActive = active;
            currentAnimation.Kill();
            currentAnimation = default;

            startPosition = placeholderRect.anchoredPosition;
            startScale = placeholderRect.localScale;
            startAlpha = placeholderGroup.alpha;
            targetPosition = active ? originalPlaceholderPosition + slideOffset : originalPlaceholderPosition;
            targetScale = active ? originalPlaceholderScale * scaleMultiplier : originalPlaceholderScale;
            targetAlpha = active ? originalPlaceholderAlpha * fadeAlpha : originalPlaceholderAlpha;
            animateSlide = startPosition != targetPosition;
            animateScale = startScale != targetScale;
            animateAlpha = !Mathf.Approximately(startAlpha, targetAlpha);

            if (!animateSlide && !animateScale && !animateAlpha)
                return;

            if (instant || animationDuration <= 0f) { ApplyPlaceholder(this, 1f); }
            else
            {
                currentAnimation = EvoTween.Create(0f, 1f, animationDuration)
                    .WithTimeMode(TimeMode.Unscaled).Bind(this, TweenUpdateCallback, 0);
            }
        }

        static void ApplyPlaceholder(InputFieldEnhancer enhancer, float progress)
        {
            float t = progress >= 1f ? 1f : enhancer.animationCurve != null ? enhancer.animationCurve.Evaluate(progress) : progress;

            if (enhancer.placeholderRect != null)
            {
                if (enhancer.animateSlide)
                    enhancer.placeholderRect.anchoredPosition = Vector2.LerpUnclamped(enhancer.startPosition, enhancer.targetPosition, t);
                if (enhancer.animateScale)
                    enhancer.placeholderRect.localScale = Vector3.LerpUnclamped(enhancer.startScale, enhancer.targetScale, t);
            }

            if (enhancer.animateAlpha && enhancer.placeholderGroup != null)
                enhancer.placeholderGroup.alpha = Mathf.Clamp01(Mathf.LerpUnclamped(enhancer.startAlpha, enhancer.targetAlpha, t));
        }

        public void Focus()
        {
            if (!isActiveAndEnabled || source == null || backend == null || !source.isActiveAndEnabled || !backend.IsInteractable(source))
                return;

            backend.Focus(source);
        }

        public void SetInteractable(bool value)
        {
            if (source != null && backend != null)
                backend.SetInteractable(source, value);
            if (interactableObject != null)
                interactableObject.SetInteractable(value);
        }

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => placeholderStyler != null ? placeholderStyler.Preset : null;
            set
            {
                if (placeholderStyler != null)
                    placeholderStyler.Preset = value;
            }
        }

        public void UpdateStyler()
        {
            if (placeholderStyler != null)
                placeholderStyler.UpdateStyler();
        }
        #endregion

        #region Obsolete API
        [System.Obsolete("Placeholder visuals now refresh automatically when focus or text changes.")]
        public void UpdatePlaceholderState() => RefreshPlaceholder();

        [System.Obsolete("Placeholder visuals now refresh automatically when focus or text changes.")]
        public void AnimatePlaceholder(bool animate) => AnimatePlaceholder(animate, false);
        #endregion
    }
}