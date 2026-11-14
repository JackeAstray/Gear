using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 搜索索引：构建期做小写化与分词，查询期只做 span 比较，不产生临时字符串。
    /// <para>
    /// 匹配规则：查询按空白切成多个词，全部命中才算匹配；
    /// 排序优先级：整词相等 &gt; 词前缀 &gt; 词内包含 &gt; 原文包含。
    /// 空查询返回全部条目。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Search Index")]
    [DisallowMultipleComponent]
    public sealed class GearSearchIndex : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private List<string> entries = new List<string>();
        [SerializeField] private List<GameObject> rows = new List<GameObject>();

        private string[] sources = Array.Empty<string>();
        private string[] lowered = Array.Empty<string>();
        private string[][] tokens = Array.Empty<string[]>();

        private readonly List<int> exact = new List<int>(16);
        private readonly List<int> prefix = new List<int>(16);
        private readonly List<int> contains = new List<int>(16);
        private readonly List<int> loose = new List<int>(16);

        public int Count => sources.Length;
        public IReadOnlyList<string> Entries => entries;

        public TMP_InputField InputField
        {
            get => inputField;
            set
            {
                Unbind();
                inputField = value;
                Bind();
            }
        }

        private void OnEnable()
        {
            if (sources.Length == 0 && entries.Count > 0) Build(entries);
            Bind();
            Filter();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (inputField == null) return;
            inputField.onValueChanged.RemoveListener(OnQueryChanged);
            inputField.onValueChanged.AddListener(OnQueryChanged);
        }

        private void Unbind()
        {
            if (inputField != null) inputField.onValueChanged.RemoveListener(OnQueryChanged);
        }

        private void OnQueryChanged(string _) => Filter();

        /// <summary>重建索引：只在数据变化时调用（小写化与分词只做一次）。</summary>
        public void Build(IList<string> items)
        {
            // OnEnable 会传 entries 自己进来；同一引用时先 Clear 会把数据清空
            if (!ReferenceEquals(items, entries))
            {
                entries.Clear();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++) entries.Add(items[i] ?? string.Empty);
                }
            }

            sources = new string[entries.Count];
            lowered = new string[entries.Count];
            tokens = new string[entries.Count][];
            for (int i = 0; i < entries.Count; i++)
            {
                sources[i] = entries[i];
                lowered[i] = entries[i].ToLowerInvariant();
                tokens[i] = Tokenize(lowered[i]);
            }
        }

        /// <summary>用已保存的 entries 重建索引（OnEnable / 场景重载用）。</summary>
        public void Rebuild() => Build(entries);

        /// <summary>把查询结果写入 results（会先 clear），返回命中数；内部缓冲复用，稳态无分配。</summary>
        public int Search(string query, List<int> results)
        {
            if (results == null) return 0;
            results.Clear();

            exact.Clear();
            prefix.Clear();
            contains.Clear();
            loose.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                for (int i = 0; i < sources.Length; i++) results.Add(i);
                return results.Count;
            }

            ReadOnlySpan<char> span = query.AsSpan();
            for (int i = 0; i < sources.Length; i++)
            {
                int rank = Rank(lowered[i], tokens[i], span);
                switch (rank)
                {
                    case 0: exact.Add(i); break;
                    case 1: prefix.Add(i); break;
                    case 2: contains.Add(i); break;
                    case 3: loose.Add(i); break;
                }
            }

            results.AddRange(exact);
            results.AddRange(prefix);
            results.AddRange(contains);
            results.AddRange(loose);
            return results.Count;
        }

        /// <summary>按输入框当前文本过滤行（rows 与 entries 同序）。</summary>
        public void Filter()
        {
            string query = inputField != null ? inputField.text : string.Empty;
            var hits = new List<int>();
            Search(query, hits);

            var visible = new bool[entries.Count];
            for (int i = 0; i < hits.Count; i++)
            {
                int index = hits[i];
                if (index >= 0 && index < visible.Length) visible[index] = true;
            }

            for (int i = 0; i < rows.Count && i < visible.Length; i++)
            {
                if (rows[i] != null && rows[i].activeSelf != visible[i]) rows[i].SetActive(visible[i]);
            }
        }

        public void SetRows(IList<GameObject> items)
        {
            rows.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++) rows.Add(items[i]);
            }
        }

        /// <summary>0 整词 / 1 前缀 / 2 词内 / 3 原文 / -1 不匹配。</summary>
        private static int Rank(string text, string[] entryTokens, ReadOnlySpan<char> query)
        {
            bool loose = false;
            int best = -1;
            int index = 0;

            while (index < query.Length)
            {
                while (index < query.Length && char.IsWhiteSpace(query[index])) index++;
                int start = index;
                while (index < query.Length && !char.IsWhiteSpace(query[index])) index++;
                if (index == start) break;

                ReadOnlySpan<char> term = query.Slice(start, index - start);
                int tokenRank = RankToken(entryTokens, term);
                if (tokenRank < 0)
                {
                    if (text.AsSpan().IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0) return -1;
                    loose = true;
                }
                else if (tokenRank > best)
                {
                    best = tokenRank;
                }
            }

            if (best < 0) return loose ? 3 : -1;
            if (loose && best > 2) return 3;
            return best;
        }

        private static int RankToken(string[] entryTokens, ReadOnlySpan<char> term)
        {
            int best = -1;
            for (int i = 0; i < entryTokens.Length; i++)
            {
                ReadOnlySpan<char> token = entryTokens[i].AsSpan();
                if (token.Equals(term, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                if (token.Length > term.Length && token.Slice(0, term.Length).Equals(term, StringComparison.OrdinalIgnoreCase))
                {
                    if (best < 0 || best > 1) best = 1;
                }
                else if (token.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (best < 0 || best > 2) best = 2;
                }
            }

            return best;
        }

        private static string[] Tokenize(string lower)
        {
            var result = new List<string>(8);
            int index = 0;
            while (index < lower.Length)
            {
                while (index < lower.Length && !char.IsLetterOrDigit(lower[index])) index++;
                int start = index;
                while (index < lower.Length && char.IsLetterOrDigit(lower[index])) index++;
                if (index > start) result.Add(lower.Substring(start, index - start));
            }

            return result.ToArray();
        }
    }
}
