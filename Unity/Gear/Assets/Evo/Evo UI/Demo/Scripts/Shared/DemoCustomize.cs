using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Remembers the starting value of every option control and puts them back on Reset.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCustomize : MonoBehaviour
    {
        [SerializeField] private Transform options;

        Switch[] switches;
        bool[] switchDefaults;
        Slider[] sliders;
        float[] sliderDefaults;
        RadioButtonGroup[] groups;
        int[] groupDefaults;

        // Start, not Awake: pages set their options from their shapes in Awake, and those are the values Reset returns to
        void Start()
        {
            switches = options.GetComponentsInChildren<Switch>(true);
            switchDefaults = new bool[switches.Length];
            for (int i = 0; i < switches.Length; i++)
                switchDefaults[i] = switches[i].IsOn;

            sliders = options.GetComponentsInChildren<Slider>(true);
            sliderDefaults = new float[sliders.Length];
            for (int i = 0; i < sliders.Length; i++)
                sliderDefaults[i] = sliders[i].value;

            groups = options.GetComponentsInChildren<RadioButtonGroup>(true);
            groupDefaults = new int[groups.Length];
            for (int i = 0; i < groups.Length; i++)
                groupDefaults[i] = groups[i].selectedIndex;
        }

        public void ResetOptions()
        {
            for (int i = 0; i < switches.Length; i++)
                switches[i].SetValue(switchDefaults[i]);

            for (int i = 0; i < sliders.Length; i++)
                sliders[i].value = sliderDefaults[i];

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].selectedIndex != groupDefaults[i])
                    groups[i].SetButton(groupDefaults[i]);
            }
        }
    }
}