using System;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 主题服务：保存当前主题、在切换时通知所有 <see cref="ImageExThemeBinder"/>。
    /// <para>
    /// 默认主题从 <c>Resources/UITheme/ImageExDefault</c> 加载（不存在时为 null，所有绑定静默跳过）；
    /// 启动时在场景加载前预读一次，避免第一帧先显示旧色再跳变。
    /// </para>
    /// </summary>
    public static class ImageExThemeService
    {
        /// <summary>默认主题的 Resources 相对路径（不带扩展名）。</summary>
        public const string DefaultResourcePath = "UITheme/ImageExDefault";

        private static ImageExThemeAsset s_current;

        /// <summary>当前主题；可能为 null。</summary>
        public static ImageExThemeAsset Current
        {
            get
            {
                if (s_current == null) s_current = LoadDefault();
                return s_current;
            }
        }

        /// <summary>主题切换事件（切主题、或手动 Refresh 时触发）。</summary>
        public static event Action<ImageExThemeAsset> ThemeChanged;

        /// <summary>
        /// 当前这次 <see cref="ThemeChanged"/> 广播的过渡时长（秒）。
        /// 绑定器可在事件回调里读取它做颜色过渡；广播结束后复位为 0。
        /// </summary>
        public static float TransitionSeconds { get; private set; }

        /// <summary>切换到指定主题并通知所有绑定。</summary>
        public static void SetTheme(ImageExThemeAsset theme)
        {
            SetTheme(theme, 0f);
        }

        /// <summary>切换到指定主题并通知所有绑定；transitionSeconds 供支持过渡的订阅者使用。</summary>
        public static void SetTheme(ImageExThemeAsset theme, float transitionSeconds)
        {
            s_current = theme;
            TransitionSeconds = Mathf.Max(0f, transitionSeconds);
            try
            {
                ThemeChanged?.Invoke(s_current);
            }
            finally
            {
                TransitionSeconds = 0f;
            }
        }

        /// <summary>重新广播当前主题（例如运行时改了主题资产内容）。</summary>
        public static void Refresh()
        {
            ThemeChanged?.Invoke(Current);
        }

        /// <summary>从 Resources 读取默认主题（不存在返回 null）。</summary>
        public static ImageExThemeAsset LoadDefault()
        {
            return Resources.Load<ImageExThemeAsset>(DefaultResourcePath);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            s_current = LoadDefault();
        }
    }
}
