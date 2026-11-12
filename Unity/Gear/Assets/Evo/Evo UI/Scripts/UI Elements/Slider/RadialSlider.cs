using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Radial Slider"), HelpURL(Constants.HelpUrl + "ui-elements/radial-slider")]
    public class RadialSlider : Interactive, IDragHandler, IInitializePotentialDragHandler
    {
        // Slider Settings
        [SerializeField] private float minValue = 0f;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private float value = 25f;
        [SerializeField] private bool wholeNumbers;

        // Radial Settings
        [Range(10f, 360f)] public float angleRange = 360f;
        [Range(0f, 360f)] public float startAngle = 270f;
        [Range(-50f, 50f)] public float handleOffset = 0f;
        [Tooltip("Allow jumping between min and max values."), UnityEngine.Serialization.FormerlySerializedAs("allowContinuousDrag")]
        public bool continuousDrag = false;
        public bool clockwise = true;

        // Formatting
        public float displayMultiplier = 1f;
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed0;
        [Tooltip("Value label format. Use {0} to add the value.")]
        public string textFormat = "{0}";

        // References
        public RectTransform handleRect;
        public ProceduralShape fillImage;
        [TextComponent] public Graphic valueText;
        [TextInput] public MonoBehaviour valueInput;

        // Events
        public UnityEvent<float> onValueChanged = new();

        // Helpers
        RectTransform cachedRectTransform;
        float lastValue;
        float lastDragAngle;
        float dragAngle;
        float dragOvershoot;
        int? dragPointer;

        // Text Cache
        readonly StringBuilder textBuilder = new(64);
        char[] numberBuffer = new char[64];
        string cachedTemplate;
        string lastTextFormat;
        bool validTextFormat;
        TextInputListener inputListener;

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();

            inputListener ??= new TextInputListener { onEndEdit = OnInputEndEdit };
            inputListener.Bind(valueInput);
        }

        protected override void OnDisable()
        {
            dragPointer = null;
            base.OnDisable();
            inputListener?.Unbind();
        }

        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateVisuals();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            float previousValue = lastValue;
            Refresh();

            if (previousValue != value)
                onValueChanged?.Invoke(value);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!MayDrag(eventData) || dragPointer.HasValue)
                return;

            base.OnPointerDown(eventData);

            if (!MayDrag(eventData))
                return;

            NormalizeSettings();
            dragPointer = eventData.pointerId;
            dragAngle = NormalizedValue * angleRange;
            lastDragAngle = float.NaN;
            UpdateDrag(eventData, true);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || dragPointer != eventData.pointerId)
                return;

            dragPointer = null;
            base.OnPointerUp(eventData);
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (dragPointer != eventData.pointerId || !MayDrag(eventData))
                return;

            UpdateDrag(eventData);
        }

        public virtual void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        void UpdateDrag(PointerEventData eventData, bool pointerDown = false)
        {
            RectTransform orbitRect = GetOrbitRect();
            if (orbitRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(orbitRect,
                eventData.position, eventData.pressEventCamera, out Vector2 localCursor))
                return;

            localCursor -= orbitRect.rect.center;
            if (localCursor.sqrMagnitude < 0.0001f)
                return;

            float pointerAngle = Mathf.Atan2(localCursor.y, localCursor.x) * Mathf.Rad2Deg;
            float sliderAngle = Mathf.Repeat(clockwise ? startAngle - pointerAngle : pointerAngle - startAngle, 360f);
            float range = Mathf.Clamp(angleRange, 10f, 360f);
            pointerDown |= float.IsNaN(lastDragAngle);

            if (pointerDown || continuousDrag)
            {
                // Keep the handle at the selected endpoint when grabbing it at the seam
                bool onHandle = pointerDown && handleRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(handleRect, eventData.position, eventData.pressEventCamera);

                if (!onHandle)
                    dragAngle = sliderAngle <= range ? sliderAngle : sliderAngle - range < 360f - sliderAngle ? range : 0f;

                // Pressing in the gap leaves the pointer past a limit
                dragOvershoot = onHandle ? 0f : Mathf.DeltaAngle(dragAngle, sliderAngle);
            }
            else
            {
                // Past a limit, the handle waits until the pointer comes back to it
                float overshoot = dragOvershoot + Mathf.DeltaAngle(lastDragAngle, sliderAngle);
                if (dragOvershoot * overshoot > 0f)
                {
                    dragOvershoot = Mathf.DeltaAngle(0f, overshoot);
                }
                else
                {
                    // Track the unrounded angle so whole-number sliders can cross each step
                    float targetAngle = dragAngle + overshoot;
                    dragAngle = Mathf.Clamp(targetAngle, 0f, range);
                    dragOvershoot = targetAngle - dragAngle;
                }
            }

            lastDragAngle = sliderAngle;
            NormalizedValue = dragAngle / range;
        }

        void OnInputEndEdit(string text)
        {
            if (!IsActive() || !IsInteractable())
                return;

            float newValue = value;
            if (float.TryParse(text, out float inputValue) && IsFinite(inputValue) && displayMultiplier != 0f)
            {
                float rawValue = inputValue / displayMultiplier;
                if (IsFinite(rawValue))
                    newValue = rawValue;
            }

            SetValue(newValue, true, true);
        }

        void SetValue(float input, bool invokeEvents = true, bool forceUpdate = false)
        {
            if (!IsFinite(input))
                return;

            float newValue = ClampValue(input);
            bool changed = value != newValue;
            if (!changed && !forceUpdate)
                return;

            value = newValue;
            lastValue = newValue;
            UpdateVisuals();
            SetText(newValue);

            if (changed && invokeEvents)
                onValueChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Refreshes the range, visuals and text after changing public settings.
        /// </summary>
        public void Refresh()
        {
            NormalizeSettings();
            SetValue(IsFinite(value) ? value : minValue, false, true);
        }

        void NormalizeSettings()
        {
            minValue = IsFinite(minValue) ? minValue : 0f;
            maxValue = IsFinite(maxValue) ? maxValue : minValue;
           
            if (wholeNumbers)
            {
                minValue = Mathf.Round(minValue);
                maxValue = Mathf.Round(maxValue);
            }

            maxValue = Mathf.Max(minValue, maxValue);
            angleRange = IsFinite(angleRange) ? Mathf.Clamp(angleRange, 10f, 360f) : 360f;
            startAngle = IsFinite(startAngle) ? Mathf.Clamp(startAngle, 0f, 360f) : 270f;
            handleOffset = IsFinite(handleOffset) ? Mathf.Clamp(handleOffset, -50f, 50f) : 0f;
            displayMultiplier = IsFinite(displayMultiplier) ? Mathf.Clamp(displayMultiplier, -1000f, 1000f) : 1f;
        }

        void UpdateVisuals()
        {
            if (cachedRectTransform == null)
                cachedRectTransform = transform as RectTransform;

            float normalizedValue = NormalizedValue;
            float range = Mathf.Clamp(angleRange, 10f, 360f);

            if (handleRect != null && handleRect.parent is RectTransform handleParent)
            {
                float targetAngle = startAngle + (clockwise ? -1f : 1f) * normalizedValue * range;
                float radians = targetAngle * Mathf.Deg2Rad;
                Rect rect = handleParent.rect;
                float radius = Mathf.Min(rect.width, rect.height) * 0.5f + handleOffset;

                handleRect.localPosition = rect.center + new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
                handleRect.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            }

            if (fillImage != null)
            {
                fillImage.ClipAmount = normalizedValue * (range / 360f);
                fillImage.ClipClockwise = clockwise;
                fillImage.transform.localEulerAngles = new Vector3(0f, 0f, startAngle);
            }
        }

        void SetText(float val)
        {
            TextBackend inputBackend = TextProvider.FindInput(valueInput);
            if (inputBackend == null && valueText == null)
                return;

            int length = PrepareText(val);

            // The input's own text is set through the input
            if (valueText != null && (inputBackend == null || valueText != inputBackend.GetTextComponent(valueInput)))
                TextProvider.SetText(valueText, textBuilder);

            // Inputs require a string; allocate only when the displayed number changes
            if (inputBackend != null && !inputBackend.ValueEquals(valueInput, numberBuffer.AsSpan(0, length)))
                inputBackend.SetValue(valueInput, new string(numberBuffer, 0, length), false);
        }

        int PrepareText(float val)
        {
            if (cachedTemplate == null || lastTextFormat != textFormat)
            {
                lastTextFormat = textFormat;
                cachedTemplate = string.IsNullOrEmpty(textFormat) ? "{0}" : textFormat;

                try
                {
                    string.Format(cachedTemplate, string.Empty);
                    validTextFormat = true;
                }
                catch (FormatException) { validTextFormat = false; }
            }

            double displayed = val * (double)displayMultiplier;
            int length;
            while (!displayed.TryFormat(numberBuffer, out length, Utilities.GetNumberFormatString(displayFormat)))
                Array.Resize(ref numberBuffer, numberBuffer.Length * 2);

            textBuilder.Clear();
            if (!validTextFormat)
            {
                textBuilder.Append(cachedTemplate).Append(' ').Append(numberBuffer, 0, length);
                return length;
            }

            // Preserve composite-format alignment and escaped braces without creating strings
            for (int i = 0; i < cachedTemplate.Length; i++)
            {
                char character = cachedTemplate[i];
                if (character != '{' || cachedTemplate[i + 1] == '{')
                {
                    textBuilder.Append(character);
                    if (character == '{' || character == '}')
                        i++;

                    continue;
                }

                int end = cachedTemplate.IndexOf('}', i + 1);
                int colon = cachedTemplate.IndexOf(':', i + 1, end - i - 1);
                int comma = cachedTemplate.IndexOf(',', i + 1, (colon >= 0 ? colon : end) - i - 1);
                int alignment = 0;

                if (comma >= 0)
                {
                    int.TryParse(cachedTemplate.AsSpan(comma + 1, (colon >= 0 ? colon : end) - comma - 1),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out alignment);
                }

                if (colon >= 0)
                {
                    while (end + 1 < cachedTemplate.Length && cachedTemplate[end + 1] == '}')
                        end = cachedTemplate.IndexOf('}', end + 2);
                }

                int padding = Mathf.Max(0, Mathf.Abs(alignment) - length);
                if (alignment > 0)
                    textBuilder.Append(' ', padding);

                textBuilder.Append(numberBuffer, 0, length);
                if (alignment < 0)
                    textBuilder.Append(' ', padding);

                i = end;
            }

            return length;
        }

        // The handle circles its parent's center, so dragging measures angles around the same point
        RectTransform GetOrbitRect() => handleRect != null && handleRect.parent is RectTransform handleParent ? handleParent : cachedRectTransform;

        bool MayDrag(PointerEventData eventData) => IsActive() && IsInteractable() && eventData.button == PointerEventData.InputButton.Left;

        static bool IsFinite(float input) => !float.IsNaN(input) && !float.IsInfinity(input);

        float ClampValue(float input) => Mathf.Clamp(wholeNumbers ? Mathf.Round(input) : input, minValue, maxValue);

        #region Public API
        public float MinValue
        {
            get => minValue;
            set
            {
                if (!IsFinite(value) || minValue == value)
                    return;

                minValue = value;
                NormalizeSettings();
                SetValue(this.value, true, true);
            }
        }

        public float MaxValue
        {
            get => maxValue;
            set
            {
                if (!IsFinite(value) || maxValue == value)
                    return;

                maxValue = value;
                minValue = Mathf.Min(minValue, maxValue);
                NormalizeSettings();
                SetValue(this.value, true, true);
            }
        }

        public bool WholeNumbers
        {
            get => wholeNumbers;
            set
            {
                if (wholeNumbers == value)
                    return;

                wholeNumbers = value;
                NormalizeSettings();
                SetValue(this.value, true, true);
            }
        }

        public virtual float Value
        {
            get => value;
            set => SetValue(value);
        }

        public float NormalizedValue
        {
            get => minValue == maxValue ? 0f : (float)(((double)value - minValue) / ((double)maxValue - minValue));
            set => Value = (float)(minValue + ((double)maxValue - minValue) * Mathf.Clamp01(value));
        }

        public void SetValueWithoutNotify(float input) => SetValue(input, false);
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            NormalizeSettings();
            value = ClampValue(IsFinite(value) ? value : minValue);
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            Refresh();
        }
#endif

        #region Obsolete API
        [Obsolete("Use continuousDrag instead.")]
        public bool allowContinuousDrag { get => continuousDrag; set => continuousDrag = value; }
        #endregion
    }
}