using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Draws a Graphic field as a text component of the active text provider.
    /// Fields marked with it are also converted when the provider changes.
    /// </summary>
    public sealed class TextComponentAttribute : PropertyAttribute { }

    /// <summary>
    /// Draws an Object field as a font asset of the active text provider.
    /// </summary>
    public sealed class TextFontAttribute : PropertyAttribute { }

    /// <summary>
    /// Draws a MonoBehaviour field as an input field of the active text provider.
    /// Fields marked with it are also converted when the provider changes.
    /// </summary>
    public sealed class TextInputAttribute : PropertyAttribute { }
}