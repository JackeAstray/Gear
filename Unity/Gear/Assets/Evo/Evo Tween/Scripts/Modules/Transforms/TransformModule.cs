using System;
using UnityEngine;

namespace Evo.Tween.Modules.Transforms
{
    /// <summary>
    /// Animates a Transform between positions in local or world space.
    /// </summary>
    [Serializable, ModuleMenu("Transform/Position")]
    public sealed class TransformPositionModule : Vector3Module<Transform>
    {
        [ModuleField(ModuleFieldGroup.Settings, Label = "Coordinate Space")]
        [Tooltip("Coordinate system used to read and write position.")]
        public CoordinateSpace coordinateSpace = CoordinateSpace.Local;

        [NonSerialized] bool capturedInLocalSpace;

        bool IsLocal => coordinateSpace == CoordinateSpace.Local;
        protected internal override long PropertyKey => 1L;
        protected override Vector3 Read() => IsLocal ? Target.localPosition : Target.position;

        protected override void Write(Vector3 value)
        {
            if (IsLocal) { Target.localPosition = value; }
            else { Target.position = value; }
        }

        protected override Vector3 CaptureOriginalValue()
        {
            capturedInLocalSpace = IsLocal;
            return capturedInLocalSpace ? Target.localPosition : Target.position;
        }

        protected override void RestoreOriginalValue(Vector3 value)
        {
            if (capturedInLocalSpace) { Target.localPosition = value; }
            else { Target.position = value; }
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            TransformPositionModule copy = (TransformPositionModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            copy.coordinateSpace = coordinateSpace;
            return true;
        }
    }

    /// <summary>
    /// Animates a Transform between rotations in local or world space.
    /// </summary>
    [Serializable, ModuleMenu("Transform/Rotation")]
    public sealed class TransformRotationModule : ValueModule<Transform, Quaternion>
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

        [ModuleField(ModuleFieldGroup.Settings, Label = "Coordinate Space")]
        [Tooltip("Coordinate system used to read and write rotation.")]
        public CoordinateSpace coordinateSpace = CoordinateSpace.Local;

        [NonSerialized] bool capturedInLocalSpace;
        [NonSerialized] EulerRotationCache fromRotation;
        [NonSerialized] EulerRotationCache toRotation;

        bool IsLocal => coordinateSpace == CoordinateSpace.Local;
        protected internal override long PropertyKey => 3L;
        protected override bool UseCurrentValue => fromCurrent;
        protected override Quaternion FromValue => fromRotation.Get(from);
        protected override Quaternion ToValue => toRotation.Get(to);
        protected override Quaternion Read() => IsLocal ? Target.localRotation : Target.rotation;

        protected override void Write(Quaternion value)
        {
            if (IsLocal) { Target.localRotation = value; }
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

        protected override Quaternion CaptureOriginalValue()
        {
            capturedInLocalSpace = IsLocal;
            return capturedInLocalSpace ? Target.localRotation : Target.rotation;
        }

        protected override void RestoreOriginalValue(Quaternion value)
        {
            if (capturedInLocalSpace) { Target.localRotation = value; }
            else { Target.rotation = value; }
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            TransformRotationModule copy = (TransformRotationModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            copy.coordinateSpace = coordinateSpace;
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
    /// Animates a Transform between local-scale values.
    /// </summary>
    [Serializable, ModuleMenu("Transform/Scale")]
    public sealed class TransformScaleModule : Vector3Module<Transform>
    {
        protected internal override long PropertyKey => 5L;

        public TransformScaleModule()
        {
            from = Vector3.one;
            to = Vector3.one;
        }

        protected override Vector3 Read() => Target.localScale;
        protected override void Write(Vector3 value) => Target.localScale = value;
    }
}