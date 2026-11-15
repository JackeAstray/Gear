using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 点阵图案：在 ImageEx 上叠 8 种解析点阵（波浪/同心圆/雷达/螺旋/蛇形/均衡器/棋盘/脉冲）。
    /// <para>
    /// 只改 ImageEx 的 DotMatrix uniform，不改网格与材质模式；可与 Pattern/Spotlight/SoftMask 叠加。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Dot Matrix")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageEx))]
    public sealed class GearDotMatrix : MonoBehaviour
    {
        [SerializeField] private DotMatrixPattern pattern = DotMatrixPattern.Wave;
        [SerializeField] private DotMatrixShape shape = DotMatrixShape.Circle;
        [SerializeField, Min(4f)] private float cell = 18f;
        [SerializeField, Range(0f, 0.5f)] private float radius = 0.32f;
        [SerializeField] private Color color = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField, Range(0f, 1f)] private float intensity = 0.6f;
        [SerializeField] private float speed = 1f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.22f;
        [SerializeField, Range(0f, 1f)] private float inactiveScale = 0.55f;
        [SerializeField, Range(0f, 1f)] private float edgeSoftness = 0.12f;
        [SerializeField] private float phaseOffset;
        [SerializeField, Min(0f)] private float trailLength = 1f;

        private ImageEx image;

        public DotMatrixPattern Pattern
        {
            get => pattern;
            set { pattern = value; ApplySettings(); }
        }

        public DotMatrixShape Shape
        {
            get => shape;
            set { shape = value; ApplySettings(); }
        }

        public float Cell
        {
            get => cell;
            set { cell = Mathf.Max(4f, value); ApplySettings(); }
        }

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Clamp(value, 0f, 0.5f); ApplySettings(); }
        }

        public Color Color
        {
            get => color;
            set { color = value; ApplySettings(); }
        }

        public float Intensity
        {
            get => intensity;
            set { intensity = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float Speed
        {
            get => speed;
            set { speed = value; ApplySettings(); }
        }

        public float InactiveAlpha
        {
            get => inactiveAlpha;
            set { inactiveAlpha = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float InactiveScale
        {
            get => inactiveScale;
            set { inactiveScale = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set { edgeSoftness = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float PhaseOffset
        {
            get => phaseOffset;
            set { phaseOffset = value; ApplySettings(); }
        }

        public float TrailLength
        {
            get => trailLength;
            set { trailLength = Mathf.Max(0f, value); ApplySettings(); }
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void OnEnable()
        {
            ApplySettings();
        }

        /// <summary>
        /// 把组件配置写给 ImageEx。运行时由 Awake/OnEnable 调用；
        /// 编辑器预览与 EditMode 测试可手动调用。
        /// </summary>
        public void ApplySettings()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (image == null) return;

            image.EnableDotMatrix = true;
            image.DotPattern = pattern;
            image.DotShape = shape;
            image.DotCell = cell;
            image.DotRadius = radius;
            image.DotColor = color;
            image.DotIntensity = intensity;
            image.DotSpeed = speed;
            image.DotInactiveAlpha = inactiveAlpha;
            image.DotInactiveScale = inactiveScale;
            image.DotEdgeSoftness = edgeSoftness;
            image.DotPhaseOffset = phaseOffset;
            image.DotTrailLength = trailLength;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cell = Mathf.Max(4f, cell);
            radius = Mathf.Clamp(radius, 0f, 0.5f);
            intensity = Mathf.Clamp01(intensity);
            if (image == null) image = GetComponent<ImageEx>();
            if (image != null) ApplySettings();
        }
#endif
    }
}
