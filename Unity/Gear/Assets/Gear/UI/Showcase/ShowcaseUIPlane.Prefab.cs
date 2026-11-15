using Gear.Common;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 展厅主窗口的 prefab 绑定：窗口层级由 ShowcaseUIPlane.prefab 提供，
    /// 这里只查找控件、补运行时事件；不再在 OnOpen 里生成界面。
    /// </summary>
    public sealed partial class ShowcaseUIPlane
    {
        private void CreateCard(ShowcaseEntry entry, int slot)
        {
            GameObject cellPrefab = Resources.Load<GameObject>("UI/Showcase/ShowcaseGridCell");
            if (cellPrefab == null)
            {
                Log.Error("[Showcase] 缺少 Resources/UI/Showcase/ShowcaseGridCell.prefab");
                return;
            }

            GameObject cellInstance = Instantiate(cellPrefab, gridRoot);
            RectTransform cell = (RectTransform)cellInstance.transform;
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0.5f, 0.5f);

            Transform host = cell.Find("CardHost");
            if (host == null) host = cell;

            if (entry.cardPrefab != null)
            {
                GameObject instance = Instantiate(entry.cardPrefab, host);
                instance.name = entry.title;
                ShowcaseUIFactory.ApplyCardLayer(instance);
                RectTransform instanceRect = (RectTransform)instance.transform;
                ShowcaseUIFactory.Stretch(instanceRect);
                ClampCardText(instance.transform, "CardTitle");
                ClampCardText(instance.transform, "CardDescription");
                cardInstances.Add(instance);
            }
            else
            {
                ImageEx fallback = ShowcaseUIFactory.CreatePanel("Missing Card", host,
                    new Color(0.22f, 0.12f, 0.12f, 1f), 12f);
                ShowcaseUIFactory.Stretch(fallback.rectTransform);
                TextMeshProUGUI label = ShowcaseUIFactory.CreateText("Missing Label", fallback.transform,
                    entry.title + "\n（预制体未生成）", 18f, ShowcaseUIFactory.PrimaryText,
                    TextAlignmentOptions.Center);
                ShowcaseUIFactory.Stretch(label.rectTransform, 12f, 12f, 12f, 12f);
            }

            Button open = cell.Find("Open")?.GetComponent<Button>();
            if (open != null)
            {
                open.onClick.RemoveAllListeners();
                open.onClick.AddListener(() => OpenDetail(entry));
                ConfigureGridCellTint(open);
            }
        }

        private static void ClampCardText(Transform root, string childName)
        {
            TMP_Text text = root.Find(childName)?.GetComponent<TMP_Text>();
            if (text == null) return;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void ConfigureGridCellTint(Button open)
        {
            Color accent = ShowcaseUIFactory.AccentColor;
            ConfigureStateTint(open, accent,
                Color.Lerp(accent, Color.white, 0.12f),
                Color.Lerp(accent, Color.black, 0.18f),
                new Color(0.5f, 0.5f, 0.5f, 0.45f));
        }

        private bool BindBakedLayout()
        {
            Transform header = transform.Find("Header");
            Transform grid = transform.Find("Grid");
            Transform footer = transform.Find("Footer");
            Transform tabs = transform.Find("Tabs");
            if (header == null || grid == null || footer == null || tabs == null)
            {
                Log.Error("[Showcase] ShowcaseUIPlane.prefab 缺少 Header/Grid/Footer/Tabs 层级，请重新烘焙预制体");
                return false;
            }

            gridRoot = grid as RectTransform;
            gridEntrance = grid.GetComponent<Gear.Utilities.UI.Motion.GearAnimatedContainer>();

            searchField = header.Find("Search")?.GetComponent<TMP_InputField>();
            if (searchField != null)
            {
                searchField.onValueChanged.RemoveListener(OnSearchChanged);
                searchField.onValueChanged.AddListener(OnSearchChanged);
            }

            themeButton = header.Find("Theme")?.GetComponent<Button>();
            if (themeButton != null)
            {
                themeLabel = themeButton.GetComponentInChildren<TMP_Text>();
                themeButton.onClick.RemoveListener(ToggleTheme);
                themeButton.onClick.AddListener(ToggleTheme);
                lightTheme = GearThemeRegistry.Current == GearThemeRegistry.LoadLight();
                UpdateThemeLabel();
            }

            motionButton = header.Find("Motion")?.GetComponent<Button>();
            if (motionButton != null)
            {
                motionLabel = motionButton.GetComponentInChildren<TMP_Text>();
                motionButton.onClick.RemoveListener(ToggleMotion);
                motionButton.onClick.AddListener(ToggleMotion);
                RefreshMotionLabel();
            }

            BindSampleButton(header, "SampleAudio", () => OpenSample<AudioMixerSampleUIPlane>());
            BindSampleButton(header, "SampleParty", () => OpenSample<PartyLobbySampleUIPlane>());
            BindSampleButton(header, "SampleInventory", () => OpenSample<InventorySampleUIPlane>());
            RefreshButtonTints(GearThemeRegistry.Current);

            Button close = header.Find("Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveListener(CloseWindow);
                close.onClick.AddListener(CloseWindow);
            }

            prevButton = footer.Find("Prev")?.GetComponent<Button>();
            nextButton = footer.Find("Next")?.GetComponent<Button>();
            pageLabel = footer.Find("PageLabel")?.GetComponent<TMP_Text>();
            countLabel = footer.Find("CountLabel")?.GetComponent<TMP_Text>();
            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(() => ChangePage(-1));
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(() => ChangePage(1));
            }

            Transform empty = transform.Find("EmptyState");
            emptyState = empty != null ? empty.gameObject : null;
            emptyLabel = empty != null ? empty.GetComponent<TMP_Text>() : null;
            if (emptyState != null) emptyState.SetActive(false);

            return true;
        }

        private void BindSampleButton(Transform header, string name, UnityEngine.Events.UnityAction action)
        {
            Button button = header.Find(name)?.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private bool ConfigureStateTint(Button button, Color normal, Color highlighted, Color pressed, Color disabled)
        {
            if (button == null) return false;
            GearInteractiveTint tint = button.GetComponent<GearInteractiveTint>();
            GearInteractive interactive = button.GetComponent<GearInteractive>();
            if (tint == null || interactive == null) return false;

            button.transition = Selectable.Transition.None;
            tint.Bind(interactive, button.targetGraphic);
            tint.SetColors(normal, highlighted, pressed, normal, disabled);
            interactive.Refresh(true);
            return true;
        }

        /// <summary>主题切换后刷新 Header/Footer 按钮的五态底色。</summary>
        private void RefreshButtonTints(ImageExThemeAsset theme)
        {
            Color normal = theme != null && theme.TryGetColor("SurfaceRaised", out Color surface)
                ? surface
                : ShowcaseUIFactory.SoftPanelColor;
            Color highlighted = Color.Lerp(normal, Color.white, 0.12f);
            Color pressed = Color.Lerp(normal, Color.black, 0.18f);
            Color disabled = new Color(0.5f, 0.5f, 0.5f, 0.45f);

            ConfigureButtonTint("Header/Theme", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Header/Motion", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Header/SampleAudio", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Header/SampleParty", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Header/SampleInventory", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Header/Close", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Footer/Prev", normal, highlighted, pressed, disabled);
            ConfigureButtonTint("Footer/Next", normal, highlighted, pressed, disabled);

            if (gridRoot != null)
            {
                for (int i = 0; i < gridRoot.childCount; i++)
                {
                    Button open = gridRoot.GetChild(i).Find("Open")?.GetComponent<Button>();
                    if (open != null) ConfigureGridCellTint(open);
                }
            }
        }

        private void ConfigureButtonTint(string path, Color normal, Color highlighted, Color pressed, Color disabled)
        {
            Button button = transform.Find(path)?.GetComponent<Button>();
            if (button != null) ConfigureStateTint(button, normal, highlighted, pressed, disabled);
        }
    }
}
