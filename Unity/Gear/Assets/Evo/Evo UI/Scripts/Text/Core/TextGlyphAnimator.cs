using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Moves and fades the glyphs of a text and draws extra digits, without rebuilding its layout.
    /// Indices are UTF-16 positions in the strings passed to <see cref="Begin"/>. Glyphs added between
    /// <see cref="Apply"/> calls make up one frame. They are drawn while the laid-out text matches the
    /// new string, where digits may differ, so a value can keep changing during a roll.
    /// </summary>
    public abstract class TextGlyphAnimator
    {
        protected TextGlyphAnimator(Graphic text) => Text = text;

        /// <summary>
        /// Text the animator draws on.
        /// </summary>
        public Graphic Text { get; }

        /// <summary>
        /// Returns the animator for the text, replacing the current one when it belongs to another text.
        /// Null when the text's provider has no animator.
        /// </summary>
        public static TextGlyphAnimator Resolve(TextGlyphAnimator current, Graphic text)
        {
            // A destroyed text compares equal to null, so a missing text never keeps the old animator
            if (text != null && current != null && current.Text == text)
                return current;

            current?.Dispose();
            return text != null ? TextProvider.Find(text)?.CreateGlyphAnimator(text) : null;
        }

        /// <summary>
        /// Starts a change from the displayed text to a new one and sets the new text.
        /// The starting layout is captured first, which may defer the new text until the old one is laid out.
        /// </summary>
        public abstract void Begin(string from, string to);

        /// <summary>
        /// Offsets the glyph at <paramref name="index"/> in the new text.
        /// </summary>
        /// <param name="fromIndex">Glyph in the old text the glyph slides from, or -1 to stay in place.</param>
        /// <param name="slide">Progress from the old position (0) to the new one (1).</param>
        /// <param name="offsetY">Vertical offset in local units.</param>
        /// <param name="heights">Vertical offset in multiples of the glyph's line height.</param>
        public abstract void MoveGlyph(int index, int fromIndex, float slide, float offsetY, float heights, float alpha);

        /// <summary>
        /// Draws a digit between the glyph at <paramref name="index"/> in the new text and <paramref name="fromIndex"/>
        /// in the old one. Either may be -1, in which case the other position is used.
        /// </summary>
        public abstract void AddDigit(char digit, int index, int fromIndex, float slide, float offsetY, float heights, float alpha);

        /// <summary>
        /// Shows the glyphs added since the previous call.
        /// </summary>
        public abstract void Apply();

        /// <summary>
        /// Stops the animation and shows the new text as laid out.
        /// </summary>
        public abstract void End();

        /// <summary>
        /// Ends the animation and removes everything the animator added to the text.
        /// </summary>
        public abstract void Dispose();
    }
}