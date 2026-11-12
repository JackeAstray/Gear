using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Dot Matrix Loader"), HelpURL(Constants.HelpUrl)]
    public class DotMatrixLoader : Image
    {
        // Settings
        [SerializeField] private PatternType pattern = PatternType.Wave;
        [SerializeField] private DotShape dotShape = DotShape.Circle;
        [SerializeField] private Vector2Int gridSize = new(8, 8);
        [SerializeField, Range(0.05f, 1f)] private float dotSize = 0.6f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.05f;
        [SerializeField, Range(0f, 1f)] private float inactiveScale = 1f;
        [SerializeField, Range(0.1f, 2f)] private float edgeSoftness = 1f;

        // Animation
        [Tooltip("Cycles per second. Negative values reverse the animation; zero holds the phase offset.")]
        [SerializeField, Range(-5f, 5f)] private float speed = 1f;
        [SerializeField, Range(0f, 1f)] private float phaseOffset;
        [SerializeField, Range(0.05f, 1f)] private float trailLength = 0.5f;

        // Enums
        public enum PatternType { Wave, Rings, Radar, Spiral, Snake, Equalizer, Checkerboard, Pulse }
        public enum DotShape { Circle, Square, Diamond }

        // Shader
        [SerializeField, HideInInspector] private Shader embeddedShader;
        const string ShaderName = "Evo/UI/Dot Matrix Loader";
        static readonly int GridSizeID = Shader.PropertyToID("_GridSize");
        static readonly int DotParamsID = Shader.PropertyToID("_DotParams");
        static readonly int AnimationParamsID = Shader.PropertyToID("_AnimationParams");
        static readonly int PatternID = Shader.PropertyToID("_Pattern");
        static readonly int DotShapeID = Shader.PropertyToID("_DotShape");
        static readonly int ExternalAlphaID = Shader.PropertyToID("_EnableExternalAlpha");

        // Cache
        Material instancedMaterial;

        // Properties
        public PatternType Pattern
        {
            get => pattern;
            set
            {
                if (pattern == value)
                    return;

                pattern = value;
                SetMaterialDirty();
            }
        }

        public DotShape Shape
        {
            get => dotShape;
            set
            {
                if (dotShape == value)
                    return;

                dotShape = value;
                SetMaterialDirty();
            }
        }

        public Vector2Int GridSize
        {
            get => gridSize;
            set
            {
                value = new Vector2Int(Mathf.Clamp(value.x, 1, 64), Mathf.Clamp(value.y, 1, 64));
                if (gridSize == value)
                    return;

                gridSize = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public float DotSize
        {
            get => dotSize;
            set => SetFloat(ref dotSize, Mathf.Clamp(value, 0.05f, 1f));
        }

        public float InactiveAlpha
        {
            get => inactiveAlpha;
            set => SetFloat(ref inactiveAlpha, Mathf.Clamp01(value));
        }

        public float InactiveScale
        {
            get => inactiveScale;
            set => SetFloat(ref inactiveScale, Mathf.Clamp01(value));
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set => SetFloat(ref edgeSoftness, Mathf.Clamp(value, 0.1f, 2f));
        }

        public float Speed
        {
            get => speed;
            set => SetFloat(ref speed, Mathf.Clamp(value, -5f, 5f));
        }

        public float PhaseOffset
        {
            get => phaseOffset;
            set => SetFloat(ref phaseOffset, Mathf.Clamp01(value));
        }

        public float TrailLength
        {
            get => trailLength;
            set => SetFloat(ref trailLength, Mathf.Clamp(value, 0.05f, 1f));
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
                instancedMaterial = new Material(embeddedShader) { hideFlags = HideFlags.HideAndDontSave };
            if (instancedMaterial == null)
                Debug.LogWarning("[Evo UI] Shader 'Evo/UI/Dot Matrix Loader' not found.", this);

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

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

            Rect rect = GetPixelAdjustedRect();
            int columns = Mathf.Clamp(gridSize.x, 1, 64);
            int rows = Mathf.Clamp(gridSize.y, 1, 64);
            float cellSize = Mathf.Max(0.0001f, Mathf.Min(rect.width / columns, rect.height / rows));
            float inverseCellSize = 1f / cellSize;
            Vector2 center = rect.center;
            UIVertex vertex = default;

            // Canvas batching transforms positions; UV0.zw keeps the grid local to this image
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.uv0.z = (vertex.position.x - center.x) * inverseCellSize + columns * 0.5f;
                vertex.uv0.w = (vertex.position.y - center.y) * inverseCellSize + rows * 0.5f;
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

            // A Mask on this image also needs matching stencil cleanup geometry
            for (int i = 0; i < canvasRenderer.popMaterialCount; i++)
                ApplyMaterialProperties(canvasRenderer.GetPopMaterial(i), alphaTexture != null);
        }

        void ApplyMaterialProperties(Material target, bool externalAlpha)
        {
            if (target == null || instancedMaterial == null || target.shader != instancedMaterial.shader)
                return;

            int columns = Mathf.Clamp(gridSize.x, 1, 64);
            int rows = Mathf.Clamp(gridSize.y, 1, 64);

            target.SetVector(GridSizeID, new Vector4(columns, rows, 1f / Mathf.Max(Mathf.Max(columns, rows) - 1, 1), 1f / (columns * rows)));
            target.SetVector(DotParamsID, new Vector4(Mathf.Clamp(dotSize, 0.05f, 1f), Mathf.Clamp01(inactiveAlpha),
                Mathf.Clamp01(inactiveScale), Mathf.Clamp(edgeSoftness, 0.1f, 2f)));
            target.SetVector(AnimationParamsID, new Vector4(Mathf.Clamp(speed, -5f, 5f), Mathf.Clamp01(phaseOffset),
                Mathf.Clamp(trailLength, 0.05f, 1f), 0f));
            target.SetFloat(PatternID, (int)pattern);
            target.SetFloat(DotShapeID, (int)dotShape);
            target.SetFloat(ExternalAlphaID, externalAlpha ? 1f : 0f);
        }

        void SetFloat(ref float field, float value)
        {
            if (field == value || float.IsNaN(value) || float.IsInfinity(value))
                return;

            field = value;
            SetMaterialDirty();
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
            gridSize = new Vector2Int(Mathf.Clamp(gridSize.x, 1, 64), Mathf.Clamp(gridSize.y, 1, 64));
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);

            base.OnValidate();
        }
#endif
    }
}