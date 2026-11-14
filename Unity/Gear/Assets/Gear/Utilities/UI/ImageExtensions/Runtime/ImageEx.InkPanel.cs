using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：墨团揭示（InkPanel）的 uniform 与属性。
    /// 由 <see cref="GearInkPanel"/> 在运行时驱动，不参与序列化。
    /// </summary>
    public partial class ImageEx
    {
        private static readonly int enableInkPanel_Sp = Shader.PropertyToID("_EnableInkPanel");
        private static readonly int inkProgress_Sp = Shader.PropertyToID("_InkProgress");
        private static readonly int inkOrigin_Sp = Shader.PropertyToID("_InkOrigin");
        private static readonly int inkSoftness_Sp = Shader.PropertyToID("_InkSoftness");
        private static readonly int inkRoughness_Sp = Shader.PropertyToID("_InkRoughness");

        private bool enableInkPanel;
        private float inkProgress = 1f;
        private Vector2 inkOrigin = new Vector2(0.5f, 0.5f);
        private float inkSoftness = 0.08f;
        private float inkRoughness = 0.35f;

        /// <summary>是否启用墨团揭示。</summary>
        public bool EnableInkPanel
        {
            get => enableInkPanel;
            set
            {
                if (enableInkPanel == value) return;
                enableInkPanel = value;
                SetMaterialDirty();
            }
        }

        /// <summary>墨团进度：0 完全隐藏（只留原点），1 完全显示。</summary>
        public float InkProgress
        {
            get => inkProgress;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(inkProgress, value)) return;
                inkProgress = value;
                SetMaterialDirty();
            }
        }

        /// <summary>墨团原点（局部 0-1）。</summary>
        public Vector2 InkOrigin
        {
            get => inkOrigin;
            set
            {
                value = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                if (inkOrigin == value) return;
                inkOrigin = value;
                SetMaterialDirty();
            }
        }

        /// <summary>边缘羽化（0.001-0.5）。</summary>
        public float InkSoftness
        {
            get => inkSoftness;
            set
            {
                value = Mathf.Clamp(value, 0.001f, 0.5f);
                if (Mathf.Approximately(inkSoftness, value)) return;
                inkSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>边缘噪声粗糙度（0-1）。</summary>
        public float InkRoughness
        {
            get => inkRoughness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(inkRoughness, value)) return;
                inkRoughness = value;
                SetMaterialDirty();
            }
        }
    }
}
