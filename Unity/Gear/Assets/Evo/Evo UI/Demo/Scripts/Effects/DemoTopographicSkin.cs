using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Topographic Skin tab: the card surface and the button share one pattern setup.
    /// Primary and Accent come from the Styler; the other colors are custom. The options start from the card's skin.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoTopographicSkin : MonoBehaviour
    {
        [SerializeField] private TopographicSkin[] skins;
        [Tooltip("Each skin's Styler Object, in the same order.")]
        [SerializeField] private StylerObject[] stylers;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Slider scale;
        [SerializeField] private Slider density;
        [SerializeField] private Slider lineWidth;
        [SerializeField] private Slider speed;

        [Header("Colors")]
        [SerializeField] private string[] stylerColors = { "Primary", "Accent" };
        [SerializeField] private Color[] customColors = { new(0.855f, 0.439f, 0.471f), new(0.498f, 0.847f, 0.651f) };

        void Awake()
        {
            TopographicSkin first = skins[0];
            StylerObject styler = stylers[0];
            DemoUtils.Show(color, styler.useCustomColor ? stylerColors.Length : System.Array.IndexOf(stylerColors, styler.colorID));
            DemoUtils.Show(scale, first.Scale);
            DemoUtils.Show(density, first.Density);
            DemoUtils.Show(lineWidth, first.LineWidth);
            DemoUtils.Show(speed, first.Speed);
        }

        public void SetColor(int index)
        {
            for (int i = 0; i < skins.Length; i++)
            {
                // Custom colors keep each skin's own alpha
                StylerObject styler = stylers[i];
                styler.useCustomColor = index >= stylerColors.Length;
                if (!styler.useCustomColor) { styler.colorID = stylerColors[index]; }
                else
                {
                    Color custom = customColors[index - stylerColors.Length];
                    custom.a = styler.alphaOverride;
                    skins[i].color = custom;
                }
                styler.UpdateStyler();
            }
        }

        public void SetScale(float value)
        {
            foreach (TopographicSkin skin in skins)
                skin.Scale = value;
        }

        public void SetDensity(float value)
        {
            foreach (TopographicSkin skin in skins)
                skin.Density = value;
        }

        public void SetLineWidth(float value)
        {
            foreach (TopographicSkin skin in skins)
            {
                skin.LineWidth = value;
                skin.MajorLineWidth = value * 1.6f;
            }
        }

        public void SetSpeed(float value)
        {
            foreach (TopographicSkin skin in skins)
                skin.Speed = value;
        }
    }
}