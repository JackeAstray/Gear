using UnityEngine;
using Evo.Tween.Modules.Audio;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the AudioSource modules used by serialized clips.
    /// </summary>
    public static class AudioSourceTweenExtensions
    {
        public static TweenHandle BindToVolume(this TweenBuilder<float> tween, AudioSource target)
        {
            return tween.Bind<AudioSourceVolumeModule>(target);
        }

        public static TweenHandle BindToPitch(this TweenBuilder<float> tween, AudioSource target)
        {
            return tween.Bind<AudioSourcePitchModule>(target);
        }

        public static TweenHandle BindToStereoPan(this TweenBuilder<float> tween, AudioSource target)
        {
            return tween.Bind<AudioSourceStereoPanModule>(target);
        }

        public static TweenHandle BindToSpatialBlend(this TweenBuilder<float> tween, AudioSource target)
        {
            return tween.Bind<AudioSourceSpatialBlendModule>(target);
        }

        public static TweenHandle BindToReverbZoneMix(this TweenBuilder<float> tween, AudioSource target)
        {
            return tween.Bind<AudioSourceReverbZoneMixModule>(target);
        }
    }
}