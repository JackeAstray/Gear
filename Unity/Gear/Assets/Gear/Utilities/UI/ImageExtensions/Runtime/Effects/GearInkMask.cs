using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 墨团遮罩：把 <see cref="GearInkPanel"/> 的覆盖度烘焙到一张 RenderTexture，
    /// 由同物体上的 <see cref="RawImage"/> + <see cref="Mask"/> 用模板缓冲真正裁剪所有子物体。
    /// <para>
    /// 与旧的「只乘 ImageEx 自身 alpha」不同，这里子树里的 Image / TMP / 嵌套 ImageEx 都会被同一张墨迹遮住。
    /// RT 按需重建（尺寸变化才重建），参数没变时不重复烘焙；OnDisable 释放。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Ink Mask")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(Mask))]
    public sealed class GearInkMask : MonoBehaviour
    {
        private static readonly int progressId = Shader.PropertyToID("_Progress");
        private static readonly int originId = Shader.PropertyToID("_Origin");
        private static readonly int softnessId = Shader.PropertyToID("_Softness");
        private static readonly int roughnessId = Shader.PropertyToID("_Roughness");
        private static readonly int aspectId = Shader.PropertyToID("_Aspect");

        [SerializeField] private Shader shader;
        [SerializeField] private Vector2 origin = new Vector2(0.5f, 0.5f);
        [SerializeField, Range(0.001f, 0.5f)] private float softness = 0.08f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.35f;
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField, Min(0.01f)] private float duration = 0.7f;
        [SerializeField, Min(32)] private int resolution = 256;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop;
        [SerializeField, Min(0.05f)] private float loopInterval = 1.2f;

        private RawImage output;
        private Mask mask;
        private Material material;
        private RenderTexture texture;
        private int textureWidth;
        private int textureHeight;
        private float current = 1f;
        private float target = 1f;
        private bool animating;
        private bool dirty = true;
        private float wait = -1f;

        /// <summary>当前进度（0 隐藏，1 显示）；写入会跳过动画。</summary>
        public float Progress
        {
            get => current;
            set
            {
                progress = Mathf.Clamp01(value);
                current = progress;
                target = current;
                animating = false;
                dirty = true;
                if (Application.isPlaying) Bake();
            }
        }

        public RenderTexture Texture => texture;

        /// <summary>烘焙用 Shader；为空时回退到 <c>Shader.Find("Gear/UI/InkCoverage")</c>。</summary>
        public Shader SourceShader { get => shader; set { shader = value; dirty = true; } }

        public Vector2 Origin
        {
            get => origin;
            set { origin = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y)); dirty = true; }
        }

        public float Softness
        {
            get => softness;
            set { softness = Mathf.Clamp(value, 0.001f, 0.5f); dirty = true; }
        }

        public float Roughness
        {
            get => roughness;
            set { roughness = Mathf.Clamp01(value); dirty = true; }
        }

        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.01f, value);
        }

        private void Awake()
        {
            output = GetComponent<RawImage>();
            mask = GetComponent<Mask>();
            if (output != null) output.raycastTarget = false;
            if (mask != null)
            {
                mask.showMaskGraphic = false;
                mask.enabled = true;
            }

            current = Mathf.Clamp01(progress);
            target = current;
        }

        private void OnEnable()
        {
            current = Mathf.Clamp01(progress);
            target = current;
            ApplyParameters();
            Bake(force: true);
            if (!playOnEnable) return;

            Step(0f);
            if (loop) wait = loopInterval;
        }

        private void OnDisable()
        {
            ReleaseTexture();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }

        private void Update()
        {
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>播放显示。</summary>
        public void Show()
        {
            target = 1f;
            animating = true;
            wait = -1f;
        }

        /// <summary>播放隐藏。</summary>
        public void Hide()
        {
            target = 0f;
            animating = true;
            wait = -1f;
        }

        /// <summary>直接设置进度（0-1），不播放动画。</summary>
        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            current = progress;
            target = current;
            animating = false;
            wait = -1f;
            dirty = true;
            Bake();
        }

        /// <summary>手动推进（测试 / 暂停时驱动）。</summary>
        public void Step(float delta)
        {
            if (loop && wait > 0f)
            {
                wait -= delta;
                if (wait <= 0f)
                {
                    target = current >= 1f ? 0f : 1f;
                    animating = true;
                    wait = -1f;
                }
            }

            if (animating && delta > 0f)
            {
                current = Mathf.MoveTowards(current, target, delta / Mathf.Max(0.01f, duration));
                progress = current;
                dirty = true;
                if (Mathf.Approximately(current, target))
                {
                    animating = false;
                    if (loop) wait = loopInterval;
                }
            }

            Bake();
        }

        /// <summary>
        /// 烘焙当前覆盖度到 RT；尺寸变化才重建纹理，参数不变时直接跳过。
        /// </summary>
        public void Bake(bool force = false)
        {
            if (output == null) output = GetComponent<RawImage>();
            if (output == null) return;

            EnsureMaterial();
            if (material == null) return;

            EnsureTexture();
            if (texture == null) return;

            if (!force && !dirty) return;
            dirty = false;

            material.SetFloat(progressId, current);
            material.SetVector(originId, new Vector4(origin.x, origin.y, 0f, 0f));
            material.SetFloat(softnessId, softness);
            material.SetFloat(roughnessId, roughness);
            material.SetFloat(aspectId, texture.height > 0 ? (float)texture.width / texture.height : 1f);

            Graphics.Blit(Texture2D.whiteTexture, texture, material);
            output.texture = texture;
            output.material = material;
            output.color = Color.white;
        }

        /// <summary>与 shader 保持一致的覆盖度（0-1），用于用例与外部查询。</summary>
        public static float ComputeCoverage(Vector2 uv, float progress, Vector2 origin, float softness,
            float roughness, float aspect)
        {
            aspect = Mathf.Max(0.001f, aspect);
            Vector2 point = new Vector2(uv.x * aspect, uv.y);
            Vector2 pivot = new Vector2(origin.x * aspect, origin.y);
            float distance = Vector2.Distance(point, pivot);
            float noise = (Hash(uv * 7f) - 0.5f) * roughness * 0.5f;
            distance = Mathf.Max(distance + noise, 0f);
            float threshold = Mathf.Clamp01(progress) * 1.45f;
            float soft = Mathf.Max(softness, 0.001f);
            return Mathf.Clamp01(1f - SmoothStep(threshold - soft, threshold + soft, distance));
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(Vector2 p)
        {
            float value = Mathf.Sin(Vector2.Dot(p, new Vector2(127.1f, 311.7f))) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        private void ApplyParameters()
        {
            dirty = true;
        }

        private void EnsureMaterial()
        {
            if (material != null) return;
            Shader source = shader != null ? shader : Shader.Find("Gear/UI/InkCoverage");
            if (source == null) return;
            material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void EnsureTexture()
        {
            if (output == null) return;

            int width = Mathf.Max(32, Mathf.RoundToInt(resolution * Mathf.Max(0.01f, output.rectTransform.rect.width / Mathf.Max(1f, output.rectTransform.rect.height))));
            int height = Mathf.Max(32, resolution);
            if (width > 2048) width = 2048;
            if (height > 2048) height = 2048;

            if (texture != null && textureWidth == width && textureHeight == height) return;

            ReleaseTexture();
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "GearInkMask",
            };
            texture.Create();
            textureWidth = width;
            textureHeight = height;
        }

        private void ReleaseTexture()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
            textureWidth = 0;
            textureHeight = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            origin = new Vector2(Mathf.Clamp01(origin.x), Mathf.Clamp01(origin.y));
            softness = Mathf.Clamp(softness, 0.001f, 0.5f);
            roughness = Mathf.Clamp01(roughness);
            progress = Mathf.Clamp01(progress);
            dirty = true;
        }
#endif
    }
}
