using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Selector tab: rows of settings, one selector each. Looping, the slide's direction and timing,
    /// and how dim the other indicators are apply to every row.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSelector : MonoBehaviour
    {
        [SerializeField] private Selector[] selectors;

        public void SetLoop(bool value)
        {
            foreach (Selector sel in selectors)
            {
                sel.loop = value;
                sel.Refresh();
            }
        }

        public void SetInvert(bool value)
        {
            foreach (Selector sel in selectors)
                sel.invertAnimationDirection = value;
        }

        public void SetSlide(float value)
        {
            foreach (Selector sel in selectors)
                sel.slideOffset = value;
        }

        public void SetDuration(float value)
        {
            foreach (Selector sel in selectors)
                sel.animationDuration = value;
        }

        public void SetUnselected(float percent)
        {
            foreach (Selector sel in selectors)
            {
                sel.unselectedIndicatorAlpha = percent / 100;
                sel.Refresh();
            }
        }
    }
}