using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 线性渐变旋转：驱动 <see cref="ImageEx"/> 的渐变旋转角，做"流光/呼吸底"。
    /// <para>
    /// 使用前提：ImageEx 的渐变已启用（<c>GradientEffect.Enabled = true</c>）且类型为线性，
    /// 否则旋转不会产生可见变化（本组件只告警一次）。
    /// 速度单位：度/秒。
    /// </para>
    /// <para>
    /// 注意：渐变旋转每帧都会让 ImageEx 置脏并重写材质参数，属于"持续动画"成本；
    /// 同屏大量元素同时旋转时，建议只给视觉焦点元素使用。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/ImageEx 渐变旋转")]
    [DisallowMultipleComponent]
    public sealed class ImageExGradientSpinner : ImageExMotionBehaviour
    {
        [SerializeField, Tooltip("角度回绕：到达 ±360 后归零，避免长期运行精度损失")]
        private bool wrapAngle = true;

        private float angle;
        private bool warnedDisabled;

        protected override void ResetPhase() => angle = 0f;

        protected override void Apply(float deltaTime)
        {
            if (Target == null) return;

            GradientEffect gradient = Target.GradientEffect;
            if (!gradient.Enabled)
            {
                WarnOnce(ref warnedDisabled, "ImageEx 渐变未启用（GradientEffect.Enabled=false），旋转不会有可见效果。");
                return;
            }

            warnedDisabled = false;

            if (deltaTime > 0f)
            {
                angle += speed * deltaTime;
                if (wrapAngle)
                {
                    angle %= 360f;
                    if (angle < 0f) angle += 360f;
                }
            }

            gradient.Rotation = angle;
            Target.GradientEffect = gradient;
        }

        /// <summary>当前角度（度）。</summary>
        public float Angle => angle;
    }
}
