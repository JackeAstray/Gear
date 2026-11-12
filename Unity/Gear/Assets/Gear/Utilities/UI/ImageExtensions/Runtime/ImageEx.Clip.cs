using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：进度裁剪（Progress Clip）。
    /// <para>
    /// 只改 alpha、不改变形状：横向/纵向/径向（扇形）三种模式，配合 <see cref="ClipAmount"/> 做
    /// 血条、加载条、扇形冷却、逐格显现。形状路径用 SDF 坐标，纯贴图路径用 UV，两边语义一致。
    /// </para>
    /// <para>
    /// 注意：裁剪只作用于主图通道；阴影通道（shadow vertices）在形状路径下也会跟随裁剪，
    /// 纯贴图路径的阴影不参与裁剪（贴图模式没有形状尺寸可用）。
    /// </para>
    /// </summary>
    public partial class ImageEx
    {
        public enum ClipMode
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2,
            Radial = 3,
        }

        [SerializeField, Tooltip("进度裁剪模式：横向 / 纵向 / 径向（扇形）")]
        private ClipMode clipMode = ClipMode.None;

        [SerializeField, Range(0f, 1f), Tooltip("可见比例：0 全隐藏，1 全显示")]
        private float clipAmount = 1f;

        [SerializeField, Tooltip("反向：从另一端开始填充")]
        private bool clipReverse = false;

        [SerializeField, Range(0f, 8f), Tooltip("边缘柔和度（像素），0.5 起")]
        private float clipSoftness = 1f;

        private static readonly int clipMode_Sp = Shader.PropertyToID("_ClipMode");
        private static readonly int clipAmount_Sp = Shader.PropertyToID("_ClipAmount");
        private static readonly int clipReverse_Sp = Shader.PropertyToID("_ClipReverse");
        private static readonly int clipSoftness_Sp = Shader.PropertyToID("_ClipSoftness");

        /// <summary>进度裁剪模式。</summary>
        public ClipMode Clip
        {
            get => clipMode;
            set
            {
                if (clipMode == value) return;
                clipMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>可见比例（0-1）。</summary>
        public float ClipAmount
        {
            get => clipAmount;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(clipAmount, value)) return;
                clipAmount = value;
                SetMaterialDirty();
            }
        }

        /// <summary>是否反向填充。</summary>
        public bool ClipReverse
        {
            get => clipReverse;
            set
            {
                if (clipReverse == value) return;
                clipReverse = value;
                SetMaterialDirty();
            }
        }

        /// <summary>边缘柔和度（像素）。</summary>
        public float ClipSoftness
        {
            get => clipSoftness;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(clipSoftness, value)) return;
                clipSoftness = value;
                SetMaterialDirty();
            }
        }
    }
}
