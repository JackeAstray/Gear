using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 过渡循环：驱动 <see cref="ImageEx.TransitionRate"/>，做加载/进度/流光式的过渡扫过。
    /// <para>
    /// 使用前提：ImageEx 已选择一种过渡模式（<c>Transition != None</c>），且已配置过渡纹理/颜色/宽度；
    /// 否则只是空转（本组件只告警一次）。
    /// 速度单位：每秒推进的进度（1 = 一秒走完 0→1）。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/ImageEx 过渡循环")]
    [DisallowMultipleComponent]
    public sealed class ImageExTransitionLooper : ImageExMotionBehaviour
    {
        [SerializeField, Tooltip("往返模式：0→1→0 循环；关闭则 0→1 后直接回绕")]
        private bool pingPong = true;

        [SerializeField, Tooltip("往返模式下在两端停留的时间占比（0-0.5），0 表示不停留")]
        [Range(0f, 0.5f)]
        private float edgeHold = 0f;

        private float phase;
        private bool warnedNoTransition;

        protected override void ResetPhase() => phase = 0f;

        protected override void Apply(float deltaTime)
        {
            if (Target == null) return;

            if (Target.Transition == ImageEx.TransitionMode.None)
            {
                WarnOnce(ref warnedNoTransition, "ImageEx 未选择过渡模式（Transition=None），过渡循环不会有可见效果。");
                return;
            }

            warnedNoTransition = false;

            if (deltaTime > 0f)
            {
                phase += speed * deltaTime;
                if (pingPong)
                {
                    // 往来一圈 4 个单位：0→1→2→3→0
                    phase %= 4f;
                    if (phase < 0f) phase += 4f;
                }
                else
                {
                    phase = Wrap01(phase);
                }
            }

            Target.TransitionRate = pingPong ? Evaluate(phase) : Wrap01(phase);
        }

        private float Evaluate(float t)
        {
            float hold = Mathf.Clamp(edgeHold, 0f, 0.5f);
            // 把 [0,4) 映射到带两端停留的三角波
            // 0..hold 停在 0；hold..2-hold 升到 1；2-hold..2+hold 停在 1；2+hold..4-hold 降回 0；4-hold..4 停在 0
            if (t < hold) return 0f;
            if (t < 2f - hold)
            {
                float span = Mathf.Max(1e-4f, 2f - 2f * hold);
                return Mathf.Clamp01((t - hold) / span);
            }
            if (t < 2f + hold) return 1f;
            if (t < 4f - hold)
            {
                float span = Mathf.Max(1e-4f, 4f - 2f * hold - (2f + hold));
                return Mathf.Clamp01(1f - (t - 2f - hold) / span);
            }
            return 0f;
        }

        private static float Wrap01(float value)
        {
            value %= 1f;
            if (value < 0f) value += 1f;
            return value;
        }

        /// <summary>当前进度（0-1）。</summary>
        public float Progress => pingPong ? Evaluate(phase) : Wrap01(phase);
    }
}
