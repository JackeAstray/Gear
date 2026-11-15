using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using Gear.Utilities.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// UI 展厅主窗口：分类页 + 搜索 + 分页网格 + 卡片放大入口。
    /// <para>
    /// 数据来自 <see cref="ShowcaseCatalog"/>（默认 Resources/UI/Showcase/ShowcaseCatalog），
    /// 卡片来自目录引用的独立预制体。窗口层级直接维护在 ShowcaseUIPlane.prefab，
    /// 本文件只负责查引用、分页和刷新数据，不再在运行时生成 UI。
    /// </para>
    /// <para>
    /// 分页而不是一次实例化 150 张卡：每页 4×2=8 张，避免 4500+ ImageEx 同时参与布局/合批。
    /// 后续要换虚拟化网格时，只替换 <see cref="RefreshGrid"/> 的实例化部分即可。
    /// </para>
    /// </summary>
    [UIWindow("ShowcaseUIPlane", Layer = PanelType.MainUI, Static = true)]
    public sealed partial class ShowcaseUIPlane : UIController
    {
        private const int Columns = 4;
        private const int Rows = 2;
        private const int PageSize = Columns * Rows;
        private const float CardWidth = 400f;
        private const float CardHeight = 340f;
        private const float CardSpacing = 20f;
        private const float HeaderHeight = 120f;
        private const float TabsHeight = 64f;
        private const float FooterHeight = 56f;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private ShowcaseCatalog catalog;
        private RectTransform gridRoot;
        private GearAnimatedContainer gridEntrance;
        private TMP_InputField searchField;
        private TMP_Text pageLabel;
        private TMP_Text countLabel;
        private Button prevButton;
        private Button nextButton;
        private GameObject emptyState;
        private TMP_Text emptyLabel;
        private Button themeButton;
        private TMP_Text themeLabel;
        private Button motionButton;
        private TMP_Text motionLabel;
        private bool lightTheme;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<ImageEx> tabBackgrounds = new List<ImageEx>();
        private readonly List<GameObject> cardInstances = new List<GameObject>();
        private readonly List<ShowcaseEntry> filtered = new List<ShowcaseEntry>();

        private string currentPageId;
        private int pageIndex;
        private bool built;

        public override void OnInit()
        {
            base.OnInit();
            if (!BindBakedLayout()) return;
            LoadCatalog();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (catalog == null) LoadCatalog();
            if (catalog != null && string.IsNullOrEmpty(currentPageId))
                SelectPage(catalog.Pages.Count > 0 ? catalog.Pages[0].id : null);
            else
                RefreshGrid();
        }

        public override void OnClose()
        {
            base.OnClose();
            ClearGrid();
        }

        private void LoadCatalog()
        {
            catalog = ShowcaseCatalog.LoadDefault();
            if (catalog == null)
            {
                ShowCatalogMissing();
                return;
            }

            if (catalog.Pages.Count == 0)
            {
                ShowCatalogMissing();
                return;
            }

            BuildTabs();
            SelectPage(catalog.Pages[0].id);
        }

        private void BuildWindow()
        {
            if (built) return;
            built = true;

            ImageEx background = ShowcaseUIFactory.CreatePanel("Background", transform, ShowcaseUIFactory.PageBackground, 0f, true);
            ShowcaseUIFactory.Stretch(background.rectTransform);
            BindTheme(background, "Background");

            BuildHeader();
            BuildTabsHost();
            BuildGrid();
            BuildFooter();
            BuildEmptyState();
        }

        private void BuildHeader()
        {
            ImageEx header = ShowcaseUIFactory.CreatePanel("Header", transform, ShowcaseUIFactory.SoftPanel, 0f);
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.offsetMin = new Vector2(0f, -HeaderHeight);
            headerRect.offsetMax = Vector2.zero;
            BindTheme(header, "Surface");

            TextMeshProUGUI title = ShowcaseUIFactory.CreateText("Title", headerRect, "Gear UI 展厅", 32f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(title.rectTransform, 48f, 22f, 560f, 40f);
            BindTheme(title, "Text");

            TextMeshProUGUI subtitle = ShowcaseUIFactory.CreateText("Subtitle", headerRect,
                "EVO 对照 · 形状 / 材质 / 效果 / 控件 / 文本 / 数据 / 动效 / 覆盖层", 18f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(subtitle.rectTransform, 48f, 68f, 900f, 28f);
            BindTheme(subtitle, "TextSecondary");

            CloseButton(headerRect);

            searchField = ShowcaseUIFactory.CreateInputField("Search", headerRect, "搜索标题 / 说明 / 标签", OnSearchChanged);
            ShowcaseUIFactory.TopRight(searchField.GetComponent<RectTransform>(), 96f, 28f, 420f, 44f);
            BindTheme(searchField.targetGraphic, "SurfaceRaised");
            BindTheme(searchField.textComponent, "Text");
            BindTheme(searchField.placeholder, "TextSecondary");

            themeButton = ShowcaseUIFactory.CreateButton("Theme", headerRect, "Light",
                ShowcaseUIFactory.SoftPanelColor, 8f, ToggleTheme, 14f);
            ShowcaseUIFactory.TopLeft(themeButton.GetComponent<RectTransform>(), 1404f, 76f, 110f, 30f);
            themeLabel = themeButton.GetComponentInChildren<TMP_Text>();
            BindTheme(themeButton.targetGraphic, "SurfaceRaised");
            BindTheme(themeLabel, "Text");
            lightTheme = GearThemeRegistry.Current == GearThemeRegistry.LoadLight();
            UpdateThemeLabel();

            Button audioSample = ShowcaseUIFactory.CreateButton("SampleAudio", headerRect, "样例 · 混音台",
                ShowcaseUIFactory.SoftPanelColor, 8f, () => OpenSample<AudioMixerSampleUIPlane>(), 14f);
            ShowcaseUIFactory.TopLeft(audioSample.GetComponent<RectTransform>(), 960f, 76f, 150f, 30f);
            BindTheme(audioSample.targetGraphic, "SurfaceRaised");
            BindTheme(audioSample.GetComponentInChildren<TMP_Text>(), "Text");

            Button partySample = ShowcaseUIFactory.CreateButton("SampleParty", headerRect, "样例 · 派对",
                ShowcaseUIFactory.SoftPanelColor, 8f, () => OpenSample<PartyLobbySampleUIPlane>(), 14f);
            ShowcaseUIFactory.TopLeft(partySample.GetComponent<RectTransform>(), 1118f, 76f, 130f, 30f);
            BindTheme(partySample.targetGraphic, "SurfaceRaised");
            BindTheme(partySample.GetComponentInChildren<TMP_Text>(), "Text");

            Button inventorySample = ShowcaseUIFactory.CreateButton("SampleInventory", headerRect, "样例 · 背包",
                ShowcaseUIFactory.SoftPanelColor, 8f, () => OpenSample<InventorySampleUIPlane>(), 14f);
            ShowcaseUIFactory.TopLeft(inventorySample.GetComponent<RectTransform>(), 1256f, 76f, 130f, 30f);
            BindTheme(inventorySample.targetGraphic, "SurfaceRaised");
            BindTheme(inventorySample.GetComponentInChildren<TMP_Text>(), "Text");
        }

        private void ToggleTheme()
        {
            ImageExThemeAsset light = GearThemeRegistry.LoadLight();
            ImageExThemeAsset dark = GearThemeRegistry.LoadDark();
            if (light == null || dark == null)
            {
                GearToast.Show("主题资产缺失：先运行 Gear/UI 展厅/生成窗口预制体", "UI 展厅", GearToastKind.Warning);
                return;
            }

            ImageExThemeAsset next = GearThemeRegistry.Current == light ? dark : light;
            lightTheme = next == light;
            GearThemeRegistry.Apply(next, 0.25f);
            RefreshTabColors(next);
            RefreshButtonTints(next);
            UpdateThemeLabel();
        }

        private void UpdateThemeLabel()
        {
            if (themeLabel != null) themeLabel.text = lightTheme ? "Dark" : "Light";
        }

        private void ToggleMotion()
        {
            GearMotionSettings.Toggle();
            RefreshMotionLabel();
            if (GearMotionSettings.Enabled && gridEntrance != null) gridEntrance.Play();
        }

        private void RefreshMotionLabel()
        {
            if (motionLabel != null) motionLabel.text = GearMotionSettings.Enabled ? "Motion On" : "Motion Off";
        }

        private async void OpenSample<T>() where T : UIController
        {
            try
            {
                await UIRouter.PushAsync<T>();
            }
            catch (Exception ex)
            {
                Log.Error("[Showcase] 打开整屏样例失败: {0}", ex.Message);
            }
        }

        private void CloseButton(RectTransform parent)
        {
            Button close = ShowcaseUIFactory.CreateButton("Close", parent, "×", new Color(0.18f, 0.2f, 0.25f, 1f), 10f,
                CloseWindow, 28f);
            ShowcaseUIFactory.TopRight(close.GetComponent<RectTransform>(), 28f, 28f, 48f, 48f);
            BindTheme(close.targetGraphic, "SurfaceRaised");
            BindTheme(close.GetComponentInChildren<TMP_Text>(), "Text");
        }

        private void BuildTabsHost()
        {
            ImageEx host = ShowcaseUIFactory.CreatePanel("Tabs", transform, ShowcaseUIFactory.PageBackground, 0f);
            RectTransform rect = host.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(HeaderHeight + TabsHeight));
            rect.offsetMax = new Vector2(0f, -HeaderHeight);
            BindTheme(host, "Background");
        }

        private void BuildGrid()
        {
            GameObject gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.layer = ShowcaseUIFactory.UILayer;
            gridRoot = (RectTransform)gridObject.transform;
            gridRoot.SetParent(transform, false);
            gridRoot.anchorMin = new Vector2(0f, 0f);
            gridRoot.anchorMax = new Vector2(1f, 1f);
            gridRoot.pivot = new Vector2(0.5f, 0.5f);
            gridRoot.offsetMin = new Vector2(0f, FooterHeight);
            gridRoot.offsetMax = new Vector2(0f, -(HeaderHeight + TabsHeight));

            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CardWidth, CardHeight);
            grid.spacing = new Vector2(CardSpacing, CardSpacing);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
        }

        private void BuildFooter()
        {
            ImageEx footer = ShowcaseUIFactory.CreatePanel("Footer", transform, ShowcaseUIFactory.SoftPanel, 0f);
            RectTransform rect = footer.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, FooterHeight);
            BindTheme(footer, "Surface");

            prevButton = ShowcaseUIFactory.CreateButton("Prev", rect, "上一页", new Color(0.16f, 0.18f, 0.23f, 1f),
                8f, () => ChangePage(-1), 18f);
            BindTheme(prevButton.targetGraphic, "SurfaceRaised");
            BindTheme(prevButton.GetComponentInChildren<TMP_Text>(), "Text");
            RectTransform prevRect = prevButton.GetComponent<RectTransform>();
            prevRect.anchorMin = new Vector2(0f, 0.5f);
            prevRect.anchorMax = new Vector2(0f, 0.5f);
            prevRect.pivot = new Vector2(0f, 0.5f);
            prevRect.anchoredPosition = new Vector2(48f, 0f);
            prevRect.sizeDelta = new Vector2(120f, 40f);

            nextButton = ShowcaseUIFactory.CreateButton("Next", rect, "下一页", new Color(0.16f, 0.18f, 0.23f, 1f),
                8f, () => ChangePage(1), 18f);
            BindTheme(nextButton.targetGraphic, "SurfaceRaised");
            BindTheme(nextButton.GetComponentInChildren<TMP_Text>(), "Text");
            RectTransform nextRect = nextButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(1f, 0.5f);
            nextRect.anchorMax = new Vector2(1f, 0.5f);
            nextRect.pivot = new Vector2(1f, 0.5f);
            nextRect.anchoredPosition = new Vector2(-48f, 0f);
            nextRect.sizeDelta = new Vector2(120f, 40f);

            pageLabel = ShowcaseUIFactory.CreateText("PageLabel", rect, "1 / 1", 20f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.Center);
            ShowcaseUIFactory.Stretch(pageLabel.rectTransform, 180f, 180f, 6f, 22f);
            BindTheme(pageLabel, "Text");

            countLabel = ShowcaseUIFactory.CreateText("CountLabel", rect, "0 张卡", 15f,
                ShowcaseUIFactory.MutedText, TextAlignmentOptions.Center);
            ShowcaseUIFactory.Stretch(countLabel.rectTransform, 180f, 180f, 32f, 4f);
            BindTheme(countLabel, "TextSecondary");
        }

        private void BuildEmptyState()
        {
            emptyLabel = ShowcaseUIFactory.CreateText("EmptyState", transform,
                "没有找到匹配的卡片。换个关键词，或切回分类页。", 24f,
                ShowcaseUIFactory.MutedText, TextAlignmentOptions.Center);
            emptyState = emptyLabel.gameObject;
            ShowcaseUIFactory.Stretch(emptyLabel.rectTransform, 0f, 0f, HeaderHeight + TabsHeight, FooterHeight);
            BindTheme(emptyLabel, "TextSecondary");
            emptyState.SetActive(false);
        }

        private void BuildTabs()
        {
            RectTransform host = transform.Find("Tabs") as RectTransform;
            if (host == null) return;

            int count = catalog.Pages.Count;
            if (count > 0 && host.childCount == count)
            {
                BindExistingTabs(host, count);
                return;
            }

            ClearTabs();
            float width = 190f;
            float gap = 8f;
            float totalWidth = count * width + (count - 1) * gap;
            float startX = (ReferenceWidth - totalWidth) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                ShowcasePage page = catalog.Pages[i];
                int index = i;
                Button button = ShowcaseUIFactory.CreateButton("Tab " + page.displayName, host, page.displayName,
                    Color.white, 8f, () => SelectPage(catalog.Pages[index].id), 18f);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * (width + gap), 0f);
                rect.sizeDelta = new Vector2(width, 44f);
                tabButtons.Add(button);
                tabBackgrounds.Add(button.targetGraphic as ImageEx);
            }

            if (tabButtons.Count > 0) firstSelected = tabButtons[0].gameObject;
        }

        private void BindExistingTabs(RectTransform host, int count)
        {
            tabButtons.Clear();
            tabBackgrounds.Clear();

            for (int i = 0; i < count; i++)
            {
                Button button = host.GetChild(i).GetComponent<Button>();
                if (button == null) continue;

                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPage(catalog.Pages[index].id));
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = catalog.Pages[i].displayName;
                tabButtons.Add(button);
                tabBackgrounds.Add(button.targetGraphic as ImageEx);
            }

            if (tabButtons.Count > 0) firstSelected = tabButtons[0].gameObject;
        }

        private void ClearTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] != null) Destroy(tabButtons[i].gameObject);
            }

            tabButtons.Clear();
            tabBackgrounds.Clear();
        }

        private void SelectPage(string pageId)
        {
            currentPageId = pageId;
            pageIndex = 0;
            RefreshTabColors(GearThemeRegistry.Current);
            RefreshGrid();
        }

        private void RefreshTabColors(ImageExThemeAsset theme)
        {
            Color accent = theme != null && theme.TryGetColor("Accent", out Color a)
                ? a
                : ShowcaseUIFactory.AccentColor;
            Color surface = theme != null && theme.TryGetColor("SurfaceRaised", out Color s)
                ? s
                : ShowcaseUIFactory.SoftPanelColor;
            Color text = theme != null && theme.TryGetColor("Text", out Color t)
                ? t
                : ShowcaseUIFactory.PrimaryText;

            for (int i = 0; i < tabButtons.Count; i++)
            {
                Button button = tabButtons[i];
                if (button == null) continue;

                bool selected = i < catalog.Pages.Count && catalog.Pages[i].id == currentPageId;
                Color normal = selected ? accent : surface;
                Color highlighted = Color.Lerp(normal, Color.white, 0.12f);
                Color pressed = Color.Lerp(normal, Color.black, 0.18f);
                Color disabled = new Color(0.5f, 0.5f, 0.5f, 0.45f);

                if (!ConfigureStateTint(button, normal, highlighted, pressed, disabled))
                {
                    ColorBlock block = button.colors;
                    block.normalColor = normal;
                    block.selectedColor = normal;
                    block.highlightedColor = highlighted;
                    block.pressedColor = pressed;
                    button.colors = block;
                }

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = selected ? Color.white : text;
            }
        }

        private static void BindTheme(Graphic graphic, string tokenId)
        {
            if (graphic == null || string.IsNullOrEmpty(tokenId)) return;
            GearThemeColorTarget target = graphic.GetComponent<GearThemeColorTarget>();
            if (target == null) target = graphic.gameObject.AddComponent<GearThemeColorTarget>();
            target.Configure(tokenId, graphic);
        }

        private void ChangePage(int delta)
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)PageSize));
            int next = Mathf.Clamp(pageIndex + delta, 0, pageCount - 1);
            if (next == pageIndex) return;
            pageIndex = next;
            RefreshGrid();
        }

        private void OnSearchChanged(string _)
        {
            pageIndex = 0;
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (gridRoot == null) return;

            filtered.Clear();
            string query = searchField != null ? searchField.text : null;

            if (!string.IsNullOrWhiteSpace(query))
            {
                for (int i = 0; i < catalog.Pages.Count; i++)
                {
                    ShowcasePage page = catalog.Pages[i];
                    for (int j = 0; j < page.entries.Count; j++)
                    {
                        ShowcaseEntry entry = page.entries[j];
                        if (ShowcaseCatalog.Matches(entry, query)) filtered.Add(entry);
                    }
                }
            }
            else
            {
                ShowcasePage page = catalog.FindPage(currentPageId) ?? (catalog.Pages.Count > 0 ? catalog.Pages[0] : null);
                if (page != null) filtered.AddRange(page.entries);
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)PageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);

            ClearGrid();

            int start = pageIndex * PageSize;
            int end = Mathf.Min(filtered.Count, start + PageSize);
            for (int i = start; i < end; i++)
            {
                CreateCard(filtered[i], i - start);
            }

            bool empty = filtered.Count == 0;
            if (emptyState != null) emptyState.SetActive(empty);
            if (emptyLabel != null && empty)
                emptyLabel.text = "没有找到匹配的卡片。换个关键词，或切回分类页。";
            if (pageLabel != null) pageLabel.text = (pageIndex + 1) + " / " + pageCount;
            if (countLabel != null)
                countLabel.text = string.IsNullOrWhiteSpace(query)
                    ? filtered.Count + " 张卡"
                    : "搜索结果 · " + filtered.Count + " 张卡";

            if (prevButton != null) prevButton.interactable = pageIndex > 0;
            if (nextButton != null) nextButton.interactable = pageIndex + 1 < pageCount;

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRoot);
            if (gridEntrance != null) gridEntrance.Play();
        }

        private void ClearGrid()
        {
            for (int i = 0; i < cardInstances.Count; i++)
            {
                if (cardInstances[i] != null)
                {
                    cardInstances[i].SetActive(false);
                    Destroy(cardInstances[i]);
                }
            }

            cardInstances.Clear();

            if (gridRoot == null) return;
            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = gridRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private async void OpenDetail(ShowcaseEntry entry)
        {
            if (entry == null || entry.cardPrefab == null)
            {
                GearToast.Show("这张卡还没有生成预制体", entry != null ? entry.title : "展厅",
                    GearToastKind.Warning);
                return;
            }

            try
            {
                await UIRouter.PushAsync<ShowcaseDetailUIPlane, ShowcaseDetailArgs>(new ShowcaseDetailArgs(
                    entry.title, entry.description, entry.cardPrefab, entry.pageId));
            }
            catch (Exception ex)
            {
                Log.Error("[Showcase] 打开详情失败: {0}", ex.Message);
            }
        }

        private void CloseWindow()
        {
            UISystem.GetOrInstance().CloseWindow(nameof(ShowcaseUIPlane));
        }

        private void ShowCatalogMissing()
        {
            if (emptyState != null) emptyState.SetActive(true);
            if (emptyLabel != null)
                emptyLabel.text = "目录缺失：先运行 Gear/UI 展厅/1. 重建卡预制体与目录";
            if (pageLabel != null) pageLabel.text = "目录缺失";
            if (countLabel != null) countLabel.text = "先运行 Gear/UI 展厅/1. 重建卡预制体与目录";
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
        }
    }
}
