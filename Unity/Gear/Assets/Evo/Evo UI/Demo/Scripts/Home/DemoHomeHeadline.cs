using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Home headline: the word sits in a container that turns into the next kit component every few seconds.
    /// Clicking the container skips ahead; Explore opens the category of the component on screen.
    /// The change itself (line glide, word swap, parts, corners) runs as Evo Tweens; each component's own motion depends on
    /// how long it has been on screen, so that part is driven per frame.
    /// The container is built at its words' font size and scaled to the line's, so its parts and corners keep their proportions.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHomeHeadline : MonoBehaviour, ILayoutElement
    {
        public enum Kind { Shape, Button, Switch, Dropdown, Selector, Toggle, TextField }

        [System.Serializable]
        public class Item
        {
            public string word;
            public Kind kind;
            [Tooltip("Home page that Explore opens.")]
            public string page;
            [Tooltip("Tab ID of the variant to open inside the page.")]
            public string variant;
            [Tooltip("Room for the component's own parts before the word.")]
            [Min(0f)] public float lead;
            [Tooltip("Room for the component's own parts after the word.")]
            [Min(0f)] public float trail;
            [Min(0f)] public float padding = 32;
            [Min(0f)] public float radius = 26;
            [Tooltip("Parts only this component shows. They fade in and out with it.")]
            public CanvasGroup parts;
        }

        [SerializeField] private DemoHome home;
        [SerializeField] private Item[] items;
        [SerializeField, Min(0.1f)] private float interval = 3f;
        [Tooltip("After a click on the container, the cycle waits this long before moving on by itself.")]
        [SerializeField, Min(0f)] private float clickPause = 3f;

        [Header("Line")]
        [SerializeField] private TMP_Text lead;
        [SerializeField] private TMP_Text tail;
        [SerializeField] private RectTransform morph;
        [SerializeField, Min(0f)] private float glideDuration = 0.55f;
        [SerializeField] private Ease glideEase = Ease.OutExpo;

        [Header("Container")]
        [SerializeField] private Interactive container;
        [SerializeField] private ProceduralShape background;
        [Tooltip("Two labels that take turns, so the old word can leave while the new one arrives.")]
        [SerializeField] private TMP_Text[] words = new TMP_Text[2];
        [Tooltip("Gap between the container's edge and its background.")]
        [SerializeField, Min(0f)] private float inset = 6;

        [Header("Switch")]
        [SerializeField] private RectTransform switchHandle;
        [SerializeField] private Graphic[] switchOff;
        [SerializeField] private Graphic[] switchOn;

        [Header("Dropdown")]
        [SerializeField] private RectTransform dropdownArrow;

        [Header("Selector")]
        [SerializeField] private Graphic previousArrow;
        [SerializeField] private Graphic nextArrow;
        [SerializeField] private RectTransform[] indicators;
        [Tooltip("Accent layer of each indicator, shown on the selected one.")]
        [SerializeField] private Graphic[] indicatorHighlights;

        [Header("Toggle")]
        [SerializeField] private Graphic toggleOff;
        [SerializeField] private Graphic toggleOn;
        [SerializeField] private Image toggleCheck;

        [Header("Text Field")]
        [SerializeField] private Graphic caret;
        [SerializeField] private Graphic selection;

        // Shape: even, leaf, mirrored leaf, round, tight
        static readonly Vector4[] shapeCorners =
        {
            new(26, 26, 26, 26), new(58, 10, 58, 10), new(10, 58, 10, 58), new(58, 58, 58, 58), new(10, 10, 10, 10)
        };

        // Selector: right, right, left, left walks the indicators 0, 1, 2, 1, 0
        static readonly int[] selectorPath = { 1, 2, 1, 0 };
        const float IndicatorWidth = 20;
        const float SelectedIndicatorWidth = 36;
        const float IndicatorSpacing = 8;

        // A word leaves upward and the next one rises in after it; parts fade with their component
        const float WordRise = 16;
        const float WordOutDuration = 0.2f;
        const float WordInDuration = 0.45f;
        const float WordInDelay = 0.1f;
        const float PartsInDuration = 0.3f;
        const float PartsInDelay = 0.18f;
        const float PartsOutDuration = 0.18f;
        const float CornerDuration = 0.3f;
        const int GlideProperty = 31701;
        const int WordProperty = 31702;
        const int PartsProperty = 31703;
        const int CornerProperty = 31704;

        int current, slot, lastPress;
        float shownAt, nextAt, pausedUntil, glowOffAt = -1;
        float wordX, caretY, toggleFlipAt, toggleValue;
        float morphScale = 1, widestLine = -1;
        bool started, toggleState;
        Vector4 glide;
        Vector2 previousArrowRest, nextArrowRest;
        float[] indicatorAmounts;
        readonly TweenHandle[] wordTweens = new TweenHandle[2];
        TweenHandle[] partTweens;
        TweenHandle glideTween, cornerTween;
        PointerEventData glowPointer, ripplePointer;

        void Awake()
        {
            partTweens = new TweenHandle[items.Length];
            indicatorAmounts = new float[indicators.Length];
            previousArrowRest = previousArrow.rectTransform.anchoredPosition;
            nextArrowRest = nextArrow.rectTransform.anchoredPosition;
            morphScale = lead.fontSize / words[0].fontSize;
            widestLine = -1;
        }

        // The first frame shows the first component, once the container's effects are ready
        void OnEnable() => started = false;

        void OnDisable()
        {
            BorderGlow.Settings glow = container.borderGlow;
            glow.Clear();
            glow.enabled = false;
            glowOffAt = -1;
            ScaleMorph(1);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (!started)
            {
                started = true;
                current = 0;
                Show(0, false);
            }

            bool hovered = container.InteractionState == InteractionState.Highlighted
                || container.InteractionState == InteractionState.Pressed;

            if (now >= nextAt)
            {
                nextAt = now + interval;
                if (!hovered && now >= pausedUntil)
                    Show((current + 1) % items.Length, true);
            }

            if (glowOffAt >= 0 && now >= glowOffAt)
            {
                glowOffAt = -1;
                container.borderGlow.Clear();
                container.borderGlow.enabled = false;
            }

            float t = now - shownAt;
            float dt = Time.unscaledDeltaTime;

            switch (items[current].kind)
            {
                case Kind.Shape:
                    UpdateShape(t, hovered);
                    break;
                case Kind.Button:
                    UpdateButton(t);
                    break;
                case Kind.Switch:
                    UpdateSwitch(t);
                    break;
                case Kind.Dropdown:
                    UpdateDropdown(t);
                    break;
                case Kind.Selector:
                    UpdateSelector(t, dt);
                    break;
                case Kind.Toggle:
                    UpdateToggle(now, t, dt);
                    break;
                case Kind.TextField:
                    UpdateTextField(t);
                    break;
            }
        }

        /// <summary>
        /// Skips to the next component. Wired to the container's click.
        /// </summary>
        public void Next()
        {
            pausedUntil = Time.unscaledTime + clickPause;
            Show((current + 1) % items.Length, true);
        }

        /// <summary>
        /// Opens the page of the component on screen, on its variant. Wired to the Explore button.
        /// </summary>
        public void Explore()
        {
            if (home != null)
                home.OpenPage(items[current].page, items[current].variant);
        }

        void Show(int index, bool animate)
        {
            float now = Time.unscaledTime;
            Kind previous = items[current].kind;
            Item item = items[index];
            current = index;
            shownAt = now;
            nextAt = now + interval;
            ScaleMorph(1);
            lastPress = -1;

            // The other label takes the new word while this one leaves
            if (animate)
            {
                SwapWord(slot, false);
                slot = 1 - slot;
            }
            else
            {
                wordTweens[1 - slot].Kill();
                SetWord(words[1 - slot], 0, WordRise);
            }

            TMP_Text word = words[slot];
            word.maxVisibleCharacters = 99999;
            word.text = item.word;
            word.ForceMeshUpdate();
            float wordWidth = word.preferredWidth;
            wordX = inset + item.padding + item.lead;
            word.rectTransform.anchoredPosition = new Vector2(wordX, 0);
            word.rectTransform.sizeDelta = new Vector2(wordWidth, word.rectTransform.sizeDelta.y);

            if (animate)
            {
                SwapWord(slot, true);
            }
            else
            {
                wordTweens[slot].Kill();
                SetWord(word, 1, 0);
            }

            for (int i = 0; i < items.Length; i++)
            {
                CanvasGroup parts = items[i].parts;
                if (parts == null)
                    continue;

                if (i == index)
                    ShowParts(i, animate);
                else if (!animate)
                    HideParts(i, false);
                else if (parts.gameObject.activeSelf)
                    HideParts(i, true);
            }

            switch (item.kind)
            {
                case Kind.Selector:
                    for (int i = 0; i < indicatorAmounts.Length; i++)
                        indicatorAmounts[i] = i == 0 ? 1 : 0;

                    break;
                case Kind.Toggle:
                    toggleState = false;
                    toggleValue = 0;
                    toggleFlipAt = now - 1;
                    break;
                case Kind.TextField:
                    PlaceCaret(word, wordWidth);
                    break;
            }

            // The shape cycles its corners per frame; every other component eases to its own radius
            if (item.kind == Kind.Shape)
            {
                cornerTween.Kill();
            }
            else
            {
                cornerTween = EvoTween.Create(background.CornerRadius, Vector4.one * item.radius, animate ? CornerDuration : 0)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(background, static (shape, radius) => shape.CornerRadius = radius, CornerProperty);
            }

            // Border Glow belongs to the shape: it lights up with it and fades out after it
            BorderGlow.Settings glow = container.borderGlow;
            if (item.kind == Kind.Shape)
            {
                glowOffAt = -1;
                glow.enabled = true;
                glow.Show(background, OrbitPointer(0));
            }
            else if (previous == Kind.Shape && glow.enabled)
            {
                glow.Hide();
                glowOffAt = now + glow.fadeOutDuration;
            }

            // The line stays centered: the lead and tail glide as the container resizes
            float width = MorphWidth(item, wordWidth);
            float leadWidth = LeadWidth;
            float left = -(leadWidth + width * morphScale + tail.preferredWidth) * 0.5f;
            var target = new Vector4(left, left + leadWidth, width, left + leadWidth + width * morphScale);

            if (animate)
            {
                glideTween = EvoTween.Create(glide, target, glideDuration)
                    .WithEase(glideEase)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (headline, value) => headline.SetGlide(value), GlideProperty);
            }
            else
            {
                glideTween.Kill();
                SetGlide(target);
            }
        }

        // TMP's preferred width leaves out a trailing space, so the gap after the lead is measured
        float LeadWidth => lead.preferredWidth + lead.GetPreferredValues("a a").x - lead.GetPreferredValues("aa").x;

        // The container around a word, in its own units
        float MorphWidth(Item item, float wordWidth) => item.padding * 2 + item.lead + wordWidth + item.trail + inset * 2;

        // The container's scale over its build scale; the Button's press dips it
        void ScaleMorph(float value) => morph.localScale = new Vector3(morphScale * value, morphScale * value, 1);

        #region Transitions
        // Lead left edge, morph left edge, morph width in its own units, tail left edge
        void SetGlide(Vector4 value)
        {
            glide = value;
            morph.sizeDelta = new Vector2(value.z, morph.sizeDelta.y);
            SetLeft(lead.rectTransform, value.x);
            SetLeft(morph, value.y, morphScale);
            SetLeft(tail.rectTransform, value.w);
        }

        void SwapWord(int index, bool enter)
        {
            TMP_Text word = words[index];

            if (enter)
            {
                SetWord(word, 0, -WordRise);
                wordTweens[index] = EvoTween.Create(0f, 1f, WordInDuration)
                    .WithDelay(WordInDelay)
                    .WithEase(Ease.OutQuint)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(word, static (label, p) => SetWord(label, p, -WordRise * (1 - p)), WordProperty);
            }
            else
            {
                wordTweens[index] = EvoTween.Create(0f, 1f, WordOutDuration)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(word, static (label, p) => SetWord(label, 1 - p, WordRise * p), WordProperty);
            }
        }

        static void SetWord(TMP_Text word, float alpha, float y)
        {
            word.canvasRenderer.SetAlpha(alpha);
            RectTransform rect = word.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        // Each part pops from its own center as it fades in
        void ShowParts(int index, bool animate)
        {
            CanvasGroup parts = items[index].parts;
            parts.gameObject.SetActive(true);

            if (!animate)
            {
                partTweens[index].Kill();
                SetParts(parts, 1, 1);
                return;
            }

            SetParts(parts, 0, 0.8f);
            partTweens[index] = EvoTween.Create(0f, 1f, PartsInDuration)
                .WithDelay(PartsInDelay)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(parts, static (group, p) => SetParts(group, p, 0.8f + 0.2f * TweenEase.Evaluate(Ease.OutBack, p)), PartsProperty);
        }

        void HideParts(int index, bool animate)
        {
            CanvasGroup parts = items[index].parts;

            if (!animate)
            {
                partTweens[index].Kill();
                parts.gameObject.SetActive(false);
                return;
            }

            partTweens[index] = EvoTween.Create(parts.alpha, 0f, PartsOutDuration)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(parts, static (group, alpha) =>
                {
                    SetParts(group, alpha, 1);
                    if (alpha <= 0)
                        group.gameObject.SetActive(false);
                }, PartsProperty);
        }

        static void SetParts(CanvasGroup parts, float alpha, float scale)
        {
            parts.alpha = alpha;
            Transform root = parts.transform;
            for (int c = 0; c < root.childCount; c++)
                root.GetChild(c).localScale = new Vector3(scale, scale, 1);
        }

        static void SetX(RectTransform rect, float x) => rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);

        // Places a rect by its left edge, whatever its pivot
        static void SetLeft(RectTransform rect, float left, float scale = 1) => SetX(rect, left + rect.rect.width * rect.pivot.x * scale);

        /// <summary>
        /// A pointer for the container's effects at a point of the background, given in its local space.
        /// </summary>
        PointerEventData PointerAt(ref PointerEventData pointer, Vector2 local)
        {
            pointer ??= new PointerEventData(EventSystem.current);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, background.rectTransform.TransformPoint(local));
            return pointer;
        }
        #endregion

        #region Components
        // The corners morph through the set while Border Glow follows the pointer over the container, or circles the edge
        void UpdateShape(float t, bool hovered)
        {
            float phase = Mathf.Max(0, t - 0.3f) / 0.75f;
            int step = Mathf.FloorToInt(phase);
            Vector4 from = shapeCorners[step % shapeCorners.Length];
            Vector4 to = shapeCorners[(step + 1) % shapeCorners.Length];
            background.CornerRadius = Vector4.Lerp(from, to, Mathf.SmoothStep(0, 1, (phase - step) / 0.6f));

            if (!hovered)
                container.borderGlow.Move(OrbitPointer(t));
        }

        PointerEventData OrbitPointer(float t)
        {
            Rect rect = background.rectTransform.rect;
            float angle = t * 1.6f;
            Vector2 offset = new(-Mathf.Cos(angle) * (rect.width * 0.5f + 12), Mathf.Sin(angle) * (rect.height * 0.5f + 12));
            return PointerAt(ref glowPointer, rect.center + offset);
        }

        // A click every couple of seconds: the container dips and ripples from where it landed
        void UpdateButton(float t)
        {
            float elapsed = t - 0.6f;
            float press = 0;
            if (elapsed >= 0)
            {
                int count = Mathf.FloorToInt(elapsed / 1.9f);
                float since = elapsed - count * 1.9f;
                if (count != lastPress)
                {
                    lastPress = count;
                    Rect rect = background.rectTransform.rect;
                    Vector2 point = new(Mathf.Lerp(rect.xMin, rect.xMax, Random.Range(0.25f, 0.75f)),
                        Mathf.Lerp(rect.yMin, rect.yMax, Random.Range(0.35f, 0.65f)));
                    container.ripple.Create(background, PointerAt(ref ripplePointer, point));
                }

                if (since < 0.22f)
                    press = Mathf.Sin(since / 0.22f * Mathf.PI);
            }

            ScaleMorph(1 - 0.035f * press);
        }

        // On after half a second, off and on again every 1.8 seconds
        void UpdateSwitch(float t)
        {
            float phase = Mathf.Repeat(t, 1.8f);
            float value = t < 0.5f ? 0
                : phase >= 0.5f ? TweenEase.Evaluate(Ease.OutBack, (phase - 0.5f) / 0.35f)
                : 1 - TweenEase.Evaluate(Ease.OutBack, phase / 0.35f);

            SetX(switchHandle, 6 + 48 * value);
            float on = Mathf.Clamp01(value);

            for (int i = 0; i < switchOff.Length; i++)
                switchOff[i].canvasRenderer.SetAlpha(1 - on);

            for (int i = 0; i < switchOn.Length; i++)
                switchOn[i].canvasRenderer.SetAlpha(on);
        }

        // Opens and closes every second
        void UpdateDropdown(float t)
        {
            float flips = Mathf.Floor(t);
            float p = TweenEase.Evaluate(Ease.OutExpo, (t - flips) / 0.35f);
            float angle = t < 1 ? 0 : flips % 2 == 1 ? p : 1 - p;
            dropdownArrow.localEulerAngles = new Vector3(0, 0, -180 * angle);
        }

        // The Selector's arrow press: that side nudges outward and lights up, and the indicators follow it
        void UpdateSelector(float t, float dt)
        {
            float elapsed = t - 0.45f;
            int beat = elapsed < 0 ? -1 : Mathf.FloorToInt(elapsed / 0.8f);
            int selected = beat < 0 ? 0 : selectorPath[beat % selectorPath.Length];
            int side = beat < 0 ? 0 : beat % selectorPath.Length < 2 ? 1 : -1;
            float press = beat < 0 ? 0 : Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - beat * 0.8f) / 0.4f));
            Nudge(previousArrow, previousArrowRest, side < 0 ? press : 0, -1);
            Nudge(nextArrow, nextArrowRest, side > 0 ? press : 0, 1);

            float total = IndicatorSpacing * (indicators.Length - 1);

            for (int i = 0; i < indicators.Length; i++)
            {
                indicatorAmounts[i] = Mathf.Lerp(indicatorAmounts[i], i == selected ? 1 : 0, 1 - Mathf.Exp(-dt * 14));
                total += Mathf.Lerp(IndicatorWidth, SelectedIndicatorWidth, indicatorAmounts[i]);
            }

            float x = -total * 0.5f;

            for (int i = 0; i < indicators.Length; i++)
            {
                float width = Mathf.Lerp(IndicatorWidth, SelectedIndicatorWidth, indicatorAmounts[i]);
                indicators[i].anchoredPosition = new Vector2(x, 0);
                indicators[i].sizeDelta = new Vector2(width, indicators[i].sizeDelta.y);
                indicatorHighlights[i].canvasRenderer.SetAlpha(indicatorAmounts[i]);
                x += width + IndicatorSpacing;
            }
        }

        static void Nudge(Graphic arrow, Vector2 rest, float press, float direction)
        {
            arrow.rectTransform.anchoredPosition = rest + new Vector2(direction * 10 * press, 0);
            arrow.canvasRenderer.SetAlpha(0.65f + 0.35f * press);
        }

        // Colored like the Switch; the check fills in and out like the kit Toggle's
        void UpdateToggle(float now, float t, float dt)
        {
            bool on = t > 0.5f && Mathf.Repeat(t - 0.5f, 1.8f) < 1.35f;
            if (on != toggleState)
            {
                toggleState = on;
                toggleFlipAt = now;
            }

            toggleValue = Mathf.Lerp(toggleValue, on ? 1 : 0, 1 - Mathf.Exp(-dt * 14));
            toggleOff.canvasRenderer.SetAlpha(1 - toggleValue);
            toggleOn.canvasRenderer.SetAlpha(toggleValue);
            float fill = TweenEase.Evaluate(Ease.OutCubic, (now - toggleFlipAt) / 0.25f);
            toggleCheck.fillAmount = on ? fill : 1 - fill;
        }

        // Types the word behind a caret, waits, then selects it all like Select on Focus
        void UpdateTextField(float t)
        {
            TMP_Text word = words[slot];
            int length = word.textInfo.characterCount;
            float elapsed = t < 0.15f ? -1 : Mathf.Repeat(t - 0.15f, 3.2f);
            bool selected = elapsed >= 2.1f;
            int visible = elapsed < 0 ? 0 : Mathf.Min(length, Mathf.FloorToInt(elapsed / 0.07f) + 1);
            bool typing = elapsed >= 0 && elapsed < length * 0.07f + 0.35f;

            word.maxVisibleCharacters = visible;
            float x = wordX + (visible > 0 ? word.textInfo.characterInfo[visible - 1].xAdvance : 0) + 4;
            caret.rectTransform.anchoredPosition = new Vector2(x, caretY);
            caret.canvasRenderer.SetAlpha(selected ? 0 : typing || Mathf.Repeat(t, 1) < 0.55f ? 1 : 0);
            selection.canvasRenderer.SetAlpha(selected ? 1 : 0);
        }

        /// <summary>
        /// The caret spans the word's ascenders down to just below its baseline, like a text field's caret.
        /// </summary>
        void PlaceCaret(TMP_Text word, float wordWidth)
        {
            float baseline = word.textInfo.lineInfo[0].baseline;
            float capLine = word.font.faceInfo.capLine * word.fontSize / word.font.faceInfo.pointSize;
            float top = baseline + capLine * 1.12f;
            float bottom = baseline - capLine * 0.2f;
            float caretHeight = top - bottom;
            caretY = (top + bottom) * 0.5f;

            RectTransform caretRect = caret.rectTransform;
            caretRect.sizeDelta = new Vector2(caretRect.sizeDelta.x, caretHeight);
            RectTransform selectionRect = selection.rectTransform;
            selectionRect.anchoredPosition = new Vector2(wordX - 8, caretY);
            selectionRect.sizeDelta = new Vector2(wordWidth + 16, caretHeight + 8);
        }
        #endregion

        #region Layout
        /// <summary>
        /// The line with its widest component. Home fits the page to it, so the fit holds whichever word is up.
        /// </summary>
        public float preferredWidth
        {
            get
            {
                if (widestLine < 0)
                {
                    float widest = 0;

                    for (int i = 0; i < items.Length; i++)
                        widest = Mathf.Max(widest, MorphWidth(items[i], words[0].GetPreferredValues(items[i].word).x));

                    widestLine = LeadWidth + widest * morphScale + tail.preferredWidth;
                }

                return widestLine;
            }
        }

        public float minWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => -1;
        public float preferredHeight => -1;
        public float flexibleHeight => -1;
        public int layoutPriority => 0;
#if UNITY_6000_6_OR_NEWER
        public float maxWidth => -1;
        public float maxHeight => -1;
#endif
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        #endregion
    }
}
