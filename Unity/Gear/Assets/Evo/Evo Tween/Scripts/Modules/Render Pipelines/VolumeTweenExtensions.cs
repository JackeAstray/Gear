using UnityEngine.Rendering;
using Evo.Tween.Modules.RenderPipelines;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the shared URP/HDRP Volume modules used by serialized clips.
    /// </summary>
    public static class VolumeTweenExtensions
    {
        public static TweenHandle BindToWeight(this TweenBuilder<float> tween, Volume target)
        {
            return tween.Bind<VolumeWeightModule>(target);
        }

        public static TweenHandle BindToPriority(this TweenBuilder<float> tween, Volume target)
        {
            return tween.Bind<VolumePriorityModule>(target);
        }

        public static TweenHandle BindToBlendDistance(this TweenBuilder<float> tween, Volume target)
        {
            return tween.Bind<VolumeBlendDistanceModule>(target);
        }
    }
}