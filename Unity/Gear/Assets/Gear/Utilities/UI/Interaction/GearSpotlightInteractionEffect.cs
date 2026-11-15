using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>Spotlight 适配器：悬停/选中点亮，离开淡出。</summary>
    [AddComponentMenu("UI/Gear/Interaction/Spotlight Effect")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImageExSpotlight))]
    public sealed class GearSpotlightInteractionEffect : MonoBehaviour, IGearInteractionEffect
    {
        [SerializeField] private int priority = 10;

        public int Priority => priority;
        public string Channel => "spotlight";

        public void OnInteraction(GearInteractionState state, GearInteractive source)
        {
            ImageExSpotlight spotlight = GetComponent<ImageExSpotlight>();
            if (spotlight != null)
                spotlight.SetHover(state == GearInteractionState.Highlighted
                    || state == GearInteractionState.Pressed
                    || state == GearInteractionState.Selected);
        }
    }
}
