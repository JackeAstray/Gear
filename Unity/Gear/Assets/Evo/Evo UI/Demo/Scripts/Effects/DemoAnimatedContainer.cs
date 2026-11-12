using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Animated Container tab: a feed whose rows pop in when the page opens. Every option replays it;
    /// sliders wait until they settle, so dragging one doesn't restart the animation on every step.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoAnimatedContainer : MonoBehaviour
    {
        [SerializeField] private AnimatedContainer container;
        [Tooltip("Linear, Ease out and Overshoot, in the order of the Curve options.")]
        [SerializeField] private AnimationCurve[] curves;

        const float SettleDelay = 0.25f;
        float replayAt = -1f;

        void OnDisable() => replayAt = -1f;

        void Update()
        {
            if (replayAt < 0f || Time.unscaledTime < replayAt)
                return;

            replayAt = -1f;
            container.Animate();
        }

        public void Replay()
        {
            replayAt = -1f;
            container.Animate();
        }

        public void SetCurve(int index)
        {
            container.animationCurve = curves[index];
            Replay();
        }

        public void SetStartScale(float percent)
        {
            container.startScale = percent / 100f;
            ReplaySoon();
        }

        public void SetFadeAfter(float percent)
        {
            container.fadeAfterScale = percent / 100f;
            ReplaySoon();
        }

        public void SetItemDelay(float seconds)
        {
            container.itemCooldown = seconds;
            ReplaySoon();
        }

        public void SetScaleDuration(float seconds)
        {
            container.scaleDuration = seconds;
            ReplaySoon();
        }

        public void SetFadeDuration(float seconds)
        {
            container.fadeDuration = seconds;
            ReplaySoon();
        }

        // Sliders also report their value before the page is shown; only a visible page replays
        void ReplaySoon()
        {
            if (isActiveAndEnabled)
                replayAt = Time.unscaledTime + SettleDelay;
        }
    }
}
