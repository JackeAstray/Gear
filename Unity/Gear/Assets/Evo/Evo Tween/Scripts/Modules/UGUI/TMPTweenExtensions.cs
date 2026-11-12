using UnityEngine;
using TMPro;
using Evo.Tween.Modules.TMP;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the TextMesh Pro modules used by serialized clips.
    /// </summary>
    public static class TMPTweenExtensions
    {
        public static TweenHandle BindToColor(this TweenBuilder<Color> tween, TMP_Text target)
        {
            return tween.Bind<TMPColorModule>(target);
        }

        public static TweenHandle BindToAlpha(this TweenBuilder<float> tween, TMP_Text target)
        {
            return tween.Bind<TMPAlphaModule>(target);
        }

        public static TweenHandle BindToFontSize(this TweenBuilder<float> tween, TMP_Text target)
        {
            return tween.Bind<TMPFontSizeModule>(target);
        }

        public static TweenHandle BindToCharacterSpacing(this TweenBuilder<float> tween, TMP_Text target)
        {
            return tween.Bind<TMPCharacterSpacingModule>(target);
        }

        public static TweenHandle BindToWordSpacing(this TweenBuilder<float> tween, TMP_Text target)
        {
            return tween.Bind<TMPWordSpacingModule>(target);
        }

        public static TweenHandle BindToLineSpacing(this TweenBuilder<float> tween, TMP_Text target)
        {
            return tween.Bind<TMPLineSpacingModule>(target);
        }

        public static TweenHandle BindToMaxVisibleCharacters(this TweenBuilder<int> tween, TMP_Text target)
        {
            return tween.Bind<TMPVisibleCharactersModule>(target);
        }
    }
}