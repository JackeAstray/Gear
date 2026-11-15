using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>
    /// 轻量 Popover：在覆盖层里创建面板，点击外部或按 Esc 关闭。
    /// <para>
    /// 用法：<c>GearPopover.Show(anchor, new Vector2(260, 120), content => { ... });</c>
    /// 内容由调用方构建；Popover 只负责遮挡、定位、关闭和背景。
    /// </para>
    /// </summary>
    public sealed class GearPopover : MonoBehaviour
    {
        private static GearPopover s_open;

        private RectTransform blocker;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Coroutine animation;
        private bool closing;
        private Action onClose;

        public bool IsOpen => isActiveAndEnabled;
        public RectTransform Content { get; private set; }

        /// <summary>打开一个 Popover；同时间只保留一个。</summary>
        public static GearPopover Show(RectTransform anchor, Vector2 size, Action<RectTransform> buildContent,
            Vector2? offset = null)
        {
            CloseOpen();

            GameObject go = new GameObject("GearPopover", typeof(RectTransform), typeof(CanvasGroup));
            go.SetActive(false);
            GearOverlayLayer.ApplyUILayer(go);
            go.transform.SetParent(GearOverlayLayer.Root, false);

            GearPopover popover = go.AddComponent<GearPopover>();
            popover.Build(anchor, size, buildContent, offset ?? new Vector2(0f, -8f));
            go.SetActive(true);
            popover.PlayOpen();
            s_open = popover;
            GearOverlayCoordinator.Open(popover, popover.Close, "popover");
            return popover;
        }

        public static void CloseOpen()
        {
            if (s_open != null)
                s_open.Close();
        }

        /// <summary>关闭回调（在面板销毁前调用）。</summary>
        public void SetOnClose(Action callback) => onClose = callback;

        private void Build(RectTransform anchor, Vector2 size, Action<RectTransform> buildContent, Vector2 offset)
        {
            rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = size;

            CanvasGroup group = GetComponent<CanvasGroup>();
            canvasGroup = group;
            group.blocksRaycasts = true;
            group.interactable = true;
            group.alpha = Application.isPlaying ? 0f : 1f;
            if (Application.isPlaying) transform.localScale = Vector3.one * 0.96f;

            // 全屏透明遮挡：点击外部关闭
            GameObject blockerObject = new GameObject("Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            GearOverlayLayer.ApplyUILayer(blockerObject);
            blockerObject.transform.SetParent(transform.parent, false);
            blocker = (RectTransform)blockerObject.transform;
            GearOverlayLayer.Stretch(blocker);
            blocker.SetSiblingIndex(transform.GetSiblingIndex());

            Image blockerImage = blockerObject.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0f);
            blockerImage.raycastTarget = true;

            Button blockerButton = blockerObject.GetComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.AddListener(Close);

            ImageEx background = GearOverlayLayer.CreateRoundedPanel(
                transform, "Background", GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised),
                12f, true, shadow: true);
            GearOverlayLayer.Stretch((RectTransform)background.transform);

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            GearOverlayLayer.ApplyUILayer(contentObject);
            contentObject.transform.SetParent(transform, false);
            Content = (RectTransform)contentObject.transform;
            GearOverlayLayer.Stretch(Content);
            Content.offsetMin = new Vector2(14f, 14f);
            Content.offsetMax = new Vector2(-14f, -14f);

            buildContent?.Invoke(Content);

            Position(anchor, offset);
        }

        private void Position(RectTransform anchor, Vector2 offset)
        {
            if (anchor == null) return;

            rectTransform.position = anchor.position + (Vector3)offset;

            // 尺寸刚写入时 rect 可能还没刷新，先强制一次布局再贴边，避免按 0 高度判断导致跑出屏幕
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            // 简单贴边修正：避免面板跑出覆盖层
            RectTransform root = GearOverlayLayer.Root;
            if (root == null) return;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float left = corners[0].x;
            float right = corners[2].x;
            float top = corners[1].y;
            float bottom = corners[0].y;

            Vector3[] rootCorners = new Vector3[4];
            root.GetWorldCorners(rootCorners);
            float rootLeft = rootCorners[0].x;
            float rootRight = rootCorners[2].x;
            float rootTop = rootCorners[1].y;
            float rootBottom = rootCorners[0].y;

            Vector2 shift = Vector2.zero;
            if (right > rootRight) shift.x -= right - rootRight;
            if (left < rootLeft) shift.x += rootLeft - left;
            if (bottom < rootBottom) shift.y += rootBottom - bottom;
            if (top > rootTop) shift.y -= top - rootTop;

            if (shift != Vector2.zero)
                rectTransform.position += (Vector3)shift;
        }

        private void Update()
        {
            if (WasEscapePressed() && GearOverlayCoordinator.TryHandleEscape(this))
                Close();
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>关闭并销毁 Popover；Play 模式下先播放淡出/缩放。</summary>
        public void Close()
        {
            if (!Application.isPlaying)
            {
                DestroyNow();
                return;
            }

            if (closing) return;
            closing = true;

            if (animation != null) StopCoroutine(animation);
            animation = StartCoroutine(CloseRoutine());
        }

        private void PlayOpen()
        {
            if (!Application.isPlaying || canvasGroup == null) return;
            if (animation != null) StopCoroutine(animation);
            animation = StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            const float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                canvasGroup.alpha = eased;
                transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
            animation = null;
        }

        private IEnumerator CloseRoutine()
        {
            const float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                canvasGroup.alpha = 1f - eased;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, eased);
                yield return null;
            }

            DestroyNow();
        }

        private void DestroyNow()
        {
            GearOverlayCoordinator.Release(this);
            onClose?.Invoke();
            onClose = null;

            if (blocker != null)
            {
                if (Application.isPlaying) Destroy(blocker.gameObject);
                else DestroyImmediate(blocker.gameObject);
                blocker = null;
            }

            if (s_open == this) s_open = null;

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            GearOverlayCoordinator.Release(this);
            if (s_open == this) s_open = null;
        }
    }
}
