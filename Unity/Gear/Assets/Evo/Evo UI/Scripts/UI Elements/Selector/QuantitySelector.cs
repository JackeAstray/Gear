using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Quantity Selector"), HelpURL(Constants.HelpUrl + "ui-elements/quantity-selector")]
    public class QuantitySelector : MonoBehaviour
    {
        // Settings
        [SerializeField] private int startQuantity = 1;
        [Min(0)] public int minQuantity = 1;
        [Min(0)] public int maxQuantity = 99;

        // Animation
        public bool useUnscaledTime = true;
        public float slideOffset = 40f;
        public float animationDuration = 0.2f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [TextInput] public MonoBehaviour inputField;
        public Button increaseButton;
        public Button decreaseButton;

        // Events
        public UnityEvent<int> onValueChanged = new();

        // Properties
        public int CurrentQuantity { get; private set; }

        // Animation State
        Action completionCallback;
        TweenHandle currentAnimation;
        static readonly Action<QuantitySelector, float> TweenUpdateCallback = ApplyTweenValue;

        // Runtime Cache
        TextBackend inputBackend;
        TextInputListener inputListener;
        RectTransform textRectTransform;
        Graphic quantityText;
        Graphic outgoingText;

        // Runtime State
        int? pendingQuantity;
        int lastNotifiedQuantity;
        string quantityString;
        float outgoingAlpha;
        Color baseTextColor;
        Vector2 originalTextPosition;
        Vector2 outgoingStartPosition;
        Vector2 slideDirection;

        void Awake()
        {
            inputBackend = TextProvider.FindInput(inputField);
            quantityText = inputBackend?.GetTextComponent(inputField);

            if (quantityText == null)
            {
                Debug.LogError("[Quantity Selector] Input Field or its Text Component is missing!", this);
                enabled = false;
                return;
            }

            textRectTransform = quantityText.rectTransform;
            completionCallback = ResetAnimation;
            inputListener = new TextInputListener
            {
                onValueChanged = OnTextInputChanged,
                onEndEdit = OnTextInputEndEdit,
                onSelect = StopAnimation
            };

            int initialQuantity = pendingQuantity ?? startQuantity;
            pendingQuantity = null;
            SetQuantity(initialQuantity, false);
        }

        void OnEnable()
        {
            if (quantityText == null)
                return;

            if (increaseButton != null)
                increaseButton.onClick.AddListener(Increase);
            if (decreaseButton != null)
                decreaseButton.onClick.AddListener(Decrease);

            inputListener.Bind(inputField);
        }

        void OnDisable()
        {
            StopAnimation();

            if (increaseButton != null)
                increaseButton.onClick.RemoveListener(Increase);
            if (decreaseButton != null)
                decreaseButton.onClick.RemoveListener(Decrease);

            inputListener?.Unbind();
        }

        void OnDestroy()
        {
            StopAnimation();

            if (outgoingText != null)
                Destroy(outgoingText.gameObject);
        }

        void OnTextInputChanged(string text)
        {
            StopAnimation();

            // Keep typing unrestricted; clamp and notify when editing finishes
            if (int.TryParse(text, out int result))
            {
                CurrentQuantity = result;
                UpdateButtonsState();
            }
        }

        void OnTextInputEndEdit(string text) => SetQuantity(int.TryParse(text, out int result) ? result : CurrentQuantity, false);

        void UpdateButtonsState()
        {
            if (increaseButton != null)
                increaseButton.SetInteractable(CurrentQuantity < maxQuantity);
            if (decreaseButton != null)
                decreaseButton.SetInteractable(CurrentQuantity > minQuantity);
        }

        void UpdateLimits()
        {
            minQuantity = Mathf.Max(0, minQuantity);
            maxQuantity = Mathf.Max(minQuantity, maxQuantity);

            int digits = 1;
            for (int value = maxQuantity; value >= 10; value /= 10)
                digits++;

            inputBackend.SetCharacterLimit(inputField, digits);
        }

        void PrepareAnimation(bool isIncreasing)
        {
            bool wasAnimating = currentAnimation.IsActive;
            currentAnimation.Kill();

            if (!wasAnimating)
            {
                originalTextPosition = textRectTransform.anchoredPosition;
                baseTextColor = quantityText.color;
            }

            // Keep the more visible layer on rapid clicks instead of snapping back to full opacity
            bool keepOutgoing = wasAnimating && outgoingText != null && outgoingText.color.a > quantityText.color.a;
            EnsureOutgoingText();

            if (!keepOutgoing)
            {
                TextProvider.SetText(outgoingText, inputBackend.GetValue(inputField));
                outgoingStartPosition = textRectTransform.anchoredPosition;
                outgoingAlpha = quantityText.color.a;
            }
            else
            {
                outgoingStartPosition = outgoingText.rectTransform.anchoredPosition;
                outgoingAlpha = outgoingText.color.a;
            }

            slideDirection = new Vector2(0f, slideOffset * (isIncreasing ? 1f : -1f));
            outgoingText.gameObject.SetActive(true);
        }

        void EnsureOutgoingText()
        {
            if (outgoingText != null)
                return;

            // A display-only copy, as the input's own text may carry the field's editing components
            outgoingText = TextProvider.Find(quantityText).Duplicate(quantityText, textRectTransform.parent);
            outgoingText.gameObject.name = "OutgoingText";
            outgoingText.gameObject.hideFlags = HideFlags.DontSave;
            outgoingText.raycastTarget = false;
        }

        static void ApplyTweenValue(QuantitySelector selector, float progress) => selector.ApplyAnimation(progress);

        void ApplyAnimation(float progress)
        {
            if (quantityText == null || outgoingText == null)
                return;

            float t = Mathf.Clamp01(progress);
            outgoingText.rectTransform.anchoredPosition = Vector2.Lerp(outgoingStartPosition, originalTextPosition + slideDirection, t);
            textRectTransform.anchoredPosition = originalTextPosition - slideDirection * (1f - t);

            Color color = baseTextColor;
            color.a = outgoingAlpha * (1f - Mathf.Clamp01(progress * 2f));
            outgoingText.color = color;
            color.a = baseTextColor.a * t;
            quantityText.color = color;
        }

        void StopAnimation()
        {
            currentAnimation.Kill();
            ResetAnimation();
        }

        void ResetAnimation()
        {
            if (outgoingText != null && outgoingText.gameObject.activeSelf)
            {
                if (quantityText != null)
                {
                    textRectTransform.anchoredPosition = originalTextPosition;
                    quantityText.color = baseTextColor;
                }

                outgoingText.gameObject.SetActive(false);
            }

            currentAnimation = default;
        }

        public void Increase()
        {
            if (CurrentQuantity < maxQuantity)
                SetQuantity(CurrentQuantity + 1);
        }

        public void Decrease()
        {
            if (CurrentQuantity > minQuantity)
                SetQuantity(CurrentQuantity - 1);
        }

        public void SetQuantity(int value, bool animate = true)
        {
            // Applied in Awake, for selectors that start inactive
            if (inputBackend == null)
            {
                pendingQuantity = value;
                return;
            }

            UpdateLimits();

            int oldQuantity = CurrentQuantity;
            int newQuantity = Mathf.Clamp(value, minQuantity, maxQuantity);
            bool playAnimation = animate && oldQuantity != newQuantity && animationDuration > 0f &&
                isActiveAndEnabled && quantityText != null && quantityText.isActiveAndEnabled && !inputBackend.IsFocused(inputField);

            if (playAnimation)
                PrepareAnimation(newQuantity > oldQuantity);
            else if (!animate || oldQuantity != newQuantity)
                StopAnimation();

            CurrentQuantity = newQuantity;

            // Reuse unchanged values. Input fields still allocate their own copy on changes
            if (quantityString == null || lastNotifiedQuantity != newQuantity)
                quantityString = newQuantity.ToString();

            inputBackend.SetValue(inputField, quantityString, false);
            UpdateButtonsState();

            if (playAnimation)
            {
                ApplyAnimation(0f);
                currentAnimation = EvoTween.Create(0f, 1f, animationDuration)
                    .WithEase(Ease.Custom, animationCurve)
                    .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                    .OnComplete(completionCallback)
                    .Bind(this, TweenUpdateCallback, 0x51534C44);

                if (!currentAnimation.IsActive)
                    ResetAnimation();
            }

            if (lastNotifiedQuantity != CurrentQuantity)
            {
                lastNotifiedQuantity = CurrentQuantity;
                onValueChanged?.Invoke(CurrentQuantity);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            minQuantity = Mathf.Max(0, minQuantity);
            maxQuantity = Mathf.Max(maxQuantity, minQuantity);
            startQuantity = Mathf.Clamp(startQuantity, minQuantity, maxQuantity);

            if (!Application.isPlaying && TextProvider.IsInput(inputField))
                TextProvider.SetValue(inputField, startQuantity.ToString(), false);
        }
#endif
    }
}