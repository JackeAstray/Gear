using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Carousel tabs: the timer, the transition, and how the indicators look.
    /// Refresh keeps the generated items and applies the new layout.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCarousel : MonoBehaviour
    {
        [SerializeField] private Carousel carousel;

        [Header("Options")]
        [SerializeField] private CanvasGroup intervalOption;

        public void SetAutoSlide(bool value)
        {
            carousel.autoSlide = value;
            carousel.Refresh();
            DemoUtils.SetLocked(intervalOption, !value);
        }

        public void SetInterval(float value) => carousel.autoSlideTimer = value;

        public void SetDuration(float value) => carousel.animationDuration = value;

        public void SetIdleSize(float percent)
        {
            carousel.indicatorShrink = percent / 100f;
            carousel.Refresh();
        }
    }
}
