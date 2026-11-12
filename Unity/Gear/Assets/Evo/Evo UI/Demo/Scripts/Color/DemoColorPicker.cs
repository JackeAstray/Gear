using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Wheel and Square tabs: the halo and channel bars follow the picker, and the bars and right-click reset drive it.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoColorPicker : MonoBehaviour
    {
        [SerializeField] private ColorPicker picker;
        [SerializeField] private DemoColorHalo halo;

        [Header("Options")]
        [SerializeField] private Slider red;
        [SerializeField] private Slider green;
        [SerializeField] private Slider blue;
        [SerializeField] private Slider alpha;
        [SerializeField] private Switch rightClickReset;

        void Awake()
        {
            DemoUtils.Show(rightClickReset, picker.resetOnRightClick);
        }

        // The picker also sends this once when it starts
        public void OnColorChanged(Color color)
        {
            halo.Show(color);

            Color32 channels = color;
            DemoUtils.Show(red, channels.r);
            DemoUtils.Show(green, channels.g);
            DemoUtils.Show(blue, channels.b);
            DemoUtils.Show(alpha, channels.a);
        }

        public void SetRed(float value) => picker.SetRed(value / 255f);
        public void SetGreen(float value) => picker.SetGreen(value / 255f);
        public void SetBlue(float value) => picker.SetBlue(value / 255f);
        public void SetAlpha(float value) => picker.SetAlpha(value / 255f);
        public void SetRightClickReset(bool value) => picker.resetOnRightClick = value;
    }
}