namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial：瞬态效果（指针位置/悬停/波纹）不参与 Pooled 材质签名。
    /// 效果组件通过 AddTransientEffect/RemoveTransientEffect 引用计数，迫使实例走独立材质。
    /// </summary>
    public partial class ImageEx
    {
        private int transientEffectReferences;

        public bool HasTransientEffects => transientEffectReferences > 0;
        public int TransientEffectReferences => transientEffectReferences;

        public void AddTransientEffect()
        {
            transientEffectReferences++;
            SetMaterialDirty();
        }

        public void RemoveTransientEffect()
        {
            transientEffectReferences = System.Math.Max(0, transientEffectReferences - 1);
            SetMaterialDirty();
        }
    }
}
