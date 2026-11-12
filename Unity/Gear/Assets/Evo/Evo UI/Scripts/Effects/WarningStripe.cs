using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Warning Stripe"), HelpURL(Constants.HelpUrl + "effects/warning-stripe")]
    public class WarningStripe : Image
    {
        // Settings
        [SerializeField, Range(0.01f, 0.5f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.01f, 1f)] private float lineSpacing = 0.2f;
        [SerializeField, Range(-180f, 180f)] private float lineAngle = 25f;
        [SerializeField, Range(0.1f, 4f)] private float edgeSoftness = 1.5f;
        [SerializeField, Range(-5f, 5f)] private float scrollSpeed = 0.25f;

        // Shader
        [SerializeField, HideInInspector] private Shader embeddedShader;
        const string ShaderName = "Evo/UI/Warning Stripe";
        static readonly int LineWidthID = Shader.PropertyToID("_LineWidth");
        static readonly int LineSpacingID = Shader.PropertyToID("_LineSpacing");
        static readonly int LineAngleID = Shader.PropertyToID("_LineAngle");
        static readonly int EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");
        static readonly int ScrollSpeedID = Shader.PropertyToID("_ScrollSpeed");
        static readonly int ExternalAlphaID = Shader.PropertyToID("_EnableExternalAlpha");

        // Cache
        Material instancedMaterial;

        // Properties
        public float LineWidth
        {
            get => lineWidth;
            set => SetFloat(ref lineWidth, Mathf.Clamp(value, 0.01f, 0.5f));
        }

        public float LineSpacing
        {
            get => lineSpacing;
            set => SetFloat(ref lineSpacing, Mathf.Clamp(value, 0.01f, 1f));
        }

        public float LineAngle
        {
            get => lineAngle;
            set => SetFloat(ref lineAngle, Mathf.Clamp(value, -180f, 180f));
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set => SetFloat(ref edgeSoftness, Mathf.Clamp(value, 0.1f, 4f));
        }

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set => SetFloat(ref scrollSpeed, Mathf.Clamp(value, -5f, 5f));
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
                Debug.LogWarning("[Evo UI] Shader 'Evo/UI/Warning Stripe' not found.", this);

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

            target.SetFloat(LineWidthID, Mathf.Max(0.01f, lineWidth));
            target.SetFloat(LineSpacingID, Mathf.Max(0.01f, lineSpacing));
            target.SetFloat(LineAngleID, lineAngle);
            target.SetFloat(EdgeSoftnessID, Mathf.Clamp(edgeSoftness, 0.1f, 4f));
            target.SetFloat(ScrollSpeedID, scrollSpeed);
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
            embeddedShader = Shader.Find(ShaderName);
        }

        protected override void OnValidate()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);

            base.OnValidate();
        }
#endif
    }
}