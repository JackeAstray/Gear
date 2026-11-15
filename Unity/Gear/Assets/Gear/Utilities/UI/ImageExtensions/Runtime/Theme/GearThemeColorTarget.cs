using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 把一个 <see cref="Graphic"/> 的颜色绑定到主题 token，并在切主题时做无补间库的过渡。
    /// <para>
    /// 挂到 ImageEx / TMP_Text / Image 上，填 tokenId（Background / Surface / Text / Accent…）；
    /// 也可以运行时 <see cref="Configure"/>。组件自注册到 <see cref="GearThemeRegistry"/>。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Theme Color Target")]
    [DisallowMultipleComponent]
    public sealed class GearThemeColorTarget : MonoBehaviour, IGearThemeTarget
    {
        [SerializeField] private string tokenId = "Text";
        [SerializeField] private Graphic target;
        [SerializeField] private AudioSource transitionAudio;
        [SerializeField] private AudioClip transitionClip;

        private static readonly Dictionary<Graphic, GearThemeColorTarget> Channels =
            new Dictionary<Graphic, GearThemeColorTarget>();

        private Color from;
        private Color to;
        private float elapsed;
        private float duration;
        private bool animating;

        public bool IsAnimating => animating;

        public AudioSource TransitionAudio
        {
            get => transitionAudio;
            set => transitionAudio = value;
        }

        public AudioClip TransitionClip
        {
            get => transitionClip;
            set => transitionClip = value;
        }

        public string TokenId
        {
            get => tokenId;
            set
            {
                tokenId = value;
                ApplyTheme(GearThemeRegistry.Current, 0f);
            }
        }

        public Graphic Target
        {
            get
            {
                if (target == null) target = GetComponent<Graphic>();
                return target;
            }
            set => target = value;
        }

        /// <summary>运行时配置并立即应用当前主题。</summary>
        public void Configure(string id, Graphic graphic = null)
        {
            tokenId = id;
            if (graphic != null) target = graphic;
            ApplyTheme(GearThemeRegistry.Current, 0f);
        }

        private void OnEnable()
        {
            if (target == null) target = GetComponent<Graphic>();
            GearThemeRegistry.Register(this);
            if (!string.IsNullOrEmpty(tokenId))
                ApplyTheme(GearThemeRegistry.Current, 0f);
        }

        private void OnDisable()
        {
            GearThemeRegistry.Unregister(this);
            animating = false;
            Graphic graphic = Target;
            if (graphic != null && Channels.TryGetValue(graphic, out GearThemeColorTarget current) && current == this)
                Channels.Remove(graphic);
        }

        public void ApplyTheme(ImageExThemeAsset theme, float transitionSeconds)
        {
            Graphic graphic = Target;
            if (graphic == null || theme == null) return;
            if (!theme.TryGetColor(tokenId, out Color color)) return;

            if (transitionSeconds <= 0f || !Application.isPlaying)
            {
                if (Channels.TryGetValue(graphic, out GearThemeColorTarget active) && active != this)
                    active.CancelTransition();
                CancelTransition();
                graphic.color = color;
                return;
            }

            if (Channels.TryGetValue(graphic, out GearThemeColorTarget previous) && previous != this)
                previous.CancelTransition();
            Channels[graphic] = this;

            from = graphic.color;
            to = color;
            duration = Mathf.Max(0.01f, transitionSeconds);
            elapsed = 0f;
            animating = true;

            if (transitionAudio != null && transitionClip != null)
                transitionAudio.PlayOneShot(transitionClip);
        }

        /// <summary>取消当前 Graphic 上的过渡，保留当前颜色。</summary>
        public void CancelTransition()
        {
            animating = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetChannels()
        {
            Channels.Clear();
        }

        private void Update()
        {
            if (!animating) return;

            Graphic graphic = Target;
            if (graphic == null)
            {
                animating = false;
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            graphic.color = Color.Lerp(from, to, t);
            if (t >= 1f) animating = false;
        }
    }
}
