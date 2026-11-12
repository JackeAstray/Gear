using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;
using TMPro;

namespace Evo.Tween.Modules.TMP.Editor
{
    public sealed class TMPTweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<TMPAlphaModule, TMP_Text, float>("color",
                new[] { "m_fontColor.a" }, (module, target) => target.alpha,
                (module, target, value) => target.alpha = value, target => false);

            yield return new TweenRecordingBinding<TMPColorModule, TMP_Text, Color>("color",
                new[] { "m_fontColor" }, (module, target) => target.color,
                (module, target, value) => target.color = value);

            yield return new TweenRecordingBinding<TMPFontSizeModule, TMP_Text, float>("fontSize",
                new[] { "m_fontSize", "m_fontSizeBase" }, (module, target) => target.fontSize,
                (module, target, value) => target.fontSize = value);

            yield return new TweenRecordingBinding<TMPCharacterSpacingModule, TMP_Text, float>("characterSpacing",
                new[] { "m_characterSpacing" }, (module, target) => target.characterSpacing,
                (module, target, value) => target.characterSpacing = value);

            yield return new TweenRecordingBinding<TMPWordSpacingModule, TMP_Text, float>("wordSpacing",
                new[] { "m_wordSpacing" }, (module, target) => target.wordSpacing,
                (module, target, value) => target.wordSpacing = value);

            yield return new TweenRecordingBinding<TMPLineSpacingModule, TMP_Text, float>("lineSpacing",
                new[] { "m_lineSpacing" }, (module, target) => target.lineSpacing,
                (module, target, value) => target.lineSpacing = value);
        }
    }
}