using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Optional appearance settings for a virtual cursor target.
    /// Attach to the selectable or pointer-handler object.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Navigation/Virtual Cursor Override"), HelpURL(Constants.HelpUrl)]
    public class VirtualCursorOverride : MonoBehaviour
    {
        // Color
        [Tooltip("Use this color for all cursor states while this object is targeted.")]
        public bool overrideColor = false;
        public Color color = Color.white;

        // Frame
        [Tooltip("Use this radius instead of the cursor's fallback or the target's Procedural Shape radius.")]
        public bool overrideRadius = false;
        [Min(0f)] public float cornerRadius = 6f;
        public bool overridePadding = false;
        public Vector2 targetPadding = new(6f, 6f);
        public bool overrideOutlineWidth = false;
        [Min(0f)] public float outlineWidth = 2f;

        // Center
        public bool overrideCenterSize = false;
        [Min(0f)] public float centerSize = 4f;
        public bool overrideEdgeSoftness = false;
        [Range(0f, 32f)] public float edgeSoftness = 0f;
    }
}