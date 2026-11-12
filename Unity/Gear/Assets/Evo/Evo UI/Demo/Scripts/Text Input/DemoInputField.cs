using System.Text;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Input Field tabs: how the placeholders move, what Enter does in a multi-line field, the pointer effects,
    /// and the info line, which shows the last submitted text. Every option applies to all fields on the page.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoInputField : MonoBehaviour
    {
        static readonly string[] EnterHints =
        {
            "Enter starts a new line.",
            "Enter submits instead of starting a new line.",
            "Enter submits, Shift+Enter starts a new line."
        };

        [Tooltip("Every field on the page, hidden variants included.")]
        [SerializeField] private InputFieldEnhancer[] fields;
        [Tooltip("Each field's input field, in the same order.")]
        [SerializeField] private TMP_InputField[] inputs;
        [SerializeField] private TMP_Text info;
        [SerializeField] private string hint;
        [Tooltip("Where Float moves the placeholder: just above the field, lined up with its left edge.")]
        [SerializeField] private Vector2 floatOffset;

        [Header("Options")]
        [SerializeField] private CanvasGroup selectOnFocusOption;
        [SerializeField] private CanvasGroup[] submitOptions;

        bool multiLine;
        bool selectOnFocus;
        int enterKey;

        // Joined once per Enter choice, so opening the tab doesn't build the hint again
        readonly string[] joinedHints = new string[EnterHints.Length];
        readonly StringBuilder submitted = new();

        void Awake()
        {
            foreach (InputFieldEnhancer field in fields)
                field.onSubmit.AddListener(ShowSubmitted);

            multiLine = inputs[0].lineType != TMP_InputField.LineType.SingleLine;
            enterKey = fields[0].handleShiftEnter ? 2 : inputs[0].lineType == TMP_InputField.LineType.MultiLineSubmit ? 1 : 0;
            selectOnFocus = inputs[0].onFocusSelectAll;
            LockEnterOptions(false);
        }

        void OnEnable() => info.text = Hint();

        string Hint()
        {
            if (!multiLine) return hint;
            return string.IsNullOrEmpty(hint) ? EnterHints[enterKey] : (joinedHints[enterKey] ??= hint + " " + EnterHints[enterKey]);
        }

        void ShowSubmitted(string value)
        {
            if (value.Length == 0)
            {
                info.text = "Submitted an empty field";
                return;
            }

            // The first line only, built in place
            int lineEnd = value.IndexOf('\n');
            submitted.Clear().Append("Submitted “");
            if (lineEnd < 0) { submitted.Append(value).Append('”'); }
            else { submitted.Append(value, 0, lineEnd).Append("…”"); }

            info.SetText(submitted);
        }

        // Placeholder: Fade hides it while the field is in use, Float lifts it above the field as a label
        public void SetPlaceholder(int index)
        {
            bool fade = index == 0;
            foreach (InputFieldEnhancer field in fields)
            {
                field.slideOffset = fade ? Vector2.zero : floatOffset;
                field.scaleMultiplier = fade ? 1 : 0.8f;
                field.fadeAlpha = fade ? 0 : 1;
                Reapply(field);
            }
        }

        public void SetDuration(float value)
        {
            foreach (InputFieldEnhancer field in fields)
                field.animationDuration = value;
        }

        // Shift+Enter keeps select-all off while it is on, so the choice waits until it is off again
        public void SetSelectOnFocus(bool value)
        {
            selectOnFocus = value;
            if (enterKey == 2)
                return;

            foreach (TMP_InputField input in inputs)
                input.onFocusSelectAll = value;
        }

        public void SetClearOnEnter(bool value)
        {
            foreach (InputFieldEnhancer field in fields)
                field.clearAfterSubmit = value;
        }

        public void SetDeselectOnEnter(bool value)
        {
            foreach (InputFieldEnhancer field in fields)
                field.deselectOnEndEdit = value;
        }

        // Multi-line fields: New line, Submit, or Shift+Enter (Enter submits, Shift+Enter breaks the line)
        public void SetEnterKey(int index)
        {
            enterKey = index;

            for (int i = 0; i < fields.Length; i++)
            {
                // The enhancer sets up Shift+Enter when it is enabled
                fields[i].enabled = false;
                fields[i].handleShiftEnter = index == 2;
                inputs[i].lineType = index == 1 ? TMP_InputField.LineType.MultiLineSubmit : TMP_InputField.LineType.MultiLineNewline;
                fields[i].enabled = true;
                if (index != 2)
                    inputs[i].onFocusSelectAll = selectOnFocus;
            }

            LockEnterOptions(true);
            info.text = Hint();
        }

        // Effects
        public void SetSpotlight(bool value)
        {
            foreach (InputFieldEnhancer field in fields)
                DemoUtils.SetEffect(field.interactableObject.spotlight, value);
        }

        public void SetBorderGlow(bool value)
        {
            foreach (InputFieldEnhancer field in fields)
                DemoUtils.SetEffect(field.interactableObject.borderGlow, value);
        }

        public void SetInteractable(bool value)
        {
            foreach (InputFieldEnhancer field in fields)
                field.SetInteractable(value);
        }

        // Without a submit there is nothing to clear or deselect on, and Shift+Enter turns Select on focus off while it is on
        void LockEnterOptions(bool animate)
        {
            if (!multiLine)
                return;

            DemoUtils.SetLocked(submitOptions, enterKey == 0, animate);
            DemoUtils.SetLocked(selectOnFocusOption, enterKey == 2, animate);
        }

        // The enhancer reads its placeholder targets when it starts a move, so a field already in use gets the new ones by re-enabling it
        static void Reapply(InputFieldEnhancer field)
        {
            if (!field.isActiveAndEnabled)
                return;

            field.enabled = false;
            field.enabled = true;
        }
    }
}
