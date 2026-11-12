using System;
using UnityEngine;
using TMPro;

namespace Evo.Tween.Modules.TMP
{
    /// <summary>
    /// Animates a TextMesh Pro component's color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Color")]
    public sealed class TMPColorModule : ColorModule<TMP_Text>
    {
        protected override long PropertyKey => 111L;
        protected override Color Read() => Target.color;
        protected override void Write(Color value) => Target.color = value;
    }

    /// <summary>
    /// Animates only the alpha channel of a TextMesh Pro component.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Alpha")]
    public sealed class TMPAlphaModule : NormalizedFloatModule<TMP_Text>
    {
        protected override long PropertyKey => 111L;
        protected override float Read() => Target.alpha;
        protected override void Write(float value) => Target.alpha = value;
    }

    /// <summary>
    /// Animates a TextMesh Pro component's non-negative font size.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Font Size")]
    public sealed class TMPFontSizeModule : NonNegativeFloatModule<TMP_Text>
    {
        protected override long PropertyKey => 201L;
        protected override float Read() => Target.fontSize;
        protected override void Write(float value) => Target.fontSize = value;
    }

    /// <summary>
    /// Animates a TextMesh Pro component's character spacing.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Character Spacing")]
    public sealed class TMPCharacterSpacingModule : FloatModule<TMP_Text>
    {
        protected override long PropertyKey => 202L;
        protected override float Read() => Target.characterSpacing;
        protected override void Write(float value) => Target.characterSpacing = value;
    }

    /// <summary>
    /// Animates a TextMesh Pro component's word spacing.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Word Spacing")]
    public sealed class TMPWordSpacingModule : FloatModule<TMP_Text>
    {
        protected override long PropertyKey => 203L;
        protected override float Read() => Target.wordSpacing;
        protected override void Write(float value) => Target.wordSpacing = value;
    }

    /// <summary>
    /// Animates a TextMesh Pro component's line spacing.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Line Spacing")]
    public sealed class TMPLineSpacingModule : FloatModule<TMP_Text>
    {
        protected override long PropertyKey => 204L;
        protected override float Read() => Target.lineSpacing;
        protected override void Write(float value) => Target.lineSpacing = value;
    }

    /// <summary>
    /// Animates how many characters a TextMesh Pro component reveals.
    /// </summary>
    [Serializable, ModuleMenu("UI/TextMesh Pro/Visible Characters")]
    public sealed class TMPVisibleCharactersModule : NonNegativeIntModule<TMP_Text>
    {
        protected override long PropertyKey => 205L;
        protected override int Read() => Target.maxVisibleCharacters;
        protected override void Write(int value) => Target.maxVisibleCharacters = value;
    }
}