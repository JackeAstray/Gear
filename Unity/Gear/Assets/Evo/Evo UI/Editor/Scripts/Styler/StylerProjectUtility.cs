using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Shares editor operation locking, handler resolution, serialized updates, and asset persistence
    /// between preset application and ID migration. Traverses prefab dependencies and scenes in the
    /// requested order, saving closed assets while leaving open scenes unsaved during project scans.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class StylerProjectUtility
    {
        static UnityEngine.Object operationOwner;
        internal static bool IsBusy => operationOwner != null;
        internal static event Action BusyChanged;

        internal static void ApplyProperties(SerializedObject serialized, bool undo)
        {
            if (undo) { serialized.ApplyModifiedProperties(); }
            else { serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        internal static void MarkDirty(Component component)
        {
            EditorUtility.SetDirty(component);
            var scene = component.gameObject.scene;
            if (scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static void SavePrefabAssets(IEnumerable<GameObject> roots)
        {
            foreach (var root in roots)
            {
                PrefabUtility.SavePrefabAsset(root, out bool success);
                if (!success)
                    throw new InvalidOperationException($"Could not save {AssetDatabase.GetAssetPath(root)}.");
            }
        }

        internal static IStylerHandler ResolveHandler(IStylerHandler handler)
        {
            // The enhancer exposes the preset owned by its placeholder, which may be outside its hierarchy
            if (handler is not InputFieldEnhancer enhancer || enhancer == null)
                return handler;

            MonoBehaviour input = enhancer.source;
            if (input == null)
                TextProvider.TryGetInput(enhancer, out input);

            UnityEngine.UI.Graphic placeholder = TextProvider.FindInput(input)?.GetPlaceholder(input);
            return placeholder != null ? placeholder.GetComponent<StylerObject>() : null;
        }

        internal static bool BeginOperation(UnityEngine.Object owner)
        {
            if (IsBusy)
                return false;

            operationOwner = owner;
            BusyChanged?.Invoke();
            return true;
        }

        internal static void EndOperation(UnityEngine.Object owner)
        {
            if (operationOwner != owner)
                return;

            operationOwner = null;
            BusyChanged?.Invoke();
        }

        internal static IEnumerator<int> Run(Func<GameObject, int> modify, bool sourcesFirst, Action<float, string> report, bool prefabsOnly = false)
        {
            var prefabPaths = new List<string>();
            var visited = new HashSet<string>();
            var paths = FindAssets("t:Prefab").Where(IsProjectPrefab).ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                report?.Invoke(0.1f * i / paths.Length, $"Finding prefab dependencies ({i + 1}/{paths.Length})");
                yield return 0;
                AddPrefabDependencies(paths[i], visited, prefabPaths);
            }

            if (!sourcesFirst)
                prefabPaths.Reverse();

            // Exit before even enumerating scenes. In particular, skip the final open-scene pass.
            if (prefabsOnly)
            {
                int prefabCount = 0;
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    report?.Invoke(0.1f + 0.9f * i / prefabPaths.Count, $"Prefab asset: {prefabPaths[i]}");
                    yield return prefabCount;
                    prefabCount += ModifyPrefab(prefabPaths[i], modify);
                }
                report?.Invoke(1, "Prefab scan complete.");
                yield return prefabCount;
                yield break;
            }

            var openScenes = GetLoadedScenes();
            var openPaths = new HashSet<string>(openScenes.Select(scene => scene.path));
            var sceneJobs = openScenes.Select(scene => (scene.path, scene, prefab: false))
                .Concat(FindAssets("t:Scene").Where(path => !openPaths.Contains(path))
                    .Select(path => (path, scene: default(Scene), prefab: false)));

            var prefabJobs = prefabPaths.Select(path => (path, scene: default(Scene), prefab: true));
            var jobs = (sourcesFirst ? prefabJobs.Concat(sceneJobs) : sceneJobs.Concat(prefabJobs)).ToList();
            int count = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                var (path, scene, prefab) = jobs[i];
                string description = prefab ? $"Prefab asset: {path}"
                    : scene.IsValid() ? $"Open scene: {scene.name}" : $"Scene asset: {path}";
                report?.Invoke(0.1f + 0.85f * i / jobs.Count, description);
                yield return count;
                if (prefab) { count += ModifyPrefab(path, modify); }
                else if (scene.IsValid() && scene.isLoaded) { count += ModifyScene(scene, modify, false); }
                else if (!string.IsNullOrEmpty(path)) { count += ModifySceneAsset(path, modify); }
            }

            // Pick up objects/scenes added while the user continued working
            var finalScenes = GetLoadedScenes();
            for (int i = 0; i < finalScenes.Count; i++)
            {
                report?.Invoke(0.95f + 0.05f * i / finalScenes.Count, $"Checking open scene: {finalScenes[i].name}");
                yield return count;
                if (finalScenes[i].IsValid() && finalScenes[i].isLoaded)
                    count += ModifyScene(finalScenes[i], modify, false);
            }
            report?.Invoke(1, "Asset scan complete.");
            yield return count;
        }

        static IEnumerable<string> FindAssets(string filter) => AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal);

        static void AddPrefabDependencies(string path, HashSet<string> visited, List<string> ordered)
        {
            if (!IsProjectPrefab(path) || !visited.Add(path))
                return;

            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                AddPrefabDependencies(dependency, visited, ordered);

            ordered.Add(path);
        }

        internal static List<Scene> GetLoadedScenes()
        {
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene))
                    scenes.Add(scene);
            }
            return scenes;
        }

        static int ModifyPrefab(string path, Func<GameObject, int> modify)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = modify(root);
                if (changed > 0)
                {
                    EnsureEditable(path);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);

                    if (!success)
                        throw new InvalidOperationException($"Could not save {path}.");
                }
                return changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static int ModifySceneAsset(string path, Func<GameObject, int> modify)
        {
            var existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded)
                return ModifyScene(existing, modify, false);

            bool wasListed = existing.IsValid();
            var activeScene = SceneManager.GetActiveScene();
            var selection = Selection.objects;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try { return ModifyScene(scene, modify, true); }
            finally
            {
                EditorSceneManager.CloseScene(scene, !wasListed);

                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);

                Selection.objects = selection;
            }
        }

        internal static int ModifyScene(Scene scene, Func<GameObject, int> modify, bool save)
        {
            int count = 0;

            foreach (var root in scene.GetRootGameObjects())
                count += modify(root);

            if (count > 0 && save)
            {
                EnsureEditable(scene.path);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException($"Could not save {scene.path}.");
            }

            return count;
        }

        internal static bool IsProjectPrefab(string path) => path.StartsWith("Assets/", StringComparison.Ordinal)
            && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        internal static void EnsureEditable(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException($"Asset is not editable: {path}. Check its file permissions or version control checkout.");
            if ((System.IO.File.GetAttributes(path) & System.IO.FileAttributes.ReadOnly) != 0)
                throw new InvalidOperationException($"Asset is read-only: {path}.");
        }
    }
}
