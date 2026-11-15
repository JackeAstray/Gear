using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 整屏样例的公共外壳：半透明遮罩、居中面板、返回/关闭、标题/副标题、内容区。
    /// <para>
    /// 外壳层级在各自 prefab 里直接维护；具体样例 prefab 已烘焙内容，运行时只绑定字段。
    /// 窗口仍走 UISystem/UIRouter，因此 ESC / 手柄 B / 返回键与普通窗口一致。
    /// </para>
    /// </summary>
    public abstract class ShowcaseSampleUIPlaneBase : UIController
    {
        protected const float PanelWidth = 1720f;
        protected const float PanelHeight = 920f;
        protected const float HeaderHeight = 100f;

        protected RectTransform Content { get; private set; }
        protected Button BackButton { get; private set; }

        public override void OnInit()
        {
            base.OnInit();
            if (!BindBakedShell())
                BuildShell();
            BuildSample();
        }

        private bool BindBakedShell()
        {
            Transform panel = transform.Find("Panel");
            if (panel == null) return false;

            Transform content = panel.Find("Content");
            if (content == null) return false;
            Content = content as RectTransform;

            BackButton = panel.Find("Back")?.GetComponent<Button>();
            if (BackButton != null)
            {
                BackButton.onClick.RemoveListener(CloseSample);
                BackButton.onClick.AddListener(CloseSample);
            }

            Button close = panel.Find("Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveListener(CloseSample);
                close.onClick.AddListener(CloseSample);
            }

            Button dim = transform.Find("Dim")?.GetComponent<Button>();
            if (dim != null)
            {
                dim.onClick.RemoveListener(CloseSample);
                dim.onClick.AddListener(CloseSample);
            }

            TextMeshProUGUI title = panel.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null) title.text = GetTitle();
            TextMeshProUGUI subtitle = panel.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
            if (subtitle != null) subtitle.text = GetSubtitle();

            BindTheme(panel.GetComponent<Graphic>(), "Surface");
            BindTheme(title, "Text");
            BindTheme(subtitle, "TextSecondary");
            if (BackButton != null)
            {
                BindTheme(BackButton.targetGraphic, "SurfaceRaised");
                BindTheme(BackButton.GetComponentInChildren<TextMeshProUGUI>(), "Text");
            }

            if (close != null)
            {
                BindTheme(close.targetGraphic, "SurfaceRaised");
                BindTheme(close.GetComponentInChildren<TextMeshProUGUI>(), "Text");
            }

            firstSelected = BackButton != null ? BackButton.gameObject : close != null ? close.gameObject : null;
            return true;
        }

        private static void BindTheme(Graphic graphic, string tokenId)
        {
            if (graphic == null || string.IsNullOrEmpty(tokenId)) return;
            GearThemeColorTarget target = graphic.GetComponent<GearThemeColorTarget>();
            if (target == null) target = graphic.gameObject.AddComponent<GearThemeColorTarget>();
            target.Configure(tokenId, graphic);
        }

        protected abstract void BuildSample();

        private void BuildShell()
        {
            ImageEx dim = ShowcaseUIFactory.CreatePanel("Dim", transform, new Color(0f, 0f, 0f, 0.62f), 0f, true);
            ShowcaseUIFactory.Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseSample);

            ImageEx panel = ShowcaseUIFactory.CreatePanel("Panel", transform, ShowcaseUIFactory.SoftPanelColor, 24f, true);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            ImageEx accent = ShowcaseUIFactory.CreatePanel("Accent", panelRect, ShowcaseUIFactory.AccentColor, 3f);
            ShowcaseUIFactory.TopLeft(accent.rectTransform, 32f, 0f, PanelWidth - 64f, 4f);

            BackButton = ShowcaseUIFactory.CreateButton("Back", panelRect, "← 展厅", new Color(0.16f, 0.18f, 0.23f, 1f),
                8f, CloseSample, 18f);
            ShowcaseUIFactory.TopLeft(BackButton.GetComponent<RectTransform>(), 32f, 28f, 120f, 40f);

            TextMeshProUGUI title = ShowcaseUIFactory.CreateText("Title", panelRect, GetTitle(), 34f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(title.rectTransform, 180f, 20f, 900f, 42f);

            TextMeshProUGUI subtitle = ShowcaseUIFactory.CreateText("Subtitle", panelRect, GetSubtitle(), 18f,
                ShowcaseUIFactory.MutedText);
            ShowcaseUIFactory.TopLeft(subtitle.rectTransform, 182f, 64f, 1000f, 26f);

            Button close = ShowcaseUIFactory.CreateButton("Close", panelRect, "×", new Color(0.18f, 0.2f, 0.25f, 1f),
                10f, CloseSample, 28f);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -28f);
            closeRect.sizeDelta = new Vector2(48f, 48f);

            ImageEx divider = ShowcaseUIFactory.CreatePanel("Divider", panelRect,
                new Color(1f, 1f, 1f, 0.08f), 0f);
            ShowcaseUIFactory.TopLeft(divider.rectTransform, 32f, 98f, PanelWidth - 64f, 1f);

            Content = ShowcaseUIFactory.CreateRect("Content", panelRect);
            ShowcaseUIFactory.Stretch(Content, 32f, 32f, 116f, 28f);

            firstSelected = BackButton.gameObject;
        }

        protected abstract string GetTitle();

        protected abstract string GetSubtitle();

        protected void CloseSample()
        {
            if (UIRouter.CanGoBack)
                UIRouter.Back();
            else
                UISystem.GetOrInstance().CloseWindow(GetType().Name);
        }

        protected static void ShowToast(string message)
        {
            GearToast.Show(message, "UI 展厅", GearToastKind.Success);
        }
    }
}
