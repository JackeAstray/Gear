using System;
using UnityEngine;

namespace Evo.Tween.Modules.Rendering
{
    /// <summary>
    /// Animates a Light's emitted color.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Color")]
    public sealed class LightColorModule : ColorModule<Light>
    {
        protected override Color Read() => Target.color;
        protected override void Write(Color value) => Target.color = value;
    }

    /// <summary>
    /// Animates a Light's non-negative intensity.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Intensity")]
    public sealed class LightIntensityModule : NonNegativeFloatModule<Light>
    {
        protected override float Read() => Target.intensity;
        protected override void Write(float value) => Target.intensity = value;
    }

    /// <summary>
    /// Animates a Light's non-negative range.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Range")]
    public sealed class LightRangeModule : NonNegativeFloatModule<Light>
    {
        protected override float Read() => Target.range;
        protected override void Write(float value) => Target.range = value;
    }

    /// <summary>
    /// Animates a spot Light's outer cone angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Spot Angle")]
    public sealed class LightSpotAngleModule : ConstrainedFloatModule<Light>
    {
        protected override float MinimumValue => 1f;
        protected override float MaximumValue => 179f;
        protected override float Read() => Target.spotAngle;
        protected override void Write(float value) => Target.spotAngle = value;
    }

    /// <summary>
    /// Animates a spot Light's inner cone angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Inner Spot Angle")]
    public sealed class LightInnerSpotAngleModule : ConstrainedFloatModule<Light>
    {
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 179f;
        protected override float Read() => Target.innerSpotAngle;
        protected override void Write(float value) => Target.innerSpotAngle = value;
    }

    /// <summary>
    /// Animates a Light's realtime shadow strength from zero to one.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Shadow Strength")]
    public sealed class LightShadowStrengthModule : NormalizedFloatModule<Light>
    {
        protected override float Read() => Target.shadowStrength;
        protected override void Write(float value) => Target.shadowStrength = value;
    }

    /// <summary>
    /// Animates a Light's correlated color temperature in Kelvin.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Color Temperature")]
    public sealed class LightColorTemperatureModule : ConstrainedFloatModule<Light>
    {
        protected override float MinimumValue => 1000f;
        protected override float MaximumValue => 20000f;
        protected override float Read() => Target.colorTemperature;
        protected override void Write(float value) => Target.colorTemperature = value;
    }

    /// <summary>
    /// Animates a Light's indirect-light bounce multiplier.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Light/Bounce Intensity")]
    public sealed class LightBounceIntensityModule : NonNegativeFloatModule<Light>
    {
        protected override float Read() => Target.bounceIntensity;
        protected override void Write(float value) => Target.bounceIntensity = value;
    }
}