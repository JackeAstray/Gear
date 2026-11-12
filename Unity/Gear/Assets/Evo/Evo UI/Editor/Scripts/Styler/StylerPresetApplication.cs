using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Applies a preset to selected Styler handlers, loaded scenes, or project prefab and scene assets.
    /// Handles default-preset assignments, selection Undo, and saving modified prefab assets.
    /// </summary>
    internal static class StylerPresetApplication
    {
        internal static IEnumerator<int> Run(StylerPreset preset, bool projectWide, Action<float, string> report)
        {
            if (projectWide)
            {
                // Update sources first so variants and scene instances inherit without new overrides
                using var project = StylerProjectUtility.Run(root => ApplyHierarchy(root, preset), true, report);
                while (project.MoveNext())
                    yield return project.Current;
            }
            else
            {
                var scenes = StylerProjectUtility.GetLoadedScenes();
                int count = 0;
                for (int i = 0; i < scenes.Count; i++)
                {
                    report?.Invoke((float)i / scenes.Count, $"Scene: {scenes[i].name}");
                    yield return count;
                    if (scenes[i].IsValid() && scenes[i].isLoaded)
                        count += StylerProjectUtility.ModifyScene(scenes[i], root => ApplyHierarchy(root, preset), false);
                }
                report?.Invoke(1, "Scene scan complete.");
                yield return count;
            }
        }

        static int ApplyHierarchy(GameObject root, StylerPreset preset) => ApplyHandlers(
            root.GetComponentsInChildren<MonoBehaviour>(true).OfType<IStylerHandler>(), preset, false);

        internal static int ApplySelected(IEnumerable<IStylerHandler> handlers, StylerPreset preset)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Styler Preset");

            try { return ApplyHandlers(handlers, preset, true); }
            finally { Undo.CollapseUndoOperations(group); }
        }

        static int ApplyHandlers(IEnumerable<IStylerHandler> handlers, StylerPreset preset, bool undo)
        {
            if (preset == null)
                throw new InvalidOperationException("The selected preset no longer exists.");

            var visited = new HashSet<MonoBehaviour>();
            var assets = new HashSet<GameObject>();
            int count = 0;

            foreach (var candidate in handlers)
            {
                var handler = StylerProjectUtility.ResolveHandler(candidate);
                if (handler is not MonoBehaviour component || component == null || !visited.Add(component))
                    continue;

                using var serialized = new SerializedObject(component);
                var useDefault = serialized.FindProperty("useDefaultStylerPreset");
                bool unlock = useDefault != null && useDefault.propertyType == SerializedPropertyType.Boolean
                    && useDefault.boolValue && Styler.GetDefaultPreset(false) != preset;
                if (handler.Preset == preset && !unlock)
                    continue;

                bool persistent = EditorUtility.IsPersistent(component);
                if (persistent)
                    StylerProjectUtility.EnsureEditable(AssetDatabase.GetAssetPath(component));

                var reference = serialized.FindProperty("preset") ?? serialized.FindProperty("stylerPreset");
                if (reference != null && reference.propertyType == SerializedPropertyType.ObjectReference)
                {
                    // Unlock from the default so the applied preset stays assigned
                    if (unlock)
                        useDefault.boolValue = false;

                    reference.objectReferenceValue = preset;
                    StylerProjectUtility.ApplyProperties(serialized, undo);
                    handler.UpdateStyler();
                }
                else
                {
                    // Keep custom IStylerHandler implementations supported through their public contract
                    if (undo)
                        Undo.RecordObject(component, "Apply Styler Preset");

                    handler.Preset = preset;
                    handler.UpdateStyler();

                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }

                StylerProjectUtility.MarkDirty(component);

                if (persistent)
                    assets.Add(component.transform.root.gameObject);

                count++;
            }

            StylerProjectUtility.SavePrefabAssets(assets);
            return count;
        }
    }
}
