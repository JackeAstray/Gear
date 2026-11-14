using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：Evo 对齐的高级效果（投影 / 图案填充 / 边缘辉光 / 渐变空间）。
    /// <para>
    /// 这些参数全部走材质 uniform 的运行时分支，不新增着色器变体；
    /// 关闭时片元里只有一次整型判断。
    /// </para>
    /// </summary>
    public partial class ImageEx
    {
        // ============================================================
        // 图案填充（Pattern）
        // ============================================================
        public enum PatternMode
        {
            None = 0,
            Dots = 1,
            Grid = 2,
            Stripes = 3,
            Checker = 4,
            Rings = 5,
        }

        [SerializeField, Tooltip("图案模式：点阵 / 网格 / 条纹 / 棋盘 / 圆环")]
        private PatternMode patternMode = PatternMode.None;

        [SerializeField, Tooltip("图案单元格尺寸（像素）")]
        private Vector2 patternSize = new Vector2(24f, 24f);

        [SerializeField, Tooltip("图案留白（像素）")]
        private Vector2 patternGap = new Vector2(4f, 4f);

        [SerializeField, Tooltip("图案旋转（度）")]
        private float patternAngle;

        [SerializeField, Tooltip("图案偏移（像素）")]
        private Vector2 patternOffset = Vector2.zero;

        [SerializeField, ColorUsage(true, true), Tooltip("图案颜色（HDR）")]
        private Color patternColor = Color.white;

        [SerializeField, Tooltip("图案柔边（像素）")]
        private float patternSoftness = 1f;

        [SerializeField, Tooltip("图案径向衰减起止（0-1，按图形半径归一化；End<=Start 时不衰减）")]
        private Vector2 patternFalloff = new Vector2(0f, 0f);

        [SerializeField, Tooltip("图案衰减原点（0-1）")]
        private Vector2 patternOrigin = new Vector2(0.5f, 0.5f);

        [SerializeField, Tooltip("图案流动速度（像素/秒）")]
        private Vector2 patternSpeed = Vector2.zero;

        [SerializeField, Range(0f, 1f), Tooltip("图案总不透明度")]
        private float patternOpacity = 1f;

        private static readonly int patternMode_Sp = Shader.PropertyToID("_PatternMode");
        private static readonly int patternSize_Sp = Shader.PropertyToID("_PatternSize");
        private static readonly int patternGap_Sp = Shader.PropertyToID("_PatternGap");
        private static readonly int patternAngle_Sp = Shader.PropertyToID("_PatternAngle");
        private static readonly int patternOffset_Sp = Shader.PropertyToID("_PatternOffset");
        private static readonly int patternColor_Sp = Shader.PropertyToID("_PatternColor");
        private static readonly int patternSoftness_Sp = Shader.PropertyToID("_PatternSoftness");
        private static readonly int patternFalloff_Sp = Shader.PropertyToID("_PatternFalloff");
        private static readonly int patternOrigin_Sp = Shader.PropertyToID("_PatternOrigin");
        private static readonly int patternSpeed_Sp = Shader.PropertyToID("_PatternSpeed");
        private static readonly int patternOpacity_Sp = Shader.PropertyToID("_PatternOpacity");

        /// <summary>图案模式。</summary>
        public PatternMode Pattern
        {
            get => patternMode;
            set
            {
                if (patternMode == value) return;
                patternMode = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案单元格尺寸（像素）。</summary>
        public Vector2 PatternSize
        {
            get => patternSize;
            set
            {
                value = Vector2.Max(value, new Vector2(2f, 2f));
                if (patternSize == value) return;
                patternSize = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案留白（像素）。</summary>
        public Vector2 PatternGap
        {
            get => patternGap;
            set
            {
                value = Vector2.Max(value, Vector2.zero);
                if (patternGap == value) return;
                patternGap = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案旋转（度）。</summary>
        public float PatternAngle
        {
            get => patternAngle;
            set
            {
                if (Mathf.Approximately(patternAngle, value)) return;
                patternAngle = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案偏移（像素）。</summary>
        public Vector2 PatternOffset
        {
            get => patternOffset;
            set
            {
                if (patternOffset == value) return;
                patternOffset = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案颜色（HDR）。</summary>
        public Color PatternColor
        {
            get => patternColor;
            set
            {
                if (patternColor == value) return;
                patternColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案柔边（像素）。</summary>
        public float PatternSoftness
        {
            get => patternSoftness;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(patternSoftness, value)) return;
                patternSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案径向衰减起止。</summary>
        public Vector2 PatternFalloff
        {
            get => patternFalloff;
            set
            {
                if (patternFalloff == value) return;
                patternFalloff = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案衰减原点。</summary>
        public Vector2 PatternOrigin
        {
            get => patternOrigin;
            set
            {
                if (patternOrigin == value) return;
                patternOrigin = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案流动速度（像素/秒）。</summary>
        public Vector2 PatternSpeed
        {
            get => patternSpeed;
            set
            {
                if (patternSpeed == value) return;
                patternSpeed = value;
                SetMaterialDirty();
            }
        }

        /// <summary>图案不透明度。</summary>
        public float PatternOpacity
        {
            get => patternOpacity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(patternOpacity, value)) return;
                patternOpacity = value;
                SetMaterialDirty();
            }
        }

        // ============================================================
        // 边缘辉光（Border Glow）
        // ============================================================
        [SerializeField, Tooltip("启用边缘辉光")]
        private bool enableBorderGlow;

        [SerializeField, ColorUsage(true, true), Tooltip("辉光颜色（HDR）")]
        private Color borderGlowColor = Color.white;

        [SerializeField, Tooltip("辉光宽度（像素）")]
        private float borderGlowWidth = 2f;

        [SerializeField, Tooltip("辉光外沿羽化（像素）")]
        private float borderGlowSoftness = 8f;

        [SerializeField, Range(0f, 1f), Tooltip("环境光比例：0 只跟随指针，1 整圈常亮")]
        private float borderGlowAmbient;

        [SerializeField, Range(0f, 1f), Tooltip("跟随强度：0 整圈，1 完全跟随指针")]
        private float borderGlowFollow;

        [SerializeField, Tooltip("跟随半径（像素）")]
        private float borderGlowRadius = 100f;

        [SerializeField, Range(0f, 1f), Tooltip("辉光强度")]
        private float borderGlowIntensity = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("悬停淡入系数（由 ImageExPointerGlow 动画）")]
        private float borderGlowHover = 1f;

        [SerializeField, Tooltip("指针位置（局部 0-1，由 ImageExPointerGlow 写入）")]
        private Vector2 borderGlowPosition = new Vector2(0.5f, 0.5f);

        private static readonly int enableBorderGlow_Sp = Shader.PropertyToID("_EnableBorderGlow");
        private static readonly int borderGlowColor_Sp = Shader.PropertyToID("_BorderGlowColor");
        private static readonly int borderGlowWidth_Sp = Shader.PropertyToID("_BorderGlowWidth");
        private static readonly int borderGlowSoftness_Sp = Shader.PropertyToID("_BorderGlowSoftness");
        private static readonly int borderGlowAmbient_Sp = Shader.PropertyToID("_BorderGlowAmbient");
        private static readonly int borderGlowFollow_Sp = Shader.PropertyToID("_BorderGlowFollow");
        private static readonly int borderGlowRadius_Sp = Shader.PropertyToID("_BorderGlowRadius");
        private static readonly int borderGlowIntensity_Sp = Shader.PropertyToID("_BorderGlowIntensity");
        private static readonly int borderGlowHover_Sp = Shader.PropertyToID("_BorderGlowHover");
        private static readonly int borderGlowPosition_Sp = Shader.PropertyToID("_BorderGlowPosition");

        /// <summary>是否启用边缘辉光。</summary>
        public bool EnableBorderGlow
        {
            get => enableBorderGlow;
            set
            {
                if (enableBorderGlow == value) return;
                enableBorderGlow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>辉光颜色（HDR）。</summary>
        public Color BorderGlowColor
        {
            get => borderGlowColor;
            set
            {
                if (borderGlowColor == value) return;
                borderGlowColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>辉光宽度（像素）。</summary>
        public float BorderGlowWidth
        {
            get => borderGlowWidth;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(borderGlowWidth, value)) return;
                borderGlowWidth = value;
                SetMaterialDirty();
            }
        }

        /// <summary>辉光羽化（像素）。</summary>
        public float BorderGlowSoftness
        {
            get => borderGlowSoftness;
            set
            {
                value = Mathf.Max(0f, value);
                if (Mathf.Approximately(borderGlowSoftness, value)) return;
                borderGlowSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>环境光比例。</summary>
        public float BorderGlowAmbient
        {
            get => borderGlowAmbient;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(borderGlowAmbient, value)) return;
                borderGlowAmbient = value;
                SetMaterialDirty();
            }
        }

        /// <summary>跟随强度。</summary>
        public float BorderGlowFollow
        {
            get => borderGlowFollow;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(borderGlowFollow, value)) return;
                borderGlowFollow = value;
                SetMaterialDirty();
            }
        }

        /// <summary>跟随半径（像素）。</summary>
        public float BorderGlowRadius
        {
            get => borderGlowRadius;
            set
            {
                value = Mathf.Max(1f, value);
                if (Mathf.Approximately(borderGlowRadius, value)) return;
                borderGlowRadius = value;
                SetMaterialDirty();
            }
        }

        /// <summary>辉光强度。</summary>
        public float BorderGlowIntensity
        {
            get => borderGlowIntensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(borderGlowIntensity, value)) return;
                borderGlowIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>指针位置（局部 0-1）。</summary>
        public Vector2 BorderGlowPosition
        {
            get => borderGlowPosition;
            set
            {
                if (borderGlowPosition == value) return;
                borderGlowPosition = value;
                SetMaterialDirty();
            }
        }

        /// <summary>悬停淡入系数（0-1）。</summary>
        public float BorderGlowHover
        {
            get => borderGlowHover;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(borderGlowHover, value)) return;
                borderGlowHover = value;
                SetMaterialDirty();
            }
        }

        // -------------------- 指针聚光（Spotlight） --------------------
        // 不参与序列化：由 ImageExSpotlight 组件在运行时驱动，避免给 ImageEx 再增加十几个字段。

        private static readonly int enableSpotlight_Sp = Shader.PropertyToID("_EnableSpotlight");
        private static readonly int spotlightColor_Sp = Shader.PropertyToID("_SpotlightColor");
        private static readonly int spotlightRadius_Sp = Shader.PropertyToID("_SpotlightRadius");
        private static readonly int spotlightSoftness_Sp = Shader.PropertyToID("_SpotlightSoftness");
        private static readonly int spotlightIntensity_Sp = Shader.PropertyToID("_SpotlightIntensity");
        private static readonly int spotlightPosition_Sp = Shader.PropertyToID("_SpotlightPosition");
        private static readonly int spotlightHover_Sp = Shader.PropertyToID("_SpotlightHover");

        private bool enableSpotlight;
        private Color spotlightColor = Color.white;
        private float spotlightRadius = 120f;
        private float spotlightSoftness = 0.6f;
        private float spotlightIntensity;
        private Vector2 spotlightPosition = new Vector2(0.5f, 0.5f);
        private float spotlightHover = 1f;

        /// <summary>是否启用指针聚光（由 <see cref="ImageExSpotlight"/> 打开）。</summary>
        public bool EnableSpotlight
        {
            get => enableSpotlight;
            set
            {
                if (enableSpotlight == value) return;
                enableSpotlight = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光颜色（HDR 可用于更强的叠加）。</summary>
        public Color SpotlightColor
        {
            get => spotlightColor;
            set
            {
                if (spotlightColor == value) return;
                spotlightColor = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光半径（像素，最小 1）。</summary>
        public float SpotlightRadius
        {
            get => spotlightRadius;
            set
            {
                value = Mathf.Max(1f, value);
                if (Mathf.Approximately(spotlightRadius, value)) return;
                spotlightRadius = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光边缘羽化比例（0-1，相对半径）。</summary>
        public float SpotlightSoftness
        {
            get => spotlightSoftness;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(spotlightSoftness, value)) return;
                spotlightSoftness = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光强度（0-1）。</summary>
        public float SpotlightIntensity
        {
            get => spotlightIntensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(spotlightIntensity, value)) return;
                spotlightIntensity = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光位置（局部 0-1，左下为原点）。</summary>
        public Vector2 SpotlightPosition
        {
            get => spotlightPosition;
            set
            {
                value = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                if (spotlightPosition == value) return;
                spotlightPosition = value;
                SetMaterialDirty();
            }
        }

        /// <summary>聚光悬停淡入系数（0-1）。</summary>
        public float SpotlightHover
        {
            get => spotlightHover;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(spotlightHover, value)) return;
                spotlightHover = value;
                SetMaterialDirty();
            }
        }

    }
}
