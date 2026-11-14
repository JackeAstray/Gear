using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 通用主题绑定：把颜色之外的字体 / Sprite / 渐变令牌写到 TMP、Image、ImageEx。
    /// <para>
    /// 与 <see cref="ImageExThemeBinder"/> 分工：后者只改 ImageEx 的颜色与数值属性；
    /// 本组件覆盖“字体 / Sprite / 渐变”三类资源令牌，避免把 TMP 和 Image 也塞进 ImageEx 绑定。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Theme Binder")]
    [DisallowMultipleComponent]
    public sealed class GearThemeBinder : MonoBehaviour
    {
        public enum Target
        {
            Font = 0,
            Sprite = 1,
            Gradient = 2,
        }

        [Serializable]
        public struct Binding
        {
            public string tokenId;
            public Target target;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();
        [SerializeField] private TMP_Text text;
        [SerializeField] private Image image;
        [SerializeField] private ImageEx imageEx;

        public IReadOnlyList<Binding> Bindings => bindings;

        private void OnEnable()
        {
            ImageExThemeService.ThemeChanged += OnThemeChanged;
            Apply(ImageExThemeService.Current);
        }

        private void OnDisable()
        {
            ImageExThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(ImageExThemeAsset theme)
        {
            Apply(theme);
        }

        /// <summary>添加一条绑定；便于运行时/测试构建。</summary>
        public void AddBinding(string tokenId, Target target)
        {
            if (string.IsNullOrEmpty(tokenId)) return;
            bindings.Add(new Binding { tokenId = tokenId, target = target });
            if (isActiveAndEnabled) Apply(ImageExThemeService.Current);
        }

        /// <summary>应用主题；返回成功写入的绑定数量。</summary>
        public int Apply(ImageExThemeAsset theme)
        {
            if (theme == null) return 0;

            ResolveTargets();
            int applied = 0;

            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (string.IsNullOrEmpty(binding.tokenId)) continue;

                switch (binding.target)
                {
                    case Target.Font:
                        if (text != null && theme.TryGetFont(binding.tokenId, out TMP_FontAsset font))
                        {
                            text.font = font;
                            applied++;
                        }
                        break;

                    case Target.Sprite:
                        if (image != null && theme.TryGetSprite(binding.tokenId, out Sprite sprite))
                        {
                            image.sprite = sprite;
                            applied++;
                        }
                        break;

                    case Target.Gradient:
                        if (imageEx != null && theme.TryGetGradient(binding.tokenId, out Gradient gradient))
                        {
                            GradientEffect effect = imageEx.GradientEffect;
                            effect.Gradient = gradient;
                            effect.Enabled = true;
                            imageEx.GradientEffect = effect;
                            applied++;
                        }
                        break;
                }
            }

            return applied;
        }

        private void ResolveTargets()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (image == null) image = GetComponent<Image>();
            if (imageEx == null) imageEx = GetComponent<ImageEx>();
        }
    }
}
