using UnityEngine;

namespace Evo.UI
{
    public partial class ProceduralShape
    {
        [Tooltip("Optional shared camera blur, tinted and masked by the fill.")]
        [SerializeField] private BackdropBlur.Settings backdropBlur = new() { enabled = false };

        // State
        BackdropBlurSource blurSource;

        /// <summary>
        /// Enables the fill blur and its refresh callback, or releases both immediately.
        /// </summary>
        public bool BackdropBlurEnabled
        {
            get => backdropBlur != null && backdropBlur.enabled;
            set
            {
                if (BackdropBlurEnabled == value)
                    return;

                backdropBlur ??= new BackdropBlur.Settings();
                backdropBlur.enabled = value;
                UpdateBackdropBlur();
                SetMaterialDirty();
            }
        }

        /// <summary>
        /// Shared blur settings. Call UpdateRect() after editing the returned settings in place.
        /// </summary>
        public BackdropBlur.Settings BackdropBlurSettings
        {
            get => backdropBlur;
            set
            {
                if (backdropBlur == value)
                    return;

                backdropBlur = value;
                UpdateBackdropBlur();
                SetMaterialDirty();
            }
        }

        void UpdateBackdropBlur()
        {
            if (!isActiveAndEnabled || !fillCenter || backdropBlur == null || !backdropBlur.enabled)
            {
                ReleaseBackdropBlur();
                return;
            }

            if (blurSource == null)
            {
                blurSource = new BackdropBlurSource(this);
                Canvas.preWillRenderCanvases += UpdateBackdropBlur;
            }

            blurSource.Refresh(backdropBlur, m_Material == null);
        }

        void ReleaseBackdropBlur()
        {
            if (blurSource == null)
                return;

            Canvas.preWillRenderCanvases -= UpdateBackdropBlur;
            blurSource.Dispose();
            blurSource = null;
        }
    }
}