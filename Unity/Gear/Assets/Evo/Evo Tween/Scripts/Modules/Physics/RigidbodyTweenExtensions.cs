using UnityEngine;
using Evo.Tween.Modules.Physics;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the Rigidbody modules used by serialized clips.
    /// </summary>
    public static class RigidbodyTweenExtensions
    {
        public static TweenHandle BindToPosition(this TweenBuilder<Vector3> tween, Rigidbody target)
        {
            return tween.Bind<RigidbodyPositionModule>(target);
        }

        public static TweenHandle BindToRotation(this TweenBuilder<Quaternion> tween, Rigidbody target)
        {
            return tween.Bind<RigidbodyRotationModule>(target);
        }

        public static TweenHandle BindToLinearVelocity(this TweenBuilder<Vector3> tween, Rigidbody target)
        {
            return tween.Bind<RigidbodyLinearVelocityModule>(target);
        }

        public static TweenHandle BindToAngularVelocity(this TweenBuilder<Vector3> tween, Rigidbody target)
        {
            return tween.Bind<RigidbodyAngularVelocityModule>(target);
        }
    }
}