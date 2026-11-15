using Gear.Common;
using Gear.Framework.Runtime.UI;
using Gear.Utilities.UI.ImageExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 展厅详情参数：卡片元数据 + 预制体引用。
    /// </summary>
    public readonly struct ShowcaseDetailArgs : IWindowArgs
    {
        public readonly string Title;
        public readonly string Description;
        public readonly GameObject CardPrefab;
        public readonly string PageId;

        public ShowcaseDetailArgs(string title, string description, GameObject cardPrefab, string pageId)
        {
            Title = title;
            Description = description;
            CardPrefab = cardPrefab;
            PageId = pageId;
        }
    }

    /// <summary>
    /// 展厅详情窗口：把选中的卡片放大展示，并保留卡片自身的交互。
    /// <para>
    /// 用 <see cref="UIRouter.PushAsync{TWindow,TArgs}"/> 打开，返回键/ESC 由导航栈关闭；
    /// 卡片本体是目录里的预制体实例，详情窗口不复制卡片逻辑。
    /// </para>
    /// </summary>
    [UIWindow("ShowcaseDetailUIPlane", Layer = PanelType.NormalUI, Modal = true)]
    public sealed class ShowcaseDetailUIPlane : UIController, IWindowArgsReceiver<ShowcaseDetailArgs>
    {
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 840f;
        private const float PreviewWidth = 640f;
        private const float PreviewHeight = 544f;
        private const float CardScale = 1.55f;

        private RectTransform previewHost;
        private TMP_Text titleLabel;
        private TMP_Text descriptionLabel;
        private Button closeButton;
        private Button customizeButton;
        private ShowcaseCustomizer customizer;
        private GameObject cardInstance;
        private bool built;
        private ShowcaseDetailArgs pendingArgs;
        private bool hasPendingArgs;

        public override void OnInit()
        {
            base.OnInit();
            if (!BindBakedLayout())
                BuildWindow();
            if (hasPendingArgs)
            {
                hasPendingArgs = false;
                OnWindowArgs(pendingArgs);
            }
        }

        private bool BindBakedLayout()
        {
            Transform panel = transform.Find("Panel");
            if (panel == null) return false;

            titleLabel = panel.Find("Title")?.GetComponent<TMP_Text>();
            descriptionLabel = panel.Find("Description")?.GetComponent<TMP_Text>();
            closeButton = panel.Find("Close")?.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
                firstSelected = closeButton.gameObject;
            }

            Button back = panel.Find("Back")?.GetComponent<Button>();
            if (back != null)
            {
                back.onClick.RemoveListener(Close);
                back.onClick.AddListener(Close);
                if (closeButton == null) firstSelected = back.gameObject;
            }

            customizeButton = panel.Find("Customize")?.GetComponent<Button>();
            if (customizeButton != null)
            {
                customizeButton.onClick.RemoveListener(ToggleCustomizer);
                customizeButton.onClick.AddListener(ToggleCustomizer);
            }

            customizer = panel.Find("Customizer")?.GetComponent<ShowcaseCustomizer>();
            if (customizer != null) customizer.gameObject.SetActive(false);

            Button dim = transform.Find("Dim")?.GetComponent<Button>();
            if (dim != null)
            {
                dim.onClick.RemoveListener(Close);
                dim.onClick.AddListener(Close);
            }

            Transform preview = panel.Find("Preview");
            previewHost = preview != null ? preview.Find("Card Host") as RectTransform : null;
            if (previewHost == null) return false;
            built = true;
            return true;
        }

        public void OnWindowArgs(ShowcaseDetailArgs args)
        {
            if (!built)
            {
                pendingArgs = args;
                hasPendingArgs = true;
                return;
            }

            if (titleLabel != null) titleLabel.text = args.Title;
            if (descriptionLabel != null) descriptionLabel.text = args.Description;
            ShowCard(args.CardPrefab);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (built && cardInstance == null && previewHost != null)
            {
                // 静态/池化窗口被复用时，参数已提前派发；这里只保证预览区可见。
                previewHost.gameObject.SetActive(true);
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            if (customizer != null) customizer.gameObject.SetActive(false);
            ShowCard(null);
        }

        private void BuildWindow()
        {
            if (built) return;
            built = true;

            ImageEx dim = ShowcaseUIFactory.CreatePanel("Dim", transform, new Color(0f, 0f, 0f, 0.72f), 0f, true);
            ShowcaseUIFactory.Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            ImageEx panel = ShowcaseUIFactory.CreatePanel("Panel", transform, ShowcaseUIFactory.SoftPanel, 18f, true);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            titleLabel = ShowcaseUIFactory.CreateText("Title", panelRect, "卡片", 30f,
                ShowcaseUIFactory.PrimaryText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            ShowcaseUIFactory.TopLeft(titleLabel.rectTransform, 40f, 28f, 820f, 40f);

            descriptionLabel = ShowcaseUIFactory.CreateText("Description", panelRect, "", 18f,
                ShowcaseUIFactory.MutedText, TextAlignmentOptions.TopLeft);
            ShowcaseUIFactory.TopLeft(descriptionLabel.rectTransform, 40f, 74f, 820f, 44f);

            closeButton = ShowcaseUIFactory.CreateButton("Close", panelRect, "×", new Color(0.18f, 0.2f, 0.25f, 1f),
                10f, Close, 28f);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -28f);
            closeRect.sizeDelta = new Vector2(48f, 48f);

            ImageEx previewPanel = ShowcaseUIFactory.CreatePanel("Preview", panelRect,
                new Color(0.045f, 0.052f, 0.068f, 1f), 14f);
            RectTransform previewRect = previewPanel.rectTransform;
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(0f, -58f);
            previewRect.sizeDelta = new Vector2(PreviewWidth, PreviewHeight);

            previewHost = ShowcaseUIFactory.CreateRect("Card Host", previewRect);
            ShowcaseUIFactory.Stretch(previewHost, 8f, 8f, 8f, 8f);

            Button back = ShowcaseUIFactory.CreateButton("Back", panelRect, "返回展厅", ShowcaseUIFactory.AccentColor,
                10f, Close, 20f);
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 28f);
            backRect.sizeDelta = new Vector2(220f, 48f);

            firstSelected = closeButton.gameObject;
        }

        private void ShowCard(GameObject prefab)
        {
            if (cardInstance != null)
            {
                cardInstance.SetActive(false);
                Destroy(cardInstance);
                cardInstance = null;
            }

            if (prefab == null || previewHost == null) return;

            cardInstance = Instantiate(prefab, previewHost);
            ShowcaseUIFactory.ApplyCardLayer(cardInstance);
            RectTransform rect = (RectTransform)cardInstance.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * CardScale;

            if (customizer != null)
                customizer.Bind(cardInstance.GetComponent<ImageEx>());
        }

        private void ToggleCustomizer()
        {
            if (customizer == null) return;
            bool open = !customizer.gameObject.activeSelf;
            customizer.gameObject.SetActive(open);
            if (open && cardInstance != null)
                customizer.Bind(cardInstance.GetComponent<ImageEx>());
        }

        private void Close()
        {
            if (UIRouter.CanGoBack)
                UIRouter.Back();
            else
                UISystem.GetOrInstance().CloseWindow(nameof(ShowcaseDetailUIPlane));
        }
    }
}
