using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Evo.Tween;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Home page: a deck of sample screens with one face up at a time. The top card can be dragged and thrown to bring up the
    /// next one, opens in place on click, and the deck moves on by itself. Cards ride per-card springs, so a throw keeps the
    /// release velocity. Fixed animations live in Tween Animators (intro, card hover, info column, tags); fades and the open
    /// and close moves run through the Evo Tween API because their ends are only known at runtime.
    /// Screens whose face is hidden are turned off. A swap hands the new screen's highlight to the accent and the background.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHome : MonoBehaviour
    {
        [Serializable]
        public struct DeckPose
        {
            public Vector2 position;
            public float angle;
            [Min(0f)] public float scale;

            public DeckPose(Vector2 position, float angle, float scale)
            {
                this.position = position;
                this.angle = angle;
                this.scale = scale;
            }

            public readonly Vector4 Value => new(position.x, position.y, angle, scale);
        }

        [Header("Deck")]
        [SerializeField] private CanvasGroup introGroup;
        [SerializeField] private CanvasGroup deckGroup;
        [SerializeField] private CanvasGroup cardsGroup;
        [SerializeField] private List<DemoHomeCard> cards = new();
        [SerializeField] private DemoHomeIndicator indicator;
        [Tooltip("Card on top when the scene opens.")]
        [SerializeField, Min(0)] private int startCard = 1;
        [Tooltip("Seconds before the deck moves on by itself. Zero turns it off.")]
        [SerializeField, Min(0f)] private float autoAdvance = 6;
        [SerializeField] private DeckPose secondPose = new(new Vector2(-27, -8), 3, 0.965f);
        [SerializeField] private DeckPose thirdPose = new(new Vector2(31, -14), -2.4f, 0.93f);

        [Header("Drag")]
        [Tooltip("Share of the card width a release has to travel to throw the card.")]
        [SerializeField, Range(0f, 1f)] private float throwDistance = 0.24f;
        [Tooltip("Release speed, in canvas units per second, that throws the card from anywhere.")]
        [SerializeField, Min(0f)] private float flickSpeed = 700;
        [SerializeField, Range(0f, 1f)] private float verticalFollow = 0.35f;
        [Tooltip("Degrees the card turns per unit dragged sideways.")]
        [SerializeField, Range(0f, 0.1f)] private float dragTilt = 0.035f;
        [SerializeField, Min(1f)] private float settleStiffness = 260;
        [SerializeField, Range(0.1f, 2f)] private float settleDamping = 0.8f;
        [SerializeField, Min(1f)] private float throwStiffness = 130;
        [SerializeField, Range(0.1f, 2f)] private float throwDamping = 0.9f;

        [Header("Reveal")]
        [Tooltip("Home's intro. The deck rises in as it starts.")]
        [SerializeField] private TweenAnimator intro;
        [SerializeField, Min(0f)] private float revealRise = 170;
        [SerializeField, Min(0f)] private float revealStagger = 0.07f;
        [SerializeField, Min(0f)] private float revealFade = 0.35f;
        [SerializeField, Min(0f)] private float indicatorDelay = 0.3f;

        [Header("Fit")]
        [Tooltip("Scale the page shows at when Home has room for it. With less room, it scales down as one to stay in view.")]
        [SerializeField, Min(0.01f)] private float fitMaxScale = 1f;
        [SerializeField, Min(0f)] private Vector2 fitMargin = new(60, 20);

        [Header("Focus")]
        [SerializeField] private CanvasGroup focusGroup;
        [SerializeField] private RectTransform focusLayer;
        [SerializeField] private RectTransform infoColumn;
        [SerializeField] private TweenAnimator infoAnimator;
        [SerializeField] private TMP_Text focusTitle;
        [SerializeField] private TMP_Text focusDescription;
        [Tooltip("Shows the focused screen's component tags. The state it is saved in is how the page opens.")]
        [SerializeField] private Toggle tagToggle;
        [SerializeField, Min(0.01f)] private float focusMaxScale = 1f;
        [SerializeField, Min(0f)] private Vector2 focusMargin = new(120, 70);
        [SerializeField, Min(0f)] private float infoGap = 80f;

        [Header("Focus Motion")]
        [SerializeField, Min(0f)] private float openDuration = 0.5f;
        [SerializeField, Min(0f)] private float closeDuration = 0.4f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;
        [SerializeField, Min(0f)] private float fadeInDelay = 0.1f;

        [Header("Theme")]
        [SerializeField] private DemoHomeAccent accent;
        [SerializeField] private DemoHomeBackground background;
        [Tooltip("Theme Selector preset the scene opens with: 0 is light, 1 is dark.")]
        [SerializeField, Range(0, 1)] private int startTheme = 1;

        [Header("Pages")]
        [SerializeField] private TabManager pages;
        [Tooltip("Opens tag and headline links on their variant inside the page, the way its results do.")]
        [SerializeField] private DemoSearch search;

        enum Mode { Stack, Out, Tuck, In }

        // Position x/y, angle and scale in one vector, so the springs treat all four alike
        struct Card
        {
            public Vector4 pose, velocity, away, applied;
            public Mode mode;
            public float face, opacity, wait;
        }

        const int ScaleProperty = 31002;
        const int AngleProperty = 31003;
        const int Samples = 6;
        const float WarmUp = 0.25f;
        // Squared: 0.0001 px, degrees or scale away, moving 0.001 of them per second. Springs stall a few float steps short of
        // their target, still creeping, so this is where a card counts as landed
        const float RestDistance = 1e-8f;
        const float RestSpeed = 1e-6f;

        Card[] states;
        int[] order, zOrder;
        readonly Vector3[] samples = new Vector3[Samples];
        int sampleCount, current, theme, homeTab = -1, pendingSteps, pendingDirection, dragEndFrame = -1, warmFrame = -1;
        float autoTime, pendingTimer, revealTime, dragProgress, pageExtent, fitScale;
        bool revealing, revealPending, onHome = true, hovered, dragging, closing, resetCards, fitReady;
        Vector2 dragOrigin, fitDelta;
        Vector4 dragStart;
        RectTransform deck;
        StylerPreset themePreset;
        DemoHomeCard focused;
        TweenHandle moveTween, scaleTween, angleTween, introFade, deckFade, cardsFade;
        Action finishClose;

        public bool IsFocused => focused != null;
        public int Current => current;
        public bool IndicatorHovered { get; set; }
        float CardWidth => cards.Count > 0 ? cards[0].Rect.rect.width : 1;
        bool TagsShown => tagToggle == null || tagToggle.IsOn;
        float CardDuration => accent != null ? accent.CardDuration : 0.4f;
        float PageDuration => accent != null ? accent.PageDuration : 0.1f;
        bool AdvanceSoon => autoAdvance > 0 && (1 - autoTime) * autoAdvance < WarmUp;

        void Awake()
        {
            int count = cards.Count;
            states = new Card[count];
            order = new int[count];
            zOrder = new int[count];
            current = Mathf.Clamp(startCard, 0, Mathf.Max(0, count - 1));
            theme = startTheme;
            deck = (RectTransform)cardsGroup.transform;
            finishClose = FinishClose;

            for (int d = 0; d < count; d++)
            {
                order[d] = (current + d) % count;
                zOrder[d] = -1;
                states[order[d]].applied = new Vector4(float.NaN, 0, 0, 0);
                cards[d].Setup(this);
            }

            if (pages != null)
            {
                for (int i = 0; i < pages.tabs.Count; i++)
                {
                    if (pages.tabs[i].tabObject == transform)
                        homeTab = i;
                }

                pages.onTabChanged.AddListener(OnTabChanged);
            }

            if (tagToggle != null)
            {
                tagToggle.onValueChanged.AddListener(SetTagsShown);
                if (tagToggle.IsOn)
                    tagToggle.SetState(InteractionState.Selected, true);
            }

            if (indicator != null)
                indicator.Setup(this, cards);
        }

        void OnDestroy()
        {
            if (pages != null)
                pages.onTabChanged.RemoveListener(OnTabChanged);
            if (tagToggle != null)
                tagToggle.onValueChanged.RemoveListener(SetTagsShown);
        }

        void OnEnable()
        {
            // Home's intro plays whenever the page opens; the deck rises in with it
            revealPending = intro != null && intro.playOnEnable;
            if (revealPending)
            {
                cardsGroup.alpha = 0;
                if (indicator != null)
                    indicator.Hide();
            }

            autoTime = 0;
            pendingSteps = 0;
            resetCards = true;

            // Home's size may have changed while another page was up
            if (fitReady)
                Fit();
        }

        void OnDisable()
        {
            introFade.Kill();
            deckFade.Kill();
            cardsFade.Kill();
            CancelDrag();

            if (focused != null)
                FinishClose();

            // Leaving Home mid-focus: the info column's Hide state never ran, so clear it here
            hovered = false;
            IndicatorHovered = false;
            focusGroup.alpha = 0;
            focusGroup.blocksRaycasts = false;
            introGroup.alpha = 1;
            deckGroup.alpha = 1;
            cardsGroup.alpha = 1;
        }

        void Start()
        {
            // Every part is awake here, so the page can be measured; from now on Home refits whenever its size changes
            pageExtent = PageExtent();
            fitReady = true;
            Fit();

            themePreset = Styler.PersistentPreset != null ? Styler.PersistentPreset : Styler.GetDefaultPreset(false);

            for (int i = 0; i < cards.Count; i++)
                cards[i].Screen.SetScale(cards[i].DeckScale);

            if (background != null && cards.Count > 0)
            {
                background.SetTheme(theme, 0);
                background.SetColor(cards[current].Highlight(theme), 0);
                background.SetTarget(deck);
            }

            // The accent takes the first screen's highlight before anything shows, so the intro runs without a color change
            ApplyAccent(0);
            Settle();

            if (revealPending)
                WarmScreens();
        }

        void Update()
        {
            // The warm-up frame draws every screen; the deck takes over from the next one
            if (warmFrame >= 0)
            {
                if (warmFrame == Time.frameCount)
                    return;

                warmFrame = -1;
                cardsGroup.alpha = 0;
                Settle();
            }

            float delta = Time.unscaledDeltaTime;

            // A card hidden while hovered keeps its lifted look; settle every card once the page is back
            if (resetCards)
            {
                resetCards = false;

                for (int i = 0; i < cards.Count; i++)
                    cards[i].ResetHover();
            }

            if (revealPending && intro.IsPlaying)
                Reveal();

            if (revealing)
            {
                revealTime += delta;
                if (revealTime >= indicatorDelay)
                    revealing = false;
            }

            if (focused == null && !dragging)
                UpdateAdvance(delta);

            UpdateCards(delta);

            if (focused != null && !closing && BackPressed())
                CloseFocus();
        }

        static bool BackPressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;

            return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
        }

        #region Deck
        Vector4 Pose(int depth) => depth <= 0 ? new Vector4(0, 0, 0, 1) : depth == 1 ? secondPose.Value : thirdPose.Value;

        void UpdateAdvance(float delta)
        {
            // A dot jump steps through the cards in between, so each one is seen leaving
            if (pendingSteps > 0)
            {
                pendingTimer -= delta;
                if (pendingTimer <= 0)
                {
                    pendingSteps--;
                    pendingTimer = pendingDirection > 0 ? 0.12f : 0.16f;
                    if (pendingDirection > 0)
                        Throw(1, Vector4.zero, false);
                    else
                        Return();
                }

                return;
            }

            if (autoAdvance <= 0 || hovered || IndicatorHovered || revealing || revealPending || !onHome)
                return;

            autoTime += delta / autoAdvance;
            if (autoTime >= 1)
                Throw(1, Vector4.zero, false);
        }

        void UpdateCards(float delta)
        {
            int count = cards.Count;
            if (count == 0)
                return;

            float width = CardWidth;
            ref Card top = ref states[order[0]];
            float progress = dragging ? dragProgress : 0;
            bool topAway = top.mode != Mode.Stack || new Vector2(top.pose.x, top.pose.y).magnitude > width * 0.22f;

            for (int d = 0; d < count; d++)
            {
                int index = order[d];
                ref Card card = ref states[index];

                if (card.wait > 0)
                {
                    card.wait -= delta;
                    Apply(index, d);
                    continue;
                }

                if (!(dragging && d == 0))
                {
                    Vector4 target;
                    float stiffness = settleStiffness, damping = settleDamping;

                    switch (card.mode)
                    {
                        case Mode.Out:
                            target = card.away;
                            stiffness = throwStiffness;
                            damping = throwDamping;
                            if (Mathf.Abs(card.pose.x) >= width * 1.12f)
                                card.mode = Mode.Tuck;
                            break;
                        case Mode.In:
                            target = card.away;
                            stiffness = settleStiffness * 0.92f;
                            damping = throwDamping;
                            if (card.pose.x <= -width * 1.04f)
                                card.mode = Mode.Stack;
                            break;
                        default:
                            target = Pose(d);
                            break;
                    }

                    if (card.mode == Mode.Tuck)
                    {
                        // Under the deck again, the card slides into the last place
                        target = Pose(2);
                        stiffness = throwStiffness * 0.92f;
                        damping = Mathf.Min(throwDamping + 0.05f, 2f);
                        if (new Vector2(card.pose.x - target.x, card.pose.y - target.y).sqrMagnitude < 4 && Mathf.Abs(card.velocity.x) < 20)
                            card.mode = Mode.Stack;
                    }
                    else if (card.mode == Mode.Stack && progress > 0)
                    {
                        // The cards underneath step up as the top one is pulled away
                        if (d == 1)
                            target = Vector4.Lerp(Pose(1), Pose(0), progress * 0.6f);
                        else if (d == 2)
                            target = Vector4.Lerp(Pose(2), Pose(1), progress * 0.5f);
                    }

                    // A landed card skips its spring, which would only grind float residue there. It lands exactly on its pose;
                    // a new pose, a throw or a drag sets it moving again
                    if (!card.pose.Equals(target) || !card.velocity.Equals(Vector4.zero))
                    {
                        DemoSpring.Step(ref card.pose, ref card.velocity, target, stiffness, damping, delta);
                        if ((card.pose - target).sqrMagnitude < RestDistance && card.velocity.sqrMagnitude < RestSpeed)
                        {
                            card.pose = target;
                            card.velocity = Vector4.zero;
                        }
                    }
                }

                // Only the top card shows its face; the next one turns over while the top is pulled away
                float faceTarget = 0;
                if (card.mode == Mode.Out)
                    faceTarget = 1;
                else if (card.mode == Mode.Stack)
                    faceTarget = d == 0 ? 1 : d == 1 && !revealing ? Mathf.Max(Smooth(0, 0.22f, progress), topAway ? 1 : 0) : 0;

                card.face = d == 1 && dragging
                    ? Mathf.Max(Approach(card.face, faceTarget, delta, 16), Smooth(0, 0.22f, progress))
                    : Approach(card.face, faceTarget, delta, card.mode == Mode.Tuck ? 7 : 14);

                float opacityTarget = card.mode != Mode.Stack || d <= 2 ? 1 : 0;
                card.opacity = opacityTarget > card.opacity ? opacityTarget : Approach(card.opacity, opacityTarget, delta, 18);
                Apply(index, d);
            }

            SortCards();

            if (indicator != null)
                indicator.Tick(delta, autoTime);
        }

        void Apply(int index, int depth)
        {
            ref Card card = ref states[index];
            DemoHomeCard view = cards[index];
            RectTransform rect = view.Rect;

            // Settled cards keep their transforms untouched, so the deck's canvas only rebuilds while something moves
            Vector4 pose = card.pose;
            if (pose.x != card.applied.x || pose.y != card.applied.y)
                rect.anchoredPosition = new Vector2(pose.x, pose.y);
            if (pose.z != card.applied.z)
                rect.localRotation = Quaternion.Euler(0, 0, pose.z);
            if (pose.w != card.applied.w)
                rect.localScale = new Vector3(pose.w, pose.w, 1);

            card.applied = pose;
            bool interactive = depth == 0 && card.mode == Mode.Stack && !revealing && focused == null;

            // A hidden face turns its screen off, which stops the screen's scripts too. The top one stays on to turn up at once,
            // and the next one comes on just before the deck moves on by itself, so the throw's frame doesn't pay for it
            bool live = depth == 0 || (card.face > 0.01f && card.opacity > 0.01f) || (depth == 1 && AdvanceSoon);
            view.SetState(card.opacity, card.face, interactive, live);
        }

        // Thrown cards fly over the deck, returning ones pass under it, the rest stack by depth
        void SortCards()
        {
            int count = order.Length;
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                int best = -1, bestKey = int.MaxValue;

                for (int d = 0; d < count; d++)
                {
                    int index = order[d];
                    int key = ZKey(index, d) * 16 + index;
                    if (key < bestKey && Array.IndexOf(zOrder, index, 0, i) < 0)
                    {
                        bestKey = key;
                        best = index;
                    }
                }

                if (zOrder[i] != best)
                {
                    zOrder[i] = best;
                    changed = true;
                }
            }

            if (!changed)
                return;

            for (int i = 0; i < count; i++)
                cards[zOrder[i]].Rect.SetSiblingIndex(i);
        }

        int ZKey(int index, int depth)
        {
            Mode mode = states[index].mode;
            return mode == Mode.Out ? 80 : mode == Mode.In ? 5 : 50 - depth * 2;
        }

        static float Approach(float value, float target, float delta, float rate) => value + (target - value) * (1 - Mathf.Exp(-delta * rate));

        static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3 - 2 * t);
        }

        /// <summary>
        /// Sends the top card off to one side; the one under it becomes the current screen.
        /// </summary>
        void Throw(int side, Vector4 velocity, bool released)
        {
            int count = order.Length;
            if (count < 2 || focused != null)
                return;

            int top = order[0];
            for (int d = 0; d < count - 1; d++)
                order[d] = order[d + 1];

            order[count - 1] = top;

            ref Card card = ref states[top];
            float width = CardWidth;
            card.mode = Mode.Out;
            card.away = new Vector4(side * width * 1.3f, Mathf.Clamp(card.pose.y + (released ? velocity.y * 0.12f : 60), -240, 280), -side * 15, 1);
            card.velocity = released ? velocity : new Vector4(side * 2300, 240, -side * 34, 0);
            autoTime = 0;
            SetCurrent(order[0], side);
        }

        /// <summary>
        /// Brings the bottom card back from the left, under the deck, and onto the top.
        /// </summary>
        void Return()
        {
            int count = order.Length;
            if (count < 2 || focused != null)
                return;

            int last = order[count - 1];
            for (int d = count - 1; d > 0; d--)
                order[d] = order[d - 1];

            order[0] = last;

            ref Card card = ref states[last];
            card.mode = Mode.In;
            card.face = 0;
            card.away = new Vector4(-CardWidth * 1.16f, 40, 12, 1);
            card.velocity = new Vector4(-1500, 0, 20, 0);
            autoTime = 0;
            SetCurrent(last, 1);
        }

        /// <summary>
        /// Steps the deck to a card: forward for one or two cards, back for the rest. The indicator's dots call it.
        /// </summary>
        public void Go(int index)
        {
            int count = cards.Count;
            if (focused != null || dragging || count < 2)
                return;

            int steps = ((index - current) % count + count) % count;
            if (steps == 0)
                return;

            pendingDirection = steps <= 2 ? 1 : -1;
            pendingSteps = pendingDirection > 0 ? steps : count - steps;
            pendingTimer = 0;
        }

        void SetCurrent(int index, int side)
        {
            current = index;

            if (indicator != null)
                indicator.Changed(side);

            if (background != null)
            {
                background.Shift();
                background.SetColor(cards[index].Highlight(theme), CardDuration);
            }

            ApplyAccent(CardDuration);
        }

        // Puts every card in its place at once, e.g. before the first frame
        void Settle()
        {
            for (int d = 0; d < order.Length; d++)
            {
                ref Card card = ref states[order[d]];
                card.mode = Mode.Stack;
                card.pose = Pose(d);
                card.velocity = Vector4.zero;
                card.wait = 0;
                card.face = d == 0 ? 1 : 0;
                card.opacity = d <= 2 ? 1 : 0;
                Apply(order[d], d);
            }

            SortCards();
        }

        // While the deck still waits for Home's intro under the scene intro, every screen comes on and draws for one frame.
        // A screen's first turn then finds its shaders compiled and its scripts awake, so the swap doesn't hitch
        void WarmScreens()
        {
            warmFrame = Time.frameCount;
            cardsGroup.alpha = 1;

            for (int i = 0; i < cards.Count; i++)
                cards[i].SetState(1, 1, false, true);
        }

        void Reveal()
        {
            revealPending = false;
            revealing = true;
            revealTime = 0;

            // Every card rises from below into its place, the deepest first; the top one turns its face up on the way
            for (int d = 0; d < order.Length; d++)
            {
                ref Card card = ref states[order[d]];
                card.mode = Mode.Stack;
                card.pose = Pose(d) + new Vector4(0, -revealRise, 0, 0);
                card.velocity = Vector4.zero;
                card.wait = (2 - Mathf.Min(d, 2)) * revealStagger;
                card.opacity = d <= 2 ? 1 : 0;
                card.face = 0;
                Apply(order[d], d);
            }

            cardsGroup.alpha = 0;
            cardsFade = DemoUtils.Fade(cardsGroup, 1, revealFade);

            if (indicator != null)
                indicator.Reveal(indicatorDelay);
        }

        public void SetHovered(DemoHomeCard card, bool value) => hovered = value && cards.Count > 0 && card == cards[order[0]];
        #endregion

        #region Fit
        /// <summary>
        /// Scales the page down as one when Home is too small for it, the way an open screen fits beside the info column.
        /// Each layer takes the inverse size, so it still covers Home and the open screen keeps fitting inside it.
        /// </summary>
        void Fit()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            float halfWidth = Mathf.Max(HalfWidth(introGroup.transform), HalfWidth(deckGroup.transform));
            float scale = Mathf.Max(0.01f, DemoUtils.Min(fitMaxScale,
                (size.x * 0.5f - fitMargin.x) / halfWidth,
                (size.y * 0.5f - fitMargin.y) / pageExtent));

            Vector2 delta = size * (1 / scale - 1);
            if (scale == fitScale && delta == fitDelta)
                return;

            fitScale = scale;
            fitDelta = delta;
            ScaleLayer(introGroup.transform, scale, delta);
            ScaleLayer(deckGroup.transform, scale, delta);
            ScaleLayer(focusGroup.transform, scale, delta);
            ScaleLayer(focusLayer, scale, delta);
        }

        static void ScaleLayer(Transform layer, float scale, Vector2 delta)
        {
            var rect = (RectTransform)layer;
            rect.sizeDelta = delta;
            rect.localScale = new Vector3(scale, scale, 1);
        }

        // Furthest the page reaches up or down from Home's center at scale 1. Home's intro samples its start as soon as it
        // plays, rows slid down, so its targets are put back for the read and sampled again where the intro was
        float PageExtent()
        {
            bool sampled = intro != null && intro.HasCapturedValues;
            float time = sampled ? intro.CurrentTime : 0;
            if (sampled)
                intro.Restore();

            float extent = Mathf.Max(HalfHeight(introGroup.transform), HalfHeight(deckGroup.transform));
            if (sampled)
                intro.Sample(time);

            return extent;
        }

        // Furthest a layer's content reaches sideways from its center. A row that spans the layer reaches as far as it shows
        static float HalfWidth(Transform layer)
        {
            float extent = 0;

            for (int i = 0; i < layer.childCount; i++)
            {
                var child = (RectTransform)layer.GetChild(i);
                if (!child.gameObject.activeSelf)
                    continue;

                float width = child.anchorMin.x != child.anchorMax.x ? LayoutUtility.GetPreferredWidth(child) : child.rect.width;
                extent = Mathf.Max(extent, Mathf.Abs(child.localPosition.x) + width * child.localScale.x * 0.5f);
            }

            return extent;
        }

        // Furthest a layer's content reaches up or down from its center
        static float HalfHeight(Transform layer)
        {
            float extent = 0;

            for (int i = 0; i < layer.childCount; i++)
            {
                var child = (RectTransform)layer.GetChild(i);
                if (!child.gameObject.activeSelf)
                    continue;

                Rect rect = child.rect;
                float y = child.localPosition.y, scale = child.localScale.y;
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(y + rect.yMax * scale), Mathf.Abs(y + rect.yMin * scale)));
            }

            return extent;
        }
        #endregion

        #region Drag
        public void BeginDrag(DemoHomeCard card, PointerEventData eventData)
        {
            if (focused != null || revealing || card != cards[order[0]] || states[order[0]].mode != Mode.Stack)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(deck, eventData.pressPosition, eventData.pressEventCamera, out dragOrigin))
                return;

            ref Card state = ref states[order[0]];
            dragging = true;
            dragProgress = 0;
            dragStart = state.pose;
            state.velocity = Vector4.zero;
            pendingSteps = 0;
            sampleCount = 0;
            Drag(eventData);
        }

        public void Drag(PointerEventData eventData)
        {
            if (!dragging || !RectTransformUtility.ScreenPointToLocalPointInRectangle(deck, eventData.position, eventData.pressEventCamera, out Vector2 point))
                return;

            if (sampleCount < Samples)
                sampleCount++;
            else
                Array.Copy(samples, 1, samples, 0, Samples - 1);

            samples[sampleCount - 1] = new Vector3(Time.unscaledTime, point.x, point.y);

            Vector2 offset = point - dragOrigin;
            ref Card state = ref states[order[0]];
            state.pose = new Vector4(dragStart.x + offset.x, dragStart.y + offset.y * verticalFollow, dragStart.z - offset.x * dragTilt, 1);
            dragProgress = Mathf.Clamp01(Mathf.Abs(state.pose.x) / (CardWidth * 0.55f));
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (!dragging)
                return;

            Drag(eventData);
            dragging = false;
            dragEndFrame = Time.frameCount;
            ref Card state = ref states[order[0]];

            // Release velocity from the last tenth of a second of samples
            float now = Time.unscaledTime;
            Vector2 velocity = Vector2.zero;
            int first = -1;

            for (int i = 0; i < sampleCount; i++)
            {
                if (now - samples[i].x < 0.1f)
                {
                    first = i;
                    break;
                }
            }

            if (first >= 0 && sampleCount - 1 > first)
            {
                Vector3 a = samples[first], b = samples[sampleCount - 1];
                float span = Mathf.Max(0.016f, b.x - a.x);
                velocity = new Vector2(Mathf.Clamp((b.y - a.y) / span, -4200, 4200), Mathf.Clamp((b.z - a.z) / span, -3000, 3000));
            }

            var release = new Vector4(velocity.x, velocity.y * verticalFollow, -velocity.x * dragTilt, 0);
            bool flick = Mathf.Abs(velocity.x) > flickSpeed && (Mathf.Sign(velocity.x) == Mathf.Sign(state.pose.x) || Mathf.Abs(state.pose.x) < 40);

            if (flick || Mathf.Abs(state.pose.x) > CardWidth * throwDistance)
                Throw((flick ? velocity.x : state.pose.x) < 0 ? -1 : 1, release, true);
            else
                state.velocity = release;
        }

        void CancelDrag()
        {
            if (!dragging)
                return;

            dragging = false;
            states[order[0]].velocity = Vector4.zero;
        }
        #endregion

        #region Focus
        public void Open(DemoHomeCard card)
        {
            // A drag's release also reaches the card's click in the same frame
            if (dragging || dragEndFrame == Time.frameCount)
                return;

            if (focused != null || card == null || card.Screen == null || card != cards[order[0]] || revealing)
                return;

            focused = card;
            closing = false;
            hovered = false;
            pendingSteps = 0;

            DemoScreen screen = card.Screen;
            focusTitle.text = screen.Title;
            focusDescription.text = screen.Description;

            // Start from where the screen is now (lifted by the hover, turned with the card); the card stays behind it
            RectTransform rect = screen.Rect;
            Vector3 center = rect.TransformPoint(rect.rect.center);
            rect.SetParent(focusLayer, true);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = focusLayer.InverseTransformPoint(center);
            rect.SetAsLastSibling();

            GetFocusTarget(rect.rect.size, out Vector2 position, out float scale);
            Move(screen, position, scale, openDuration, null);
            Fade(0, fadeOutDuration, 0);

            screen.Focus(this, TagsShown);
            focusGroup.blocksRaycasts = true;
            deckGroup.blocksRaycasts = false;
            introGroup.blocksRaycasts = false;
            infoAnimator.Play("Show");

            if (background != null)
                background.SetOpen(true);
        }

        public void CloseFocus()
        {
            if (focused == null || closing)
                return;

            closing = true;
            focused.ResetHover();

            // Fly back inside the card under the deck's fade-in. The screen skips that fade on the way
            DemoScreen screen = focused.Screen;
            RectTransform rect = screen.Rect;
            RectTransform frame = focused.Frame;
            Vector3 center = rect.TransformPoint(rect.rect.center);
            rect.SetParent(frame, true);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (Vector2)frame.InverseTransformPoint(center) - frame.rect.center;
            screen.IgnoreDeckFade(true);

            Move(screen, Vector2.zero, focused.DeckScale, closeDuration, finishClose);
            Fade(1, fadeInDuration, fadeInDelay);

            screen.Unfocus();
            focusGroup.blocksRaycasts = false;
            infoAnimator.Play("Hide");

            if (background != null)
                background.SetOpen(false);
        }

        // The tag toggle sits in the info column, so it only changes the open screen. Checked, it keeps its Selected look the
        // way a tab button does; unchecked, it is still under the pointer or the keyboard
        void SetTagsShown(bool value)
        {
            tagToggle.SetState(value ? InteractionState.Selected : InteractionState.Highlighted);

            if (focused != null && !closing)
                focused.Screen.SetTagsShown(value);
        }

        void Move(DemoScreen screen, Vector2 position, float scale, float duration, Action done)
        {
            moveTween.Kill();
            scaleTween.Kill();
            angleTween.Kill();

            RectTransform rect = screen.Rect;
            TweenBuilder<Vector2> move = EvoTween.Create(rect.anchoredPosition, position, duration)
                .WithEase(Ease.OutExpo)
                .WithTimeMode(TimeMode.Unscaled);

            if (done != null)
                move = move.OnComplete(done);

            moveTween = move.BindToAnchoredPosition(rect);
            scaleTween = EvoTween.Create(rect.localScale.x, scale, duration)
                .WithEase(Ease.OutExpo)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(screen, static (target, value) => target.SetScale(value), ScaleProperty);

            // The card may be turned a little; the screen straightens on the way
            float angle = Mathf.DeltaAngle(0, rect.localEulerAngles.z);
            if (angle != 0)
            {
                angleTween = EvoTween.Create(angle, 0f, duration)
                    .WithEase(Ease.OutExpo)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(rect, static (target, value) => target.localRotation = Quaternion.Euler(0, 0, value), AngleProperty);
            }
        }

        /// <summary>
        /// Fades the headline and the deck together while a screen is open.
        /// </summary>
        void Fade(float alpha, float duration, float delay)
        {
            introFade = DemoUtils.Fade(introGroup, alpha, duration, delay);
            deckFade = DemoUtils.Fade(deckGroup, alpha, duration, delay);
        }

        void FinishClose()
        {
            DemoHomeCard card = focused;
            DemoScreen screen = card.Screen;
            moveTween.Kill();
            scaleTween.Kill();
            angleTween.Kill();

            // A normal close already unfocused the screen; leaving Home mid-focus didn't
            if (!closing)
            {
                screen.Unfocus();
                if (background != null)
                    background.SetOpen(false);
            }

            screen.HideTags();
            screen.IgnoreDeckFade(false);

            RectTransform rect = screen.Rect;
            rect.SetParent(card.Frame, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            screen.SetScale(card.DeckScale);

            focused = null;
            closing = false;
            autoTime = 0;
            focusGroup.blocksRaycasts = false;
            deckGroup.blocksRaycasts = true;
            introGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// Screen beside the info column, both centered, scaled to fit.
        /// </summary>
        void GetFocusTarget(Vector2 screenSize, out Vector2 position, out float scale)
        {
            Rect area = focusLayer.rect;
            float infoWidth = infoColumn.rect.width;
            scale = DemoUtils.Min(focusMaxScale,
                (area.width - focusMargin.x * 2 - infoWidth - infoGap) / screenSize.x,
                (area.height - focusMargin.y * 2) / screenSize.y);

            float total = infoWidth + infoGap + screenSize.x * scale;
            float left = area.center.x - total * 0.5f;
            infoColumn.anchoredPosition = new Vector2(left, infoColumn.anchoredPosition.y);
            position = new Vector2(left + infoWidth + infoGap + screenSize.x * scale * 0.5f, area.center.y);
        }

        void OnRectTransformDimensionsChange()
        {
            if (!fitReady || !isActiveAndEnabled)
                return;

            Fit();
            if (focused == null || closing)
                return;

            RectTransform rect = focused.Screen.Rect;
            GetFocusTarget(rect.rect.size, out Vector2 position, out float scale);
            moveTween.Kill();
            scaleTween.Kill();
            rect.anchoredPosition = position;
            focused.Screen.SetScale(scale);
        }
        #endregion

        #region Theme
        /// <summary>
        /// The Theme Selector routes its presets through Home, so the accent can follow the current screen in either theme.
        /// </summary>
        public void SetTheme(int index, StylerPreset preset, float duration)
        {
            theme = index;
            themePreset = preset;
            ApplyAccent(duration);

            if (background != null)
            {
                background.SetTheme(index, duration);
                background.SetColor(BackgroundColor(), duration);
            }
        }

        void ApplyAccent(float duration)
        {
            if (themePreset == null || cards.Count == 0)
                return;

            if (accent != null)
                accent.Apply(themePreset, current, cards[current].Highlight(theme), onHome, duration);
            else
                Styler.ApplyPreset(themePreset, true, duration);
        }

        // The background takes the screen's highlight on Home and the preset's own accent on other pages
        Color BackgroundColor()
        {
            if (onHome)
                return cards[current].Highlight(theme);

            return themePreset != null && themePreset.TryGetColor("Accent", out Color color) ? color : cards[current].Highlight(theme);
        }
        #endregion

        #region Pages
        void OnTabChanged(int index)
        {
            bool home = index == homeTab;
            if (home == onHome)
                return;

            onHome = home;
            ApplyAccent(PageDuration);

            if (background != null)
            {
                background.SetPage(!home);
                background.SetColor(BackgroundColor(), PageDuration);
            }
        }

        int FindPage(string id)
        {
            if (pages == null || string.IsNullOrEmpty(id))
                return -1;

            for (int i = 0; i < pages.tabs.Count; i++)
            {
                if (pages.tabs[i].tabID == id)
                    return i;
            }

            return -1;
        }

        public bool HasPage(string id) => FindPage(id) >= 0;

        /// <summary>
        /// Opens a page by its tab ID, on the variant with the given tab ID. Tags and the headline's Explore call it.
        /// </summary>
        public void OpenPage(string id, string variant)
        {
            if (search != null)
                search.OpenPage(id, variant);
        }
        #endregion

#if UNITY_EDITOR
        // Edit mode: inspector changes and undos show in the scene right away
        void OnValidate()
        {
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += PreviewStart;
        }

        /// <summary>
        /// Edit mode: lays the deck out the way the page opens, start card on top, and lights the background around it.
        /// It only shows the state; Play mode sets the same one up at Start.
        /// </summary>
        public void PreviewStart()
        {
            if (this == null || Application.isPlaying || cards.Count == 0)
                return;

            int count = cards.Count;
            int start = Mathf.Clamp(startCard, 0, count - 1);
            DemoHomeCard top = cards[start];
            if (top == null)
                return;

            // Deepest first, so each card goes in over the one under it
            for (int d = count - 1; d >= 0; d--)
            {
                DemoHomeCard card = cards[(start + d) % count];
                if (card == null)
                    continue;

                Vector4 pose = Pose(d);
                RectTransform rect = card.Rect;
                rect.anchoredPosition = new Vector2(pose.x, pose.y);
                rect.localRotation = Quaternion.Euler(0, 0, pose.z);
                rect.localScale = new Vector3(pose.w, pose.w, 1);
                card.SetState(d <= 2 ? 1 : 0, d == 0 ? 1 : 0, true, true);

                if (rect.GetSiblingIndex() != count - 1 - d)
                    rect.SetSiblingIndex(count - 1 - d);
            }

            if (indicator != null)
                indicator.PreviewStart(start, top.name);

            if (background != null)
                background.PreviewStart((RectTransform)cardsGroup.transform, startTheme, top.Highlight(startTheme));
        }
#endif
    }
}