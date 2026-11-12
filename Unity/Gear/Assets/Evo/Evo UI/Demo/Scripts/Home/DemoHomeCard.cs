using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A card in the Home deck. Its Interactive drives the hover look through a Tween Animator and tells the deck
    /// while the pointer is on the card; this component passes drags and clicks to the deck and holds the screen's highlight color.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHomeCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform frame;
        [SerializeField] private DemoScreen screen;
        [Tooltip("Fades the whole card, back included.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Interactive button;
        [SerializeField] private TweenAnimator hoverAnimator;

        [Header("Highlight")]
        [Tooltip("Accent and background color while this screen is on top, light theme.")]
        [SerializeField] private Color lightHighlight = new Color32(0, 132, 255, 255);
        [Tooltip("Accent and background color while this screen is on top, dark theme.")]
        [SerializeField] private Color darkHighlight = new Color32(100, 180, 255, 255);

        DemoHome home;
        RectTransform rect;

        public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;
        public RectTransform Frame => frame;
        public DemoScreen Screen => screen;
        public float DeckScale => frame.rect.width / screen.Rect.rect.width;

        /// <summary>
        /// Highlight for a Theme Selector preset index: 0 is light, 1 is dark.
        /// </summary>
        public Color Highlight(int theme) => theme == 0 ? lightHighlight : darkHighlight;

        public void Setup(DemoHome owner)
        {
            home = owner;
            button.OnStateChanged += OnStateChanged;
        }

        /// <summary>
        /// Card and face opacity from the deck, whether the card takes the pointer and whether its screen is on.
        /// </summary>
        public void SetState(float opacity, float face, bool interactive, bool live)
        {
            if (group.alpha != opacity)
                group.alpha = opacity;
            if (group.blocksRaycasts != interactive)
                group.blocksRaycasts = interactive;

            screen.SetFace(face, live);
        }

        public void OnBeginDrag(PointerEventData eventData) => home.BeginDrag(this, eventData);

        public void OnDrag(PointerEventData eventData) => home.Drag(eventData);

        public void OnEndDrag(PointerEventData eventData) => home.EndDrag(eventData);

        /// <summary>
        /// Called by the card's Interactive.
        /// </summary>
        public void Open() => home.Open(this);

        /// <summary>
        /// Jumps to the resting look, the hover animator's default state. DemoHome calls it on close and when the page comes back.
        /// </summary>
        public void ResetHover()
        {
            if (hoverAnimator == null || !hoverAnimator.isActiveAndEnabled)
                return;

            hoverAnimator.Play();
            hoverAnimator.Complete();
        }

        // A pressed card is still under the pointer
        void OnStateChanged(InteractionState state)
        {
            bool hovered = state == InteractionState.Highlighted || state == InteractionState.Pressed;
            if (!hovered) { home.SetHovered(this, false); }
            else if (!home.IsFocused) { home.SetHovered(this, true); }
        }
    }
}
