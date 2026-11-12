using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Topographic Skin"), HelpURL(Constants.HelpUrl + "effects/topographic-skin")]
    public class TopographicSkin : Image
    {
        // Pattern
        [Tooltip("Size of the landforms. Larger values spread the same contours over a bigger area.")]
        [SerializeField, Range(0.1f, 10f)] private float scale = 1f;
        [Tooltip("Number of contour levels. Higher values pack the lines closer together.")]
        [SerializeField, Range(1f, 100f)] private float density = 30f;
        [Tooltip("Selects a different part of the pattern.")]
        [SerializeField, Range(0f, 100f)] private float seed;

        // Lines
        [Tooltip("Contour line width in canvas units.")]
        [SerializeField, Range(0.1f, 20f)] private float lineWidth = 3f;
        [Tooltip("Draws every Nth contour as a major line. Zero disables major lines.")]
        [SerializeField, Range(0, 10)] private int majorLineInterval;
        [Tooltip("Major contour line width in canvas units.")]
        [SerializeField, Range(0.1f, 20f)] private float majorLineWidth = 4f;
        [Tooltip("Antialiasing width in screen pixels.")]
        [SerializeField, Range(0.1f, 4f)] private float edgeSoftness = 1f;

        // Falloff
        [Tooltip("Fades the lines with distance from an anchor in the rect.")]
        [SerializeField] private bool falloff;
        [Tooltip("Anchor clamped to 0-1: (0, 0) is bottom left; (1, 1) is top right.")]
        [SerializeField] private Vector2 falloffOrigin = new(0.5f, 0.5f);
        [Tooltip("Distance in canvas units where the fade begins.")]
        [SerializeField, Min(0f)] private float falloffStart;
        [Tooltip("Distance in canvas units where the lines reach Outside Opacity.")]
        [SerializeField, Min(0.001f)] private float falloffEnd = 500f;
        [Tooltip("Width divided by height of the falloff ellipse. 1 is a circle; 2 reaches twice as far horizontally.")]
        [SerializeField, Range(0.1f, 10f)] private float falloffAspect = 1f;
        [Tooltip("Line opacity beyond End Distance. Zero hides the lines there.")]
        [SerializeField, Range(0f, 1f)] private float falloffOutsideOpacity;

        // Animation
        [Tooltip("Unscaled evolution speed. Keeps moving when Time.timeScale is zero.")]
        [SerializeField, Range(0f, 1f)] private float speed = 0.02f;

        // Noise cells per canvas unit at scale 1 (a landform spans about 330 units) and contour
        // levels per unit of noise height for each point of density
        const float BaseFrequency = 0.003f;
        const float LevelsPerDensity = 0.5f;

        // Shader
        [SerializeField, HideInInspector] private Shader embeddedShader;
        const string ShaderName = "Evo/UI/Topographic Skin";
        static readonly int PatternID = Shader.PropertyToID("_Pattern");
        static readonly int LinesID = Shader.PropertyToID("_Lines");
        static readonly int SeedID = Shader.PropertyToID("_Seed");
        static readonly int FalloffOriginID = Shader.PropertyToID("_FalloffOrigin");
        static readonly int FalloffRangeID = Shader.PropertyToID("_FalloffRange");
        static readonly int ExternalAlphaID = Shader.PropertyToID("_EnableExternalAlpha");
        const string FalloffKeyword = "EVO_TOPOGRAPHIC_FALLOFF";

        // Cache
        Material instancedMaterial;
        bool clockRegistered;
        bool falloffKeywordUsed;

        // Properties
        public float Scale
        {
            get => scale;
            set => SetFloat(ref scale, Mathf.Clamp(value, 0.1f, 10f));
        }

        public float Density
        {
            get => density;
            set => SetFloat(ref density, Mathf.Clamp(value, 1f, 100f));
        }

        public float Seed
        {
            get => seed;
            set => SetFloat(ref seed, Mathf.Clamp(value, 0f, 100f));
        }

        public float LineWidth
        {
            get => lineWidth;
            set => SetFloat(ref lineWidth, Mathf.Clamp(value, 0.1f, 20f));
        }

        public int MajorLineInterval
        {
            get => majorLineInterval;
            set
            {
                value = Mathf.Clamp(value, 0, 10);
                if (majorLineInterval == value)
                    return;

                majorLineInterval = value;
                SetMaterialDirty();
            }
        }

        public float MajorLineWidth
        {
            get => majorLineWidth;
            set => SetFloat(ref majorLineWidth, Mathf.Clamp(value, 0.1f, 20f));
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set => SetFloat(ref edgeSoftness, Mathf.Clamp(value, 0.1f, 4f));
        }

        public bool Falloff
        {
            get => falloff;
            set
            {
                if (falloff == value)
                    return;

                falloff = value;
                SetMaterialDirty();
            }
        }

        public Vector2 FalloffOrigin
        {
            get => falloffOrigin;
            set
            {
                bool changed = TrySet(ref falloffOrigin.x, Mathf.Clamp01(value.x));
                changed |= TrySet(ref falloffOrigin.y, Mathf.Clamp01(value.y));

                if (changed)
                    UpdateFalloff();
            }
        }

        public float FalloffStart
        {
            get => falloffStart;
            set => SetFalloffFloat(ref falloffStart, Mathf.Max(0f, value));
        }

        public float FalloffEnd
        {
            get => falloffEnd;
            set => SetFalloffFloat(ref falloffEnd, Mathf.Max(0.001f, value));
        }

        public float FalloffAspect
        {
            get => falloffAspect;
            set => SetFalloffFloat(ref falloffAspect, Mathf.Clamp(value, 0.1f, 10f));
        }

        public float FalloffOutsideOpacity
        {
            get => falloffOutsideOpacity;
            set => SetFalloffFloat(ref falloffOutsideOpacity, Mathf.Clamp01(value));
        }

        public float Speed
        {
            get => speed;
            set => SetFloat(ref speed, Mathf.Clamp01(value));
        }

        // Image Overrides
        public override Material defaultMaterial => instancedMaterial != null ? instancedMaterial : base.defaultMaterial;
        public override Material material
        {
            get => m_Material != null ? m_Material : defaultMaterial;
            set => base.material = value;
        }

        protected override void OnEnable()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
            if (instancedMaterial == null && embeddedShader != null)
            {
                instancedMaterial = new Material(embeddedShader) { name = "Topographic Skin (Instance)", hideFlags = HideFlags.HideAndDontSave };
                falloffKeywordUsed = false;
            }
            if (instancedMaterial == null)
                Debug.LogWarning("[Evo UI] Shader 'Evo/UI/Topographic Skin' not found.", this);

            base.OnEnable();

            if (!clockRegistered)
            {
                Clock.Register();
                clockRegistered = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (clockRegistered)
            {
                Clock.Unregister();
                clockRegistered = false;
            }

            if (instancedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(instancedMaterial);
                else
                    DestroyImmediate(instancedMaterial);

                instancedMaterial = null;
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            base.OnPopulateMesh(helper);

            Vector2 center = GetPixelAdjustedRect().center;
            UIVertex vertex = default;

            // UV0.zw carries rect-local positions, so the pattern moves, scales and rotates with the graphic
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.uv0.z = vertex.position.x - center.x;
                vertex.uv0.w = vertex.position.y - center.y;
                helper.SetUIVertex(vertex, i);
            }
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            Sprite activeSprite = overrideSprite;
            Texture2D alphaTexture = activeSprite != null ? activeSprite.associatedAlphaSplitTexture : null;
            ApplyMaterialProperties(instancedMaterial, alphaTexture != null);
            base.UpdateMaterial();
            canvasRenderer.SetAlphaTexture(alphaTexture);

            // A custom material owns its settings; never modify a shared user asset
            if (material != instancedMaterial)
                return;

            // Mask and SoftMask can return cached copies of the source material
            Material renderedMaterial = canvasRenderer.GetMaterial(0);
            if (renderedMaterial != instancedMaterial)
                ApplyMaterialProperties(renderedMaterial, alphaTexture != null);

            // A Mask on this graphic also needs matching stencil cleanup geometry
            for (int i = 0; i < canvasRenderer.popMaterialCount; i++)
                ApplyMaterialProperties(canvasRenderer.GetPopMaterial(i), alphaTexture != null);
        }

        void ApplyMaterialProperties(Material target, bool externalAlpha)
        {
            if (target == null || instancedMaterial == null || target.shader != instancedMaterial.shader)
                return;

            float seedValue = Mathf.Clamp(seed, 0f, 100f);
            target.SetVector(PatternID, new Vector4(BaseFrequency / Mathf.Clamp(scale, 0.1f, 10f),
                Mathf.Clamp(density, 1f, 100f) * LevelsPerDensity, Mathf.Clamp01(speed), 0f));
            int interval = Mathf.Clamp(majorLineInterval, 0, 10);
            target.SetVector(LinesID, new Vector4(Mathf.Clamp(lineWidth, 0.1f, 20f), Mathf.Clamp(majorLineWidth, 0.1f, 20f),
                1f / Mathf.Clamp(edgeSoftness, 0.1f, 4f), interval > 0 ? 1f / interval : 0f));
            // The base offset keeps the default slice off lattice points, where the noise flattens out
            target.SetVector(SeedID, new Vector4(0.37f + seedValue * 2.718f, 0.71f + seedValue * 1.618f, 0.43f + seedValue * 1.414f, 0f));
            target.SetFloat(ExternalAlphaID, externalAlpha ? 1f : 0f);

            // Materials copied from this instance only carry the keyword once it was enabled, so skins that never
            // used the falloff skip the keyword lookup
            if (!falloff)
            {
                if (falloffKeywordUsed)
                    target.DisableKeyword(FalloffKeyword);
                return;
            }

            GetFalloffVectors(out Vector4 origin, out Vector4 range);
            target.SetVector(FalloffOriginID, origin);
            target.SetVector(FalloffRangeID, range);
            target.EnableKeyword(FalloffKeyword);
            falloffKeywordUsed = true;
        }

        /// <summary>
        /// Moves and resizes the falloff with one material write. Cheap enough to call every frame.
        /// </summary>
        public void SetFalloff(Vector2 origin, float start, float end, float aspect)
        {
            bool changed = TrySet(ref falloffOrigin.x, Mathf.Clamp01(origin.x));
            changed |= TrySet(ref falloffOrigin.y, Mathf.Clamp01(origin.y));
            changed |= TrySet(ref falloffStart, Mathf.Max(0f, start));
            changed |= TrySet(ref falloffEnd, Mathf.Max(0.001f, end));
            changed |= TrySet(ref falloffAspect, Mathf.Clamp(aspect, 0.1f, 10f));

            if (changed)
                UpdateFalloff();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            // The shader takes the origin in canvas units, so it follows the rect size
            UpdateFalloff();
        }

        // Falloff values only change uniforms. Writing them to the materials this skin renders with skips
        // SetMaterialDirty and the canvas rebatch that comes with it.
        void UpdateFalloff()
        {
            if (!falloff || instancedMaterial == null)
                return;

            GetFalloffVectors(out Vector4 origin, out Vector4 range);
            instancedMaterial.SetVector(FalloffOriginID, origin);
            instancedMaterial.SetVector(FalloffRangeID, range);

            // Same rule as UpdateMaterial: a custom material owns its settings
            CanvasRenderer graphicRenderer = canvasRenderer;
            if (!IsActive() || material != instancedMaterial || graphicRenderer.materialCount == 0)
                return;

            WriteFalloff(graphicRenderer.GetMaterial(0), origin, range);

            for (int i = 0; i < graphicRenderer.popMaterialCount; i++)
                WriteFalloff(graphicRenderer.GetPopMaterial(i), origin, range);
        }

        void WriteFalloff(Material target, Vector4 origin, Vector4 range)
        {
            if (target == null || target == instancedMaterial || target.shader != instancedMaterial.shader)
                return;

            target.SetVector(FalloffOriginID, origin);
            target.SetVector(FalloffRangeID, range);
        }

        void GetFalloffVectors(out Vector4 origin, out Vector4 range)
        {
            // UV0.zw counts from the rect center, so the origin only depends on the rect size. The vertex stage divides
            // horizontal distances by the aspect with one multiply-add: local * origin.zw - origin.xy.
            Vector2 size = GetPixelAdjustedRect().size;
            float inverseAspect = 1f / Mathf.Clamp(falloffAspect, 0.1f, 10f);
            origin = new Vector4((Mathf.Clamp01(falloffOrigin.x) - 0.5f) * size.x * inverseAspect,
                (Mathf.Clamp01(falloffOrigin.y) - 0.5f) * size.y, inverseAspect, 1f);

            // The fragment stage fades by smoothstep(saturate(distance * range.y + range.x)) * range.z
            float start = Mathf.Max(0f, falloffStart);
            float inverseWidth = 1f / Mathf.Max(falloffEnd - start, 0.001f);
            range = new Vector4(-start * inverseWidth, inverseWidth, 1f - Mathf.Clamp01(falloffOutsideOpacity), 0f);
        }

        void SetFloat(ref float field, float value)
        {
            if (TrySet(ref field, value))
                SetMaterialDirty();
        }

        void SetFalloffFloat(ref float field, float value)
        {
            if (TrySet(ref field, value))
                UpdateFalloff();
        }

        static bool TrySet(ref float field, float value)
        {
            if (field == value || float.IsNaN(value) || float.IsInfinity(value))
                return false;

            field = value;
            return true;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
            embeddedShader = Shader.Find(ShaderName);
        }

        protected override void OnValidate()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);

            base.OnValidate();
        }
