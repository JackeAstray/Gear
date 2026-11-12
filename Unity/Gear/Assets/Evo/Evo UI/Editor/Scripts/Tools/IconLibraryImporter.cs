using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;

    /// <summary>
    /// Imports the sprites in a folder and its subfolders into an Icon Library. Sprite names become icon IDs,
    /// the nearest subfolder becomes the category, and sprite dimensions determine the resolution.
    /// </summary>
    public class IconLibraryImporter : EditorWindow
    {
        [SerializeField] IconLibrary targetLibrary;
        [SerializeField] string rootPath = "Assets/Evo/Evo UI/Sprites/Icons";
        [SerializeField] bool clearExisting;
        string importSummary;
        bool importSucceeded;
        bool importFailed;

        ObjectField libraryField;
        ObjectField folderField;
        EvoEditorUITK.SegmentedControl<bool> modes;
        Label modeDescription;
        Label validation;
        EvoEditorUITK.StatusView status;
        Button importButton;

        public static void ShowWindow() => ShowWindow(null);

        public static void ShowWindow(IconLibrary library)
        {
            var window = GetWindow<IconLibraryImporter>();
            if (library != null)
                window.targetLibrary = library;

            window.importSummary = null;
            window.Show();
            window.Refresh();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Icon Library Importer");
            minSize = new Vector2(320, 340);

            if (targetLibrary == null)
                targetLibrary = IconLibrary.GetDefault();

            EditorApplication.projectChanged += Refresh;
        }

        void OnDisable() => EditorApplication.projectChanged -= Refresh;

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            EvoEditorUITK.Apply(this);

            root.Add(EvoEditorUITK.CreateHeader("Import Icons", "Adds the sprites in a folder and its subfolders to an Icon Library. " +
                "Sprite names become icon IDs, the nearest subfolder becomes the category, and sprite size sets the resolution."));

            ScrollView body = EvoEditorUITK.CreateBody();
            root.Add(body);

            libraryField = new ObjectField("Library") { objectType = typeof(IconLibrary), allowSceneObjects = false, tooltip = "The Icon Library to import into" };
            libraryField.AddToClassList("evo-field");
            libraryField.RegisterValueChangedCallback(evt =>
            {
                targetLibrary = evt.newValue as IconLibrary;
                importSummary = null;
                Refresh();
            });
            body.Add(libraryField);

            folderField = new ObjectField("Source Folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, tooltip = "A folder in the Project window" };
            folderField.AddToClassList("evo-field");
            folderField.RegisterValueChangedCallback(evt =>
            {
                rootPath = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : "";
                importSummary = null;
                Refresh();
            });
            body.Add(folderField);

            body.Add(EvoEditorUITK.CreateHeading("Existing Icons"));

            modes = new EvoEditorUITK.SegmentedControl<bool>(replace =>
            {
                clearExisting = replace;
                importSummary = null;
                Refresh();
            });
            modes.AddOption(false, "Keep", "Keep the library's icons and add the new icons and resolutions")
                .AddOption(true, "Replace", "Replace the library's icons, categories, and keywords");
            body.Add(modes);

            modeDescription = EvoEditorUITK.CreateDescription();
            body.Add(modeDescription);

            validation = EvoEditorUITK.CreateDescription(null, "evo-description--warning");
            body.Add(validation);

            status = new EvoEditorUITK.StatusView(() =>
            {
                importSummary = null;
                Refresh();
            });
            body.Add(status);

            var footer = new VisualElement();
            footer.AddToClassList("evo-footer");
            importButton = EvoEditorUITK.CreateButton(null, null, ImportIcons);
            importButton.AddToClassList("evo-primary");
            footer.Add(importButton);
            root.Add(footer);

            Refresh();
        }

        void Refresh()
        {
            if (importButton == null)
                return;

            libraryField.SetValueWithoutNotify(targetLibrary);
            folderField.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(rootPath));
            modes.SetValueWithoutNotify(clearExisting);

            modeDescription.text = clearExisting
                ? "The imported icons will replace this library's icons, categories, and keywords."
                : "New icons and new resolutions are added. Existing icons stay as they are.";
            modeDescription.EnableInClassList("evo-description--warning", clearExisting);

            bool validFolder = AssetDatabase.IsValidFolder(rootPath);
            string error = targetLibrary == null ? "Choose a target Icon Library."
                : !validFolder ? "Choose a valid source folder from the Project window." : null;
            validation.text = error ?? "";
            validation.style.display = error != null ? DisplayStyle.Flex : DisplayStyle.None;

            status.SetState(importSucceeded ? 1 : 0, importSummary, false, importFailed);

            importButton.text = clearExisting ? "Replace and Import Icons" : "Import Icons";
            importButton.SetEnabled(error == null);
        }

        void ImportIcons()
        {
            importSucceeded = importFailed = false;
            try { Import(); }
            finally { Refresh(); }
        }

        void Import()
        {
            if (targetLibrary == null || !AssetDatabase.IsValidFolder(rootPath))
                return;

            // Build a separate list first so an empty scan or failed import never clears the library
            var importedIcons = new List<IconLibrary.Item>();
            var itemsByID = new Dictionary<string, IconLibrary.Item>(StringComparer.Ordinal);
            if (!clearExisting && targetLibrary.icons != null)
            {
                foreach (var item in targetLibrary.icons)
                {
                    if (item == null)
                    {
                        importedIcons.Add(null);
                        continue;
                    }

                    var copy = new IconLibrary.Item
                    {
                        ID = item.ID,
                        category = item.category,
                        searchKeywords = item.searchKeywords != null ? new List<string>(item.searchKeywords) : new List<string>(),
                        resolutions = item.resolutions != null ? new List<IconLibrary.Item.Resolution>(item.resolutions) : new List<IconLibrary.Item.Resolution>()
                    };
                    importedIcons.Add(copy);

                    if (!string.IsNullOrEmpty(copy.ID) && !itemsByID.ContainsKey(copy.ID))
                        itemsByID.Add(copy.ID, copy);
                }
            }

            int importedCount = 0;
            int skippedCount = 0;
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { rootPath });
                var paths = new List<string>(guids.Length);
                foreach (string guid in guids)
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));

                paths.Sort(StringComparer.Ordinal);
                for (int i = 0; i < paths.Count; i++)
                {
                    string assetPath = paths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Importing Icons", assetPath, (float)i / paths.Count))
                    {
                        importSummary = "Import cancelled. The library was not changed.";
                        return;
                    }

                    string category = GetCategoryFromPath(Path.GetDirectoryName(assetPath), rootPath);
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (asset is not Sprite sprite || string.IsNullOrEmpty(sprite.name))
                            continue;

                        int resolution = Mathf.RoundToInt(Mathf.Max(sprite.rect.width, sprite.rect.height));
                        if (resolution <= 0)
                            continue;

                        if (!itemsByID.TryGetValue(sprite.name, out IconLibrary.Item item))
                        {
                            item = new IconLibrary.Item { ID = sprite.name, category = category };
                            importedIcons.Add(item);
                            itemsByID.Add(item.ID, item);
                        }

                        int existingIndex = item.resolutions.FindIndex(entry => entry != null && entry.resolution == resolution);
                        if (existingIndex >= 0 && item.resolutions[existingIndex].sprite != null)
                        {
                            skippedCount++;
                            continue;
                        }

                        var entry = new IconLibrary.Item.Resolution { resolution = resolution, sprite = sprite };
                        if (existingIndex >= 0)
                            item.resolutions[existingIndex] = entry;
                        else
                            item.resolutions.Add(entry);

                        importedCount++;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                importSummary = "Import failed. The library was not changed. See the Console for details.";
                importFailed = true;
                return;
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (importedCount == 0)
            {
                importSummary = skippedCount > 0
                    ? $"No changes. Skipped {skippedCount} existing icon resolutions."
                    : "No sprites found. The library was not changed.";
                return;
            }

            var importedCategories = new List<string>();
            if (!clearExisting && targetLibrary.categories != null)
            {
                foreach (string category in targetLibrary.categories)
                    AddCategory(importedCategories, category);
            }
            foreach (var item in importedIcons)
            {
                if (item != null)
                    AddCategory(importedCategories, item.category);
            }

            Undo.RecordObject(targetLibrary, "Import Icons");
            targetLibrary.icons = importedIcons;
            targetLibrary.categories = importedCategories;
            targetLibrary.InvalidateCache();
            EditorUtility.SetDirty(targetLibrary);
            AssetDatabase.SaveAssetIfDirty(targetLibrary);
            importSummary = $"Imported {importedCount} icon resolutions. Skipped {skippedCount} duplicates.";
            importSucceeded = true;
        }

        static void AddCategory(List<string> categories, string category)
        {
            if (!string.IsNullOrEmpty(category) && !categories.Contains(category))
                categories.Add(category);
        }

        static string GetCategoryFromPath(string fullPath, string rootPath)
        {
            fullPath = fullPath.Replace('\\', '/').TrimEnd('/');
            rootPath = rootPath.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(fullPath, rootPath, StringComparison.Ordinal))
                return "";

            return Path.GetFileName(fullPath);
        }
    }
}