using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The demo pages the header search looks through, and how a query ranks them.
    /// A query is split into words and every word has to land somewhere: the page name, its category,
    /// an alias, an option label or the description. Where and how it lands sets the score.
    /// </summary>
    public class DemoSearchIndex
    {
        public class Category
        {
            public string name;
            public Sprite icon;
            public int tabIndex;
            public RadioButtonGroup variants;
            public TabManager tabs;
            internal string lowerName;
        }

        public class Page
        {
            public int category;
            public int variant;
            public string name;
            public string description;
            public string[] aliases;
            public string[] options;
            internal string lowerName;
            internal string lowerDescription;
            internal string[] lowerAliases;
            internal string[] lowerOptions;
        }

        public enum Reason { None, Alias, Option, Description }

        public struct Result
        {
            public Page page;
            public float score;
            public Reason reason;
            public string reasonText;
            public int reasonStart;
            public int reasonLength;

            // Bit i marks name character i as a hit
            public ulong nameMarks;
        }

        public readonly List<Category> categories = new();
        public readonly List<Page> pages = new();

        // The query's words as start and length in the lowercase buffer, so a search allocates nothing
        readonly List<Vector2Int> words = new();
        char[] lowered = new char[64];

        public Category AddCategory(string name, Sprite icon, int tabIndex, RadioButtonGroup variants, TabManager tabs)
        {
            var category = new Category { name = name, icon = icon, tabIndex = tabIndex, variants = variants, tabs = tabs, lowerName = Lower(name) };
            categories.Add(category);
            return category;
        }

        public Page AddPage(int category, int variant, string name, string description, string[] aliases, string[] options)
        {
            var page = new Page
            {
                category = category,
                variant = variant,
                name = name,
                description = description ?? string.Empty,
                aliases = aliases ?? Array.Empty<string>(),
                options = options ?? Array.Empty<string>(),
                lowerName = Lower(name),
                lowerDescription = Lower(description),
            };

            page.lowerAliases = new string[page.aliases.Length];
            for (int i = 0; i < page.aliases.Length; i++)
                page.lowerAliases[i] = Lower(page.aliases[i]);

            page.lowerOptions = new string[page.options.Length];
            for (int i = 0; i < page.options.Length; i++)
                page.lowerOptions[i] = Lower(page.options[i]);

            pages.Add(page);
            return page;
        }

        /// <summary>
        /// Ranks every page against the query, best first. Exact hits come first; only when nothing hits
        /// does a typo-tolerant pass run, so "sldr" still finds Slider.
        /// </summary>
        public void Search(ReadOnlySpan<char> query, List<Result> results)
        {
            results.Clear();
            words.Clear();

            // Lowercase, split on white space
            if (lowered.Length < query.Length)
                lowered = new char[Mathf.NextPowerOfTwo(query.Length)];

            int start = -1;
            for (int i = 0; i <= query.Length; i++)
            {
                if (i < query.Length && !char.IsWhiteSpace(query[i]))
                {
                    lowered[i] = char.ToLowerInvariant(query[i]);
                    if (start < 0)
                        start = i;
                }
                else if (start >= 0)
                {
                    words.Add(new Vector2Int(start, i - start));
                    start = -1;
                }
            }

            if (words.Count == 0)
                return;

            Pass(false, results);
            if (results.Count == 0)
                Pass(true, results);

            // Insertion sort: List.Sort allocates in Unity's Mono. Rank is a total order, so the result is the same
            for (int i = 1; i < results.Count; i++)
            {
                Result result = results[i];
                int j = i - 1;
                while (j >= 0 && Rank(results[j], result) > 0)
                {
                    results[j + 1] = results[j];
                    j--;
                }

                results[j + 1] = result;
            }
        }

        // Best score first; ties keep the dock and sidebar order
        static int Rank(Result a, Result b)
        {
            int c = b.score.CompareTo(a.score);
            if (c != 0)
                return c;

            c = a.page.category.CompareTo(b.page.category);
            return c != 0 ? c : a.page.variant.CompareTo(b.page.variant);
        }

        void Pass(bool fuzzy, List<Result> results)
        {
            for (int p = 0; p < pages.Count; p++)
            {
                Page page = pages[p];
                string category = categories[page.category].lowerName;
                var result = new Result { page = page };
                bool matched = true;

                foreach (Vector2Int range in words)
                {
                    ReadOnlySpan<char> word = lowered.AsSpan(range.x, range.y);
                    float best = 0f;
                    Reason reason = Reason.None;
                    string reasonText = null;
                    int reasonStart = 0;

                    int kind = HitKind(page.lowerName, word, out int at);
                    if (kind > 0)
                    {
                        best = kind == 3 ? 100f : kind == 2 ? 85f : 60f;
                        result.nameMarks |= Mask(at, word.Length);
                    }

                    kind = HitKind(category, word, out _);
                    if (kind >= 2 && best < 42f) { best = 42f; }
                    else if (kind == 1 && best < 26f) { best = 26f; }

                    // Aliases and option labels only for words of three letters or more, so "sl" stays on names
                    if (word.Length >= 3)
                    {
                        for (int i = 0; i < page.lowerAliases.Length; i++)
                        {
                            if (HitKind(page.lowerAliases[i], word, out _) >= 2 && best < 48f)
                            {
                                best = 48f;
                                reason = Reason.Alias;
                                reasonText = page.aliases[i];
                            }
                        }

                        for (int i = 0; i < page.lowerOptions.Length; i++)
                        {
                            kind = HitKind(page.lowerOptions[i], word, out at);
                            float score = kind == 3 ? 40f : kind == 2 ? 36f : 0f;
                            if (score > best)
                            {
                                best = score;
                                reason = Reason.Option;
                                reasonText = page.options[i];
                                reasonStart = at;
                            }
                        }

                        kind = HitKind(page.lowerDescription, word, out at);
                        if (kind >= 2 && best < 18f)
                        {
                            best = 18f;
                            reason = Reason.Description;
                            reasonText = page.description;
                            reasonStart = at;
                        }

                        if (best <= 0f && fuzzy && TrySubsequence(page.lowerName, word, out ulong marks))
                        {
                            best = 12f;
                            result.nameMarks |= marks;
                        }
                    }

                    if (best <= 0f)
                    {
                        matched = false;
                        break;
                    }

                    result.score += best;
                    if (reason != Reason.None && result.reason == Reason.None)
                    {
                        result.reason = reason;
                        result.reasonText = reasonText;
                        result.reasonStart = reasonStart;
                        result.reasonLength = word.Length;
                    }
                }

                if (!matched)
                    continue;

                // Shorter names win ties, so "Slider" sits above "Slider (Input)"
                result.score -= page.name.Length * 0.15f;
                results.Add(result);
            }
        }

        // 3 = the string starts with the word, 2 = a word inside starts with it, 1 = it sits inside a word, 0 = nowhere
        static int HitKind(string hay, ReadOnlySpan<char> word, out int at)
        {
            at = IndexOf(hay, word, 0);
            if (at < 0)
                return 0;
            if (at == 0)
                return 3;

            for (int i = at; i >= 0; i = IndexOf(hay, word, i + 1))
            {
                if (IsWordStart(hay, i))
                {
                    at = i;
                    return 2;
                }
            }

            return 1;
        }

        // Ordinal, like string.IndexOf(word, from, StringComparison.Ordinal)
        static int IndexOf(string hay, ReadOnlySpan<char> word, int from)
        {
            int at = hay.AsSpan(from).IndexOf(word);
            return at < 0 ? -1 : at + from;
        }

        // The word's letters in order, starting at a word start and staying close together
        static bool TrySubsequence(string hay, ReadOnlySpan<char> word, out ulong marks)
        {
            for (int start = 0; start < hay.Length; start++)
            {
                if (hay[start] != word[0] || !IsWordStart(hay, start))
                    continue;

                marks = Mask(start, 1);
                int k = 1, last = start;
                for (int i = start + 1; i < hay.Length && k < word.Length; i++)
                {
                    if (hay[i] != word[k])
                        continue;

                    marks |= Mask(i, 1);
                    last = i;
                    k++;
                }

                if (k == word.Length && last - start < word.Length * 2.2f)
                    return true;
            }

            marks = 0;
            return false;
        }

        static bool IsWordStart(string text, int index)
        {
            if (index == 0)
                return true;

            char c = text[index - 1];
            return c == ' ' || c == '(' || c == '-' || c == '/';
        }

        static ulong Mask(int start, int length)
        {
            ulong mask = 0;
            for (int i = start; i < start + length && i < 64; i++)
                mask |= 1UL << i;

            return mask;
        }

        static string Lower(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();
    }
}