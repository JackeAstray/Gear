using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 展厅目录里的一张卡。
    /// <para>
    /// 只存元数据与卡片预制体引用；卡片本身是 <c>UI Example</c> 场景导出的独立 Prefab，
    /// 因此目录和展厅窗口都不会依赖那个 150 卡的大场景。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class ShowcaseEntry
    {
        [Tooltip("稳定 id；同页内唯一")]
        public string id;

        [Tooltip("卡片标题，直接显示在展厅里")]
        public string title;

        [TextArea(2, 4)]
        [Tooltip("卡片说明")]
        public string description;

        [Tooltip("所属页 id；用于从任意页反查")]
        public string pageId;

        [Tooltip("搜索用标签；可为空")]
        public string[] tags;

        [Tooltip("卡片预制体；为 null 时展厅显示占位卡，不报错")]
        public GameObject cardPrefab;
    }

    /// <summary>
    /// 展厅的一个分类页。
    /// </summary>
    [Serializable]
    public sealed class ShowcasePage
    {
        [Tooltip("稳定 id；窗口按 id 切页")]
        public string id;

        [Tooltip("分类页显示名")]
        public string displayName;

        public List<ShowcaseEntry> entries = new List<ShowcaseEntry>();
    }

    /// <summary>
    /// UI 展厅目录：分类页 + 卡片元数据 + 卡片预制体引用。
    /// <para>
    /// 默认从 <c>Resources/UI/Showcase/ShowcaseCatalog</c> 加载；没有目录时窗口显示错误提示而不是抛异常，
    /// 方便在没有跑过生成器的工程里先看空窗口。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gear/UI Showcase/Showcase Catalog", fileName = "ShowcaseCatalog")]
    public sealed class ShowcaseCatalog : ScriptableObject
    {
        /// <summary>默认 Resources 路径（不带扩展名）。</summary>
        public const string DefaultResourcePath = "UI/Showcase/ShowcaseCatalog";

        [SerializeField] private List<ShowcasePage> pages = new List<ShowcasePage>();

        /// <summary>分类页（只读）。</summary>
        public IReadOnlyList<ShowcasePage> Pages => pages;

        /// <summary>所有页的卡片总数。</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < pages.Count; i++)
                {
                    ShowcasePage page = pages[i];
                    if (page?.entries != null) total += page.entries.Count;
                }

                return total;
            }
        }

        /// <summary>按 id 找页；找不到返回 null。</summary>
        public ShowcasePage FindPage(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null && pages[i].id == id) return pages[i];
            }

            return null;
        }

        /// <summary>按标题/说明/标签做包含匹配；空查询返回 true。</summary>
        public static bool Matches(ShowcaseEntry entry, string query)
        {
            if (entry == null) return false;
            if (string.IsNullOrWhiteSpace(query)) return true;

            string q = query.Trim();
            if (Contains(entry.title, q) || Contains(entry.description, q)) return true;
            if (entry.tags != null)
            {
                for (int i = 0; i < entry.tags.Length; i++)
                {
                    if (Contains(entry.tags[i], q)) return true;
                }
            }

            return false;
        }

        /// <summary>由编辑器生成器调用：整体替换页列表；空页会被丢掉（例如没有卡片的“其他”）。</summary>
        public void SetPages(IList<ShowcasePage> value)
        {
            pages.Clear();
            if (value == null) return;
            for (int i = 0; i < value.Count; i++)
            {
                ShowcasePage page = value[i];
                if (page == null || page.entries == null || page.entries.Count == 0) continue;
                pages.Add(page);
            }
        }

        /// <summary>从默认 Resources 路径加载目录；不存在返回 null。</summary>
        public static ShowcaseCatalog LoadDefault()
        {
            return Resources.Load<ShowcaseCatalog>(DefaultResourcePath);
        }

#if UNITY_EDITOR
        /// <summary>编辑器直接补卡：追加到指定页；不重排已有条目。</summary>
        public void EditorAddEntry(string pageId, ShowcaseEntry entry)
        {
            ShowcasePage page = FindPage(pageId);
            if (page == null || entry == null) return;
            page.entries.Add(entry);
        }
#endif

        private static bool Contains(string source, string query)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
