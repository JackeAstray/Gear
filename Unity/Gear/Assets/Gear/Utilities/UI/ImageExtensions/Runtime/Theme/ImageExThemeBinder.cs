using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 把 <see cref="ImageExThemeAsset"/> 里的令牌绑定到同一个物体上的 ImageEx 属性。
    /// <para>
    /// 用法：挂在 ImageEx 所在物体上 → 添加若干「tokenId → 目标属性」→ 运行时由
    /// <see cref="ImageExThemeService"/> 驱动应用。换主题只需替换 Theme 资产。
    /// </para>
    /// <para>
    /// 设计约束：只改 ImageEx 公开属性，不接管渲染；未解析的 token 静默跳过（编辑器窗口会列出来）。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ImageEx))]
    [AddComponentMenu("UI/Gear/ImageEx 主题绑定")]
    [DisallowMultipleComponent]
    public sealed class ImageExThemeBinder : MonoBehaviour
    {
        public enum Target
        {
            Tint = 0,
            OutlineColor = 1,
            EdgeColor = 2,
            InnerShadowColor = 3,
            ShadowColor = 4,
            TransitionColor = 5,
            OutlineWidth = 6,
            EdgeWidth = 7,
            InnerShadowWidth = 8,
            ClipSoftness = 9,
            PatternColor = 12,
            PatternOpacity = 13,
            BorderGlowColor = 14,
            BorderGlowIntensity = 15,
        }

        [Serializable]
        public struct Binding
        {
            [Tooltip("主题资产里的令牌 ID，例如 Accent / Background")]
            public string tokenId;

            [Tooltip("要写入的 ImageEx 属性")]
            public Target target;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();

        private ImageEx targetImage;

        /// <summary>绑定列表（运行时只读）。</summary>
        public IReadOnlyList<Binding> Bindings => bindings;

        /// <summary>绑定的 ImageEx。</summary>
        public ImageEx TargetImage
        {
            get
            {
                if (targetImage == null) targetImage = GetComponent<ImageEx>();
                return targetImage;
            }
        }

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

        /// <summary>应用指定主题；返回成功写入的属性数量。</summary>
        public int Apply(ImageExThemeAsset theme)
        {
            if (theme == null) return 0;

            ImageEx image = TargetImage;
            if (image == null) return 0;

            int applied = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (string.IsNullOrEmpty(binding.tokenId)) continue;

                switch (binding.target)
                {
                    case Target.Tint:
                        if (theme.TryGetColor(binding.tokenId, out Color tint)) { image.color = tint; applied++; }
                        break;
                    case Target.OutlineColor:
                        if (theme.TryGetColor(binding.tokenId, out Color outline)) { image.OutlineColor = outline; applied++; }
                        break;
                    case Target.EdgeColor:
                        if (theme.TryGetColor(binding.tokenId, out Color edge)) { image.EdgeColor = edge; applied++; }
                        break;
                    case Target.InnerShadowColor:
                        if (theme.TryGetColor(binding.tokenId, out Color inner)) { image.InnerShadowColor = inner; applied++; }
                        break;
                    case Target.ShadowColor:
                        if (theme.TryGetColor(binding.tokenId, out Color shadow)) { image.ShadowColor = shadow; applied++; }
                        break;
                    case Target.TransitionColor:
                        if (theme.TryGetColor(binding.tokenId, out Color transition)) { image.TransitionColor = transition; applied++; }
                        break;
                    case Target.OutlineWidth:
                        if (theme.TryGetFloat(binding.tokenId, out float outlineWidth)) { image.OutlineWidth = outlineWidth; applied++; }
                        break;
                    case Target.EdgeWidth:
                        if (theme.TryGetFloat(binding.tokenId, out float edgeWidth)) { image.EdgeWidth = edgeWidth; applied++; }
                        break;
                    case Target.InnerShadowWidth:
                        if (theme.TryGetFloat(binding.tokenId, out float innerWidth)) { image.InnerShadowWidth = innerWidth; applied++; }
                        break;
                    case Target.ClipSoftness:
                        if (theme.TryGetFloat(binding.tokenId, out float clipSoftness)) { image.ClipSoftness = clipSoftness; applied++; }
                        break;
                    case Target.PatternColor:
                        if (theme.TryGetColor(binding.tokenId, out Color patternColor)) { image.PatternColor = patternColor; applied++; }
                        break;
                    case Target.PatternOpacity:
                        if (theme.TryGetFloat(binding.tokenId, out float patternOpacity)) { image.PatternOpacity = patternOpacity; applied++; }
                        break;
                    case Target.BorderGlowColor:
                        if (theme.TryGetColor(binding.tokenId, out Color glowColor)) { image.BorderGlowColor = glowColor; applied++; }
                        break;
                    case Target.BorderGlowIntensity:
                        if (theme.TryGetFloat(binding.tokenId, out float glowIntensity)) { image.BorderGlowIntensity = glowIntensity; applied++; }
                        break;
                }
            }

            return applied;
        }

        /// <summary>编辑器/排查用：列出当前主题里解析不到的 tokenId（去重）。</summary>
        public List<string> GetUnresolvedTokenIds(ImageExThemeAsset theme)
        {
            List<string> unresolved = new List<string>();
            if (theme == null) return unresolved;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bindings.Count; i++)
            {
                string id = bindings[i].tokenId;
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;

                bool isColorTarget = IsColorTarget(bindings[i].target);
                bool resolved = isColorTarget ? theme.HasColor(id) : theme.HasFloat(id);
                if (!resolved) unresolved.Add(id);
            }

            return unresolved;
        }

        private static bool IsColorTarget(Target target)
        {
            switch (target)
            {
                case Target.Tint:
                case Target.OutlineColor:
                case Target.EdgeColor:
                case Target.InnerShadowColor:
                case Target.ShadowColor:
                case Target.TransitionColor:
                case Target.PatternColor:
                case Target.BorderGlowColor:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>运行时替换绑定（谨慎使用：会改变序列化内容）。</summary>
        public void SetBindings(List<Binding> newBindings)
        {
            bindings = newBindings ?? new List<Binding>();
        }
    }
}
