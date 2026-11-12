using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    using Button = UnityEngine.UIElements.Button;
    using Image = UnityEngine.UIElements.Image;

    /// <summary>
    /// Browsable, searchable view of everything in the GameObject/Evo UI menu, with rendered previews.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal class CatalogWindow : EditorWindow
    {
        const string AllCollection = "@all";
        const string FavoritesCollection = "@favorites";
        const string FavoritesPref = "Evo.UI.Catalog.Favorites";
        const string PlacementPref = "Evo.UI.Catalog.Placement";
        const float BaseCardWidth = 150;
        const float DragThreshold = 6;
        const float DetailsMinWidth = 240;
        const float GridMinWidth = 240;

        enum SortMode { Category, Name }
        internal enum Placement { Sibling, Child, CanvasRoot }

        static readonly List<string> PlacementLabels = new() { "Sibling of Selection", "Child of Selection", "Canvas Root" };
        static readonly float[] CardScales = { 0.75f, 1, 1.25f, 1.5f, 2 };
        static event Action LibraryChanged;
        static Texture2D FavoriteOnIcon => EvoEditorGUI.GetIcon("Favorite_colored", false);
        static Texture2D FavoriteOffIcon => EvoEditorGUI.GetIcon("Favorite", false);

        [SerializeField] string collection = AllCollection;
        [SerializeField] string selectedItemId;
        [SerializeField] string searchText = "";
        [SerializeField] float cardScale = 1;
        [SerializeField] SortMode sortMode;
        [SerializeField] bool hideDetails;

        readonly Dictionary<CatalogItem, Card> cards = new();
        readonly Dictionary<string, NavRow> navRows = new();
        readonly Dictionary<CatalogItem, VisualElement> variants = new();
        readonly List<CatalogItem> visibleItems = new();

        ToolbarSearchField searchField;
        ToolbarMenu sizeMenu;
        ScrollView gridScroll;
        TwoPaneSplitView contentSplit;
        Label statusLabel;

        CatalogItem detailsItem;
        VisualElement detailsContent;
        Label detailsEmpty;
        VisualElement detailsPreview;
        Image detailsImage;
        Image detailsPlaceholder;
        Label detailsTitle;
        Button detailsCategory;
        Button detailsPrefab;
        Label detailsDescription;
        Label variantsHeading;
        VisualElement variantsContainer;
        VisualElement detailsTarget;
        Button favoriteButton;

        CatalogItem dragCandidate;
        Vector2 dragStart;

        sealed class Card
        {
            public VisualElement Root;
            public VisualElement Preview;
            public Image Image;
            public Image Placeholder;
            public VisualElement Favorite;
            public Image FavoriteIcon;
        }

        sealed class NavRow
        {
            public VisualElement Root;
            public Label Count;
        }

        /// <summary>
        /// Where created elements go in the Hierarchy. Shared with Scene view drops, so it's stored per user.
        /// </summary>
        internal static Placement CurrentPlacement
        {
            get => (Placement)EditorPrefs.GetInt(PlacementPref, (int)Placement.Sibling);
            set => EditorPrefs.SetInt(PlacementPref, (int)value);
        }

        public static void ShowWindow()
        {
            var window = GetWindow<CatalogWindow>();
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Evo UI Catalog", Resources.Load<Texture2D>("GUI/UI_UICatalog"));
            minSize = new Vector2(680, 380);
            if (Array.IndexOf(CardScales, cardScale) < 0)
                cardScale = 1;

            CatalogPreviews.AddUser();
            CatalogPreviews.Changed += OnPreviewChanged;
            LibraryChanged += OnLibraryChanged;
            Selection.selectionChanged += UpdateTarget;
            EditorApplication.hierarchyChanged += UpdateTarget;
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
        }

        void OnDisable()
        {
            CatalogPreviews.Changed -= OnPreviewChanged;
            LibraryChanged -= OnLibraryChanged;
            Selection.selectionChanged -= UpdateTarget;
            EditorApplication.hierarchyChanged -= UpdateTarget;
            PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
            PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
            CatalogPreviews.RemoveUser();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            EvoEditorUITK.Apply(this, nameof(CatalogWindow));

            root.Add(CreateToolbar());

            var mainSplit = new TwoPaneSplitView(0, 190, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "evo-catalog-main-split" };
            mainSplit.AddToClassList("catalog-split");
            mainSplit.Add(CreateNavigation());

            contentSplit = new TwoPaneSplitView(1, 280, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "evo-catalog-content-split" };
            contentSplit.Add(CreateGrid());
            contentSplit.Add(CreateDetails());
            mainSplit.Add(contentSplit);
            root.Add(mainSplit);

            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);

            foreach (CatalogItem item in Catalog.Items)
                cards[item] = CreateCard(item);

            ApplyCardWidth();
            SetDetailsVisible(!hideDetails);
            RefreshContent(true);
            SelectItem(Catalog.Find(selectedItemId), true);
        }

        #region Layout
        VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("catalog-toolbar");

            searchField = EvoEditorUITK.CreateSearchField("Search by name, category, or keyword", value =>
            {
                searchText = value;
                RefreshContent(true);
            });
            searchField.AddToClassList("catalog-search");
            searchField.SetValueWithoutNotify(searchText);
            toolbar.Add(searchField);
            toolbar.Add(new ToolbarSpacer { flex = true });

            sizeMenu = new ToolbarMenu { tooltip = "Card size" };
            sizeMenu.AddToClassList("catalog-toolbar-menu");
            foreach (float scale in CardScales)
                sizeMenu.menu.AppendAction(FormatScale(scale), _ => SetCardScale(scale), _ => EvoEditorUITK.MenuChecked(Mathf.Approximately(cardScale, scale)));
            toolbar.Add(sizeMenu);

            var viewMenu = new ToolbarMenu { text = "View" };
            viewMenu.AddToClassList("catalog-toolbar-menu");
            viewMenu.menu.AppendAction("Sort by Category", _ => SetSortMode(SortMode.Category), _ => EvoEditorUITK.MenuChecked(sortMode == SortMode.Category));
            viewMenu.menu.AppendAction("Sort by Name", _ => SetSortMode(SortMode.Name), _ => EvoEditorUITK.MenuChecked(sortMode == SortMode.Name));
            viewMenu.menu.AppendSeparator();
            viewMenu.menu.AppendAction("Details Panel", _ => SetDetailsVisible(hideDetails), _ => EvoEditorUITK.MenuChecked(!hideDetails));
            viewMenu.menu.AppendSeparator();
            viewMenu.menu.AppendAction("Refresh Previews", _ => CatalogPreviews.RefreshAll());
            toolbar.Add(viewMenu);

            return toolbar;
        }

        static string FormatScale(float scale) => $"{Mathf.RoundToInt(scale * 100)}%";

        VisualElement CreateNavigation()
        {
            var navigation = new ScrollView(ScrollViewMode.Vertical);
            navigation.AddToClassList("catalog-nav");
            navigation.contentContainer.AddToClassList("catalog-nav__content");

            navigation.Add(EvoEditorUITK.CreateHeading("Library", "catalog-nav__header"));
            navigation.Add(CreateNavRow(AllCollection, "All Elements", EditorGUIUtility.IconContent("PreMatCube").image));
            navigation.Add(CreateNavRow(FavoritesCollection, "Favorites", EditorGUIUtility.IconContent("Favorite").image));

            navigation.Add(EvoEditorUITK.CreateHeading("Categories", "catalog-nav__header"));
            foreach (CatalogCategory category in Catalog.Categories)
                navigation.Add(CreateNavRow(category.Name, category.Name, category.Icon));

            return navigation;
        }

        VisualElement CreateNavRow(string id, string text, Texture icon)
        {
            var row = new VisualElement();
            row.AddToClassList("catalog-nav__row");

            var image = new Image { image = icon, pickingMode = PickingMode.Ignore };
            image.AddToClassList("catalog-nav__icon");
            row.Add(image);

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("catalog-nav__label");
            row.Add(label);

            var count = new Label { pickingMode = PickingMode.Ignore };
            count.AddToClassList("catalog-nav__count");
            row.Add(count);

            row.RegisterCallback<ClickEvent>(_ => SelectCollection(id));
            navRows[id] = new NavRow { Root = row, Count = count };
            return row;
        }

        VisualElement CreateGrid()
        {
            var content = new VisualElement { style = { minWidth = GridMinWidth } };
            content.AddToClassList("catalog-content");

            gridScroll = new ScrollView(ScrollViewMode.Vertical) { focusable = true };
            gridScroll.AddToClassList("catalog-grid-scroll");
            gridScroll.contentContainer.AddToClassList("catalog-grid-scroll__content");
            gridScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content.Add(gridScroll);

            statusLabel = new Label();
            statusLabel.AddToClassList("catalog-status");
            statusLabel.schedule.Execute(UpdateStatus).Every(250); // Tracks preview progress
            content.Add(statusLabel);

            return content;
        }

        Card CreateCard(CatalogItem item)
        {
            var card = new Card { Root = new VisualElement { tooltip = item.Description } };
            card.Root.AddToClassList("catalog-card");

            card.Preview = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Preview.AddToClassList("catalog-card__preview");
            card.Root.Add(card.Preview);

            card.Placeholder = new Image { pickingMode = PickingMode.Ignore };
            card.Placeholder.AddToClassList("catalog-card__placeholder");
            card.Preview.Add(card.Placeholder);

            card.Image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            card.Image.AddToClassList("catalog-card__image");
            card.Preview.Add(card.Image);

            card.Favorite = new VisualElement { tooltip = "Favorite" };
            card.Favorite.AddToClassList("catalog-card__favorite");
            card.FavoriteIcon = new Image { pickingMode = PickingMode.Ignore };
            card.FavoriteIcon.AddToClassList("catalog-card__favorite-icon");
            card.Favorite.Add(card.FavoriteIcon);
            card.Favorite.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                ToggleFavorite(item);
                evt.StopPropagation();
            });
            card.Root.Add(card.Favorite);

            var label = new Label(item.Name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("catalog-card__label");
            card.Root.Add(label);

            card.Root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                SelectItem(item);
                gridScroll.Focus();

                if (evt.clickCount == 2)
                {
                    dragCandidate = null;
                    CreateItem(item, CurrentPlacement);
                    return;
                }

                dragCandidate = item;
                dragStart = evt.position;
            });
            card.Root.RegisterCallback<PointerMoveEvent>(evt => TryStartDrag(item, evt.position, evt.pressedButtons));
            card.Root.RegisterCallback<PointerLeaveEvent>(evt => TryStartDrag(item, evt.position, evt.pressedButtons, true));
            card.Root.RegisterCallback<PointerUpEvent>(_ => dragCandidate = null);
            card.Root.AddManipulator(new ContextualMenuManipulator(evt => PopulateItemMenu(evt.menu, item)));

            UpdateCard(item, card);
            return card;
        }

        void TryStartDrag(CatalogItem item, Vector2 position, int pressedButtons, bool leaving = false)
        {
            if (dragCandidate != item || (pressedButtons & 1) == 0)
                return;

            if (!leaving && (position - dragStart).sqrMagnitude < DragThreshold * DragThreshold)
                return;

            dragCandidate = null;
            CatalogDragAndDrop.StartDrag(item);
        }

        VisualElement CreateDetails()
        {
            var details = new ScrollView(ScrollViewMode.Vertical) { style = { minWidth = DetailsMinWidth } };
            details.AddToClassList("catalog-details");
            details.contentContainer.AddToClassList("catalog-details__content");
            details.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            // The panel is darker than the window, so its controls use the lighter fill
            details.AddToClassList("evo-inverted");

            detailsEmpty = new Label("Select an element to see its details.\n\nDouble-click a card or press Enter to create it, or drag it into the Hierarchy or Scene view.");
            detailsEmpty.AddToClassList("catalog-details__empty");
            details.Add(detailsEmpty);

            detailsContent = new VisualElement();
            details.Add(detailsContent);

            detailsPreview = new VisualElement();
            detailsPreview.AddToClassList("catalog-details__preview");
            detailsPreview.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                // Match the thumbnail's aspect ratio
                float height = Mathf.Round(evt.newRect.width * CatalogPreviews.Height / CatalogPreviews.Width);
                if (Mathf.Abs(evt.newRect.height - height) > 0.5f)
                    detailsPreview.style.height = height;
            });
            detailsContent.Add(detailsPreview);

            detailsPlaceholder = new Image();
            detailsPlaceholder.AddToClassList("catalog-details__placeholder");
            detailsPreview.Add(detailsPlaceholder);

            detailsImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            detailsImage.AddToClassList("catalog-details__image");
            detailsPreview.Add(detailsImage);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("catalog-details__title-row");
            detailsTitle = new Label();
            detailsTitle.AddToClassList("catalog-details__title");
            titleRow.Add(detailsTitle);
            var docsButton = EvoEditorUITK.CreateIconButton(EditorGUIUtility.IconContent("_Help").image,
                "Open the online documentation", () => OpenDocumentation(detailsItem));
            docsButton.AddToClassList("catalog-details__docs");
            titleRow.Add(docsButton);
            detailsContent.Add(titleRow);

            var meta = new VisualElement();
            meta.AddToClassList("catalog-details__meta");
            detailsCategory = new Button(() =>
            {
                if (detailsItem != null)
                    SelectCollection(detailsItem.Category.Name);
            })
            { tooltip = "Show this category" };
            detailsCategory.AddToClassList("evo-chip");
            detailsCategory.AddToClassList("evo-chip--link");
            meta.Add(detailsCategory);
            detailsPrefab = new Button(() => ShowPrefab(detailsItem)) { text = "Show Prefab" };
            detailsPrefab.AddToClassList("evo-chip");
            detailsPrefab.AddToClassList("evo-chip--link");
            meta.Add(detailsPrefab);
            detailsContent.Add(meta);

            detailsDescription = new Label();
            detailsDescription.AddToClassList("catalog-details__description");
            detailsContent.Add(detailsDescription);

            variantsHeading = EvoEditorUITK.CreateHeading("Variants", "catalog-details__heading");
            detailsContent.Add(variantsHeading);
            variantsContainer = new VisualElement();
            variantsContainer.AddToClassList("catalog-details__variants");
            detailsContent.Add(variantsContainer);

            detailsContent.Add(EvoEditorUITK.CreateHeading("Create", "catalog-details__heading"));
            var placementField = EvoEditorUITK.CreateDropdown("Placement", PlacementLabels, (int)CurrentPlacement,
                "Where new elements go in the Hierarchy, for both the Create button and Scene view drops", index =>
                {
                    CurrentPlacement = (Placement)Mathf.Max(0, index);
                    UpdateTarget();
                });
            placementField.AddToClassList("catalog-details__placement");
            detailsContent.Add(placementField);

            detailsTarget = EvoEditorUITK.CreateCallout(null);
            detailsTarget.AddToClassList("catalog-details__target");
            detailsContent.Add(detailsTarget);

            var createRow = new VisualElement();
            createRow.AddToClassList("catalog-details__create-row");

            favoriteButton = EvoEditorUITK.CreateButton(null, FavoriteOffIcon, "Add to Favorites", () => ToggleFavorite(detailsItem));
            favoriteButton.AddToClassList("evo-button--colored-icon");
            favoriteButton.AddToClassList("catalog-details__favorite");
            createRow.Add(favoriteButton);

            var createButton = EvoEditorUITK.CreateButton("Create", null, () => CreateItem(detailsItem, CurrentPlacement));
            createButton.AddToClassList("evo-primary");
            createButton.AddToClassList("catalog-details__create");
            createRow.Add(createButton);
            detailsContent.Add(createRow);

            return details;
        }
        #endregion

        #region Content
        void RefreshContent(bool resetScroll)
        {
            if (gridScroll == null)
                return;

            string query = searchText.Trim().ToLowerInvariant();
            string[] terms = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool searching = terms.Length > 0;

            var scores = new Dictionary<CatalogItem, int>();
            foreach (CatalogItem item in Catalog.Items)
            {
                int score = item.GetMatchScore(query, terms);
                if (score >= 0)
                    scores[item] = score;
            }

            HashSet<string> favorites = new(ReadList(FavoritesPref));
            CatalogCategory category = Catalog.FindCategory(collection);
            if (category == null && collection != FavoritesCollection)
                collection = AllCollection;

            UpdateNavigation(scores, favorites, searching);

            IEnumerable<CatalogItem> source = collection switch
            {
                FavoritesCollection => Catalog.Items.Where(item => favorites.Contains(item.Name)),
                AllCollection => Catalog.Items,
                _ => category.Items
            };

            List<CatalogItem> items = source.Where(scores.ContainsKey).ToList();
            if (searching) { items = items.OrderBy(item => scores[item]).ThenBy(item => item.Order).ToList(); }
            else if (sortMode == SortMode.Name) { items = items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(); }

            gridScroll.Clear();
            visibleItems.Clear();

            if (items.Count == 0) { gridScroll.Add(CreateCollectionEmptyState(searching)); }
            else if (!searching && sortMode == SortMode.Category && collection == AllCollection)
            {
                foreach (CatalogCategory group in Catalog.Categories)
                {
                    var groupItems = items.Where(item => item.Category == group).ToList();
                    if (groupItems.Count > 0)
                        gridScroll.Add(CreateSection(group, groupItems));
                }
            }
            else
            {
                gridScroll.Add(CreateItemGrid(items));
            }

            if (resetScroll)
                gridScroll.scrollOffset = Vector2.zero;

            foreach (KeyValuePair<CatalogItem, Card> pair in cards)
                UpdateFavoriteState(pair.Key, pair.Value, favorites.Contains(pair.Key.Name));

            UpdateStatus();
            RequestPreviews();
        }

        VisualElement CreateCollectionEmptyState(bool searching)
        {
            if (searching)
            {
                bool scoped = collection != AllCollection;
                return new EvoEditorUITK.EmptyState().Set($"No elements match \"{searchText.Trim()}\"",
                    scoped ? "Try searching all elements, or use a different keyword." : "Try a different keyword, such as \"slider\" or \"date\".",
                    scoped ? "Search All Elements" : "Clear Search",
                    scoped ? () => SelectCollection(AllCollection) : () => searchField.value = "");
            }

            return new EvoEditorUITK.EmptyState().Set("No favorites yet", "Click the star on a card to keep it here.");
        }

        VisualElement CreateSection(CatalogCategory category, List<CatalogItem> items)
        {
            var section = new VisualElement();
            section.AddToClassList("catalog-section");

            var header = new EvoEditorUITK.Section(category.Name, category.Icon) { tooltip = $"Show only {category.Name}" };
            header.AddToClassList("evo-section--link");
            header.AddToClassList("catalog-section__header");
            header.RegisterCallback<ClickEvent>(_ => SelectCollection(category.Name));
            section.Add(header);
            section.Add(CreateItemGrid(items));
            return section;
        }

        VisualElement CreateItemGrid(List<CatalogItem> items)
        {
            var grid = new VisualElement();
            grid.AddToClassList("catalog-grid");

            foreach (CatalogItem item in items)
            {
                grid.Add(cards[item].Root);
                visibleItems.Add(item);
            }

            return grid;
        }

        void UpdateNavigation(Dictionary<CatalogItem, int> scores, HashSet<string> favorites, bool searching)
        {
            SetNavCount(AllCollection, scores.Count, searching);
            SetNavCount(FavoritesCollection, scores.Keys.Count(item => favorites.Contains(item.Name)), searching);

            foreach (CatalogCategory category in Catalog.Categories)
                SetNavCount(category.Name, category.Items.Count(scores.ContainsKey), searching);

            foreach (KeyValuePair<string, NavRow> pair in navRows)
                pair.Value.Root.EnableInClassList("catalog-nav__row--selected", pair.Key == collection);
        }

        void SetNavCount(string id, int count, bool searching)
        {
            if (!navRows.TryGetValue(id, out NavRow row))
                return;

            row.Count.text = count > 0 ? count.ToString() : "";
            row.Root.EnableInClassList("catalog-nav__row--empty", searching && count == 0);
        }

        void UpdateStatus()
        {
            if (statusLabel == null)
                return;

            string text = visibleItems.Count == 1 ? "1 element" : $"{visibleItems.Count} elements";
            int pending = CatalogPreviews.PendingCount;
            text += pending == 0 ? "  ·  Double-click or press Enter to create, or drag into the Hierarchy or Scene view"
                : CatalogPreviews.HasTextResources ? $"  ·  Rendering previews ({pending} left)"
                : "  ·  Import TMP Essential Resources to render previews";

            statusLabel.text = text;
        }

        void RequestPreviews()
        {
            // Visible cards first, top to bottom, then the rest in the background
            for (int i = visibleItems.Count - 1; i >= 0; i--)
                CatalogPreviews.Request(visibleItems[i], true);

            foreach (CatalogItem item in Catalog.Items)
                CatalogPreviews.Request(item);
        }

        void SetCardScale(float scale)
        {
            cardScale = scale;
            ApplyCardWidth();
        }

        void ApplyCardWidth()
        {
            float width = Mathf.Round(BaseCardWidth * cardScale);
            float previewHeight = Mathf.Round(width * CatalogPreviews.Height / CatalogPreviews.Width);
            foreach (Card card in cards.Values)
            {
                card.Root.style.width = width;
                card.Preview.style.height = previewHeight;
            }

            if (sizeMenu != null)
                sizeMenu.text = FormatScale(cardScale);
        }

        void UpdateCard(CatalogItem item, Card card)
        {
            Texture2D texture = CatalogPreviews.Get(item);
            card.Image.image = texture;
            card.Image.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
            card.Placeholder.style.display = texture != null ? DisplayStyle.None : DisplayStyle.Flex;
            card.Placeholder.image = card.Placeholder.image != null ? card.Placeholder.image : item.Icon;
            card.Preview.style.backgroundColor = CatalogPreviews.Background;
            card.Root.EnableInClassList("catalog-card--failed", CatalogPreviews.HasFailed(item));
        }

        static void UpdateFavoriteState(CatalogItem _, Card card, bool isFavorite)
        {
            card.Favorite.EnableInClassList("catalog-card__favorite--on", isFavorite);
            card.Favorite.tooltip = isFavorite ? "Remove from Favorites" : "Add to Favorites";
            card.FavoriteIcon.image = isFavorite ? FavoriteOnIcon : FavoriteOffIcon;
        }

        void OnPreviewChanged(CatalogItem item)
        {
            if (cards.TryGetValue(item, out Card card))
                UpdateCard(item, card);

            if (item == detailsItem)
                UpdateDetailsPreview();

            if (variants.TryGetValue(item, out VisualElement variant))
                UpdateVariant(item, variant);
        }

        void OnLibraryChanged()
        {
            RefreshContent(false);
            UpdateFavoriteButton();
        }

        void SelectCollection(string id)
        {
            collection = id;
            RefreshContent(true);
        }

        void SetSortMode(SortMode mode)
        {
            sortMode = mode;
            RefreshContent(true);
        }

        void SetDetailsVisible(bool visible)
        {
            hideDetails = !visible;
            if (contentSplit == null)
                return;

            // Pane minimums don't propagate up, so the outer split has to reserve room for both
            contentSplit.style.minWidth = visible ? GridMinWidth + DetailsMinWidth : GridMinWidth;
            if (visible) { contentSplit.UnCollapse(); }
            else { contentSplit.CollapseChild(1); }
        }
        #endregion

        #region Selection and Details
        void SelectItem(CatalogItem item, bool scrollTo = false)
        {
            selectedItemId = item?.Name;
            foreach (KeyValuePair<CatalogItem, Card> pair in cards)
                pair.Value.Root.EnableInClassList("catalog-card--selected", pair.Key == item);

            ShowDetails(item);
            if (item == null)
                return;

            CatalogPreviews.Request(item, true);
            if (scrollTo && cards.TryGetValue(item, out Card card) && card.Root.panel != null)
            {
                // Wait for layout when the window was just built
                card.Root.schedule.Execute(() => gridScroll.ScrollTo(card.Root));
            }
        }

        void ShowDetails(CatalogItem item)
        {
            if (detailsContent == null)
                return;

            detailsItem = item;
            detailsEmpty.style.display = item == null ? DisplayStyle.Flex : DisplayStyle.None;
            detailsContent.style.display = item == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (item == null)
                return;

            detailsTitle.text = item.Name;
            detailsCategory.text = item.Category.Name;
            detailsPrefab.tooltip = item.AssetPath + "\nClick to show it in the Project window.";
            detailsDescription.text = item.Description;
            detailsPlaceholder.image = item.Icon;

            UpdateDetailsPreview();
            UpdateVariants(item);
            UpdateFavoriteButton();
            UpdateTarget();
        }

        void UpdateDetailsPreview()
        {
            if (detailsItem == null)
                return;

            Texture2D texture = CatalogPreviews.Get(detailsItem);
            detailsImage.image = texture;
            detailsImage.style.display = texture != null ? DisplayStyle.Flex : DisplayStyle.None;
            detailsPlaceholder.style.display = texture != null ? DisplayStyle.None : DisplayStyle.Flex;
            detailsPreview.style.backgroundColor = CatalogPreviews.Background;
            detailsPreview.tooltip = CatalogPreviews.HasFailed(detailsItem) ? "The preview couldn't be rendered. See the Console for details." : null;
        }

        void UpdateVariants(CatalogItem item)
        {
            variants.Clear();
            variantsContainer.Clear();

            string family = GetFamily(item);
            var familyItems = Catalog.Items.Where(other => GetFamily(other) == family).ToList();
            bool show = familyItems.Count > 1;
            variantsHeading.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            variantsContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;

            foreach (CatalogItem other in familyItems)
            {
                var variant = new VisualElement { tooltip = other.Name };
                variant.AddToClassList("catalog-variant");
                variant.EnableInClassList("catalog-variant--current", other == item);
                variant.RegisterCallback<ClickEvent>(_ => SelectItem(other, true));

                var image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("catalog-variant__image");
                variant.Add(image);

                variantsContainer.Add(variant);
                variants[other] = variant;
                UpdateVariant(other, variant);
                CatalogPreviews.Request(other);
            }
        }

        static void UpdateVariant(CatalogItem item, VisualElement variant)
        {
            var image = variant.Q<Image>();
            Texture2D texture = CatalogPreviews.Get(item);
            image.image = texture != null ? texture : item.Icon;
            image.EnableInClassList("catalog-variant__image--icon", texture == null);
            variant.style.backgroundColor = CatalogPreviews.Background;
        }

        // "Timer (Radial)" and "Timer" share the "Timer" family
        static string GetFamily(CatalogItem item)
        {
            int index = item.Name.IndexOf(" (", StringComparison.Ordinal);
            return index > 0 ? item.Name[..index] : item.Name;
        }

        void UpdateFavoriteButton()
        {
            if (favoriteButton == null || detailsItem == null)
                return;

            bool isFavorite = IsFavorite(detailsItem);
            favoriteButton.iconImage = Background.FromTexture2D(isFavorite ? FavoriteOnIcon : FavoriteOffIcon);
            favoriteButton.tooltip = isFavorite ? "Remove from Favorites" : "Add to Favorites";
        }

        void OnPrefabStageChanged(PrefabStage stage) => UpdateTarget();

        void UpdateTarget()
        {
            if (detailsTarget == null)
                return;

            Transform parent = ResolveParent(CurrentPlacement, false, out int siblingIndex);
            string message;
            if (parent == null) { message = "Creates a new Canvas to hold it."; }
            else if (siblingIndex >= 0) { message = $"Creates it after '{Selection.activeGameObject.name}' in '{parent.name}'."; }
            else { message = $"Creates it inside '{parent.name}'."; }

            EvoEditorUITK.SetCallout(detailsTarget, message);
        }

        internal static Transform ResolveParent(Placement placement, bool createCanvas, out int siblingIndex)
        {
            siblingIndex = -1;
            GameObject selected = Selection.activeGameObject;
            bool hasSelection = selected != null && !EditorUtility.IsPersistent(selected);

            switch (placement)
            {
                // Root objects, such as the Canvas itself, have no siblings that could hold UI
                case Placement.Sibling when hasSelection && selected.transform.parent != null:
                    siblingIndex = selected.transform.GetSiblingIndex() + 1;
                    return selected.transform.parent;

                case Placement.CanvasRoot:
                    var canvas = hasSelection ? selected.GetComponentInParent<Canvas>() : null;
                    return canvas != null ? canvas.rootCanvas.transform : CreateMenu.GetRootParent(createCanvas);

                default:
                    return CreateMenu.GetDefaultParent(createCanvas);
            }
        }
        #endregion

        #region Actions
        void CreateItem(CatalogItem item, Placement itemPlacement)
        {
            if (item == null)
                return;

            Transform parent = ResolveParent(itemPlacement, true, out int siblingIndex);
            item.Create(parent, siblingIndex);
        }

        void PopulateItemMenu(DropdownMenu menu, CatalogItem item)
        {
            SelectItem(item);
            menu.AppendAction("Create as Sibling of Selection", _ => CreateItem(item, Placement.Sibling));
            menu.AppendAction("Create as Child of Selection", _ => CreateItem(item, Placement.Child));
            menu.AppendAction("Create at Canvas Root", _ => CreateItem(item, Placement.CanvasRoot));
            menu.AppendSeparator();
            menu.AppendAction(IsFavorite(item) ? "Remove from Favorites" : "Add to Favorites", _ => ToggleFavorite(item));
            menu.AppendAction("Show Prefab", _ => ShowPrefab(item));
            menu.AppendAction("Open Documentation", _ => OpenDocumentation(item));
            menu.AppendSeparator();
            menu.AppendAction("Refresh Preview", _ => CatalogPreviews.Refresh(item));
        }

        static void ShowPrefab(CatalogItem item)
        {
            if (item != null && item.Prefab != null)
                EditorGUIUtility.PingObject(item.Prefab);
        }

        static void OpenDocumentation(CatalogItem item)
        {
            if (item != null)
                Application.OpenURL(item.DocumentationUrl);
        }

        static bool IsFavorite(CatalogItem item) => item != null && ReadList(FavoritesPref).Contains(item.Name);

        static void ToggleFavorite(CatalogItem item)
        {
            if (item == null)
                return;

            List<string> favorites = ReadList(FavoritesPref);
            if (!favorites.Remove(item.Name))
                favorites.Add(item.Name);

            WriteList(FavoritesPref, favorites);
            LibraryChanged?.Invoke();
        }

        static List<string> ReadList(string key)
        {
            // Older entries stored prefab paths; keep only the name
            return EditorPrefs.GetString(key, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Path.GetFileName).Distinct().ToList();
        }

        static void WriteList(string key, List<string> values) => EditorPrefs.SetString(key, string.Join("\n", values));
        #endregion

        #region Keyboard
        void OnKeyDown(KeyDownEvent evt)
        {
            if (EvoEditorUITK.HandleSearchKeys(evt, searchField, gridScroll, SelectFirstIfNone))
                return;

            var target = evt.target as VisualElement;
            bool inSearch = target != null && searchField.Contains(target);
            bool inGrid = target != null && gridScroll.Contains(target);
            if (!inSearch && !inGrid)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CatalogItem item = GetSelectedVisibleItem() ?? visibleItems.FirstOrDefault();
                    if (item != null)
                    {
                        SelectItem(item, true);
                        CreateItem(item, CurrentPlacement);
                    }

                    evt.StopPropagation();
                    break;

                case KeyCode.LeftArrow when inGrid: MoveSelection(-1, 0); evt.StopPropagation(); break;
                case KeyCode.RightArrow when inGrid: MoveSelection(1, 0); evt.StopPropagation(); break;
                case KeyCode.UpArrow when inGrid: MoveSelection(0, -1); evt.StopPropagation(); break;
                case KeyCode.DownArrow when inGrid: MoveSelection(0, 1); evt.StopPropagation(); break;
            }
        }

        // Arrow keys move the card selection; don't let them also move keyboard focus
        void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (evt.target is VisualElement target && gridScroll.Contains(target))
            {
                evt.StopPropagation();
                rootVisualElement.focusController?.IgnoreEvent(evt);
            }
        }

        CatalogItem GetSelectedVisibleItem()
        {
            CatalogItem selected = Catalog.Find(selectedItemId);
            return selected != null && visibleItems.Contains(selected) ? selected : null;
        }

        void SelectFirstIfNone()
        {
            if (GetSelectedVisibleItem() == null && visibleItems.Count > 0)
                SelectItem(visibleItems[0], true);
        }

        void MoveSelection(int horizontal, int vertical)
        {
            if (visibleItems.Count == 0)
                return;

            CatalogItem current = GetSelectedVisibleItem();
            if (current == null)
            {
                SelectItem(visibleItems[0], true);
                return;
            }

            CatalogItem next = null;
            if (vertical == 0)
            {
                int index = Mathf.Clamp(visibleItems.IndexOf(current) + horizontal, 0, visibleItems.Count - 1);
                next = visibleItems[index];
            }
            else
            {
                // Closest card in the nearest row above or below
                Rect from = cards[current].Root.worldBound;
                float bestScore = float.MaxValue;
                foreach (CatalogItem item in visibleItems)
                {
                    Rect rect = cards[item].Root.worldBound;
                    float distance = (rect.center.y - from.center.y) * vertical;
                    if (distance < from.height * 0.5f)
                        continue;

                    float score = distance * 10 + Mathf.Abs(rect.center.x - from.center.x);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        next = item;
                    }
                }
            }

            if (next != null)
                SelectItem(next, true);
        }
        #endregion
    }
}