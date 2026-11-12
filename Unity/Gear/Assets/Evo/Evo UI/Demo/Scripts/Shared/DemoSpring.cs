using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A damped spring, stepped in short slices so stiff ones stay stable at low frame rates.
    /// It counts as settled within epsilon: pixels and degrees by default, fractions pass a smaller one.
    /// </summary>
    public struct DemoSpring
    {
        const float Slice = 0.004f;

        public float value, velocity, target, stiffness, damping, epsilon;

        public void Set(float k, float zeta, float settle = 0.05f)
        {
            stiffness = k;
            damping = 2f * zeta * Mathf.Sqrt(k);
            epsilon = settle;
        }

        public void Snap(float v)
        {
            value = target = v;
            velocity = 0f;
        }

        public void Step(float dt, float slice = Slice)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / slice));
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                velocity += (stiffness * (target - value) - damping * velocity) * h;
                value += velocity * h;
            }
        }

        // For a target that moves every frame: chases it with the stiffness and damping passed along
        public void Step(float to, float dt, float k, float zeta, float slice)
        {
            target = to;
            Set(k, zeta, epsilon);
            Step(dt, slice);
        }

        // Several values on one spring, e.g. a card's position, angle and scale; zeta 1 settles without overshoot
        public static void Step(ref Vector4 value, ref Vector4 velocity, Vector4 target, float k, float zeta, float dt)
        {
            float friction = 2f * zeta * Mathf.Sqrt(k);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / Slice));
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                velocity += (k * (target - value) - friction * velocity) * h;
                value += velocity * h;
            }
        }

        public bool Settled => Mathf.Abs(target - value) < epsilon && Mathf.Abs(velocity) < epsilon;

        // Steps the spring and returns whether it still moves; a settled one lands exactly on its target
        public bool Advance(float dt)
        {
            Step(dt);
            if (!Settled)
                return true;

            Snap(target);
            return false;
        }
    }
}
