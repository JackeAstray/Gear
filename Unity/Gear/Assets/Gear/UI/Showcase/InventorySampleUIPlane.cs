using System;
using System.Collections.Generic;
using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using Gear.Utilities.UI.Interaction;
using Gear.Utilities.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 整屏样例 3：Inventory。
    /// 6×4 物品格 + 选中详情 + 悬浮 Tooltip + 右键 ContextMenu + 排序。
    /// </summary>
    [UIWindow("InventorySampleUIPlane", Layer = PanelType.NormalUI, Modal = true)]
    public sealed class InventorySampleUIPlane : ShowcaseSampleUIPlaneBase
    {
        private const int Columns = 6;
        private const int Rows = 4;
        private const float CellSize = 170f;
        private const float CellGap = 12f;
        private const float GridX = 0f;
        private const float GridY = 56f;
        private const float GridWidth = Columns * CellSize + (Columns - 1) * CellGap;
        private const float GridHeight = Rows * CellSize + (Rows - 1) * CellGap;

        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        private static readonly Color[] RarityColors =
        {
            new Color(0.62f, 0.66f, 0.74f, 1f),
            new Color(0.30f, 0.62f, 1f, 1f),
            new Color(0.69f, 0.42f, 1f, 1f),
            new Color(1f, 0.72f, 0.28f, 1f),
        };

        private sealed class ItemData
        {
            public string Name;
            public string Description;
            public string Stats;
            public int Count;
            public int Rarity;
            public DrawShape Shape;
        }

        private readonly List<ItemData> items = new List<ItemData>();
        private readonly List<GameObject> cells = new List<GameObject>();
        private RectTransform gridRoot;
        private GearAnimatedContainer entrance;
        private ImageEx detailIcon;
        private ImageEx detailAccent;
        private TextMeshProUGUI detailName;
        private TextMeshProUGUI detailRarity;
        private TextMeshProUGUI detailCount;
        private TextMeshProUGUI detailDescription;
        private TextMeshProUGUI detailStats;
        private int selectedIndex;

        protected override string GetTitle() => "Inventory";

        protected override string GetSubtitle() => "整屏样例 · 6×4 物品格 / Tooltip / ContextMenu / 排序";

        protected override void BuildSample()
        {
            SeedItems();
            if (TryBindBaked()) return;

            BuildToolbar();
            BuildGrid();
            BuildDetail();
            SelectItem(0);
        }

        private void SeedItems()
        {
            items.Clear();
            Add("Iron Sword", "A reliable blade for the first dive.", "ATK +12 · SPD +2", 1, 0, DrawShape.Triangle);
            Add("Frost Blade", "Chills on hit; slows the target.", "ATK +28 · ICE +8", 1, 2, DrawShape.Triangle);
            Add("Sunforged Edge", "Legendary blade that burns on impact.", "ATK +64 · FIRE +20", 1, 3, DrawShape.Triangle);
            Add("Oak Shield", "Heavy but dependable.", "DEF +14", 1, 0, DrawShape.Hexagon);
            Add("Aegis Core", "Projects a short bubble on block.", "DEF +30 · SHIELD +6", 1, 2, DrawShape.Hexagon);
            Add("Titan Plate", "Legendary armor from the deep forge.", "DEF +72 · HP +40", 1, 3, DrawShape.Hexagon);
            Add("Health Flask", "Restores 40 HP.", "HEAL 40", 12, 1, DrawShape.Circle);
            Add("Mana Flask", "Restores 30 MP.", "MP 30", 8, 1, DrawShape.Circle);
            Add("Antidote", "Clears poison.", "CLEANSE", 5, 0, DrawShape.Circle);
            Add("Bomb", "Throws a small explosive.", "DMG 25 · AOE", 6, 1, DrawShape.Circle);
            Add("Iron Ore", "Common crafting material.", "CRAFT", 42, 0, DrawShape.Squircle);
            Add("Crystal Shard", "Refined magic crystal.", "CRAFT", 17, 2, DrawShape.Squircle);
            Add("Ember Dust", "Legendary crafting material.", "CRAFT", 3, 3, DrawShape.Squircle);
            Add("Leather Strip", "Basic binding material.", "CRAFT", 28, 0, DrawShape.Squircle);
            Add("Moon Sigil", "Unlocks the moon gate.", "KEY ITEM", 1, 2, DrawShape.Heart);
            Add("Compass", "Points to the nearest shrine.", "UTILITY", 1, 1, DrawShape.Circle);
            Add("Grappling Hook", "Reach high ledges.", "UTILITY", 1, 1, DrawShape.Triangle);
            Add("Smoke Bomb", "Breaks line of sight.", "UTILITY", 4, 1, DrawShape.Circle);
            Add("Ration", "Restores a little stamina.", "STAMINA 15", 9, 0, DrawShape.Circle);
            Add("Lucky Coin", "Slightly improves drops.", "LUCK +1", 2, 2, DrawShape.Squircle);
            Add("Ancient Key", "Opens the vault door.", "KEY ITEM", 1, 3, DrawShape.Heart);
            Add("Torch", "Lights dark corridors.", "UTILITY", 3, 0, DrawShape.Circle);
            Add("Repair Kit", "Restores armor durability.", "REPAIR", 4, 1, DrawShape.Hexagon);
            Add("Star Fragment", "A shard of a fallen star.", "CRAFT", 7, 3, DrawShape.Squircle);
        }

        private void Add(string name, string description, string stats, int count, int rarity, DrawShape shape)
        {
            items.Add(new ItemData
            {
                Name = name,
                Description = description,
                Stats = stats,
                Count = count,
                Rarity = Mathf.Clamp(rarity, 0, RarityNames.Length - 1),
                Shape = shape,
            });
        }

        private bool TryBindBaked()
        {
            Transform panel = transform.Find("Panel");
            Transform content = panel != null ? panel.Find("Content") : null;
            if (content == null) return false;

            gridRoot = content.Find("Grid") as RectTransform;
            Transform detail = content.Find("Detail");
            if (gridRoot == null || detail == null) return false;

            Button sort = content.Find("Sort")?.GetComponent<Button>();
            if (sort != null)
            {
                sort.onClick.RemoveAllListeners();
                sort.onClick.AddListener(SortItems);
            }

            Transform iconPanel = detail.Find("IconPanel");
            detailIcon = iconPanel != null ? iconPanel.Find("Icon")?.GetComponent<ImageEx>() : null;
            detailAccent = detail.Find("Accent")?.GetComponent<ImageEx>();
            detailName = detail.Find("Name")?.GetComponent<TextMeshProUGUI>();
            detailRarity = detail.Find("Rarity")?.GetComponent<TextMeshProUGUI>();
            detailCount = detail.Find("Count")?.GetComponent<TextMeshProUGUI>();
            detailDescription = detail.Find("Description")?.GetComponent<TextMeshProUGUI>();
            detailStats = detail.Find("Stats")?.GetComponent<TextMeshProUGUI>();
            if (detailIcon == null || detailAccent == null || detailName == null || detailRarity == null
                || detailCount == null || detailDescription == null || detailStats == null) return false;

            Button use = detail.Find("Use")?.GetComponent<Button>();
            Button equip = detail.Find("Equip")?.GetComponent<Button>();
            Button discard = detail.Find("Discard")?.GetComponent<Button>();
            if (use != null)
            {
                use.onClick.RemoveAllListeners();
                use.onClick.AddListener(UseSelected);
            }

            if (equip != null)
            {
                equip.onClick.RemoveAllListeners();
                equip.onClick.AddListener(EquipSelected);
            }

            if (discard != null)
            {
                discard.onClick.RemoveAllListeners();
                discard.onClick.AddListener(DiscardSelected);
            }

            cells.Clear();
            PopulateGrid();
            entrance = gridRoot.GetComponent<GearAnimatedContainer>();
            if (entrance == null) entrance = gridRoot.gameObject.AddComponent<GearAnimatedContainer>();
            entrance.FromScale = 0.94f;
            entrance.Stagger = 0.012f;
            entrance.Play();
            SelectItem(0);
            return true;
        }

        private void BuildToolbar()
        {
            TextMeshProUGUI capacity = ShowcaseUIFactory.CreateText("Capacity", Content, "24 / 40 slots", 20f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(capacity.rectTransform, 0f, 0f, 300f, 40f);

            TextMeshProUGUI hint = ShowcaseUIFactory.CreateText("ToolbarHint", Content,
                "左键选中 · 右键菜单 · 悬停 Tooltip", 15f, ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(hint.rectTransform, 320f, 0f, 500f, 40f);

            Button sort = ShowcaseUIFactory.CreateButton("Sort", Content, "Sort by rarity",
                new Color(0.16f, 0.18f, 0.23f, 1f), 8f, SortItems, 16f);
            ShowcaseUIFactory.TopLeft(sort.GetComponent<RectTransform>(), 940f, 2f, 140f, 36f);
        }

        private void BuildGrid()
        {
            gridRoot = ShowcaseUIFactory.CreateRect("Grid", Content);
            ShowcaseUIFactory.TopLeft(gridRoot, GridX, GridY, GridWidth, GridHeight);

            PopulateGrid();

            entrance = gridRoot.gameObject.AddComponent<GearAnimatedContainer>();
            entrance.FromScale = 0.94f;
            entrance.Stagger = 0.012f;
            entrance.Play();
        }

        private void PopulateGrid()
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] != null) Destroy(cells[i]);
            }

            cells.Clear();

            for (int i = 0; i < items.Count && i < Columns * Rows; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                BuildCell(items[i], i, col * (CellSize + CellGap), row * (CellSize + CellGap));
            }
        }

        private void BuildCell(ItemData item, int index, float x, float y)
        {
            GameObject prefab = Resources.Load<GameObject>("UI/Showcase/InventoryItemCell");
            if (prefab == null)
            {
                Debug.LogError("[Inventory] 缺少 Resources/UI/Showcase/InventoryItemCell.prefab");
                return;
            }

            GameObject instance = Instantiate(prefab, gridRoot);
            instance.name = item.Name;
            RectTransform rect = (RectTransform)instance.transform;
            ShowcaseUIFactory.TopLeft(rect, x, y, CellSize, CellSize);
            cells.Add(instance);

            Color rarityColor = RarityColors[item.Rarity];
            ImageEx cell = instance.GetComponent<ImageEx>();
            ImageEx icon = rect.Find("Icon")?.GetComponent<ImageEx>();
            TextMeshProUGUI name = rect.Find("Name")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI count = rect.Find("Count")?.GetComponent<TextMeshProUGUI>();
            ImageEx rarity = rect.Find("Rarity")?.GetComponent<ImageEx>();
            if (cell == null || icon == null || name == null || count == null || rarity == null)
            {
                Debug.LogError("[Inventory] InventoryItemCell.prefab 缺少 Icon/Name/Count/Rarity");
                Destroy(instance);
                return;
            }

            icon.DrawShape = item.Shape;
            icon.color = rarityColor;
            name.text = item.Name;
            count.text = "x" + item.Count;
            rarity.color = rarityColor;

            GearInteractiveTint tint = instance.GetComponent<GearInteractiveTint>();
            if (tint != null)
            {
                Color normal = ShowcaseUIFactory.SoftPanelColor;
                bool nearWhite = normal.r > 0.95f && normal.g > 0.95f && normal.b > 0.95f;
                tint.SetColors(normal,
                    nearWhite ? new Color(0.86f, 0.89f, 0.95f, normal.a) : Color.Lerp(normal, Color.white, 0.10f),
                    Color.Lerp(normal, Color.black, 0.15f),
                    ShowcaseUIFactory.AccentColor,
                    new Color(0.5f, 0.5f, 0.5f, 0.45f));
            }

            Button button = instance.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectItem(index));
            }

            GearTooltip.Attach(instance, item.Description, item.Name + " · " + RarityNames[item.Rarity], 0.25f);

            ShowcaseContextClick context = instance.GetComponent<ShowcaseContextClick>();
            if (context != null) context.RightClicked += _ => ShowItemMenu(item, rect);
        }

        private void BuildDetail()
        {
            ImageEx panel = ShowcaseUIFactory.CreatePanel("Detail", Content, ShowcaseUIFactory.SoftPanelColor, 16f, true);
            ShowcaseUIFactory.TopLeft(panel.rectTransform, 1120f, 0f, 536f, 772f);

            ImageEx iconPanel = ShowcaseUIFactory.CreatePanel("IconPanel", panel.transform,
                new Color(0.045f, 0.052f, 0.068f, 1f), 14f);
            ShowcaseUIFactory.TopLeft(iconPanel.rectTransform, 24f, 24f, 160f, 160f);

            detailIcon = ShowcaseUIFactory.CreateCircle("Icon", iconPanel.transform,
                RarityColors[0], 120f);
            ShowcaseUIFactory.TopLeft(detailIcon.rectTransform, 20f, 20f, 120f, 120f);

            detailAccent = ShowcaseUIFactory.CreatePanel("Accent", panel.transform, RarityColors[0], 2f);
            ShowcaseUIFactory.TopLeft(detailAccent.rectTransform, 200f, 24f, 4f, 96f);

            detailName = ShowcaseUIFactory.CreateText("Name", panel.transform, "", 26f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(detailName.rectTransform, 222f, 28f, 290f, 40f);

            detailRarity = ShowcaseUIFactory.CreateText("Rarity", panel.transform, "", 16f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(detailRarity.rectTransform, 222f, 72f, 290f, 24f);

            detailCount = ShowcaseUIFactory.CreateText("Count", panel.transform, "", 16f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(detailCount.rectTransform, 222f, 100f, 290f, 24f);

            ImageEx divider = ShowcaseUIFactory.CreatePanel("Divider", panel.transform,
                new Color(1f, 1f, 1f, 0.08f), 0f);
            ShowcaseUIFactory.TopLeft(divider.rectTransform, 24f, 210f, 488f, 1f);

            detailDescription = ShowcaseUIFactory.CreateText("Description", panel.transform, "", 17f,
                ShowcaseUIFactory.MutedText, TextAlignmentOptions.TopLeft);
            ShowcaseUIFactory.TopLeft(detailDescription.rectTransform, 24f, 230f, 488f, 96f);

            TextMeshProUGUI statsLabel = ShowcaseUIFactory.CreateText("StatsLabel", panel.transform, "STATS", 14f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(statsLabel.rectTransform, 24f, 350f, 120f, 22f);

            detailStats = ShowcaseUIFactory.CreateText("Stats", panel.transform, "", 18f,
                ShowcaseUIFactory.PrimaryText);
            ShowcaseUIFactory.TopLeft(detailStats.rectTransform, 24f, 378f, 488f, 60f);

            Button use = ShowcaseUIFactory.CreateButton("Use", panel.transform, "Use",
                ShowcaseUIFactory.AccentColor, 10f, UseSelected, 20f);
            ShowcaseUIFactory.TopLeft(use.GetComponent<RectTransform>(), 24f, 540f, 230f, 56f);

            Button equip = ShowcaseUIFactory.CreateButton("Equip", panel.transform, "Equip",
                new Color(0.16f, 0.18f, 0.23f, 1f), 10f, EquipSelected, 20f);
            ShowcaseUIFactory.TopLeft(equip.GetComponent<RectTransform>(), 278f, 540f, 234f, 56f);

            Button discard = ShowcaseUIFactory.CreateButton("Discard", panel.transform, "Discard",
                new Color(0.34f, 0.14f, 0.16f, 1f), 10f, DiscardSelected, 18f);
            ShowcaseUIFactory.TopLeft(discard.GetComponent<RectTransform>(), 24f, 614f, 488f, 44f);
        }

        private void SelectItem(int index)
        {
            if (items.Count == 0) return;
            selectedIndex = Mathf.Clamp(index, 0, items.Count - 1);
            ItemData item = items[selectedIndex];
            Color color = RarityColors[item.Rarity];

            detailIcon.DrawShape = item.Shape;
            detailIcon.color = color;
            detailAccent.color = color;
            detailName.text = item.Name;
            detailRarity.text = RarityNames[item.Rarity];
            detailRarity.color = color;
            detailCount.text = "Owned: " + item.Count;
            detailDescription.text = item.Description;
            detailStats.text = item.Stats;
        }

        private void SortItems()
        {
            items.Sort((a, b) =>
            {
                int rarity = b.Rarity.CompareTo(a.Rarity);
                return rarity != 0 ? rarity : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
            PopulateGrid();
            SelectItem(0);
            ShowToast("已按稀有度排序");
        }

        private void ShowItemMenu(ItemData item, RectTransform anchor)
        {
            List<GearContextMenuItem> menu = new List<GearContextMenuItem>
            {
                new GearContextMenuItem("Use", () => Use(item)),
                new GearContextMenuItem("Equip", () => Equip(item)),
                GearContextMenuItem.Separator(),
                new GearContextMenuItem("Discard", () => Discard(item), true, true),
            };
            GearContextMenu.Show(anchor, menu);
        }

        private void UseSelected()
        {
            if (items.Count > 0) Use(items[selectedIndex]);
        }

        private void EquipSelected()
        {
            if (items.Count > 0) Equip(items[selectedIndex]);
        }

        private void DiscardSelected()
        {
            if (items.Count > 0) Discard(items[selectedIndex]);
        }

        private void Use(ItemData item) => ShowToast("Used · " + item.Name);

        private void Equip(ItemData item) => ShowToast("Equipped · " + item.Name);

        private void Discard(ItemData item) => ShowToast("Discarded · " + item.Name);
    }
}
