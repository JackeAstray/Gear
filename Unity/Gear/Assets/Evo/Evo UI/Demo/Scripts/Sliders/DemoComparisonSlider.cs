using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Comparison Slider tab: its orientation and how the divider follows the pointer.
    /// A tag names each image from its own corner and fades out while the other image covers it.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoComparisonSlider : MonoBehaviour
    {
        [SerializeField] private ComparisonSlider slider;
        [SerializeField] private RectTransform handleIcon;

        [Header("Tags")]
        [SerializeField] private CanvasGroup firstTag;
        [SerializeField] private CanvasGroup secondTag;
        [SerializeField] private float tagInset = 21f;
        [Tooltip("How far before the divider reaches a tag it starts to fade, in pixels.")]
        [SerializeField] private float tagFade = 16f;

        readonly Vector3[] corners = new Vector3[4];

        void Awake()
        {
            if (firstTag == null || secondTag == null)
                return;

            slider.onValueChanged.AddListener(_ => FadeTags());
            PlaceTags();
        }

        // The dots on the handle turn with the divider
        public void SetOrientation(int index)
        {
            slider.SliderOrientation = (ComparisonSlider.Orientation)index;
            handleIcon.localEulerAngles = new Vector3(0, 0, index == 1 ? 90 : 0);
            PlaceTags();
        }

        public void SetReverse(bool value)
        {
            slider.Reverse = value;
            PlaceTags();
        }

        public void SetClickToMove(bool value) => slider.clickToMove = value;

        public void SetDragOnHover(bool value) => slider.dragOnHover = value;

        // The first image starts on the left, or at the bottom when vertical; Reverse moves it to the other side
        void PlaceTags()
        {
            if (firstTag == null || secondTag == null)
                return;

            bool vertical = slider.SliderOrientation == ComparisonSlider.Orientation.Vertical;
            bool reverse = slider.Reverse;
            Place(firstTag, !vertical && reverse, vertical && !reverse);
            Place(secondTag, !vertical && !reverse, vertical && reverse);
            FadeTags();
        }

        void Place(CanvasGroup tag, bool right, bool bottom)
        {
            var rect = (RectTransform)tag.transform;
            var anchor = new Vector2(right ? 1 : 0, bottom ? 0 : 1);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(right ? -tagInset : tagInset, bottom ? tagInset : -tagInset);
        }

        void FadeTags()
        {
            var area = (RectTransform)slider.transform;
            bool vertical = slider.SliderOrientation == ComparisonSlider.Orientation.Vertical;
            int axis = vertical ? 1 : 0;
            float length = area.rect.size[axis];
            float padding = 5f;
            float position = slider.Reverse ? 1f - slider.Value : slider.Value;
            float divider = padding + position * (length - padding * 2f);

            // The first image covers the start of the axis up to the divider, or the rest when reversed
            Fade(firstTag, area, axis, divider, !slider.Reverse);
            Fade(secondTag, area, axis, divider, slider.Reverse);
        }

        void Fade(CanvasGroup tag, RectTransform area, int axis, float divider, bool beforeDivider)
        {
            ((RectTransform)tag.transform).GetWorldCorners(corners);
            float min = area.InverseTransformPoint(corners[0])[axis] - area.rect.min[axis];
            float max = area.InverseTransformPoint(corners[2])[axis] - area.rect.min[axis];
            float room = beforeDivider ? divider - max : min - divider;
            tag.alpha = Mathf.Clamp01(room / tagFade);
        }
    }
}
