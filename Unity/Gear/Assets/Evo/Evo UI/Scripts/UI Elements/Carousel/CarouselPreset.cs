using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "ui-elements/carousel")]
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class CarouselPreset : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, TextComponent] private Graphic titleText;
        [SerializeField, TextComponent] private Graphic descriptionText;
        [SerializeField, Tooltip("Takes priority over Background Image when assigned.")] private ProceduralShape backgroundShape;
        [SerializeField] private Image backgroundImage;

        [Header("Optional")]
        public Button actionButton;
        [SerializeField, Tooltip("Fades out and back in on its own timing during single item transitions.")]
        private CanvasGroup contentGroup;

        // Share of a transition the old content fades out in; the new content fades in over the rest
        const float ContentFadeSplit = 0.4f;

        // Cache
        RectTransform rectTransform;
        CanvasGroup canvasGroup;
        Carousel.Item content;
        Button boundButton;
        UnityAction clickAction;
        Vector2 layoutOrigin;
        TweenHandle contentTween;
        Action<float> setContentAlpha;

        // Authored background values, kept while a transition resizes it
        bool revealing;
        Vector2 revealOffsetMin;
        Vector2 revealOffsetMax;
        Vector4 revealCornerRadius;
        bool revealIndependentCorners;
        float revealRadius;

        internal RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                    rectTransform = GetComponent<RectTransform>();

                return rectTransform;
            }
        }

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
        }

        void OnDestroy()
        {
            Unbind();
            contentTween.Kill();
        }

        internal bool RefreshContent(Carousel.Item item, bool bind)
        {
            bool changed = false;
            content = item;
            string title = item?.title ?? string.Empty;
            string description = item?.description ?? string.Empty;
            Sprite background = item?.background;

            changed |= TextProvider.UpdateText(titleText, title);
            changed |= TextProvider.UpdateText(descriptionText, description);

            if (backgroundShape != null)
            {
                if (backgroundShape.Sprite != background)
                {
                    backgroundShape.Sprite = background;
                    changed = true;
                }
            }
            else if (backgroundImage != null && backgroundImage.sprite != background)
            {
                backgroundImage.sprite = background;
                changed = true;
            }

            if (boundButton != (bind ? actionButton : null))
            {
                Unbind();

                if (bind && actionButton != null)
                {
                    clickAction ??= OnClick;
                    boundButton = actionButton;
                    boundButton.onClick.AddListener(clickAction);
                }
            }

            if (actionButton != null)
            {
                bool active = item?.onClick != null && item.onClick.GetPersistentEventCount() > 0;
                if (actionButton.gameObject.activeSelf != active)
                {
                    actionButton.gameObject.SetActive(active);
                    changed = true;
                }
            }

            return changed;
        }

        internal void Unbind()
        {
            if (boundButton != null)
                boundButton.onClick.RemoveListener(clickAction);

            boundButton = null;
        }

        void OnClick() => content?.onClick?.Invoke();

        internal bool PrepareLayout(Vector3 scale, Vector2 size, Vector2 parentSize)
        {
            RectTransform rect = RectTransform;
            bool changed = rect.localScale != scale || rect.sizeDelta != size;
            if (changed)
            {
                rect.localScale = scale;
                rect.sizeDelta = size;
            }

            // Center the rect in the parent from local values only, so the target is identical on every refresh
            Vector2 anchor = rect.anchorMin + Vector2.Scale(rect.anchorMax - rect.anchorMin, rect.pivot);
            Vector2 center = rect.localRotation * Vector3.Scale(rect.localScale, rect.rect.center);
            layoutOrigin = Vector2.Scale(parentSize, new Vector2(0.5f, 0.5f) - anchor) - center;
            return changed;
        }

        internal void SetContent(Carousel.Item item) => RefreshContent(item, boundButton != null);

#if UNITY_EDITOR
        internal void RecordPrefabChanges()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                GeneratedObjects.Record(gameObject, RectTransform, GetComponent<CanvasGroup>(), titleText, descriptionText,
                    backgroundShape, backgroundImage, actionButton != null ? actionButton.gameObject : null);
        }
