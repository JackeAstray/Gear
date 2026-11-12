using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Evo.UI
{
    /// <summary>
    /// Edit-mode support for objects a component generates from a preset. Prefab asset presets stay linked,
    /// so prefab edits reach the preview, the saved objects, and the runtime objects that reuse them.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class GeneratedObjects
    {
        /// <summary>
        /// The preset's asset path when generated objects should link to it (prefab asset roots in edit mode), otherwise null.
        /// </summary>
        internal static string GetLinkPath(Object owner, Object preset)
        {
#if UNITY_EDITOR
            GameObject root = GameObjectOf(preset);
            if (root != null && root.transform.parent == null && !Application.IsPlaying(owner) && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(root))
                return UnityEditor.AssetDatabase.GetAssetPath(root);
#endif
            return null;
        }

        internal static T Create<T>(T preset, Transform parent, string linkPath) where T : Object
        {
#if UNITY_EDITOR
            if (linkPath != null)
            {
                var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(GameObjectOf(preset), parent);
                return preset is Component ? instance.GetComponent(preset.GetType()) as T : instance as T;
            }
#endif
            return Object.Instantiate(preset, parent);
        }

        /// <summary>
        /// Whether a generated object lost its link and should be rebuilt. Copies owned by an outer prefab instance
        /// are left alone, since only that prefab can replace them.
        /// </summary>
        internal static bool NeedsRelink(Object instance, Object preset, string linkPath)
        {
#if UNITY_EDITOR
            if (linkPath == null || instance == null)
                return false;

            GameObject target = GameObjectOf(instance);
            return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSourceAtPath(target, linkPath) != GameObjectOf(preset)
                && (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target) || UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(target));
#else
            return false;
#endif
        }

        /// <summary>
        /// Fills empty slots with the parent's linked children that no slot holds, then destroys the rest. These appear when
        /// a prefab regenerates objects that an instance of it lists through its own overrides. Returns whether anything changed.
        /// </summary>
        internal static bool ClaimOrphans<T>(List<T> objects, int count, Transform parent, T preset, string linkPath, Action<T> destroy) where T : Object
        {
#if UNITY_EDITOR
            if (!FindOrphans(objects, parent, preset, linkPath))
                return false;

            int slot = 0;
            for (int i = 0; i < orphans.Count; i++)
            {
                while (slot < count && objects[slot] != null)
                    slot++;

                T orphan = preset is Component ? orphans[i].GetComponent(preset.GetType()) as T : orphans[i] as T;
                if (slot < count) { objects[slot++] = orphan; }
                else { destroy(orphan); }
            }

            orphans.Clear();
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Whether the parent has linked children that no slot holds, which a refresh would claim or destroy.
        /// </summary>
        internal static bool HasOrphans<T>(List<T> objects, Transform parent, T preset, string linkPath) where T : Object
        {
#if UNITY_EDITOR
            bool found = FindOrphans(objects, parent, preset, linkPath);
            orphans.Clear();
            return found;
#else
            return false;
#endif
        }

        static GameObject GameObjectOf(Object target) => target is Component component ? component.gameObject : target as GameObject;

#if UNITY_EDITOR
        static readonly List<GameObject> orphans = new();
        static readonly Dictionary<Component, Action> watchers = new();
        static readonly List<Component> removedWatchers = new();
        static readonly List<Action> pendingRefreshes = new();

        static bool FindOrphans<T>(List<T> objects, Transform parent, T preset, string linkPath) where T : Object
        {
            orphans.Clear();
            if (linkPath == null || parent == null)
                return false;

            GameObject source = GameObjectOf(preset);
            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(child) && !Holds(objects, child)
                    && UnityEditor.PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, linkPath) == source)
                    orphans.Add(child);
            }
            return orphans.Count > 0;
        }

        static bool Holds<T>(List<T> objects, GameObject target) where T : Object
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && GameObjectOf(objects[i]) == target)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Records values set from script on prefab instance parts. Unity reverts unrecorded ones when it merges a prefab change.
        /// </summary>
        internal static void Record(params Object[] targets)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(targets[i]))
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(targets[i]);
            }
        }

        /// <summary>
        /// Calls refresh after Unity updates a prefab instance inside or around the owner. A preset edit can overwrite
        /// generated values that matched the old prefab (those aren't overrides), so the owner applies them again.
        /// Call from OnValidate. Destroyed owners are dropped when a scene closes or another owner registers.
        /// </summary>
        internal static void Watch(Component owner, Action refresh)
        {
            if (owner == null || UnityEditor.EditorUtility.IsPersistent(owner))
                return;

            if (!watchers.ContainsKey(owner))
            {
                Prune();
                if (watchers.Count == 0)
                {
                    UnityEditor.PrefabUtility.prefabInstanceUpdated += OnPrefabInstanceUpdated;
                    UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += OnSceneClosed;
                }
            }

            watchers[owner] = refresh;
        }

        static void OnPrefabInstanceUpdated(GameObject instance)
        {
            if (instance == null)
                return;

            Transform root = instance.transform;
            foreach (var watcher in watchers)
            {
                if (watcher.Key == null || Application.IsPlaying(watcher.Key))
                    continue;

                Transform owner = watcher.Key.transform;
                if (root.IsChildOf(owner) || owner.IsChildOf(root))
                    pendingRefreshes.Add(watcher.Value);
            }

            Prune();

            // Refreshes can register again, so they run after the loop
            for (int i = 0; i < pendingRefreshes.Count; i++)
                pendingRefreshes[i]?.Invoke();

            pendingRefreshes.Clear();
        }

        static void OnSceneClosed(UnityEngine.SceneManagement.Scene scene) => Prune();

        // Drops destroyed owners, so the registry never keeps closed scenes' components alive
        static void Prune()
        {
            foreach (var watcher in watchers)
            {
                if (watcher.Key == null)
                    removedWatchers.Add(watcher.Key);
            }

            for (int i = 0; i < removedWatchers.Count; i++)
                watchers.Remove(removedWatchers[i]);

            removedWatchers.Clear();
            if (watchers.Count == 0)
            {
                UnityEditor.PrefabUtility.prefabInstanceUpdated -= OnPrefabInstanceUpdated;
                UnityEditor.SceneManagement.EditorSceneManager.sceneClosed -= OnSceneClosed;
            }
        }
#endif
    }
}
