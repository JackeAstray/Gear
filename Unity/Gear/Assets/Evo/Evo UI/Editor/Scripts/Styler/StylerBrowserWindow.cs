using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;
    using Toggle = UnityEngine.UIElements.Toggle;

    internal class StylerBrowserWindow : EditorWindow
    {
        enum ApplyScope { Selection, Scenes, Project }

        const long StatusDuration = 4000;
        const string SessionStartedKey = "Evo.UI.StylerBrowser.SessionStarted";

        [SerializeField] StylerPreset selectedPreset;
        [SerializeField] ApplyScope applyScope;
        [SerializeField] bool includeChildren = true;
        [SerializeField] bool showApplyOptions;

        readonly List<StylerPreset> presets = new();
        readonly List<IStylerHandler> selectedHandlers = new();
        readonly List<IStylerHandler> childHandlers = new();
        int selectedCount;
        int childCount;

        // Header
        ToolbarMenu presetMenu;
        TextField renameField;
        StylerPreset renamingPreset;
        ToolbarMenu moreMenu;
        VisualElement metaRow;
        Label defaultChip;
        Label pathLabel;
        VisualElement palette;
        EvoEditorUITK.AssetTracker paletteTracker;

        // Preset items
        ScrollView itemsScroll;
        VisualElement emptyState;
        InspectorElement inspector;
        StylerPreset inspectedPreset;

        // Apply
        VisualElement footer;
        VisualElement applyOptions;
        VisualElement applyRow;
        Button optionsButton;
        EvoEditorUITK.SegmentedControl<ApplyScope> scopes;
        Toggle childrenToggle;
        Label applyDescription;
        EvoEditorUITK.StatusView status;
        Button applyButton;

        IVisualElementScheduledItem selectionRefresh;
        bool selectionStale;
        IVisualElementScheduledItem statusTimeout;
        IEnumerator<int> presetApplication;
        float applicationProgress;
        string applicationStatus;
        bool applicationFailed;
        int appliedCount;

        void OnEnable()
        {
            titleContent = new GUIContent("Styler Browser", Resources.Load<Texture2D>("GUI/UI_StylerBrowser"));
            minSize = new Vector2(280, 340);
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += ScheduleSelectionRefresh;
            EditorApplication.projectChanged += RefreshPresetList;
            Undo.undoRedoPerformed += OnUndoRedo;
            StylerProjectUtility.BusyChanged += UpdateState;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;

            // Each editor launch starts on the default preset; domain reloads keep the current one
            if (!SessionState.GetBool(SessionStartedKey, false))
            {
                SessionState.SetBool(SessionStartedKey, true);
                selectedPreset = null;
            }
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= ScheduleSelectionRefresh;
            EditorApplication.projectChanged -= RefreshPresetList;
            EditorApplication.delayCall -= RefreshPresetList;
            Undo.undoRedoPerformed -= OnUndoRedo;
            StylerProjectUtility.BusyChanged -= UpdateState;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            selectionRefresh?.Pause();
            statusTimeout?.Pause();

            if (presetApplication != null)
            {
                StopPresetApplication();
                Debug.LogWarning("Preset application was interrupted. Some assets may already be saved; apply the same preset again to finish.");
            }

            paletteTracker?.Stop();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            EvoEditorUITK.Apply(this, nameof(StylerBrowserWindow));
            root.Add(CreateHeader());
            root.Add(CreateItems());
            root.Add(CreateFooter());

            inspectedPreset = null;
            RefreshPresetList();
            RefreshSelection();
        }

        bool CanApplyToSelection => selectedPreset != null && !StylerProjectUtility.IsBusy && !EditorApplication.isPlayingOrWillChangePlaymode;

        bool CanApplyPreset => CanApplyToSelection && PrefabStageUtility.GetCurrentPrefabStage() == null;

        #region Layout
        VisualElement CreateHeader()
        {
            VisualElement header = EvoEditorUITK.CreateMenuHeader("Switch preset", "Preset actions", out presetMenu, out moreMenu, out metaRow);
            PopulateMoreMenu(moreMenu.menu);

            // Renaming swaps the title menu for this field in place
            renameField = new TextField { isDelayed = true, style = { display = DisplayStyle.None } };
            renameField.AddToClassList("styler-rename-field");
            renameField.RegisterValueChangedCallback(evt =>
            {
                RenamePreset(renamingPreset, evt.newValue);
                EndRename();
            });
            renameField.RegisterCallback<FocusOutEvent>(_ => EndRename());
            renameField.RegisterCallback<KeyDownEvent>(OnRenameKeyDown, TrickleDown.TrickleDown);
            presetMenu.parent.Insert(presetMenu.parent.IndexOf(presetMenu) + 1, renameField);

            defaultChip = new Label("Default") { tooltip = "The project's default preset. Styler components set to Default use it." };
            defaultChip.AddToClassList("evo-chip");
            defaultChip.AddToClassList("evo-chip--accent");
            metaRow.Add(defaultChip);

            pathLabel = EvoEditorUITK.CreatePathLabel(PingPreset);
            metaRow.Add(pathLabel);

            palette = new VisualElement();
            palette.AddToClassList("styler-palette");
            header.Add(palette);

            paletteTracker?.Stop();
            paletteTracker = new EvoEditorUITK.AssetTracker();
            header.Add(paletteTracker);

            return header;
        }

        VisualElement CreateItems()
        {
            var content = new VisualElement();
            content.AddToClassList("styler-content");

            itemsScroll = new ScrollView(ScrollViewMode.Vertical);
            itemsScroll.AddToClassList("styler-items");
            itemsScroll.contentContainer.AddToClassList("styler-items__content");
            itemsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content.Add(itemsScroll);

            emptyState = new EvoEditorUITK.EmptyState().Set("No Styler presets yet",
                "A preset holds the colors, fonts, and styles that Styler components share.", "Create Preset", CreatePreset);
            content.Add(emptyState);

            // Last, so it draws over the scrolled content
            content.Add(new EvoEditorUITK.ScrollShadow(itemsScroll));

            return content;
        }

        VisualElement CreateFooter()
        {
            footer = new VisualElement();
            footer.AddToClassList("evo-footer");

            // The options are rarely changed, so they stay folded above the Apply button until asked for
            applyOptions = new VisualElement();
            footer.Add(applyOptions);

            // The children option shares the heading row so it costs no height
            var headingRow = new VisualElement();
            headingRow.AddToClassList("styler-footer__heading-row");
            applyOptions.Add(headingRow);

            headingRow.Add(EvoEditorUITK.CreateHeading("Apply To"));

            childrenToggle = EvoEditorUITK.CreateToggle("Include Children", includeChildren,
                "Also apply to Styler components on the children of selected objects", value =>
                {
                    includeChildren = value;
                    ClearStatus();
                    UpdateFooter();
                }, onText: null, offText: null);
            childrenToggle.AddToClassList("evo-flat");
            childrenToggle.AddToClassList("styler-children-toggle");
            headingRow.Add(childrenToggle);

            scopes = new EvoEditorUITK.SegmentedControl<ApplyScope>(SetScope)
                .AddOption(ApplyScope.Selection, "Selection", "Selected objects, or prefabs selected in the Project window")
                .AddOption(ApplyScope.Scenes, "Scenes", "Every Styler component in the loaded scenes")
                .AddOption(ApplyScope.Project, "Project", "Every Styler component in prefabs and scenes under Assets");
            applyOptions.Add(scopes);

            applyDescription = EvoEditorUITK.CreateDescription();
            applyOptions.Add(applyDescription);

            status = new EvoEditorUITK.StatusView(ClearStatus);
            footer.Add(status);

            applyRow = new VisualElement();
            applyRow.AddToClassList("styler-apply-row");
            footer.Add(applyRow);

            optionsButton = EvoEditorUITK.CreateButton(null, EditorTools.IconLibrary.Expand, null, ToggleApplyOptions);
            optionsButton.AddToClassList("styler-options-button");
            applyRow.Add(optionsButton);

            applyButton = EvoEditorUITK.CreateButton(null, null, Apply);
            applyButton.AddToClassList("evo-primary");
            applyButton.AddToClassList("styler-apply");
            applyRow.Add(applyButton);

            return footer;
        }

        void ToggleApplyOptions()
        {
            showApplyOptions = !showApplyOptions;
            UpdateFooter();
        }

        void PopulateMoreMenu(DropdownMenu menu)
        {
            menu.AppendAction("Rename", _ => BeginRename(), PresetActionStatus);
            menu.AppendAction("Duplicate", _ => DuplicatePreset(selectedPreset), PresetActionStatus);
            menu.AppendAction("Delete...", _ => DeletePreset(selectedPreset), PresetActionStatus);
            menu.AppendSeparator();
            menu.AppendAction("Show in Project", _ => PingPreset(), PresetActionStatus);
            menu.AppendAction("Open in Inspector", _ => Selection.activeObject = selectedPreset, PresetActionStatus);
            menu.AppendSeparator();
            menu.AppendAction("ID Migrator...", _ => StylerIDMigratorWindow.Open(selectedPreset), PresetActionStatus);
        }

        DropdownMenuAction.Status PresetActionStatus(DropdownMenuAction action)
        {
            return selectedPreset != null && !StylerProjectUtility.IsBusy ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        #endregion

        #region Presets
        void RefreshPresetList()
        {
            presets.Clear();
            presets.AddRange(EvoEditorUITK.FindAssets<StylerPreset>());

            if (selectedPreset == null || !presets.Contains(selectedPreset))
            {
                var defaultPreset = Styler.GetDefaultPreset(false);
                selectedPreset = presets.Contains(defaultPreset) ? defaultPreset : presets.FirstOrDefault();
            }

            UpdatePresetMenu();
            ShowPreset();
        }

        void UpdatePresetMenu()
        {
            if (presetMenu == null)
                return;

            var menu = presetMenu.menu;
            EvoEditorUITK.RebuildAssetMenu(menu, presets, () => selectedPreset, SelectPreset);
            menu.AppendAction("Create New Preset", _ => CreatePreset(),
                _ => StylerProjectUtility.IsBusy ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        void SelectPreset(StylerPreset preset)
        {
            if (selectedPreset == preset || StylerProjectUtility.IsBusy)
                return;

            selectedPreset = preset;
            ClearStatus();
            ShowPreset();
        }

        void ShowPreset()
        {
            if (itemsScroll == null)
                return;

            // Compare references: a deleted preset equals null and would otherwise keep its inspector
            if (!ReferenceEquals(inspectedPreset, selectedPreset) || (selectedPreset != null && inspector == null))
            {
                EndRename();
                itemsScroll.Clear();
                inspector = null;
                inspectedPreset = selectedPreset;
                itemsScroll.scrollOffset = Vector2.zero;
                paletteTracker.Track(selectedPreset, UpdatePalette);

                if (selectedPreset != null)
                {
                    inspector = new InspectorElement(selectedPreset);
                    itemsScroll.Add(inspector);
                }
            }

            itemsScroll.style.display = selectedPreset != null ? DisplayStyle.Flex : DisplayStyle.None;
            emptyState.style.display = selectedPreset != null ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateHeader();
            UpdateState();
        }

        void UpdateHeader()
        {
            if (presetMenu == null)
                return;

            bool hasPreset = selectedPreset != null;
            presetMenu.text = hasPreset ? selectedPreset.name : "No Preset";
            metaRow.style.display = hasPreset ? DisplayStyle.Flex : DisplayStyle.None;
            moreMenu.style.display = hasPreset ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasPreset)
            {
                defaultChip.style.display = Styler.GetDefaultPreset(false) == selectedPreset ? DisplayStyle.Flex : DisplayStyle.None;
                EvoEditorUITK.SetPath(pathLabel, selectedPreset);
            }

            UpdatePalette();
        }

        void UpdatePalette()
        {
            if (palette == null)
                return;

            palette.Clear();
            var colors = selectedPreset != null ? selectedPreset.colorItems : null;
            palette.style.display = colors != null && colors.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (colors == null)
                return;

            foreach (var item in colors)
            {
                if (item == null)
                    continue;

                string hex = ColorUtility.ToHtmlStringRGB(item.colorValue);
                string alpha = item.colorValue.a < 1 ? $" ({Mathf.RoundToInt(item.colorValue.a * 100)}%)" : "";
                var swatch = new VisualElement { tooltip = $"{item.itemID}\n#{hex}{alpha}" };
                swatch.AddToClassList("styler-palette__swatch");
                swatch.style.backgroundColor = item.colorValue;
                palette.Add(swatch);
            }
        }

        void PingPreset()
        {
            if (selectedPreset != null)
                EditorGUIUtility.PingObject(selectedPreset);
        }

        void BeginRename()
        {
            if (selectedPreset == null || StylerProjectUtility.IsBusy)
                return;

            renamingPreset = selectedPreset;
            renameField.SetValueWithoutNotify(selectedPreset.name);
            presetMenu.style.display = DisplayStyle.None;
            renameField.style.display = DisplayStyle.Flex;

            // Wait for the field to be laid out, or it won't take focus
            renameField.schedule.Execute(() =>
            {
                renameField.Focus();
                renameField.SelectAll();
            });
        }

        void EndRename()
        {
            if (renameField == null || renameField.style.display == DisplayStyle.None)
                return;

            renameField.style.display = DisplayStyle.None;
            presetMenu.style.display = DisplayStyle.Flex;
            renameField.Blur();
        }

        void OnRenameKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && renamingPreset != null)
                renameField.SetValueWithoutNotify(renamingPreset.name);
            else if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;

            // Losing focus commits the delayed field, so Enter also closes an unchanged name
            EndRename();
            evt.StopPropagation();
        }

        void RenamePreset(StylerPreset asset, string newName)
        {
            newName = newName?.Trim();
            if (StylerProjectUtility.IsBusy || asset == null || string.IsNullOrEmpty(newName) || asset.name == newName)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string result = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(result))
                Debug.LogError($"[Styler] Could not rename preset: {result}", asset);

            // Rebuilding from a field's change callback can invalidate the current focus event
            EditorApplication.delayCall -= RefreshPresetList;
            EditorApplication.delayCall += RefreshPresetList;
        }

        void CreatePreset()
        {
            if (StylerProjectUtility.IsBusy)
                return;

            string folder = selectedPreset != null ? EvoEditorUITK.GetAssetFolder(selectedPreset) : "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/New Styler Preset.asset");
            var preset = CreateInstance<StylerPreset>();
            AssetDatabase.CreateAsset(preset, path);

            selectedPreset = preset;
            RefreshPresetList();
            BeginRename();
        }

        void DuplicatePreset(StylerPreset asset)
        {
            if (asset == null || StylerProjectUtility.IsBusy)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.SaveAssetIfDirty(asset);
            if (!AssetDatabase.CopyAsset(path, newPath))
            {
                Debug.LogError($"[Styler] Could not duplicate preset at {path}.", asset);
                return;
            }

            var newAsset = AssetDatabase.LoadAssetAtPath<StylerPreset>(newPath);
            if (newAsset)
                selectedPreset = newAsset;

            RefreshPresetList();
            if (newAsset)
                BeginRename();
        }

        void DeletePreset(StylerPreset asset)
        {
            if (asset == null || StylerProjectUtility.IsBusy)
                return;

            if (EditorUtility.DisplayDialog("Delete Styler Preset", $"Are you sure you want to delete {asset.name}?", "Delete", "Cancel"))
            {
                if (!AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset)))
                {
                    Debug.LogError($"[Styler] Could not delete preset '{asset.name}'.", asset);
                    return;
                }

                selectedPreset = null;
                RefreshPresetList();
            }
        }
        #endregion

        #region State
        // Selection.selectionChanged also fires while this tab is hidden behind another one in its dock;
        // the OnSelectionChange message doesn't
        void OnSelectionChanged()
        {
            // A finished result describes the previous selection
            if (applyScope == ApplyScope.Selection)
                ClearStatus();

            ScheduleSelectionRefresh();
        }

        void ScheduleSelectionRefresh()
        {
            selectionStale = true;

            // Hidden tabs catch up in OnBecameVisible. Play Mode can't apply presets and may change
            // the hierarchy every frame, so it catches up on returning to Edit Mode.
            if (rootVisualElement.panel == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Coalesce notifications without recreating the embedded inspector or losing focus
            selectionRefresh?.Pause();
            selectionRefresh = rootVisualElement.schedule.Execute(RefreshSelection);
        }

        void OnBecameVisible()
        {
            if (selectionStale && !EditorApplication.isPlayingOrWillChangePlaymode)
                RefreshSelection();
            else
                UpdateFooter(); // Loaded scenes or Prefab Mode may have changed meanwhile
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && selectionStale)
                ScheduleSelectionRefresh();

            UpdateState();
        }

        void OnPrefabStageChanged(PrefabStage stage) => ScheduleSelectionRefresh();

        void OnUndoRedo()
        {
            UpdateHeader();
            inspector?.Q<IMGUIContainer>()?.MarkDirtyRepaint();
        }

        void RefreshSelection()
        {
            selectionStale = false;
            selectionRefresh?.Pause();

            var objects = Selection.gameObjects.Where(obj => obj != null).ToList();
            selectedHandlers.Clear();
            selectedHandlers.AddRange(objects.SelectMany(obj => obj.GetComponents<MonoBehaviour>()).OfType<IStylerHandler>());
            childHandlers.Clear();
            childHandlers.AddRange(objects.SelectMany(obj => obj.GetComponentsInChildren<MonoBehaviour>(true)).OfType<IStylerHandler>().Distinct());
            selectedCount = CountComponents(selectedHandlers);
            childCount = CountComponents(childHandlers);
            UpdateFooter();
        }

        static int CountComponents(IEnumerable<IStylerHandler> handlers)
        {
            return handlers.Select(StylerProjectUtility.ResolveHandler).OfType<MonoBehaviour>().Where(component => component != null).Distinct().Count();
        }

        void UpdateState()
        {
            bool busy = StylerProjectUtility.IsBusy;
            presetMenu?.SetEnabled(!busy);
            moreMenu?.SetEnabled(!busy);
            inspector?.SetEnabled(!busy);
            UpdateFooter();
        }

        void SetScope(ApplyScope scope)
        {
            if (applyScope == scope || presetApplication != null)
                return;

            applyScope = scope;
            ClearStatus();
            UpdateFooter();
        }

        void UpdateFooter()
        {
            if (footer == null)
                return;

            bool running = presetApplication != null;
            footer.style.display = selectedPreset != null ? DisplayStyle.Flex : DisplayStyle.None;
            footer.EnableInClassList("styler-footer--folded", !showApplyOptions);

            applyOptions.style.display = showApplyOptions ? DisplayStyle.Flex : DisplayStyle.None;
            optionsButton.EnableInClassList("styler-options-button--open", showApplyOptions);
            optionsButton.tooltip = showApplyOptions ? "Hide apply options" : "Show apply options";

            scopes.SetValueWithoutNotify(applyScope);
            scopes.SetEnabled(!running);

            childrenToggle.style.display = applyScope == ApplyScope.Selection ? DisplayStyle.Flex : DisplayStyle.None;
            childrenToggle.SetValueWithoutNotify(includeChildren);
            childrenToggle.SetEnabled(!running);

            string description = GetApplyDescription(out bool canApply, out bool blocked);
            applyDescription.text = description;
            applyDescription.EnableInClassList("evo-description--warning", blocked);

            // Folded options leave the description to the button, so hovering still tells what it updates or why it can't
            applyButton.tooltip = showApplyOptions ? null : description;
            applyButton.text = GetApplyLabel();
            applyButton.SetEnabled(canApply && !running);
            UpdateStatus();
        }

        string GetApplyLabel()
        {
            switch (applyScope)
            {
                case ApplyScope.Scenes: return "Apply to Loaded Scenes...";
                case ApplyScope.Project: return "Apply to Entire Project...";
            }

            var objects = Selection.gameObjects;
            return objects.Length == 1 ? $"Apply to '{objects[0].name}'"
                : objects.Length > 1 ? $"Apply to {objects.Length} Objects" : "Apply to Selection";
        }

        string GetApplyDescription(out bool canApply, out bool blocked)
        {
            canApply = false;
            blocked = true;

            if (selectedPreset == null)
                return "";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Exit Play Mode to apply presets.";
            if (StylerProjectUtility.IsBusy)
                return "Wait for the current Styler operation to finish.";
            if (applyScope != ApplyScope.Selection && PrefabStageUtility.GetCurrentPrefabStage() != null)
                return "Exit Prefab Mode to apply to scenes or the project.";

            blocked = false;
            switch (applyScope)
            {
                case ApplyScope.Scenes:
                    int scenes = StylerProjectUtility.GetLoadedScenes().Count;
                    canApply = scenes > 0;
                    return $"Updates every Styler component in {EvoEditorUITK.Plural(scenes, "loaded scene")}. Scenes are left unsaved.";

                case ApplyScope.Project:
                    canApply = true;
                    return "Updates every prefab and scene under Assets and saves them. This can't be fully undone.";
            }

            var objects = Selection.gameObjects;
            int count = includeChildren ? childCount : selectedCount;
            canApply = count > 0;

            if (objects.Length == 0)
                return "Select objects in the Hierarchy, or prefabs in the Project window.";

            bool single = objects.Length == 1;
            string target = single ? $"'{objects[0].name}'" : $"{objects.Length} objects";
            string children = single ? "its children" : "their children";

            if (count > 0)
                return $"{EvoEditorUITK.Plural(count, "Styler component")} on {target}" + (includeChildren ? $" and {children}." : ".");
            if (!includeChildren && childCount > 0)
                return $"No Styler components on {target}. Include children to update {EvoEditorUITK.Plural(childCount, "component")}.";

            return $"No Styler components on {target}" + (includeChildren ? $" or {children}." : ".");
        }

        #endregion

        #region Application
        void Apply()
        {
            switch (applyScope)
            {
                case ApplyScope.Scenes: ApplyPresetToScene(); break;
                case ApplyScope.Project: ApplyPresetToProject(); break;
                default: ApplyPresetToSelection(); break;
            }
        }

        void ApplyPresetToSelection()
        {
            if (!CanApplyToSelection)
                return;

            try
            {
                int count = StylerPresetApplication.ApplySelected(includeChildren ? childHandlers : selectedHandlers, selectedPreset);
                Debug.Log($"[Styler] Applied '{selectedPreset.name}' to {count} component(s).");

                applicationFailed = false;
                ShowApplicationProgress(1, count > 0
                    ? $"Applied '{selectedPreset.name}' to {EvoEditorUITK.Plural(count, "component")}."
                    : "Already up to date. These components use this preset.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                applicationFailed = true;
                ShowApplicationProgress(1, "Couldn't apply the preset. See the Console for details.");
            }

            // A confirmation only needs a moment; the description returns afterwards
            statusTimeout?.Pause();
            statusTimeout = status.schedule.Execute(ClearStatus).StartingIn(StatusDuration);
        }

        void ApplyPresetToScene()
        {
            if (!CanApplyPreset || !EditorUtility.DisplayDialog("Apply Preset to Scene",
                $"Apply '{selectedPreset.name}' to all Styler components in loaded scenes?"
                + $"\n\nPrefab instances receive scene overrides. Scenes are left unsaved.", "Apply", "Cancel"))
                return;

            StartPresetApplication(false);
        }

        void ApplyPresetToProject()
        {
            if (!CanApplyPreset || !EditorUtility.DisplayDialog("Apply Preset to Project",
                $"Apply '{selectedPreset.name}' to all Styler components in prefab and scene assets under Assets?"
                + "\n\nThis replaces existing preset assignments, including instance overrides. "
                + "Prefab and closed scene assets are saved; open scenes remain unsaved. "
                + "\n\nThis writes source files and cannot be fully undone. Continue?", "Apply to Project", "Cancel"))
                return;

            StartPresetApplication(true);
        }

        void StartPresetApplication(bool projectWide)
        {
            var preset = selectedPreset;
            if (!CanApplyPreset || !StylerProjectUtility.BeginOperation(this))
                return;

            appliedCount = 0;
            applicationFailed = false;
            statusTimeout?.Pause();
            presetApplication = StylerPresetApplication.Run(preset, projectWide, ShowApplicationProgress);
            ShowApplicationProgress(0, $"Applying '{preset.name}'...");
            EditorApplication.update += AdvancePresetApplication;
            UpdateFooter();
        }

        void AdvancePresetApplication()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { return; }
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                    throw new InvalidOperationException("Preset application stopped because Play Mode or Prefab Mode was entered.");

                if (presetApplication.MoveNext())
                {
                    appliedCount = presetApplication.Current;
                    return;
                }

                StopPresetApplication();
                ShowApplicationProgress(1, $"Done. Updated {EvoEditorUITK.Plural(appliedCount, "component")}. Save modified open scenes when ready.");
                Debug.Log($"[Styler] Preset application updated {appliedCount} component(s).");
            }
            catch (Exception exception)
            {
                StopPresetApplication();
                applicationFailed = true;
                ShowApplicationProgress(applicationProgress, "Application stopped. Some changes may already be saved. " +
                    "Apply the same preset again after fixing the error.");
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Preset Application Stopped", exception.Message + "\n\n" + applicationStatus, "OK");
            }
        }

        void StopPresetApplication()
        {
            EditorApplication.update -= AdvancePresetApplication;
            var application = presetApplication;
            presetApplication = null;

            try { application?.Dispose(); }
            finally { StylerProjectUtility.EndOperation(this); }
        }

        void ShowApplicationProgress(float progress, string description)
        {
            applicationProgress = progress;
            applicationStatus = description;
            UpdateStatus();
            Repaint();
        }

        void ClearStatus()
        {
            if (presetApplication != null)
                return;

            statusTimeout?.Pause();
            applicationStatus = null;
            applicationFailed = false;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            if (status == null)
                return;

            bool running = presetApplication != null;
            bool visible = running || !string.IsNullOrEmpty(applicationStatus);
            applyDescription.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            applyRow.EnableInClassList("styler-apply-row--alone", !showApplyOptions && !visible);
            status.SetState(applicationProgress, applicationStatus, running, applicationFailed);

            if (running)
                applyButton.text = $"Applying... {Mathf.RoundToInt(applicationProgress * 100)}%";
        }
        #endregion
    }
}