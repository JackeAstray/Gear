using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "ui-elements/progress-bar")]
    [AddComponentMenu("Evo/UI/UI Elements/Progress Bar")]
    public class ProgressBar : MonoBehaviour
    {
        // Bar Settings
        [SerializeField] private float value = 50f;
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;

        // Settings
        [Tooltip("Invert the visual fill amount: minimum is full and maximum is empty. Value and text are unchanged.")]
        public bool invertFill;
        [Min(0f)] public float displayMultiplier = 1f;
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed0;
        public string textFormat = "{0}";

        // Animation
        public bool enableSmoothing = true;
        [Range(0.05f, 4f)] public float smoothingDuration = 0.3f;
        public AnimationCurve smoothingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // References
        public RectTransform fillRect;
        [TextComponent] public Graphic valueText;

        // Events
        public UnityEvent<float> onValueChanged = new();

        // Cache
        RectTransform cachedFillRect;
        ProceduralShape fillShape;
        Image fillImage;

        // Last-resort RectTransform fallback for backwards compatibility.
        // Keep its original axis cached so shrinking the fill cannot change the direction.
        int fallbackFillAxis;

        // Text Cache
        const string NumberMarker = "\u0001";
        char[] numberBuffer = new char[64];
        char[] textBuffer = new char[64];
        string cachedTemplate;
        string lastTextFormat;
        int lastTextLength;
        Graphic cachedValueText;

        // Runtime State
        float currentVisualValue;
        TweenHandle valueAnimation;
        Action<float> updateCallback;
        Action completeCallback;

        void OnEnable()
        {
            CacheFillComponents(true);
            cachedValueText = null;
            minValue = Mathf.Min(minValue, maxValue);
            Set(value, false, true);
        }

        void OnDisable()
        {
            valueAnimation.Kill();
            valueAnimation = default;
        }

#if UNITY_EDITOR
        void OnDestroy() => UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif

        void Set(float newValue, bool sendCallback = true, bool instant = false)
        {
            if (float.IsNaN(newValue))
                return;

            float clampedValue = Mathf.Clamp(newValue, minValue, maxValue);
            if (value == clampedValue && !instant)
                return;

            value = clampedValue;
            valueAnimation.Kill();
            valueAnimation = default;

            if (!instant && enableSmoothing && Application.isPlaying && isActiveAndEnabled &&
                smoothingDuration > 0f && !float.IsInfinity(smoothingDuration) && currentVisualValue != value)
            {
                updateCallback ??= UpdateDisplay;
                completeCallback ??= CompleteAnimation;
                valueAnimation = EvoTween.Create(currentVisualValue, value, smoothingDuration)
                    .WithEase(Ease.Custom, smoothingCurve)
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(completeCallback)
                    .Bind(updateCallback);
            }
            else
            {
                UpdateDisplay(value);
            }

            if (sendCallback)
                onValueChanged?.Invoke(value);
        }

        void CompleteAnimation()
        {
            valueAnimation = default;
            UpdateDisplay(value);
        }

        void UpdateDisplay(float displayValue)
        {
            currentVisualValue = Mathf.Clamp(displayValue, minValue, maxValue);
            UpdateFill();
            UpdateText();
        }

        void CacheFillComponents(bool force = false)
        {
            bool referenceChanged = cachedFillRect != fillRect;
            if (!force && !referenceChanged)
                return;

            cachedFillRect = fillRect;
            fillShape = null;
            fillImage = null;

            if (fillRect == null)
                return;

            fillRect.TryGetComponent(out fillShape);

            if (fillShape == null)
                fillRect.TryGetComponent(out fillImage);

            // Infer the last-resort axis once, from anchors and the fill area rather than the fill's value-driven size
            if (referenceChanged)
                fallbackFillAxis = Utilities.GetFillAxis(fillRect);
        }

        void UpdateFill()
        {
            CacheFillComponents();

            if (fillRect == null)
                return;

            float normalizedValue = Mathf.InverseLerp(minValue, maxValue, currentVisualValue);
            if (invertFill)
                normalizedValue = 1f - normalizedValue;

            if (fillShape != null && fillShape.ClippingMethod != ProceduralShape.ClipMethod.None)
                fillShape.ClipAmount = normalizedValue;
            else if (fillImage != null && fillImage.type == Image.Type.Filled)
                fillImage.fillAmount = normalizedValue;
            else
            {
                // Last resort for older bars without a clipped Shape or Filled Image
                Vector2 anchor = fillRect.anchorMax;
                anchor[fallbackFillAxis] = normalizedValue;
                if (fillRect.anchorMax != anchor) { fillRect.anchorMax = anchor; }
            }
        }

        void UpdateText()
        {
            if (valueText == null)
                return;

            if (cachedTemplate == null || lastTextFormat != textFormat)
            {
                lastTextFormat = textFormat;
                string format = string.IsNullOrEmpty(textFormat) ? "{0}" : textFormat;
                if (!format.Contains("{0}"))
                    format += " {0}";

                try { cachedTemplate = string.Format(format, NumberMarker); }
                catch (FormatException) { cachedTemplate = textFormat + " " + NumberMarker; }
            }

            int numberLength;
            while (!(currentVisualValue * displayMultiplier).TryFormat(numberBuffer, out numberLength, Utilities.GetNumberFormatString(displayFormat)))
                Array.Resize(ref numberBuffer, numberBuffer.Length * 2);

            int textLength = 0;
            bool changed = cachedValueText != valueText;

            for (int i = 0; i < cachedTemplate.Length; i++)
            {
                ReadOnlySpan<char> part = cachedTemplate[i] == NumberMarker[0]
                    ? numberBuffer.AsSpan(0, numberLength)
                    : cachedTemplate.AsSpan(i, 1);

                if (textLength + part.Length > textBuffer.Length)
                    Array.Resize(ref textBuffer, Mathf.Max(textBuffer.Length * 2, textLength + part.Length));

                Span<char> destination = textBuffer.AsSpan(textLength, part.Length);
                changed |= !part.SequenceEqual(destination);
                part.CopyTo(destination);
                textLength += part.Length;
            }

            if (changed || lastTextLength != textLength)
                TextProvider.SetText(valueText, textBuffer, 0, textLength);

            lastTextLength = textLength;
            cachedValueText = valueText;
        }

        #region Public API
        public float Value
        {
            get => value;
            set => Set(value);
        }

        public float MinValue
        {
            get => minValue;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || minValue == value)
                    return;

                minValue = Mathf.Min(value, maxValue);
                Set(this.value, this.value < minValue, true);
            }
        }

        public float MaxValue
        {
            get => maxValue;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || maxValue == value)
                    return;

                maxValue = Mathf.Max(value, minValue);
                Set(this.value, this.value > maxValue, true);
            }
        }


        [Obsolete("Use the Value property instead.")]
        public void SetValue(float newValue) => Value = newValue;

        public void SetValueWithoutNotify(float newValue) => Set(newValue, false);

        public void SetValueInstant(float newValue) => Set(newValue, true, true);
        #endregion

#if UNITY_EDITOR
        void OnValidate()
        {
            if (minValue > maxValue)
                minValue = maxValue;

            value = Mathf.Clamp(value, minValue, maxValue);
            displayMultiplier = Mathf.Clamp(displayMultiplier, -1000, 1000);
            smoothingDuration = Mathf.Max(smoothingDuration, 0.01f);

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            CacheFillComponents(true);
            cachedValueText = null;
            Set(value, false, true);
        }
#endif
    }
}