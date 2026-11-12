using System;
using System.Collections.Generic;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Evo.UI
{
    /// <summary>
    /// Routes text and input field operations to the backend that owns each component.
    /// TextMesh Pro is built in. UniText is added automatically when its package is installed.
    /// </summary>
    [NoAutoStaticsCleanup]
    public static class TextProvider
    {
        static readonly List<TextBackend> backends = new()
        {
            TMPTextBackend.Instance,
#if EVO_UNITEXT
            UniTextBackend.Instance,
#endif
        };

        static string selectedId;

        /// <summary>
        /// Registered backends, built-in ones first.
        /// </summary>
        public static IReadOnlyList<TextBackend> Backends => backends;

        /// <summary>
        /// Provider selected in Project Settings > Evo UI, or TextMesh Pro when it is not installed.
        /// Builds read no settings, as Evo UI components keep the provider they were saved with (see <see cref="Resolve"/>).
        /// </summary>
        public static TextBackend Active => Find(selectedId) ?? TMPTextBackend.Instance;

        /// <summary>
        /// Adds a custom backend. Built-in backends are registered automatically.
        /// </summary>
        public static void Register(TextBackend backend)
        {
            if (backend == null || Find(backend.Id) != null)
                return;

            backends.Add(backend);
        }

        /// <summary>
        /// Selects the active provider by id. The editor calls this when Project Settings load or change.
        /// </summary>
        public static void Select(string id) => selectedId = id;

        /// <summary>
        /// Returns the backend a component creates text with, kept in the component's own serialized id.
        /// While editing, the id follows <see cref="Active"/> and is saved with the owner, so builds create the same text.
        /// </summary>
        public static TextBackend Resolve(ref string id, Object owner)
        {
            // Components saved before text providers existed used TextMesh Pro, so they stay untouched
            string current = string.IsNullOrEmpty(id) ? TMPTextBackend.ID : id;
#if UNITY_EDITOR
            // Only an installed selection is synced, so early or partial script loads never rewrite saved data
            if (!Application.isPlaying && Find(selectedId) is TextBackend selected && current != selected.Id)
            {
                id = current = selected.Id;
                UnityEditor.EditorUtility.SetDirty(owner);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            }
#endif
            return Find(current) ?? TMPTextBackend.Instance;
        }

        #region Lookup
        public static TextBackend Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < backends.Count; i++)
            {
                if (backends[i].Id == id)
                    return backends[i];
            }

            return null;
        }

        /// <summary>
        /// Returns the backend that owns the text component, or null when it is not a supported text.
        /// </summary>
        public static TextBackend Find(Graphic text)
        {
            if (text == null)
                return null;

            for (int i = 0; i < backends.Count; i++)
            {
                if (backends[i].IsText(text))
                    return backends[i];
            }

            return null;
        }

        public static bool IsText(Graphic graphic) => Find(graphic) != null;

        /// <summary>
        /// Finds a supported text component on the same GameObject.
        /// </summary>
        public static bool TryGetText(Component source, out Graphic text)
        {
            if (source != null && source.TryGetComponent(out Graphic graphic) && IsText(graphic))
            {
                text = graphic;
                return true;
            }

            text = null;
            return false;
        }

        /// <summary>
        /// Returns the backend that owns the input field, or null when it is not a supported field.
        /// </summary>
        public static TextBackend FindInput(Object input)
        {
            if (input == null)
                return null;

            for (int i = 0; i < backends.Count; i++)
            {
                if (backends[i].IsInput(input))
                    return backends[i];
            }

            return null;
        }

        public static bool IsInput(Object input) => FindInput(input) != null;

        /// <summary>
        /// Finds a supported input field on the same GameObject.
        /// </summary>
        public static bool TryGetInput(Component source, out MonoBehaviour input)
        {
            if (source != null)
            {
                for (int i = 0; i < backends.Count; i++)
                {
                    Type type = backends[i].InputType;
                    if (type != null && source.TryGetComponent(type, out Component component))
                    {
                        input = (MonoBehaviour)component;
                        return true;
                    }
                }
            }

            input = null;
            return false;
        }
        #endregion

        #region Shortcuts
        public static string GetText(Graphic text)
        {
            TextBackend backend = Find(text);
            return backend != null ? backend.GetText(text) : string.Empty;
        }

        public static void SetText(Graphic text, string value) => Find(text)?.SetText(text, value);
        public static void SetText(Graphic text, char[] buffer, int start, int length) => Find(text)?.SetText(text, buffer, start, length);
        public static void SetText(Graphic text, StringBuilder buffer) => Find(text)?.SetText(text, buffer);

        /// <summary>
        /// Sets the text and returns true when it changed.
        /// </summary>
        public static bool UpdateText(Graphic text, string value)
        {
            TextBackend backend = Find(text);
            if (backend == null || backend.GetText(text) == value)
                return false;

            backend.SetText(text, value);
            return true;
        }

        public static float GetFontSize(Graphic text)
        {
            TextBackend backend = Find(text);
            return backend != null ? backend.GetFontSize(text) : 0f;
        }

        public static void SetFontSize(Graphic text, float size) => Find(text)?.SetFontSize(text, size);
        #endregion

        #region Input Shortcuts
        public static string GetValue(MonoBehaviour input)
        {
            TextBackend backend = FindInput(input);
            return backend != null ? backend.GetValue(input) : string.Empty;
        }

        public static bool ValueEquals(MonoBehaviour input, ReadOnlySpan<char> value)
        {
            TextBackend backend = FindInput(input);
            return backend != null && backend.ValueEquals(input, value);
        }

        /// <summary>
        /// Sets the field's value. Without notify, value change listeners are not called.
        /// </summary>
        public static void SetValue(MonoBehaviour input, string value, bool notify) => FindInput(input)?.SetValue(input, value, notify);

        public static Graphic GetTextComponent(MonoBehaviour input) => FindInput(input)?.GetTextComponent(input);

        public static void SetCharacterLimit(MonoBehaviour input, int limit) => FindInput(input)?.SetCharacterLimit(input, limit);

        public static bool IsFocused(MonoBehaviour input)
        {
            TextBackend backend = FindInput(input);
            return backend != null && backend.IsFocused(input);
        }
        #endregion
    }
}