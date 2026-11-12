using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;

    [NoAutoStaticsCleanup]
    internal class StylerIDMigratorWindow : EditorWindow
    {
        [SerializeField] StylerPreset preset;
        [SerializeField] Styler.ItemType itemType = Styler.ItemType.Color;
        [SerializeField] string sourceID;
        [SerializeField] string targetID = "";
        [SerializeField] StylerIDMigration.ApplyTo applyTo;

        static readonly (StylerIDMigration.ApplyTo scope, string text, string tooltip)[] Scopes =
        {
            (StylerIDMigration.ApplyTo.SelectedObjects, "Selection", "Selected objects or prefabs, and their children"),
            (StylerIDMigration.ApplyTo.SceneObjects, "Scenes", "Every loaded scene"),
            (StylerIDMigration.ApplyTo.Prefabs, "Prefabs", "Prefab assets under Assets"),
            (StylerIDMigration.ApplyTo.EntireProject, "Project", "Every prefab and scene under Assets")
        };

        DropdownField sourceField;
        Label validation;
        Label scopeDescription;
        Button migrateButton;
        Button closeButton;
        VisualElement fields;
        EvoEditorUITK.StatusView status;
        float migrationProgress;
        bool migrationFailed;
        IEnumerator<int> migration;
        int migratedCount;
        bool referencesOnlyMigration;
        static StylerIDMigratorWindow activeWindow;

        internal static void Open(StylerPreset preset, Styler.ItemType? itemType = null, string sourceID = null, string targetID = null)
        {
            if (activeWindow != null)
            {
                if (activeWindow.migration == null)
                {
                    activeWindow.SetFields(preset, itemType, sourceID, targetID);
                    activeWindow.CreateGUI();
                }
                activeWindow.Focus();
                return;
            }

            var window = CreateInstance<StylerIDMigratorWindow>();
            window.SetFields(preset, itemType, sourceID, targetID);
            window.titleContent = new GUIContent("Styler ID Migrator");
            window.minSize = new Vector2(300, 300);
            window.maxSize = new Vector2(600, 500);
            window.ShowUtility();
        }

        void SetFields(StylerPreset preset, Styler.ItemType? itemType, string sourceID, string targetID)
        {
            this.preset = preset;
            if (itemType == null)
                return;

            this.itemType = itemType.Value;
            this.sourceID = sourceID;
            this.targetID = targetID ?? "";
        }

        void OnEnable()
        {
            activeWindow = this;
            Selection.selectionChanged += RefreshValidation;
            StylerProjectUtility.BusyChanged += RefreshValidation;
            Undo.undoRedoPerformed += RefreshSources;
            EditorApplication.projectChanged += RefreshSources;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= RefreshValidation;
            StylerProjectUtility.BusyChanged -= RefreshValidation;
            Undo.undoRedoPerformed -= RefreshSources;
            EditorApplication.projectChanged -= RefreshSources;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            EditorApplication.delayCall -= RefreshValidation;

            if (activeWindow == this)
                activeWindow = null;

            if (migration != null)
            {
                StopMigration();
                Debug.LogWarning("[Styler] ID migration was interrupted. Some asset changes may already have been saved. " +
                    "For Scene Objects, Entire Project, or Prefabs, rerun the same source and target IDs to finish the migration.");
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            EvoEditorUITK.Apply(this, nameof(StylerBrowserWindow));

            VisualElement header = EvoEditorUITK.CreateHeader("Migrate ID", preset != null
                ? $"Rename an ID in '{preset.name}' and update the components that use it."
                : "The preset no longer exists.");
            header.Q<Label>(className: "evo-subtitle").EnableInClassList("evo-description--warning", preset == null);
            root.Add(header);

            ScrollView body = EvoEditorUITK.CreateBody();
            root.Add(body);

            fields = new VisualElement();
            body.Add(fields);

            var typeField = new EnumField("Type", itemType);
            typeField.AddToClassList("evo-field");
            typeField.RegisterValueChangedCallback(evt => { itemType = (Styler.ItemType)evt.newValue; RefreshSources(); });
            fields.Add(typeField);

            sourceField = new DropdownField("Source ID");
            sourceField.AddToClassList("evo-field");
            sourceField.RegisterValueChangedCallback(evt => { sourceID = evt.newValue; RefreshValidation(); });
            fields.Add(sourceField);

            var targetField = new TextField("Target ID") { value = targetID };
            targetField.AddToClassList("evo-field");
            targetField.RegisterValueChangedCallback(evt => { targetID = evt.newValue; RefreshValidation(); });
            fields.Add(targetField);

            fields.Add(EvoEditorUITK.CreateHeading("Apply To"));

            var scopes = new EvoEditorUITK.SegmentedControl<StylerIDMigration.ApplyTo>(scope => { applyTo = scope; RefreshValidation(); });
            foreach (var (scope, text, tooltip) in Scopes)
                scopes.AddOption(scope, text, tooltip);
            scopes.SetValueWithoutNotify(applyTo);
            fields.Add(scopes);

            scopeDescription = EvoEditorUITK.CreateDescription();
            body.Add(scopeDescription);
            validation = EvoEditorUITK.CreateDescription(null, "evo-description--warning");
            body.Add(validation);

            status = new EvoEditorUITK.StatusView(ClearStatus);
            body.Add(status);

            var buttons = new VisualElement();
            buttons.AddToClassList("evo-footer");
            buttons.AddToClassList("evo-buttons");
            closeButton = EvoEditorUITK.CreateButton("Close", null, Close);
            closeButton.AddToClassList("evo-secondary");
            buttons.Add(closeButton);
            migrateButton = EvoEditorUITK.CreateButton("Migrate ID", null, Migrate);
            migrateButton.AddToClassList("evo-primary");
            buttons.Add(migrateButton);
            root.Add(buttons);
            RefreshSources();
        }

        void OnFocus()
        {
            if (sourceField != null && migration == null)
                RefreshSources();
        }

        void OnPlayModeChanged(PlayModeStateChange state) => RefreshValidation();

        void OnPrefabStageChanged(PrefabStage stage)
        {
            EditorApplication.delayCall -= RefreshValidation;
            EditorApplication.delayCall += RefreshValidation;
        }

        void RefreshSources()
        {
            if (sourceField == null || migration != null)
                return;

            var ids = StylerIDMigration.GetIDs(preset, itemType).Where(id => !string.IsNullOrEmpty(id))
                .Concat(StylerIDMigration.GetFormerIDs(preset, itemType).Keys).Distinct().ToList();
            if (!ids.Contains(sourceID))
                sourceID = ids.FirstOrDefault();

            EvoEditorUITK.SetChoices(sourceField, ids, ids.IndexOf(sourceID));
            sourceField.SetEnabled(ids.Count > 0);
            RefreshValidation();
        }

        void RefreshValidation()
        {
            if (validation == null || migration != null)
                return;

            bool existingTarget = StylerIDMigration.GetIDs(preset, itemType).Contains(targetID);
            string presetDescription;
            if (sourceID != null && StylerIDMigration.GetFormerIDs(preset, itemType).TryGetValue(sourceID, out string owner))
                presetDescription = $"Source ID is a former ID of '{owner}'. Only component references will change; the preset keeps the former ID until you clear it.\n\n";
            else if (existingTarget)
                presetDescription = "Target ID already exists. Only component references will change; the preset and both IDs remain unchanged.\n\n";
            else if (applyTo == StylerIDMigration.ApplyTo.Prefabs)
                presetDescription = "Target ID will be created, while Source ID will be retained for scene users.\n\n";
            else
                presetDescription = "Source ID will be renamed to Target ID and kept as its former ID.\n\n";

            string usersDescription = applyTo switch
            {
                StylerIDMigration.ApplyTo.SelectedObjects => "Selected objects/prefabs and their children will be updated.",
                StylerIDMigration.ApplyTo.SceneObjects => "All loaded scenes will be updated.",
                StylerIDMigration.ApplyTo.Prefabs => "Prefab assets under Assets will be updated.",
                _ => "Every prefab and scene under Assets will be updated."
            };

            if (existingTarget && applyTo == StylerIDMigration.ApplyTo.SceneObjects)
                usersDescription += " Scenes remain unsaved.";

            scopeDescription.text = presetDescription + usersDescription;

            string error = StylerIDMigration.Validate(preset, itemType, sourceID, targetID);
            if (StylerProjectUtility.IsBusy)
                error = "Wait for the current Styler operation to finish.";

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                error = "Exit Play Mode before migrating IDs.";
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                error = "Save and exit Prefab Mode before migrating IDs.";
            else if (applyTo == StylerIDMigration.ApplyTo.SelectedObjects && Selection.gameObjects.Length == 0)
                error = "Select at least one object or prefab.";

            validation.text = error ?? "";
            validation.style.display = error == null ? DisplayStyle.None : DisplayStyle.Flex;
            migrateButton.SetEnabled(error == null);
        }

        void Migrate()
        {
            RefreshValidation();

            if (!migrateButton.enabledSelf)
                return;

            referencesOnlyMigration = StylerIDMigration.GetIDs(preset, itemType).Contains(targetID);
            if (applyTo != StylerIDMigration.ApplyTo.SelectedObjects)
            {
                string warning = "This writes source files and cannot be fully undone. Continue?";
                string confirmButtonText = "Migrate and Save";
                bool unsavedScenesOnly = referencesOnlyMigration && applyTo == StylerIDMigration.ApplyTo.SceneObjects;
                if (unsavedScenesOnly)
                {
                    warning = "This changes loaded scene objects and cannot be undone. Continue?";
                    confirmButtonText = "Migrate";
                }

                string message = $"Migrate {itemType} ID '{sourceID}' to '{targetID}' in '{preset.name}'?" + $"\n\n{scopeDescription.text}\n\n{warning}";
                bool confirmed = EditorUtility.DisplayDialog("Styler ID Migrator", message, confirmButtonText, "Cancel");
                if (!confirmed)
                    return;
            }

            try
            {
                if (!StylerProjectUtility.BeginOperation(this))
                    return;

                migratedCount = 0;
                migrationFailed = false;
                migration = StylerIDMigration.Run(preset, itemType, sourceID, targetID, applyTo, ShowProgress);
                fields.SetEnabled(false);
                migrateButton.SetEnabled(false);
                closeButton.SetEnabled(false);
                ShowProgress(0, "Preparing migration...");
                EditorApplication.update += AdvanceMigration;
            }
            catch (Exception exception) { MigrationFailed(exception); }
        }

        void AdvanceMigration()
        {
            // Let imports/compilation finish before starting another asset operation
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                    throw new InvalidOperationException("Migration stopped because Play Mode or Prefab Mode was entered.");

                if (migration.MoveNext())
                {
                    migratedCount = migration.Current;
                    return;
                }

                StopMigration();

                if (migratedCount < 0) { ShowProgress(0, "Migration cancelled. No IDs changed."); }
                else
                {
                    ShowProgress(1, $"Complete. Updated {migratedCount} ID reference(s)." +
                        (referencesOnlyMigration ? " Preset unchanged." : "") +
                        (applyTo == StylerIDMigration.ApplyTo.EntireProject
                            || referencesOnlyMigration && applyTo == StylerIDMigration.ApplyTo.SceneObjects ? " Save modified open scenes when ready." :
                        applyTo == StylerIDMigration.ApplyTo.Prefabs ? " Prefab assets saved. Source ID retained for scene users." : ""));
                    Debug.Log($"[Styler] Migrated {itemType} ID '{sourceID}' to '{targetID}' in '{preset.name}' and " +
                        $"updated {migratedCount} ID reference(s)." +
                        (referencesOnlyMigration ? " Preset unchanged." : "") +
                        (applyTo == StylerIDMigration.ApplyTo.Prefabs ? " Source ID retained for scene users." : ""), preset);
                }

                RefreshSources();
            }
            catch (Exception exception) { MigrationFailed(exception); }
        }

        void ShowProgress(float progress, string description)
        {
            migrationProgress = progress;
            status.SetState(progress, description, migration != null, migrationFailed);
            Repaint();
        }

        void ClearStatus()
        {
            if (migration == null)
                status.Hide();
        }

        void StopMigration()
        {
            EditorApplication.update -= AdvanceMigration;
            var operation = migration;
            migration = null;

            try { operation?.Dispose(); }
            finally
            {
                StylerProjectUtility.EndOperation(this);
                fields?.SetEnabled(true);
                closeButton?.SetEnabled(true);
            }
        }

        void MigrationFailed(Exception exception)
        {
            StopMigration();
            Debug.LogException(exception);
            migrationFailed = true;
            ShowProgress(migrationProgress, "Migration stopped. Some changes may already have been saved.");
            EditorUtility.DisplayDialog("ID Migration Stopped", exception.Message + "\n\nSome changes may already have been saved. " +
                "See the Console for details." + (applyTo != StylerIDMigration.ApplyTo.SelectedObjects
                ? "\nThe source ID is retained while migrating. After fixing the error, rerun the same migration to finish."
                : ""), "OK");
            RefreshSources();
        }
    }
}
