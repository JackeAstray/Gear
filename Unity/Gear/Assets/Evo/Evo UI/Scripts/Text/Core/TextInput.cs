using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Receives events from an input field of any text provider.
    /// Keep one per owner and bind it to the field, so it can always be removed from the field it was added to.
    /// </summary>
    public sealed class TextInputListener
    {
        /// <summary>
        /// The value changed through editing or a notifying set.
        /// </summary>
        public Action<string> onValueChanged;

        /// <summary>
        /// Editing ended, with the final value.
        /// </summary>
        public Action<string> onEndEdit;

        public Action<string> onSubmit;
        public Action onSelect;
        public Action onDeselect;

        /// <summary>
        /// The displayed text may have changed, including sets that do not notify.
        /// </summary>
        public Action onTextUpdated;

        TextBackend backend;
        object subscription;

        /// <summary>
        /// Field this listener is bound to, or null.
        /// </summary>
        public MonoBehaviour Input { get; private set; }

        /// <summary>
        /// Moves the listener to the field. Null or an unsupported field only unbinds it.
        /// </summary>
        public void Bind(MonoBehaviour input)
        {
            if (Input == input && backend != null)
                return;

            Unbind();

            TextBackend inputBackend = TextProvider.FindInput(input);
            if (inputBackend == null)
                return;

            backend = inputBackend;
            Input = input;
            subscription = inputBackend.AddListener(input, this);
        }

        public void Unbind()
        {
            // A destroyed field still holds its events, so removal does not depend on Unity's null check
            backend?.RemoveListener(Input, subscription);
            backend = null;
            subscription = null;
            Input = null;
        }

        #region Raise
        public void RaiseValueChanged(string value) => onValueChanged?.Invoke(value);
        public void RaiseEndEdit(string value) => onEndEdit?.Invoke(value);
        public void RaiseSubmit(string value) => onSubmit?.Invoke(value);
        public void RaiseSelect() => onSelect?.Invoke();
        public void RaiseDeselect() => onDeselect?.Invoke();
        public void RaiseTextUpdated() => onTextUpdated?.Invoke();
        #endregion
    }

    /// <summary>
    /// Input field settings carried across text providers. TMP types are the shared vocabulary.
    /// </summary>
    public sealed class TextInputSettings
    {
        public string value = string.Empty;
        public Graphic placeholder;
        public int characterLimit;
        public TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard;
        public TMP_InputField.LineType lineType = TMP_InputField.LineType.SingleLine;
        public bool readOnly;
        public bool interactable = true;
        public bool enabled = true;
        public bool selectAllOnFocus = true;
        public bool restoreOnEscape = true;
        public Color selectionColor = new(0.65882355f, 0.80784315f, 1f, 0.7529412f);
    }
}