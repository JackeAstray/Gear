using System;
using UnityEngine;
using UnityEngine.Video;

namespace Evo.Tween.Modules.Video
{
    /// <summary>
    /// Animates a VideoPlayer's playback-speed multiplier when the active backend supports it.
    /// </summary>
    [Serializable, ModuleMenu("Video/Video Player/Playback Speed")]
    public sealed class VideoPlayerPlaybackSpeedModule : FloatModule<VideoPlayer>
    {
        protected override long PropertyKey => 401L;
        protected override float Read() => Target.playbackSpeed;
        protected override void Write(float value)
        {
            if (Target.canSetPlaybackSpeed)
                Target.playbackSpeed = value;
        }
    }

    /// <summary>
    /// Animates the VideoPlayer's current presentation time in seconds.
    /// </summary>
    [Serializable, ModuleMenu("Video/Video Player/Time")]
    public sealed class VideoPlayerTimeModule : NonNegativeFloatModule<VideoPlayer>
    {
        protected override long PropertyKey => 402L;
        protected override float Read() => Mathf.Max(0f, (float)Target.time);
        protected override void Write(float value)
        {
            if (Target.canSetTime)
                Target.time = value;
        }
    }

    /// <summary>
    /// Animates the VideoPlayer's camera-plane transparency from transparent to opaque.
    /// </summary>
    [Serializable, ModuleMenu("Video/Video Player/Target Camera Alpha")]
    public sealed class VideoPlayerTargetCameraAlphaModule : NormalizedFloatModule<VideoPlayer>
    {
        protected override long PropertyKey => 403L;
        protected override float Read() => Target.targetCameraAlpha;
        protected override void Write(float value) => Target.targetCameraAlpha = value;
    }
}