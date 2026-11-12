using UnityEngine;
using Evo.Tween.Modules.Rendering;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the SpriteRenderer modules used by serialized clips.
    /// </summary>
    public static class SpriteRendererTweenExtensions
    {
        public static TweenHandle BindToColor(this TweenBuilder<Color> tween, SpriteRenderer target)
        {
            return tween.Bind<SpriteRendererColorModule>(target);
        }

        public static TweenHandle BindToAlpha(this TweenBuilder<float> tween, SpriteRenderer target)
        {
            return tween.Bind<SpriteRendererAlphaModule>(target);
        }

        public static TweenHandle BindToSize(this TweenBuilder<Vector2> tween, SpriteRenderer target)
        {
            return tween.Bind<SpriteRendererSizeModule>(target);
        }
    }
}