using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Validates and renames preset item IDs, updating their references within the requested scope.
    /// Preserves prefab inheritance and keeps both IDs valid during asset migrations
    /// so an interrupted migration can resume safely. Existing targets only redirect component references.
    /// Prefab-only migrations retain the source ID for scene users.
    /// A renamed ID stays on its item as a former ID, so references outside the scope keep resolving.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class StylerIDMigration
    {
        internal enum ApplyTo { SelectedObjects = 0, SceneObjects = 1, Prefabs = 2, EntireProject = 3 }

        // Items added in the inspector this session that no object has picked yet.
        // Nothing can use their old names, so renaming them keeps no former ID.
        static readonly HashSet<(StylerPreset preset, Styler.ItemType type, string id)> newItems = new();

        internal static void MarkNew(StylerPreset preset, Styler.ItemType type, string id) => newItems.Add((preset, type, id));

        /// <summary>
        /// Called when an object picks an ID, so its item keeps former IDs from now on.
        /// </summary>
        internal static void MarkUsed(StylerPreset preset, string id)
        {
            if (newItems.Count == 0)
                return;

            foreach (Styler.ItemType type in Enum.GetValues(typeof(Styler.ItemType)))
                newItems.Remove((preset, type, id));
        }

        static string ItemListName(Styler.ItemType type)
        {
            string name = type.ToString();
            return char.ToLowerInvariant(name[0]) + name[1..] + "Items";
        }

        internal static List<string> GetIDs(StylerPreset preset, Styler.ItemType type)
        {
            var ids = new List<string>();
            if (preset == null)
                return ids;

            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));
            for (int i = 0; i < items.arraySize; i++)
                ids.Add(items.GetArrayElementAtIndex(i).FindPropertyRelative("itemID").stringValue);

            return ids;
        }

        /// <summary>
        /// Maps each former ID that still resolves to the current ID of its item, matching the runtime lookup:
        /// current IDs win, then the first item that lists the former ID.
        /// </summary>
        internal static Dictionary<string, string> GetFormerIDs(StylerPreset preset, Styler.ItemType type)
        {
            var formerIDs = new Dictionary<string, string>();
            if (preset == null)
                return formerIDs;

            var ids = GetIDs(preset, type);
            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));

            for (int i = 0; i < items.arraySize; i++)
            {
                var itemFormerIDs = items.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(Styler.Item.formerIDs));

                for (int j = 0; j < itemFormerIDs.arraySize; j++)
                {
                    string id = itemFormerIDs.GetArrayElementAtIndex(j).stringValue;
                    if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                        formerIDs.TryAdd(id, ids[i]);
                }
            }

            return formerIDs;
        }

        internal static string Validate(StylerPreset preset, Styler.ItemType type, string source, string target)
        {
            if (preset == null)
                return "Select a Styler preset.";

            var ids = GetIDs(preset, type);
            bool formerSource = !string.IsNullOrEmpty(source) && !ids.Contains(source) && GetFormerIDs(preset, type).ContainsKey(source);
            if (string.IsNullOrEmpty(source) || !ids.Contains(source) && !formerSource)
                return "Choose an available source ID.";
            if (!formerSource && ids.Count(id => id == source) != 1)
                return "The source ID is duplicated in this preset. Resolve the duplicates first.";
            if (string.IsNullOrWhiteSpace(target))
                return "Enter a target ID.";
            if (source == target)
                return "Source and target IDs must differ.";
            if (ids.Count(id => id == target) > 1)
                return "The target ID is duplicated in this preset. Resolve the duplicates first.";
            if (formerSource && !ids.Contains(target))
                return "A former ID can only be migrated to an existing ID.";

            return null;
        }

        /// <summary>
        /// Renames the item at <paramref name="index"/>. The new ID leaves every former ID list, and the old ID
        /// becomes a former ID of this item unless another item still uses it or <paramref name="keepFormerID"/> is off.
        /// </summary>
        internal static void SetID(SerializedProperty items, int index, string id, bool keepFormerID = true)
        {
            var item = items.GetArrayElementAtIndex(index);
            var itemID = item.FindPropertyRelative(nameof(Styler.Item.itemID));
            string previous = itemID.stringValue;
            if (previous == id)
                return;

            itemID.stringValue = id;
            bool previousInUse = !keepFormerID || string.IsNullOrEmpty(previous);

            for (int i = 0; i < items.arraySize; i++)
            {
                var other = items.GetArrayElementAtIndex(i);
                var otherFormerIDs = other.FindPropertyRelative(nameof(Styler.Item.formerIDs));

                for (int j = otherFormerIDs.arraySize - 1; j >= 0; j--)
                {
                    if (otherFormerIDs.GetArrayElementAtIndex(j).stringValue == id)
                        otherFormerIDs.DeleteArrayElementAtIndex(j);
                }

                previousInUse |= i != index && other.FindPropertyRelative(nameof(Styler.Item.itemID)).stringValue == previous;
            }

            if (!previousInUse)
                AddFormerID(item, previous);
        }

        static void AddFormerID(SerializedProperty item, string id)
        {
            var formerIDs = item.FindPropertyRelative(nameof(Styler.Item.formerIDs));

            for (int i = 0; i < formerIDs.arraySize; i++)
            {
                if (formerIDs.GetArrayElementAtIndex(i).stringValue == id)
                    return;
            }

            formerIDs.arraySize++;
            formerIDs.GetArrayElementAtIndex(formerIDs.arraySize - 1).stringValue = id;
        }

        /// <summary>
        /// Renames an item from the preset inspector. Other presets that share the old ID can follow,
        /// so themes keep matching. Objects keep resolving the old ID through the former ID list.
        /// A new item that no object has picked yet is renamed in this preset only, without a former ID.
        /// </summary>
        internal static void Rename(StylerPreset preset, Styler.ItemType type, int index, string source, string target)
        {
            if (preset == null)
                return;

            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));
            if (index < 0 || index >= items.arraySize
                || items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(Styler.Item.itemID)).stringValue != source)
                return;

            bool isNew = newItems.Contains((preset, type, source));
            var others = new List<StylerPreset>();
            if (!isNew && !string.IsNullOrEmpty(source))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(StylerPreset), new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var other = AssetDatabase.LoadAssetAtPath<StylerPreset>(path);
                    if (other == null || other == preset || !AssetDatabase.IsOpenForEdit(path))
                        continue;

                    var otherIDs = GetIDs(other, type);
                    if (otherIDs.Contains(source) && !otherIDs.Contains(target))
                        others.Add(other);
                }
            }

            string conflict = null;
            if (!string.IsNullOrEmpty(target) && GetFormerIDs(preset, type).TryGetValue(target, out string owner) && owner != source)
                conflict = $"'{target}' is a former ID of '{owner}'. Objects that still use '{target}' will switch to this item.";

            bool renameOthers = false;
            if (others.Count > 0)
            {
                string message = $"'{source}' is also an ID in {string.Join(", ", others.Select(other => $"'{other.name}'"))}. " +
                    "Rename it there too, so those presets keep matching this one?" + (conflict != null ? "\n\n" + conflict : "");
                int choice = EditorUtility.DisplayDialogComplex("Rename Styler ID", message, "Rename in All", "Cancel", "Only This Preset");
                if (choice == 1)
                    return;

                renameOthers = choice == 0;
            }
            else if (conflict != null && !EditorUtility.DisplayDialog("Rename Styler ID", conflict, "Rename", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename Styler ID");

            SetID(items, index, target, !isNew);
            serialized.ApplyModifiedProperties();

            // The source stays listed, so the item is still new after an undo
            if (isNew) { newItems.Add((preset, type, target)); }
            else { newItems.Remove((preset, type, target)); }

            if (renameOthers)
            {
                foreach (var other in others)
                {
                    using var otherSerialized = new SerializedObject(other);
                    SetID(otherSerialized.FindProperty(ItemListName(type)), GetIDs(other, type).IndexOf(source), target);
                    otherSerialized.ApplyModifiedProperties();
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Removes every former ID of an item after confirmation. Objects that still use them show them as missing.
        /// </summary>
        internal static void ClearFormerIDs(StylerPreset preset, Styler.ItemType type, int index)
        {
            if (preset == null)
                return;

            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));
            if (index < 0 || index >= items.arraySize)
                return;

            var formerIDs = items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(Styler.Item.formerIDs));
            if (formerIDs.arraySize == 0)
                return;

            var names = new List<string>();

            for (int i = 0; i < formerIDs.arraySize; i++)
                names.Add($"'{formerIDs.GetArrayElementAtIndex(i).stringValue}'");

            string message = $"Objects that still use {string.Join(", ", names)} will show {(names.Count == 1 ? "it" : "them")} as missing. " +
                "Run Update References first if any still do.";
            if (!EditorUtility.DisplayDialog("Clear Former IDs", message, "Clear", "Cancel"))
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Clear Former IDs");
            formerIDs.ClearArray();
            serialized.ApplyModifiedProperties();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        static bool ItemValuesEqual(SerializedProperty source, SerializedProperty target)
        {
            var child = source.Copy();
            if (!child.Next(true))
                return true;

            do
            {
                if (child.depth <= source.depth)
                    break;
                if (child.name != nameof(Styler.Item.itemID) && child.name != nameof(Styler.Item.formerIDs)
                    && !SerializedProperty.DataEquals(child, target.FindPropertyRelative(child.name)))
                    return false;
            }
            while (child.Next(false));

            return true;
        }

        internal static IEnumerator<int> Run(StylerPreset preset, Styler.ItemType type, string source, string target,
            ApplyTo scope, Action<float, string> reportProgress = null)
        {
            string error = Validate(preset, type, source, target);
            if (error != null)
                throw new InvalidOperationException(error);

            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Exit Play Mode and Prefab Mode before migrating IDs.");

            var selected = Selection.gameObjects;
            if (scope == ApplyTo.SelectedObjects && selected.Length == 0)
                throw new InvalidOperationException("Select at least one object or prefab.");

            // Decide once per operation. An existing target is never a request to merge or delete preset entries,
            // even when its values match the source (including a retry after an interrupted rename).
            bool referencesOnly = GetIDs(preset, type).Contains(target);
            if (!referencesOnly)
                StylerProjectUtility.EnsureEditable(AssetDatabase.GetAssetPath(preset));

            if (scope == ApplyTo.SelectedObjects)
            {
                foreach (var root in selected)
                {
                    if (EditorUtility.IsPersistent(root))
                        StylerProjectUtility.EnsureEditable(AssetDatabase.GetAssetPath(root));
                }
            }

            // Save before renaming so cancelling an untitled scene's Save dialog changes no IDs.
            // Reference-only edits leave scenes unsaved: SaveOpenScenes can also flush dirty preset assets.
            if (scope == ApplyTo.SceneObjects && !referencesOnly && !EditorSceneManager.SaveOpenScenes())
            {
                yield return -1;
                yield break;
            }

            int count = 0;
            reportProgress?.Invoke(0, "Preparing migration...");
            yield return count;

            // Revalidate after yielding: the user may have edited the preset between updates
            error = Validate(preset, type, source, target);
            if (error != null)
                throw new InvalidOperationException(error);

            if (GetIDs(preset, type).Contains(target) != referencesOnly)
                throw new InvalidOperationException("The target ID changed while preparing migration. Run the migration again.");

            if (!referencesOnly)
                StylerProjectUtility.EnsureEditable(AssetDatabase.GetAssetPath(preset));

            // Selected edits stay in one update so unrelated editor actions cannot enter this Undo group
            if (scope == ApplyTo.SelectedObjects)
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Migrate Styler ID");

                try
                {
                    if (!referencesOnly)
                        RenameID(preset, type, source, target);

                    var visited = new HashSet<Component>();
                    var prefabs = new HashSet<GameObject>();

                    foreach (var root in selected)
                    {
                        if (root == null)
                            continue;

                        bool asset = EditorUtility.IsPersistent(root);
                        int changed = MigrateHierarchy(root, preset, type, source, target, false, true, visited);
                        count += changed;

                        if (asset && changed > 0)
                            prefabs.Add(root.transform.root.gameObject);
                    }

                    StylerProjectUtility.SavePrefabAssets(prefabs);
                }
                finally { Undo.CollapseUndoOperations(undoGroup); }
            }
            else
            {
                // Preserve a valid source ID across every yield and every possible save failure.
                // A subsequent run redirects remaining users without modifying the now-existing entries.
                if (!referencesOnly)
                    EnsureTargetID(preset, type, source, target);

                int MigrateRoot(GameObject root, bool preserveInheritance)
                {
                    // Asset scans yield between jobs; stop if either ID was removed or became ambiguous
                    string validationError = Validate(preset, type, source, target);
                    if (validationError != null || !GetIDs(preset, type).Contains(target))
                        throw new InvalidOperationException(validationError ?? "The target ID was removed during migration.");

                    return MigrateHierarchy(root, preset, type, source, target, preserveInheritance, false);
                }

                if (scope == ApplyTo.EntireProject || scope == ApplyTo.Prefabs)
                {
                    using var project = StylerProjectUtility.Run(
                        root => MigrateRoot(root, true), false,
                        (progress, description) => reportProgress?.Invoke(progress * 0.99f, description),
                        prefabsOnly: scope == ApplyTo.Prefabs);
                    while (project.MoveNext())
                    {
                        count = project.Current;
                        yield return count;
                    }
                }
                else
                {
                    var scenes = StylerProjectUtility.GetLoadedScenes();
                    for (int i = 0; i < scenes.Count; i++)
                    {
                        reportProgress?.Invoke(0.99f * i / scenes.Count, $"Scene {i + 1}/{scenes.Count}: {scenes[i].name}");
                        yield return count;
                        if (scenes[i].IsValid() && scenes[i].isLoaded)
                            count += StylerProjectUtility.ModifyScene(scenes[i], root => MigrateRoot(root, false), !referencesOnly);
                    }
                }

                reportProgress?.Invoke(0.99f, "Finishing preset ID migration...");
                // Finish in the same update as the last scene so no edit can enter before removal
                error = Validate(preset, type, source, target);
                if (error != null || !GetIDs(preset, type).Contains(target))
                    throw new InvalidOperationException(error ?? "The target ID was removed during migration.");

                // Scene references are outside the prefab-only scope and still need the old ID.
                // Only remove a source when this operation created the target, and its values still match.
                if (!referencesOnly && scope != ApplyTo.Prefabs)
                {
                    using var serialized = new SerializedObject(preset);
                    var ids = GetIDs(preset, type);
                    var items = serialized.FindProperty(ItemListName(type));
                    if (!ids.Contains(target) || !ItemValuesEqual(items.GetArrayElementAtIndex(ids.IndexOf(source)),
                        items.GetArrayElementAtIndex(ids.IndexOf(target))))
                        throw new InvalidOperationException("Preset values changed during migration. Both IDs have been retained.");

                    StylerProjectUtility.EnsureEditable(AssetDatabase.GetAssetPath(preset));
                    RemoveSourceID(preset, type, source, target);
                }
            }

            MarkUsed(preset, target);
            SceneView.RepaintAll();
            reportProgress?.Invoke(1, $"Complete. Updated {count} ID reference(s).");
            yield return count;
        }

        static void RenameID(StylerPreset preset, Styler.ItemType type, string source, string target)
        {
            using var serialized = new SerializedObject(preset);
            SetID(serialized.FindProperty(ItemListName(type)), GetIDs(preset, type).IndexOf(source), target);
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        static void EnsureTargetID(StylerPreset preset, Styler.ItemType type, string source, string target)
        {
            var ids = GetIDs(preset, type);
            if (ids.Contains(target))
                return; // An identical entry is safe to reuse when resuming

            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));
            int index = ids.IndexOf(source);
            // Keep both IDs valid while OnValidate/OnEnable and editor preview updates run
            items.InsertArrayElementAtIndex(index);
            SetID(items, index + 1, target);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        static void RemoveSourceID(StylerPreset preset, Styler.ItemType type, string source, string target)
        {
            var ids = GetIDs(preset, type);
            using var serialized = new SerializedObject(preset);
            var items = serialized.FindProperty(ItemListName(type));
            int sourceIndex = ids.IndexOf(source);
            int targetIndex = ids.IndexOf(target);
            var sourceFormerIDs = items.GetArrayElementAtIndex(sourceIndex).FindPropertyRelative(nameof(Styler.Item.formerIDs));
            var movedIDs = new List<string>();

            for (int i = 0; i < sourceFormerIDs.arraySize; i++)
                movedIDs.Add(sourceFormerIDs.GetArrayElementAtIndex(i).stringValue);

            // References outside the migrated scope keep resolving through the target
            movedIDs.Add(source);
            items.DeleteArrayElementAtIndex(sourceIndex);
            var targetItem = items.GetArrayElementAtIndex(targetIndex > sourceIndex ? targetIndex - 1 : targetIndex);

            foreach (string id in movedIDs)
            {
                if (id != target)
                    AddFormerID(targetItem, id);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        static int MigrateHierarchy(GameObject root, StylerPreset preset, Styler.ItemType type, string source,
            string target, bool preserveInheritance, bool undo, HashSet<Component> visited = null)
        {
            int count = 0;
            visited ??= new HashSet<Component>();
            string idName = ItemListName(type).Replace("Items", "ID");
            string mappingType = type + "Mapping";

            foreach (var candidate in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (candidate == null || candidate is not IStylerHandler originalHandler)
                    continue;

                var handler = StylerProjectUtility.ResolveHandler(originalHandler);
                if (handler is not Component component || component == null || !visited.Add(component))
                    continue;

                bool matchesPreset = EffectivePreset(component, handler) == preset;
                var inherited = preserveInheritance ? PrefabUtility.GetCorrespondingObjectFromSource(component) : null;
                bool inheritsPreset = inherited is IStylerHandler sourceHandler && EffectivePreset(inherited, sourceHandler) == preset
                    && StylerProjectUtility.IsProjectPrefab(AssetDatabase.GetAssetPath(inherited));
                if (!matchesPreset && !inheritsPreset)
                    continue;

                using var serialized = new SerializedObject(component);
                var paths = new HashSet<string>();
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.String && property.name == idName)
                        paths.Add(property.propertyPath);
                    else if (property.propertyType == SerializedPropertyType.Generic && property.type == mappingType)
                    {
                        var id = property.FindPropertyRelative("stylerID");
                        if (id != null)
                            paths.Add(id.propertyPath);
                    }
                }

                using var serializedSource = inheritsPreset ? new SerializedObject(inherited) : null;
                int changed = 0;
                var pinnedIDs = new List<string>();
                foreach (string path in paths)
                {
                    var id = serialized.FindProperty(path);
                    if (id.stringValue != source)
                        continue;

                    var inheritedID = serializedSource?.FindProperty(path);
                    bool willInheritRename = !id.prefabOverride && inheritedID != null
                        && inheritedID.propertyType == SerializedPropertyType.String && inheritedID.stringValue == source;
                    if (matchesPreset)
                    {
                        if (willInheritRename)
                            continue;

                        id.stringValue = target;
                    }
                    else
                    {
                        // This instance switched to another preset. Pin its old ID so changing the
                        // source prefab cannot silently redirect it to an unrelated/missing item.
                        if (!willInheritRename)
                            continue;

                        pinnedIDs.Add(path);
                    }

                    changed++;
                }

                if (changed == 0)
                    continue;

                StylerProjectUtility.ApplyProperties(serialized, undo);

                if (pinnedIDs.Count > 0)
                    PinInheritedIDs(component, pinnedIDs, source);

                StylerProjectUtility.MarkDirty(component);
                count += changed;
            }

            return count;
        }

        static void PinInheritedIDs(Component component, List<string> paths, string value)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(component);
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            var source = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, assetPath);
            if (source == null)
                throw new InvalidOperationException($"Could not preserve inherited IDs on {component.name}.");

            var modifications = (PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>()).ToList();
            foreach (string path in paths)
            {
                modifications.RemoveAll(modification => modification.target == source && modification.propertyPath == path);
                modifications.Add(new PropertyModification { target = source, propertyPath = path, value = value });
            }

            // SerializedObject discards a newly added override equal to the current source value.
            // An explicit modification is needed before that source changes later in the migration.
            PrefabUtility.SetPropertyModifications(root, modifications.ToArray());
        }

        static StylerPreset EffectivePreset(Component component, IStylerHandler handler)
        {
            bool usesDefault = component switch
            {
                StylerObject obj => obj.useDefaultStylerPreset,
                ProceduralShape shape => shape.useDefaultStylerPreset,
                Interactive interactive => interactive.useDefaultStylerPreset,
                ListView listView => listView.useDefaultStylerPreset,
                ChartBase chart => chart.useDefaultStylerPreset,
                VirtualCursor cursor => cursor.useDefaultStylerPreset,
                _ => false
            };

            return usesDefault ? Styler.GetDefaultPreset(false) : handler.Preset;
        }
    }
}
