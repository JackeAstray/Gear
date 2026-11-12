using System;
using UnityEngine;

namespace Evo.Tween.Modules.Rendering
{
    /// <summary>
    /// Animates a SpriteRenderer's tint color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Sprite Renderer/Color")]
    public sealed class SpriteRendererColorModule : ColorModule<SpriteRenderer>
    {
        protected internal override long PropertyKey => 501L;
        protected override Color Read() => Target.color;
        protected override void Write(Color value) => Target.color = value;
    }

    /// <summary>
    /// Animates only the alpha channel of a SpriteRenderer's tint color.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Sprite Renderer/Alpha")]
    public sealed class SpriteRendererAlphaModule : ColorAlphaModule<SpriteRenderer>
    {
        protected internal override long PropertyKey => 501L;
        protected override Color ReadColor() => Target.color;
        protected override void WriteColor(Color color) => Target.color = color;
    }

    /// <summary>
    /// Animates the size used by a tiled or sliced SpriteRenderer.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Sprite Renderer/Size")]
    public sealed class SpriteRendererSizeModule : Vector2Module<SpriteRenderer>
    {
        protected internal override long PropertyKey => 502L;
        protected override Vector2 Read() => Target.size;
        protected override void Write(Vector2 value) => Target.size = value;
    }
}