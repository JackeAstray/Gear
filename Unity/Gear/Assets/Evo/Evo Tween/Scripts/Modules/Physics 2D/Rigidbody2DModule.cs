using System;
using UnityEngine;

namespace Evo.Tween.Modules.Physics
{
    /// <summary>
    /// Animates a Rigidbody2D between world-space positions.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody 2D/Position")]
    public sealed class Rigidbody2DPositionModule : Vector2Module<Rigidbody2D>
    {
        protected override long PropertyKey => 311L;
        protected override Vector2 Read() => Target.position;

        protected override void Write(Vector2 value)
        {
            if (Application.isPlaying && Target.bodyType == RigidbodyType2D.Kinematic) { Target.MovePosition(value); }
            else { Target.position = value; }
        }

        protected override void RestoreOriginalValue(Vector2 value) => Target.position = value;
    }

    /// <summary>
    /// Animates a Rigidbody2D between world-space rotation angles in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody 2D/Rotation")]
    public sealed class Rigidbody2DRotationModule : ValueModule<Rigidbody2D, float>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start rotation when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public float from;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public float to;

        protected override long PropertyKey => 312L;
        protected override bool UseCurrentValue => fromCurrent;
        protected override float FromValue => from;
        protected override float ToValue => to;
        protected override float Read() => Target.rotation;

        protected override void Write(float value)
        {
            if (Application.isPlaying && Target.bodyType == RigidbodyType2D.Kinematic) { Target.MoveRotation(value); }
            else { Target.rotation = value; }
        }

        protected override float Interpolate(float start, float end, float time)
        {
            return start + Mathf.DeltaAngle(start, end) * time;
        }

        public override void SetValues(float start, float end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override void RestoreOriginalValue(float value) => Target.rotation = value;

        protected override bool TryCopyTo(TweenModule destination)
        {
            Rigidbody2DRotationModule copy = (Rigidbody2DRotationModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Animates a Rigidbody2D's world-space linear velocity.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody 2D/Linear Velocity")]
    public sealed class Rigidbody2DLinearVelocityModule : Vector2Module<Rigidbody2D>
    {
        protected override long PropertyKey => 313L;
        protected override Vector2 Read() => Target.linearVelocity;
        protected override void Write(Vector2 value) => Target.linearVelocity = value;
    }

    /// <summary>
    /// Animates a Rigidbody2D's angular velocity in degrees per second.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody 2D/Angular Velocity")]
    public sealed class Rigidbody2DAngularVelocityModule : FloatModule<Rigidbody2D>
    {
        protected override long PropertyKey => 314L;
        protected override float Read() => Target.angularVelocity;
        protected override void Write(float value) => Target.angularVelocity = value;
    }
}