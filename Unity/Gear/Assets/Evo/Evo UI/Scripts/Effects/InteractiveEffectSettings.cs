using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Shared target, color, material dependency and owner-scoped lifetime for interactive effects.
    /// </summary>
    [Serializable]
    public abstract class InteractiveEffectSettings
    {
        public bool enabled = true;
        [Tooltip("Graphic that receives the effect. If empty, uses the host's target graphic.")]
        public Graphic targetGraphic;
        public Color color;
        public ColorMapping stylerColor;
        [Tooltip("Keeps the effect's own opacity on translucent graphics. The sprite or shape outline, Canvas Groups and masks still apply.")]
        public bool ignoreGraphicAlpha = true;

        // Shader Dependency
        [SerializeField, HideInInspector] Shader shader;
        public const string ShaderName = "Hidden/Evo/UI/Interactive Effects";

        // State
        [NonSerialized] Graphic activeGraphic;
        [NonSerialized] internal InteractiveEffectRenderer renderer;
        [NonSerialized] bool useStyler;
        [NonSerialized] Color stylerValue;

        /// <summary>
        /// The custom color, or the Styler color resolved from the host's preset.
        /// </summary>
        public Color ResolvedColor => useStyler ? stylerValue : color;

        protected InteractiveEffectSettings(float alpha)
        {
            color = new Color(1f, 1f, 1f, alpha);
            stylerColor = new ColorMapping { stylerID = "Primary", overrideAlpha = true, alphaOverride = alpha };
        }

        /// <summary>
        /// Resolves the color from the host's provider and preset. Hosts call this when either changes.
        /// </summary>
        public void UpdateStyler(StyleProvider provider, StylerPreset preset)
        {
            useStyler = provider == StyleProvider.Styler;
            if (useStyler)
                stylerValue = Styler.GetColor(provider, stylerColor, preset);
        }

        protected InteractiveEffectRenderer Resolve(Graphic fallback)
        {
            Graphic target = targetGraphic != null ? targetGraphic : fallback;

            if (!enabled || activeGraphic != target)
                Clear();

            if (!enabled || target == null || !Application.IsPlaying(target.gameObject))
                return null;

            activeGraphic = target;

            if (renderer == null)
            {
                if (!InteractiveEffectRenderer.IsSupported(target))
                    return null;
                
                Shader resolved = shader != null ? shader : Shader.Find(ShaderName);
                if (resolved == null)
                    return null;
             
                if (!target.TryGetComponent(out renderer))
                {
                    renderer = target.gameObject.AddComponent<InteractiveEffectRenderer>();
                    renderer.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                }

                renderer.Initialize(resolved);
            }

            return renderer;
        }

        /// <summary>
        /// Removes only this owner's effect; other effects on the target continue.
        /// </summary>
        public void Clear()
        {
            if (renderer != null)
                renderer.Remove(this);

            renderer = null;
            activeGraphic = null;
        }

        internal bool IsValidFor(Graphic target)
        {
            return enabled && renderer != null && activeGraphic == target && (targetGraphic == null || targetGraphic == target);
        }

#if UNITY_EDITOR
        [NonSerialized] UnityEditor.EditorApplication.CallbackFunction pendingValidation;

        public void Validate(UnityEngine.Object owner)
        {
            if (pendingValidation != null)
                UnityEditor.EditorApplication.delayCall -= pendingValidation;
           
            pendingValidation = () =>
            {
                pendingValidation = null;

                if (owner == null || Application.isPlaying || !UpdateShaderReference())
                    return;
              
                UnityEditor.EditorUtility.SetDirty(owner);
               
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(owner))
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            };

            UnityEditor.EditorApplication.delayCall += pendingValidation;
        }

        /// <summary>
        /// Keep the shader in player builds without an Always Included Shaders entry.
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