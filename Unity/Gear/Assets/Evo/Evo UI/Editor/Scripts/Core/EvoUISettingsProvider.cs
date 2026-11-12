using System;
using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;

    /// <summary>
    /// Project Settings > Evo UI.
    /// </summary>
    [NoAutoStaticsCleanup]
    class EvoUISettingsProvider : SettingsProvider
    {
        const string SettingsPath = "Project/Evo UI";

        // Supported out of the box, so they're listed even before their package is installed
        static readonly (string id, string name)[] KnownProviders = { ("UniText", "UniText") };

        static readonly TextConverter.Options ConvertOptions = new();
        static EvoUISettings settings;

        // Kept for the session, so they're still shown after visiting another page
        static string switchedTo;
        static string result;
        static bool resultFailed;

        VisualElement providerList;
        Label convertIntro;
        VisualElement switchCallout;
        VisualElement prefabOptions;
        VisualElement prefabWarning;
        Button convertButton;
        EvoEditorUITK.StatusView status;

        EvoUISettingsProvider() : base(SettingsPath, SettingsScope.Project, new[] { "Evo", "Text", "TextMesh Pro", "TMP", "UniText", "Font" }) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new EvoUISettingsProvider();

        public static void Open() => SettingsService.OpenProjectSettings(SettingsPath);

        // Re-read on open, so changes from version control show up
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            settings = EvoUISettings.Load();
            settings.Apply();
            CreateGUI(rootElement);
        }

        #region Layout
        void CreateGUI(VisualElement rootElement)
        {
            // The settings window reuses its panel for every page, so Evo's styles go on a page of their own
            var page = new VisualElement();
            page.AddToClassList("evo-settings");
            EvoEditorUITK.Apply(page);
            StyleSheet styleSheet = FindStyleSheet();
            if (styleSheet != null)
                page.styleSheets.Add(styleSheet);
            rootElement.Add(page);

            // UI Toolkit pages draw their own title, unlike IMGUI ones
            var title = new Label(label);
            title.AddToClassList("evo-settings__title");
            page.Add(title);

            ScrollView body = EvoEditorUITK.CreateBody();
            page.Add(body);

            body.Add(EvoEditorUITK.CreateHeading("Text Provider", "evo-heading--first"));
            providerList = new VisualElement();
            providerList.AddToClassList("evo-well");
            providerList.AddToClassList("evo-settings__providers");
            body.Add(providerList);
            body.Add(EvoEditorUITK.CreateDescription("Evo UI components, Styler and the Create menu create text with the selected provider.", "evo-settings__note"));

            body.Add(EvoEditorUITK.CreateHeading("Convert Existing Text"));
            convertIntro = EvoEditorUITK.CreateDescription(null, "evo-settings__intro");
            body.Add(convertIntro);

            switchCallout = EvoEditorUITK.CreateCallout(null);
            body.Add(switchCallout);

            body.Add(EvoEditorUITK.CreateToggle("Open Scenes", ConvertOptions.openScenes,
                "Convert text in every loaded scene and the open prefab stage.", value =>
                {
                    ConvertOptions.openScenes = value;
                    UpdateState();
                }));

            body.Add(EvoEditorUITK.CreateToggle("Prefabs", ConvertOptions.prefabs, "Convert prefab assets inside the folder.", value =>
            {
                ConvertOptions.prefabs = value;
                UpdateState();
            }));

            prefabOptions = new VisualElement();
            prefabOptions.AddToClassList("evo-settings__nested");
            body.Add(prefabOptions);

            var folder = EvoEditorUITK.CreateObjectField("Folder", typeof(DefaultAsset), LoadPrefabFolder(), "Folder searched for prefab assets.");
            folder.RegisterValueChangedCallback(evt =>
            {
                // Only a folder can be searched, so anything else puts the previous one back
                string path = AssetDatabase.GetAssetPath(evt.newValue);
                if (AssetDatabase.IsValidFolder(path)) { ConvertOptions.prefabFolder = path; }
                else
                    folder.SetValueWithoutNotify(LoadPrefabFolder());
            });
            prefabOptions.Add(folder);

            prefabOptions.Add(EvoEditorUITK.CreateToggle("Include Evo UI Prefabs", ConvertOptions.includePackagePrefabs,
                "Also convert Evo UI's own prefabs. They are templates that package updates replace, and Create menu items already convert new objects.",
                value => ConvertOptions.includePackagePrefabs = value));

            body.Add(EvoEditorUITK.CreateToggle("Include All Text", ConvertOptions.allTexts,
                "Convert every text and input field, not only those referenced by Evo UI fields.",
                value => ConvertOptions.allTexts = value));

            prefabWarning = EvoEditorUITK.CreateCallout("Prefabs are saved as they're converted and can't be undone. Commit or back up your project first.", CalloutType.Warning);
            body.Add(prefabWarning);

            var buttons = new VisualElement();
            buttons.AddToClassList("evo-buttons");
            buttons.AddToClassList("evo-settings__actions");
            body.Add(buttons);

            convertButton = EvoEditorUITK.CreateButton(null, (Texture2D)null, null, Convert);
            convertButton.AddToClassList("evo-primary");
            buttons.Add(convertButton);

            status = new EvoEditorUITK.StatusView(() =>
            {
                result = null;
                UpdateState();
            });
            status.AddToClassList("evo-settings__status");
            body.Add(status);

            RefreshProviders();
        }

        // The sheet's name is distinctive enough to find it wherever the package was imported
        static StyleSheet FindStyleSheet()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{nameof(EvoUISettingsProvider)} t:StyleSheet"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == nameof(EvoUISettingsProvider))
                    return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }

            Debug.LogWarning($"[Evo UI] Couldn't find {nameof(EvoUISettingsProvider)}.uss. Reimport Evo UI to restore it.");
            return null;
        }

        static DefaultAsset LoadPrefabFolder() => AssetDatabase.LoadAssetAtPath<DefaultAsset>(ConvertOptions.prefabFolder);
        #endregion

        #region Providers
        void RefreshProviders()
        {
            providerList.Clear();

            string selectedId = string.IsNullOrEmpty(settings.textProvider) ? TMPTextBackend.ID : settings.textProvider;
            bool missing = TextProvider.Find(selectedId) == null;
            TextBackend active = TextProvider.Active;

            foreach (TextBackend backend in TextProvider.Backends)
            {
                string detail = backend == TMPTextBackend.Instance ? "Built in" : GetInstalledDetail(backend);
                VisualElement row = AddProviderRow(backend.DisplayName, detail, backend.Id == selectedId, true);

                // A selection that isn't installed falls back to this one, so it's marked as the one in use
                if (missing && backend == active)
                {
                    var chip = new Label("In use") { tooltip = "Used until the selected provider is installed." };
                    chip.AddToClassList("evo-chip");
                    chip.AddToClassList("evo-settings__chip");
                    row.Add(chip);
                }

                if (backend.Id != selectedId)
                {
                    row.tooltip = $"Create Evo UI text with {backend.DisplayName}.";
                    row.RegisterCallback<ClickEvent>(_ => SetProvider(backend));
                }
            }

            bool listed = !missing;
            foreach (var (id, name) in KnownProviders)
            {
                if (TextProvider.Find(id) != null)
                    continue;

                AddProviderRow(name, id == selectedId ? MissingDetail(active) : "Not installed", id == selectedId, false)
                    .tooltip = $"Install the {name} package to use it.";
                listed |= id == selectedId;
            }

            // A custom provider that isn't registered in this project
            if (!listed)
                AddProviderRow(selectedId, MissingDetail(active), true, false);

            UpdateState();
        }

        VisualElement AddProviderRow(string name, string detail, bool selected, bool installed)
        {
            var row = new VisualElement();
            row.AddToClassList("evo-settings__provider");
            row.EnableInClassList("evo-settings__provider--selected", selected);
            row.EnableInClassList("evo-settings__provider--unavailable", !installed);
            providerList.Add(row);

            var radio = new VisualElement { pickingMode = PickingMode.Ignore };
            radio.AddToClassList("evo-settings__radio");
            row.Add(radio);

            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("evo-settings__radio-dot");
            radio.Add(dot);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("evo-settings__provider-text");
            row.Add(text);

            var nameLabel = new Label(name) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("evo-settings__provider-name");
            text.Add(nameLabel);

            var detailLabel = new Label(detail) { pickingMode = PickingMode.Ignore };
            detailLabel.AddToClassList("evo-settings__provider-detail");
            detailLabel.EnableInClassList("evo-settings__provider-detail--warning", selected && !installed);
            text.Add(detailLabel);

            return row;
        }

        static string GetInstalledDetail(TextBackend backend)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(backend.ComponentType.Assembly);
            return package != null ? $"Installed · {package.version}" : "Installed";
        }

        static string MissingDetail(TextBackend active) => $"Selected, but not installed. {active.DisplayName} is used until it is.";

        void SetProvider(TextBackend backend)
        {
            // Picking the fallback for a provider that isn't installed changes nothing for new text
            bool changed = backend != TextProvider.Active;

            // The default needs no file, so projects that keep TextMesh Pro stay untouched
            settings.textProvider = backend.Id;
            if (File.Exists(EvoUISettings.FilePath) || backend != TMPTextBackend.Instance) { settings.Save(); }
            else
                settings.Apply();

            // Existing text keeps its provider until it's converted, which the page now offers below
            if (changed)
            {
                switchedTo = backend.Id;
                result = null;
            }

            RefreshProviders();
        }
        #endregion

        #region Conversion
        void UpdateState()
        {
            TextBackend target = TextProvider.Active;
            convertIntro.text = $"Swaps text components and input fields for {target.DisplayName} ones on the same objects and repoints every reference to them. " +
                "Fonts are paired through Styler font items.";

            bool switched = switchedTo == target.Id;
            switchCallout.style.display = switched ? DisplayStyle.Flex : DisplayStyle.None;
            if (switched)
                EvoEditorUITK.SetCallout(switchCallout, $"Switched to {target.DisplayName}. New Evo UI text uses it from now on. Convert existing text below, now or later.");

            prefabOptions.SetEnabled(ConvertOptions.prefabs);
            prefabWarning.style.display = ConvertOptions.prefabs ? DisplayStyle.Flex : DisplayStyle.None;

            convertButton.text = $"Convert to {target.DisplayName}";
            convertButton.tooltip = ConvertOptions.openScenes || ConvertOptions.prefabs
                ? $"Convert text in the selected places to {target.DisplayName}. Skipped items are listed in the Console."
                : "Turn on Open Scenes or Prefabs to choose what to convert.";
            convertButton.SetEnabled(ConvertOptions.openScenes || ConvertOptions.prefabs);

            status.SetState(1, result, false, resultFailed);
        }

        void Convert()
        {
            TextBackend target = TextProvider.Active;
            if (ConvertOptions.prefabs && !EditorUtility.DisplayDialog("Evo UI", $"Convert text to {target.DisplayName}?\n\n" +
                "Prefabs are saved as they're converted and can't be undone. Make sure your project is backed up or under version control.",
                "Convert", "Cancel"))
                return;

            try
            {
                TextConverter.Report report = TextConverter.Convert(target, ConvertOptions);
                TextConverter.Log(report, target);
                result = Describe(report, target);
                resultFailed = false;
                switchedTo = null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = "The conversion stopped with an error. Details are in the Console.";
                resultFailed = true;
            }

            UpdateState();
        }

        static string Describe(TextConverter.Report report, TextBackend target)
        {
            int skipped = report.skipped.Count;
            string attention = skipped switch
            {
                0 => "",
                1 => " 1 item needs attention. Details are in the Console.",
                _ => $" {skipped:N0} items need attention. Details are in the Console."
            };

            if (report.converted == 0 && report.inputs == 0 && report.fonts == 0)
                return $"Found nothing to convert to {target.DisplayName}." + attention;

            return $"Converted {EvoEditorUITK.Plural(report.converted, "text component")}, {EvoEditorUITK.Plural(report.inputs, "input field")} " +
                $"and {EvoEditorUITK.Plural(report.fonts, "font reference")} to {target.DisplayName}." + attention;
        }
        #endregion
    }
}
