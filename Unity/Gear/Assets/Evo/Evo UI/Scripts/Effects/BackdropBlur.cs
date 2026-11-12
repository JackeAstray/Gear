using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// An Image whose sprite alpha masks a shared, filtered camera capture.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Evo/UI/Effects/Backdrop Blur"), HelpURL(Constants.HelpUrl)]
    public sealed class BackdropBlur : Image
    {
        public Settings settings = new();
        public enum Resolution { Full = 1, Half = 2, Quarter = 4, Eighth = 8 }

        // Shader Dependency
        [SerializeField, HideInInspector] Shader embeddedShader;
        const string ShaderName = "Evo/UI/Backdrop Blur";

        // State
        BackdropBlurSource source;

        // Properties
        public bool IsReady => source != null && source.IsReady;
        public bool CaptureBeforeTransparents => BackdropBlurSource.BeforeTransparents(this);
        public RenderTexture CaptureTexture => source?.Texture;
        public Camera ResolvedCamera => settings.ResolveCamera(this);
        public override Material defaultMaterial => source?.GetMaterial(null, embeddedShader) ?? base.defaultMaterial;

        protected override void OnEnable()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
           
            base.OnEnable();
            Refresh();
        }

        protected override void OnDisable()
        {
            ReleaseSource();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseSource();
            base.OnDestroy();
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();
            if (m_Material == null)
                source?.BindRenderedMaterials();
        }

        /// <summary>
        /// Apply settings immediately. Call after enabling previously disabled settings at runtime.
        /// </summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled || settings == null || !settings.enabled)
            {
                ReleaseSource();
                return;
            }

            if (source == null)
            {
                source = new BackdropBlurSource(this);
                Canvas.preWillRenderCanvases += Refresh;
            }

            source.Refresh(settings, m_Material == null);
        }

        void ReleaseSource()
        {
            if (source == null)
                return;

            Canvas.preWillRenderCanvases -= Refresh;
            source.Dispose();
            source = null;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            embeddedShader = Shader.Find(ShaderName);
            settings.UpdateShaderReference();
        }

        protected override void OnValidate()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
           
            settings?.Validate(this, Refresh);
            base.OnValidate();
        }
#endif

        /// <summary>
        /// Capture, appearance and optional shader dependency shared by images and procedural fills.
        /// </summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("Blurs the camera image behind this graphic. Overlay canvas UI elements are excluded." +
                "\n\nAdds capture and filtering passes, which can significantly increase GPU cost and draw/SetPass calls." +
                "\n\nHigher resolutions and more iterations increase the cost.")]
            public bool enabled = true;
           
            [Tooltip("Defaults to the canvas camera, then Main Camera. Assign explicitly for multiple cameras or displays.")]
            public Camera sourceCamera;
            
            [Tooltip("Resolution of the shared camera capture. Lower resolutions reduce GPU work and texture memory.")]
            public Resolution resolution = Resolution.Half;
           
            [Min(1), Tooltip("Capture every N camera renders. 1 is smoothest; higher values save GPU time.")]
            public int updateInterval = 1;
            
            [Range(1, 5), Tooltip("More levels produce a wider, smoother blur, but increase batching.")]
            public int iterations = 2;
           
            [Range(0.5f, 6f), Tooltip("Kawase blur sample spacing.")]
            public float radius = 3f;
           
            [Range(0f, 2f), Tooltip("Color saturation. 0 is grayscale; 1 preserves the captured colors.")]
            public float saturation = 1f;
           
            [Range(0f, 4f), Tooltip("Captured color multiplier. 1 preserves the original brightness.")]
            public float brightness = 1f;
           
            [Range(0f, 0.1f), Tooltip("Screen-space grain strength, useful for reducing visible color banding.")]
            public float noise;

            // Shader Dependency
            [SerializeField, HideInInspector] Shader shader;
            const string ShaderName = "Hidden/Evo/UI/Kawase Blur";
            internal Shader FilterShader => shader != null ? shader : Shader.Find(ShaderName);

            public Camera ResolveCamera(Graphic owner) => sourceCamera != null ? sourceCamera
                : owner.canvas != null && owner.canvas.rootCanvas.worldCamera != null
                    ? owner.canvas.rootCanvas.worldCamera : Camera.main;

            internal Vector3 Adjustments => new(
                BackdropBlurCapture.ClampFinite(brightness, 0f, 4f, 1f),
                BackdropBlurCapture.ClampFinite(saturation, 0f, 2f, 1f),
                BackdropBlurCapture.ClampFinite(noise, 0f, 0.1f, 0f));

#if UNITY_EDITOR
            [NonSerialized] UnityEditor.EditorApplication.CallbackFunction pendingValidation;

            public void Validate(UnityEngine.Object owner, Action refresh = null)
            {
                if (pendingValidation != null)
                    UnityEditor.EditorApplication.delayCall -= pendingValidation;

                pendingValidation = () =>
                {
                    pendingValidation = null;
                    if (owner == null)
                        return;

                    if (!Application.isPlaying && UpdateShaderReference())
                    {
                        UnityEditor.EditorUtility.SetDirty(owner);
                        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(owner))
                            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                    }
                    refresh?.Invoke();
                };
                UnityEditor.EditorApplication.delayCall += pendingValidation;
            }

            /// <summary>
            /// Disabled settings retain no Kawase shader dependency in player builds.
            /// </summary>
            public bool UpdateShaderReference()
            {
                Shader next = enabled ? Shader.Find(ShaderName) : null;
                if (shader == next)
                    return false;

                shader = next;
                return true;
            }
#endif
        }
    }
}