#endif

        /// <summary>
        /// One global float per frame for every skin. Evolution never dirties meshes or materials.
        /// </summary>
        [NoAutoStaticsCleanup]
        static class Clock
        {
            static readonly int TimeID = Shader.PropertyToID("_EvoTopographicTime");
            static int users;
            static int frame = -1;
            static bool playing;
            static double epoch;

            public static void Register()
            {
                if (users++ != 0)
                    return;

                Canvas.preWillRenderCanvases += Tick;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.update += Tick;
#endif
                Restart();
            }

            public static void Unregister()
            {
                users = Mathf.Max(0, users - 1);
                if (users != 0)
                    return;

                Canvas.preWillRenderCanvases -= Tick;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.update -= Tick;
#endif
            }

            static void Restart()
            {
                // Counting from the first visible skin keeps the float time small and precise
                playing = Application.isPlaying;
                epoch = Now();
                frame = -1;
                Tick();
            }

            static void Tick()
            {
                if (playing != Application.isPlaying)
                {
                    Restart();
                    return;
                }

                if (playing && frame == Time.frameCount)
                    return;

                frame = Time.frameCount;
                Shader.SetGlobalFloat(TimeID, (float)(Now() - epoch));
            }

            static double Now()
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return UnityEditor.EditorApplication.timeSinceStartup;
#endif
                return Time.unscaledTimeAsDouble;
            }
        }
    }
}