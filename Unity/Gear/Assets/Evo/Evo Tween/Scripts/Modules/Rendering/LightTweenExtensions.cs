using UnityEngine;
using Evo.Tween.Modules.Rendering;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the Light modules used by serialized clips.
    /// </summary>
    public static class LightTweenExtensions
    {
        public static TweenHandle BindToColor(this TweenBuilder<Color> tween, Light target)
        {
            return tween.Bind<LightColorModule>(target);
        }

        public static TweenHandle BindToIntensity(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightIntensityModule>(target);
        }

        public static TweenHandle BindToRange(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightRangeModule>(target);
        }

        public static TweenHandle BindToSpotAngle(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightSpotAngleModule>(target);
        }

        public static TweenHandle BindToInnerSpotAngle(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightInnerSpotAngleModule>(target);
        }

        public static TweenHandle BindToShadowStrength(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightShadowStrengthModule>(target);
        }

        public static TweenHandle BindToColorTemperature(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightColorTemperatureModule>(target);
        }

        public static TweenHandle BindToBounceIntensity(this TweenBuilder<float> tween, Light target)
        {
            return tween.Bind<LightBounceIntensityModule>(target);
        }
    }
}