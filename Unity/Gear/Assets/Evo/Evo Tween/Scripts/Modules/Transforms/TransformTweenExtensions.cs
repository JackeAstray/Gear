using UnityEngine;
using Evo.Tween.Modules.Transforms;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the Transform modules used by serialized clips.
    /// </summary>
    public static class TransformTweenExtensions
    {
        readonly struct PositionConfiguration : ITweenModuleConfiguration<TransformPositionModule>
        {
            readonly CoordinateSpace coordinateSpace;

            public PositionConfiguration(CoordinateSpace coordinateSpace) => this.coordinateSpace = coordinateSpace;
            public void Apply(TransformPositionModule module) => module.coordinateSpace = coordinateSpace;
        }

        readonly struct RotationConfiguration : ITweenModuleConfiguration<TransformRotationModule>
        {
            readonly CoordinateSpace coordinateSpace;

            public RotationConfiguration(CoordinateSpace coordinateSpace) => this.coordinateSpace = coordinateSpace;
            public void Apply(TransformRotationModule module) => module.coordinateSpace = coordinateSpace;
        }

        public static TweenHandle BindToPosition(this TweenBuilder<Vector3> tween, Transform target)
        {
            return tween.Bind<TransformPositionModule, PositionConfiguration>(target, new PositionConfiguration(CoordinateSpace.World));
        }

        public static TweenHandle BindToLocalPosition(this TweenBuilder<Vector3> tween, Transform target)
        {
            return tween.Bind<TransformPositionModule, PositionConfiguration>(target, new PositionConfiguration(CoordinateSpace.Local));
        }

        public static TweenHandle BindToRotation(this TweenBuilder<Quaternion> tween, Transform target)
        {
            return tween.Bind<TransformRotationModule, RotationConfiguration>(target, new RotationConfiguration(CoordinateSpace.World));
        }

        public static TweenHandle BindToLocalRotation(this TweenBuilder<Quaternion> tween, Transform target)
        {
            return tween.Bind<TransformRotationModule, RotationConfiguration>(target, new RotationConfiguration(CoordinateSpace.Local));
        }

        public static TweenHandle BindToScale(this TweenBuilder<Vector3> tween, Transform target)
        {
            return tween.Bind<TransformScaleModule>(target);
        }
    }
}