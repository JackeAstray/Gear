using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Editor
{
    [CreateAssetMenu(fileName = "Icon Library", menuName = "Evo/UI/Icon Library")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Evo.UI", sourceAssembly: "Evo.UI", sourceClassName: "IconLibrary")]
    public class IconLibrary : ScriptableObject
    {
        [Header("Note:\nThis is an editor only data holder."), Space]
        public List<string> categories = new();
        public List<Item> icons = new();
        Dictionary<string, Item> iconCache;
        int cachedIconCount;

        void OnValidate() => InvalidateCache();

        void EnsureCache()
        {
            int count = icons?.Count ?? 0;
            if (iconCache != null && cachedIconCount == count)
                return;

            iconCache = new Dictionary<string, Item>(StringComparer.Ordinal);
            cachedIconCount = count;
            if (icons == null)
                return;

            foreach (var item in icons)
            {
                if (item != null && !string.IsNullOrEmpty(item.ID) && !iconCache.ContainsKey(item.ID))
                    iconCache.Add(item.ID, item);
            }
        }

        public void InvalidateCache() => iconCache = null;

        public Item GetItem(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureCache();
            return iconCache.TryGetValue(id, out Item item) ? item : null;
        }

        public Sprite GetSprite(string id, int resolution = 0) => GetItem(id)?.GetSprite(resolution);

        public List<Item> Search(string query, string category = "")
        {
            var results = new List<Item>();
            if (icons == null)
                return results;

            query = query?.Trim();
            foreach (var item in icons)
            {
                if (item == null || string.IsNullOrEmpty(item.ID))
                    continue;
                if (!string.IsNullOrEmpty(category) && item.category != category)
                    continue;

                if (string.IsNullOrEmpty(query) || item.ID.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(item);
                    continue;
                }

                if (item.searchKeywords == null)
                    continue;

                foreach (string keyword in item.searchKeywords)
                {
                    if (!string.IsNullOrEmpty(keyword) && keyword.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(item);
                        break;
                    }
                }
            }
            return results;
        }

        public List<Item> GetItemsByCategory(string category)
        {
            var results = new List<Item>();
            if (icons != null)
            {
                foreach (var item in icons)
                {
                    if (item != null && item.category == category)
                        results.Add(item);
                }
            }
            return results;
        }

        /// <summary>
        /// Gets the default library from the Resources folder.
        /// </summary>
        public static IconLibrary GetDefault() => Resources.Load<IconLibrary>(Constants.DefaultIconLibraryPath);

        [Serializable]
        public class Item
        {
            public string ID;
            public string category;
            public List<string> searchKeywords = new();
            public List<Resolution> resolutions = new();

            public Sprite GetSprite(int resolution = 0)
            {
                Sprite fallback = null;
                if (resolutions == null)
                    return null;

                foreach (var entry in resolutions)
                {
                    if (entry == null || entry.sprite == null || entry.resolution <= 0)
                        continue;

                    if (fallback == null)
                        fallback = entry.sprite;
                    if (entry.resolution == resolution)
                        return entry.sprite;
                }
                return fallback;
            }

            public List<int> GetAvailableResolutions()
            {
                var values = new List<int>();
                if (resolutions != null)
                {
                    foreach (var entry in resolutions)
                    {
                        if (entry != null && entry.sprite != null && entry.resolution > 0 && !values.Contains(entry.resolution))
                            values.Add(entry.resolution);
                    }
                }
                values.Sort();
                return values;
            }

            [Serializable]
            public class Resolution
            {
                public int resolution = 64;
                public Sprite sprite;
            }
        }
    }
}