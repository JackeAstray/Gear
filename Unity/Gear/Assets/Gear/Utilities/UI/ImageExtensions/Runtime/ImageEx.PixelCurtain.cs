using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：像素幕帘（逐格揭示/隐藏）的 uniform 与属性。
    /// 由 <see cref="GearPixelCurtain"/> 在运行时驱动，不参与序列化。
    /// </summary>
    public partial class ImageEx
    {
        private static readonly int enablePixelCurtain_Sp = Shader.PropertyToID("_EnablePixelCurtain");
        private static readonly int curtainProgress_Sp = Shader.PropertyToID("_CurtainProgress");
        private static readonly int curtainCell_Sp = Shader.PropertyToID("_CurtainCell");
        private static readonly int curtainRandomness_Sp = Shader.PropertyToID("_CurtainRandomness");
        private static readonly int curtainSoftness_Sp = Shader.PropertyToID("_CurtainSoftness");
        private static readonly int curtainDirection_Sp = Shader.PropertyToID("_CurtainDirection");

        private bool enablePixelCurtain;
        private float curtainProgress = 1f;
        private float curtainCell = 24f;
        private float curtainRandomness = 0.3f;
        private float curtainSoftness = 0.08f;
        private int curtainDirection;

        /// <summary>是否启用像素幕帘。</summary>
        public bool EnablePixelCurtain
        {
            get => enablePixelCurtain;
            set
            {
                if (enablePixelCurtain == value) return;
                enablePixelCurtain = value;
                SetMaterialDirty();
            }
        }

        /// <summary>幕帘进度：0 完全隐藏，1 完全显示。</summary>
        public float CurtainProgress
        {
            get => curtainProgress;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(curtainProgress, value)) return;
                curtainProgress = value;
                SetMaterialDirty();
            }
        }

        /// <summary>格子尺寸（像素，最小 2）。</summary>
        public float CurtainCell
        {
            get => curtainCell;
            set
            {
                value = Mathf.Max(2f, value);
                if (Mathf.Approximately(curtainCell, value)) return;
                curtainCell = value;
                SetMaterialDirty();
            }
        }

        /// <summary>逐格随机延迟（0-1）。</summary>
        public float CurtainRandomness
        {
            get => curtainRandomness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(curtainRandomness, value)) return;
                curtainRandomness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>格子边缘羽化（0-1）。</summary>
        public float CurtainSoftness
        {
            get => curtainSoftness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(curtainSoftness, value)) return;
                curtainSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>揭示方向：0 左→右，1 右→左，2 下→上，3 上→下。</summary>
        public int CurtainDirection
        {
            get => curtainDirection;
            set
            {
                value = Mathf.Clamp(value, 0, 3);
                if (curtainDirection == value) return;
                curtainDirection = value;
                SetMaterialDirty();
            }
        }
    }
}
