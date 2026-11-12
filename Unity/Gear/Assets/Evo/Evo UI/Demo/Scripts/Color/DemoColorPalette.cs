using UnityEngine;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Palette tab: the halo and hex line follow the selected swatch, and the options change the palette's look and behavior.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoColorPalette : MonoBehaviour
    {
        [SerializeField] private PaletteColorPicker palette;
        [SerializeField] private DemoColorHalo halo;
        [SerializeField] private TMP_Text hex;

        [Header("Options")]
        [SerializeField] private Slider size;
        [SerializeField] private Slider hoverScale;
        [SerializeField] private Slider push;
        [SerializeField] private Switch deselect;
        [SerializeField] private Switch rightClickClear;
        [Tooltip("Locked while Deselect is off: the palette only clears a selection it is allowed to deselect.")]
        [SerializeField] private CanvasGroup rightClickClearOption;

        const string HexDigits = "0123456789ABCDEF";
        readonly char[] hexBuffer = { '#', '0', '0', '0', '0', '0', '0' };
        int startIndex;

        void Awake()
        {
            startIndex = palette.SelectedIndex;
            DemoUtils.Show(size, palette.swatchRadius);
            DemoUtils.Show(hoverScale, palette.hoverScale);
            DemoUtils.Show(push, palette.pushDistance);
            DemoUtils.Show(deselect, palette.allowDeselection);
            DemoUtils.Show(rightClickClear, palette.clearOnRightClick);
            DemoUtils.SetLocked(rightClickClearOption, !palette.allowDeselection, false);
        }

        // The palette doesn't announce the selection it starts with
        void Start() => OnSelectionChanged(palette.SelectedIndex);

        public void OnSelectionChanged(int index)
        {
            if (index < 0)
            {
                halo.Hide();
                hex.text = "-";
                return;
            }

            Color color = palette.SelectedColor;
            halo.Show(color);

            // A char buffer keeps the text update free of garbage
            Color32 c = color;
            WriteHex(c.r, 1);
            WriteHex(c.g, 3);
            WriteHex(c.b, 5);
            hex.SetText(hexBuffer);
        }

        public void SetSize(float value)
        {
            palette.swatchRadius = value;
            palette.Refresh();
        }

        public void SetHoverScale(float value)
        {
            palette.hoverScale = value;
            palette.Refresh();
        }

        public void SetPush(float value)
        {
            palette.pushDistance = value;
            palette.Refresh();
        }

        public void SetDeselect(bool value)
        {
            palette.allowDeselection = value;
            DemoUtils.SetLocked(rightClickClearOption, !value);
        }

        public void SetRightClickClear(bool value) => palette.clearOnRightClick = value;

        // Reset Button: the selection isn't an option control, so it goes back here
        public void ResetSelection() => palette.SetSelectedIndex(startIndex);

        void WriteHex(byte value, int index)
        {
            hexBuffer[index] = HexDigits[value >> 4];
            hexBuffer[index + 1] = HexDigits[value & 15];
        }
    }
}