using System;

namespace Evo.Tween.Modules.RenderPipelines
{
    /// <summary>
    /// Animates the total influence of a render-pipeline Volume from zero to one.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Volume/Weight")]
    public sealed class VolumeWeightModule : NormalizedFloatModule<UnityEngine.Rendering.Volume>
    {
        protected override long PropertyKey => 601L;
        protected override float Read() => Target.weight;
        protected override void Write(float value) => Target.weight = value;
    }

    /// <summary>
    /// Animates the priority used to resolve overlapping render-pipeline Volumes.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Volume/Priority")]
    public sealed class VolumePriorityModule : FloatModule<UnityEngine.Rendering.Volume>
    {
        protected override long PropertyKey => 602L;
        protected override float Read() => Target.priority;
        protected override void Write(float value) => Target.priority = value;
    }

    /// <summary>
    /// Animates the non-negative outer blending distance of a local render-pipeline Volume.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Volume/Blend Distance")]
    public sealed class VolumeBlendDistanceModule : NonNegativeFloatModule<UnityEngine.Rendering.Volume>
    {
        protected override long PropertyKey => 603L;
        protected override float Read() => Target.blendDistance;
        protected override void Write(float value) => Target.blendDistance = value;
    }
}