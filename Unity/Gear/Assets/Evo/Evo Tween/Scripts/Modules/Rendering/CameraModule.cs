using System;
using UnityEngine;

namespace Evo.Tween.Modules.Rendering
{
    /// <summary>
    /// Animates a perspective Camera's vertical field of view in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Camera/Field Of View")]
    public sealed class CameraFieldOfViewModule : ConstrainedFloatModule<Camera>
    {
        protected override float MinimumValue => 1f;
        protected override float MaximumValue => 179f;
        protected override float Read() => Target.fieldOfView;
        protected override void Write(float value) => Target.fieldOfView = value;
    }

    /// <summary>
    /// Animates an orthographic Camera's half-height size.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Camera/Orthographic Size")]
    public sealed class CameraOrthographicSizeModule : ConstrainedFloatModule<Camera>
    {
        protected override float MinimumValue => 0.0001f;
        protected override float Read() => Target.orthographicSize;
        protected override void Write(float value) => Target.orthographicSize = value;
    }

    /// <summary>
    /// Animates a Camera's background clear color.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Camera/Background Color")]
    public sealed class CameraBackgroundColorModule : ColorModule<Camera>
    {
        protected override Color Read() => Target.backgroundColor;
        protected override void Write(Color value) => Target.backgroundColor = value;
    }
}