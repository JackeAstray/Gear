using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Displays a countdown, count-up timer or local system clock with optional bar smoothing and rolling digits.
    /// </summary>
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Timer"), HelpURL(Constants.HelpUrl + "ui-elements/timer")]
    public class Timer : MonoBehaviour
    {
        // Timer
        [Tooltip("Current time in seconds. In system clock mode, this is the local time elapsed since midnight.")]
        public float currentTime = 30f;
        [Tooltip("Timer duration in seconds. Ignored in system clock mode, where the bar represents a 24-hour day.")]
        public float duration = 60f;
        [Tooltip("Scales the timer's speed. Zero pauses time progression. Ignored in system clock mode.")]
        public float timeMultiplier = 1f;
        [Tooltip("Text template containing {0}, which is replaced by the formatted time. A placeholder is appended if missing.")]
        public string textFormat = "{0}";
        [Tooltip("Displays seconds with fixed decimal precision or as a time string.")]
        public DisplayFormat displayFormat = DisplayFormat.Time_MM_SS;
        [SerializeField, Tooltip("Gives every digit the same advance so changing glyph widths cannot shift the time. UniText uses the font's tabular figures.")]
        private bool useUniformDigitWidth = true;
        [SerializeField, Range(0.5f, 2f), Tooltip("Scales the automatically measured width of the widest digit. TextMesh Pro only.")]
        private float digitWidthScale = 1f;

        // Settings
        [Tooltip("Starts the timer when this component gets enabled.")]
        public bool autoStart = false;
        [Tooltip("Counts toward zero when enabled, or up to Duration when disabled. Ignored in system clock mode.")]
        public bool countDown = true;
        [Tooltip("Resets and restarts the timer after completion. Ignored in system clock mode.")]
        public bool loop = false;
        [Tooltip("Displays local time of day. The bar represents a 24-hour day; duration, speed, direction and looping are ignored.")]
        public bool useSystemClock = false;
        [Tooltip("Uses unscaled time so the timer and its animations continue while Time.timeScale is zero.")]
        public bool useUnscaledTime = true;
        [Tooltip("Invert the visual fill amount: zero is full and the time range maximum is empty. Time and text are unchanged.")]
        public bool invertFill;

        // Bar Animation
        [Tooltip("Smoothly animates the bar toward its current time value.")]
        public bool enableSmoothing = true;
        [Tooltip("Updates the bar's target using whole seconds instead of fractional seconds.")]
        public bool updateBarOnSecondsOnly = false;
        [Min(0f), Tooltip("Duration in seconds of each bar transition. Zero updates the bar immediately.")]
        public float smoothingDuration = 0.25f;

        // Rolling Animation
        [Tooltip("Rolls changing whole-second digits. Decimal places update immediately.")]
        public bool enableRolling = false;
        [Range(0f, 0.99f), Tooltip("Duration in seconds of a digit roll. Shortened at higher timer speeds; zero disables rolling.")]
        public float animationDuration = 0.3f;
        [Min(0f), Tooltip("Vertical distance travelled by rolling digits, in the text object's local units.")]
        public float slideDistance = 40f;
        public Ease animationEase = Ease.OutSine;
        [Tooltip("Controls rolling progress when Ease is Custom. A missing curve uses linear progress.")]
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [Tooltip("Optional timer bar.")]
        public RectTransform fillRect;
        [Tooltip("Optional text that displays the formatted time.")]
        [TextComponent] public Graphic valueText;

        // Events
        [Tooltip("Invoked with the current time in seconds when it changes through ticking or a notifying time setter.")]
        public UnityEvent<float> onValueChanged = new();
        [Tooltip("Invoked when the timer reaches zero while counting down or Duration while counting up, before any automatic restart.")]
        public UnityEvent onTimerComplete = new();
        [Tooltip("Invoked when StartTimer starts a timer that is not already running.")]
        public UnityEvent onTimerStart = new();
        [Tooltip("Invoked when StopTimer is called. Pausing and normal completion do not invoke this event.")]
        public UnityEvent onTimerStop = new();
        [Tooltip("Invoked after ResetTimer restores the starting time or refreshes the local system clock.")]
        public UnityEvent onTimerReset = new();

        // Properties
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public float DisplayValue => currentTime;
        float TimeRange => useSystemClock ? 86400f : Mathf.Max(0f, duration);
        bool CanAnimate => Application.isPlaying && isActiveAndEnabled;

        // Bumped by every playback control call, so Tick can tell whether onTimerComplete took over
        int controlVersion;

        // Timer State
        double preciseTime;
        float lastCurrentTime = float.NaN;

        // Display Cache
        RectTransform cachedFillRect;
        ProceduralShape fillShape;
        Image fillImage;
        int fallbackFillAxis; // Keep the original fallback axis cached so shrinking the fill cannot change the direction
        Graphic boundText;
        TMP_Text cachedText; // TMP text rolls by editing its mesh, other providers through a glyph animator
        TextGlyphAnimator glyphAnimator;
        Utilities.DigitSpacingCache digitSpacing;
        string cachedTemplate;
        string cachedNumberFormat;
        string lastTextFormat;
        string lastFormattedText; // Text last set as a string, null after a buffer write
        bool hasFormattedText;
        DisplayFormat lastDisplayFormat;
        CultureInfo lastCulture;
        double lastDisplayStep = double.NaN;
        int wholeNumberLength;
        int numberLength;
        float lastDisplayedValue;
        double lastWholeValue;

        // Tween State
        const string NumberMarker = "\u0001";
        const float FadeStart = 0.25f; // Digits cross-fade over the middle of their travel, so neither lingers half-faded
        const float FadeEnd = 0.75f;
        TweenHandle barAnimation;
        TweenHandle rollAnimation;
        float barValue;
        float barTarget = float.NaN;
        float rollProgress = 1f;
        float rollDirection;
        Action rollCompletionCallback;
        static readonly Action<Timer, float> BarUpdateCallback = ApplyBarValue;
        static readonly Action<Timer, float> RollUpdateCallback = ApplyRollValue;

        // Reusable Text Buffers
        char[] numberText = new char[32];
        char[] formattedText = new char[64];
        char[] shownText = new char[64]; // Text last set from the buffer, so an unchanged one is skipped
        int shownLength = -1; // -1 when the text was not set from the buffer

        // Reusable Digit Buffers
        readonly List<DigitInfo> outgoingDigits = new(16);
        readonly List<DigitInfo> incomingDigits = new(16);
        readonly List<DigitTransition> digitTransitions = new(16);
        readonly List<GlyphRoll> glyphRolls = new(16);

        void OnEnable()
        {
            CacheFillComponents(true);
            rollCompletionCallback ??= CompleteRoll;        
            UnbindText();
          
            if (useSystemClock)
                UpdateSystemClock();

            UpdateDisplay(false);

            if (autoStart)
                StartTimer();
        }

        void OnDisable()
        {
            barAnimation.Kill();
            CompleteRoll();
            UnbindText();
        }

        void UnbindText()
        {
            if (cachedText != null)
                cachedText.OnPreRenderText -= OnTextRebuilt;

            cachedText = null;
            boundText = null;
            shownLength = -1;
        }

        void OnDestroy()
        {
            barAnimation.Kill();
            rollAnimation.Kill();
            glyphAnimator?.Dispose();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
        }

        void Update()
        {
            if (IsRunning && !IsCompleted)
                Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

            // Preprocessors such as TextEffects only process strings, so text set from the buffer is set again as one
            if (shownLength >= 0 && cachedText.textPreprocessor != null && cachedText != null)
                SetShownTextAsString();
        }

        void Tick(float deltaTime)
        {
            float previousTime = currentTime;

            if (useSystemClock) { UpdateSystemClock(); }
            else
            {
                // Keep sub-frame precision without changing the serialized/public float API
                if (!currentTime.Equals(lastCurrentTime))
                    preciseTime = currentTime;

                double step = Math.Max(0f, deltaTime) * (double)Mathf.Max(0f, timeMultiplier);
                preciseTime = Math.Max(0d, Math.Min(TimeRange, preciseTime + (countDown ? -step : step)));
                currentTime = (float)preciseTime;
                lastCurrentTime = currentTime;
            }

            bool complete = !useSystemClock && (countDown ? preciseTime <= 0d : preciseTime >= TimeRange);
            if (complete)
            {
                IsCompleted = true;
                IsRunning = false;
            }

            UpdateDisplay(true, complete);
         
            if (!currentTime.Equals(previousTime))
                onValueChanged?.Invoke(DisplayValue);

            if (!complete)
                return;

            // A start, stop, pause or reset from onTimerComplete takes precedence over looping
            int versionBeforeComplete = controlVersion;
            onTimerComplete?.Invoke();

            if (loop && versionBeforeComplete == controlVersion && IsCompleted && !IsRunning && TimeRange > 0f)
            {
                ResetTimer();
                StartTimer();
            }
        }

        void SetTime(float newTime, bool notify, bool animate)
        {
            float previousTime = currentTime;
            currentTime = Mathf.Clamp(newTime, 0f, TimeRange);
            preciseTime = currentTime;
            lastCurrentTime = currentTime;

            if (useSystemClock || (countDown ? currentTime > 0f : currentTime < TimeRange))
                IsCompleted = false;

            UpdateDisplay(animate);

            if (notify && !previousTime.Equals(currentTime))
                onValueChanged?.Invoke(DisplayValue);
        }

        #region Types
        /// <summary>
        /// Selects fixed decimal precision for seconds or a time string with optional leading zeros.
        /// </summary>
        public enum DisplayFormat
        {
            [InspectorName("Fixed - No Decimals (0)")] Fixed0,
            [InspectorName("Fixed - 1 Decimal (0.0)")] Fixed1,
            [InspectorName("Fixed - 2 Decimals (0.00)")] Fixed2,
            [InspectorName("Fixed - 3 Decimals (0.000)")] Fixed3,
            [InspectorName("Fixed - 4 Decimals (0.0000)")] Fixed4,
            [InspectorName("Fixed - 5 Decimals (0.00000)")] Fixed5,
            [InspectorName("Time - S")] Time_S,
            [InspectorName("Time - MM:SS")] Time_MM_SS,
            [InspectorName("Time - HH:MM:SS")] Time_HH_MM_SS,
            [InspectorName("Time - M:SS")] Time_M_SS,
            [InspectorName("Time - H:MM:SS")] Time_H_MM_SS
        }

        /// <summary>
        /// Stores incoming and outgoing glyph geometry and the extra mesh quad used during a digit roll.
        /// </summary>
        struct DigitTransition
        {
            public TMP_CharacterInfo Incoming;
            public TMP_CharacterInfo Outgoing;
            public int VertexIndex;
        }

        /// <summary>
        /// Identifies a visible whole-second digit by its value placeholder and position within the number.
        /// </summary>
        struct DigitInfo
        {
            public TMP_CharacterInfo Character; // TMP only
            public int Index;
            public char Value;
            public int Placeholder;
            public int Place;
        }

        /// <summary>
        /// A digit rolled through the glyph animator, by its index in the new text.
        /// </summary>
        struct GlyphRoll
        {
            public int Index;
            public char Outgoing;
        }
        #endregion

        #region Display
        void UpdateSystemClock()
        {
            preciseTime = DateTime.Now.TimeOfDay.TotalSeconds;
            currentTime = (float)preciseTime;
            lastCurrentTime = currentTime;
        }

        void UpdateDisplay(bool animate, bool forceBar = false)
        {
            if (!animate)
                CompleteRoll();

            CacheFillComponents();

            if (boundText != valueText)
            {
                CompleteRoll();
                UnbindText();

                boundText = valueText;
                cachedText = valueText as TMP_Text;
               
                if (cachedText != null && CanAnimate)
                    cachedText.OnPreRenderText += OnTextRebuilt;

                lastDisplayStep = double.NaN;
                lastFormattedText = null;
                hasFormattedText = false;
            }

            UpdateBar(animate && !forceBar);
            UpdateText(animate);
        }

        void CacheFillComponents(bool force = false)
        {
            bool referenceChanged = cachedFillRect != fillRect;
            if (!force && !referenceChanged)
                return;

            cachedFillRect = fillRect;
            fillShape = null;
            fillImage = null;
            barTarget = float.NaN;

            if (fillRect == null)
                return;

            fillRect.TryGetComponent(out fillShape);

            if (fillShape == null)
                fillRect.TryGetComponent(out fillImage);

            // Infer the last-resort axis once, from anchors and the fill area rather than the fill's value-driven size
            if (referenceChanged)
                fallbackFillAxis = Utilities.GetFillAxis(fillRect);
        }

        void UpdateBar(bool animate)
        {
            if (fillRect == null)
                return;

            float value = updateBarOnSecondsOnly ? Mathf.Floor(DisplayValue) : DisplayValue;
            float target = TimeRange > 0f ? Mathf.Clamp01(value / TimeRange) : 0f;
         
            if (invertFill)
                target = 1f - target;
            if (animate && target.Equals(barTarget) && (enableSmoothing || !barAnimation.IsActive))
                return;

            barAnimation.Kill();
            barTarget = target;

            if (animate && CanAnimate && enableSmoothing && smoothingDuration > 0f)
            {
                barAnimation = EvoTween.Create(barValue, target, smoothingDuration)
                    .WithEase(Ease.OutQuad)
                    .WithTimeMode(useSystemClock || useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                    .Bind(this, BarUpdateCallback, 0x54494D42);
            }
            else { ApplyBarValue(this, target); }
        }

        static void ApplyBarValue(Timer timer, float value)
        {
            timer.barValue = value;

            if (timer.fillRect == null)
                return;

            if (timer.fillShape != null && timer.fillShape.ClippingMethod != ProceduralShape.ClipMethod.None)
                timer.fillShape.ClipAmount = value;
            else if (timer.fillImage != null && timer.fillImage.type == Image.Type.Filled)
                timer.fillImage.fillAmount = value;
            else
            {
                // Last resort for older bars without a clipped Shape or Filled Image
                Vector2 anchor = timer.fillRect.anchorMax;
                anchor[timer.fallbackFillAxis] = value;
                if (timer.fillRect.anchorMax != anchor)
                    timer.fillRect.anchorMax = anchor;
            }
        }

        void UpdateText(bool animate)
        {
            if (valueText == null)
                return;

            TMP_Text tmpText = valueText as TMP_Text;
            bool spacingChanged = digitSpacing.Update(valueText, useUniformDigitWidth, digitWidthScale);
            bool formatChanged = spacingChanged || cachedTemplate == null || lastTextFormat != textFormat ||
                lastDisplayFormat != displayFormat || lastCulture != CultureInfo.CurrentCulture;
           
            if (formatChanged)
            {
                CompleteRoll();
                UpdateCachedFormat();
                lastDisplayStep = double.NaN;
            }

            if (!enableRolling && rollProgress < 1f)
                CompleteRoll();

            double value = Math.Max(0d, useSystemClock ? preciseTime : DisplayValue);
            double step = displayFormat >= DisplayFormat.Time_S 
                ? Math.Floor(value) 
                : Math.Round(value, (int)displayFormat, MidpointRounding.AwayFromZero);
           
            // TMP text takes the buffer in play mode. Other providers, edit mode and
            // TMP preprocessors (which only process strings) get a string.
            bool useBuffer = tmpText != null && tmpText.textPreprocessor == null && Application.isPlaying;
            if (step.Equals(lastDisplayStep) && IsTextCurrent())
                return;

            ReadOnlySpan<char> number = numberText.AsSpan(0, FormatValue(step));
            int decimalIndex = displayFormat <= DisplayFormat.Fixed5
                ? number.IndexOf(lastCulture.NumberFormat.NumberDecimalSeparator.AsSpan())
                : -1;
            int wholeLength = decimalIndex >= 0 ? decimalIndex : number.Length;
            double wholeValue = Math.Floor(step);
            bool startRoll = animate && CanAnimate && enableRolling && animationDuration > 0f &&
                !formatChanged && hasFormattedText && wholeValue != lastWholeValue && CanRoll(tmpText);

            if (startRoll)
            {
                CompleteRoll();

                if (tmpText != null)
                {
                    tmpText.ForceMeshUpdate();
                    CollectDigits(tmpText.textInfo);
                }
                else { CollectDigits(lastFormattedText); }

                outgoingDigits.Clear();

                for (int i = 0; i < incomingDigits.Count; i++)
                    outgoingDigits.Add(incomingDigits[i]);

                rollDirection = useSystemClock || DisplayValue >= lastDisplayedValue ? 1f : -1f;
                rollProgress = 0f;
            }

            string previousText = lastFormattedText;
            numberLength = number.Length;
            wholeNumberLength = wholeLength;
            lastWholeValue = wholeValue;
            lastDisplayedValue = DisplayValue;
            lastDisplayStep = step;
            hasFormattedText = true;
            int length = FormatText(number);

            if (useBuffer) { SetBufferText(tmpText, length); }
            else
            {
                shownLength = -1;
                Utilities.ReuseString(ref lastFormattedText, formattedText.AsSpan(0, length));

                // The glyph animator sets the text itself, after capturing the old layout
                if (startRoll && tmpText == null) { BeginGlyphRoll(previousText); }
                else { TextProvider.SetText(valueText, lastFormattedText); }
            }

            if (!startRoll)
                return;
            if (tmpText != null)
                tmpText.ForceMeshUpdate();

            if (digitTransitions.Count == 0 && glyphRolls.Count == 0)
            {
                CompleteRoll();
                return;
            }

            float speed = useSystemClock ? 1f : Mathf.Max(1f, timeMultiplier);
            rollAnimation = EvoTween.Create(0f, 1f, Mathf.Min(animationDuration, 0.99f / speed))
                .WithEase(Ease.Linear)
                .WithTimeMode(useSystemClock || useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                .OnComplete(rollCompletionCallback)
                .Bind(this, RollUpdateCallback, 0x54494D52);
        }

        void UpdateCachedFormat()
        {
            lastTextFormat = textFormat;
            lastDisplayFormat = displayFormat;
            lastCulture = CultureInfo.CurrentCulture;
            cachedNumberFormat = "F" + Mathf.Clamp((int)displayFormat, 0, 5);
            
            string format = string.IsNullOrEmpty(textFormat) ? "{0}" : textFormat;
            if (!format.Contains("{0}"))
                format += " {0}";
           
            try { cachedTemplate = string.Format(lastCulture, format, NumberMarker); }
            catch (FormatException) { cachedTemplate = textFormat + " " + NumberMarker; }

            if (!cachedTemplate.Contains(NumberMarker))
                cachedTemplate += " " + NumberMarker;

            // Keep tags in the template so rolling digit indices include their source lengths
            if (digitSpacing.OpenTag.Length > 0)
                cachedTemplate = cachedTemplate.Replace(NumberMarker, digitSpacing.OpenTag + NumberMarker + "</mspace>");
        }

        /// <summary>
        /// Formats the value into the number buffer and returns its length.
        /// </summary>
        int FormatValue(double value)
        {
            int length = 0;
            if (displayFormat <= DisplayFormat.Fixed5)
            {
                while (!value.TryFormat(numberText, out length, cachedNumberFormat, lastCulture))
                    Array.Resize(ref numberText, numberText.Length * 2);

                return length;
            }

            long totalSeconds = (long)Math.Min(value, long.MaxValue - 2048d);
            long hours = totalSeconds / 3600;
            long minutes = totalSeconds / 60;
            long seconds = totalSeconds % 60;

            switch (displayFormat)
            {
                case DisplayFormat.Time_MM_SS:
                    AppendNumber(ref length, minutes, "D2");
                    AppendNumber(ref length, seconds, "D2", true);
                    break;
                case DisplayFormat.Time_HH_MM_SS:
                    AppendNumber(ref length, hours, "D2");
                    AppendNumber(ref length, minutes % 60, "D2", true);
                    AppendNumber(ref length, seconds, "D2", true);
                    break;
                case DisplayFormat.Time_M_SS:
                    AppendNumber(ref length, minutes, null);
                    AppendNumber(ref length, seconds, "D2", true);
                    break;
                case DisplayFormat.Time_H_MM_SS:
                    AppendNumber(ref length, hours, null);
                    AppendNumber(ref length, minutes % 60, "D2", true);
                    AppendNumber(ref length, seconds, "D2", true);
                    break;
                default:
                    AppendNumber(ref length, totalSeconds, null);
                    break;
            }

            return length;
        }

        /// <summary>
        /// Appends the number to the number buffer, after a colon when it continues a time.
        /// </summary>
        void AppendNumber(ref int length, long value, string format, bool separate = false)
        {
            if (separate)
                Utilities.Append(ref numberText, ref length, ':');

            int written;
            while (!value.TryFormat(numberText.AsSpan(length), out written, format, lastCulture))
                Array.Resize(ref numberText, numberText.Length * 2);

            length += written;
        }

        /// <summary>
        /// Writes the template into the text buffer with every placeholder replaced by the number and returns its length.
        /// </summary>
        int FormatText(ReadOnlySpan<char> number)
        {
            string template = cachedTemplate;
            int length = 0;
            int start = 0;

            for (int marker = template.IndexOf(NumberMarker[0]); marker >= 0; marker = template.IndexOf(NumberMarker[0], start))
            {
                Utilities.Append(ref formattedText, ref length, template.AsSpan(start, marker - start));
                Utilities.Append(ref formattedText, ref length, number);
                start = marker + 1;
            }

            Utilities.Append(ref formattedText, ref length, template.AsSpan(start));
            return length;
        }

        /// <summary>
        /// True when the text still shows the last formatted value. TMP text set from the buffer is not read back, as that allocates.
        /// </summary>
        bool IsTextCurrent() => shownLength >= 0 || TextProvider.GetText(valueText) == lastFormattedText;

        void SetBufferText(TMP_Text tmpText, int length)
        {
            // A buffer always rebuilds the text, so an unchanged one is skipped, as TMP skips an unchanged string
            if (length != shownLength || !formattedText.AsSpan(0, length).SequenceEqual(shownText.AsSpan(0, length)))
                tmpText.SetText(formattedText, 0, length);

            (shownText, formattedText) = (formattedText, shownText);
            shownLength = length;
            lastFormattedText = null;
        }

        void SetShownTextAsString()
        {
            lastFormattedText = new string(shownText, 0, shownLength);
            shownLength = -1;

            // In the editor a buffer write leaves an equal string behind, which TMP would skip
            cachedText.text = string.Empty;
            cachedText.text = lastFormattedText;
        }
        #endregion

        #region Rolling Animation
        bool CanRoll(TMP_Text tmpText)
        {
            if (tmpText != null)
                return tmpText.isActiveAndEnabled && tmpText.geometrySortingOrder == VertexSortingOrder.Normal;

            glyphAnimator = TextGlyphAnimator.Resolve(glyphAnimator, valueText);
            return glyphAnimator != null && valueText.isActiveAndEnabled;
        }

        void CollectDigits(TMP_TextInfo textInfo)
        {
            incomingDigits.Clear();
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (character.isVisible)
                    CollectDigit(character.index, character.character, character);
            }
        }

        void CollectDigits(string text)
        {
            incomingDigits.Clear();
            for (int i = 0; i < text.Length; i++)
                CollectDigit(i, text[i], default);
        }

        /// <summary>
        /// Adds the character when it is a whole-number digit of a value placeholder in the template.
        /// </summary>
        void CollectDigit(int index, char value, in TMP_CharacterInfo character)
        {
            if (value < '0' || value > '9')
                return;

            int offset = 0;
            int placeholder = 0;
            for (int marker = 0; marker < cachedTemplate.Length; marker++)
            {
                if (cachedTemplate[marker] != NumberMarker[0])
                    continue;

                int start = marker + offset;
                if (index >= start && index < start + wholeNumberLength)
                {
                    incomingDigits.Add(new DigitInfo
                    {
                        Character = character,
                        Index = index,
                        Value = value,
                        Placeholder = placeholder,
                        Place = start + wholeNumberLength - 1 - index
                    });
                    return;
                }

                offset += numberLength - 1;
                placeholder++;
            }
        }

        int FindOutgoingDigit(in DigitInfo digit)
        {
            for (int i = 0; i < outgoingDigits.Count; i++)
            {
                if (outgoingDigits[i].Placeholder == digit.Placeholder && outgoingDigits[i].Place == digit.Place)
                    return i;
            }
            return -1;
        }

        void OnTextRebuilt(TMP_TextInfo textInfo)
        {
            if (rollProgress >= 1f)
                return;

            // Rebuild only when TMP rebuilds (format, decimals, font or layout changes).
            // Extra outgoing quads share TMP's existing material meshes.
            CollectDigits(textInfo);
            digitTransitions.Clear();
            for (int i = 0; i < incomingDigits.Count; i++)
            {
                DigitInfo digit = incomingDigits[i];
                int match = FindOutgoingDigit(digit);
                TMP_CharacterInfo incoming = digit.Character;
                TMP_CharacterInfo outgoing = match >= 0 ? outgoingDigits[match].Character : default;

                if (!outgoing.isVisible || incoming.character == outgoing.character
                    || incoming.fontAsset != outgoing.fontAsset || incoming.material != outgoing.material)
                    continue;

                int material = incoming.materialReferenceIndex;
                ref TMP_MeshInfo mesh = ref textInfo.meshInfo[material];
                int vertex = mesh.vertexCount;
             
                if (vertex + 4 > mesh.vertices.Length)
                    mesh.ResizeMeshInfo(Mathf.NextPowerOfTwo(vertex / 4 + 1));
                if (vertex + 4 > mesh.vertices.Length)
                    continue;

                mesh.vertexCount += 4;

                digitTransitions.Add(new DigitTransition
                {
                    Incoming = incoming,
                    Outgoing = outgoing,
                    VertexIndex = vertex
                });
            }
            ApplyRollGeometry(textInfo);
        }

        static void ApplyRollValue(Timer timer, float value)
        {
            timer.rollProgress = value;

            if (timer.cachedText != null)
            {
                timer.ApplyRollGeometry(timer.cachedText.textInfo);
                timer.cachedText.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            }
            else if (timer.glyphRolls.Count > 0)
                timer.ApplyGlyphRoll();
        }

        /// <summary>
        /// Pairs the changed whole-second digits on the strings and hands the new text to the glyph animator.
        /// </summary>
        void BeginGlyphRoll(string previousText)
        {
            CollectDigits(lastFormattedText);
            glyphRolls.Clear();

            for (int i = 0; i < incomingDigits.Count; i++)
            {
                DigitInfo digit = incomingDigits[i];
                int match = FindOutgoingDigit(digit);
                if (match >= 0 && outgoingDigits[match].Value != digit.Value)
                    glyphRolls.Add(new GlyphRoll { Index = digit.Index, Outgoing = outgoingDigits[match].Value });
            }

            if (glyphRolls.Count == 0)
            {
                TextProvider.SetText(valueText, lastFormattedText);
                return;
            }

            glyphAnimator.Begin(previousText, lastFormattedText);
            ApplyGlyphRoll();
        }

        /// <summary>
        /// Mirrors <see cref="ApplyRollGeometry"/>: the outgoing digit is drawn over the incoming one and slides away.
        /// </summary>
        void ApplyGlyphRoll()
        {
            float t = TweenEase.Evaluate(animationEase, rollProgress, animationCurve);
            float fade = GetCrossFade(t);
            float distance = Mathf.Max(0f, slideDistance) * rollDirection;

            for (int i = 0; i < glyphRolls.Count; i++)
            {
                GlyphRoll roll = glyphRolls[i];
                glyphAnimator.MoveGlyph(roll.Index, -1, 1f, -distance * (1f - t), 0f, fade);
                glyphAnimator.AddDigit(roll.Outgoing, roll.Index, -1, 1f, distance * t, 0f, 1f - fade);
            }

            glyphAnimator.Apply();
        }

        void ApplyRollGeometry(TMP_TextInfo textInfo)
        {
            float t = TweenEase.Evaluate(animationEase, rollProgress, animationCurve);
            float fade = GetCrossFade(t);
            float distance = Mathf.Max(0f, slideDistance) * rollDirection;
           
            for (int i = 0; i < digitTransitions.Count; i++)
            {
                DigitTransition digit = digitTransitions[i];
                TMP_MeshInfo mesh = textInfo.meshInfo[digit.Incoming.materialReferenceIndex];
                Vector3 incomingCenter = (digit.Incoming.vertex_BL.position + digit.Incoming.vertex_TR.position) * 0.5f;
                Vector3 outgoingCenter = (digit.Outgoing.vertex_BL.position + digit.Outgoing.vertex_TR.position) * 0.5f;
                Vector3 incomingOffset = new(0f, -distance * (1f - t), 0f);
                Vector3 outgoingOffset = incomingCenter - outgoingCenter + new Vector3(0f, distance * t, 0f);
                WriteQuad(mesh, digit.Incoming.vertexIndex, digit.Incoming, incomingOffset, fade);
                WriteQuad(mesh, digit.VertexIndex, digit.Outgoing, outgoingOffset, 1f - fade);
            }
        }

        static float GetCrossFade(float t) => TweenEase.Evaluate(Ease.InOutSine, (t - FadeStart) / (FadeEnd - FadeStart));

        static void WriteQuad(TMP_MeshInfo mesh, int index, TMP_CharacterInfo character, Vector3 offset, float alpha)
        {
            WriteVertex(mesh, index, character.vertex_BL, offset, alpha);
            WriteVertex(mesh, index + 1, character.vertex_TL, offset, alpha);
            WriteVertex(mesh, index + 2, character.vertex_TR, offset, alpha);
            WriteVertex(mesh, index + 3, character.vertex_BR, offset, alpha);
        }

        static void WriteVertex(TMP_MeshInfo mesh, int index, TMP_Vertex vertex, Vector3 offset, float alpha)
        {
            mesh.vertices[index] = vertex.position + offset;
            mesh.uvs0[index] = vertex.uv;
            mesh.uvs2[index] = vertex.uv2;
            Color32 color = vertex.color;
            color.a = (byte)Mathf.RoundToInt(color.a * alpha);
            mesh.colors32[index] = color;
        }

        void CompleteRoll()
        {
            rollAnimation.Kill();
            rollAnimation = default;
          
            bool wasRolling = rollProgress < 1f || digitTransitions.Count > 0;
          
            rollProgress = 1f;
            digitTransitions.Clear();

            if (wasRolling && cachedText != null)
                cachedText.ForceMeshUpdate();

            if (glyphRolls.Count > 0)
            {
                glyphRolls.Clear();
                glyphAnimator?.End();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Gives every digit the same advance so changing glyph widths cannot shift the time.
        /// </summary>
        public bool UseUniformDigitWidth
        {
            get => useUniformDigitWidth;
            set
            {
                if (useUniformDigitWidth == value)
                    return;

                useUniformDigitWidth = value;
                UpdateDisplay(false);
            }
        }

        /// <summary>
        /// Starts time updates and invokes onTimerStart.
        /// Resets a completed timer first; does nothing if the timer is already running.
        /// </summary>
        public void StartTimer()
        {
            if (IsRunning)
                return;

            if (IsCompleted)
                ResetTimer();

            controlVersion++;
            IsRunning = true;
            IsCompleted = false;
            onTimerStart?.Invoke();
        }

        /// <summary>
        /// Stops time updates without resetting the current time and invokes onTimerStop.
        /// </summary>
        public void StopTimer()
        {
            controlVersion++;
            IsRunning = false;
            onTimerStop?.Invoke();
        }

        /// <summary>
        /// Pauses time updates without invoking onTimerStop or cancelling active display animations.
        /// </summary>
        public void PauseTimer()
        {
            controlVersion++;
            IsRunning = false;
        }

        /// <summary>
        /// Resumes time updates without invoking onTimerStart. Does nothing if the timer is completed.
        /// </summary>
        public void ResumeTimer()
        {
            if (!IsCompleted)
                IsRunning = true;
        }

        /// <summary>
        /// Stops and clears completion, restores Duration for a countdown or zero for a count-up,
        /// and refreshes the display. In system clock mode, refreshes local time instead.
        /// Invokes onTimerReset without invoking onValueChanged.
        /// </summary>
        public void ResetTimer()
        {
            controlVersion++;
            IsRunning = false;
            IsCompleted = false;
            CompleteRoll();

            if (!useSystemClock) { SetTime(countDown ? TimeRange : 0f, false, false); }
            else
            {
                UpdateSystemClock();
                UpdateDisplay(false);
            }

            onTimerReset?.Invoke();
        }

        /// <summary>
        /// Sets the current time within zero and Duration, updates the display and
        /// invokes onValueChanged if the value changes.
        /// </summary>
        public void SetCurrentTime(float newTime)
        {
            if (!useSystemClock)
                SetTime(newTime, true, true);
        }

        /// <summary>
        /// Sets the current time within zero and Duration and updates the display without invoking
        /// </summary>
        public void SetCurrentTimeWithoutNotify(float newTime)
        {
            if (!useSystemClock)
                SetTime(newTime, false, true);
        }

        /// <summary>
        /// Adds seconds through SetCurrentTime, applying its range limits, display update and notification.
        /// </summary>
        public void AddTime(float timeToAdd) => SetCurrentTime(currentTime + timeToAdd);

        /// <summary>
        /// Subtracts seconds through SetCurrentTime, applying its range limits, display update and notification.
        /// </summary>
        public void SubtractTime(float timeToSubtract) => SetCurrentTime(currentTime - timeToSubtract);

        /// <summary>
        /// Gets the seconds until the timer's endpoint, or until midnight in system clock mode.
        /// </summary>
        public float GetRemainingTime() => countDown && !useSystemClock ? currentTime : TimeRange - currentTime;

        /// <summary>
        /// Gets the elapsed seconds based on the current value and counting direction,
        /// or since midnight in system clock mode.
        /// </summary>
        public float GetElapsedTime() => countDown && !useSystemClock ? TimeRange - currentTime : currentTime;
        #endregion

#if UNITY_EDITOR
        void OnValidate()
        {
            duration = Mathf.Max(0f, duration);

            if (!useSystemClock)
                currentTime = Mathf.Clamp(currentTime, 0f, duration);

            timeMultiplier = Mathf.Max(0f, timeMultiplier);
            smoothingDuration = Mathf.Max(0f, smoothingDuration);
            animationDuration = Mathf.Clamp(animationDuration, 0f, 0.99f);
            slideDistance = Mathf.Max(0f, slideDistance);
            digitWidthScale = Mathf.Clamp(digitWidthScale, 0.5f, 2f);
     
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            CacheFillComponents(true);

            if (useSystemClock)
                UpdateSystemClock();

            UpdateDisplay(false);
        }
#endif
    }
}