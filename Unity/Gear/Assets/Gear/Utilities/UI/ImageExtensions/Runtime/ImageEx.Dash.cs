using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：像素级虚线参数（虚线长度 / 间隔 / 柔边）。
    /// <para>
    /// 语义：<c>DashLength &gt; 0</c> 时启用"像素级虚线"——矩形按横向/纵向像素、
    /// 圆形按周长像素计算虚线，长度/间隔一致，并随 <see cref="CustomTime"/> 平移；
    /// 为 0 时回退到旧的 UV 比例虚线，保持旧资源观感不变。
    /// </para>
    /// </summary>
    public partial class ImageEx
    {
        [SerializeField, Tooltip("虚线长度（像素）。0 = 使用旧的 UV 比例虚线")]
        private float dashLength = 0f;

        [SerializeField, Tooltip("虚线间隔（像素）")]
        private float dashGap = 14f;

        [SerializeField, Range(0f, 1f), Tooltip("虚线两端的柔边（0 = 硬边，1 = 更软）")]
        private float dashSoftness = 0f;

        private static readonly int dashLength_Sp = Shader.PropertyToID("_DashLength");
        private static readonly int dashGap_Sp = Shader.PropertyToID("_DashGap");
        private static readonly int dashSoftness_Sp = Shader.PropertyToID("_DashSoftness");

        /// <summary>虚线长度（像素）；0 回退旧逻辑。</summary>
        public float DashLength
        {
            get => dashLength;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(dashLength, value)) return;
                dashLength = value;
                SetMaterialDirty();
            }
        }

        /// <summary>虚线间隔（像素）。</summary>
        public float DashGap
        {
            get => dashGap;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(dashGap, value)) return;
                dashGap = value;
                SetMaterialDirty();
            }
        }

        /// <summary>虚线两端柔边（0-1）。</summary>
        public float DashSoftness
        {
            get => dashSoftness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(dashSoftness, value)) return;
                dashSoftness = value;
                SetMaterialDirty();
            }
        }
    }
}
