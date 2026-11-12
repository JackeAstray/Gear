using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;
    using EvoButton = Evo.UI.Button;
    using Image = UnityEngine.UI.Image;
    using ImageElement = UnityEngine.UIElements.Image;
    using ListView = UnityEngine.UIElements.ListView;

    /// <summary>
    /// Searchable grid of an Icon Library's icons that applies the selected one to an Image, RawImage,
    /// ProceduralShape, or Evo UI Button. Updates on selection, component, and library changes only.
    /// </summary>
    public class IconBrowserWindow : EditorWindow
    {
        enum IconSize { Small, Medium, Large }

        const string AllCategories = "All Categories";
        const float GridPadding = 10;
        const float TileGap = 6;
        const float LabelHeight = 18;
        const float SectionHeight = 30;
        static readonly float[] MinTileWidths = { 40, 52, 68 };

        [SerializeField] IconLibrary currentLibrary;
        [SerializeField] string searchQuery = "";
        [SerializeField] string selectedCategory = "";
        [SerializeField] string currentIconID = "";
        [SerializeField] int currentResolution;
        [SerializeField] IconSize iconSize = IconSize.Medium;

        readonly List<IconLibrary> libraries = new();
        readonly List<string> categories = new();
        readonly List<GridRow> rows = new();
        readonly List<int> itemRows = new();
        List<IconLibrary.Item> results = new();
        List<int> resolutions = new();
        int columns;
        float tileWidth;

        // Header
        ToolbarMenu libraryMenu;
        ToolbarMenu moreMenu;
        VisualElement metaRow;
        Label pathLabel;
        Label countLabel;
        ToolbarSearchField searchField;
        DropdownField categoryField;
        EvoEditorUITK.AssetTracker libraryTracker;

        // Grid
        ListView grid;
        ScrollView gridScroll;
        EvoEditorUITK.EmptyState emptyState;

        // Footer
        VisualElement footer;
        ImageElement selectedImage;
        Label selectedName;
        Label selectedCategoryLabel;
        DropdownField resolutionField;
        Label targetDescription;
        Button applyButton;

        void OnEnable()
        {
            titleContent = new GUIContent("Icon Browser", Resources.Load<Texture2D>("GUI/UI_IconBrowser"));
            minSize = new Vector2(280, 420);
            if (currentLibrary == null)
                currentLibrary = IconLibrary.GetDefault();

            Selection.selectionChanged += UpdateFooter;
            Undo.undoRedoPerformed += UpdateFooter;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= UpdateFooter;
            Undo.undoRedoPerformed -= UpdateFooter;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            EditorApplication.projectChanged -= OnProjectChanged;
            libraryTracker?.Stop();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("icon-browser");
            EvoEditorUITK.Apply(this, nameof(IconBrowserWindow));

            root.Add(CreateHeader());
            root.Add(CreateGrid());
            root.Add(CreateFooter());
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            columns = 0;
            RefreshLibraries();
            RefreshLibrary(true);
        }

        #region Layout
        VisualElement CreateHeader()
        {
            VisualElement header = EvoEditorUITK.CreateMenuHeader("Switch icon library", "More", out libraryMenu, out moreMenu, out metaRow);
            PopulateMoreMenu(moreMenu.menu);

            pathLabel = EvoEditorUITK.CreatePathLabel(PingLibrary);
            metaRow.Add(pathLabel);

            countLabel = new Label();
            countLabel.AddToClassList("evo-header__count");
            metaRow.Add(countLabel);

            var filters = new VisualElement();
            filters.AddToClassList("icon-filters");
            header.Add(filters);

            searchField = EvoEditorUITK.CreateSearchField("Search by name or keyword", value =>
            {
                searchQuery = value;
                RefreshResults(true);
            });
            searchField.AddToClassList("icon-search");
            filters.Add(searchField);

            categoryField = EvoEditorUITK.CreateDropdown(null, new List<string> { AllCategories }, 0, "Category", index =>
            {
                selectedCategory = index > 0 ? categories[index - 1] : "";
                RefreshResults(true);
            });
            categoryField.AddToClassList("icon-category");
            filters.Add(categoryField);

            libraryTracker?.Stop();
            libraryTracker = new EvoEditorUITK.AssetTracker();
            header.Add(libraryTracker);

            return header;
        }

        VisualElement CreateGrid()
        {
            var content = new VisualElement();
            content.AddToClassList("icon-content");

            grid = new ListView
            {
                itemsSource = rows,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                focusable = true
            };
            grid.AddToClassList("icon-grid");
            content.Add(grid);

            // An always visible scrollbar keeps the width stable while the row count changes
            gridScroll = grid.Q<ScrollView>();
            gridScroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            gridScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateColumns());

            emptyState = new EvoEditorUITK.EmptyState();
            content.Add(emptyState);

            // Last, so it draws over the scrolled content
            content.Add(new EvoEditorUITK.ScrollShadow(gridScroll));

            return content;
        }

        VisualElement CreateFooter()
        {
            footer = new VisualElement();
            footer.AddToClassList("evo-footer");
            footer.AddToClassList("icon-footer");

            var selection = new VisualElement();
            selection.AddToClassList("icon-selection");
            footer.Add(selection);

            var stage = new VisualElement();
            stage.AddToClassList("icon-stage");
            stage.AddToClassList("icon-selection__stage");
            selectedImage = new ImageElement { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            selectedImage.AddToClassList("icon-selection__image");
            stage.Add(selectedImage);
            selection.Add(stage);

            var text = new VisualElement();
            text.AddToClassList("icon-selection__text");
            selectedName = new Label();
            selectedName.AddToClassList("icon-selection__name");
            text.Add(selectedName);
            selectedCategoryLabel = new Label();
            selectedCategoryLabel.AddToClassList("icon-selection__category");
            text.Add(selectedCategoryLabel);
            selection.Add(text);

            resolutionField = new DropdownField { tooltip = "Sprite resolution to apply" };
            resolutionField.AddToClassList("icon-resolution");
            resolutionField.RegisterValueChangedCallback(_ =>
            {
                if (resolutionField.index >= 0 && resolutionField.index < resolutions.Count)
                    currentResolution = resolutions[resolutionField.index];

                UpdateFooter();
            });
            selection.Add(resolutionField);

            targetDescription = EvoEditorUITK.CreateDescription();
            footer.Add(targetDescription);

            applyButton = EvoEditorUITK.CreateButton(null, null, ApplyIconToTarget);
            applyButton.AddToClassList("evo-primary");
            applyButton.AddToClassList("icon-apply");
            footer.Add(applyButton);

            return footer;
        }

        void PopulateMoreMenu(DropdownMenu menu)
        {
            menu.AppendAction("Icon Library Importer...", _ => IconLibraryImporter.ShowWindow(currentLibrary));
            menu.AppendSeparator();
            menu.AppendAction("Show Library in Project", _ => PingLibrary(), LibraryActionStatus);
            menu.AppendAction("Open Library in Inspector", _ => Selection.activeObject = currentLibrary, LibraryActionStatus);
            menu.AppendSeparator();

            foreach (IconSize size in Enum.GetValues(typeof(IconSize)))
                menu.AppendAction($"Icon Size/{size}", _ => SetIconSize(size), _ => EvoEditorUITK.MenuChecked(iconSize == size));
        }

        DropdownMenuAction.Status LibraryActionStatus(DropdownMenuAction action)
        {
            return currentLibrary != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
        }

        void SetIconSize(IconSize size)
        {
            iconSize = size;
            columns = 0;
            UpdateColumns();
        }
        #endregion

        #region Grid
        sealed class GridRow
        {
            public string Category; // Set on section headers only
            public int Start;
            public int Length;
        }

        // Like the UI Catalog, sections only appear while the whole library is in view
        bool ShowSections => string.IsNullOrEmpty(selectedCategory) && string.IsNullOrWhiteSpace(searchQuery) && categories.Count > 1;

        VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("icon-row");

            var section = new EvoEditorUITK.Section();
            section.AddToClassList("icon-section");
            section.RegisterCallback<ClickEvent>(_ =>
            {
                if (section.userData is string category && categories.Contains(category))
                    categoryField.index = categories.IndexOf(category) + 1;
            });
            row.Add(section);

            var tiles = new VisualElement { pickingMode = PickingMode.Ignore };
            tiles.AddToClassList("icon-row__tiles");
            row.Add(tiles);

            return row;
        }

        VisualElement MakeTile()
        {
            var tile = new VisualElement();
            tile.AddToClassList("icon-tile");

            var stage = new VisualElement { pickingMode = PickingMode.Ignore };
            stage.AddToClassList("icon-stage");
            stage.AddToClassList("icon-tile__stage");
            tile.Add(stage);

            var image = new ImageElement { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("icon-tile__image");
            stage.Add(image);

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("icon-tile__label");
            tile.Add(label);

            tile.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || tile.userData is not int index || index >= results.Count)
                    return;

                grid.Focus();
                SelectIcon(results[index].ID);
                if (evt.clickCount == 2)
                    ApplyIconToTarget();
            });

            return tile;
        }

        void BindRow(VisualElement element, int rowIndex)
        {
            GridRow row = rows[rowIndex];
            var section = (EvoEditorUITK.Section)element[0];
            VisualElement tiles = element[1];
            bool isSection = row.Category != null;
            section.style.display = isSection ? DisplayStyle.Flex : DisplayStyle.None;
            tiles.style.display = isSection ? DisplayStyle.None : DisplayStyle.Flex;
            // The list's top spacing sits inside the scroll area, so content scrolls under the header.
            // It matches the gap that ends every row, which is also the space after the last one.
            float top = rowIndex == 0 && !isSection ? TileGap : 0;
            element.style.paddingTop = top;
            element.style.height = (isSection ? SectionHeight : tileWidth + LabelHeight + TileGap) + top;

            if (isSection)
            {
                bool named = row.Category.Length > 0;
                section.userData = row.Category;
                section.tooltip = named ? $"Show only {row.Category}" : null;
                section.EnableInClassList("evo-section--link", named);
                section.Text = named ? row.Category : "Other";
                return;
            }

            while (tiles.childCount < columns)
                tiles.Add(MakeTile());

            float padding = Mathf.Round(tileWidth * 0.22f);
            for (int column = 0; column < tiles.childCount; column++)
            {
                VisualElement tile = tiles[column];
                if (column >= row.Length)
                {
                    tile.style.display = DisplayStyle.None;
                    continue;
                }

                int index = row.Start + column;
                IconLibrary.Item item = results[index];
                tile.style.display = DisplayStyle.Flex;
                tile.style.width = tileWidth;
                tile.style.marginRight = column < columns - 1 ? TileGap : 0;
                tile.userData = index;
                tile.tooltip = item.ID;
                tile.EnableInClassList("icon-tile--selected", item.ID == currentIconID);

                VisualElement stage = tile[0];
                stage.style.height = tileWidth;
                stage.style.paddingLeft = stage.style.paddingRight = stage.style.paddingTop = stage.style.paddingBottom = padding;
                ((ImageElement)stage[0]).sprite = item.GetSprite();
                ((Label)tile[1]).text = item.ID;
            }
        }

        void UpdateColumns()
        {
            float width = gridScroll.contentViewport.layout.width;
            if (float.IsNaN(width) || width <= 0)
                return;

            // Tiles stretch to fill the row, so the grid always uses the full width
            float available = width - GridPadding * 2;
            float minWidth = MinTileWidths[(int)iconSize];
            int newColumns = Mathf.Max(1, Mathf.FloorToInt((available + TileGap) / (minWidth + TileGap)));
            float newWidth = Mathf.Floor((available - TileGap * (newColumns - 1)) / newColumns);
            if (newColumns == columns && Mathf.Approximately(newWidth, tileWidth))
                return;

            bool firstLayout = columns == 0;
            columns = newColumns;
            tileWidth = newWidth;
            RebuildRows();

            // Opening the window or changing the icon size keeps the selected icon in view
            if (firstLayout)
                grid.schedule.Execute(ScrollToSelection);
        }

        void RebuildRows()
        {
            if (grid == null || columns == 0)
                return;

            rows.Clear();
            itemRows.Clear();
            bool sections = ShowSections;
            for (int start = 0; start < results.Count;)
            {
                int end = results.Count;
                if (sections)
                {
                    // Results are already grouped by category, so a section runs until the category changes
                    string category = results[start].category ?? "";
                    end = start + 1;
                    while (end < results.Count && (results[end].category ?? "") == category)
                        end++;

                    rows.Add(new GridRow { Category = category });
                }

                for (int i = start; i < end; i += columns)
                {
                    int length = Mathf.Min(columns, end - i);
                    for (int j = 0; j < length; j++)
                        itemRows.Add(rows.Count);

                    rows.Add(new GridRow { Start = i, Length = length });
                }

                start = end;
            }

            grid.Rebuild();
        }

        void ScrollToSelection()
        {
            int index = results.FindIndex(item => item.ID == currentIconID);
            if (index >= 0 && index < itemRows.Count)
                grid.ScrollToItem(itemRows[index]);
        }

        void MoveSelection(int offset)
        {
            if (results.Count == 0)
                return;

            int index = results.FindIndex(item => item.ID == currentIconID);
            index = index < 0 ? 0 : Mathf.Clamp(index + offset, 0, results.Count - 1);
            SelectIcon(results[index].ID);
            ScrollToSelection();
        }

        void SelectFirstIfNone()
        {
            if (string.IsNullOrEmpty(currentIconID))
                MoveSelection(0);
        }

        void MoveVertical(int direction)
        {
            int index = results.FindIndex(item => item.ID == currentIconID);
            if (index < 0 || index >= itemRows.Count)
            {
                MoveSelection(0);
                return;
            }

            // Keep the column, skipping section headers and clamping to shorter rows
            int row = itemRows[index];
            int column = index - rows[row].Start;
            for (int next = row + direction; next >= 0 && next < rows.Count; next += direction)
            {
                if (rows[next].Category != null)
                    continue;

                SelectIcon(results[rows[next].Start + Mathf.Min(column, rows[next].Length - 1)].ID);
                ScrollToSelection();
                return;
            }
        }
        #endregion

        #region Library
        void RefreshLibraries()
        {
            libraries.Clear();
            libraries.AddRange(EvoEditorUITK.FindAssets<IconLibrary>());

            if (libraryMenu == null)
                return;

            var menu = libraryMenu.menu;
            EvoEditorUITK.RebuildAssetMenu(menu, libraries, () => currentLibrary, SelectLibrary);
            menu.AppendAction("Icon Library Importer...", _ => IconLibraryImporter.ShowWindow(currentLibrary));
        }

        void SelectLibrary(IconLibrary library)
        {
            if (currentLibrary == library)
                return;

            currentLibrary = library;
            searchQuery = selectedCategory = currentIconID = "";
            searchField?.SetValueWithoutNotify("");
            RefreshLibrary(true);
        }

        void OnProjectChanged()
        {
            RefreshLibraries();
            if (currentLibrary == null)
                currentLibrary = IconLibrary.GetDefault();

            // Sprites may have been reimported, so rebind without losing the scroll position
            RefreshLibrary(false);
        }

        void RefreshLibrary(bool resetScroll)
        {
            categories.Clear();
            if (currentLibrary != null)
            {
                currentLibrary.InvalidateCache();
                if (currentLibrary.categories != null)
                {
                    foreach (string category in currentLibrary.categories)
                    {
                        if (!string.IsNullOrEmpty(category) && !categories.Contains(category))
                            categories.Add(category);
                    }
                }
            }

            if (!categories.Contains(selectedCategory))
                selectedCategory = "";

            TrackLibrary();
            UpdateHeader();
            SelectIcon(currentIconID);
            RefreshResults(resetScroll);
        }

        void TrackLibrary()
        {
            if (libraryTracker == null || currentLibrary != null && libraryTracker.Target == currentLibrary)
                return;

            // Edits in the Inspector or the importer update the grid without polling
            libraryTracker.Track(currentLibrary, () => RefreshLibrary(false));
        }

        void UpdateHeader()
        {
            if (libraryMenu == null)
                return;

            bool hasLibrary = currentLibrary != null;
            libraryMenu.text = hasLibrary ? currentLibrary.name : "No Library";
            metaRow.style.display = hasLibrary ? DisplayStyle.Flex : DisplayStyle.None;
            searchField.SetEnabled(hasLibrary);
            searchField.SetValueWithoutNotify(searchQuery);

            var choices = new List<string> { AllCategories };
            choices.AddRange(categories);
            EvoEditorUITK.SetChoices(categoryField, choices, categories.IndexOf(selectedCategory) + 1);
            categoryField.SetEnabled(hasLibrary && categories.Count > 0);

            if (hasLibrary)
                EvoEditorUITK.SetPath(pathLabel, currentLibrary);
        }

        void RefreshResults(bool resetScroll)
        {
            results = currentLibrary != null ? currentLibrary.Search(searchQuery, selectedCategory) : new List<IconLibrary.Item>();

            // Group by the library's category order; the sort is stable, so icons keep their order within a section
            if (ShowSections)
                results = results.OrderBy(item => categories.IndexOf(item.category ?? "") is int i && i >= 0 ? i : int.MaxValue).ToList();
            RebuildRows();

            if (resetScroll && gridScroll != null)
                gridScroll.scrollOffset = Vector2.zero;

            UpdateCount();
            UpdateEmptyState();
        }

        void UpdateCount()
        {
            if (countLabel == null)
                return;

            int total = currentLibrary != null && currentLibrary.icons != null ? currentLibrary.icons.Count : 0;
            bool filtered = !string.IsNullOrWhiteSpace(searchQuery) || !string.IsNullOrEmpty(selectedCategory);
            countLabel.text = filtered ? $"{results.Count} of {total} icons" : total == 1 ? "1 icon" : $"{total} icons";
        }

        void UpdateEmptyState()
        {
            if (emptyState == null)
                return;

            bool empty = results.Count == 0;
            grid.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            emptyState.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            if (!empty)
                return;

            if (currentLibrary == null)
                emptyState.Set("No icon library", "Import icons with the Icon Library Importer to start browsing.",
                    "Open Importer", () => IconLibraryImporter.ShowWindow());
            else if (currentLibrary.icons == null || currentLibrary.icons.Count == 0)
                emptyState.Set("This library is empty", "Add icons with the Icon Library Importer.",
                    "Open Importer", () => IconLibraryImporter.ShowWindow(currentLibrary));
            else if (!string.IsNullOrWhiteSpace(searchQuery))
                emptyState.Set($"No icons match \"{searchQuery.Trim()}\"", "Try a different name or keyword.",
                    "Clear Search", () => searchField.value = "");
            else
                emptyState.Set("No icons in this category", "Pick another category to see more icons.",
                    "Show All Categories", () => categoryField.index = 0);
        }

        void PingLibrary()
        {
            if (currentLibrary != null)
                EditorGUIUtility.PingObject(currentLibrary);
        }
        #endregion

        #region Selection
        void SelectIcon(string id)
        {
            var item = currentLibrary != null ? currentLibrary.GetItem(id) : null;
            bool changed = (item != null ? item.ID : "") != currentIconID;
            currentIconID = item != null ? item.ID : "";
            resolutions = item != null ? item.GetAvailableResolutions() : new List<int>();
            if (!resolutions.Contains(currentResolution))
                currentResolution = resolutions.Count > 0 ? resolutions[0] : 0;

            if (resolutionField != null)
            {
                EvoEditorUITK.SetChoices(resolutionField, resolutions.ConvertAll(value => $"{value}px"), resolutions.IndexOf(currentResolution));
                resolutionField.style.display = item != null ? DisplayStyle.Flex : DisplayStyle.None;
                resolutionField.SetEnabled(resolutions.Count > 1);
            }

            // Only the visible rows rebind
            if (changed)
                grid?.RefreshItems();

            UpdateFooter();
        }

        Sprite GetSelectedSprite() => currentLibrary != null ? currentLibrary.GetSprite(currentIconID, currentResolution) : null;

        void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            // Inspector edits such as enabling a button's icon don't change the selection
            UpdateFooter();
        }

        void UpdateFooter()
        {
            if (footer == null)
                return;

            var item = currentLibrary != null ? currentLibrary.GetItem(currentIconID) : null;
            Sprite sprite = GetSelectedSprite();
            selectedImage.sprite = sprite;
            selectedName.text = item != null ? item.ID : "No icon selected";
            selectedName.EnableInClassList("icon-selection__name--empty", item == null);
            selectedCategoryLabel.text = item != null && !string.IsNullOrEmpty(item.category) ? item.category : "";
            selectedCategoryLabel.style.display = selectedCategoryLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            Component target = GetTarget(out string message);
            bool warning = target == null && Selection.activeGameObject != null;
            if (target is RawImage && sprite != null && !TryGetRawImageData(sprite, out _, out _))
            {
                target = null;
                warning = true;
                message = "This packed sprite has no source texture. Select an Image or ProceduralShape instead.";
            }

            // A compatible selection only needs the button; anything else only needs the reason
            bool hasTarget = target != null;
            targetDescription.text = message;
            targetDescription.EnableInClassList("evo-description--warning", warning);
            targetDescription.style.display = hasTarget ? DisplayStyle.None : DisplayStyle.Flex;
            applyButton.style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasTarget)
                return;

            applyButton.text = $"Apply to '{target.name}'";
            applyButton.tooltip = sprite != null ? $"Apply {currentIconID} to {target.name}. Double-clicking an icon also applies it." : "Select an icon first.";
            applyButton.SetEnabled(sprite != null);
        }
        #endregion

        #region Keyboard
        void OnKeyDown(KeyDownEvent evt)
        {
            if (EvoEditorUITK.HandleSearchKeys(evt, searchField, grid, SelectFirstIfNone))
                return;

            var target = evt.target as VisualElement;
            bool inSearch = target != null && searchField.Contains(target);
            bool inGrid = target != null && grid.Contains(target);
            if (!inSearch && !inGrid)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (inSearch)
                        SelectFirstIfNone();

                    ApplyIconToTarget();
                    evt.StopPropagation();
                    break;

                case KeyCode.LeftArrow when inGrid: MoveSelection(-1); evt.StopPropagation(); break;
                case KeyCode.RightArrow when inGrid: MoveSelection(1); evt.StopPropagation(); break;
                case KeyCode.UpArrow when inGrid: MoveVertical(-1); evt.StopPropagation(); break;
                case KeyCode.DownArrow when inGrid: MoveVertical(1); evt.StopPropagation(); break;
            }
        }
        #endregion

        #region Apply
        static Component GetTarget(out string message)
        {
            message = "Select an Image, RawImage, ProceduralShape, or Evo UI Button to apply an icon.";
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return null;

            // A button's background graphic must never take precedence over its icon
            if (selected.TryGetComponent(out EvoButton button))
            {
                if (button.imageObject != null)
                    return button;

                message = "Assign an icon Image reference on the selected Evo UI Button.";
                return null;
            }

            if (selected.TryGetComponent(out Image image))
                return image;
            if (selected.TryGetComponent(out RawImage rawImage))
                return rawImage;
            if (selected.TryGetComponent(out ProceduralShape shape))
                return shape;

            return null;
        }

        static bool TryGetRawImageData(Sprite sprite, out Texture2D texture, out Rect uvRect)
        {
            // Use the original texture for packed sprites: RawImage cannot represent atlas rotation or tight meshes
            texture = sprite.packed ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite)) : sprite.texture;
            uvRect = default;
            if (texture == null || texture.width == 0 || texture.height == 0)
                return false;

            Rect rect = sprite.rect;
            uvRect = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            return true;
        }

        void ApplyIconToTarget()
        {
            Component target = GetTarget(out _);
            Sprite sprite = GetSelectedSprite();
            if (target == null || sprite == null)
                return;

            Texture2D texture = null;
            Rect uvRect = default;
            if (target is RawImage && !TryGetRawImageData(sprite, out texture, out uvRect))
                return;

            // Setting a button icon also shows its object
            UnityEngine.Object[] changedObjects = target is EvoButton button
                ? new UnityEngine.Object[] { button.imageObject, button.imageObject.gameObject }
                : new UnityEngine.Object[] { target };
            Undo.RecordObjects(changedObjects, "Apply Icon");

            switch (target)
            {
                case EvoButton evoButton:
                    evoButton.Icon = sprite;
                    break;
                case Image image:
                    image.sprite = sprite;
                    break;
                case RawImage rawImage:
                    rawImage.texture = texture;
                    rawImage.uvRect = uvRect;
                    break;
                case ProceduralShape shape:
                    shape.Sprite = sprite;
                    break;
            }

            foreach (UnityEngine.Object changedObject in changedObjects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(changedObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(changedObject);

                EditorUtility.SetDirty(changedObject);
            }
            SceneView.RepaintAll();
        }
        #endregion
    }
}
