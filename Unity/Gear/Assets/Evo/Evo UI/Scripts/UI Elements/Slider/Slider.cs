using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Slider"), HelpURL(Constants.HelpUrl + "ui-elements/slider")]
    public class Slider : UnityEngine.UI.Slider, IStylerInteractable
    {
        // References
        [TextComponent] public Graphic valueText;
        [TextInput] public MonoBehaviour valueInput;

        // Formatting
        public float displayMultiplier = 1f;
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed0;
        [Tooltip("Value label format. Use {0} to add the value.")]
        public string textFormat = "{0}";

        // Animation
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, 2f)] public float transitionDuration = 0.1f;
        [Range(0f, 2f)] public float highlightedScale = 1.25f;
        [Range(0f, 2f)] public float pressedScale = 1.15f;

        // Runtime State
        bool isPointerOver;
        bool isPressed;
        TweenHandle scaleAnimation;
        TweenHandle highlightAnimation;
        Vector3 originalScale = Vector3.one;
        Vector3 targetHandleScale;
        Action scaleCompleteCallback;

        // Text Cache
        const string NumberMarker = "\u0001";
        readonly StringBuilder textBuilder = new(64);
        char[] numberBuffer = new char[64];
        string cachedTemplate;
        string lastTextFormat;
        TextInputListener inputListener;

        protected override void Awake()
        {
            base.Awake();
            scaleCompleteCallback = CompleteHandleScale;

            if (Application.isPlaying)
                RaycastArea.ResolveRaycast(gameObject);
        }

        protected override void OnEnable()
        {
            if (Application.isPlaying && handleRect != null)
                originalScale = handleRect.localScale;

            base.OnEnable();
            isPointerOver = currentSelectionState == SelectionState.Selected || currentSelectionState == SelectionState.Highlighted;
            UpdateAnimationState(true);
        }

        protected override void Start()
        {
            base.Start();
            SetText(value);
            inputListener = new TextInputListener { onEndEdit = OnInputEndEdit };
            inputListener.Bind(valueInput);
        }

        protected override void OnDestroy()
        {
            inputListener?.Unbind();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
            base.OnDestroy();
        }

        protected override void InstantClearState()
        {
            base.InstantClearState();
            isPointerOver = false;
            isPressed = false;
            UpdateAnimationState(true);
        }

        protected override void Set(float input, bool sendCallback = true)
        {
            if (float.IsNaN(input))
                return;

            base.Set(input, sendCallback);
            SetText(value);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            InteractionState newState = state switch
            {
                SelectionState.Normal => InteractionState.Normal,
                SelectionState.Highlighted => InteractionState.Highlighted,
                SelectionState.Pressed => InteractionState.Pressed,
                SelectionState.Selected => InteractionState.Selected,
                SelectionState.Disabled => InteractionState.Disabled,
                _ => InteractionState.Normal
            };

            if (state == SelectionState.Disabled)
                isPressed = false;
           
            if (state == SelectionState.Disabled || currentInteractionState == InteractionState.Disabled)
                UpdateAnimationState(instant);

            if (currentInteractionState != newState)
            {
                currentInteractionState = newState;
                OnStateChanged?.Invoke(currentInteractionState);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            isPointerOver = true;
            UpdateAnimationState();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            isPointerOver = false;
            UpdateAnimationState();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnPointerDown(eventData);
            isPressed = true;
            UpdateAnimationState();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnPointerUp(eventData);
            isPressed = false;
            UpdateAnimationState();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            isPointerOver = true;
            UpdateAnimationState();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            isPointerOver = false;
            UpdateAnimationState();
        }

        void OnInputEndEdit(string text)
        {
            if (!IsActive() || !IsInteractable())
                return;

            if (float.TryParse(text, out float result) && !float.IsNaN(result) && !float.IsInfinity(result))
            {
                float rawValue = displayMultiplier != 0f ? result / displayMultiplier : result;
                value = rawValue;
            }

            SetText(value);
        }

        void UpdateAnimationState(bool instant = false)
        {
            if (!Application.isPlaying)
                return;

            scaleAnimation.Kill();
            highlightAnimation.Kill();
            scaleAnimation = default;
            highlightAnimation = default;

            bool canInteract = IsActive() && IsInteractable();
            float targetScale = !canInteract ? 1f : isPressed ? pressedScale : isPointerOver ? highlightedScale : 1f;
            float targetAlpha = canInteract && (isPressed || isPointerOver) ? 1f : 0f;

            if (handleRect != null)
            {
                targetHandleScale = originalScale * targetScale;

                if (instant || transitionDuration <= 0f) { CompleteHandleScale(); }
                else if (handleRect.localScale != targetHandleScale)
                {
                    scaleAnimation = EvoTween.Create(handleRect.localScale, targetHandleScale, transitionDuration)
                        .WithEase(Ease.Custom, animationCurve)
                        .WithTimeMode(TimeMode.Unscaled)
                        .OnComplete(scaleCompleteCallback)
                        .BindToScale(handleRect);
                }
            }
        }

        void CompleteHandleScale()
        {
            if (handleRect != null)
                handleRect.localScale = targetHandleScale;
        }

        public void SetText(float val)
        {
            TextBackend inputBackend = TextProvider.FindInput(valueInput);
            if (inputBackend == null && valueText == null)
                return;

            PrepareText(val);

            // The input's own text is set through the input
            if (valueText != null && (inputBackend == null || valueText != inputBackend.GetTextComponent(valueInput)))
                TextProvider.SetText(valueText, textBuilder);

            if (inputBackend == null)
                return;

            // Inputs require a string; allocate only when the displayed value changes
            int length = textBuilder.Length;
            Span<char> chars = length <= 256 ? stackalloc char[length] : new char[length];
            textBuilder.CopyTo(0, chars, length);

            if (!inputBackend.ValueEquals(valueInput, chars))
                inputBackend.SetValue(valueInput, textBuilder.ToString(), false);
        }

        void PrepareText(float val)
        {
            if (cachedTemplate == null || lastTextFormat != textFormat)
            {
                lastTextFormat = textFormat;
                string format = string.IsNullOrEmpty(textFormat) ? "{0}" : textFormat;
                if (!format.Contains("{0}"))
                    format += " {0}";

                try { cachedTemplate = string.Format(format, NumberMarker); }
                catch (FormatException) { cachedTemplate = textFormat + " " + NumberMarker; }
            }

            int length;
            while (!(val * displayMultiplier).TryFormat(numberBuffer, out length, Utilities.GetNumberFormatString(displayFormat)))
                Array.Resize(ref numberBuffer, numberBuffer.Length * 2);

            textBuilder.Clear();

            for (int i = 0; i < cachedTemplate.Length; i++)
            {
                if (cachedTemplate[i] == NumberMarker[0])
                    textBuilder.Append(numberBuffer, 0, length);
                else
                    textBuilder.Append(cachedTemplate[i]);
            }
        }

        /// <summary>
        /// Returns a new string. SetText uses the text provider's buffer API for the value label.
        /// </summary>
        public string GetFormattedText(float sValue)
        {
            PrepareText(sValue);
            return textBuilder.ToString();
        }

        #region IStylerInteractable Implementation
        public event Action<InteractionState> OnStateChanged;

        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = value;
        }

        public InteractionState InteractionState => currentInteractionState;

        InteractionState currentInteractionState = InteractionState.Normal;
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            SetText(value);
        }
#endif

        #region Obsolete API
        [Obsolete("Use GetFormattedText(sValue) instead.")]
        public string FormatValue(float sValue) => GetFormattedText(sValue);
        #endregion
    }
}