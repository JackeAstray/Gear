using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Utilities.UI.Navigation
{
    /// <summary>
    /// 菜单栏：按标签生成一排按钮，点击回调序号；通常配合
    /// <c>GearContextMenu.Show(anchor, items)</c> 做贴按钮的下拉菜单。
    /// </summary>
    [AddComponentMenu("UI/Gear/Menu Bar")]
    [DisallowMultipleComponent]
    public sealed class GearMenuBar : MonoBehaviour
    {
        [SerializeField] private float spacing = 6f;
        [SerializeField] private float itemHeight = 34f;
        [SerializeField] private float itemMinWidth = 84f;

        private readonly List<Button> buttons = new List<Button>();
        private Action<int> onClick;

        /// <summary>生成的按钮，可按序号取 RectTransform 作为菜单锚点。</summary>
        public IReadOnlyList<Button> Buttons => buttons;

        /// <summary>
        /// 重新生成菜单项。<paramref name="onClick"/> 收到的是点击项序号。
        /// </summary>
        public void Configure(IList<string> labels, Action<int> onClick)
        {
            this.onClick = onClick;
            Clear();

            HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (labels == null) return;

            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i] ?? string.Empty;

                ImageEx background = GearOverlayLayer.CreateRoundedPanel(
                    transform, "Item " + label,
                    GearOverlayTheme.Get("OverlaySurface", GearOverlayTheme.SurfaceRaised), 8f, true);
                background.rectTransform.sizeDelta = new Vector2(itemMinWidth, itemHeight);

                LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = itemMinWidth;
                element.preferredHeight = itemHeight;

                TextMeshProUGUI text = GearOverlayLayer.CreateText(
                    background.transform, "Label", 15f, GearOverlayTheme.TextPrimary, true,
                    TextAlignmentOptions.Center);
                text.text = label;
                text.raycastTarget = false;
                GearOverlayLayer.Stretch(text.rectTransform);

                Button button = background.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int index = i;
                button.onClick.AddListener(() => this.onClick?.Invoke(index));
                buttons.Add(button);
            }
        }

        private void Clear()
        {
            buttons.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}
