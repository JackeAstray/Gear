using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Page Manager tab: the swipe axis and thresholds, and the page transition.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoPageManager : MonoBehaviour
    {
        [SerializeField] private PageManager pages;

        public void SetDirection(int index)
        {
            pages.swipeDirection = index == 0 ? PageManager.SwipeDirection.Horizontal : PageManager.SwipeDirection.Vertical;
            pages.PositionPages();
        }

        public void SetThreshold(float percent) => pages.swipeThreshold = percent / 100f;

        public void SetEdgeStretch(float percent) => pages.elasticResistance = percent / 100f;

        public void SetDuration(float value) => pages.transitionDuration = value;

        public void SetPageSpacing(float value)
        {
            pages.pageSpacing = value;
            pages.PositionPages();
        }
    }
}
