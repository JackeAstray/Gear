using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Plays a Tween Animator's default state while its switch is on. The option the loop animates
    /// is locked meanwhile, and puts its own value back when the loop stops. An optional speed bar
    /// sets the state's playback speed and is only unlocked while the loop plays.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeLoop : MonoBehaviour
    {
        [SerializeField] private TweenAnimator animator;
        [SerializeField] private Switch toggle;
        [SerializeField] private CanvasGroup lockedOption;
        [Tooltip("The locked option's slider, if it has one. It applies its value again when the loop stops.")]
        [SerializeField] private Slider lockedSlider;
        [SerializeField] private Slider speed;
        [SerializeField] private CanvasGroup speedOption;

        void Awake()
        {
            if (speed != null)
                DemoUtils.Show(speed, animator.GetState(animator.DefaultStateIndex).playbackSpeed);
        }

        void OnEnable()
        {
            if (toggle == null)
                return;

            // When a tab opens, the animator can be enabled after this and would ignore Play,
            // so it also gets to start the loop itself through Play On Enable
            animator.playOnEnable = toggle.IsOn;
            if (animator.isActiveAndEnabled) { Loop(toggle.IsOn, false); }
            else
            {
                DemoUtils.SetLocked(lockedOption, toggle.IsOn, false);
                DemoUtils.SetLocked(speedOption, !toggle.IsOn, false);
            }
        }

        public void SetLoop(bool value) => Loop(value, true);

        // Read every frame while playing, so a change applies without restarting the loop
        public void SetSpeed(float value) => animator.GetState(animator.DefaultStateIndex).playbackSpeed = value;

        void Loop(bool value, bool animate)
        {
            animator.playOnEnable = value;
            if (value) { animator.Play(); }
            else { animator.Stop(false); }

            DemoUtils.SetLocked(speedOption, !value, animate);
            if (lockedOption == null)
                return;

            DemoUtils.SetLocked(lockedOption, value, animate);
            if (!value && lockedSlider != null)
                lockedSlider.onValueChanged.Invoke(lockedSlider.value);
        }
    }
}