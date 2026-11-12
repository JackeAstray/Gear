using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Draws interaction ripples inside an Image, RawImage or Procedural Shape.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Ripple")]
    [HelpURL(Constants.HelpUrl + "effects/ripple")]
    public class Ripple : InteractiveEffect, IPointerDownHandler, ISubmitHandler
    {
        public Settings ripple = new();
        public bool createOnClick = true;
        public bool createOnSubmit = true;

        public const int MaximumRipples = 8; // Keep in sync with InteractiveEffects.cginc

        protected override InteractiveEffectSettings EffectSettings => ripple;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (createOnClick && eventData != null && eventData.button == PointerEventData.InputButton.Left)
                Create(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (createOnSubmit)
                Create();
        }

        /// <summary>
        /// Creates a ripple. Calls without a pointer event are centered.
        /// </summary>
        public void Create(PointerEventData eventData = null)
        {
            if (!isActiveAndEnabled)
                return;

            Graphic fallback = null;

            if (ripple.targetGraphic == null)
                TryGetComponent(out fallback);

            ripple.Create(fallback, eventData);
        }

        /// <summary>
        /// Per-owner ripple configuration and renderer cache.
        /// </summary>
        [Serializable]
        public sealed class Settings : InteractiveEffectSettings
        {
            [Range(0.1f, 2f), Tooltip("Radius relative to the farthest corner at Coverage Duration.")]
            public float coverage = 1.5f;
            [Range(0f, 1f), Tooltip("0 gives an antialiased sharp edge; 1 gives a soft radial falloff.")]
            public float softness = 1f;
            public bool centered;
            [Range(0.01f, 1f), Tooltip("Time to reach Coverage. Growth continues during fade out.")]
            public float duration = 0.1f;
            [Range(0f, 3f), Tooltip("Time to fade out after reaching Coverage. 0 disappears immediately.")]
            public float fadeOutDuration = 0.4f;

            public Settings() : base(0.125f) { }

            /// <summary>
            /// Emits on the configured graphic or supplied default.
            /// </summary>
            public void Create(Graphic fallbackGraphic = null, PointerEventData eventData = null)
            {
                if (ResolvedColor.a <= 0f) 
                {
                    Clear();
                    return;
                }

                InteractiveEffectRenderer resolved = Resolve(fallbackGraphic);
                if (resolved != null)
                    resolved.Emit(this, eventData);
            }
        }
    }
}