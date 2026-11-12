using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evo.EditorTools
{
    /// <summary>
    /// UI Toolkit counterpart of EvoEditorGUI. Adds the colors and building blocks shared by Evo's editor windows
    /// (EvoEditorUITK.uss), creates the shared elements that use them, and finds the assets windows switch between.
    /// </summary>
    public static partial class EvoEditorUITK
    {
        #region Theme
        /// <summary>
        /// Adds the shared colors and building blocks to a window, then the window's own stylesheet, so its rules win
        /// over the shared ones. The window's sheet is loaded from the folder that holds the window's script, so a sheet
        /// with the same name elsewhere in the project is never picked up.
        /// </summary>
        public static void Apply(EditorWindow window, string styleSheetName = null)
        {
            Apply(window.rootVisualElement);
            if (string.IsNullOrEmpty(styleSheetName))
                return;

            var script = MonoScript.FromScriptableObject(window);
            string path = script != null ? $"{GetAssetFolder(script)}/{styleSheetName}.uss" : null;
            var styleSheet = path != null ? AssetDatabase.LoadAssetAtPath<StyleSheet>(path) : null;
            if (styleSheet == null)
            {
                Debug.LogWarning($"[Evo] Couldn't find {styleSheetName}.uss next to the {window.GetType().Name} script.");
                return;
            }

            AddStyleSheet(window.rootVisualElement, styleSheet);
        }

        /// <summary>
        /// Adds the shared colors and building blocks (EvoEditorUITK.uss) to an element, such as a window's root.
        /// Safe to call again.
        /// </summary>
        public static void Apply(VisualElement root)
        {
            root.AddToClassList("evo-window");

            // Set both, so applying again after the editor skin changed doesn't leave the old one behind
            root.EnableInClassList("evo-window--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("evo-window--light", !EditorGUIUtility.isProSkin);

            AddStyleSheet(root, FindThemeStyleSheet());
        }

        // Shared can live anywhere and has no window script to sit next to, so its sheet is found by its own distinctive name
        static StyleSheet FindThemeStyleSheet()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(EvoEditorUITK)} t:StyleSheet"))
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                if (styleSheet != null && styleSheet.name == nameof(EvoEditorUITK))
                    return styleSheet;
            }

            Debug.LogWarning($"[Evo] Couldn't find {nameof(EvoEditorUITK)}.uss. Reimport the Evo package to restore it.");
            return null;
        }

        static void AddStyleSheet(VisualElement root, StyleSheet styleSheet)
        {
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }
        #endregion

        #region Assets
        /// <summary>
        /// Every asset of a type in the project, sorted by path.
        /// </summary>
        public static List<T> FindAssets<T>() where T : Object
        {
            var assets = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    assets.Add(asset);
            }

            assets.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
            return assets;
        }

        /// <summary>
        /// The folder that holds an asset, with forward slashes, such as "Assets/Presets".
        /// </summary>
        public static string GetAssetFolder(Object asset)
        {
            return (Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)) ?? "").Replace('\\', '/');
        }

        // Every project asset lives under Assets, so the prefix only takes up room
        static string GetDisplayFolder(Object asset)
        {
            string folder = GetAssetFolder(asset);
            return folder.StartsWith("Assets/", StringComparison.Ordinal) ? folder["Assets/".Length..] : folder;
        }

        // Menu paths treat '/' as a submenu, so duplicates are told apart by their folder name only
        static List<string> GetMenuLabels<T>(List<T> assets) where T : Object
        {
            var nameCounts = assets.GroupBy(asset => asset.name).ToDictionary(group => group.Key, group => group.Count());
            return assets.ConvertAll(asset => nameCounts[asset.name] > 1
                ? $"{asset.name}  ({Path.GetFileName(GetAssetFolder(asset))})"
                : asset.name);
        }
        #endregion

        #region Text
        /// <summary>
        /// A count with its noun, such as "1 key" or "1,024 keys".
        /// </summary>
        public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count:N0} {noun}s";
        #endregion
    }
}