using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Distribution Bar"), HelpURL(Constants.HelpUrl + "ui-elements/distribution-bar")]
    public class DistributionBar : MonoBehaviour
    {
        // Bar Settings
        [Tooltip("Ordered shares of the total. Assign a separate fill shape to each segment. Call Refresh after direct runtime edits.")]
        public List<Segment> segments = new()
        {
            new("Blue", 70f),
            new("Red", 30f)
        };
        public Direction direction = Direction.LeftToRight;

        // Text Settings
        public TextDisplay textDisplay = TextDisplay.Percentage;
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed0;
        [Range(0f, 100f), Tooltip("Hide a segment's value text below this share of the total. Zero-value text is always hidden.")]
        public float minimumTextPercentage = 5f;

        // Animation
        public bool enableSmoothing = true;
        [Range(0.05f, 4f)] public float smoothingDuration = 0.3f;
        public AnimationCurve smoothingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Events
        public UnityEvent onValuesChanged = new();

        // Screen pixels a fill tucks under its neighbor: one covers both anti-aliasing ramps, the rest is margin
        const float SeamOverlapPixels = 1.5f;

        // Runtime State
        double totalValue;
        Action completeCallback;
        Action<float> updateCallback;
        TweenHandle valueAnimation;
        DrivenRectTransformTracker tracker;
        bool drivingFills;
        SegmentState[] states = Array.Empty<SegmentState>();

        void OnEnable()
        {
            Refresh();
#if UNITY_EDITOR
            // Plain Instantiate copies, such as Catalog previews, never get OnValidate, so Edit Mode lays out right away too.
            // The delayed refresh still follows, as after OnValidate.
            if (!Application.IsPlaying(gameObject))
                RequestEditorRefresh();
#endif
        }

        void OnDisable()
        {
            StopAnimation();
            tracker.Clear();
            drivingFills = false;
        }

#if UNITY_EDITOR
        void OnDestroy() => CancelEditorRefresh();
#endif

        static float Positive(float value) => value > 0f && !float.IsInfinity(value) ? value : 0f;

        void StopAnimation()
        {
            valueAnimation.Kill();
            valueAnimation = default;
        }

        void CompleteAnimation()
        {
            valueAnimation = default;
            UpdateDisplay(1f);
        }

        void UpdateDisplay(float progress)
        {
            // Clamp custom curves so overshoot cannot produce negative or overlapping shares
            progress = Mathf.Clamp01(Positive(progress));
            int lastVisible = -1;

            for (int i = 0; i < states.Length; i++)
            {
                ref SegmentState state = ref states[i];
                state.currentShare = Mathf.Lerp(state.startShare, state.targetShare, progress);
                state.currentValue = Mathf.Lerp(state.startValue, state.targetValue, progress);
                if (state.currentShare > 0f)
                    lastVisible = i;
            }

            bool vertical = direction == Direction.BottomToTop || direction == Direction.TopToBottom;
            bool reverse = direction == Direction.RightToLeft || direction == Direction.TopToBottom;
            int axis = vertical ? 1 : 0;
            float position = 0f;
            int previousVisible = -1;

            for (int i = 0; i < states.Length; i++)
            {
                ref SegmentState state = ref states[i];
                state.start = state.drawStart = position;
                state.end = state.drawEnd = i == lastVisible ? 1f : Mathf.Min(1f, position + state.currentShare);
                position = state.end;

                if (state.currentShare <= 0f)
                    continue;
                if (previousVisible >= 0)
                    TuckSeam(ref states[previousVisible], ref state, axis);

                previousVisible = i;
            }

            for (int i = 0; i < states.Length; i++)
            {
                ref SegmentState state = ref states[i];
                SetFill(state.fill, axis, reverse ? 1f - state.drawEnd : state.drawStart, reverse ? 1f - state.drawStart : state.drawEnd);
                UpdateText(ref state);
            }
        }

        // Abutting anti-aliased edges let the track show through as a faint line.
        // Tuck the fill drawn first under its neighbor so the boundary pixels stay covered.
        static void TuckSeam(ref SegmentState first, ref SegmentState second, int axis)
        {
            if (first.fill == null || second.fill == null)
                return;

            // Fills under different parents keep segment order
            RectTransform a = first.fill.rectTransform;
            RectTransform b = second.fill.rectTransform;
            Transform parentA = a.parent;
            Transform parentB = b.parent;
            if (parentA == parentB && a.GetSiblingIndex() > b.GetSiblingIndex())
                second.drawStart -= Mathf.Min(GetSeamOverlap(second.fill, parentB, axis), first.end - first.start);
            else
                first.drawEnd += Mathf.Min(GetSeamOverlap(first.fill, parentA, axis), second.end - second.start);
        }

        /// <summary>
        /// Converts the seam overlap from screen pixels to the fill's anchor range.
        /// </summary>
        static float GetSeamOverlap(ProceduralShape fill, Transform fillParent, int axis)
        {
            Canvas canvas = fill.canvas;
            if (canvas == null || fillParent is not RectTransform parent)
                return 0f;

            // A screen-space root Canvas unit spans scaleFactor pixels; world space approximates units as pixels
            Canvas root = canvas.rootCanvas;
            Vector3 length = Vector3.zero;
            length[axis] = parent.rect.size[axis];
            float pixels = root.transform.InverseTransformVector(parent.TransformVector(length)).magnitude * root.scaleFactor;
            return pixels > 0.001f ? SeamOverlapPixels / pixels : 0f;
        }

        bool CanDrive(ProceduralShape fill) => fill != null && fill.transform != transform && !transform.IsChildOf(fill.transform);

        // Bound fills passed CanDrive when the bar refreshed
        static void SetFill(ProceduralShape fill, int axis, float start, float end)
        {
            if (fill == null)
                return;

            RectTransform rect = fill.rectTransform;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.one;
            min[axis] = start;
            max[axis] = end;

            if (rect.anchorMin != min)
                rect.anchorMin = min;
            if (rect.anchorMax != max)
                rect.anchorMax = max;
            if (rect.sizeDelta != Vector2.zero)
                rect.sizeDelta = Vector2.zero;
            if (rect.anchoredPosition != Vector2.zero)
                rect.anchoredPosition = Vector2.zero;
        }

        void UpdateText(ref SegmentState state)
        {
            Graphic text = state.text;
            if (text == null)
                return;

            bool visible = state.currentShare > 0f && state.currentShare * 100f >= minimumTextPercentage;
            if (text.enabled != visible)
                text.enabled = visible;
            if (!visible)
                return;

            double number = textDisplay == TextDisplay.Percentage ? state.currentShare * 100d : state.currentValue;
            string format = Utilities.GetNumberFormatString(displayFormat);
            number = Math.Round(number, format[1] - '0', MidpointRounding.AwayFromZero);
            if (state.cachedText == text && state.lastNumber == number && state.cachedFormat == format && state.lastTextDisplay == textDisplay)
                return;

            state.textBuffer ??= new char[64];
            int length;
            while (!number.TryFormat(state.textBuffer.AsSpan(0, state.textBuffer.Length - 1), out length, format))
                Array.Resize(ref state.textBuffer, state.textBuffer.Length * 2);

            if (textDisplay == TextDisplay.Percentage)
                state.textBuffer[length++] = '%';

            TextProvider.SetText(text, state.textBuffer, 0, length);
            state.cachedText = text;
            state.cachedFormat = format;
            state.lastNumber = number;
            state.lastTextDisplay = textDisplay;
        }

        // Whether one of the first count segments uses the visual as its fill or as its text
        bool IsAssigned(Graphic visual, int count, bool fill)
        {
            for (int i = 0; i < count; i++)
            {
                Segment segment = segments[i];
                if (segment != null && ReferenceEquals(fill ? segment.fillShape : segment.valueText, visual))
                    return true;
            }

            return false;
        }

        void Refresh(bool instant, bool rewriteTexts)
        {
#if UNITY_EDITOR
            CancelEditorRefresh();
#endif
            StopAnimation();
            int count = SegmentCount;
            bool fillsChanged = states.Length != count;

            // Release removed or reassigned visuals without destroying authored UI
            for (int i = 0; i < states.Length; i++)
            {
                ProceduralShape fill = states[i].fill;
                Graphic text = states[i].text;
                if (fill != null && !IsAssigned(fill, count, true) && CanDrive(fill))
                    SetFill(fill, 0, 0f, 0f);
                if (text != null && !IsAssigned(text, count, false))
                    text.enabled = false;
            }

            if (states.Length != count)
            {
                Array.Resize(ref states, count);
                instant = true;
            }

            double previousTotal = totalValue;
            totalValue = TotalValue;
            bool drive = isActiveAndEnabled;
            bool sharesChanged = false;

            for (int i = 0; i < count; i++)
            {
                ref SegmentState state = ref states[i];
                if (!ReferenceEquals(state.segment, segments[i])) { instant = true; }
                state.segment = segments[i];
                ProceduralShape fill = state.segment?.fillShape;
                Graphic text = state.segment?.valueText;

                // A visual shared by several segments follows the first one
                ProceduralShape previousFill = state.fill;
                Graphic previousText = state.text;
                state.fill = CanDrive(fill) && !IsAssigned(fill, i, true) ? fill : null;
                state.text = text != null && !IsAssigned(text, i, false) ? text : null;
                fillsChanged |= !ReferenceEquals(state.fill, previousFill);

                // Another segment may have written to a text this one gets back
                if (rewriteTexts || !ReferenceEquals(state.text, previousText)) { state.cachedText = null; }
                state.startShare = state.currentShare;
                state.startValue = state.currentValue;
                state.targetValue = GetValue(i);
                state.targetShare = totalValue > 0d ? (float)(state.targetValue / totalValue) : 0f;
                sharesChanged |= state.startShare != state.targetShare || state.startValue != state.targetValue;
            }

            // Re-driving the same fills would record them in an open undo group again, and prefab instances
            // would get zero-valued anchor overrides
            if (fillsChanged || drive != drivingFills)
            {
                tracker.Clear();
                drivingFills = drive;

                for (int i = 0; drive && i < count; i++)
                {
                    if (states[i].fill != null)
                        tracker.Add(this, states[i].fill.rectTransform, DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
                }
            }

            // An empty distribution has no proportions to interpolate from or to
            if (!instant && enableSmoothing && sharesChanged && previousTotal > 0d && totalValue > 0d &&
                Application.IsPlaying(gameObject) && drive && Positive(smoothingDuration) > 0f)
            {
                updateCallback ??= UpdateDisplay;
                completeCallback ??= CompleteAnimation;
                valueAnimation = EvoTween.Create(0f, 1f, smoothingDuration)
                    .WithEase(Ease.Custom, smoothingCurve)
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(completeCallback)
                    .Bind(updateCallback);
            }
            else
            {
                UpdateDisplay(1f);
            }
        }

        // Value changes keep the texts whose number stays the same
        void ApplyValues(bool changed, bool sendCallback, bool instant)
        {
            if (!changed && !instant)
                return;

            Refresh(instant, false);

            if (changed && sendCallback)
                onValuesChanged?.Invoke();
        }

        #region Types
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public enum TextDisplay { Percentage, Value }

        [Serializable]
        public class Segment
        {
            public string label;
            [Min(0f)] public float value;
            [Tooltip("The bar controls this shape's anchors and offsets. All fills should share the same parent, without a Layout Group." +
                "\n\nAn Intersect operation with the track clips the fill to the track's rounded corners.")]
            public ProceduralShape fillShape;
            [TextComponent] public Graphic valueText;

            public Segment(string label = "", float value = 0f)
            {
                this.label = label;
                this.value = value;
            }
        }

        struct SegmentState
        {
            public Segment segment;
            public ProceduralShape fill;
            public Graphic text;
            public float start, end, drawStart, drawEnd;
            public float startShare, targetShare, currentShare;
            public float startValue, targetValue, currentValue;
            public char[] textBuffer;
            public Graphic cachedText;
            public string cachedFormat;
            public double lastNumber;
            public TextDisplay lastTextDisplay;
        }
        #endregion

        #region Public API
        public int SegmentCount => segments?.Count ?? 0;

        /// <summary>
        /// Sum of finite, positive values. Uses double precision to avoid float overflow.
        /// </summary>
        public double TotalValue
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < SegmentCount; i++)
                    total += GetValue(i);

                return total;
            }
        }

        public float GetValue(int index) => index >= 0 && index < SegmentCount ? Positive(segments[index]?.value ?? 0f) : 0f;

        public float GetNormalizedValue(int index)
        {
            double total = TotalValue;
            return total > 0d ? (float)(GetValue(index) / total) : 0f;
        }

        public void SetValue(int index, float value, bool sendCallback = true, bool instant = false)
        {
            if (index < 0 || index >= SegmentCount || segments[index] == null)
                return;

            value = Positive(value);
            bool changed = !segments[index].value.Equals(value);
            if (changed || instant)
                segments[index].value = value;

            ApplyValues(changed, sendCallback, instant);
        }

        /// <summary>
        /// Update all sides atomically, with a single animation and notification.
        /// </summary>
        public void SetValues(IReadOnlyList<float> values, bool sendCallback = true, bool instant = false)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count != SegmentCount)
                throw new ArgumentException("Provide one value per segment.", nameof(values));

            bool changed = false;
            for (int i = 0; i < SegmentCount; i++)
            {
                if (segments[i] == null)
                    continue;

                float value = Positive(values[i]);
                changed |= !segments[i].value.Equals(value);
                segments[i].value = value;
            }

            ApplyValues(changed, sendCallback, instant);
        }

        /// <summary>
        /// Apply direct segment, reference or setting edits without sending a value event.
        /// </summary>
        public void Refresh(bool instant = true) => Refresh(instant, true);
        #endregion

#if UNITY_EDITOR
        [NonSerialized] bool refreshPending;

        void OnValidate()
        {
            minimumTextPercentage = Mathf.Clamp(Positive(minimumTextPercentage), 0f, 100f);
            smoothingDuration = Mathf.Clamp(Positive(smoothingDuration), 0.05f, 4f);
            RequestEditorRefresh();
        }

        void RequestEditorRefresh()
        {
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        void CancelEditorRefresh()
        {
            if (!refreshPending)
                return;

            refreshPending = false;
            UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
        }

        void RefreshDelayed()
        {
            refreshPending = false;
            if (this == null || !isActiveAndEnabled || UnityEditor.EditorUtility.IsPersistent(this))
                return;

            Refresh();
        }
#endif
    }
}
