using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Project-wide Evo UI settings, edited in Project Settings > Evo UI.
    /// Stored in the ProjectSettings folder, so nothing is added to Assets and package updates never replace it.
    /// </summary>
    [Serializable, InitializeOnLoad]
    public class EvoUISettings
    {
        public const string FilePath = "ProjectSettings/Evo/EvoUISettings.json";

        /// <summary>
        /// Identifier of the text backend used whenever Evo UI creates text.
        /// </summary>
        public string textProvider = TMPTextBackend.ID;

        // Builds read no settings, so the selection is handed to the runtime whenever scripts load
        static EvoUISettings() => Load().Apply();

        public static EvoUISettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<EvoUISettings>(File.ReadAllText(FilePath)) ?? new EvoUISettings();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Evo UI] Could not read {FilePath}: {exception.Message}");
            }

            return new EvoUISettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            Apply();
        }

        public void Apply() => TextProvider.Select(textProvider);
    }
}