using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Input Field (Multi Line) tab: a feedback card. The stars set the rating, and Send (or Enter, when it submits)
    /// thanks you for a moment, then clears the card. The Scrollbar option swaps the text area for its scrollbar variant and keeps the text.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFeedback : MonoBehaviour
    {
        [Tooltip("The plain text area, then its scrollbar variant.")]
        [SerializeField] private InputFieldEnhancer[] variants;
        [Tooltip("Each variant's input field, in the same order.")]
        [SerializeField] private TMP_InputField[] inputs;
        [SerializeField] private Interactive[] stars;
        [Tooltip("Each star's filled layer, in the same order.")]
        [SerializeField] private CanvasGroup[] starFills;

        [Header("Send")]
        [SerializeField] private Interactive send;
        [Tooltip("The Send button's accent layer; it shows once there is a rating.")]
        [SerializeField] private CanvasGroup sendReady;
        [SerializeField] private CanvasGroup body;
        [SerializeField] private CanvasGroup thanks;
        [SerializeField, Min(0f)] private float thanksDuration = 2.4f;

        const float StarDuration = 0.2f;
        const float FadeDuration = 0.25f;
        const float StarOffScale = 0.7f;

        int rating;
        int hovered = -1;
        int variant;
        bool[] starOn;
        float clearAt = -1;

        void Awake()
        {
            starOn = new bool[stars.Length];
            for (int i = 0; i < stars.Length; i++)
            {
                int index = i;
                stars[i].onPointerEnter.AddListener(() => Hover(index));
                stars[i].onPointerExit.AddListener(() => Hover(-1));
                stars[i].onClick.AddListener(() => Rate(index + 1));
            }

            foreach (InputFieldEnhancer field in variants)
                field.onSubmit.AddListener(OnSubmit);

            send.onClick.AddListener(Send);
            variant = variants[1].gameObject.activeSelf ? 1 : 0;
            Draw(false);
        }

        // The thanks shows for its time on the real-time clock, then the card clears
        void Update()
        {
            if (clearAt < 0 || Time.realtimeSinceStartup < clearAt)
                return;

            clearAt = -1;
            Clear(true);
        }

        void Hover(int index)
        {
            hovered = index;
            Draw(true);
        }

        // Clicking the current rating again clears it
        void Rate(int value)
        {
            rating = rating == value ? 0 : value;
            Draw(true);
        }

        void Draw(bool animate)
        {
            int shown = hovered >= 0 ? hovered + 1 : rating;
            for (int i = 0; i < stars.Length; i++)
            {
                bool on = i < shown;
                if (animate && on == starOn[i])
                    continue;

                starOn[i] = on;
                Fade(starFills[i], on ? 1 : 0, animate ? StarDuration : 0);
                Transform fill = starFills[i].transform;
                Vector3 scale = on ? Vector3.one : Vector3.one * StarOffScale;
                if (!animate)
                    fill.localScale = scale;

                // A zero-length move still replaces one that is running
                if (Application.isPlaying)
                {
                    EvoTween.Create(fill.localScale, scale, animate ? StarDuration : 0)
                        .WithEase(on ? Ease.OutBack : Ease.OutCubic)
                        .WithTimeMode(TimeMode.Unscaled)
                        .BindToScale(fill);
                }
            }

            Fade(sendReady, rating > 0 ? 1 : 0, animate ? FadeDuration : 0);
        }

        void OnSubmit(string _) => Send();

        void Send()
        {
            if (rating == 0 || !body.interactable)
                return;

            body.interactable = false;
            body.blocksRaycasts = false;
            Fade(body, 0, FadeDuration);
            Fade(thanks, 1, FadeDuration);
            clearAt = Time.realtimeSinceStartup + thanksDuration;
        }

        // Leaving the tab mid-thanks skips straight to the cleared card
        void OnDisable()
        {
            if (body.interactable)
                return;

            clearAt = -1;
            Clear(false);
        }

        void Clear(bool animate)
        {
            rating = 0;
            hovered = -1;
            Draw(false);
            inputs[variant].text = string.Empty;

            body.interactable = true;
            body.blocksRaycasts = true;
            Fade(body, 1, animate ? FadeDuration : 0);
            Fade(thanks, 0, animate ? FadeDuration : 0);
        }

        // The variant that shows takes over the text and the interactable state
        public void SetScrollbar(bool value)
        {
            int next = value ? 1 : 0;
            if (next == variant)
                return;

            TMP_InputField current = inputs[variant];
            inputs[next].text = current.text;
            variants[next].gameObject.SetActive(true);
            variants[next].SetInteractable(current.interactable);
            variants[variant].gameObject.SetActive(false);
            variant = next;
        }

        // A zero-length fade still replaces one that is running, so it can't undo an instant change
        static void Fade(CanvasGroup group, float alpha, float duration)
        {
            if (duration <= 0)
                group.alpha = alpha;

            if (Application.isPlaying)
                DemoUtils.Fade(group, alpha, duration);
        }
    }
}
