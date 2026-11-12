using System;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Used where interaction states are fixed, such as Button.
    /// </summary>
    public enum InteractionState
    {
        Disabled = 0,
        Normal = 1,
        Highlighted = 2,
        Pressed = 3,
        Selected = 4
    }

    /// <summary>
    /// Used where multiple styling sources are available.
    /// </summary>
    public enum StyleProvider
    {
        Custom = 0,
        Styler = 1
    }

    /// <summary>
    /// Used where custom offset position is offered, such as Tooltip and Context Menu.
    /// </summary>
    public enum OffsetPosition
    {
        Custom = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomLeft = 3,
        BottomRight = 4,
        Top = 5,
        Bottom = 6,
        Left = 7,
        Right = 8
    }

    /// <summary>
    /// Built-in numeric formats supported by the value.
    /// </summary>
    public enum NumberDisplayFormat
    {
        [InspectorName("Fixed - No Decimals (0)")] Fixed0,
        [InspectorName("Fixed - 1 Decimal (0.0)")] Fixed1,
        [InspectorName("Fixed - 2 Decimals (0.00)")] Fixed2,
        [InspectorName("Fixed - 3 Decimals (0.000)")] Fixed3,
        [InspectorName("Fixed - 4 Decimals (0.0000)")] Fixed4,
        [InspectorName("Fixed - 5 Decimals (0.00000)")] Fixed5,
        [InspectorName("Number - No Decimals (1,234)")] Number0,
        [InspectorName("Number - 1 Decimal (1,234.5)")] Number1,
        [InspectorName("Number - 2 Decimals (1,234.56)")] Number2,
        [InspectorName("Number - 3 Decimals (1,234.567)")] Number3
    }

    /// <summary>
    /// Interaction states that can override the cursor while the pointer is over an Interactive.
    /// </summary>
    [Flags]
    public enum CursorStates
    {
        None = 0,
        Disabled = 1 << (int)InteractionState.Disabled,
        Normal = 1 << (int)InteractionState.Normal,
        Highlighted = 1 << (int)InteractionState.Highlighted,
        Pressed = 1 << (int)InteractionState.Pressed,
        Selected = 1 << (int)InteractionState.Selected,
        Everything = ~0
    }

    /// <summary>
    /// Cursor texture and hotspot for a single interaction state.
    /// </summary>
    [Serializable]
    public struct CursorSettings
    {
        [Tooltip("The cursor texture for this state. Leave empty to use the default cursor.")]
        public Texture2D texture;
        [Tooltip("The offset in pixels from the top-left of the texture. Clamped to the texture bounds at runtime.")]
        public Vector2 hotspot;
    }

    /// <summary>
    /// Retains the last resolved Styler value across reloads without changing custom values.
    /// </summary>
    [Serializable]
    public abstract class StylerMapping<T>
    {
        [SerializeField, HideInInspector] T lastValue;
        [SerializeField, HideInInspector] bool hasValue;

        internal T Remember(T value)
        {
            lastValue = value;
            hasValue = true;
            return value;
        }

        internal T LastValueOr(T fallback) => hasValue ? lastValue : fallback;
    }

    /// <summary>
    /// Used for dynamic objects where multiple audio sources are available.
    /// </summary>
    [Serializable]
    public class AudioMapping : StylerMapping<AudioClip>
    {
        public string stylerID;
        public AudioClip audioClip;
    }

    /// <summary>
    /// Used for dynamic objects where multiple color sources are available.
    /// </summary>
    [Serializable]
    public class ColorMapping : StylerMapping<Color>
    {
        public string stylerID;
        public Color color = Color.white;
        [Tooltip("Override the alpha channel of the resolved Styler preset color.")]
        public bool overrideAlpha = false;
        [Tooltip("Alpha value to apply (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)] public float alphaOverride = 1f;
    }

    /// <summary>
    /// Used for dynamic objects where multiple font sources are available.
    /// Fonts can belong to any text provider; each backend uses only the types it supports.
    /// </summary>
    [Serializable]
    public class FontMapping : StylerMapping<UnityEngine.Object>
    {
        public string stylerID;
        [TextFont] public UnityEngine.Object font;
    }

    /// <summary>
    /// Used for dynamic objects where multiple gradient sources are available.
    /// </summary>
    [Serializable]
    public class GradientMapping : StylerMapping<Gradient>
    {
        public string stylerID;
        public Gradient gradient = new();
    }

    /// <summary>
    /// Used for dynamic objects where multiple sprite sources are available.
    /// </summary>
    [Serializable]
    public class SpriteMapping : StylerMapping<Sprite>
    {
        public string stylerID;
        public Sprite sprite;
    }
}