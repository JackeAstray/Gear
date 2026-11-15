using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 主题注册表：组件自注册，<see cref="Apply"/> 一次广播给所有目标，并带上过渡时长。
    /// <para>
    /// 与 <see cref="ImageExThemeService"/> 的关系：
    /// 后者继续广播旧的 <c>ThemeChanged</c> 事件（现有 Binder 不用改）；
    /// 本注册表额外支持“按目标驱动 + 过渡”，新组件用 <see cref="IGearThemeTarget"/> 接入。
    /// </para>
    /// </summary>
    public static class GearThemeRegistry
    {
        public const string DarkResourcePath = "UITheme/ImageExDefault";
        public const string LightResourcePath = "UITheme/ImageExLight";

        private static readonly List<IGearThemeTarget> targets = new List<IGearThemeTarget>(32);

        /// <summary>当前主题；可能为 null。</summary>
        public static ImageExThemeAsset Current => ImageExThemeService.Current;

        /// <summary>当前注册目标数；用于诊断/用例。</summary>
        public static int TargetCount => targets.Count;

        /// <summary>主题应用完成事件（在目标广播之后）。</summary>
        public static event Action<ImageExThemeAsset> ThemeApplied;

        /// <summary>注册主题目标；重复注册会被忽略。</summary>
        public static void Register(IGearThemeTarget target)
        {
            if (target == null || targets.Contains(target)) return;
            targets.Add(target);
        }

        /// <summary>注销主题目标。</summary>
        public static void Unregister(IGearThemeTarget target)
        {
            if (target == null) return;
            targets.Remove(target);
        }

        /// <summary>应用主题：旧 Binder 走 ThemeChanged，新目标走 ApplyTheme(theme, seconds)。</summary>
        public static void Apply(ImageExThemeAsset theme, float transitionSeconds = 0.25f)
        {
            if (theme == null) return;

            ImageExThemeService.SetTheme(theme, transitionSeconds);

            IGearThemeTarget[] snapshot = targets.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                IGearThemeTarget target = snapshot[i];
                if (target == null)
                {
                    targets.Remove(target);
                    continue;
                }

                target.ApplyTheme(theme, Mathf.Max(0f, transitionSeconds));
            }

            ThemeApplied?.Invoke(theme);
        }

        /// <summary>用当前主题重新广播一次（例如运行时改了主题资产内容）。</summary>
        public static void Refresh(float transitionSeconds = 0f)
        {
            Apply(Current, transitionSeconds);
        }

        /// <summary>从 Resources 读取暗色主题（默认主题）。</summary>
        public static ImageExThemeAsset LoadDark()
        {
            return Resources.Load<ImageExThemeAsset>(DarkResourcePath);
        }

        /// <summary>从 Resources 读取亮色主题；不存在返回 null。</summary>
        public static ImageExThemeAsset LoadLight()
        {
            return Resources.Load<ImageExThemeAsset>(LightResourcePath);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            targets.Clear();
            ThemeApplied = null;
        }
    }
}
