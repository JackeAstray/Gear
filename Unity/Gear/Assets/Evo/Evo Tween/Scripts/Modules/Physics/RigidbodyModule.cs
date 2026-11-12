using System;
using UnityEngine;

namespace Evo.Tween.Modules.Physics
{
    /// <summary>
    /// Animates a Rigidbody between world-space positions.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody/Position")]
    public sealed class RigidbodyPositionModule : Vector3Module<Rigidbody>
    {
        protected override long PropertyKey => 301L;
        protected override Vector3 Read() => Target.position;

        protected override void Write(Vector3 value)
        {
            if (Application.isPlaying && Target.isKinematic) { Target.MovePosition(value); }
            else { Target.position = value; }
        }

        protected override void RestoreOriginalValue(Vector3 value) => Target.position = value;
    }

    /// <summary>
    /// Animates a Rigidbody between world-space rotations.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody/Rotation")]
    public sealed class RigidbodyRotationModule : ValueModule<Rigidbody, Quaternion>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start rotation when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        [Tooltip("Euler rotation used when Start From Current is disabled.")]
        public Vector3 from = Vector3.zero;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        [Tooltip("Euler rotation reached at the end of the clip.")]
        public Vector3 to = Vector3.zero;

        [NonSerialized] EulerRotationCache fromRotation;
        [NonSerialized] EulerRotationCache toRotation;

        protected override long PropertyKey => 302L;
        protected override bool UseCurrentValue => fromCurrent;
        protected override Quaternion FromValue => fromRotation.Get(from);
        protected override Quaternion ToValue => toRotation.Get(to);
        protected override Quaternion Read() => Target.rotation;

        protected override void Write(Quaternion value)
        {
            if (Application.isPlaying && Target.isKinematic) { Target.MoveRotation(value); }
            else { Target.rotation = value; }
        }

        protected override Quaternion Interpolate(Quaternion start, Quaternion end, float time)
        {
            return Quaternion.SlerpUnclamped(start, end, time);
        }

        public override void SetValues(Quaternion start, Quaternion end)
        {
            fromCurrent = false;
            from = start.eulerAngles;
            to = end.eulerAngles;

            // Code tweens keep the exact rotations they were given instead of an Euler round trip
            fromRotation.Set(from, start);
            toRotation.Set(to, end);
        }

        protected override void RestoreOriginalValue(Quaternion value) => Target.rotation = value;

        protected override bool TryCopyTo(TweenModule destination)
        {
            RigidbodyRotationModule copy = (RigidbodyRotationModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }

        // Converts an Euler value only when it changes, so edits made while playing still apply
        struct EulerRotationCache
        {
            Vector3 euler;
            Quaternion rotation;
            bool valid;

            public Quaternion Get(Vector3 value)
            {
                if (!valid || value.x != euler.x || value.y != euler.y || value.z != euler.z)
                    Set(value, Quaternion.Euler(value));

                return rotation;
            }

            public void Set(Vector3 value, Quaternion exact)
            {
                euler = value;
                rotation = exact;
                valid = true;
            }
        }
    }

    /// <summary>
    /// Animates a Rigidbody's world-space linear velocity.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody/Linear Velocity")]
    public sealed class RigidbodyLinearVelocityModule : Vector3Module<Rigidbody>
    {
        protected override long PropertyKey => 303L;
        protected override Vector3 Read() => Target.linearVelocity;
        protected override void Write(Vector3 value) => Target.linearVelocity = value;
    }

    /// <summary>
    /// Animates a Rigidbody's angular velocity in radians per second.
    /// </summary>
    [Serializable, ModuleMenu("Physics/Rigidbody/Angular Velocity")]
    public sealed class RigidbodyAngularVelocityModule : Vector3Module<Rigidbody>
    {
        protected override long PropertyKey => 304L;
        protected override Vector3 Read() => Target.angularVelocity;
        protected override void Write(Vector3 value) => Target.angularVelocity = value;
    }
}