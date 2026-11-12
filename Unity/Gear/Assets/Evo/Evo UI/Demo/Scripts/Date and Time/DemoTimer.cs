using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Timer tabs: a small story around the timer (a lot that sells, a quiz question, an episode queue) and the options:
    /// format, duration and playback, plus the digit options of Timer and the bar options of the others.
    /// The timer's settings are plain fields, so each option has a setter here, and changes show at once even while paused.
    /// Duration starts the timer over, so it starts the story over. Loop here means the story goes on by itself;
    /// the timer's own loop stays off so its completion reaches the story.
    /// </summary>
    public abstract class DemoTimer : MonoBehaviour
    {
        [SerializeField] protected Timer timer;
        [SerializeField] private Timer.DisplayFormat[] formats;
        [SerializeField] private bool loop = true;

        [Header("Options")]
        [SerializeField] private Switch runningOption;
        [SerializeField] private CanvasGroup rollTimeOption;
        [SerializeField] private CanvasGroup smoothingTimeOption;

        protected bool Running => runningOption == null || runningOption.IsOn;
        protected bool Loop => loop;

        protected virtual void Awake()
        {
            timer.onTimerComplete.AddListener(OnTimerComplete);
            DemoUtils.SetLocked(rollTimeOption, !timer.enableRolling, false);
            DemoUtils.SetLocked(smoothingTimeOption, !timer.enableSmoothing, false);
        }

        // The tab opens on a fresh start
        protected virtual void OnEnable() => Restart();

        /// <summary>
        /// Starts the story over after an option changed it. The timer runs only if Running is on.
        /// </summary>
        public abstract void Restart();

        protected abstract void OnTimerComplete();

        public void SetFormat(int index)
        {
            timer.displayFormat = formats[index];
            Redraw();
        }

        public void SetDuration(float value)
        {
            timer.duration = value;
            Restart();
        }

        public void SetSpeed(float value) => timer.timeMultiplier = value;

        public virtual void SetRunning(bool value)
        {
            if (value) { timer.ResumeTimer(); }
            else { timer.PauseTimer(); }
        }

        public virtual void SetLoop(bool value) => loop = value;

        public void SetRolling(bool value)
        {
            timer.enableRolling = value;
            DemoUtils.SetLocked(rollTimeOption, !value);
        }

        public void SetRollTime(float value) => timer.animationDuration = value;

        public void SetSmoothing(bool value)
        {
            timer.enableSmoothing = value;
            DemoUtils.SetLocked(smoothingTimeOption, !value);
        }

        public void SetSmoothingTime(float value) => timer.smoothingDuration = value;

        public void SetWholeSeconds(bool value)
        {
            timer.updateBarOnSecondsOnly = value;
            Redraw();
        }

        public void SetInvertFill(bool value)
        {
            timer.invertFill = value;
            Redraw();
        }

        // A step the visitor takes (a new lot, a new episode) starts the timer, so the switch follows
        protected void ShowRunning(bool value) => DemoUtils.Show(runningOption, value);

        // StartTimer leaves a running timer alone and resumes a paused one
        protected void Run()
        {
            if (Running)
                timer.StartTimer();
        }

        // A paused timer doesn't tick, so show the change now
        void Redraw() => timer.SetCurrentTimeWithoutNotify(timer.currentTime);
    }
}
