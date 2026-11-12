using UnityEngine;
using Evo.Tween.Modules.Physics;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the Rigidbody2D modules used by serialized clips.
    /// </summary>
    public static class Rigidbody2DTweenExtensions
    {
        public static TweenHandle BindToPosition(this TweenBuilder<Vector2> tween, Rigidbody2D target)
        {
            return tween.Bind<Rigidbody2DPositionModule>(target);
        }

        public static TweenHandle BindToRotation(this TweenBuilder<float> tween, Rigidbody2D target)
        {
            return tween.Bind<Rigidbody2DRotationModule>(target);
        }

        public static TweenHandle BindToLinearVelocity(this TweenBuilder<Vector2> tween, Rigidbody2D target)
        {
            return tween.Bind<Rigidbody2DLinearVelocityModule>(target);
        }

        public static TweenHandle BindToAngularVelocity(this TweenBuilder<float> tween, Rigidbody2D target)
        {
            return tween.Bind<Rigidbody2DAngularVelocityModule>(target);
        }
    }
}