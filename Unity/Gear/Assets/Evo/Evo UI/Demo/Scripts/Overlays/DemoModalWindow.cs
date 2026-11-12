using System.Text;
using Evo.Tween;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Modal Window tab: a save slot whose Delete button asks first. Delete clears the slot into an empty one,
    /// which starts a new save when clicked (no question asked). Motion, close and focus settings are the page options.
    /// Slide is the window's own animation; Pop and Fade are sequences kept here.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoModalWindow : MonoBehaviour
    {
        const float FadeDuration = 0.25f;

        [SerializeField] private ModalWindow modal;
        [SerializeField] private string title = "Delete Slot 1?";
        [SerializeField] private int hours = 14;

        [Header("Slot")]
        [SerializeField] private CanvasGroup save;
        [SerializeField] private CanvasGroup empty;
        [SerializeField] private CanvasGroup deleteButton;

        [Header("Motion")]
        [SerializeField] private TweenSequence popOpen = new();
        [SerializeField] private TweenSequence popClose = new();
        [SerializeField] private TweenSequence fadeOpen = new();
        [SerializeField] private TweenSequence fadeClose = new();

        [Header("Options")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private CanvasGroup confirmOption;
        [SerializeField] private CanvasGroup cancelOption;

        TweenSequence slideOpen;
        TweenSequence slideClose;
        string answer;
        bool answered, answerStays, cleared;
        string description;
        int describedHours = -1;
        // Room for the longest line up front: a builder that grows into a second block allocates again on every Clear
        readonly StringBuilder infoText = new(128);

        void Awake()
        {
            slideOpen = modal.openAnimation;
            slideClose = modal.closeAnimation;
            UpdateLocks(false);
            ShowSlot(false);
        }

        void OnEnable() => UpdateInfo();

        // The Delete button's click
        public void Ask()
        {
            // Built again only when the hours change
            if (description == null || describedHours != hours)
            {
                describedHours = hours;
                description = hours > 0
                    ? "This removes " + hours + " hours of progress and can't be undone."
                    : "This removes the save and can't be undone.";
            }

            modal.Title = title;
            modal.Description = description;
            modal.Open();
        }

        // The empty slot's click
        public void NewSave()
        {
            if (!cleared)
                return;

            cleared = false;
            hours = 0;
            ShowSlot(true);
        }

        // Slide, Pop or Fade
        public void SetMotion(int index)
        {
            modal.openAnimation = index == 0 ? slideOpen : index == 1 ? popOpen : fadeOpen;
            modal.closeAnimation = index == 0 ? slideClose : index == 1 ? popClose : fadeClose;
        }

        // At least one button has to close the window, so each tile locks the other while it's off
        public void SetCloseOnConfirm(bool value)
        {
            modal.closeOnConfirm = value;
            UpdateLocks(true);
        }

        public void SetCloseOnCancel(bool value)
        {
            modal.closeOnCancel = value;
            UpdateLocks(true);
        }

        public void SetFocusLock(bool value)
        {
            modal.navigationMode = value ? ModalWindow.NavigationMode.Focused : ModalWindow.NavigationMode.Free;
            UpdateInfo();
        }

        public void OnConfirm()
        {
            if (!cleared)
            {
                cleared = true;
                ShowSlot(true);
            }

            SetAnswer(modal.confirmButton.Text, modal.closeOnConfirm);
        }

        public void OnCancel() => SetAnswer(modal.cancelButton.Text, modal.closeOnCancel);

        // Kept as the button text plus a flag, and joined into the info line in place
        void SetAnswer(string text, bool closes)
        {
            answer = text;
            answerStays = !closes;
            answered = !closes || text != null;
            UpdateInfo();
        }

        // The save and its Delete button make way for the empty slot, and back
        void ShowSlot(bool animate)
        {
            Fade(save, !cleared, animate);
            Fade(deleteButton, !cleared, animate);
            Fade(empty, cleared, animate);
        }

        static void Fade(CanvasGroup group, bool visible, bool animate)
        {
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (!animate)
            {
                group.alpha = visible ? 1 : 0;
                return;
            }

            DemoUtils.Fade(group, visible ? 1f : 0f, FadeDuration);
        }

        void UpdateLocks(bool animate)
        {
            DemoUtils.SetLocked(confirmOption, !modal.closeOnCancel, animate);
            DemoUtils.SetLocked(cancelOption, !modal.closeOnConfirm, animate);
        }

        void UpdateInfo()
        {
            if (answered)
            {
                infoText.Clear().Append("Last answer: ").Append(answer);
                if (answerStays)
                    infoText.Append(" (the window stays open)");

                info.SetText(infoText);
            }
            else if (modal.navigationMode == ModalWindow.NavigationMode.Focused) { info.SetText("Open it and use the arrow keys: focus can't leave the window."); }
            else { info.SetText("Delete the save: the window dims the stage and waits for an answer."); }
        }
    }
}
