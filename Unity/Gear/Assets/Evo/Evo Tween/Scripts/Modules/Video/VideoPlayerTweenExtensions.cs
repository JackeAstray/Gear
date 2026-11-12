using UnityEngine.Video;
using Evo.Tween.Modules.Video;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the VideoPlayer modules used by serialized clips.
    /// </summary>
    public static class VideoPlayerTweenExtensions
    {
        public static TweenHandle BindToPlaybackSpeed(this TweenBuilder<float> tween, VideoPlayer target)
        {
            return tween.Bind<VideoPlayerPlaybackSpeedModule>(target);
        }

        public static TweenHandle BindToTime(this TweenBuilder<float> tween, VideoPlayer target)
        {
            return tween.Bind<VideoPlayerTimeModule>(target);
        }

        public static TweenHandle BindToTargetCameraAlpha(this TweenBuilder<float> tween, VideoPlayer target)
        {
            return tween.Bind<VideoPlayerTargetCameraAlphaModule>(target);
        }
    }
}