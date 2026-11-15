using UnityEngine;
using Gear.Utilities.UI.Widgets;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>Ripple 适配器：Pressed 时在中心发一波。</summary>
    [AddComponentMenu("UI/Gear/Interaction/Ripple Effect")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GearRipple))]
    public sealed class GearRippleInteractionEffect : MonoBehaviour, IGearInteractionEffect
    {
        [SerializeField] private float strength = 1f;

        public int Priority => 0;
        public string Channel => "ripple";

        public void OnInteraction(GearInteractionState state, GearInteractive source)
        {
            if (state != GearInteractionState.Pressed) return;
            GearRipple ripple = GetComponent<GearRipple>();
            if (ripple != null) ripple.Emit(new Vector2(0.5f, 0.5f), strength);
        }
    }
}
