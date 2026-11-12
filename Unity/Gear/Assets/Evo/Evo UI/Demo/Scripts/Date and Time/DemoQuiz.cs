using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Timer (Horizontal) tab: a quiz card. The bar runs under the question; picking an answer pauses it, and a moment
    /// later the right answer shows (time running out shows it too). The dots keep score over five questions.
    /// Loop moves on to the next question by itself; without it, a click on the card does.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoQuiz : DemoTimer
    {
        const int Questions = 5;
        const float BarDuration = 0.3f;

        [SerializeField] private DemoQuizAnswer[] answers;
        [SerializeField] private RectTransform[] questionBars;
        [SerializeField] private Vector2[] questionWidths;
        [SerializeField] private ProceduralShape[] dots;
        [SerializeField] private float revealDelay = 0.45f;
        [SerializeField] private float nextDelay = 1.6f;

        [Header("Info")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private string askText = "Pick an answer before the bar runs out.";
        [SerializeField] private string waitText = "Click the card for the next question.";

        enum Step { Ask, Picked, Reveal }

        Step step;
        int question;
        int correct;
        int picked;
        float revealAt;
        float nextAt;

        protected override void OnEnable()
        {
            question = 0;
            for (int i = 0; i < dots.Length; i++)
                SetDot(i, "Primary (Soft)");

            base.OnEnable();
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (step == Step.Picked && now >= revealAt) { Reveal(picked); }
            else if (step == Step.Reveal && nextAt > 0 && now >= nextAt) { Next(); }
        }

        public override void Restart() => Ask(false);

        public override void SetRunning(bool value)
        {
            if (step == Step.Ask)
                base.SetRunning(value);
        }

        public override void SetLoop(bool value)
        {
            base.SetLoop(value);
            if (step != Step.Reveal)
                return;

            nextAt = value ? Time.unscaledTime + nextDelay * 0.5f : 0;
            info.text = value ? askText : waitText;
        }

        // An answer's click; once the answer shows, any click moves on
        public void Pick(int index)
        {
            if (step == Step.Reveal) { Continue(); }
            else if (step == Step.Ask)
            {
                step = Step.Picked;
                picked = index;
                revealAt = Time.unscaledTime + revealDelay;
                timer.PauseTimer();
                answers[index].SetState(DemoQuizAnswer.State.Picked);
            }
        }

        // The card's click
        public void Continue()
        {
            if (step == Step.Reveal && nextAt == 0)
                Next();
        }

        protected override void OnTimerComplete()
        {
            if (step == Step.Ask)
                Reveal(-1);
        }

        void Next()
        {
            question = (question + 1) % Questions;
            if (question == 0)
            {
                for (int i = 0; i < dots.Length; i++)
                    SetDot(i, "Primary (Soft)");
            }

            Ask(true);
        }

        void Ask(bool animate)
        {
            step = Step.Ask;
            nextAt = 0;
            correct = Random.Range(0, answers.Length);
            info.text = askText;
            SetDot(question, "Accent");

            foreach (DemoQuizAnswer answer in answers)
            {
                answer.SetState(DemoQuizAnswer.State.Open, animate);
                answer.Shuffle(animate);
            }

            for (int i = 0; i < questionBars.Length; i++)
                SetWidth(questionBars[i], Random.Range(questionWidths[i].x, questionWidths[i].y), animate);

            timer.ResetTimer();
            Run();
        }

        void Reveal(int pick)
        {
            step = Step.Reveal;
            for (int i = 0; i < answers.Length; i++)
            {
                answers[i].SetState(i == correct ? DemoQuizAnswer.State.Correct
                    : i == pick ? DemoQuizAnswer.State.Wrong
                    : DemoQuizAnswer.State.Faded);
            }

            SetDot(question, pick == correct ? "Accent (Tint)" : "Negative");
            nextAt = Loop ? Time.unscaledTime + nextDelay : 0;
            info.text = Loop ? askText : waitText;
        }

        void SetDot(int index, string color)
        {
            if (index >= dots.Length || dots[index].fillStyler.color.stylerID == color)
                return;

            dots[index].fillStyler.color.stylerID = color;
            dots[index].UpdateStyler();
        }

        static void SetWidth(RectTransform rect, float width, bool animate)
        {
            if (!animate)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

            EvoTween.Create(rect.sizeDelta.x, width, animate ? BarDuration : 0)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(rect, static (r, w) => r.sizeDelta = new Vector2(w, r.sizeDelta.y), 1);
        }
    }
}
