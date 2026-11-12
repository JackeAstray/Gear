using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A sample screen in the Home deck: its focus texts, component tags and its own script.
    /// The deck turns the screen off while its face is hidden, which also stops that script. The tags only come on while
    /// the screen is focused with Home's tag toggle on, and go off again once they have faded out.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoScreen : MonoBehaviour
    {
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private DemoScreenContent content;
        [Tooltip("Fades the screen in the deck. Only a focused screen takes input.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private CanvasGroup tagGroup;
        [SerializeField] private TweenAnimator tagAnimator;
        [SerializeField] private DemoScreenTag[] tags;
        [SerializeField] private ProceduralShape shape;
        [Tooltip("Scale the shape's corner radius and outline are sized for. They keep that size on screen at any scale.")]
        [SerializeField, Min(0.01f)] private float edgeScale = 0.37267f;

        // Tag animator states: in after the open move, in at once from where the fade is, out from where it is
        const string OpenTagsState = "Show";
        const string ToggleTagsState = "Fade In";
        const string HideTagsState = "Hide";

        DemoHome home;
        Vector4 edgeRadius;
        float edgeOutline = -1;
        bool tagsShown;

        public string Title => title;
        public string Description => description;
        public RectTransform Rect => (RectTransform)transform;

        void Awake() => tagAnimator.Completed += OnTagsAnimated;

        /// <summary>
        /// Scales the screen without scaling its rounded corners and outline.
        /// </summary>
        public void SetScale(float scale)
        {
            Rect.localScale = new Vector3(scale, scale, 1);

            if (shape == null || scale <= 0)
                return;

            // The authored edges, read before the first change
            if (edgeOutline < 0)
            {
                edgeRadius = shape.CornerRadius * edgeScale;
                edgeOutline = shape.OutlineWidth * edgeScale;
            }

            float inverse = 1 / scale;
            shape.CornerRadius = edgeRadius * inverse;
            // shape.OutlineWidth = edgeOutline * inverse;
        }

        /// <summary>
        /// Face opacity from the deck, and whether the screen is on at all.
        /// </summary>
        public void SetFace(float opacity, bool active)
        {
            if (gameObject.activeSelf != active)
                gameObject.SetActive(active);
            if (group.alpha != opacity)
                group.alpha = opacity;
        }

        /// <summary>
        /// Only a focused screen takes input; in the deck the card handles the pointer. Shown tags come in after the open move.
        /// </summary>
        public void Focus(DemoHome owner, bool showTags)
        {
            home = owner;
            SetInput(true);

            if (showTags)
                ShowTags(OpenTagsState);
        }

        public void Unfocus()
        {
            SetInput(false);
            FadeOutTags();
        }

        /// <summary>
        /// Fades the tags in or out while the screen is focused. Home's tag toggle calls it.
        /// </summary>
        public void SetTagsShown(bool value)
        {
            if (value)
                ShowTags(ToggleTagsState);
            else
                FadeOutTags();
        }

        void ShowTags(string state)
        {
            tagsShown = true;

            foreach (DemoScreenTag tag in tags)
                tag.Place(Rect, home);

            // Off since its last fade out, so it starts from nothing
            GameObject tagObject = tagGroup.gameObject;
            if (!tagObject.activeSelf)
            {
                tagGroup.alpha = 0;
                tagObject.SetActive(true);
            }

            tagAnimator.Play(state);
        }

        void FadeOutTags()
        {
            tagsShown = false;

            if (tagGroup.gameObject.activeInHierarchy)
                tagAnimator.Play(HideTagsState);
        }

        // Faded out: the tags stay off until they are shown again
        void OnTagsAnimated(TweenAnimator animator)
        {
            if (!tagsShown)
                tagGroup.gameObject.SetActive(false);
        }

        void SetInput(bool focused)
        {
            group.blocksRaycasts = focused;
            tagGroup.blocksRaycasts = focused;

            if (content != null)
                content.Focused = focused;
        }

        /// <summary>
        /// Turns the tags off once the screen is back in its card.
        /// </summary>
        public void HideTags()
        {
            tagsShown = false;
            tagGroup.gameObject.SetActive(false);
        }

        /// <summary>
        /// A closing screen flies back inside its card; this keeps it out of the deck's fade-in until it lands.
        /// </summary>
        public void IgnoreDeckFade(bool value) => group.ignoreParentGroups = value;
    }
}