#endif

        internal bool SetLayout(Vector2 offset, float alpha, bool interactable)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            bool active = alpha > 0f;
            bool changed = SetPosition(layoutOrigin + offset) || canvasGroup.alpha != alpha
                || canvasGroup.interactable != interactable || canvasGroup.blocksRaycasts != interactable || gameObject.activeSelf != active;

            SetAlpha(alpha);
            SetInteractable(interactable);

            if (gameObject.activeSelf != active)
                gameObject.SetActive(active);

            return changed;
        }

        /// <summary>
        /// Resizes the background to a share (0-1) of its authored size along an axis, kept at its min or max edge.
        /// Corners on the moving edge are square, or shrink near the far edge to stay inside the authored outline;
        /// the covered item reaches under the covering one, so the seam never shows a corner.
        /// A share of 1 restores the authored values, so edit mode never changes anything.
        /// </summary>
        internal void SetReveal(float amount, int axis, bool atMax, bool covering)
        {
            Graphic background = backgroundShape != null ? backgroundShape : backgroundImage;
            if (background == null)
                return;

            RectTransform rect = background.rectTransform;
            if (amount >= 1f)
            {
                if (revealing)
                    EndReveal(rect);

                return;
            }

            if (!revealing)
                BeginReveal(rect);

            float length = (rect.anchorMax[axis] - rect.anchorMin[axis]) * (rect.parent is RectTransform parent ? parent.rect.size[axis] : 0f)
                + revealOffsetMax[axis] - revealOffsetMin[axis];
            float radius = Mathf.Min(revealRadius, length * 0.5f);
            float shown = covering ? amount * length : Mathf.Min(length, amount * length + radius * 2f);

            // Moving edge corners shrink by their distance from the far edge, which keeps them tangent inside the outline;
            // a piece shorter than its corners rounds as far as it can and pulls in across the axis
            float half = shown * 0.5f;
            float outer = Mathf.Min(half, radius);
            float inner = Mathf.Min(half, Mathf.Max(0f, radius - (length - shown)));
            float minRadius = atMax ? inner : outer;
            float maxRadius = atMax ? outer : inner;
            float inset = Mathf.Max(0f, radius - half);

            Vector2 offsetMin = revealOffsetMin;
            Vector2 offsetMax = revealOffsetMax;
            if (atMax)
                offsetMin[axis] += length - shown;
            else
                offsetMax[axis] -= length - shown;

            offsetMin[1 - axis] += inset;
            offsetMax[1 - axis] -= inset;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            // Stored order is top-left, top-right, bottom-right, bottom-left; the min side is left or bottom
            if (backgroundShape != null)
                backgroundShape.CornerRadius = new Vector4(axis == 0 ? minRadius : maxRadius, maxRadius, axis == 0 ? maxRadius : minRadius, minRadius);
        }

        void BeginReveal(RectTransform rect)
        {
            revealing = true;
            revealOffsetMin = rect.offsetMin;
            revealOffsetMax = rect.offsetMax;
            revealRadius = 0f;

            if (backgroundShape == null)
                return;

            // Corners are written per side in pixels while a transition runs
            Vector4 radii = backgroundShape.GetRadiiPixels(backgroundShape.GetDrawingRect());
            revealRadius = Mathf.Max(Mathf.Max(radii.x, radii.y), Mathf.Max(radii.z, radii.w));
            revealCornerRadius = backgroundShape.CornerRadius;
            revealIndependentCorners = backgroundShape.IndependentCorners;
            backgroundShape.IndependentCorners = true;
        }

        void EndReveal(RectTransform rect)
        {
            revealing = false;
            rect.offsetMin = revealOffsetMin;
            rect.offsetMax = revealOffsetMax;

            if (backgroundShape == null)
                return;

            backgroundShape.CornerRadius = revealCornerRadius;
            backgroundShape.IndependentCorners = revealIndependentCorners;
        }

        /// <summary>
        /// Fades the content on its own timing, apart from the background's ease: out over the first part
        /// of a transition, in over the rest. A duration of 0 sets it right away.
        /// </summary>
        internal void FadeContent(bool visible, float duration, TimeMode timeMode)
        {
            if (contentGroup == null)
                return;

            float alpha = visible ? 1f : 0f;
            contentTween.Kill();

            if (duration <= 0f || contentGroup.alpha == alpha)
            {
                contentGroup.alpha = alpha;
                return;
            }

            setContentAlpha ??= SetContentAlpha;
            contentTween = EvoTween.Create(contentGroup.alpha, alpha, duration * (visible ? 1f - ContentFadeSplit : ContentFadeSplit))
                .WithDelay(visible ? duration * ContentFadeSplit : 0f)
                .WithEase(Ease.InOutSine)
                .WithTimeMode(timeMode)
                .Bind(setContentAlpha);
        }

        void SetContentAlpha(float alpha) => contentGroup.alpha = alpha;

        bool SetPosition(Vector2 position)
        {
            Vector2 current = RectTransform.anchoredPosition;
            if (current == position)
                return false;

            rectTransform.anchoredPosition = position;
            return !rectTransform.anchoredPosition.Equals(current);
        }

        void SetAlpha(float alpha)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup.alpha != alpha)
                canvasGroup.alpha = alpha;
        }

        void SetInteractable(bool interactable)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup.interactable != interactable)
                canvasGroup.interactable = interactable;
            if (canvasGroup.blocksRaycasts != interactable)
                canvasGroup.blocksRaycasts = interactable;
        }
    }
}