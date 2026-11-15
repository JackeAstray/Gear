namespace Gear.Utilities.UI.Interaction
{
    /// <summary>
    /// 交互效果契约：由 <see cref="GearInteractionEffectBus"/> 按优先级/通道调度。
    /// </summary>
    public interface IGearInteractionEffect
    {
        int Priority { get; }
        string Channel { get; }
        void OnInteraction(GearInteractionState state, GearInteractive source);
    }
}
