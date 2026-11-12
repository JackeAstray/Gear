using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "styler")]
    [CreateAssetMenu(fileName = "Styler Preset", menuName = "Evo/UI/Styler Preset")]
    public class StylerPreset : ScriptableObject
    {
        // Items
        public List<Styler.AudioItem> audioItems = new() { };
        public List<Styler.ColorItem> colorItems = new() { };
        public List<Styler.FontItem> fontItems = new() { };
        public List<Styler.GradientItem> gradientItems = new() { };
        public List<Styler.ShapeStyleItem> shapeStyleItems = new() { };
        public List<Styler.SpriteItem> spriteItems = new() { };
        public List<Styler.TextStyleItem> textStyleItems = new() { };

        // Settings
        [Tooltip("Default update mode for all StylerObjects using this preset.")]
        public Styler.UpdateMode updateMode = Styler.UpdateMode.Adaptive;
        [Tooltip("Objects using this preset keep it when another preset is applied globally (ApplyPreset or PersistentPreset). ApplyPresetTo still changes them.")]
        public bool keepOnGlobalApply = false;

        #region Runtime Lookup Cache
        [NonSerialized] Dictionary<string, Styler.AudioItem> audioLookup;
        [NonSerialized] Dictionary<string, Styler.ColorItem> colorLookup;
        [NonSerialized] Dictionary<string, Styler.FontItem> fontLookup;
        [NonSerialized] Dictionary<string, Styler.GradientItem> gradientLookup;
        [NonSerialized] Dictionary<string, Styler.ShapeStyleItem> shapeStyleLookup;
        [NonSerialized] Dictionary<string, Styler.SpriteItem> spriteLookup;
        [NonSerialized] Dictionary<string, Styler.TextStyleItem> textStyleLookup;

        bool TryGetItem<T>(List<T> items, ref Dictionary<string, T> lookup, string itemID, out T item) where T : Styler.Item
        {
            if (string.IsNullOrEmpty(itemID))
            {
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        T candidate = items[i];
                        if (candidate != null && candidate.itemID == itemID)
                        {
                            item = candidate;
                            return true;
                        }
                    }
                }

                item = null;
                return false;
            }

            if (lookup == null)
            {
                int capacity = items != null ? items.Count : 0;
                lookup = new Dictionary<string, T>(capacity, StringComparer.Ordinal);
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        T candidate = items[i];
                        if (candidate != null && !string.IsNullOrEmpty(candidate.itemID))
                            lookup.TryAdd(candidate.itemID, candidate);
                    }

                    // Former IDs resolve only where no current ID claims the name
                    for (int i = 0; i < items.Count; i++)
                    {
                        List<string> formerIDs = items[i]?.formerIDs;
                        if (formerIDs == null)
                            continue;

                        for (int j = 0; j < formerIDs.Count; j++)
                        {
                            if (!string.IsNullOrEmpty(formerIDs[j]))
                                lookup.TryAdd(formerIDs[j], items[i]);
                        }
                    }
                }
            }

            return lookup.TryGetValue(itemID, out item);
        }

        bool AddItem<T>(List<T> items, ref Dictionary<string, T> lookup, T item, string itemType) where T : Styler.Item
        {
            if (TryGetItem(items, ref lookup, item.itemID, out T existing) && existing.itemID == item.itemID)
            {
                Debug.LogWarning($"{itemType} item with ID '{item.itemID}' already exists.", this);
                return false;
            }

            // A new item takes its ID over from any item that had it before
            for (int i = 0; i < items.Count; i++)
                items[i]?.formerIDs?.Remove(item.itemID);

            items.Add(item);
            lookup = null;
            return true;
        }

        bool RemoveItem<T>(List<T> items, ref Dictionary<string, T> lookup, string itemID) where T : Styler.Item
        {
            if (!TryGetItem(items, ref lookup, itemID, out T item))
                return false;

            bool removed = items.Remove(item);
            if (removed)
                lookup = null;

            return removed;
        }

        void InvalidateLookupCaches()
        {
            audioLookup = null;
            colorLookup = null;
            fontLookup = null;
            textStyleLookup = null;
            gradientLookup = null;
            spriteLookup = null;
            shapeStyleLookup = null;
        }
        #endregion

        #region Try Get Methods
        /// <summary>
        /// Try get an audio clip from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetAudio(string itemID, out AudioClip clip)
        {
            if (TryGetItem(audioItems, ref audioLookup, itemID, out Styler.AudioItem item))
            {
                clip = item.audioAsset;
                return true;
            }

            clip = null;
            return false;
        }

        /// <summary>
        /// Try get a color from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetColor(string itemID, out Color color)
        {
            if (TryGetItem(colorItems, ref colorLookup, itemID, out Styler.ColorItem item))
            {
                color = item.colorValue;
                return true;
            }

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Try get a TextMesh Pro font from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetFont(string itemID, out TMP_FontAsset font)
        {
            if (TryGetFontItem(itemID, out Styler.FontItem item))
            {
                font = item.fontAsset;
                return true;
            }

            font = null;
            return false;
        }

        /// <summary>
        /// Try get the font defined for a text backend. Returns false if ID is missing.
        /// The font is null when the item has no font for the backend.
        /// </summary>
        public bool TryGetFont(string itemID, TextBackend backend, out UnityEngine.Object font)
        {
            if (TryGetFontItem(itemID, out Styler.FontItem item))
            {
                font = item.GetFont(backend);
                return true;
            }

            font = null;
            return false;
        }

        /// <summary>
        /// Try get the full font item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetFontItem(string itemID, out Styler.FontItem fontItem) => TryGetItem(fontItems, ref fontLookup, itemID, out fontItem);

        /// <summary>
        /// Try get a gradient from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetGradient(string itemID, out Gradient gradient)
        {
            if (TryGetItem(gradientItems, ref gradientLookup, itemID, out Styler.GradientItem item))
            {
                gradient = item.gradientValue;
                return true;
            }

            gradient = null;
            return false;
        }

        /// <summary>
        /// Try get the full shape style item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetShapeStyleItem(string itemID, out Styler.ShapeStyleItem shapeStyleItem)
        {
            return TryGetItem(shapeStyleItems, ref shapeStyleLookup, itemID, out shapeStyleItem);
        }

        /// <summary>
        /// Try get a sprite from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetSprite(string itemID, out Sprite sprite)
        {
            if (TryGetSpriteItem(itemID, out Styler.SpriteItem item))
            {
                sprite = item.spriteAsset;
                return true;
            }

            sprite = null;
            return false;
        }

        /// <summary>
        /// Try get the full sprite item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetSpriteItem(string itemID, out Styler.SpriteItem spriteItem)
        {
            return TryGetItem(spriteItems, ref spriteLookup, itemID, out spriteItem);
        }

        /// <summary>
        /// Try get the full text style item definition from the preset. Returns false if ID is missing.
        /// </summary>
        public bool TryGetTextStyleItem(string itemID, out Styler.TextStyleItem textStyleItem)
        {
            return TryGetItem(textStyleItems, ref textStyleLookup, itemID, out textStyleItem);
        }
        #endregion

        #region Get Methods
        /// <summary>
        /// Get audio clip from the preset.
        /// </summary>
        public AudioClip GetAudio(string itemID)
        {
            TryGetAudio(itemID, out AudioClip clip);
            return clip;
        }

        /// <summary>
        /// Get color from the preset.
        /// </summary>
        public Color GetColor(string itemID)
        {
            TryGetColor(itemID, out Color color);
            return color;
        }

        /// <summary>
        /// Get TextMesh Pro font from the preset.
        /// </summary>
        public TMP_FontAsset GetFont(string itemID)
        {
            TryGetFont(itemID, out TMP_FontAsset font);
            return font;
        }

        /// <summary>
        /// Get the font defined for a text backend from the preset.
        /// </summary>
        public UnityEngine.Object GetFont(string itemID, TextBackend backend)
        {
            TryGetFont(itemID, backend, out UnityEngine.Object font);
            return font;
        }

        /// <summary>
        /// Get gradient from the preset.
        /// </summary>
        public Gradient GetGradient(string itemID)
        {
            TryGetGradient(itemID, out Gradient gradient);
            return gradient;
        }

        /// <summary>
        /// Get the full shape style item definition from the preset.
        /// </summary>
        public Styler.ShapeStyleItem GetShapeStyleItem(string itemID)
        {
            TryGetShapeStyleItem(itemID, out Styler.ShapeStyleItem shapeStyleItem);
            return shapeStyleItem;
        }

        /// <summary>
        /// Get sprite from the preset.
        /// </summary>
        public Sprite GetSprite(string itemID)
        {
            TryGetSprite(itemID, out Sprite sprite);
            return sprite;
        }

        /// <summary>
        /// Get the full sprite item definition from the preset.
        /// </summary>
        public Styler.SpriteItem GetSpriteItem(string itemID)
        {
            TryGetSpriteItem(itemID, out Styler.SpriteItem spriteItem);
            return spriteItem;
        }

        /// <summary>
        /// Get the full text style item definition from the preset.
        /// </summary>
        public Styler.TextStyleItem GetTextStyleItem(string itemID)
        {
            TryGetTextStyleItem(itemID, out Styler.TextStyleItem textStyleItem);
            return textStyleItem;
        }
        #endregion

        #region Set Methods
        /// <summary>
        /// Set audio clip for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetAudio(string itemID, AudioClip audioClip)
        {
            if (TryGetItem(audioItems, ref audioLookup, itemID, out Styler.AudioItem item))
            {
                item.audioAsset = audioClip;
                return;
            }

            audioItems.Add(new Styler.AudioItem(itemID, audioClip));
            audioLookup = null;
        }

        /// <summary>
        /// Set color for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetColor(string itemID, Color color)
        {
            if (TryGetItem(colorItems, ref colorLookup, itemID, out Styler.ColorItem item))
            {
                item.colorValue = color;
                return;
            }

            colorItems.Add(new Styler.ColorItem(itemID, color));
            colorLookup = null;
        }

        /// <summary>
        /// Set TextMesh Pro font for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetFont(string itemID, TMP_FontAsset font)
        {
            if (TryGetItem(fontItems, ref fontLookup, itemID, out Styler.FontItem item))
            {
                item.fontAsset = font;
                return;
            }

            fontItems.Add(new Styler.FontItem(itemID, font));
            fontLookup = null;
        }

        /// <summary>
        /// Set gradient for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetGradient(string itemID, Gradient gradient)
        {
            if (TryGetItem(gradientItems, ref gradientLookup, itemID, out Styler.GradientItem item))
            {
                item.gradientValue = gradient;
                return;
            }

            gradientItems.Add(new Styler.GradientItem(itemID, gradient));
            gradientLookup = null;
        }

        /// <summary>
        /// Set sprite for an existing item or add new if it doesn't exist.
        /// </summary>
        public void SetSprite(string itemID, Sprite sprite)
        {
            if (TryGetSpriteItem(itemID, out Styler.SpriteItem item))
            {
                item.spriteAsset = sprite;
                return;
            }

            spriteItems.Add(new Styler.SpriteItem(itemID, sprite));
            spriteLookup = null;
        }
        #endregion

        #region Add Methods
        /// <summary>
        /// Add an audio item to the preset.
        /// </summary>
        public void AddAudio(string itemID, AudioClip audioClip)
        {
            AddItem(audioItems, ref audioLookup, new Styler.AudioItem(itemID, audioClip), "Audio");
        }

        /// <summary>
        /// Add a color item to the preset.
        /// </summary>
        public void AddColor(string itemID, Color color)
        {
            AddItem(colorItems, ref colorLookup, new Styler.ColorItem(itemID, color), "Color");
        }

        /// <summary>
        /// Add a font item with a TextMesh Pro font to the preset.
        /// </summary>
        public void AddFont(string itemID, TMP_FontAsset font)
        {
            AddItem(fontItems, ref fontLookup, new Styler.FontItem(itemID, font), "Font");
        }

        /// <summary>
        /// Add a gradient item to the preset.
        /// </summary>
        public void AddGradient(string itemID, Gradient gradient)
        {
            AddItem(gradientItems, ref gradientLookup, new Styler.GradientItem(itemID, gradient), "Gradient");
        }

        /// <summary>
        /// Add a shape style item to the preset.
        /// </summary>
        public void AddShapeStyle(string itemID)
        {
            AddItem(shapeStyleItems, ref shapeStyleLookup, new Styler.ShapeStyleItem(itemID), "Shape style");
        }

        /// <summary>
        /// Add a sprite item to the preset.
        /// </summary>
        public void AddSprite(string itemID, Sprite sprite)
        {
            AddItem(spriteItems, ref spriteLookup, new Styler.SpriteItem(itemID, sprite), "Sprite");
        }

        /// <summary>
        /// Add a text style item to the preset.
        /// </summary>
        public void AddTextStyle(string itemID)
        {
            AddItem(textStyleItems, ref textStyleLookup, new Styler.TextStyleItem(itemID), "Text style");
        }
        #endregion

        #region Remove Methods
        /// <summary>
        /// Remove an audio item from the preset.
        /// </summary>
        public bool RemoveAudio(string itemID) => RemoveItem(audioItems, ref audioLookup, itemID);

        /// <summary>
        /// Remove a color item from the preset.
        /// </summary>
        public bool RemoveColor(string itemID) => RemoveItem(colorItems, ref colorLookup, itemID);

        /// <summary>
        /// Remove a font item from the preset.
        /// </summary>
        public bool RemoveFont(string itemID) => RemoveItem(fontItems, ref fontLookup, itemID);

        /// <summary>
        /// Remove a gradient item from the preset.
        /// </summary>
        public bool RemoveGradient(string itemID) => RemoveItem(gradientItems, ref gradientLookup, itemID);

        /// <summary>
        /// Remove a shape style item from the preset.
        /// </summary>
        public bool RemoveShapeStyle(string itemID) => RemoveItem(shapeStyleItems, ref shapeStyleLookup, itemID);

        /// <summary>
        /// Remove a sprite item from the preset.
        /// </summary>
        public bool RemoveSprite(string itemID) => RemoveItem(spriteItems, ref spriteLookup, itemID);

        /// <summary>
        /// Remove a text style item from the preset.
        /// </summary>
        public bool RemoveTextStyle(string itemID) => RemoveItem(textStyleItems, ref textStyleLookup, itemID);
        #endregion

#if UNITY_EDITOR
        void OnValidate()
        {
            InvalidateLookupCaches();
            TidyFormerIDs(audioItems);
            TidyFormerIDs(colorItems);
            TidyFormerIDs(fontItems);
            TidyFormerIDs(gradientItems);
            TidyFormerIDs(shapeStyleItems);
            TidyFormerIDs(spriteItems);
            TidyFormerIDs(textStyleItems);

            if (updateMode == Styler.UpdateMode.Always || (!Application.isPlaying && updateMode == Styler.UpdateMode.Adaptive))
            {
                UnityEditor.EditorApplication.delayCall -= NotifyStylerObjects;
                UnityEditor.EditorApplication.delayCall += NotifyStylerObjects;
            }
        }

        // Duplicated or pasted items copy their former IDs. Each former ID belongs to the first item that lists it
        // and never shadows a current ID, the same order the runtime lookup resolves.
        static void TidyFormerIDs<T>(List<T> items) where T : Styler.Item
        {
            if (items == null)
                return;

            var claimed = UnityEngine.Pool.HashSetPool<string>.Get();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !string.IsNullOrEmpty(items[i].itemID))
                    claimed.Add(items[i].itemID);
            }

            for (int i = 0; i < items.Count; i++)
            {
                List<string> formerIDs = items[i]?.formerIDs;
                if (formerIDs == null)
                    continue;

                for (int j = 0; j < formerIDs.Count; j++)
                {
                    if (string.IsNullOrEmpty(formerIDs[j]) || !claimed.Add(formerIDs[j]))
                        formerIDs.RemoveAt(j--);
                }
            }

            UnityEngine.Pool.HashSetPool<string>.Release(claimed);
        }

        void NotifyStylerObjects()
        {
            if (this == null)
                return;

            var targets = UnityEngine.Pool.ListPool<IStylerHandler>.Get();
            if (Application.isPlaying) { targets.AddRange(Styler.RegisteredHandlers); }
            else
            {
#if UNITY_6000_4_OR_NEWER
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
#else
                var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
                foreach (var behaviour in behaviours)
                    if (behaviour is IStylerHandler handler)
                        targets.Add(handler);
            }

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var handler = targets[i];
                    if (handler is UnityEngine.Object target && target != null && handler.Preset == this)
                        handler.UpdateStyler();
                }
            }
            finally
            {
                UnityEngine.Pool.ListPool<IStylerHandler>.Release(targets);
            }
        }
#endif
    }
}