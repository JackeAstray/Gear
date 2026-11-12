using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Slider (Alt) tab: a smart bulb. The slider dims the glow and the bulb's accent.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoLight : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private CanvasGroup glow;
        [SerializeField] private CanvasGroup bulbOn;

        void Awake()
        {
            slider.onValueChanged.AddListener(Apply);
            Apply(slider.value);
        }

        void Apply(float value)
        {
            float brightness = Mathf.InverseLerp(slider.minValue, slider.maxValue, value);
            glow.alpha = brightness;
            glow.transform.localScale = Vector3.one * (0.4f + brightness * 0.8f);
            bulbOn.alpha = brightness > 0f ? 0.35f + brightness * 0.65f : 0f;
        }
    }
}