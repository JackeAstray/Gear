using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Border Glow tab: every tile has its own Border Glow, and the options change all of them together.
    /// The options start from the first tile's glow.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoBorderGlow : MonoBehaviour
    {
        [SerializeField] private BorderGlow[] glows;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup visibility;
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Slider width;
        [SerializeField] private Slider radius;
        [SerializeField] private Slider ambient;

        void Awake()
        {
            BorderGlow.Settings first = glows[0].borderGlow;
            DemoUtils.Show(visibility, (int)first.visibility);
            DemoUtils.Show(color, DemoUtils.GetEffectColor(first));
            DemoUtils.Show(width, first.width);
            DemoUtils.Show(radius, first.radius);
            DemoUtils.Show(ambient, first.ambient * 100f);
        }

        public void SetVisibility(int index)
        {
            var value = (BorderGlow.Settings.Visibility)index;
            foreach (BorderGlow glow in glows)
            {
                glow.borderGlow.visibility = value;
                if (value == BorderGlow.Settings.Visibility.Always) { glow.Show(); }
                else { glow.Hide(); }
            }
        }

        public void SetColor(int index)
        {
            foreach (BorderGlow glow in glows)
                DemoUtils.SetEffectColor(glow, glow.borderGlow, index);
        }

        public void SetWidth(float value)
        {
            foreach (BorderGlow glow in glows)
                glow.borderGlow.width = value;
        }

        public void SetRadius(float value)
        {
            foreach (BorderGlow glow in glows)
                glow.borderGlow.radius = value;
        }

        public void SetAmbient(float value)
        {
            foreach (BorderGlow glow in glows)
                glow.borderGlow.ambient = value / 100f;
        }
    }
}