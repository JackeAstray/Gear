using System;
using UnityEngine;

namespace Evo.Tween.Modules.Audio
{
    /// <summary>
    /// Animates an AudioSource's output volume.
    /// </summary>
    [Serializable, ModuleMenu("Audio/Audio Source/Volume")]
    public sealed class AudioSourceVolumeModule : NormalizedFloatModule<AudioSource>
    {
        protected override float Read() => Target.volume;
        protected override void Write(float value) => Target.volume = value;
    }

    /// <summary>
    /// Animates an AudioSource's playback pitch within Unity's supported range.
    /// </summary>
    [Serializable, ModuleMenu("Audio/Audio Source/Pitch")]
    public sealed class AudioSourcePitchModule : ConstrainedFloatModule<AudioSource>
    {
        protected override float MinimumValue => -3f;
        protected override float MaximumValue => 3f;
        protected override float Read() => Target.pitch;
        protected override void Write(float value) => Target.pitch = value;
    }

    /// <summary>
    /// Animates an AudioSource's left-to-right stereo pan.
    /// </summary>
    [Serializable, ModuleMenu("Audio/Audio Source/Stereo Pan")]
    public sealed class AudioSourceStereoPanModule : ConstrainedFloatModule<AudioSource>
    {
        protected override float MinimumValue => -1f;
        protected override float MaximumValue => 1f;
        protected override float Read() => Target.panStereo;
        protected override void Write(float value) => Target.panStereo = value;
    }

    /// <summary>
    /// Animates an AudioSource's blend between two-dimensional and three-dimensional sound.
    /// </summary>
    [Serializable, ModuleMenu("Audio/Audio Source/Spatial Blend")]
    public sealed class AudioSourceSpatialBlendModule : NormalizedFloatModule<AudioSource>
    {
        protected override float Read() => Target.spatialBlend;
        protected override void Write(float value) => Target.spatialBlend = value;
    }

    /// <summary>
    /// Animates the amount of an AudioSource routed through nearby reverb zones.
    /// </summary>
    [Serializable, ModuleMenu("Audio/Audio Source/Reverb Zone Mix")]
    public sealed class AudioSourceReverbZoneMixModule : ConstrainedFloatModule<AudioSource>
    {
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 1.1f;
        protected override float Read() => Target.reverbZoneMix;
        protected override void Write(float value) => Target.reverbZoneMix = value;
    }
}