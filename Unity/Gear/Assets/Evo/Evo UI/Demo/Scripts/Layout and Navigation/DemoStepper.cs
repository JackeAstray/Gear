using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Stepper tab: horizontal or vertical layout, which steps can be opened from their indicators, and the step transition.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoStepper : MonoBehaviour
    {
        [SerializeField] private Stepper stepper;

        [Header("Options")]
        [SerializeField] private CanvasGroup skipOption;

        public void SetVertical(int index)
        {
            stepper.orientation = index == 0 ? Stepper.Orientation.Horizontal : Stepper.Orientation.Vertical;
            stepper.Refresh();
        }

        // Skipping ahead only applies to steps opened from their indicators
        public void SetClickSteps(bool value)
        {
            stepper.allowIndicatorNavigation = value;
            stepper.Refresh();
            DemoUtils.SetLocked(skipOption, !value);
        }

        public void SetSkipAhead(bool value)
        {
            stepper.allowStepSkipping = value;
            stepper.Refresh();
        }

        public void SetGoBack(bool value)
        {
            stepper.allowBackwardNavigation = value;
            stepper.Refresh();
        }

        public void SetDuration(float value)
        {
            stepper.animationDuration = value;
            stepper.indicatorDuration = value;
        }
    }
}
