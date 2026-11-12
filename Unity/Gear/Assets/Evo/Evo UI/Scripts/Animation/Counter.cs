using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Displays formatted numeric values with direction-aware, per-character rolling and smooth layout transitions.
    /// </summary>
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Animation/Counter"), HelpURL(Constants.HelpUrl + "ui-elements/counter")]
    public class Counter : MonoBehaviour
    {
        // References
        [Tooltip("Text that displays the value.")]
        [TextComponent] public Graphic textObject;

        // Value Settings
        [Tooltip("The target value displayed by the counter.")]
        public float value = 100f;
        [Tooltip("Composite format containing {0}, which is replaced by the formatted number.")]
        public string textFormat = "${0}";
        [Tooltip("The numeric precision and grouping style applied to value.")]
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Number0;
        [SerializeField, Tooltip("Gives every digit the same advance so changing glyph widths cannot shift the number. UniText uses the font's tabular figures.")]
        private bool useUniformDigitWidth = true;
        [SerializeField, Range(0.5f, 2f), Tooltip("Scales the automatically measured width of the widest digit. TextMesh Pro only.")]
        private float digitWidthScale = 1f;

        // Animation Settings
        [Tooltip("Rolls from zero to the configured value when this component first becomes active.")]
        public bool animateOnEnable;
        [Tooltip("Keeps the counter moving while Time.timeScale is zero.")]
        public bool useUnscaledTime = true;
        [Tooltip("Visits the intermediate digits in the direction of the change. For example, 6 to 3 rolls through 5 and 4.")]
        public bool rollThroughDigits = true;
        [Range(0f, 0.2f), Tooltip("Minimum average time per digit step. Zero uses only Roll Duration.")]
        public float minimumStepDuration = 0.1f;
        [Range(0.01f, 20f), Tooltip("Minimum time each changed digit takes to finish its entire sequence.")]
        public float counterDuration = 0.3f;
        [Range(0f, 5f), Tooltip("Time to wait before the first digit starts moving.")]
        public float delay;
        [Range(0f, 0.15f), Tooltip("Extra delay between changed digits, starting at the least-significant digit.")]
        public float digitStagger = 0.025f;
        [Range(0f, 2f), Tooltip("Vertical travel measured as a multiple of each glyph's height.")]
        public float rollDistance = 0.8f;
        [Range(0f, 1f), Tooltip("How strongly outgoing and incoming digits cross-fade.")]
        public float fadeAmount = 1f;
        public Ease animationEase = Ease.OutSine;
        public AnimationCurve animationCurve = new(new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        // Constants
        const int TweenPropertyKey = 0x43545252; // CTRR
        const string RollLayerName = "[CounterRollLayer]";
        const TMP_VertexDataUpdateFlags RollChannels = TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32;
        const float FadeStart = 0.25f; // Digits cross-fade over the middle of their travel, so neither lingers half-faded
        const float FadeEnd = 0.75f;

        // Cached Delegates
        Action completionCallback;
        static readonly Action<Counter, float> TweenUpdateCallback = ApplyTweenValue;

        // Reusable Buffers
        readonly List<CharacterTransition> characterTransitions = new(16);
        readonly List<int> incomingDigitIndices = new(16);
        readonly List<int> outgoingDigitIndices = new(16);
        readonly List<int> incomingLayoutIndices = new(8);
        readonly List<int> outgoingLayoutIndices = new(8);
        readonly MeshSnapshot incomingMesh = new();
        readonly MeshSnapshot outgoingMesh = new();

        // Text Buffers (values are formatted into these, so changing them allocates no strings)
        char[] numberText = new char[64];
        char[] fromText = new char[64];
        char[] toText = new char[64];
        char[] stripText = new char[256];
        char[] shownText = new char[64];
        int shownLength;
        TMP_Text shownTarget;
        string lastString;

        // Runtime State
        float currentValue;
        float animationStartValue;
        float animationTargetValue;
        float animationDuration;
        float layoutDuration;
        float lastElapsed;
        int rollingDigitCount;
        bool initialized;
        bool hasRollVisuals;
        TweenHandle currentAnimation;
        FormattedValue animationToText;
        CharacterSequence incomingCharacters;
        CharacterSequence outgoingCharacters;

        // TMP text rolls by editing its mesh, other providers through a glyph animator
        TMP_Text rollText;
        TextMeshProUGUI outgoingText;
        TMP_Text subscribedText;
        TextGlyphAnimator glyphAnimator;

        // Formatting Cache
        Utilities.DigitSpacingCache digitSpacing;
        string cachedFormatString;
        string cachedTextFormat;
        string cachedTextFormatSource;
        string cachedFontSizeText;
        float cachedFontSize;
        bool expandTextFormat;

        // Properties
        public float CurrentValue => currentValue;
        public bool IsAnimating => currentAnimation.IsActive;

        void Awake() => Initialize();

        void OnEnable()
        {
            Initialize();

            if (animateOnEnable && !Mathf.Approximately(currentValue, value))
                AnimateToTarget();
            else
                SetValueInstant(value);
        }

        void OnDisable()
        {
            StopAnimation();
            currentValue = value;
            UpdateDisplay(currentValue);
        }

        void OnDestroy()
        {
            currentAnimation.Kill();

            if (subscribedText != null)
                subscribedText.OnPreRenderText -= OnIncomingTextRebuilt;

            if (outgoingText != null)
                Destroy(outgoingText.gameObject);

            glyphAnimator?.Dispose();
        }

        void Initialize()
        {
            if (initialized)
                return;

            if (textObject == null)
                TextProvider.TryGetText(this, out textObject);

            completionCallback = CompleteAnimation;
            UpdateCachedFormats();
            currentValue = animateOnEnable ? 0f : value;
            UpdateDisplay(currentValue);
            initialized = true;
        }

        #region Types
        enum CharacterFilter
        {
            All,
            Digits,
            NonDigits
        }

        readonly struct FormattedValue
        {
            public readonly char[] Buffer;
            public readonly int Length;
            public readonly int NumberStart;
            public readonly int NumberLength;

            public FormattedValue(char[] buffer, int length, int numberStart, int numberLength)
            {
                Buffer = buffer;
                Length = length;
                NumberStart = numberStart;
                NumberLength = numberLength;
            }

            public ReadOnlySpan<char> Text => new(Buffer, 0, Length);
        }

        /// <summary>
        /// Characters of a value as laid out by TMP, or the formatted string itself for other providers,
        /// whose glyph animator works with string indices.
        /// </summary>
        readonly struct CharacterSequence
        {
            readonly TMP_TextInfo textInfo;
            readonly string text;

            public CharacterSequence(TMP_Text laidOut)
            {
                textInfo = laidOut.textInfo;
                text = null;
            }

            public CharacterSequence(string value)
            {
                textInfo = null;
                text = value;
            }

            public int Count => textInfo != null ? textInfo.characterCount : text.Length;
            public char CharAt(int index) => textInfo != null ? textInfo.characterInfo[index].character : text[index];
            public int SourceIndex(int index) => textInfo != null ? textInfo.characterInfo[index].index : index;
            public int SourceEnd(int index) => textInfo != null ? textInfo.characterInfo[index].index + textInfo.characterInfo[index].stringLength : index + 1;

            // Strings have no glyph data, so whitespace and the second half of a surrogate pair count as invisible
            public bool IsVisible(int index) => textInfo != null
                ? textInfo.characterInfo[index].isVisible
                : !char.IsWhiteSpace(text[index]) && !char.IsLowSurrogate(text[index]);
        }

        struct CharacterTransition
        {
            public readonly int IncomingIndex;
            public int OutgoingIndex;
            public readonly float Delay;
            public float HorizontalOffset;
            public readonly bool RollDigit;
            public float Duration;
            public Vector3 StartAnchor;
            public Vector3 EndAnchor;
            public float Height;
            public int OutgoingSourceIndex;
            public int StripSourceIndex;
            public int StripIndex;
            public int PreviousGlyph;
            public int StartDigit;
            public int Direction;
            public int Steps;

            public CharacterTransition(int incomingIndex, int outgoingIndex, float delay, float horizontalOffset, bool rollDigit) : this()
            {
                IncomingIndex = incomingIndex;
                OutgoingIndex = outgoingIndex;
                Delay = delay;
                HorizontalOffset = horizontalOffset;
                RollDigit = rollDigit;
                StripIndex = -1;
                PreviousGlyph = -1;
                OutgoingSourceIndex = -1;
            }
        }

        sealed class MeshSnapshot
        {
            Vector3[][] vertices = Array.Empty<Vector3[]>();
            Color32[][] colors = Array.Empty<Color32[]>();
            int[] vertexCounts = Array.Empty<int>();
            int[] colorCounts = Array.Empty<int>();
            int meshCount;

            public void Capture(TMP_Text text)
            {
                TMP_MeshInfo[] meshInfo = text.textInfo.meshInfo;
                EnsureMeshCapacity(meshInfo.Length);
                meshCount = meshInfo.Length;

                for (int i = 0; i < meshCount; i++)
                {
                    Vector3[] sourceVertices = meshInfo[i].vertices;
                    Color32[] sourceColors = meshInfo[i].colors32;
                    vertices[i] = EnsureCapacity(vertices[i], sourceVertices.Length);
                    colors[i] = EnsureCapacity(colors[i], sourceColors.Length);
                    vertexCounts[i] = sourceVertices.Length;
                    colorCounts[i] = sourceColors.Length;
                    Array.Copy(sourceVertices, vertices[i], sourceVertices.Length);
                    Array.Copy(sourceColors, colors[i], sourceColors.Length);
                }
            }

            public void Clear() => meshCount = 0;

            public void Restore(TMP_Text text)
            {
                TMP_MeshInfo[] meshInfo = text.textInfo.meshInfo;
                int count = Mathf.Min(meshInfo.Length, meshCount);
                for (int i = 0; i < count; i++)
                {
                    Array.Copy(vertices[i], meshInfo[i].vertices, Mathf.Min(vertexCounts[i], meshInfo[i].vertices.Length));
                    Array.Copy(colors[i], meshInfo[i].colors32, Mathf.Min(colorCounts[i], meshInfo[i].colors32.Length));
                }
            }

            public void SetAlpha(TMP_Text text, float alpha)
            {
                // Include decoration geometry (underline / strikethrough), not just
                // character quads, so the hidden strip cannot leak extra graphics.
                TMP_MeshInfo[] meshInfo = text.textInfo.meshInfo;
                for (int material = 0; material < Mathf.Min(meshCount, meshInfo.Length); material++)
                {
                    Color32[] destination = meshInfo[material].colors32;
                    int count = Mathf.Min(colorCounts[material], destination.Length);
                    for (int vertex = 0; vertex < count; vertex++)
                    {
                        Color32 color = colors[material][vertex];
                        color.a = (byte)Mathf.RoundToInt(color.a * Mathf.Clamp01(alpha));
                        destination[vertex] = color;
                    }
                }
            }

            public void ApplyCharacter(TMP_Text text, int characterIndex, Vector3 offset, float alpha)
            {
                TMP_TextInfo textInfo = text.textInfo;
                if (characterIndex < 0 || characterIndex >= textInfo.characterCount)
                    return;

                // Character info is a large struct, so it is read in place every frame instead of copied
                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[characterIndex];
                if (!character.isVisible)
                    return;

                int materialIndex = character.materialReferenceIndex;
                int vertexIndex = character.vertexIndex;
                TMP_MeshInfo[] meshInfo = textInfo.meshInfo;

                if (materialIndex < 0 || materialIndex >= meshInfo.Length || materialIndex >= meshCount ||
                    vertexIndex < 0 || vertexIndex + 3 >= meshInfo[materialIndex].vertices.Length ||
                    vertexIndex + 3 >= vertexCounts[materialIndex])
                    return;

                Vector3[] destinationVertices = meshInfo[materialIndex].vertices;
                Vector3[] sourceVertices = vertices[materialIndex];

                for (int i = 0; i < 4; i++)
                    destinationVertices[vertexIndex + i] = sourceVertices[vertexIndex + i] + offset;

                SetCharacterAlpha(materialIndex, vertexIndex, meshInfo, alpha);
            }

            void SetCharacterAlpha(int materialIndex, int vertexIndex, TMP_MeshInfo[] meshInfo, float alpha)
            {
                if (materialIndex < 0 || materialIndex >= meshInfo.Length || materialIndex >= meshCount ||
                    vertexIndex < 0 || vertexIndex + 3 >= meshInfo[materialIndex].colors32.Length ||
                    vertexIndex + 3 >= colorCounts[materialIndex])
                    return;

                Color32[] destinationColors = meshInfo[materialIndex].colors32;
                Color32[] sourceColors = colors[materialIndex];
                float clampedAlpha = Mathf.Clamp01(alpha);

                for (int i = 0; i < 4; i++)
                {
                    Color32 color = sourceColors[vertexIndex + i];
                    color.a = (byte)Mathf.RoundToInt(color.a * clampedAlpha);
                    destinationColors[vertexIndex + i] = color;
                }
            }

            void EnsureMeshCapacity(int required)
            {
                if (vertices.Length >= required)
                    return;

                int capacity = Mathf.NextPowerOfTwo(Mathf.Max(1, required));
                Array.Resize(ref vertices, capacity);
                Array.Resize(ref colors, capacity);
                Array.Resize(ref vertexCounts, capacity);
                Array.Resize(ref colorCounts, capacity);
            }

            static T[] EnsureCapacity<T>(T[] buffer, int required)
            {
                if (buffer != null && buffer.Length >= required)
                    return buffer;

                if (required == 0)
                    return Array.Empty<T>();

                int capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, required));
                return new T[capacity];
            }
        }
        #endregion

        #region Animation
        void AnimateToTarget()
        {
            Initialize();

            // Retarget from the dominant visible digit in each column, not the unrelated
            // numeric interpolation (columns have independent delays and paths).
            FormattedValue from = hasRollVisuals ? CaptureVisibleValue() : FormatValue(currentValue, ref fromText);
            StopAnimation();
            UpdateCachedFormats();

            float startValue = currentValue;
            float targetValue = value;
            FormattedValue to = FormatValue(targetValue, ref toText);

            rollText = textObject as TMP_Text;
            if (rollText == null)
                glyphAnimator = TextGlyphAnimator.Resolve(glyphAnimator, textObject);

            if ((rollText == null && glyphAnimator == null) || counterDuration <= 0f || from.Text.SequenceEqual(to.Text))
            {
                currentValue = targetValue;
                WriteText(to);
                return;
            }

            animationStartValue = startValue;
            animationTargetValue = targetValue;
            animationToText = to;
            layoutDuration = Mathf.Max(0.01f, counterDuration);

            if (rollText != null) { PrepareRoll(from, to); }
            else { PrepareGlyphRoll(from, to); }

            if (characterTransitions.Count == 0)
            {
                currentValue = targetValue;
                HideRollLayer();
                return;
            }

            ApplyRoll(0f);
            currentAnimation = EvoTween.Create(0f, animationDuration, animationDuration)
                .WithDelay(delay)
                .WithEase(Ease.Linear)
                .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                .OnComplete(completionCallback)
                .Bind(this, TweenUpdateCallback, TweenPropertyKey);
        }

        static void ApplyTweenValue(Counter counter, float elapsed) => counter.ApplyRoll(elapsed);

        void ApplyRoll(float elapsed)
        {
            lastElapsed = elapsed;

            if (rollText != null)
            {
                ApplyRollGeometry(elapsed);

                if (hasRollVisuals && outgoingText != null)
                {
                    rollText.UpdateVertexData(RollChannels);
                    outgoingText.UpdateVertexData(RollChannels);
                }
            }
            else if (hasRollVisuals)
                ApplyGlyphRoll(elapsed);

            float normalizedTime = animationDuration > 0f ? Mathf.Clamp01(elapsed / animationDuration) : 1f;
            currentValue = Mathf.LerpUnclamped(animationStartValue, animationTargetValue, EvaluateCurve(normalizedTime));
        }

        /// <summary>
        /// Draws the roll through the glyph animator. Mirrors <see cref="ApplyRollGeometry"/>: the new text
        /// is the incoming layer, and outgoing digits and digit strips are drawn as extra digits.
        /// </summary>
        void ApplyGlyphRoll(float elapsed)
        {
            float direction = GetRollDirection();
            float layoutTime = GetLayoutTime(elapsed);

            for (int i = 0; i < characterTransitions.Count; i++)
            {
                CharacterTransition transition = characterTransitions[i];

                if (!transition.RollDigit)
                {
                    glyphAnimator.MoveGlyph(transition.IncomingIndex, transition.OutgoingIndex, layoutTime, 0f, 0f, 1f);
                    continue;
                }

                float localTime = GetLocalTime(transition, elapsed);
                float easedTime = EvaluateCurve(localTime);

                // The strip replaces the incoming glyph until it settles on it
                if (transition.Steps > 0)
                {
                    float position = easedTime * transition.Steps;
                    int step = Mathf.Clamp(Mathf.FloorToInt(position), 0, transition.Steps - 1);
                    float phase = position - step;

                    glyphAnimator.MoveGlyph(transition.IncomingIndex, -1, 1f, 0f, 0f, 0f);
                    AddStripDigit(transition, step, layoutTime, transition.Direction * rollDistance * phase, GetFadeAlpha(1f - phase, phase >= 1f));
                    AddStripDigit(transition, step + 1, layoutTime, -transition.Direction * rollDistance * (1f - phase), GetFadeAlpha(phase, phase <= 0f));
                    continue;
                }

                float incomingAlpha = GetFadeAlpha(easedTime, localTime <= 0f);
                float outgoingAlpha = GetFadeAlpha(1f - easedTime, localTime >= 1f);

                if (transition.IncomingIndex >= 0)
                {
                    glyphAnimator.MoveGlyph(transition.IncomingIndex, transition.OutgoingIndex, layoutTime,
                        0f, -direction * rollDistance * (1f - easedTime), incomingAlpha);
                }

                if (transition.OutgoingIndex >= 0 && outgoingAlpha > 0f)
                {
                    glyphAnimator.AddDigit(outgoingCharacters.CharAt(transition.OutgoingIndex), transition.IncomingIndex,
                        transition.OutgoingIndex, layoutTime, 0f, direction * rollDistance * easedTime, outgoingAlpha);
                }
            }

            glyphAnimator.Apply();
        }

        void AddStripDigit(in CharacterTransition transition, int step, float layoutTime, float heights, float alpha)
        {
            if (alpha <= 0f)
                return;

            glyphAnimator.AddDigit(GetStripDigit(transition, step), transition.IncomingIndex, transition.OutgoingIndex, layoutTime, 0f, heights, alpha);
        }

        void ApplyRollGeometry(float elapsed)
        {
            if (!hasRollVisuals || rollText == null || outgoingText == null)
                return;

            float direction = GetRollDirection();
            float layoutTime = GetLayoutTime(elapsed);

            for (int i = 0; i < characterTransitions.Count; i++)
            {
                CharacterTransition transition = characterTransitions[i];
                float incomingX = -transition.HorizontalOffset * (1f - layoutTime);

                if (!transition.RollDigit)
                {
                    // A glyph that does not move keeps its captured vertices
                    if (Mathf.Abs(transition.HorizontalOffset) >= 0.001f)
                        incomingMesh.ApplyCharacter(rollText, transition.IncomingIndex, Vector3.right * incomingX, 1f);
                   
                    continue;
                }

                float localTime = GetLocalTime(transition, elapsed);
                float easedTime = EvaluateCurve(localTime);

                // The hidden layer contains the whole path. Only its two adjacent glyphs
                // are visible. No string changes, glyph lookups, or TMP rebuilds per frame.
                if (transition.StripIndex >= 0)
                {
                    outgoingMesh.ApplyCharacter(outgoingText, transition.PreviousGlyph, Vector3.zero, 0f);
                    outgoingMesh.ApplyCharacter(outgoingText, transition.PreviousGlyph + 1, Vector3.zero, 0f);
                    incomingMesh.ApplyCharacter(rollText, transition.IncomingIndex, Vector3.zero, 0f);

                    float position = easedTime * transition.Steps;
                    int step = Mathf.Clamp(Mathf.FloorToInt(position), 0, transition.Steps - 1);
                    float phase = position - step;
                    int glyph = transition.StripIndex + step;
                    Vector3 anchor = Vector3.LerpUnclamped(transition.StartAnchor, transition.EndAnchor, layoutTime);
                    float distance = transition.Height * rollDistance;

                    ApplyStripGlyph(glyph, anchor, transition.Direction * distance * phase, GetFadeAlpha(1f - phase, phase >= 1f));
                    ApplyStripGlyph(glyph + 1, anchor, -transition.Direction * distance * (1f - phase), GetFadeAlpha(phase, phase <= 0f));

                    transition.PreviousGlyph = glyph;
                    characterTransitions[i] = transition;
                    continue;
                }

                float incomingAlpha = GetFadeAlpha(easedTime, localTime <= 0f);
                float outgoingAlpha = GetFadeAlpha(1f - easedTime, localTime >= 1f);

                if (transition.IncomingIndex >= 0)
                {
                    float distance = GetCharacterHeight(rollText, transition.IncomingIndex) * rollDistance;
                    Vector3 offset = new(incomingX, -direction * distance * (1f - easedTime), 0f);
                    incomingMesh.ApplyCharacter(rollText, transition.IncomingIndex, offset, incomingAlpha);
                }

                if (transition.OutgoingIndex >= 0)
                {
                    Vector3 anchor = Vector3.LerpUnclamped(transition.StartAnchor, transition.EndAnchor, layoutTime);
                    ApplyStripGlyph(transition.OutgoingIndex, anchor, direction * transition.Height * rollDistance * easedTime, outgoingAlpha);
                }
            }
        }

        void ApplyStripGlyph(int index, Vector3 anchor, float y, float alpha)
        {
            if (index < 0 || index >= outgoingText.textInfo.characterCount)
                return;

            Vector3 offset = anchor - GetCharacterAnchor(outgoingText, index);
            offset.y += y;
            outgoingMesh.ApplyCharacter(outgoingText, index, offset, alpha);
        }

        float GetRollDirection() => animationTargetValue >= animationStartValue ? 1f : -1f;

        float GetLayoutTime(float elapsed) => EvaluateCurve(Mathf.Clamp01(elapsed / layoutDuration));

        static float GetLocalTime(in CharacterTransition transition, float elapsed) => Mathf.Clamp01((elapsed - transition.Delay) / transition.Duration);

        static char GetStripDigit(in CharacterTransition transition, int step) => (char)('0' + (transition.StartDigit + transition.Direction * step + 10) % 10);

        // A moving digit fades by Fade Amount, and is hidden before it enters or after it leaves
        float GetFadeAlpha(float visibility, bool hidden) => hidden ? 0f : Mathf.Lerp(1f, GetCrossFade(visibility), fadeAmount);

        static float GetCrossFade(float visibility) => TweenEase.Evaluate(Ease.InOutSine, (visibility - FadeStart) / (FadeEnd - FadeStart));

        void CompleteAnimation()
        {
            currentAnimation = default;
            currentValue = animationTargetValue;
            HideRollLayer();
        }

        void StopAnimation()
        {
            currentAnimation.Kill();
            currentAnimation = default;
            HideRollLayer();
        }
        #endregion

        #region Character Transitions
        void PrepareRoll(in FormattedValue from, in FormattedValue to)
        {
            EnsureRollLayer();
            SyncRollLayerStyle();

            WriteText(to);
            outgoingText.SetText(from.Buffer, 0, from.Length);
            outgoingText.gameObject.SetActive(true);
            rollText.ForceMeshUpdate(true, true);
            outgoingText.ForceMeshUpdate(true, true);
            incomingMesh.Capture(rollText);
            incomingCharacters = new CharacterSequence(rollText);
            outgoingCharacters = new CharacterSequence(outgoingText);

            BuildTransitions(from, to);
            BuildDigitStrips(from, to);
           
            outgoingMesh.Capture(outgoingText);
            outgoingMesh.SetAlpha(outgoingText, 0f);
            hasRollVisuals = true;
        }

        /// <summary>
        /// Plans the roll on the formatted strings. The animator measures glyphs once the new text is laid out.
        /// </summary>
        void PrepareGlyphRoll(in FormattedValue from, in FormattedValue to)
        {
            // Glyph animators take strings. The start is usually the text already shown, so it is reused
            string startText = Utilities.ReuseString(ref lastString, from.Text);
            string targetText = Utilities.ReuseString(ref lastString, to.Text);

            incomingCharacters = new CharacterSequence(targetText);
            outgoingCharacters = new CharacterSequence(startText);
            BuildTransitions(from, to);

            glyphAnimator.Begin(startText, targetText);
            hasRollVisuals = true;
        }

        /// <summary>
        /// Pairs digits by place and plans their timing. Glyph geometry is only measured for TMP text.
        /// </summary>
        void BuildTransitions(in FormattedValue from, in FormattedValue to)
        {
            characterTransitions.Clear();
            rollingDigitCount = 0;
            animationDuration = layoutDuration;

            CollectDigitIndices(incomingCharacters, to.NumberStart, to.NumberLength, incomingDigitIndices);
            CollectDigitIndices(outgoingCharacters, from.NumberStart, from.NumberLength, outgoingDigitIndices);

            bool measure = rollText != null;
            int digitCount = Mathf.Max(incomingDigitIndices.Count, outgoingDigitIndices.Count);
            bool crossedZero = (animationStartValue < 0f) != (animationTargetValue < 0f);

            for (int place = 0; place < digitCount; place++)
            {
                int incomingIndex = GetIndexFromRight(incomingDigitIndices, place);
                int outgoingIndex = GetIndexFromRight(outgoingDigitIndices, place);
                float horizontalOffset = 0f;
                if (measure && incomingIndex >= 0 && outgoingIndex >= 0)
                    horizontalOffset = GetCharacterAnchor(rollText, incomingIndex).x - GetCharacterAnchor(outgoingText, outgoingIndex).x;

                bool sameDigit = !crossedZero && incomingIndex >= 0 && outgoingIndex >= 0 &&
                    incomingCharacters.CharAt(incomingIndex) == outgoingCharacters.CharAt(outgoingIndex);
                if (sameDigit)
                {
                    AddLayoutTransition(incomingIndex, outgoingIndex, horizontalOffset);
                    continue;
                }

                CharacterTransition transition = new(incomingIndex, outgoingIndex, rollingDigitCount * digitStagger, horizontalOffset, true)
                {
                    Duration = layoutDuration
                };

                if (measure)
                {
                    transition.StartAnchor = outgoingIndex >= 0 ? GetCharacterAnchor(outgoingText, outgoingIndex) : GetCharacterAnchor(rollText, incomingIndex);
                    transition.EndAnchor = incomingIndex >= 0 ? GetCharacterAnchor(rollText, incomingIndex) : transition.StartAnchor;
                    transition.Height = incomingIndex >= 0 ? GetCharacterHeight(rollText, incomingIndex) : GetCharacterHeight(outgoingText, outgoingIndex);
                    transition.OutgoingSourceIndex = outgoingIndex >= 0 ? outgoingCharacters.SourceIndex(outgoingIndex) : -1;
                }

                if (rollThroughDigits && incomingIndex >= 0 && outgoingIndex >= 0)
                {
                    char start = outgoingCharacters.CharAt(outgoingIndex);
                    char end = incomingCharacters.CharAt(incomingIndex);

                    // Formatted numbers use ASCII digits. Other characters retain the direct transition.
                    if (start >= '0' && start <= '9' && end >= '0' && end <= '9' && start != end)
                    {
                        transition.StartDigit = start - '0';
                        // Negative values display magnitudes: -6 -> -3 must roll 6, 5, 4, 3.
                        transition.Direction = Mathf.Abs(animationTargetValue) >= Mathf.Abs(animationStartValue) ? 1 : -1;
                        transition.Steps = (transition.Direction * (end - start) + 10) % 10;
                        transition.Duration = Mathf.Max(layoutDuration, transition.Steps * Mathf.Max(0f, minimumStepDuration));
                    }
                }

                animationDuration = Mathf.Max(animationDuration, transition.Delay + transition.Duration);
                characterTransitions.Add(transition);
                rollingDigitCount++;
            }

            AddStableTextLayoutTransitions();
        }

        void BuildDigitStrips(in FormattedValue from, in FormattedValue to)
        {
            int stripLength = 0;
            ReadOnlySpan<char> target = to.Text;

            // Expand digits in the destination's original rich-text context. TMP handles
            // font fallback, materials, colors and glyph UVs once for the complete strip.
            for (int source = 0; source < target.Length; source++)
            {
                int match = -1;
                for (int i = 0; i < characterTransitions.Count; i++)
                {
                    CharacterTransition t = characterTransitions[i];
                    if (t.Steps > 0 && rollText.textInfo.characterInfo[t.IncomingIndex].index == source)
                    {
                        match = i;
                        break;
                    }
                }

                if (match < 0)
                {
                    Utilities.Append(ref stripText, ref stripLength, target[source]);
                    continue;
                }

                CharacterTransition transition = characterTransitions[match];
                transition.StripSourceIndex = stripLength;
                for (int step = 0; step <= transition.Steps; step++)
                    Utilities.Append(ref stripText, ref stripLength, GetStripDigit(transition, step));

                characterTransitions[match] = transition;
            }

            float previousFontSize = outgoingText.fontSize;
            Utilities.Append(ref stripText, ref stripLength, '\n');

            if (rollText.richText)
            {
                Utilities.Append(ref stripText, ref stripLength, "<size=");
                Utilities.Append(ref stripText, ref stripLength, GetFontSizeText(previousFontSize));
                Utilities.Append(ref stripText, ref stripLength, '>');
            }

            int outgoingSourceStart = stripLength;
            Utilities.Append(ref stripText, ref stripLength, from.Text);

            if (rollText.richText)
                Utilities.Append(ref stripText, ref stripLength, "</size>");

            outgoingText.enableAutoSizing = false;
            outgoingText.fontSize = rollText.fontSize;
            outgoingText.textWrappingMode = TextWrappingModes.NoWrap;
            outgoingText.overflowMode = TextOverflowModes.Overflow;
            outgoingText.SetText(stripText, 0, stripLength);
            outgoingText.ForceMeshUpdate(true, true);

            for (int i = 0; i < characterTransitions.Count; i++)
            {
                CharacterTransition transition = characterTransitions[i];
                transition.OutgoingIndex = transition.OutgoingSourceIndex < 0 ? -1 : FindCharacterAtSource(outgoingText, outgoingSourceStart + transition.OutgoingSourceIndex);
                if (transition.Steps > 0)
                {
                    transition.StripIndex = FindCharacterAtSource(outgoingText, transition.StripSourceIndex);
                    transition.PreviousGlyph = transition.StripIndex;
                }
                characterTransitions[i] = transition;
            }
        }

        static int FindCharacterAtSource(TMP_Text text, int source)
        {
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                if (text.textInfo.characterInfo[i].index == source)
                    return i;
            }
            return -1;
        }

        FormattedValue CaptureVisibleValue()
        {
            int length = 0;
            Utilities.Append(ref fromText, ref length, animationToText.Text);

            for (int i = 0; i < characterTransitions.Count; i++)
            {
                CharacterTransition transition = characterTransitions[i];
                if (transition.Steps <= 0 || transition.IncomingIndex < 0)
                    continue;
                float time = EvaluateCurve(GetLocalTime(transition, lastElapsed));
                int step = Mathf.Clamp(Mathf.RoundToInt(time * transition.Steps), 0, transition.Steps);
                int source = incomingCharacters.SourceIndex(transition.IncomingIndex);
                fromText[source] = GetStripDigit(transition, step);
            }

            return new FormattedValue(fromText, length, animationToText.NumberStart, animationToText.NumberLength);
        }

        void OnIncomingTextRebuilt(TMP_TextInfo _)
        {
            if (!hasRollVisuals)
                return;

            incomingMesh.Capture(rollText);
            for (int i = 0; i < characterTransitions.Count; i++)
            {
                CharacterTransition transition = characterTransitions[i];
                if (transition.IncomingIndex < 0 || transition.IncomingIndex >= rollText.textInfo.characterCount)
                    continue;

                Vector3 end = GetCharacterAnchor(rollText, transition.IncomingIndex);
                transition.HorizontalOffset += end.x - transition.EndAnchor.x;
                transition.EndAnchor = end;
                characterTransitions[i] = transition;
            }

            ApplyRollGeometry(lastElapsed);
        }

        void OnOutgoingTextRebuilt(TMP_TextInfo _)
        {
            if (!hasRollVisuals)
                return;

            outgoingMesh.Capture(outgoingText);
            outgoingMesh.SetAlpha(outgoingText, 0f);
            ApplyRollGeometry(lastElapsed);
        }

        void AddStableTextLayoutTransitions()
        {
            int incomingNumberStart = GetNumberStart(incomingCharacters, incomingDigitIndices);
            int incomingNumberEnd = GetNumberEnd(incomingCharacters, incomingDigitIndices);
            int outgoingNumberStart = GetNumberStart(outgoingCharacters, outgoingDigitIndices);
            int outgoingNumberEnd = GetNumberEnd(outgoingCharacters, outgoingDigitIndices);

            CollectCharacterIndices(incomingCharacters, 0, incomingNumberStart, CharacterFilter.All, incomingLayoutIndices);
            CollectCharacterIndices(outgoingCharacters, 0, outgoingNumberStart, CharacterFilter.All, outgoingLayoutIndices);
            AddMatchingLayoutTransitions(false);

            CollectCharacterIndices(incomingCharacters, incomingNumberEnd, int.MaxValue, CharacterFilter.All, incomingLayoutIndices);
            CollectCharacterIndices(outgoingCharacters, outgoingNumberEnd, int.MaxValue, CharacterFilter.All, outgoingLayoutIndices);
            AddMatchingLayoutTransitions(true);

            CollectCharacterIndices(incomingCharacters, incomingNumberStart, incomingNumberEnd, CharacterFilter.NonDigits, incomingLayoutIndices);
            CollectCharacterIndices(outgoingCharacters, outgoingNumberStart, outgoingNumberEnd, CharacterFilter.NonDigits, outgoingLayoutIndices);
            AddMatchingLayoutTransitions(true);
        }

        void AddMatchingLayoutTransitions(bool fromRight)
        {
            int count = Mathf.Min(incomingLayoutIndices.Count, outgoingLayoutIndices.Count);

            for (int i = 0; i < count; i++)
            {
                int incomingListIndex = fromRight ? incomingLayoutIndices.Count - 1 - i : i;
                int outgoingListIndex = fromRight ? outgoingLayoutIndices.Count - 1 - i : i;
                int incomingIndex = incomingLayoutIndices[incomingListIndex];
                int outgoingIndex = outgoingLayoutIndices[outgoingListIndex];
                if (incomingCharacters.CharAt(incomingIndex) != outgoingCharacters.CharAt(outgoingIndex))
                    continue;

                float horizontalOffset = rollText != null ? GetCharacterAnchor(rollText, incomingIndex).x - GetCharacterAnchor(outgoingText, outgoingIndex).x : 0f;
                AddLayoutTransition(incomingIndex, outgoingIndex, horizontalOffset);
            }
        }

        /// <summary>
        /// Slides a glyph that keeps its character. TMP glyphs that did not move are kept too, since a resize
        /// during the roll (a fitter growing the rect around its pivot) moves them later;
        /// the glyph animator measures the offset itself once the new text is laid out.
        /// </summary>
        void AddLayoutTransition(int incomingIndex, int outgoingIndex, float horizontalOffset)
        {
            if (incomingIndex < 0)
                return;

            if (rollText == null)
            {
                characterTransitions.Add(new CharacterTransition(incomingIndex, outgoingIndex, 0f, 0f, false));
                return;
            }

            characterTransitions.Add(new CharacterTransition(incomingIndex, -1, 0f, horizontalOffset, false)
            {
                EndAnchor = GetCharacterAnchor(rollText, incomingIndex)
            });
        }

        static int GetNumberStart(in CharacterSequence characters, List<int> digitIndices)
        {
            if (digitIndices.Count == 0)
                return 0;

            return characters.SourceIndex(digitIndices[0]);
        }

        static int GetNumberEnd(in CharacterSequence characters, List<int> digitIndices)
        {
            if (digitIndices.Count == 0)
                return int.MaxValue;

            return characters.SourceEnd(digitIndices[^1]);
        }

        static void CollectCharacterIndices(in CharacterSequence characters, int start, int end, CharacterFilter filter, List<int> results)
        {
            results.Clear();
            for (int i = 0; i < characters.Count; i++)
            {
                int source = characters.SourceIndex(i);
                if (!characters.IsVisible(i) || source < start || source >= end)
                    continue;

                bool isDigit = char.IsDigit(characters.CharAt(i));
                if (filter == CharacterFilter.All || (filter == CharacterFilter.Digits && isDigit) || (filter == CharacterFilter.NonDigits && !isDigit))
                    results.Add(i);
            }
        }

        static int GetIndexFromRight(List<int> indices, int offset)
        {
            int index = indices.Count - 1 - offset;
            return index >= 0 ? indices[index] : -1;
        }

        static void CollectDigitIndices(in CharacterSequence characters, int start, int length, List<int> results)
        {
            CollectCharacterIndices(characters, start, start + length, CharacterFilter.Digits, results);
        }

        static Vector3 GetCharacterAnchor(TMP_Text text, int characterIndex)
        {
            ref readonly TMP_CharacterInfo character = ref text.textInfo.characterInfo[characterIndex];
            return new Vector3((character.bottomLeft.x + character.topRight.x) * 0.5f, character.baseLine, 0f);
        }

        static float GetCharacterHeight(TMP_Text text, int characterIndex)
        {
            ref readonly TMP_CharacterInfo character = ref text.textInfo.characterInfo[characterIndex];
            return Mathf.Max(1f, character.ascender - character.descender);
        }

        float EvaluateCurve(float time)
        {
            if (time <= 0f)
                return 0f;
            if (time >= 1f)
                return 1f;
            if (animationEase == Ease.Custom && (animationCurve == null || animationCurve.length == 0))
                return Mathf.SmoothStep(0f, 1f, time);

            // Not clamped, so Back and Elastic eases overshoot
            return TweenEase.Evaluate(animationEase, time, animationCurve);
        }
        #endregion

        #region Roll Layer
        void EnsureRollLayer()
        {
            if (subscribedText != rollText)
            {
                if (subscribedText != null)
                    subscribedText.OnPreRenderText -= OnIncomingTextRebuilt;
                subscribedText = rollText;
                if (subscribedText != null)
                    subscribedText.OnPreRenderText += OnIncomingTextRebuilt;
                if (outgoingText != null && rollText != null)
                    outgoingText.rectTransform.SetParent(rollText.rectTransform, false);
            }

            if (outgoingText != null || rollText == null)
                return;

            GameObject layerObject = new(RollLayerName, typeof(RectTransform), typeof(CanvasRenderer))
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = rollText.gameObject.layer
            };
            layerObject.transform.SetParent(rollText.rectTransform, false);
            outgoingText = layerObject.AddComponent<TextMeshProUGUI>();
            layerObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            outgoingText.OnPreRenderText += OnOutgoingTextRebuilt;

            RectTransform layerRect = outgoingText.rectTransform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            layerRect.localRotation = Quaternion.identity;
            layerRect.localScale = Vector3.one;
        }

        void SyncRollLayerStyle()
        {
            if (outgoingText == null || rollText == null)
                return;

            outgoingText.font = rollText.font;
            outgoingText.fontSharedMaterial = rollText.fontSharedMaterial;
            outgoingText.color = rollText.color;
            outgoingText.fontSize = rollText.fontSize;
            outgoingText.fontStyle = rollText.fontStyle;
            outgoingText.fontWeight = rollText.fontWeight;
            outgoingText.alignment = rollText.alignment;
            outgoingText.enableAutoSizing = rollText.enableAutoSizing;
            outgoingText.fontSizeMin = rollText.fontSizeMin;
            outgoingText.fontSizeMax = rollText.fontSizeMax;
            outgoingText.characterSpacing = rollText.characterSpacing;
            outgoingText.wordSpacing = rollText.wordSpacing;
            outgoingText.lineSpacing = rollText.lineSpacing;
            outgoingText.paragraphSpacing = rollText.paragraphSpacing;
            outgoingText.textWrappingMode = rollText.textWrappingMode;
            outgoingText.overflowMode = rollText.overflowMode;
            outgoingText.richText = rollText.richText;
            outgoingText.parseCtrlCharacters = rollText.parseCtrlCharacters;
            outgoingText.isRightToLeftText = rollText.isRightToLeftText;
            outgoingText.extraPadding = rollText.extraPadding;
            outgoingText.tintAllSprites = rollText.tintAllSprites;
            outgoingText.enableVertexGradient = rollText.enableVertexGradient;
            outgoingText.colorGradient = rollText.colorGradient;
            outgoingText.colorGradientPreset = rollText.colorGradientPreset;
            outgoingText.overrideColorTags = rollText.overrideColorTags;
            outgoingText.spriteAsset = rollText.spriteAsset;
            outgoingText.styleSheet = rollText.styleSheet;
            outgoingText.margin = rollText.margin;
            // Glyph positions are relative to the pivot, so the layer shares the text's pivot (and with it the same local space)
            outgoingText.rectTransform.pivot = rollText.rectTransform.pivot;
            outgoingText.raycastTarget = false;
            outgoingText.maskable = rollText.maskable;
            outgoingText.enabled = rollText.enabled;
        }

        void HideRollLayer()
        {
            if (hasRollVisuals && rollText != null)
            {
                incomingMesh.Restore(rollText);
                rollText.UpdateVertexData(RollChannels);
            }
            else if (hasRollVisuals)
                glyphAnimator?.End();

            hasRollVisuals = false;
            characterTransitions.Clear();
            incomingMesh.Clear();
            outgoingMesh.Clear();

            if (outgoingText != null)
                outgoingText.gameObject.SetActive(false);
        }
        #endregion

        #region Formatting
        void UpdateDisplay(float displayValue)
        {
            // The roll's target stays in toText, so displays format into fromText
            if (textObject != null)
                WriteText(FormatValue(displayValue, ref fromText));
        }

        /// <summary>
        /// Shows the text. TextMesh Pro takes the buffer in play mode. Other providers, edit mode and
        /// TMP preprocessors (which only process strings) get a string, reused while the text does not change.
        /// </summary>
        void WriteText(in FormattedValue formatted)
        {
            if (textObject == null)
                return;

            ReadOnlySpan<char> text = formatted.Text;
            if (textObject is TMP_Text tmp && tmp.textPreprocessor == null && Application.isPlaying)
            {
                // A buffer always rebuilds the text, so an unchanged one is skipped, as TMP skips an unchanged string
                if (ReferenceEquals(shownTarget, tmp) && text.SequenceEqual(shownText.AsSpan(0, shownLength)))
                    return;

                tmp.SetText(formatted.Buffer, 0, formatted.Length);
                shownTarget = tmp;
                shownLength = 0;
                Utilities.Append(ref shownText, ref shownLength, text);
                return;
            }

            shownTarget = null;
            TextProvider.SetText(textObject, Utilities.ReuseString(ref lastString, text));
        }

        /// <summary>
        /// Formats the value into the buffer. Formats made of text, {0} and escaped braces are expanded here,
        /// others (alignment, format strings or invalid formats) go through string.Format.
        /// </summary>
        FormattedValue FormatValue(float displayValue, ref char[] buffer)
        {
            // The number as displayed, inside the spacing tag when there is one
            string spacingTag = digitSpacing.OpenTag;
            int displayLength = 0;
            int numberLength;
            Utilities.Append(ref numberText, ref displayLength, spacingTag);

            while (!displayValue.TryFormat(numberText.AsSpan(displayLength), out numberLength, cachedFormatString, CultureInfo.CurrentCulture))
                Array.Resize(ref numberText, numberText.Length * 2);

            displayLength += numberLength;
            if (spacingTag.Length > 0)
                Utilities.Append(ref numberText, ref displayLength, "</mspace>");

            ReadOnlySpan<char> displayNumber = numberText.AsSpan(0, displayLength);
            int length = 0;

            if (string.IsNullOrEmpty(cachedTextFormat) || cachedTextFormat == "{0}")
            {
                Utilities.Append(ref buffer, ref length, displayNumber);
                return new FormattedValue(buffer, length, spacingTag.Length, numberLength);
            }

            if (expandTextFormat) { AppendTextFormat(ref buffer, ref length, displayNumber); }
            else { Utilities.Append(ref buffer, ref length, string.Format(CultureInfo.CurrentCulture, cachedTextFormat, displayNumber.ToString())); }

            int displayNumberStart = buffer.AsSpan(0, length).IndexOf(displayNumber);
            int numberStart = Mathf.Max(0, displayNumberStart) + spacingTag.Length;
            return new FormattedValue(buffer, length, numberStart, numberLength);
        }

        void AppendTextFormat(ref char[] buffer, ref int length, ReadOnlySpan<char> displayNumber)
        {
            string format = cachedTextFormat;

            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                if (c == '{' && format[i + 1] == '0')
                {
                    Utilities.Append(ref buffer, ref length, displayNumber);
                    i += 2;
                    continue;
                }

                // An escaped brace is written once
                Utilities.Append(ref buffer, ref length, c);
                if (c == '{' || c == '}')
                    i++;
            }
        }

        /// <summary>
        /// True when the format holds only text, {0} items, {{ and }}, which expand exactly as string.Format would.
        /// </summary>
        static bool CanExpand(string format)
        {
            for (int i = 0; i < format.Length; i++)
            {
                char c = format[i];
                if (c == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{') { i++; }
                    else if (i + 2 < format.Length && format[i + 1] == '0' && format[i + 2] == '}') { i += 2; }
                    else { return false; }
                }
                else if (c == '}')
                {
                    if (i + 1 < format.Length && format[i + 1] == '}') { i++; }
                    else { return false; }
                }
            }

            return true;
        }

        // The size tag of the roll layer's outgoing text, formatted again only when the size changes
        string GetFontSizeText(float size)
        {
            if (cachedFontSizeText == null || !cachedFontSize.Equals(size))
            {
                cachedFontSize = size;
                cachedFontSizeText = size.ToString("R", CultureInfo.InvariantCulture);
            }

            return cachedFontSizeText;
        }

        void UpdateCachedFormats()
        {
            cachedFormatString = Utilities.GetNumberFormatString(displayFormat);
            string sourceFormat = textFormat ?? string.Empty;
            if (!string.Equals(cachedTextFormatSource, sourceFormat, StringComparison.Ordinal))
            {
                cachedTextFormatSource = sourceFormat;
                cachedTextFormat = sourceFormat.Length == 0 ? string.Empty : sourceFormat.Contains("{0}") ? sourceFormat : sourceFormat + " {0}";
                expandTextFormat = CanExpand(cachedTextFormat);
            }
            digitSpacing.Update(textObject, useUniformDigitWidth, digitWidthScale);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Animates from the current visual value to a new floating-point value.
        /// </summary>
        public void SetValue(float newValue)
        {
            if (value.Equals(newValue) && (currentAnimation.IsActive || currentValue.Equals(newValue)))
                return;

            value = newValue;

            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                AnimateToTarget();
            else
                SetValueInstant(newValue);
        }

        /// <summary>
        /// Animates from the current visual value to a new integer value.
        /// </summary>
        public void SetValue(int newValue) => SetValue((float)newValue);

        /// <summary>
        /// Immediately displays a floating-point value without playing an animation.
        /// </summary>
        public void SetValueInstant(float newValue)
        {
            Initialize();
            StopAnimation();
            value = newValue;
            currentValue = newValue;
            UpdateCachedFormats();
            UpdateDisplay(currentValue);
        }

        /// <summary>
        /// Immediately displays an integer value without playing an animation.
        /// </summary>
        public void SetValueInstant(int newValue) => SetValueInstant((float)newValue);

        /// <summary>
        /// Adds an amount to the target value and animates to the result.
        /// </summary>
        public void AddToValue(float amount) => SetValue(value + amount);

        /// <summary>
        /// Subtracts an amount from the target value and animates to the result.
        /// </summary>
        public void SubtractFromValue(float amount) => SetValue(value - amount);
        #endregion

#if UNITY_EDITOR
        void Reset() => TextProvider.TryGetText(this, out textObject);

        void OnValidate()
        {
            if (textObject == null)
                TextProvider.TryGetText(this, out textObject);

            counterDuration = Mathf.Max(0.01f, counterDuration);
            minimumStepDuration = Mathf.Max(0f, minimumStepDuration);
            delay = Mathf.Max(0f, delay);
            digitStagger = Mathf.Max(0f, digitStagger);
            rollDistance = Mathf.Max(0f, rollDistance);
            fadeAmount = Mathf.Clamp01(fadeAmount);
            digitWidthScale = Mathf.Clamp(digitWidthScale, 0.5f, 2f);
            UpdateCachedFormats();

            if (textObject == null || (Application.isPlaying && currentAnimation.IsActive))
                return;

            UpdateDisplay(Application.isPlaying ? currentValue : value);
        }
#endif
    }
}