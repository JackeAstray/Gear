using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// One answer tile on the Timer (Horizontal) quiz: a letter chip and a skeleton bar on a Dim well,
    /// with layers for picked (accent outline), correct (accent tile and check) and wrong (negative tile and cross).
    /// </summary>
    [AddComponentMenu("")]
    public class DemoQuizAnswer : MonoBehaviour
    {
        public enum State { Open, Picked, Correct, Wrong, Faded }

        const float FadeDuration = 0.2f;
        const float FadedAlpha = 0.45f;
        const float BarDuration = 0.3f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private CanvasGroup picked;
        [SerializeField] private CanvasGroup correct;
        [SerializeField] private CanvasGroup wrong;
        [SerializeField] private StylerObject letter;
        [SerializeField] private ProceduralShape bar;
        [SerializeField] private Vector2 barWidth = new(72, 122);

        public void SetState(State state, bool animate = true)
        {
            Fade(group, state == State.Faded ? FadedAlpha : 1, animate);
            Fade(picked, state == State.Picked ? 1 : 0, animate);
            Fade(correct, state == State.Correct ? 1 : 0, animate);
            Fade(wrong, state == State.Wrong ? 1 : 0, animate);

            string letterColor = state == State.Correct ? "Accent" : state == State.Wrong ? "Negative" : "Primary (Dim)";
            if (letter.colorID != letterColor)
            {
                letter.colorID = letterColor;
                letter.UpdateStyler();
            }

            string barColor = state == State.Correct ? "Accent (Tint)" : "Primary (Soft)";
            if (bar.fillStyler.color.stylerID != barColor)
            {
                bar.fillStyler.color.stylerID = barColor;
                bar.UpdateStyler();
            }
        }

        // A new question gets a new answer length
        public void Shuffle(bool animate)
        {
            RectTransform rect = bar.rectTransform;
            float width = Random.Range(barWidth.x, barWidth.y);
            if (!animate)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

            EvoTween.Create(rect.sizeDelta.x, width, animate ? BarDuration : 0)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(rect, static (r, w) => r.sizeDelta = new Vector2(w, r.sizeDelta.y), 1);
        }

        static void Fade(CanvasGroup layer, float alpha, bool animate)
        {
            if (!animate)
                layer.alpha = alpha;

            DemoUtils.Fade(layer, alpha, animate ? FadeDuration : 0);
        }
    }
}
