using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>ImageEx partial part：点阵图案（Dot Matrix）的 uniform 与属性。</summary>
    public partial class ImageEx
    {
        // -------------------- 点阵图案（Dot Matrix） --------------------
        // 与 Spotlight 一样不参与序列化：由 GearDotMatrix 组件在运行时驱动，避免继续膨胀 ImageEx 字段。

        private static readonly int enableDotMatrix_Sp = Shader.PropertyToID("_EnableDotMatrix");
        private static readonly int dotPattern_Sp = Shader.PropertyToID("_DotPattern");
        private static readonly int dotShape_Sp = Shader.PropertyToID("_DotShape");
        private static readonly int dotCell_Sp = Shader.PropertyToID("_DotCell");
        private static readonly int dotRadius_Sp = Shader.PropertyToID("_DotRadius");
        private static readonly int dotColor_Sp = Shader.PropertyToID("_DotColor");
        private static readonly int dotIntensity_Sp = Shader.PropertyToID("_DotIntensity");
        private static readonly int dotSpeed_Sp = Shader.PropertyToID("_DotSpeed");
        private static readonly int dotInactiveAlpha_Sp = Shader.PropertyToID("_DotInactiveAlpha");
        private static readonly int dotInactiveScale_Sp = Shader.PropertyToID("_DotInactiveScale");
        private static readonly int dotEdgeSoftness_Sp = Shader.PropertyToID("_DotEdgeSoftness");
        private static readonly int dotPhaseOffset_Sp = Shader.PropertyToID("_DotPhaseOffset");
        private static readonly int dotTrailLength_Sp = Shader.PropertyToID("_DotTrailLength");

        private bool enableDotMatrix;
        private DotMatrixPattern dotPattern = DotMatrixPattern.Wave;
        private DotMatrixShape dotShape = DotMatrixShape.Circle;
        private float dotCell = 18f;
        private float dotRadius = 0.32f;
        private Color dotColor = Color.white;
        private float dotIntensity = 0.6f;
        private float dotSpeed = 1f;
        private float dotInactiveAlpha = 0.22f;
        private float dotInactiveScale = 0.55f;
        private float dotEdgeSoftness = 0.12f;
        private float dotPhaseOffset;
        private float dotTrailLength = 1f;

        /// <summary>是否启用点阵图案（由 <see cref="GearDotMatrix"/> 打开）。</summary>
        public bool EnableDotMatrix
        {
            get => enableDotMatrix;
            set
            {
                if (enableDotMatrix == value) return;
                enableDotMatrix = value;
                SetMaterialDirty();
            }
        }

        /// <summary>解析图案。</summary>
        public DotMatrixPattern DotPattern
        {
            get => dotPattern;
            set
            {
                if (dotPattern == value) return;
                dotPattern = value;
                SetMaterialDirty();
            }
        }

        /// <summary>点形状。</summary>
        public DotMatrixShape DotShape
        {
            get => dotShape;
            set
            {
                if (dotShape == value) return;
                dotShape = value;
                SetMaterialDirty();
            }
        }

        /// <summary>格子尺寸（像素，最小 4）。</summary>
        public float DotCell
        {
            get => dotCell;
            set
            {
                value = Mathf.Max(4f, value);
                if (Mathf.Approximately(dotCell, value)) return;
                dotCell = value;
                SetMaterialDirty();
            }
        }

        /// <summary>点半径占格子的比例（0-0.5）。</summary>
        public float DotRadius
        {
            get => dotRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 0.5f);
                if (Mathf.Approximately(dotRadius, value)) return;
                dotRadius = value;
                SetMaterialDirty();
            }
        }

        /// <summary>点的颜色。</summary>
        public Color DotColor
        {
            get => dotColor;
            set
            {
                if (dotColor == value) return;
                dotColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>整体强度（0-1）。</summary>
        public float DotIntensity
        {
            get => dotIntensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(dotIntensity, value)) return;
                dotIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>动画速度（乘 _Time.y）。</summary>
        public float DotSpeed
        {
            get => dotSpeed;
            set
            {
                if (Mathf.Approximately(dotSpeed, value)) return;
                dotSpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>不活跃点保留的亮度比例（0 时只显示活跃点）。</summary>
        public float DotInactiveAlpha
        {
            get => dotInactiveAlpha;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(dotInactiveAlpha, value)) return;
                dotInactiveAlpha = value;
                SetMaterialDirty();
            }
        }

        /// <summary>不活跃点的半径缩放（0-1）。</summary>
        public float DotInactiveScale
        {
            get => dotInactiveScale;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(dotInactiveScale, value)) return;
                dotInactiveScale = value;
                SetMaterialDirty();
            }
        }

        /// <summary>点的边缘羽化（0 硬边）。</summary>
        public float DotEdgeSoftness
        {
            get => dotEdgeSoftness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(dotEdgeSoftness, value)) return;
                dotEdgeSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案相位偏移（秒），用于错峰/多面板。</summary>
        public float DotPhaseOffset
        {
            get => dotPhaseOffset;
            set
            {
                if (Mathf.Approximately(dotPhaseOffset, value)) return;
                dotPhaseOffset = value;
                SetMaterialDirty();
            }
        }

        /// <summary>拖尾长度：活跃点向后保留的衰减长度（越大越长）。</summary>
        public float DotTrailLength
        {
            get => dotTrailLength;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(dotTrailLength, value)) return;
                dotTrailLength = value;
                SetMaterialDirty();
            }
        }
    }
}
