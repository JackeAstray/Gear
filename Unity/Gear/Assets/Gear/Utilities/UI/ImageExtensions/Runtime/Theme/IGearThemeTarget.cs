namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 主题消费者契约：注册到 <see cref="GearThemeRegistry"/> 后，
    /// 每次切主题都会收到目标主题与过渡时长。
    /// </summary>
    public interface IGearThemeTarget
    {
        void ApplyTheme(ImageExThemeAsset theme, float transitionSeconds);
    }
}
