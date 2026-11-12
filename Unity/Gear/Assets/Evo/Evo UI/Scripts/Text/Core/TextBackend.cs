using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Evo.UI
{
    /// <summary>
    /// Bridges Evo UI to a text engine such as TextMesh Pro or UniText.
    /// Members receive text components owned by this backend (see <see cref="IsText"/>).
    /// TMP enums are the shared vocabulary for alignment, overflow and font styles.
    /// </summary>
    public abstract class TextBackend
    {
        /// <summary>
        /// Stable identifier saved in project settings. Never change it once shipped.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Name shown in Project Settings and editor tools.
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Base text component type. Used for detection and inspector filtering.
        /// </summary>
        public abstract Type TextType { get; }

        /// <summary>
        /// Canvas text component added when Evo UI creates text.
        /// </summary>
        public abstract Type ComponentType { get; }

        /// <summary>
        /// Font asset type used by Styler and font fields.
        /// </summary>
        public abstract Type FontType { get; }

        /// <summary>
        /// Font used when nothing else resolves. Null lets the engine decide.
        /// </summary>
        public virtual Object DefaultFont => null;

        /// <summary>
        /// Serialized property path of the text, for editors that edit it through SerializedObject.
        /// </summary>
        public abstract string TextProperty { get; }

        /// <summary>
        /// Serialized property paths of the font size. The first holds the value; the rest are kept equal to it.
        /// </summary>
        public abstract IReadOnlyList<string> FontSizeProperties { get; }

        public abstract bool IsText(Graphic graphic);
       
        public abstract bool IsFont(Object font);

        /// <summary>
        /// Destroys helper objects the engine created for the text.
        /// Called before the component is removed when converting to another provider.
        /// </summary>
        public virtual void Cleanup(Graphic text) { }

        /// <summary>
        /// Creates a display-only copy of the text under the parent, without the input components some engines keep on text objects.
        /// </summary>
        public Graphic Duplicate(Graphic text, Transform parent)
        {
            // Copied components stay asleep until the input parts are stripped
            var holder = new GameObject { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);

            try
            {
                Graphic copy = Object.Instantiate(text, holder.transform, false);
                StripInput(copy.gameObject);
                copy.rectTransform.SetParent(parent, false);
                return copy;
            }
            finally
            {
                // A hidden root survives scene loads, so release it even if copying fails
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// Removes input components from a copied text object before it wakes up.
        /// </summary>
        protected virtual void StripInput(GameObject copy) { }

        #region Content
        public abstract string GetText(Graphic text);
     
        public abstract void SetText(Graphic text, string value);

        /// <summary>
        /// Sets text from a character buffer without allocating a string.
        /// The buffer must stay owned by the caller, as some engines keep a reference to it.
        /// </summary>
        public abstract void SetText(Graphic text, char[] buffer, int start, int length);

        /// <summary>
        /// Sets text from a builder without allocating a string. The builder can be reused afterwards.
        /// </summary>
        public abstract void SetText(Graphic text, StringBuilder buffer);
        #endregion

        #region Font
        public abstract Object GetFont(Graphic text);

        /// <summary>
        /// Assigns the font when this backend supports its type. Other fonts are ignored.
        /// </summary>
        public abstract void SetFont(Graphic text, Object font);

        public abstract float GetFontSize(Graphic text);
      
        public abstract void SetFontSize(Graphic text, float size);

        /// <summary>
        /// Width and line adjustments are TMP auto-size options and may be ignored by other backends.
        /// </summary>
        public abstract void SetAutoSize(Graphic text, bool enabled, float min, float max,
            float characterWidthAdjustment = 0f, float lineSpacingAdjustment = 0f);

        public abstract void SetFontStyle(Graphic text, FontStyles style);

        /// <summary>
        /// Gives every digit the same advance through the font's tabular figures, where the engine supports them.
        /// TextMesh Pro spaces digits with an mspace tag instead (see <see cref="Utilities.DigitSpacingCache"/>).
        /// </summary>
        public virtual void SetTabularDigits(Graphic text, bool enabled) { }
        #endregion

        #region Layout
        public abstract TextAlignmentOptions GetAlignment(Graphic text);
      
        public abstract void SetAlignment(Graphic text, TextAlignmentOptions alignment);
       
        public abstract void SetWordWrap(Graphic text, bool enabled);
     
        public abstract void SetOverflow(Graphic text, TextOverflowModes mode);

        /// <summary>
        /// Spacing values use TMP units, which are hundredths of an em.
        /// </summary>
        public abstract void SetSpacing(Graphic text, float character, float word, float line, float paragraph);

        /// <summary>
        /// Margin uses TMP order: left, top, right, bottom.
        /// </summary>
        public abstract void SetMargin(Graphic text, Vector4 margin);

        /// <summary>
        /// Measures the value as it would render in the text component.
        /// </summary>
        public abstract Vector2 GetPreferredValues(Graphic text, string value, float width, float height);
        #endregion

        #region Animation
        /// <summary>
        /// Creates an animator that moves glyphs without rebuilding the layout, or null when the engine has none.
        /// TextMesh Pro text is animated by editing its mesh directly, so it returns null.
        /// </summary>
        public virtual TextGlyphAnimator CreateGlyphAnimator(Graphic text) => null;
        #endregion

        #region Input
        /// <summary>
        /// Input field component that edits this backend's text, or null when the engine has none.
        /// Members below receive input fields of this type (see <see cref="IsInput"/>).
        /// </summary>
        public abstract Type InputType { get; }

        public bool IsInput(Object input) => input != null && InputType != null && InputType.IsInstanceOfType(input);

        /// <summary>
        /// Text component the input field edits.
        /// </summary>
        public abstract Graphic GetTextComponent(MonoBehaviour input);

        public abstract Graphic GetPlaceholder(MonoBehaviour input);

        /// <summary>
        /// Assigns the text the field shows while empty. Null detaches the current one and leaves it visible.
        /// </summary>
        public abstract void SetPlaceholder(MonoBehaviour input, Graphic placeholder);

        /// <summary>
        /// Returns the field's value. Some engines build the string on demand.
        /// </summary>
        public abstract string GetValue(MonoBehaviour input);

        /// <summary>
        /// Compares the field's value without allocating.
        /// </summary>
        public abstract bool ValueEquals(MonoBehaviour input, ReadOnlySpan<char> value);

        /// <summary>
        /// Sets the field's value. Without notify, <see cref="TextInputListener.onValueChanged"/> is not raised.
        /// Outside play mode the value is saved with the field.
        /// </summary>
        public abstract void SetValue(MonoBehaviour input, string value, bool notify);

        /// <summary>
        /// Limits the value length in UTF-16 units. Zero removes the limit.
        /// </summary>
        public abstract void SetCharacterLimit(MonoBehaviour input, int limit);

        public abstract bool IsFocused(MonoBehaviour input);

        /// <summary>
        /// Selects the field in the EventSystem and starts editing.
        /// </summary>
        public abstract void Focus(MonoBehaviour input);

        public abstract bool IsInteractable(MonoBehaviour input);
       
        public abstract void SetInteractable(MonoBehaviour input, bool interactable);

        /// <summary>
        /// Raises the field's end-edit event with its current value, for a value committed from code.
        /// Engines whose event cannot be raised from outside ignore it.
        /// </summary>
        public virtual void RaiseEndEdit(MonoBehaviour input) { }

        /// <summary>
        /// Forwards the field's events to the listener. Returns a subscription token passed back to <see cref="RemoveListener"/>.
        /// </summary>
        public abstract object AddListener(MonoBehaviour input, TextInputListener listener);
    
        public abstract void RemoveListener(MonoBehaviour input, object subscription);

        /// <summary>
        /// Object the input component belongs on for the text, or null when the text cannot host a field.
        /// Used when converting fields between providers.
        /// </summary>
        public abstract GameObject GetInputHost(Graphic text);

        /// <summary>
        /// Adds the components that make up the field, in the order they can be destroyed.
        /// </summary>
        public virtual void CollectInputComponents(MonoBehaviour input, List<Component> results) => results.Add(input);

        /// <summary>
        /// Reads the field's settings. Used to carry them across backends.
        /// </summary>
        public abstract void CaptureInput(MonoBehaviour input, TextInputSettings target);

        /// <summary>
        /// Configures a field added to <see cref="GetInputHost"/> for the text.
        /// </summary>
        public abstract void ApplyInput(MonoBehaviour input, Graphic text, TextInputSettings settings);
        #endregion

        #region Styles
        /// <summary>
        /// Applies the feature groups enabled on the style.
        /// </summary>
        public void ApplyTextStyle(Graphic text, Styler.TextStyleItem style)
        {
            if (style == null)
                return;

            Styler.TextStyleFlags flags = style.flags;

            if ((flags & Styler.TextStyleFlags.Size) != 0)
            {
                SetAutoSize(text, style.enableAutoSizing, style.fontSizeMin, style.fontSizeMax,
                    style.characterWidthAdjustment, style.lineSpacingAdjustment);

                if (!style.enableAutoSizing)
                    SetFontSize(text, style.fontSize);
            }

            if ((flags & Styler.TextStyleFlags.Alignment) != 0)
                SetAlignment(text, style.alignment);

            if ((flags & Styler.TextStyleFlags.WrappingAndOverflow) != 0)
            {
                SetWordWrap(text, style.enableWordWrapping);
                SetOverflow(text, style.overflowMode);
            }

            if ((flags & Styler.TextStyleFlags.Spacing) != 0)
                SetSpacing(text, style.characterSpacing, style.wordSpacing, style.lineSpacing, style.paragraphSpacing);

            if ((flags & Styler.TextStyleFlags.Margin) != 0)
                SetMargin(text, style.margin);

            if ((flags & Styler.TextStyleFlags.FontStyle) != 0)
                SetFontStyle(text, style.fontStyle);
        }

        /// <summary>
        /// Reads every style value into the target without changing its flags.
        /// Used to carry settings across backends.
        /// </summary>
        public abstract void CaptureTextStyle(Graphic text, Styler.TextStyleItem target);
        #endregion
    }
}