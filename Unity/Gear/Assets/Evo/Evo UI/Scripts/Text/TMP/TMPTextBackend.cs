using System;
using System.Collections.Generic;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Evo.UI
{
    /// <summary>
    /// TextMesh Pro backend. Built in and selected by default.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed class TMPTextBackend : TextBackend
    {
        public const string ID = "TMP";
        public static readonly TMPTextBackend Instance = new();

        // TMP restores the base size from m_fontSizeBase, so both must match
        static readonly string[] FontSizePaths = { "m_fontSize", "m_fontSizeBase" };

        TMPTextBackend() { }

        public override string Id => ID;
        public override string DisplayName => "TextMesh Pro";
        public override Type TextType => typeof(TMP_Text);
        public override Type ComponentType => typeof(TextMeshProUGUI);
        public override Type FontType => typeof(TMP_FontAsset);
        public override Object DefaultFont => TMP_Settings.GetFontAsset(); // Null-safe while Essential Resources are missing
        public override string TextProperty => "m_text";
        public override IReadOnlyList<string> FontSizeProperties => FontSizePaths;

        public override bool IsText(Graphic graphic) => graphic is TMP_Text;
        public override bool IsFont(Object font) => font is TMP_FontAsset;

        // Sub meshes are unsaved children that would outlive their text
        public override void Cleanup(Graphic text)
        {
            foreach (TMP_SubMeshUI subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
            {
                if (subMesh != null && subMesh.transform.parent == text.transform)
                    Object.DestroyImmediate(subMesh.gameObject);
            }
        }

        #region Content
        public override string GetText(Graphic text) => ((TMP_Text)text).text;
        public override void SetText(Graphic text, string value) => ((TMP_Text)text).text = value;
        public override void SetText(Graphic text, char[] buffer, int start, int length) => ((TMP_Text)text).SetText(buffer, start, length);
        public override void SetText(Graphic text, StringBuilder buffer) => ((TMP_Text)text).SetText(buffer);
        #endregion

        #region Font
        public override Object GetFont(Graphic text) => ((TMP_Text)text).font;

        public override void SetFont(Graphic text, Object font)
        {
            if (font is TMP_FontAsset asset)
                ((TMP_Text)text).font = asset;
        }

        public override float GetFontSize(Graphic text) => ((TMP_Text)text).fontSize;
     
        public override void SetFontSize(Graphic text, float size) => ((TMP_Text)text).fontSize = size;

        public override void SetAutoSize(Graphic text, bool enabled, float min, float max,
            float characterWidthAdjustment = 0f, float lineSpacingAdjustment = 0f)
        {
            var tmp = (TMP_Text)text;
            tmp.enableAutoSizing = enabled;

            if (!enabled)
                return;

            tmp.fontSizeMin = min;
            tmp.fontSizeMax = max;

            if (tmp.characterWidthAdjustment != characterWidthAdjustment)
                tmp.characterWidthAdjustment = characterWidthAdjustment;
            if (tmp.lineSpacingAdjustment != lineSpacingAdjustment)
                tmp.lineSpacingAdjustment = lineSpacingAdjustment;
        }

        public override void SetFontStyle(Graphic text, FontStyles style) => ((TMP_Text)text).fontStyle = style;
        #endregion

        #region Layout
        public override TextAlignmentOptions GetAlignment(Graphic text) => ((TMP_Text)text).alignment;
       
        public override void SetAlignment(Graphic text, TextAlignmentOptions alignment) => ((TMP_Text)text).alignment = alignment;

        public override void SetWordWrap(Graphic text, bool enabled)
        {
            ((TMP_Text)text).textWrappingMode = enabled ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        public override void SetOverflow(Graphic text, TextOverflowModes mode) => ((TMP_Text)text).overflowMode = mode;

        public override void SetSpacing(Graphic text, float character, float word, float line, float paragraph)
        {
            var tmp = (TMP_Text)text;
            if (tmp.characterSpacing != character)
                tmp.characterSpacing = character;
            if (tmp.wordSpacing != word)
                tmp.wordSpacing = word;
            if (tmp.lineSpacing != line)
                tmp.lineSpacing = line;
            if (tmp.paragraphSpacing != paragraph)
                tmp.paragraphSpacing = paragraph;
        }

        public override void SetMargin(Graphic text, Vector4 margin)
        {
            var tmp = (TMP_Text)text;
            if (tmp.margin != margin)
                tmp.margin = margin;
        }

        public override Vector2 GetPreferredValues(Graphic text, string value, float width, float height)
            => ((TMP_Text)text).GetPreferredValues(value, width, height);
        #endregion

        #region Input
        public override Type InputType => typeof(TMP_InputField);

        public override Graphic GetTextComponent(MonoBehaviour input) => ((TMP_InputField)input).textComponent;
        public override Graphic GetPlaceholder(MonoBehaviour input) => ((TMP_InputField)input).placeholder;

        public override void SetPlaceholder(MonoBehaviour input, Graphic placeholder)
        {
            var field = (TMP_InputField)input;
            Graphic previous = field.placeholder;
            field.placeholder = placeholder;

            // TMP only toggles its placeholder on later changes
            if (placeholder != null) { placeholder.enabled = string.IsNullOrEmpty(field.text); }
            else if (previous != null) { previous.enabled = true; }
        }

        public override string GetValue(MonoBehaviour input) => ((TMP_InputField)input).text;

        public override bool ValueEquals(MonoBehaviour input, ReadOnlySpan<char> value) => ((TMP_InputField)input).text.AsSpan().SequenceEqual(value);

        public override void SetValue(MonoBehaviour input, string value, bool notify)
        {
            var field = (TMP_InputField)input;

#if UNITY_EDITOR
            // Outside play mode the serialized value is written, so the field keeps it
            if (!Application.isPlaying)
            {
                using (var serializedField = new UnityEditor.SerializedObject(field))
                {
                    serializedField.FindProperty("m_Text").stringValue = value;
                    serializedField.ApplyModifiedPropertiesWithoutUndo();
                }

                field.ForceLabelUpdate();
                return;
            }
#endif

            if (notify) { field.text = value; }
            else { field.SetTextWithoutNotify(value); }
        }

        public override void SetCharacterLimit(MonoBehaviour input, int limit)
        {
            var field = (TMP_InputField)input;
            if (field.characterLimit != limit)
                field.characterLimit = limit;
        }

        public override bool IsFocused(MonoBehaviour input) => ((TMP_InputField)input).isFocused;

        public override void Focus(MonoBehaviour input)
        {
            var field = (TMP_InputField)input;
            field.Select();
            field.ActivateInputField();
        }

        public override bool IsInteractable(MonoBehaviour input) => ((TMP_InputField)input).IsInteractable();

        public override void SetInteractable(MonoBehaviour input, bool interactable)
        {
            var field = (TMP_InputField)input;
            if (field.interactable != interactable)
                field.interactable = interactable;
        }

        public override void RaiseEndEdit(MonoBehaviour input)
        {
            var field = (TMP_InputField)input;
            field.onEndEdit.Invoke(field.text);
        }

        public override object AddListener(MonoBehaviour input, TextInputListener listener) => new InputSubscription((TMP_InputField)input, listener);

        public override void RemoveListener(MonoBehaviour input, object subscription) => ((InputSubscription)subscription).Remove();

        /// <summary>
        /// TMP fields hold a viewport that holds the text. A viewport that draws the field's own background,
        /// as UniText fields are built, hosts the field itself. Another Selectable on the host would compete for focus.
        /// </summary>
        public override GameObject GetInputHost(Graphic text)
        {
            Transform viewport = text.rectTransform.parent;
            if (viewport == null || viewport.TryGetComponent(out Canvas _))
                return null;

            GameObject host = viewport.parent == null || viewport.TryGetComponent(out Graphic _) ? viewport.gameObject : viewport.parent.gameObject;
            return host.TryGetComponent(out Selectable _) || host.TryGetComponent(out Canvas _) ? null : host;
        }

        public override void CaptureInput(MonoBehaviour input, TextInputSettings target)
        {
            var field = (TMP_InputField)input;

            target.value = field.text;
            target.placeholder = field.placeholder;
            target.characterLimit = field.characterLimit;
            target.contentType = field.contentType != TMP_InputField.ContentType.Custom ? field.contentType : field.characterValidation switch
            {
                TMP_InputField.CharacterValidation.Digit or TMP_InputField.CharacterValidation.Integer => TMP_InputField.ContentType.IntegerNumber,
                TMP_InputField.CharacterValidation.Decimal => TMP_InputField.ContentType.DecimalNumber,
                TMP_InputField.CharacterValidation.Alphanumeric => TMP_InputField.ContentType.Alphanumeric,
                TMP_InputField.CharacterValidation.Name => TMP_InputField.ContentType.Name,
                TMP_InputField.CharacterValidation.EmailAddress => TMP_InputField.ContentType.EmailAddress,
                _ => TMP_InputField.ContentType.Standard
            };
            target.lineType = field.lineType;
            target.readOnly = field.readOnly;
            target.interactable = field.interactable;
            target.enabled = field.enabled;
            target.selectAllOnFocus = field.onFocusSelectAll;
            target.restoreOnEscape = field.restoreOriginalTextOnEscape;
            target.selectionColor = field.selectionColor;
        }

        public override void ApplyInput(MonoBehaviour input, Graphic text, TextInputSettings settings)
        {
            var field = (TMP_InputField)input;

            field.textViewport = text.rectTransform.parent as RectTransform;
            field.textComponent = (TMP_Text)text;
            field.placeholder = settings.placeholder;
            field.transition = Selectable.Transition.None;
            if (field.targetGraphic == null && field.TryGetComponent(out Graphic graphic))
                field.targetGraphic = graphic;

            // The content type may force single-line, so it is set after the line type
            field.lineType = settings.lineType;
            field.contentType = settings.contentType;
            field.characterLimit = settings.characterLimit;
            field.readOnly = settings.readOnly;
            field.interactable = settings.interactable;
            field.onFocusSelectAll = settings.selectAllOnFocus;
            field.restoreOriginalTextOnEscape = settings.restoreOnEscape;
            field.selectionColor = settings.selectionColor;
            field.text = settings.value ?? string.Empty;
            field.enabled = settings.enabled;
        }

        /// <summary>
        /// Forwards a field's events. Engines remove listeners by target and method, so the same handlers are found again.
        /// </summary>
        sealed class InputSubscription
        {
            readonly TMP_InputField field;
            readonly TextInputListener listener;
            readonly Graphic text;
            readonly bool watchesText;

            public InputSubscription(TMP_InputField field, TextInputListener listener)
            {
                this.field = field;
                this.listener = listener;
                text = field.textComponent;

                field.onValueChanged.AddListener(listener.RaiseValueChanged);
                field.onEndEdit.AddListener(listener.RaiseEndEdit);
                field.onSubmit.AddListener(listener.RaiseSubmit);
                field.onSelect.AddListener(OnSelect);
                field.onDeselect.AddListener(OnDeselect);

                // TMP dirties its text when the value is set without notification
                if (listener.onTextUpdated != null && text != null)
                {
                    text.RegisterDirtyVerticesCallback(listener.RaiseTextUpdated);
                    watchesText = true;
                }
            }

            public void Remove()
            {
                field.onValueChanged.RemoveListener(listener.RaiseValueChanged);
                field.onEndEdit.RemoveListener(listener.RaiseEndEdit);
                field.onSubmit.RemoveListener(listener.RaiseSubmit);
                field.onSelect.RemoveListener(OnSelect);
                field.onDeselect.RemoveListener(OnDeselect);

                if (watchesText && text != null)
                    text.UnregisterDirtyVerticesCallback(listener.RaiseTextUpdated);
            }

            void OnSelect(string _) => listener.RaiseSelect();
            void OnDeselect(string _) => listener.RaiseDeselect();
        }
        #endregion

        #region Styles
        public override void CaptureTextStyle(Graphic text, Styler.TextStyleItem target)
        {
            var tmp = (TMP_Text)text;

            target.fontSize = tmp.fontSize;
            target.enableAutoSizing = tmp.enableAutoSizing;
            target.fontSizeMin = tmp.fontSizeMin;
            target.fontSizeMax = tmp.fontSizeMax;
            target.characterWidthAdjustment = tmp.characterWidthAdjustment;
            target.lineSpacingAdjustment = tmp.lineSpacingAdjustment;
            target.alignment = tmp.alignment;
            target.enableWordWrapping = tmp.textWrappingMode == TextWrappingModes.Normal || tmp.textWrappingMode == TextWrappingModes.PreserveWhitespace;
            target.overflowMode = tmp.overflowMode;
            target.characterSpacing = tmp.characterSpacing;
            target.wordSpacing = tmp.wordSpacing;
            target.lineSpacing = tmp.lineSpacing;
            target.paragraphSpacing = tmp.paragraphSpacing;
            target.margin = tmp.margin;
            target.fontStyle = tmp.fontStyle;
        }
        #endregion
    }
}