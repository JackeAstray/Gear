using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>Toast 语义类型，决定强调色与默认图标。</summary>
    public enum GearToastKind
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// 轻量 Toast：右上角堆叠、自动关闭、走 unscaled time（暂停菜单里也能正常消失）。
    /// <para>
    /// 用法：<c>GearToast.Show("已保存", "进度", GearToastKind.Success);</c>
    /// </para>
    /// </summary>
    public static class GearToast
    {
        private const float Width = 380f;
        private static RectTransform s_container;
        private static readonly List<GearToastView> s_visible = new List<GearToastView>();
        private static readonly Queue<GearToastView> s_pending = new Queue<GearToastView>();

        /// <summary>同时可见的最大条数；超出后排队，关闭一条再显示下一条。</summary>
        public static int MaxVisible { get; set; } = 3;

        /// <summary>显示一条 Toast，返回可手动关闭的视图。超出 MaxVisible 时会先排队。</summary>
        public static GearToastView Show(
            string message,
            string title = null,
            GearToastKind kind = GearToastKind.Info,
            Sprite icon = null,
            float duration = 3f)
        {
            EnsureContainer();

            GameObject go = new GameObject("GearToast", typeof(RectTransform), typeof(CanvasGroup));
            GearOverlayLayer.ApplyUILayer(go);
            go.SetActive(false);
            go.transform.SetParent(s_container, false);

            GearToastView view = go.AddComponent<GearToastView>();
            view.Build(message, title, kind, icon, duration);
            s_pending.Enqueue(view);
            Pump();
            return view;
        }

        /// <summary>清除所有 Toast（包含排队中的）。</summary>
        public static void ClearAll()
        {
            s_pending.Clear();
            s_visible.Clear();
            if (s_container == null) return;
            for (int i = s_container.childCount - 1; i >= 0; i--)
            {
                GameObject child = s_container.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }

        internal static void NotifyClosed(GearToastView view)
        {
            s_visible.Remove(view);
            Pump();
        }

        private static void Pump()
        {
            int limit = Mathf.Max(1, MaxVisible);
            while (s_visible.Count < limit && s_pending.Count > 0)
            {
                GearToastView view = s_pending.Dequeue();
                if (view == null) continue;

                s_visible.Add(view);
                view.gameObject.SetActive(true);
            }
        }

        private static void EnsureContainer()
        {
            if (s_container != null) return;

            GameObject go = new GameObject("ToastContainer", typeof(RectTransform));
            GearOverlayLayer.ApplyUILayer(go);
            go.transform.SetParent(GearOverlayLayer.Root, false);

            s_container = (RectTransform)go.transform;
            s_container.anchorMin = new Vector2(1f, 1f);
            s_container.anchorMax = new Vector2(1f, 1f);
            s_container.pivot = new Vector2(1f, 1f);
            s_container.anchoredPosition = new Vector2(-24f, -24f);
            s_container.sizeDelta = new Vector2(Width, 0f);

            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>单条 Toast 的视图与生命周期。</summary>
    [DisallowMultipleComponent]
    public sealed class GearToastView : MonoBehaviour
    {
        private const float Width = 380f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private ImageEx progress;
        private RectTransform progressRect;
        private float duration = 3f;
        private Coroutine routine;

        internal void Build(string message, string title, GearToastKind kind, Sprite icon, float toastDuration)
        {
            duration = Mathf.Max(0.2f, toastDuration);
            rectTransform = (RectTransform)transform;
            rectTransform.sizeDelta = new Vector2(Width, string.IsNullOrEmpty(title) ? 68f : 86f);

            LayoutElement layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Width;
            layout.preferredHeight = rectTransform.sizeDelta.y;

            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            Color accent = kind switch
            {
                GearToastKind.Success => GearOverlayTheme.Get("OverlaySuccess", GearOverlayTheme.Success),
                GearToastKind.Warning => GearOverlayTheme.Get("OverlayWarning", GearOverlayTheme.Warning),
                GearToastKind.Error => GearOverlayTheme.Get("OverlayDanger", GearOverlayTheme.Danger),
                _ => GearOverlayTheme.Get("OverlayAccent", GearOverlayTheme.Accent),
            };

            ImageEx background = GearOverlayLayer.CreateRoundedPanel(
                transform, "Background", GearOverlayTheme.Get("OverlaySurface", GearOverlayTheme.Surface), 14f, shadow: true);
            GearOverlayLayer.Stretch((RectTransform)background.transform);

            // 上方 1px 内高光 + 左侧强调条，让 Toast 在深色背景里有层次
            ImageEx highlight = GearOverlayLayer.CreateRoundedPanel(
                transform, "Highlight", new Color(1f, 1f, 1f, 0.06f), 14f);
            RectTransform highlightRect = (RectTransform)highlight.transform;
            highlightRect.anchorMin = new Vector2(0f, 1f);
            highlightRect.anchorMax = new Vector2(1f, 1f);
            highlightRect.pivot = new Vector2(0.5f, 1f);
            highlightRect.offsetMin = new Vector2(1f, -1f);
            highlightRect.offsetMax = new Vector2(-1f, 0f);

            ImageEx bar = GearOverlayLayer.CreateRoundedPanel(transform, "Accent", accent, 4f);
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.offsetMin = new Vector2(10f, 10f);
            barRect.offsetMax = new Vector2(14f, -10f);

            progress = GearOverlayLayer.CreateRoundedPanel(transform, "Progress", accent, 1f);
            progressRect = (RectTransform)progress.transform;
            progressRect.anchorMin = new Vector2(0f, 0f);
            progressRect.anchorMax = new Vector2(1f, 0f);
            progressRect.pivot = new Vector2(0f, 0f);
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = new Vector2(0f, 2f);
            progress.raycastTarget = false;

            float textLeft = 30f;
            if (icon != null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                GearOverlayLayer.ApplyUILayer(iconObject);
                iconObject.transform.SetParent(transform, false);
                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(26f, 0f);
                iconRect.sizeDelta = new Vector2(28f, 28f);
                textLeft = 66f;
            }
            else
            {
                ImageEx dot = GearOverlayLayer.CreateRoundedPanel(transform, "Dot", accent, 4f);
                RectTransform dotRect = (RectTransform)dot.transform;
                dotRect.anchorMin = new Vector2(0f, 0.5f);
                dotRect.anchorMax = new Vector2(0f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(34f, 0f);
                dotRect.sizeDelta = new Vector2(8f, 8f);
            }

            TextMeshProUGUI titleText = GearOverlayLayer.CreateText(
                transform, "Title", 17f, GearOverlayTheme.Get("OverlayText", GearOverlayTheme.TextPrimary),
                true, TextAlignmentOptions.Left);
            RectTransform titleRect = (RectTransform)titleText.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(textLeft, -30f);
            titleRect.offsetMax = new Vector2(-16f, -10f);

            TextMeshProUGUI messageText = GearOverlayLayer.CreateText(
                transform, "Message", 14f, GearOverlayTheme.Get("OverlayTextSecondary", GearOverlayTheme.TextSecondary),
                false, TextAlignmentOptions.Left);
            RectTransform messageRect = (RectTransform)messageText.transform;
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.pivot = new Vector2(0f, 1f);
            if (string.IsNullOrEmpty(title))
            {
                messageRect.offsetMin = new Vector2(textLeft, 10f);
                messageRect.offsetMax = new Vector2(-16f, -10f);
                messageText.alignment = TextAlignmentOptions.Left;
            }
            else
            {
                messageRect.offsetMin = new Vector2(textLeft, 10f);
                messageRect.offsetMax = new Vector2(-16f, -32f);
                titleText.text = title;
            }

            messageText.text = message ?? string.Empty;
        }

        private void OnEnable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            // 位置由父级 VerticalLayoutGroup 控制，这里只做透明度动画，避免和布局互相覆盖
            float elapsed = 0f;
            const float fadeIn = 0.18f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeIn);
                canvasGroup.alpha = 1f - (1f - t) * (1f - t);
                yield return null;
            }

            canvasGroup.alpha = 1f;

            float remaining = duration;
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                if (progressRect != null)
                    progressRect.localScale = new Vector3(Mathf.Clamp01(remaining / duration), 1f, 1f);
                yield return null;
            }

            if (progressRect != null)
                progressRect.localScale = Vector3.zero;

            yield return FadeOut(0.2f);
            Destroy(gameObject);
        }

        private IEnumerator FadeOut(float fadeTime)
        {
            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        /// <summary>立即关闭。</summary>
        public void Close()
        {
            if (!Application.isPlaying)
            {
                GearToast.NotifyClosed(this);
                DestroyImmediate(gameObject);
                return;
            }

            if (!gameObject.activeSelf)
            {
                Destroy(gameObject);
                return;
            }

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            yield return FadeOut(0.15f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            GearToast.NotifyClosed(this);
        }
    }
}
