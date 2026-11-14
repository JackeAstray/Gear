using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：地形等值线（Topographic）的 uniform 与属性。
    /// 由 <see cref="GearTopographic"/> 在运行时驱动，不参与序列化。
    /// </summary>
    public partial class ImageEx
    {
        private static readonly int enableTopographic_Sp = Shader.PropertyToID("_EnableTopographic");
        private static readonly int topoScale_Sp = Shader.PropertyToID("_TopoScale");
        private static readonly int topoDensity_Sp = Shader.PropertyToID("_TopoDensity");
        private static readonly int topoLineWidth_Sp = Shader.PropertyToID("_TopoLineWidth");
        private static readonly int topoMajorEvery_Sp = Shader.PropertyToID("_TopoMajorEvery");
        private static readonly int topoColor_Sp = Shader.PropertyToID("_TopoColor");
        private static readonly int topoIntensity_Sp = Shader.PropertyToID("_TopoIntensity");
        private static readonly int topoSpeed_Sp = Shader.PropertyToID("_TopoSpeed");
        private static readonly int topoFalloff_Sp = Shader.PropertyToID("_TopoFalloff");

        private bool enableTopographic;
        private float topoScale = 3f;
        private float topoDensity = 6f;
        private float topoLineWidth = 0.05f;
        private float topoMajorEvery = 5f;
        private Color topoColor = Color.white;
        private float topoIntensity = 0.6f;
        private float topoSpeed = 0.2f;
        private float topoFalloff;

        /// <summary>是否启用等值线。</summary>
        public bool EnableTopographic
        {
            get => enableTopographic;
            set
            {
                if (enableTopographic == value) return;
                enableTopographic = value;
                SetMaterialDirty();
            }
        }

        /// <summary>噪声频率（越大等高线越密）。</summary>
        public float TopoScale
        {
            get => topoScale;
            set
            {
                value = Mathf.Max(0.1f, value);
                if (Mathf.Approximately(topoScale, value)) return;
                topoScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>等高线层级密度。</summary>
        public float TopoDensity
        {
            get => topoDensity;
            set
            {
                value = Mathf.Max(1f, value);
                if (Mathf.Approximately(topoDensity, value)) return;
                topoDensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>线宽（层级单位，0.001-0.5）。</summary>
        public float TopoLineWidth
        {
            get => topoLineWidth;
            set
            {
                value = Mathf.Clamp(value, 0.001f, 0.5f);
                if (Mathf.Approximately(topoLineWidth, value)) return;
                topoLineWidth = value;
                SetMaterialDirty();
            }
        }

        /// <summary>每多少条加一条主线。</summary>
        public float TopoMajorEvery
        {
            get => topoMajorEvery;
            set
            {
                value = Mathf.Max(1f, value);
                if (Mathf.Approximately(topoMajorEvery, value)) return;
                topoMajorEvery = value;
                SetMaterialDirty();
            }
        }

        /// <summary>线条颜色。</summary>
        public Color TopoColor
        {
            get => topoColor;
            set
            {
                if (topoColor == value) return;
                topoColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>整体强度（0-1）。</summary>
        public float TopoIntensity
        {
            get => topoIntensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(topoIntensity, value)) return;
                topoIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>滚动速度。</summary>
        public float TopoSpeed
        {
            get => topoSpeed;
            set
            {
                if (Mathf.Approximately(topoSpeed, value)) return;
                topoSpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>从中心向外的衰减（0-1）。</summary>
        public float TopoFalloff
        {
            get => topoFalloff;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(topoFalloff, value)) return;
                topoFalloff = value;
                SetMaterialDirty();
            }
        }
    }
}
