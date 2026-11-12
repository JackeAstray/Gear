using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Backdrop Blur tab: blur radius and tint on the glass, plus saturation, brightness and noise of what it shows.
    /// The glass fill stays white and opaque, because the blur is multiplied by it; the tint is a layer on top.
    /// The options start from the glass's own values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeBlur : MonoBehaviour
    {
        [SerializeField] private ProceduralShape glass;
        [SerializeField] private ProceduralShape tint;

        [Header("Options")]
        [SerializeField] private Slider blur;
        [SerializeField] private Slider tintAmount;
        [Tooltip("Percent; 100 keeps the captured colors.")]
        [SerializeField] private Slider saturation;
        [Tooltip("Percent; 100 keeps the captured brightness.")]
        [SerializeField] private Slider brightness;
        [Tooltip("Percent of the kit's 0.1 maximum grain.")]
        [SerializeField] private Slider noise;

        void Awake()
        {
            BackdropBlur.Settings settings = glass.BackdropBlurSettings;
            DemoUtils.Show(blur, settings.radius);
            DemoUtils.Show(saturation, settings.saturation * 100f);
            DemoUtils.Show(brightness, settings.brightness * 100f);
            DemoUtils.Show(noise, settings.noise * 1000f);

            if (tint != null)
                DemoUtils.Show(tintAmount, tint.fillStyler.color.alphaOverride * 100f);
        }

        public void SetBlur(float value)
        {
            glass.BackdropBlurSettings.radius = value;
            glass.UpdateRect();
        }

        public void SetTint(float value)
        {
            if (tint == null)
                return;

            tint.fillStyler.color.overrideAlpha = true;
            tint.fillStyler.color.alphaOverride = value / 100f;
            tint.UpdateRect();
        }

        public void SetSaturation(float value)
        {
            glass.BackdropBlurSettings.saturation = value / 100f;
            glass.UpdateRect();
        }

        public void SetBrightness(float value)
        {
            glass.BackdropBlurSettings.brightness = value / 100f;
            glass.UpdateRect();
        }

        public void SetNoise(float value)
        {
            glass.BackdropBlurSettings.noise = value / 1000f;
            glass.UpdateRect();
        }
    }
}