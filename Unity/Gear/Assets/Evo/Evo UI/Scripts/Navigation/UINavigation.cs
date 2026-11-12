using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation"), HelpURL(Constants.HelpUrl + "navigation/ui-navigation")]
    public class UINavigation : Selectable
    {
        protected override void Start()
        {
            // Hey stranger, not sure why you're here, but there's nothing to see here!
            // This is a wrapper component, we don't need Selectable's visual features like transitions.
            // If you need transitions, just use the Interactive component. Believe me, it's much better.
            // If you need complex animations, also consider using Evo Tween <3
            if (transition != Transition.None)
                transition = Transition.None;
        }
    }
}