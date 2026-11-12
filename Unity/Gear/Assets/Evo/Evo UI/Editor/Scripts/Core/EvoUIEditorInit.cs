using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Evo.UI.Editor
{
    [InitializeOnLoad, NoAutoStaticsCleanup]
    public static class EditorInit
    {
        static readonly StringComparison PathComparison = Application.platform == RuntimePlatform.WindowsEditor
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        static readonly NamedBuildTarget[] Targets = GetTargets();

        static string scriptPath = NormalizePath(GetSourcePath());

        static EditorInit() => QueueSynchronization();

        static string GetSourcePath([CallerFilePath] string path = "") => path;

        internal static void QueueSynchronization()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;

            EditorApplication.delayCall -= Synchronize;
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                QueueSynchronization();
                return;
            }

            SetDefine(File.Exists(scriptPath));
        }

        internal static void BeforeDelete(string assetPath)
        {
            // OnWillDeleteAsset must not use AssetDatabase APIs. Remove now, while
            // this assembly still exists, rather than waiting for the next reload.
            if (!ContainsScript(NormalizePath(assetPath)))
                return;

            EditorApplication.delayCall -= Synchronize;
            SetDefine(false);

            // Restore the symbol if another deletion processor cancels the deletion.
            // The file existence check also prevents re-adding it after removal.
            EditorApplication.delayCall += Synchronize;
        }

        internal static void AfterImport(string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (string.IsNullOrEmpty(scriptPath))
                return;

            for (int i = 0; i < movedFromAssetPaths.Length; i++)
            {
                if (string.IsNullOrEmpty(movedFromAssetPaths[i]) || string.IsNullOrEmpty(movedAssets[i]))
                    continue;

                string oldPath = NormalizePath(movedFromAssetPaths[i]);
                if (!ContainsScript(oldPath))
                    continue;

                scriptPath = NormalizePath(movedAssets[i]) + scriptPath[oldPath.Length..];
                QueueSynchronization();
            }

            // Best effort for external deletions if the old assembly is still loaded.
            // A script cannot guarantee cleanup after it has itself been unloaded.
            foreach (string deletedAsset in deletedAssets)
            {
                if (string.IsNullOrEmpty(deletedAsset) || !ContainsScript(NormalizePath(deletedAsset)))
                    continue;

                SetDefine(File.Exists(scriptPath));
                break;
            }
        }

        static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');

        static bool ContainsScript(string path) => string.Equals(scriptPath, path, PathComparison) || scriptPath.StartsWith(path + "/", PathComparison);

        private static NamedBuildTarget[] GetTargets()
        {
            var targets = new List<NamedBuildTarget>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            // NamedBuildTarget includes Server, which BuildTargetGroup.Standalone
            // alone does not cover. Reflect only public fields, once per domain.
            foreach (var field in typeof(NamedBuildTarget).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(NamedBuildTarget) || Attribute.IsDefined(field, typeof(ObsoleteAttribute)))
                    continue;

                var target = (NamedBuildTarget)field.GetValue(null);
                if (target != NamedBuildTarget.Unknown && names.Add(target.TargetName))
                    targets.Add(target);
            }

            return targets.ToArray();
        }

        private static void SetDefine(bool present)
        {
            foreach (var target in Targets)
            {
                try
                {
                    string current = PlayerSettings.GetScriptingDefineSymbols(target);
                    string updated = UpdateSymbols(current, present);
                    if (!string.Equals(current, updated, StringComparison.Ordinal))
                        PlayerSettings.SetScriptingDefineSymbols(target, updated);
                }
                catch (Exception exception)
                {
                    // One unavailable target must not prevent cleanup of the others.
                    Debug.LogWarning($"[Evo UI] Could not {(present ? "add" : "remove")} " +
                        $"{Constants.DefineSymbol} for {target.TargetName}: {exception.Message}");
                }
            }
        }

        private static string UpdateSymbols(string current, bool present)
        {
            var symbols = (current ?? string.Empty).Split(';');
            bool found = false;
            foreach (string symbol in symbols)
            {
                if (string.Equals(symbol.Trim(), Constants.DefineSymbol, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            // Preserve unrelated symbols and formatting; do not write on a no-op
            if (found == present)
                return current;

            if (present)
                return string.IsNullOrEmpty(current) ? Constants.DefineSymbol : current.TrimEnd(';') + ";" + Constants.DefineSymbol;

            var remaining = new List<string>(symbols.Length);
            foreach (string symbol in symbols)
            {
                if (!string.Equals(symbol.Trim(), Constants.DefineSymbol, StringComparison.Ordinal))
                    remaining.Add(symbol);
            }

            return string.Join(";", remaining);
        }
    }

    public class EvoUIAssetModificationProcessor : AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            EditorInit.BeforeDelete(assetPath);
            return AssetDeleteResult.DidNotDelete;
        }
    }

    public class EvoUIAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (deletedAssets.Length == 0 && movedAssets.Length == 0)
                return;

            EditorInit.AfterImport(deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}