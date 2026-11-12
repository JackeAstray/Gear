using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Radial Slider and Knob tabs: the arc and how dragging works, for every slider on the stage.
    /// The Radial Slider tab's goal chip turns into a check while its slider sits at the maximum.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRadialSlider : MonoBehaviour
    {
        [SerializeField] private RadialSlider[] sliders;
        [Tooltip("The track arc behind each slider's fill, in the same order.")]
        [SerializeField] private ProceduralShape[] tracks;

        [Header("Goal")]
        [SerializeField] private CanvasGroup goalDone;

        const float GoalFadeDuration = 0.2f;

        void Awake()
        {
            if (goalDone == null || sliders.Length == 0)
                return;

            sliders[0].onValueChanged.AddListener(UpdateGoal);
            goalDone.alpha = Reached(sliders[0].Value) ? 1f : 0f;
        }

        bool Reached(float value) => value >= sliders[0].MaxValue;

        void UpdateGoal(float value) => DemoUtils.Fade(goalDone, Reached(value) ? 1f : 0f, GoalFadeDuration);

        public void SetAngleRange(float value)
        {
            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i].angleRange = value;
                UpdateArc(i);
            }
        }

        public void SetStartAngle(float value)
        {
            for (int i = 0; i < sliders.Length; i++)
            {
                sliders[i].startAngle = value;
                UpdateArc(i);
            }
        }

        public void SetContinuousDrag(bool value)
        {
            foreach (RadialSlider slider in sliders)
                slider.continuousDrag = value;
        }

        public void SetInteractable(bool value)
        {
            foreach (RadialSlider slider in sliders)
                slider.SetInteractable(value);
        }

        // The kit turns and clips the fill; the track arc behind it is set in the prefab, so it follows here
        void UpdateArc(int index)
        {
            RadialSlider slider = sliders[index];
            ProceduralShape track = tracks[index];
            slider.Refresh();
            track.ClipAmount = slider.angleRange / 360;
            track.transform.localEulerAngles = new Vector3(0, 0, slider.startAngle);
        }
    }
}
