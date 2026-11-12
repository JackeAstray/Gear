using UnityEngine;

namespace Evo.UI.Demo
{
    [AddComponentMenu("")]
    public class DemoThemeSelector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float transitionDuration = 0.3f;
        [SerializeField] private StylerPreset[] presets;
        [Tooltip("Optional. Home applies the preset, so its accent can follow the current screen.")]
        [SerializeField] private DemoHome home;

        public void SetPreset(int index)
        {
            if (home != null)
                home.SetTheme(index, presets[index], transitionDuration);
            else
                Styler.ApplyPreset(presets[index], transitionDuration: transitionDuration);
        }
    }
}
