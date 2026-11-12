using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dock Magnifier tab: a small launcher dock built from the bottom dock's buttons, with their tooltips as labels.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoDockMagnifier : MonoBehaviour
    {
        [SerializeField] private DockMagnifier dock;

        static readonly DockMagnifier.Orientation[] GrowDirections = { DockMagnifier.Orientation.Free, DockMagnifier.Orientation.Horizontal, DockMagnifier.Orientation.Vertical };

        public void SetMaxScale(float value) => dock.maxScale = value;

        public void SetReach(float value) => dock.influenceRadius = value;

        public void SetSmoothing(float value) => dock.smoothTime = value;

        public void SetGrow(int index) => dock.scaleDirection = GrowDirections[index];
    }
}
