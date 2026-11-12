using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;

namespace Evo.Tween.Modules.Audio.Editor
{
    public sealed class AudioTweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<AudioSourceVolumeModule, AudioSource, float>("volume",
                new[] { "m_Volume" }, (module, target) => target.volume,
                (module, target, value) => target.volume = value);

            yield return new TweenRecordingBinding<AudioSourcePitchModule, AudioSource, float>("pitch",
                new[] { "m_Pitch" }, (module, target) => target.pitch,
                (module, target, value) => target.pitch = value);

            yield return new TweenRecordingBinding<AudioSourceStereoPanModule, AudioSource, float>("panStereo",
                new[] { "Pan2D" }, (module, target) => target.panStereo,
                (module, target, value) => target.panStereo = value);

            yield return new TweenRecordingBinding<AudioSourceSpatialBlendModule, AudioSource, float>("spatialBlend",
                new[] { "panLevelCustomCurve" }, (module, target) => target.spatialBlend,
                (module, target, value) => target.spatialBlend = value);

            yield return new TweenRecordingBinding<AudioSourceReverbZoneMixModule, AudioSource, float>("reverbZoneMix",
                new[] { "reverbZoneMixCustomCurve" }, (module, target) => target.reverbZoneMix,
                (module, target, value) => target.reverbZoneMix = value);
        }
    }
}