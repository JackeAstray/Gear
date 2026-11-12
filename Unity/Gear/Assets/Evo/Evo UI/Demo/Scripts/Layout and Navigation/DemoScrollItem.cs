using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// An item in the Scroll Rect tab's picker. Its icon and bar turn accent while it's the focused item, the one in the band.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoScrollItem : MonoBehaviour
    {
        [SerializeField] private int index;
        [SerializeField] private StylerObject icon;
        [SerializeField] private ProceduralShape bar;

        // -1 until the first call; copies made by infinite scrolling start there too
        int focused = -1;

        public int Index => index;

        // Only a change restyles, so a focus moving through the list restyles two items instead of all of them
        public void SetFocused(bool value)
        {
            int state = value ? 1 : 0;
            if (state == focused)
                return;

            focused = state;
            icon.colorID = value ? "Accent" : "Primary (Dim)";
            icon.UpdateStyler();
            bar.fillStyler.color.stylerID = value ? "Accent (Tint)" : "Primary (Soft)";
            bar.UpdateStyler();
        }
    }
}
