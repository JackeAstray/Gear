using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Progress Bar (Horizontal) tab: a transfer adds a random step every interval, and each step eases in with the bar's own smoothing.
    /// When it's full, the done text shows for a moment and it starts over. Options switch smoothing, invert fill and the number format.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoProgressBar : MonoBehaviour
    {
        [SerializeField] private ProgressBar bar;
        [SerializeField] private float size = 4.2f;
        [SerializeField] private float interval = 0.5f;
        [SerializeField] private Vector2 step = new(2, 6);
        [SerializeField] private float restartDelay = 3.5f;
        [SerializeField] private bool showSpeed = true;

        [Header("Text")]
        [SerializeField] private TMP_Text status;
        [SerializeField] private string doneStatus = "Done";

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Sprite pauseIcon;
        [SerializeField] private Sprite resumeIcon;
        [SerializeField] private Sprite restartIcon;

        [Header("Options")]
        [SerializeField] private CanvasGroup decimalsOption;

        bool running;
        float elapsed;
        int speed;
        int textMode;
        int decimals;
        int shownButtons = -1;

        bool Done => bar.Value >= bar.MaxValue;

        void OnEnable() => Restart(true);

        void Update()
        {
            if (!running)
                return;

            elapsed += Time.unscaledDeltaTime;
            if (Done)
            {
                if (elapsed >= restartDelay)
                    Restart(false);

                return;
            }

            if (elapsed < interval)
                return;

            elapsed = 0;
            speed = Random.Range(12, 27);
            bar.Value = Mathf.Min(bar.MaxValue, bar.Value + Random.Range(step.x, step.y));
            UpdateLabels();
        }

        void Restart(bool instant)
        {
            running = true;
            elapsed = 0;

            if (instant) { bar.SetValueInstant(bar.MinValue); }
            else { bar.Value = bar.MinValue; }

            UpdateLabels();
        }

        // Pause, Resume, or start again once it's done
        public void TogglePause()
        {
            if (Done) { Restart(false); }
            else { running = !running; }

            UpdateLabels();
        }

        public void Cancel()
        {
            running = false;
            bar.Value = bar.MinValue;
            UpdateLabels();
        }

        void UpdateLabels()
        {
            float done = (bar.Value - bar.MinValue) / (bar.MaxValue - bar.MinValue) * size;

            if (Done) { status.SetText(doneStatus); }
            else if (!running) { status.SetText("{0:1} of {1:1} GB - Paused", done, size); }
            else if (showSpeed) { status.SetText("{0:1} of {1:1} GB - {2} MB/s", done, size, speed); }
            else { status.SetText("{0:1} of {1:1} GB", done, size); }

            // Running, paused or done: the buttons only change with the state
            int buttons = Done ? 2 : running ? 0 : 1;
            if (pauseButton == null || buttons == shownButtons)
                return;

            shownButtons = buttons;
            pauseButton.Icon = buttons == 2 ? restartIcon : buttons == 0 ? pauseIcon : resumeIcon;
            pauseButton.Text = buttons == 2 ? "Again" : buttons == 0 ? "Pause" : "Resume";
            cancelButton.gameObject.SetActive(buttons != 2);
        }

        public void SetSmoothing(bool value) => bar.enableSmoothing = value;

        public void SetInvertFill(bool value)
        {
            bar.invertFill = value;
            Redraw();
        }

        // Percent, Size or None
        public void SetTextMode(int index)
        {
            textMode = index;
            DemoUtils.SetLocked(decimalsOption, index == 2);
            UpdateFormat();
        }

        public void SetDecimals(int index)
        {
            decimals = index;
            UpdateFormat();
        }

        void UpdateFormat()
        {
            bool inSize = textMode == 1;
            bar.valueText.gameObject.SetActive(textMode != 2);
            bar.textFormat = inSize ? "{0} GB" : "{0}%";
            bar.displayMultiplier = inSize ? size / (bar.MaxValue - bar.MinValue) : 1;

            // Sizes keep at least one decimal
            bar.displayFormat = (NumberDisplayFormat)(inSize ? Mathf.Max(1, decimals) : decimals);
            Redraw();
        }

        // The bar applies these settings on its next value change, so set the value again to show them now
        void Redraw() => bar.SetValueInstant(bar.Value);
    }
}
