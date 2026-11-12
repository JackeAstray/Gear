using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Names generated objects after their content. Reading a name allocates, so at runtime a slot that keeps
    /// its object and name isn't read again, which keeps unchanged refreshes allocation-free.
    /// </summary>
    internal sealed class GeneratedNames
    {
        readonly List<Object> objects = new();
        readonly List<string> names = new();

        /// <summary>
        /// Names the slot's object and returns whether its name changed. Edit mode always compares the current name.
        /// </summary>
        internal bool Apply(int index, Object instance, string name)
        {
            bool runtime = Application.IsPlaying(instance);
            if (runtime && index < objects.Count && ReferenceEquals(objects[index], instance) && names[index] == name)
                return false;

            bool changed = instance.name != name;
            if (changed)
                instance.name = name;

            if (runtime)
            {
                while (objects.Count <= index)
                {
                    objects.Add(null);
                    names.Add(null);
                }

                objects[index] = instance;
                names[index] = name;
            }

            return changed;
        }
    }
}
