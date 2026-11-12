using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Showcase Panel tab: timer and transition options.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShowcasePanel : MonoBehaviour
    {
        [SerializeField] private ShowcasePanel panel;

        public void SetTimer(float value) => panel.timer = value;
        public void SetDuration(float value) => panel.animationDuration = value;
        public void SetSlideOffset(float value) => panel.slideOffset = new Vector2(panel.slideOffset.x, value);
    }
}
