using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.UI.Showcase;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Gear.UI.Showcase.Editor
{
    /// <summary>
    /// 展厅资源生成器：
    /// 1) 从 <c>UI Example</c> 场景的 150 张卡导出为独立预制体；
    /// 2) 生成 <see cref="ShowcaseCatalog"/>；
    /// 3) 生成 <c>UI Showcase Example</c> 示例场景。
    /// <para>
    /// 窗口 prefab 已改为直接维护（ShowcaseUIPlane / Detail / Audio / Party / Inventory），
    /// 生成器不再覆盖窗口层级，避免把手改的 prefab 冲掉。
    /// </para>
    /// </summary>
    public static class ShowcaseBuilder
    {
        private const string CardsRoot = "Assets/Gear/UI/Showcase/Cards";
        private const string CatalogFolder = "Assets/Gear/Resources/UI/Showcase";
        private const string CatalogPath = CatalogFolder + "/ShowcaseCatalog.asset";
        private const string WindowRoot = "Assets/Gear/Resources/Prefabs/UIs";
        private const string ExampleScenePath = "Assets/Gear/Scenes/Example/UI Showcase Example.unity";

        private sealed class PageSpec
        {
            public string Id;
            public string DisplayName;
            public string[] Categories;
        }

        private static readonly PageSpec[] PageSpecs =
        {
            new PageSpec
            {
                Id = "shape",
                DisplayName = "形状 · 图像",
                Categories = new[] { "形状", "图片", "圆角", "命中", "组合" },
            },
            new PageSpec
            {
                Id = "material",
                DisplayName = "材质 · 滤镜",
                Categories = new[]
                {
                    "渐变", "裁剪", "过渡", "颜色", "投影", "细节", "图案", "色调", "采样",
                    "虚线", "内阴影", "辉光", "目标", "边缘", "描边", "抖动", "滤镜",
                },
            },
            new PageSpec
            {
                Id = "effects",
                DisplayName = "效果 · 遮罩",
                Categories = new[] { "效果", "遮罩" },
            },
            new PageSpec
            {
                Id = "widgets",
                DisplayName = "控件",
                Categories = new[] { "控件" },
            },
            new PageSpec
            {
                Id = "text",
                DisplayName = "文本",
                Categories = new[] { "文本" },
            },
            new PageSpec
            {
                Id = "data",
                DisplayName = "数据",
                Categories = new[] { "数据", "RADAR" },
            },
            new PageSpec
            {
                Id = "motion",
                DisplayName = "动效 · 布局 · 导航",
                Categories = new[] { "动效", "布局", "导航" },
            },
            new PageSpec
            {
                Id = "overlays",
                DisplayName = "覆盖层",
                Categories = new[] { "覆盖层" },
            },
            new PageSpec
            {
                Id = "misc",
                DisplayName = "其他",
                Categories = new string[0],
            },
        };

        [MenuItem("Gear/UI 展厅/全部重建（卡 + 目录 + 场景）", false, 100)]
        public static void RebuildAll()
        {
            BuildCatalogAndCards();
            BuildExampleScene();
            Debug.Log("[ShowcaseBuilder] 全部重建完成（窗口 prefab 不参与生成）");
        }

        [MenuItem("Gear/UI 展厅/1. 重建卡预制体与目录", false, 110)]
        public static void BuildCatalogAndCards()
        {
            BuildCatalog(true);
        }

        /// <summary>只重建目录：复用已导出的卡片预制体，适合改分类/跳过空页后快速重跑。</summary>
        public static void RebuildCatalogOnly()
        {
            BuildCatalog(false);
        }

        private static void BuildCatalog(bool exportPrefabs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ShowcaseBuilder] Play Mode 下不生成资源");
                return;
            }

            GridLayoutGroup gallery = FindGalleryGrid();
            if (gallery == null)
            {
                Debug.LogError("[ShowcaseBuilder] 当前场景找不到 150 张卡的 GridLayoutGroup；请先打开 UI Example 场景");
                return;
            }

            List<ShowcasePage> pages = CreatePages();
            Dictionary<string, ShowcasePage> pageMap = new Dictionary<string, ShowcasePage>();
            for (int i = 0; i < pages.Count; i++) pageMap[pages[i].id] = pages[i];
            List<ShowcaseEntry> preservedExtras = LoadPreservedExtras();

            int total = 0;
            for (int i = 0; i < gallery.transform.childCount; i++)
            {
                Transform card = gallery.transform.GetChild(i);
                string title = Normalize(ReadText(card, "CardTitle"), card.name);
                string description = Normalize(ReadText(card, "CardDescription"), "");
                string category = ExtractCategory(title);
                PageSpec spec = FindSpec(category);
                ShowcasePage page = pageMap[spec.Id];

                string folder = CardsRoot + "/" + spec.Id;
                EnsureFolder(folder);
                string fileName = i.ToString("000") + "-" + MakeFileName(title);
                string prefabPath = folder + "/" + fileName + ".prefab";

                GameObject prefab;
                if (exportPrefabs)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                        AssetDatabase.DeleteAsset(prefabPath);

                    prefab = PrefabUtility.SaveAsPrefabAsset(card.gameObject, prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogError("[ShowcaseBuilder] 导出失败: " + title + " -> " + prefabPath);
                    }
                }
                else
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogError("[ShowcaseBuilder] 目录重建找不到预制体: " + prefabPath);
                    }
                }

                page.entries.Add(new ShowcaseEntry
                {
                    id = spec.Id + "/" + i.ToString("000"),
                    title = title,
                    description = description,
                    pageId = spec.Id,
                    tags = string.IsNullOrEmpty(category) ? null : new[] { category },
                    cardPrefab = prefab,
                });
                total++;
            }

            EnsureFolder(CatalogFolder);
            if (AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(CatalogPath) != null)
                AssetDatabase.DeleteAsset(CatalogPath);

            ShowcaseCatalog catalog = ScriptableObject.CreateInstance<ShowcaseCatalog>();
            catalog.SetPages(pages);
            for (int i = 0; i < preservedExtras.Count; i++)
                catalog.EditorAddEntry(preservedExtras[i].pageId, preservedExtras[i]);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder report = new StringBuilder();
            report.Append("[ShowcaseBuilder] 卡=").Append(total).Append(" 目录=").Append(CatalogPath);
            for (int i = 0; i < pages.Count; i++)
                report.Append("\n  ").Append(pages[i].displayName).Append(": ").Append(pages[i].entries.Count);
            Debug.Log(report.ToString());
        }

        private static List<ShowcaseEntry> LoadPreservedExtras()
        {
            var preserved = new List<ShowcaseEntry>();
            ShowcaseCatalog existing = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(CatalogPath);
            if (existing == null) return preserved;

            for (int i = 0; i < existing.Pages.Count; i++)
            {
                ShowcasePage page = existing.Pages[i];
                if (page?.entries == null) continue;
                for (int j = 0; j < page.entries.Count; j++)
                {
                    ShowcaseEntry entry = page.entries[j];
                    if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                    if (entry.id.IndexOf("/extras-", StringComparison.Ordinal) >= 0)
                        preserved.Add(entry);
                }
            }

            return preserved;
        }

        [MenuItem("Gear/UI 展厅/2. 定位主窗口 prefab（直接维护）", false, 111)]
        public static void SelectWindowPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowRoot + "/ShowcaseUIPlane.prefab");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ShowcaseBuilder] 窗口 prefab 已改为直接维护，生成器不会覆盖: " + WindowRoot);
        }

        [MenuItem("Gear/UI 展厅/3. 生成示例场景", false, 112)]
        public static void BuildExampleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ShowcaseBuilder] Play Mode 下不生成场景");
                return;
            }

            Scene loaded = SceneManager.GetSceneByPath(ExampleScenePath);
            if (loaded.isLoaded)
            {
                Debug.LogWarning("[ShowcaseBuilder] 示例场景已打开，请先关闭: " + ExampleScenePath);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExampleScenePath) != null)
                AssetDatabase.DeleteAsset(ExampleScenePath);

            EnsureFolder(Path.GetDirectoryName(ExampleScenePath).Replace('\\', '/'));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject launcher = new GameObject("ShowcaseLauncher", typeof(ShowcaseLauncher));
            SceneManager.MoveGameObjectToScene(launcher, scene);
            EditorSceneManager.SaveScene(scene, ExampleScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("[ShowcaseBuilder] 示例场景: " + ExampleScenePath);
        }

        private static GridLayoutGroup FindGalleryGrid()
        {
            GridLayoutGroup[] grids = UnityEngine.Object.FindObjectsByType<GridLayoutGroup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            GridLayoutGroup best = null;
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i] != null && grids[i].transform.childCount > 100
                    && (best == null || grids[i].transform.childCount > best.transform.childCount))
                {
                    best = grids[i];
                }
            }

            return best;
        }

        private static List<ShowcasePage> CreatePages()
        {
            List<ShowcasePage> pages = new List<ShowcasePage>(PageSpecs.Length);
            for (int i = 0; i < PageSpecs.Length; i++)
            {
                pages.Add(new ShowcasePage
                {
                    id = PageSpecs[i].Id,
                    displayName = PageSpecs[i].DisplayName,
                });
            }

            return pages;
        }

        private static PageSpec FindSpec(string category)
        {
            for (int i = 0; i < PageSpecs.Length; i++)
            {
                string[] categories = PageSpecs[i].Categories;
                for (int j = 0; j < categories.Length; j++)
                {
                    if (categories[j] == category) return PageSpecs[i];
                }
            }

            return PageSpecs[PageSpecs.Length - 1];
        }

        private static string ExtractCategory(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            int split = title.IndexOf(" · ", StringComparison.Ordinal);
            if (split <= 0) return string.Empty;
            return title.Substring(0, split).Trim().Trim('"');
        }

        private static string ReadText(Transform card, string objectName)
        {
            TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == objectName) return texts[i].text;
            }

            return null;
        }

        private static string Normalize(string value, string fallback)
        {
            string text = (value ?? string.Empty).Trim();
            while (text.Length > 0 && text[0] == '"') text = text.Substring(1).Trim();
            if (text.EndsWith("\"", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1).Trim();
            return text.Length > 0 ? text : (fallback ?? string.Empty);
        }

        private static string MakeFileName(string title)
        {
            StringBuilder sb = new StringBuilder(title.Length);
            for (int i = 0; i < title.Length && sb.Length < 32; i++)
            {
                char c = title[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else if (char.IsWhiteSpace(c) || c == '·') sb.Append('-');
            }

            string name = sb.ToString().Trim('-');
            return string.IsNullOrEmpty(name) ? "card" : name;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
