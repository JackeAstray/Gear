using System;
using UnityEngine;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 轻量阻尼弹簧：固定 4ms 切片积分，不受帧率影响；可序列化进组件，也可作为纯数据使用。
    /// </summary>
    [Serializable]
    public struct GearSpring
    {
        private const float StepSeconds = 0.004f;
        private const int MaxSlices = 8;

        [SerializeField, Min(0f)] private float stiffness;
        [SerializeField, Min(0f)] private float damping;
        [SerializeField] private float value;
        [SerializeField] private float velocity;

        public GearSpring(float stiffness, float damping, float value = 0f)
        {
            this.stiffness = Mathf.Max(0f, stiffness);
            this.damping = Mathf.Max(0f, damping);
            this.value = value;
            velocity = 0f;
        }

        /// <summary>当前值。</summary>
        public float Value
        {
            get => value;
            set => this.value = value;
        }

        /// <summary>当前速度。</summary>
        public float Velocity
        {
            get => velocity;
            set => this.velocity = value;
        }

        /// <summary>弹簧刚度（越大越快）。</summary>
        public float Stiffness
        {
            get => stiffness;
            set => stiffness = Mathf.Max(0f, value);
        }

        /// <summary>阻尼（越大越稳）。</summary>
        public float Damping
        {
            get => damping;
            set => damping = Mathf.Max(0f, value);
        }

        /// <summary>复位到指定值并清速度。</summary>
        public void Reset(float resetValue)
        {
            value = resetValue;
            velocity = 0f;
        }

        /// <summary>是否已基本静止。</summary>
        public bool Settled(float epsilon = 0.001f)
        {
            return Mathf.Abs(velocity) < epsilon;
        }

        /// <summary>
        /// 朝目标推进 delta 秒（内部按 4ms 切片，最多 8 片；超长帧按 32ms 处理避免爆炸）。
        /// </summary>
        public float Step(float target, float delta)
        {
            if (delta <= 0f) return value;

            int slices = Mathf.Clamp(Mathf.CeilToInt(delta / StepSeconds), 1, MaxSlices);
            float dt = Mathf.Min(delta, StepSeconds * MaxSlices) / slices;

            for (int i = 0; i < slices; i++)
            {
                float force = -stiffness * (value - target) - damping * velocity;
                velocity += force * dt;
                value += velocity * dt;
            }

            return value;
        }
    }
}
