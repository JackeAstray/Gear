using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 虚线描边流动：驱动 <see cref="ImageEx.CustomTime"/>，让虚线描边产生"跑马灯"位移。
    /// <para>
    /// 使用前提：ImageEx 上 <c>EnableDashedOutline = 1</c>，否则本组件只告警一次、不产生视觉变化。
    /// 速度单位：CustomTime 每秒推进量；<c>period</c> 用于回绕，避免长时间运行后浮点精度损失。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/ImageEx 虚线描边流动")]
    [DisallowMultipleComponent]
    public sealed class ImageExDashCrawler : ImageExMotionBehaviour
    {
        [SerializeField, Tooltip("回绕周期：相位推进到该值后归零，0 表示不回绕")]
        private float period = 64f;

        private float phase;
        private bool warnedNoDash;

        protected override void ResetPhase() => phase = 0f;

        protected override void Apply(float deltaTime)
        {
            if (Target == null) return;

            if (Target.EnableDashedOutline == 0)
            {
                WarnOnce(ref warnedNoDash, "ImageEx.EnableDashedOutline 未启用，虚线流动不会有可见效果。");
                return;
            }

            warnedNoDash = false;

            if (deltaTime > 0f)
            {
                phase += speed * deltaTime;
                if (period > 0f)
                {
                    phase %= period;
                    if (phase < 0f) phase += period;
                }
            }

            Target.CustomTime = phase;
        }

        /// <summary>供外部（如暂停/恢复）读取当前相位。</summary>
        public float Phase => phase;
    }
}
