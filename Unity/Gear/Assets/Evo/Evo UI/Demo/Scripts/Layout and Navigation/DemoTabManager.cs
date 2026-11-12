using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Tab Manager tab: how the indicator moves between buttons.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoTabManager : MonoBehaviour
    {
        [SerializeField] private TabManager tabs;

        public void SetStretch(float value) => tabs.indicatorStretch = value;

        public void SetIndicatorDuration(float value) => tabs.indicatorDuration = value;
    }
}
