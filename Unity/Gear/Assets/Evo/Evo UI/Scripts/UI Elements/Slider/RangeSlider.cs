using System;
using System.Globalization;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;
using Direction = UnityEngine.UI.Slider.Direction;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Range Slider"), HelpURL(Constants.HelpUrl + "ui-elements/range-slider")]
    public class RangeSlider : Interactive, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
    {
        // Range
        [SerializeField] private float lowerValue;
        [SerializeField] private float upperValue = 1f;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 1f;
        [SerializeField, Min(0f)] private float minGap;
        [SerializeField] private bool wholeNumbers;
        [SerializeField] private Direction direction;
        [Tooltip("Push the other handle when it reaches the minimum gap. Both handles stop at the range limits.")]
        public bool pushOtherHandle;

        // References
        public RectTransform fillRect;
        public RectTransform lowerHandleRect;
        public RectTransform upperHandleRect;
        [TextComponent] public Graphic lowerValueText;
        [TextComponent] public Graphic upperValueText;
        [TextComponent] public Graphic minValueText;
        [TextComponent] public Graphic maxValueText;
        [TextInput] public MonoBehaviour lowerValueInput;
        [TextInput] public MonoBehaviour upperValueInput;

        // Formatting
        public float displayMultiplier = 1f;
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed0;
        [Tooltip("Value label format. Input fields display the numeric value without decoration.")]
        public string textFormat = "{0}";

        // Animation
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0f, 2f)] public float highlightedScale = 1.25f;
        [Range(0f, 2f)] public float pressedScale = 1.15f;

        // Events
        public UnityEvent<float, float> onValueChanged = new();

        // Runtime State
        Handle activeHandle;
        bool isSelected;
        bool dragging;
        bool chooseDragHandle;
        int pointerID;
        float dragOffset;
        float pressPosition;
        RectTransform dragRect;
        DrivenRectTransformTracker tracker;
        TextInputListener lowerInputListener;
        TextInputListener upperInputListener;
        float lastLowerValue;
        float lastUpperValue;

        // Animation Cache
        HandleVisual lowerVisual;
        HandleVisual upperVisual;
        TweenHandle currentAnimation;
        static readonly Action<RangeSlider, float> TweenUpdateCallback = ApplyAnimation;

        // Text Cache
        const string NumberMarker = "\u0001";
        readonly StringBuilder textBuilder = new(128);
        char[] numberBuffer = new char[128];
        string cachedTemplate;
        string lastTextFormat;

        // Properties
        int Axis => direction == Direction.LeftToRight || direction == Direction.RightToLeft ? 0 : 1;
        bool Reversed => direction == Direction.RightToLeft || direction == Direction.TopToBottom;
        double Span => (double)maxValue - minValue;

        protected override void OnEnable()
        {
            isSelected = Utilities.GetSelectedObject() == gameObject;
            Refresh();

            if (Application.isPlaying)
            {
                lowerInputListener ??= new TextInputListener { onEndEdit = OnLowerInputEndEdit };
                upperInputListener ??= new TextInputListener { onEndEdit = OnUpperInputEndEdit };
                lowerInputListener.Bind(lowerValueInput);
                upperInputListener.Bind(upperValueInput != lowerValueInput ? upperValueInput : null);
            }

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            isSelected = false;
            CancelDrag();
            lowerInputListener?.Unbind();
            upperInputListener?.Unbind();
            tracker.Clear();
            base.OnDisable();
            currentAnimation.Kill();
            currentAnimation = default;
            lowerVisual.Clear();
            upperVisual.Clear();
            lowerVisual = default;
            upperVisual = default;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
        }

        protected override void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
            base.OnDestroy();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || !dragging)
                return;

            InstantClearState();

            if (interactionState != InteractionState.Selected)
                SetState(IsInteractable() ? InteractionState.Normal : InteractionState.Disabled, true);
        }

        protected override void InstantClearState()
        {
            base.InstantClearState();
            CancelDrag();
            UpdateAnimationState(true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            if (state == SelectionState.Disabled)
                CancelDrag();

            UpdateAnimationState(instant);
        }

        public override void SetState(InteractionState state, bool instant = false)
        {
            base.SetState(state, instant);

            if (state == InteractionState.Disabled)
                CancelDrag();

            UpdateAnimationState(instant);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (IsActive())
                UpdateVisuals();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();

            float previousLower = lastLowerValue;
            float previousUpper = lastUpperValue;
            Refresh();

            if (previousLower != lowerValue || previousUpper != upperValue)
                onValueChanged?.Invoke(lowerValue, upperValue);
        }

        #region Types
        public enum Handle { Lower, Upper }

        struct HandleVisual
        {
            public RectTransform rect;
            public Vector3 originalScale;
            public Vector3 fromScale;
            public Vector3 targetScale;

            public readonly void Clear()
            {
                if (rect != null)
                    rect.localScale = originalScale;
            }

            public void Bind(RectTransform newRect)
            {
                if (rect == newRect)
                    return;

                Clear();
                rect = newRect;
                originalScale = rect != null ? rect.localScale : Vector3.one;
                targetScale = originalScale;
            }

            public bool SetTarget(float scale)
            {
                Vector3 nextScale = originalScale * scale;
                bool changed = targetScale != nextScale;
                targetScale = nextScale;
                return changed;
            }

            public void CaptureStart() => fromScale = rect != null ? rect.localScale : targetScale;

            public readonly void Apply(float progress)
            {
                if (rect != null)
                    rect.localScale = Vector3.LerpUnclamped(fromScale, targetScale, progress);
            }
        }
        #endregion

        #region Range
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        float Normalize(float value) => Span > 0d ? (float)(((double)value - minValue) / Span) : 0f;

        float Denormalize(float value) => (float)(minValue + Span * Mathf.Clamp01(value));

        float ClampValue(float value) => Mathf.Clamp(wholeNumbers ? Mathf.Round(value) : value, minValue, maxValue);

        // Round toward the valid side of a gap boundary, including gaps smaller than a float step.
        static float RoundBoundary(double value, bool upward)
        {
            float result = (float)value;
            if (upward ? result < value : result > value)
                result = NextFloat(result, upward);

            return result;
        }

        static float NextFloat(float value, bool upward)
        {
            if (value == 0f)
                return upward ? float.Epsilon : -float.Epsilon;

            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(bits + ((value > 0f) == upward ? 1 : -1));
        }

        float OffsetByGap(float value, bool upward)
        {
            float result = RoundBoundary(value + (upward ? (double)minGap : -(double)minGap), upward);
            return minGap > 0f && result == value ? NextFloat(value, upward) : result;
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
            minGap = IsFinite(minGap) ? Mathf.Max(0f, minGap) : 0f;
            minGap = RoundBoundary(Math.Min(Span, wholeNumbers ? Math.Ceiling(minGap) : minGap), false);
        }

        void ConstrainValues(ref float lower, ref float upper)
        {
            lower = ClampValue(IsFinite(lower) ? lower : minValue);
            upper = ClampValue(IsFinite(upper) ? upper : maxValue);
            lower = Mathf.Min(lower, OffsetByGap(maxValue, false));
            upper = Mathf.Max(upper, OffsetByGap(lower, true));
        }

        void RefreshRange()
        {
            NormalizeSettings();
            SetValues(lowerValue, upperValue);

            if (IsActive())
                UpdateVisuals();

            SetLimitText();
        }

        void SetHandleValue(Handle handle, float value, bool sendCallback = true)
        {
            if (!IsFinite(value))
                return;

            float lower = lowerValue;
            float upper = upperValue;
            value = ClampValue(value);

            if (handle == Handle.Lower)
            {
                lower = Mathf.Min(value, pushOtherHandle ? OffsetByGap(maxValue, false) : MaxLowerValue);
                if (pushOtherHandle)
                    upper = Mathf.Max(upper, OffsetByGap(lower, true));
            }
            else
            {
                upper = Mathf.Max(value, pushOtherHandle ? OffsetByGap(minValue, true) : MinUpperValue);
                if (pushOtherHandle)
                    lower = Mathf.Min(lower, OffsetByGap(upper, false));
            }

            SetValues(lower, upper, sendCallback);
        }

        void SetValues(float lower, float upper, bool sendCallback)
        {
            if (!IsFinite(lower) || !IsFinite(upper))
                return;

            ConstrainValues(ref lower, ref upper);

            if (lower == lowerValue && upper == upperValue)
                return;

            lowerValue = lastLowerValue = lower;
            upperValue = lastUpperValue = upper;

            if (IsActive())
                UpdateVisuals();

            SetValueText();

            if (sendCallback)
                onValueChanged?.Invoke(lowerValue, upperValue);
        }
        #endregion

        #region Visuals
        RectTransform ValidRect(RectTransform rect) => rect != null && rect != transform && rect.IsChildOf(transform) ? rect : null;

        void UpdateVisuals()
        {
            tracker.Clear();
            float lower = NormalizedLowerValue;
            float upper = NormalizedUpperValue;
            SetAnchors(ValidRect(fillRect), Reversed ? 1f - upper : lower, Reversed ? 1f - lower : upper);
            SetAnchors(ValidRect(lowerHandleRect), Reversed ? 1f - lower : lower, Reversed ? 1f - lower : lower);
            SetAnchors(ValidRect(upperHandleRect), Reversed ? 1f - upper : upper, Reversed ? 1f - upper : upper);
        }

        void SetAnchors(RectTransform rect, float lower, float upper)
        {
            if (rect == null)
                return;

            tracker.Add(this, rect, DrivenTransformProperties.Anchors);
            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.one;
            anchorMin[Axis] = lower;
            anchorMax[Axis] = upper;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
        }

        void UpdateAnimationState(bool instant = false)
        {
            if (!Application.isPlaying)
                return;

            bool canInteract = IsActive() && IsInteractable() && interactionState != InteractionState.Disabled;
            bool highlighted = canInteract && (isPointerOn || isSelected || interactionState == InteractionState.Selected);
            float scale = !canInteract ? 1f : dragging ? pressedScale : highlighted ? highlightedScale : 1f;
            bool lowerChanged = lowerVisual.SetTarget(activeHandle == Handle.Lower ? scale : 1f);
            bool upperChanged = upperVisual.SetTarget(activeHandle == Handle.Upper ? scale : 1f);

            if (!instant && !lowerChanged && !upperChanged)
                return;

            currentAnimation.Kill();
            currentAnimation = default;
            lowerVisual.CaptureStart();
            upperVisual.CaptureStart();

            if (instant || transitionDuration <= 0f || !IsActive()) { ApplyAnimation(this, 1f); }
            else
            {
                currentAnimation = EvoTween.Create(0f, 1f, transitionDuration)
                    .WithTimeMode(TimeMode.Unscaled).Bind(this, TweenUpdateCallback, 0x52534C44);
            }
        }

        static void ApplyAnimation(RangeSlider slider, float progress)
        {
            float scaleTime = progress >= 1f ? 1f : slider.animationCurve != null ? slider.animationCurve.Evaluate(progress) : progress;
            slider.lowerVisual.Apply(scaleTime);
            slider.upperVisual.Apply(scaleTime);
        }
        #endregion

        #region Pointer & Navigation
        RectTransform GetTrack(Handle handle)
        {
            RectTransform rect = ValidRect(handle == Handle.Lower ? lowerHandleRect : upperHandleRect);
            if (rect == null)
                rect = ValidRect(handle == Handle.Lower ? upperHandleRect : lowerHandleRect);
            if (rect == null)
                rect = ValidRect(fillRect);

            return rect != null ? rect.parent as RectTransform : transform as RectTransform;
        }

        bool TryGetPosition(RectTransform rect, PointerEventData eventData, out float position)
        {
            position = 0f;
            Camera camera = eventData.pressEventCamera;

            if (camera == null)
                camera = eventData.enterEventCamera;

            if (rect == null || rect.rect.size[Axis] <= 0f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, camera, out Vector2 local))
                return false;

            position = (local[Axis] - rect.rect.position[Axis]) / rect.rect.size[Axis];
            if (Reversed)
                position = 1f - position;

            return true;
        }

        Handle PickHandle(PointerEventData eventData)
        {
            Camera camera = eventData.pressEventCamera;
            if (camera == null)
                camera = eventData.enterEventCamera;

            bool onLower = ValidRect(lowerHandleRect) != null && RectTransformUtility.RectangleContainsScreenPoint(lowerHandleRect, eventData.position, camera);
            bool onUpper = ValidRect(upperHandleRect) != null && RectTransformUtility.RectangleContainsScreenPoint(upperHandleRect, eventData.position, camera);
            if (onLower != onUpper)
                return onLower ? Handle.Lower : Handle.Upper;

            if (!TryGetPosition(GetTrack(Handle.Lower), eventData, out float position))
                return activeHandle;

            float lowerDistance = Mathf.Abs(position - NormalizedLowerValue);
            float upperDistance = Mathf.Abs(position - NormalizedUpperValue);
            if (lowerDistance == upperDistance)
                return position < NormalizedLowerValue ? Handle.Lower : position > NormalizedUpperValue ? Handle.Upper : activeHandle;

            return lowerDistance < upperDistance ? Handle.Lower : Handle.Upper;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || dragging || eventData.button != PointerEventData.InputButton.Left)
                return;

            activeHandle = PickHandle(eventData);
            dragRect = GetTrack(activeHandle);

            if (!TryGetPosition(dragRect, eventData, out pressPosition))
                return;

            dragging = true;
            pointerID = eventData.pointerId;
            RectTransform handle = ValidRect(activeHandle == Handle.Lower ? lowerHandleRect : upperHandleRect);
            bool onHandle = handle != null && RectTransformUtility.RectangleContainsScreenPoint(handle, eventData.position, eventData.pressEventCamera);
            dragOffset = onHandle ? pressPosition - (activeHandle == Handle.Lower ? NormalizedLowerValue : NormalizedUpperValue) : 0f;
            chooseDragHandle = onHandle && lowerValue == upperValue;

            base.OnPointerDown(eventData);

            if (!IsActive() || !IsInteractable() || !dragging)
                return;

            if (!onHandle)
                SetHandleValue(activeHandle, Denormalize(pressPosition));

            UpdateAnimationState();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != pointerID || eventData.button != PointerEventData.InputButton.Left
                || !IsActive() || !IsInteractable() || !TryGetPosition(dragRect, eventData, out float position))
                return;

            if (chooseDragHandle)
            {
                if (Mathf.Approximately(position, pressPosition))
                    return;

                ActiveHandle = position < pressPosition ? Handle.Lower : Handle.Upper;
                chooseDragHandle = false;
            }

            SetHandleValue(activeHandle, Denormalize(position - dragOffset));
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!dragging || eventData.pointerId != pointerID || eventData.button != PointerEventData.InputButton.Left)
                return;

            CancelDrag();
            base.OnPointerUp(eventData);
            UpdateAnimationState();
        }

        public void OnEndDrag(PointerEventData eventData) => OnPointerUp(eventData);

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (!dragging && IsActive() && IsInteractable())
                activeHandle = PickHandle(eventData);

            base.OnPointerEnter(eventData);
            UpdateAnimationState();
        }

        public override void OnPointerMove(PointerEventData eventData)
        {
            base.OnPointerMove(eventData);

            if (!dragging && IsActive() && IsInteractable())
                ActiveHandle = PickHandle(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            CancelDrag();
            base.OnDeselect(eventData);
            UpdateAnimationState();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
            base.OnSelect(eventData);
            UpdateAnimationState();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || dragging)
                return;

            ActiveHandle = activeHandle == Handle.Lower ? Handle.Upper : Handle.Lower;
            base.OnSubmit(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || navigation.mode == Navigation.Mode.None || dragging)
                return;

            bool horizontal = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right;
            bool vertical = eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down;
            Selectable next = eventData.moveDir switch
            {
                MoveDirection.Left => FindSelectableOnLeft(),
                MoveDirection.Right => FindSelectableOnRight(),
                MoveDirection.Up => FindSelectableOnUp(),
                MoveDirection.Down => FindSelectableOnDown(),
                _ => null
            };

            if (next != null || !(Axis == 0 ? horizontal : vertical))
            {
                base.OnMove(eventData);
                return;
            }

            bool increase = eventData.moveDir == MoveDirection.Right || eventData.moveDir == MoveDirection.Up;
            double step = wholeNumbers ? 1d : Span * 0.1d;
            double value = (activeHandle == Handle.Lower ? lowerValue : upperValue) + (increase != Reversed ? step : -step);
            SetHandleValue(activeHandle, (float)Math.Max(minValue, Math.Min(maxValue, value)));
            eventData.Use();
        }

        public override Selectable FindSelectableOnLeft() => navigation.mode == Navigation.Mode.Automatic && Axis == 0 ? null : base.FindSelectableOnLeft();

        public override Selectable FindSelectableOnRight() => navigation.mode == Navigation.Mode.Automatic && Axis == 0 ? null : base.FindSelectableOnRight();

        public override Selectable FindSelectableOnUp() => navigation.mode == Navigation.Mode.Automatic && Axis == 1 ? null : base.FindSelectableOnUp();

        public override Selectable FindSelectableOnDown() => navigation.mode == Navigation.Mode.Automatic && Axis == 1 ? null : base.FindSelectableOnDown();

        void CancelDrag()
        {
            dragging = false;
            chooseDragHandle = false;
            dragRect = null;
            isPressedDown = false;
        }
        #endregion

        #region Text & Input
        void OnLowerInputEndEdit(string text) => OnInputEndEdit(Handle.Lower, text);

        void OnUpperInputEndEdit(string text) => OnInputEndEdit(Handle.Upper, text);

        void OnInputEndEdit(Handle handle, string text)
        {
            if (!IsActive() || !IsInteractable())
                return;

            if (float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out float result)
                && IsFinite(result) && IsFinite(displayMultiplier) && displayMultiplier != 0f)
                SetHandleValue(handle, result / displayMultiplier);

            SetValueText();
        }

        public void SetText()
        {
            SetValueText();
            SetLimitText();
        }

        void SetValueText()
        {
            SetText(lowerValue, lowerValueText, lowerValueInput);
            SetText(upperValue, upperValueText, upperValueInput != lowerValueInput ? upperValueInput : null);
        }

        // Range limits only change with the range settings, not while dragging
        void SetLimitText()
        {
            SetText(minValue, minValueText, null);
            SetText(maxValue, maxValueText, null);
        }

        void SetText(float value, Graphic label, MonoBehaviour input)
        {
            TextBackend inputBackend = TextProvider.FindInput(input);
            if (label == null && inputBackend == null)
                return;

            int length = PrepareText(value);

            // The input's own text is set through the input
            if (label != null && (inputBackend == null || label != inputBackend.GetTextComponent(input)))
                TextProvider.SetText(label, textBuilder);

            // Inputs require a string; allocate only when the displayed number changes
            if (inputBackend != null && !inputBackend.ValueEquals(input, numberBuffer.AsSpan(0, length)))
                inputBackend.SetValue(input, new string(numberBuffer, 0, length), false);
        }

        int PrepareText(float value)
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

            double displayed = value * (double)(IsFinite(displayMultiplier) ? displayMultiplier : 1f);
            int length;

            while (!displayed.TryFormat(numberBuffer, out length, Utilities.GetNumberFormatString(displayFormat)))
                Array.Resize(ref numberBuffer, numberBuffer.Length * 2);

            textBuilder.Clear();
            for (int i = 0; i < cachedTemplate.Length; i++)
            {
                if (cachedTemplate[i] == NumberMarker[0])
                    textBuilder.Append(numberBuffer, 0, length);
                else
                    textBuilder.Append(cachedTemplate[i]);
            }
            return length;
        }
        #endregion

        #region Public API
        public float LowerValue
        {
            get => lowerValue;
            set => SetHandleValue(Handle.Lower, value);
        }

        public float UpperValue
        {
            get => upperValue;
            set => SetHandleValue(Handle.Upper, value);
        }

        public float MinValue
        {
            get => minValue;
            set
            {
                if (!IsFinite(value) || minValue == value)
                    return;

                minValue = value;
                RefreshRange();
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
                RefreshRange();
            }
        }

        public float MinGap
        {
            get => minGap;
            set
            {
                if (!IsFinite(value) || minGap == value)
                    return;

                minGap = value;
                RefreshRange();
            }
        }

        public float MaxLowerValue => OffsetByGap(upperValue, false);

        public float MinUpperValue => OffsetByGap(lowerValue, true);

        public bool WholeNumbers
        {
            get => wholeNumbers;
            set
            {
                if (wholeNumbers == value)
                    return;

                wholeNumbers = value;
                RefreshRange();
            }
        }

        public Direction Direction
        {
            get => direction;
            set => SetDirection(value, false);
        }

        public float NormalizedLowerValue
        {
            get => Normalize(lowerValue);
            set
            {
                if (IsFinite(value))
                    LowerValue = Denormalize(value);
            }
        }

        public float NormalizedUpperValue
        {
            get => Normalize(upperValue);
            set
            {
                if (IsFinite(value))
                    UpperValue = Denormalize(value);
            }
        }

        public Handle ActiveHandle
        {
            get => activeHandle;
            set
            {
                if (value != Handle.Lower && value != Handle.Upper || activeHandle == value)
                    return;

                activeHandle = value;
                UpdateAnimationState();
            }
        }

        public void SetValues(float lower, float upper) => SetValues(lower, upper, true);

        public void SetValuesWithoutNotify(float lower, float upper) => SetValues(lower, upper, false);

        public void SetLowerValueWithoutNotify(float value) => SetHandleValue(Handle.Lower, value, false);

        public void SetUpperValueWithoutNotify(float value) => SetHandleValue(Handle.Upper, value, false);

        /// <summary>
        /// Refreshes handle references, range and text after changing public fields.
        /// </summary>
        public void Refresh()
        {
            NormalizeSettings();
            ConstrainValues(ref lowerValue, ref upperValue);
            lastLowerValue = lowerValue;
            lastUpperValue = upperValue;

            if (IsActive())
            {
                if (Application.isPlaying)
                {
                    lowerVisual.Bind(ValidRect(lowerHandleRect));
                    upperVisual.Bind(ValidRect(upperHandleRect));
                }

                UpdateVisuals();
                UpdateAnimationState(true);
            }

            SetText();
        }

        public void SetDirection(Direction value, bool includeRectLayouts)
        {
            if ((int)value < 0 || (int)value > 3 || direction == value)
                return;

            int oldAxis = Axis;
            bool oldReversed = Reversed;
            direction = value;
            CancelDrag();

            if (includeRectLayouts)
            {
                if (Axis != oldAxis)
                    RectTransformUtility.FlipLayoutAxes((RectTransform)transform, true, true);
                if (Reversed != oldReversed)
                    RectTransformUtility.FlipLayoutOnAxis((RectTransform)transform, Axis, true, true);
            }

            UpdateVisuals();
            UpdateAnimationState();
        }

        /// <summary>
        /// Returns a new string. SetText uses the text provider's buffer API for value labels.
        /// </summary>
        public string GetFormattedText(float value)
        {
            PrepareText(value);
            return textBuilder.ToString();
        }
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            NormalizeSettings();
            ConstrainValues(ref lowerValue, ref upperValue);
            displayMultiplier = IsFinite(displayMultiplier) ? Mathf.Clamp(displayMultiplier, -1000f, 1000f) : 1f;
            base.OnValidate();
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this == null || !IsActive())
                return;

            float previousLower = lastLowerValue;
            float previousUpper = lastUpperValue;
            Refresh();

            if (Application.isPlaying && (previousLower != lowerValue || previousUpper != upperValue))
                onValueChanged?.Invoke(lowerValue, upperValue);
        }
#endif
    }
}