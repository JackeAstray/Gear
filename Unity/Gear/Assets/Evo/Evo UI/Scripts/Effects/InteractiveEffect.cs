using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Shared lifetime and Styler color source for standalone interactive effects.
    /// </summary>
    public abstract class InteractiveEffect : MonoBehaviour, IStylerHandler
    {
        public StyleProvider colorMode = StyleProvider.Custom;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = true;
        public StylerPreset stylerPreset;

        protected abstract InteractiveEffectSettings EffectSettings { get; }

        // Procedural Shapes draw these effects with their full shader program
        ProceduralShape programShape;

        protected virtual void OnEnable()
        {
            UpdateStyler();
            Styler.Register(this);
            RequestShapeProgram(true);
        }

        protected virtual void OnDisable()
        {
            Styler.Unregister(this);
            EffectSettings.Clear();
            RequestShapeProgram(false);
        }

        // Asking from OnEnable switches and compiles the program with the Canvas instead of on the first hover.
        // An unassigned target is still covered when the effect first shows (see ProceduralShape.RequiresFullShader).
        void RequestShapeProgram(bool request)
        {
            InteractiveEffectSettings settings = EffectSettings;
            ProceduralShape shape = request && Application.isPlaying && settings.enabled ? settings.targetGraphic as ProceduralShape : null;
            if (ReferenceEquals(shape, programShape))
                return;

            if (programShape != null)
                programShape.SetEffectRequest(false);

            programShape = shape;
            if (shape != null)
                shape.SetEffectRequest(true);
        }

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        /// <summary>
        /// Resolves the effect color. Call after changing Color Mode or the Styler color from code.
        /// </summary>
        public void UpdateStyler()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, colorMode, useDefaultStylerPreset);
            EffectSettings.UpdateStyler(colorMode, stylerPreset);
        }
        #endregion

#if UNITY_EDITOR
        protected virtual void Reset()
        {
            TryGetComponent(out EffectSettings.targetGraphic);
            EffectSettings.UpdateShaderReference();
        }

        protected virtual void OnValidate()
        {
            EffectSettings.Validate(this);
            UpdateStyler();
        }
#endif
    }
}