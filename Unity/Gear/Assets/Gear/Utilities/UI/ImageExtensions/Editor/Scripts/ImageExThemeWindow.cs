using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageEx 主题窗口：浏览主题资产、应用到场景、设为默认（写入 Resources/UITheme/ImageExDefault）。
    /// <para>
    /// 与 ImageEx 工具台分开：工具台面向单个组件的效果/批量，主题面向"整套换肤"。
    /// 所有修改走 Undo；"设为默认"会生成/覆盖 Resources 下的默认主题资产。
    /// </para>
    /// </summary>
    public sealed class ImageExThemeWindow : EditorWindow
    {
        private readonly List<ImageExThemeAsset> themes = new List<ImageExThemeAsset>();
        private Vector2 scroll;

        [MenuItem("Gear/ImageEx 主题", false, 21)]
        private static void Open()
        {
            ImageExThemeWindow window = GetWindow<ImageExThemeWindow>();
            window.titleContent = new GUIContent("ImageEx 主题");
            window.minSize = new Vector2(420f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshThemes();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新", GUILayout.Width(60f))) RefreshThemes();
                if (GUILayout.Button("应用默认主题到场景", GUILayout.Width(160f))) ApplyCurrentDefault();
                if (GUILayout.Button("检查场景绑定", GUILayout.Width(110f))) Repaint();
            }

            EditorGUILayout.HelpBox(
                "主题资产按 ID 存颜色/数值；ImageExThemeBinder 只认 tokenId。" +
                "「设为默认」会覆盖 Assets/Gear/Resources/UITheme/ImageExDefault.asset。",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (themes.Count == 0)
            {
                EditorGUILayout.HelpBox("工程里还没有 ImageExThemeAsset。\n右键 Assets > Create > Gear/UI/ImageEx Theme 新建。",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            foreach (ImageExThemeAsset theme in themes)
            {
                DrawThemeRow(theme);
            }

            EditorGUILayout.Space(6f);
            DrawSceneBinders();

            EditorGUILayout.EndScrollView();
        }

        private void DrawThemeRow(ImageExThemeAsset theme)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(theme, typeof(ImageExThemeAsset), false, GUILayout.Width(200f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("应用到场景", GUILayout.Width(90f))) ApplyTheme(theme);
                    if (GUILayout.Button("设为默认", GUILayout.Width(70f))) SetAsDefault(theme);
                    if (GUILayout.Button("定位", GUILayout.Width(46f)))
                    {
                        Selection.activeObject = theme;
                        EditorGUIUtility.PingObject(theme);
                    }
                }

                DrawSwatches(theme);
                GUILayout.Label($"颜色 {theme.Colors.Count} · 数值 {theme.Floats.Count}", EditorStyles.miniLabel);
            }
        }

        private static void DrawSwatches(ImageExThemeAsset theme)
        {
            if (theme.Colors.Count == 0) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                int shown = 0;
                foreach (ImageExThemeAsset.ColorToken token in theme.Colors)
                {
                    if (shown++ >= 10) break;

                    Rect rect = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                    EditorGUI.DrawRect(rect, token.color);
                    GUILayout.Label(string.IsNullOrEmpty(token.id) ? "-" : token.id, EditorStyles.miniLabel, GUILayout.Width(56f));
                }
            }
        }

        private void DrawSceneBinders()
        {
            ImageExThemeBinder[] binders = UnityEngine.Object.FindObjectsByType<ImageExThemeBinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            EditorGUILayout.LabelField($"场景内绑定：{binders.Length} 个", EditorStyles.boldLabel);

            ImageExThemeAsset theme = ImageExThemeService.Current;
            int unresolvedTotal = 0;
            foreach (ImageExThemeBinder binder in binders)
            {
                List<string> unresolved = binder.GetUnresolvedTokenIds(theme);
                unresolvedTotal += unresolved.Count;

                if (unresolved.Count == 0) continue;
                EditorGUILayout.HelpBox(
                    $"{binder.name} 未解析：{string.Join(", ", unresolved)}",
                    MessageType.Warning);
            }

            if (binders.Length > 0 && unresolvedTotal == 0)
            {
                EditorGUILayout.HelpBox("所有绑定都能在当前主题里解析。", MessageType.None);
            }
        }

        private void ApplyTheme(ImageExThemeAsset theme)
        {
            if (theme == null) return;

            ImageExThemeBinder[] binders = UnityEngine.Object.FindObjectsByType<ImageExThemeBinder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (ImageExThemeBinder binder in binders)
            {
                if (binder.TargetImage != null)
                {
                    Undo.RecordObject(binder.TargetImage, "应用 ImageEx 主题");
                }
            }

            ImageExThemeService.SetTheme(theme);

            foreach (ImageExThemeBinder binder in binders)
            {
                if (binder.TargetImage != null) UnityEditor.EditorUtility.SetDirty(binder.TargetImage);
                UnityEditor.EditorUtility.SetDirty(binder);
            }

            MarkSceneDirty();
            ShowNotification(new GUIContent($"已应用「{theme.name}」到 {binders.Length} 个绑定"));
        }

        private void ApplyCurrentDefault()
        {
            ImageExThemeAsset theme = ImageExThemeService.LoadDefault();
            if (theme == null)
            {
                ShowNotification(new GUIContent("还没有默认主题（Resources/UITheme/ImageExDefault）"));
                return;
            }

            ApplyTheme(theme);
        }

        private void SetAsDefault(ImageExThemeAsset theme)
        {
            if (theme == null) return;

            string targetPath = "Assets/Gear/Resources/" + ImageExThemeService.DefaultResourcePath + ".asset";
            string folder = Path.GetDirectoryName(targetPath)?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(folder)) EnsureFolder(folder);

            ImageExThemeAsset existing = AssetDatabase.LoadAssetAtPath<ImageExThemeAsset>(targetPath);
            if (existing == null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(theme);
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    Debug.LogError($"[ImageEx 主题] 复制主题失败：{sourcePath} -> {targetPath}");
                    return;
                }
            }
            else
            {
                UnityEditor.EditorUtility.CopySerialized(theme, existing);
                UnityEditor.EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            RefreshThemes();
            ShowNotification(new GUIContent("已设为默认主题"));
        }

        private void RefreshThemes()
        {
            themes.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:ImageExThemeAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ImageExThemeAsset theme = AssetDatabase.LoadAssetAtPath<ImageExThemeAsset>(path);
                if (theme != null) themes.Add(theme);
            }

            themes.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private static void MarkSceneDirty()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;

            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
