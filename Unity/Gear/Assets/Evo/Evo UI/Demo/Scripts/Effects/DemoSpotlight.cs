using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Spotlight tab: every tile has its own Spotlight, and the options change all of them together.
    /// The options start from the first tile's light.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSpotlight : MonoBehaviour
    {
        [SerializeField] private Spotlight[] spotlights;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup visibility;
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Slider radius;
        [SerializeField] private Slider softness;
        [SerializeField] private Slider opacity;
        [SerializeField] private Slider follow;

        void Awake()
        {
            Spotlight.Settings first = spotlights[0].spotlight;
            DemoUtils.Show(visibility, (int)first.visibility);
            DemoUtils.Show(color, DemoUtils.GetEffectColor(first));
            DemoUtils.Show(radius, first.radius);
            DemoUtils.Show(softness, first.softness * 100f);
            DemoUtils.Show(opacity, first.stylerColor.alphaOverride * 100f);
            DemoUtils.Show(follow, first.followDuration);
        }

        public void SetVisibility(int index)
        {
            var value = (Spotlight.Settings.Visibility)index;
            foreach (Spotlight light in spotlights)
            {
                light.spotlight.visibility = value;
                if (value == Spotlight.Settings.Visibility.Always) { light.Show(); }
                else { light.Hide(); }
            }
        }

        public void SetColor(int index)
        {
            foreach (Spotlight light in spotlights)
                DemoUtils.SetEffectColor(light, light.spotlight, index);
        }

        public void SetRadius(float value)
        {
            foreach (Spotlight light in spotlights)
                light.spotlight.radius = value;
        }

        // Softness and opacity are percent bars
        public void SetSoftness(float value)
        {
            foreach (Spotlight light in spotlights)
                light.spotlight.softness = value / 100f;
        }

        public void SetOpacity(float value)
        {
            foreach (Spotlight light in spotlights)
                DemoUtils.SetEffectOpacity(light, light.spotlight, value);
        }

        public void SetFollow(float value)
        {
            foreach (Spotlight light in spotlights)
                light.spotlight.followDuration = value;
        }
    }
}