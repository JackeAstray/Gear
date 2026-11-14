using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>挂在按钮/图片上，点击后打开一个简单文本 Popover。</summary>
    [AddComponentMenu("UI/Gear/Popover Trigger")]
    [DisallowMultipleComponent]
    public sealed class GearPopoverTrigger : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string title = "Popover";
        [SerializeField, TextArea(2, 5)] private string message = "点击外部或按 Esc 关闭";
        [SerializeField] private Vector2 size = new Vector2(260f, 128f);

        public string Title { get => title; set => title = value; }
        public string Message { get => message; set => message = value; }
        public Vector2 Size { get => size; set => size = value; }

        public void SetContent(string newTitle, string newMessage, Vector2? newSize = null)
        {
            title = newTitle;
            message = newMessage;
            if (newSize.HasValue) size = newSize.Value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            GearPopover.Show((RectTransform)transform, size, content =>
            {
                TextMeshProUGUI titleText = GearOverlayLayer.CreateText(
                    content, "Title", 17f, GearOverlayTheme.Get("OverlayText", GearOverlayTheme.TextPrimary),
                    true, TextAlignmentOptions.Left);
                RectTransform titleRect = (RectTransform)titleText.transform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0f, 1f);
                titleRect.offsetMin = new Vector2(0f, -24f);
                titleRect.offsetMax = new Vector2(0f, 0f);
                titleText.text = title;

                TextMeshProUGUI messageText = GearOverlayLayer.CreateText(
                    content, "Message", 13f, GearOverlayTheme.Get("OverlayTextSecondary", GearOverlayTheme.TextSecondary),
                    false, TextAlignmentOptions.TopLeft);
                RectTransform messageRect = (RectTransform)messageText.transform;
                messageRect.anchorMin = Vector2.zero;
                messageRect.anchorMax = Vector2.one;
                messageRect.offsetMin = new Vector2(0f, 0f);
                messageRect.offsetMax = new Vector2(0f, -32f);
                messageText.text = message;
            });
        }
    }
}
