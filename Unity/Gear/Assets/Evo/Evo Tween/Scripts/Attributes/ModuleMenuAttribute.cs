using System;

namespace Evo.Tween
{
    /// <summary>
    /// Registers a <see cref="TweenModule"/> in clip type dropdowns and add menus.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ModuleMenuAttribute : Attribute
    {
        /// <summary>
        /// Slash-delimited path shown in module selectors.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Primary sort order used before category and name.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Registers a module type with a slash-delimited editor menu path.
        /// </summary>
        public ModuleMenuAttribute(string path, int order = 0)
        {
            Path = Normalize(path);
            Order = order;
        }

        static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A module menu path is required.", nameof(path));

            string[] segments = path.Replace('\\', '/').Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = segments[i].Trim();
                if (segments[i].Length == 0)
                    throw new ArgumentException("Module menu paths cannot contain empty segments.", nameof(path));
            }

            return string.Join("/", segments);
        }
    }
}