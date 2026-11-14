using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 地形等值线：在 ImageEx 上叠一层程序化噪声等高线（无贴图），可缓慢滚动。
    /// <para>
    /// 适合科技面板、地图底纹、HUD 背景；只加色不改 alpha，可与图案/点阵/幕帘叠加。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Topographic")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageEx))]
    public sealed class GearTopographic : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float scale = 3f;
        [SerializeField, Min(1f)] private float density = 6f;
        [SerializeField, Range(0.001f, 0.5f)] private float lineWidth = 0.05f;
        [SerializeField, Min(1f)] private float majorEvery = 5f;
        [SerializeField] private Color color = new Color(0.14f, 0.75f, 0.95f, 1f);
        [SerializeField, Range(0f, 1f)] private float intensity = 0.6f;
        [SerializeField] private float speed = 0.2f;
        [SerializeField, Range(0f, 1f)] private float falloff;

        private ImageEx image;

        public float Scale
        {
            get => scale;
            set { scale = Mathf.Max(0.1f, value); ApplySettings(); }
        }

        public float Density
        {
            get => density;
            set { density = Mathf.Max(1f, value); ApplySettings(); }
        }

        public float LineWidth
        {
            get => lineWidth;
            set { lineWidth = Mathf.Clamp(value, 0.001f, 0.5f); ApplySettings(); }
        }

        public float MajorEvery
        {
            get => majorEvery;
            set { majorEvery = Mathf.Max(1f, value); ApplySettings(); }
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

        public float Falloff
        {
            get => falloff;
            set { falloff = Mathf.Clamp01(value); ApplySettings(); }
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void OnEnable()
        {
            ApplySettings();
        }

        /// <summary>把组件配置写给 ImageEx；编辑器/测试可手动调用。</summary>
        public void ApplySettings()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (image == null) return;

            image.EnableTopographic = true;
            image.TopoScale = scale;
            image.TopoDensity = density;
            image.TopoLineWidth = lineWidth;
            image.TopoMajorEvery = majorEvery;
            image.TopoColor = color;
            image.TopoIntensity = intensity;
            image.TopoSpeed = speed;
            image.TopoFalloff = falloff;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            scale = Mathf.Max(0.1f, scale);
            density = Mathf.Max(1f, density);
            lineWidth = Mathf.Clamp(lineWidth, 0.001f, 0.5f);
            majorEvery = Mathf.Max(1f, majorEvery);
            intensity = Mathf.Clamp01(intensity);
            falloff = Mathf.Clamp01(falloff);
            if (image == null) image = GetComponent<ImageEx>();
            if (image != null) ApplySettings();
        }
#endif
    }
}
