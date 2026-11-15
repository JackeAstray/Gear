using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>Border Glow 适配器：悬停/按下点亮。</summary>
    [AddComponentMenu("UI/Gear/Interaction/Glow Effect")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageExPointerGlow))]
    public sealed class GearGlowInteractionEffect : MonoBehaviour, IGearInteractionEffect
    {
        [SerializeField] private int priority = 20;

        public int Priority => priority;
        public string Channel => "glow";

        public void OnInteraction(GearInteractionState state, GearInteractive source)
        {
            ImageExPointerGlow glow = GetComponent<ImageExPointerGlow>();
            if (glow != null)
                glow.SetHover(state == GearInteractionState.Highlighted
                    || state == GearInteractionState.Pressed
                    || state == GearInteractionState.Selected);
        }
    }
}
