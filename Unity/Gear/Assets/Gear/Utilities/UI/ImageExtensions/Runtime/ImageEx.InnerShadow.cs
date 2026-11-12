using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：内阴影（Inner Shadow）。
    /// <para>
    /// 语义：在形状描边的内侧，按 SDF 深度把颜色叠向 <see cref="InnerShadowColor"/>，
    /// 适合做"卡片下沉/凹陷/玻璃内壁"的观感。只在 <c>DrawShape != None</c> 的形状路径生效，
    /// 纯贴图模式（DrawShape=None）不参与（贴图模式没有形状距离场）。
    /// </para>
    /// <para>
    /// 实现：全部由 <c>_EnableInnerShadow / _InnerShadowColor / _InnerShadowWidth / _InnerShadowSoftness</c>
    /// 四个 uniform 驱动，不新增着色器变体；关闭时片元里只有一次 early-return 判断。
    /// </para>
    /// </summary>
    public partial class ImageEx
    {
        [SerializeField, Tooltip("启用内阴影（仅形状路径生效）")]
        private bool enableInnerShadow = false;

        [SerializeField, ColorUsage(true, true), Tooltip("内阴影颜色（HDR）")]
        private Color innerShadowColor = new Color(0f, 0f, 0f, 1f);

        [SerializeField, Tooltip("内阴影宽度（像素）：从形状边缘向内的深度")]
        private float innerShadowWidth = 8f;

        [SerializeField, Range(0f, 1f), Tooltip("内阴影柔和度：0 线性、1 更软")]
        private float innerShadowSoftness = 0.5f;

        private static readonly int enableInnerShadow_Sp = Shader.PropertyToID("_EnableInnerShadow");
        private static readonly int innerShadowColor_Sp = Shader.PropertyToID("_InnerShadowColor");
        private static readonly int innerShadowWidth_Sp = Shader.PropertyToID("_InnerShadowWidth");
        private static readonly int innerShadowSoftness_Sp = Shader.PropertyToID("_InnerShadowSoftness");

        /// <summary>是否启用内阴影。</summary>
        public bool EnableInnerShadow
        {
            get => enableInnerShadow;
            set
            {
                if (enableInnerShadow == value) return;
                enableInnerShadow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>内阴影颜色（HDR，用于暗部染色/发光）。</summary>
        public Color InnerShadowColor
        {
            get => innerShadowColor;
            set
            {
                if (innerShadowColor == value) return;
                innerShadowColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>内阴影宽度（像素）。</summary>
        public float InnerShadowWidth
        {
            get => innerShadowWidth;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(innerShadowWidth, value)) return;
                innerShadowWidth = value;
                SetMaterialDirty();
            }
        }

        /// <summary>内阴影柔和度（0-1）。</summary>
        public float InnerShadowSoftness
        {
            get => innerShadowSoftness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(innerShadowSoftness, value)) return;
                innerShadowSoftness = value;
                SetMaterialDirty();
            }
        }
    }
}
