using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Button (Magnetic) tab: a title screen with a Play button and two corner buttons. Each one's content leans toward
    /// the cursor while hovered; Play carries its disc and its ring along (the ring pulses on a Tween Animator).
    /// Motion options apply to every button.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoMagnetic : DemoButtonPage
    {
        [SerializeField] private RectSway[] sways;

        // Motion
        public void SetSwayIntensity(float value)
        {
            foreach (RectSway sway in sways)
                sway.swayIntensity = value;
        }

        public void SetSwaySmoothness(float value)
        {
            foreach (RectSway sway in sways)
                sway.smoothness = value;
        }

        // 0: both, 1: horizontal, 2: vertical
        public void SetSwayAxis(int index)
        {
            foreach (RectSway sway in sways)
            {
                sway.enableXAxis = index != 2;
                sway.enableYAxis = index != 1;
            }
        }

        // Sway follows the pointer on its own, so it stops with the buttons
        protected override void OnInteractableChanged(bool value)
        {
            foreach (RectSway sway in sways)
            {
                // A disabled sway misses the pointer leaving, so it can't keep following it afterwards
                if (!value)
                {
                    sway.AllowSway = false;
                    sway.ResetSway();
                }

                sway.enabled = value;
            }
        }
    }
}
