using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Pointer lighting along the inside of a sprite's alpha edge or a procedural shape's border.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Border Glow"), HelpURL(Constants.HelpUrl)]
    public class BorderGlow : InteractiveEffect, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Settings borderGlow = new();

        protected override InteractiveEffectSettings EffectSettings => borderGlow;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (borderGlow.visibility != Settings.Visibility.Always)
                return;

            Show();
        }

        public void OnPointerEnter(PointerEventData eventData) => Show(eventData);

        public void OnPointerMove(PointerEventData eventData) => borderGlow.Move(eventData);

        public void OnPointerExit(PointerEventData eventData) => borderGlow.OnPointerExit();

        /// <summary>
        /// Shows the light at the pointer without an offset. A null event follows the global pointer.
        /// </summary>
        public void Show(PointerEventData eventData = null)
        {
            if (!isActiveAndEnabled)
                return;

            Graphic fallback = null;

            if (borderGlow.targetGraphic == null)
                TryGetComponent(out fallback);

            borderGlow.Show(fallback, eventData);
        }

        public void Hide() => borderGlow.Hide();

        /// <summary>
        /// Per-owner border glow configuration and renderer cache.
        /// </summary>
        [Serializable]
        public sealed class Settings : InteractiveEffectSettings
        {
            public Visibility visibility = Visibility.HoverOnly;
            [Min(0.1f), Tooltip("Border thickness in local UI units. The glow stays inside the graphic.")]
            public float width = 2f;
            [Range(0f, 1f), Tooltip("Minimum lighting around the whole border while visible.")]
            public float ambient = 0f;
            [Min(1f), Tooltip("Light radius in the target's local UI units.")]
            public float radius = 100f;
            [Range(0f, 1f), Tooltip("Soft radial falloff from the center of the light.")]
            public float softness = 1f;
            [Range(0f, 1f), Tooltip("Pointer smoothing time. 0 follows immediately.")]
            public float followDuration = 0f;
            [Range(0f, 2f)] public float fadeInDuration = 0.1f;
            [Range(0f, 2f)] public float fadeOutDuration = 0.1f;

            public enum Visibility { HoverOnly, Always }

            public Settings() : base(0.8f) { }

            /// <summary>
            /// Shows on the configured graphic or fallback. A null event follows the global pointer.
            /// </summary>
            public void Show(Graphic fallbackGraphic = null, PointerEventData eventData = null)
            {
                if (ResolvedColor.a <= 0f)
                {
                    Clear();
                    return;
                }

                InteractiveEffectRenderer resolved = Resolve(fallbackGraphic);
                if (resolved != null)
                    resolved.Show(this, eventData);
            }

            public void Move(PointerEventData eventData)
            {
                if (renderer != null)
                    renderer.Move(this, eventData);
            }

            public void Hide()
            {
                if (renderer != null)
                    renderer.Hide(this, fadeOutDuration);
            }

            internal void UpdateVisibility(Graphic fallbackGraphic, bool hovered, PointerEventData eventData)
            {
                if (hovered || visibility == Visibility.Always)
                    Show(fallbackGraphic, hovered ? eventData : null);
                else
                    Hide();
            }

            internal void OnPointerExit()
            {
                if (visibility == Visibility.HoverOnly)
                    Hide();
                else
                    Move(null); // Resume the global pointer after leaving the hit target
            }
        }
    }
}