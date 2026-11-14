using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>
    /// 悬停提示：把 <see cref="GearTooltipTrigger"/> 挂到任意 UI 物体上，
    /// 或调用 <c>GearTooltip.Attach(target, "说明")</c> 自动挂载。
    /// </summary>
    public static class GearTooltip
    {
        private static GearTooltipView s_view;

        /// <summary>提示面板最大宽度；已有视图会立即应用。</summary>
        public static float MaxWidth
        {
            get => s_maxWidth;
            set
            {
                s_maxWidth = Mathf.Max(120f, value);
                if (s_view != null) s_view.SetMaxWidth(s_maxWidth);
            }
        }

        private static float s_maxWidth = 320f;

        /// <summary>提前创建并复用提示视图，避免首次悬停分配。</summary>
        public static void Prewarm()
        {
            EnsureView();
        }

        private static void EnsureView()
        {
            if (s_view != null) return;

            GameObject go = new GameObject("GearTooltip", typeof(RectTransform), typeof(CanvasGroup));
            go.SetActive(false);
            GearOverlayLayer.ApplyUILayer(go);
            go.transform.SetParent(GearOverlayLayer.Root, false);
            s_view = go.AddComponent<GearTooltipView>();
            s_view.Build();
            s_view.SetMaxWidth(s_maxWidth);
        }

        /// <summary>立即显示提示。</summary>
        public static void Show(string title, string message, Sprite icon = null, Vector2? screenPosition = null)
        {
            EnsureView();

            s_view.gameObject.SetActive(true);
            s_view.SetContent(title, message, icon);
            s_view.SetScreenPosition(screenPosition ?? GetPointerScreenPosition());
        }

        /// <summary>显示富内容提示；由调用方填充根节点（建议自行加 LayoutGroup/LayoutElement）。</summary>
        public static void ShowCustom(Action<RectTransform> buildContent, Vector2? screenPosition = null)
        {
            if (buildContent == null) return;

            EnsureView();
            s_view.gameObject.SetActive(true);
            s_view.SetCustomContent(buildContent);
            s_view.SetScreenPosition(screenPosition ?? GetPointerScreenPosition());
        }

        /// <summary>隐藏提示。</summary>
        public static void Hide()
        {
            if (s_view != null) s_view.gameObject.SetActive(false);
        }

        /// <summary>给目标挂一个触发器（已存在则更新文案）。</summary>
        public static GearTooltipTrigger Attach(GameObject target, string message, string title = null, float delay = 0.35f)
        {
            if (target == null) return null;
            GearTooltipTrigger trigger = target.GetComponent<GearTooltipTrigger>();
            if (trigger == null) trigger = target.AddComponent<GearTooltipTrigger>();
            trigger.SetContent(title, message);
            trigger.Delay = delay;
            return trigger;
        }

        internal static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
#endif
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
    }

    /// <summary>悬停触发器：延迟后显示提示，移出立即隐藏。</summary>
    [AddComponentMenu("UI/Gear/提示触发器")]
    [DisallowMultipleComponent]
    public sealed class GearTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string message;
        [SerializeField] private Sprite icon;
        [SerializeField, Tooltip("悬停多久后显示（秒）")]
        private float delay = 0.35f;

        private Coroutine routine;
        private bool pointerInside;

        /// <summary>显示延迟（秒）。</summary>
        public float Delay
        {
            get => delay;
            set => delay = Mathf.Max(0f, value);
        }

        /// <summary>设置文案。</summary>
        public void SetContent(string newTitle, string newMessage, Sprite newIcon = null)
        {
            title = newTitle;
            message = newMessage;
            if (newIcon != null) icon = newIcon;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            GearTooltip.Hide();
        }

        private void OnDisable()
        {
            if (pointerInside) GearTooltip.Hide();
            pointerInside = false;
        }

        private IEnumerator ShowAfterDelay()
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (!pointerInside) yield break;
            GearTooltip.Show(title, message, icon);
        }
    }

    /// <summary>提示面板视图：跟随指针、自动高度、不拦截射线。</summary>
    [DisallowMultipleComponent]
    public sealed class GearTooltipView : MonoBehaviour
    {
        private const float DefaultMaxWidth = 320f;

        private RectTransform panel;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI messageText;
        private Vector2 offset = new Vector2(18f, -18f);
        private float maxWidth = DefaultMaxWidth;
        private RectTransform customRoot;

        internal void Build()
        {
            rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(maxWidth, 0f);

            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            panel = (RectTransform)transform;

            ImageEx background = GearOverlayLayer.CreateRoundedPanel(
                transform, "Background", GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised), 10f, shadow: true);
            GearOverlayLayer.Stretch(background.transform as RectTransform);
            LayoutElement backgroundLayout = background.gameObject.AddComponent<LayoutElement>();
            backgroundLayout.ignoreLayout = true;

            VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = GearOverlayLayer.CreateText(
                transform, "Title", 15f, GearOverlayTheme.Get("OverlayText", GearOverlayTheme.TextPrimary),
                true, TextAlignmentOptions.Left);
            titleText.gameObject.SetActive(false);

            messageText = GearOverlayLayer.CreateText(
                transform, "Message", 13f, GearOverlayTheme.Get("OverlayTextSecondary", GearOverlayTheme.TextSecondary),
                false, TextAlignmentOptions.Left);
            messageText.text = string.Empty;
        }

        internal void SetContent(string title, string message, Sprite icon)
        {
            ClearCustom();
            titleText.gameObject.SetActive(true);
            messageText.gameObject.SetActive(true);

            bool hasTitle = !string.IsNullOrEmpty(title);
            titleText.gameObject.SetActive(hasTitle);
            titleText.text = hasTitle ? title : string.Empty;
            messageText.text = message ?? string.Empty;

            // 内容变化后等一帧让布局组算出高度，再把面板挪到指针旁边
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            SetScreenPosition(GearTooltip.GetPointerScreenPosition());
        }

        internal void SetCustomContent(Action<RectTransform> buildContent)
        {
            ClearCustom();
            titleText.gameObject.SetActive(false);
            messageText.gameObject.SetActive(false);

            GameObject custom = new GameObject("Custom", typeof(RectTransform));
            custom.transform.SetParent(transform, false);
            customRoot = (RectTransform)custom.transform;

            buildContent(customRoot);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private void ClearCustom()
        {
            if (customRoot == null) return;

            if (Application.isPlaying) Destroy(customRoot.gameObject);
            else DestroyImmediate(customRoot.gameObject);
            customRoot = null;
        }

        internal void SetMaxWidth(float width)
        {
            maxWidth = Mathf.Max(120f, width);
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            rectTransform.sizeDelta = new Vector2(maxWidth, rectTransform.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        internal void SetScreenPosition(Vector2 screenPosition)
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            RectTransform root = GearOverlayLayer.Root;
            if (root == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, null, out Vector2 local))
            {
                local = Vector2.zero;
            }

            Vector2 size = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            Vector2 position = local + offset;

            // 贴边时翻到指针另一侧，避免超出屏幕
            Rect rootRect = root.rect;
            if (position.x + size.x > rootRect.xMax - 8f) position.x = local.x - size.x - offset.x;
            if (position.y - size.y < rootRect.yMin + 8f) position.y = local.y + size.y - offset.y;

            position.x = Mathf.Clamp(position.x, rootRect.xMin + 8f, rootRect.xMax - size.x - 8f);
            position.y = Mathf.Clamp(position.y, rootRect.yMin + size.y + 8f, rootRect.yMax - 8f);
            rectTransform.anchoredPosition = position;
            rectTransform.pivot = pivot;
        }
    }
}
