using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;

namespace Evo.Tween.Modules.Rendering.Editor
{
    public sealed class RenderingTweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<SpriteRendererAlphaModule, SpriteRenderer, float>("color",
                new[] { "m_Color.a" }, (module, target) => target.color.a,
                (module, target, value) => { Color color = target.color; color.a = value; target.color = color; }, target => false);

            yield return new TweenRecordingBinding<LightColorModule, Light, Color>("color",
                new[] { "m_Color" }, (module, target) => target.color,
                (module, target, value) => target.color = value);

            yield return new TweenRecordingBinding<LightIntensityModule, Light, float>("intensity",
                new[] { "m_Intensity" }, (module, target) => target.intensity,
                (module, target, value) => target.intensity = value);

            yield return new TweenRecordingBinding<LightRangeModule, Light, float>("range",
                new[] { "m_Range" }, (module, target) => target.range,
                (module, target, value) => target.range = value);

            yield return new TweenRecordingBinding<LightSpotAngleModule, Light, float>("spotAngle",
                new[] { "m_SpotAngle" }, (module, target) => target.spotAngle,
                (module, target, value) => target.spotAngle = value);

            yield return new TweenRecordingBinding<LightInnerSpotAngleModule, Light, float>("innerSpotAngle",
                new[] { "m_InnerSpotAngle" }, (module, target) => target.innerSpotAngle,
                (module, target, value) => target.innerSpotAngle = value);

            yield return new TweenRecordingBinding<LightShadowStrengthModule, Light, float>("shadowStrength",
                new[] { "m_Shadows.m_Strength" }, (module, target) => target.shadowStrength,
                (module, target, value) => target.shadowStrength = value);

            yield return new TweenRecordingBinding<LightColorTemperatureModule, Light, float>("colorTemperature",
                new[] { "m_ColorTemperature" }, (module, target) => target.colorTemperature,
                (module, target, value) => target.colorTemperature = value);

            yield return new TweenRecordingBinding<LightBounceIntensityModule, Light, float>("bounceIntensity",
                new[] { "m_BounceIntensity" }, (module, target) => target.bounceIntensity,
                (module, target, value) => target.bounceIntensity = value);

            yield return new TweenRecordingBinding<CameraFieldOfViewModule, Camera, float>("fieldOfView",
                new[] { "field of view" }, (module, target) => target.fieldOfView,
                (module, target, value) => target.fieldOfView = value);

            yield return new TweenRecordingBinding<CameraOrthographicSizeModule, Camera, float>("orthographicSize",
                new[] { "orthographic size" }, (module, target) => target.orthographicSize,
                (module, target, value) => target.orthographicSize = value);

            yield return new TweenRecordingBinding<CameraBackgroundColorModule, Camera, Color>("backgroundColor",
                new[] { "m_BackGroundColor" }, (module, target) => target.backgroundColor,
                (module, target, value) => target.backgroundColor = value);

            yield return new TweenRecordingBinding<SpriteRendererColorModule, SpriteRenderer, Color>("color",
                new[] { "m_Color" }, (module, target) => target.color,
                (module, target, value) => target.color = value);

            yield return new TweenRecordingBinding<SpriteRendererSizeModule, SpriteRenderer, Vector2>("size",
                new[] { "m_Size" }, (module, target) => target.size,
                (module, target, value) => target.size = value);
        }
    }
}