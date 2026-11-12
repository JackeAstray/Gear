using UnityEngine;
using Evo.Tween.Modules.Rendering;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the Camera modules used by serialized clips.
    /// </summary>
    public static class CameraTweenExtensions
    {
        public static TweenHandle BindToFieldOfView(this TweenBuilder<float> tween, Camera target)
        {
            return tween.Bind<CameraFieldOfViewModule>(target);
        }

        public static TweenHandle BindToOrthographicSize(this TweenBuilder<float> tween, Camera target)
        {
            return tween.Bind<CameraOrthographicSizeModule>(target);
        }

        public static TweenHandle BindToBackgroundColor(this TweenBuilder<Color> tween, Camera target)
        {
            return tween.Bind<CameraBackgroundColorModule>(target);
        }
    }
}