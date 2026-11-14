using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 下拉选择：点击按钮弹出 <see cref="GearPopover"/> 选项列表，选中后回写标签并派发序号。
    /// <para>
    /// 覆盖 uGUI 原生 Dropdown 缺的部分：Popup 走 Gear 覆盖层（主题/圆角/贴边）、
    /// 关闭时清空选中、支持运行时改选项；键盘/手柄导航复用行 Button 的 Selectable。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Dropdown")]
    [DisallowMultipleComponent]
    public sealed class GearDropdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;
        [SerializeField] private List<string> options = new List<string>();
        [SerializeField] private int value = -1;
        [SerializeField, Min(12f)] private float itemHeight = 34f;
        [SerializeField, Min(40f)] private float popupWidth = 220f;
        [SerializeField, Min(40f)] private float maxHeight = 280f;
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();

        private GearPopover popover;
        private readonly List<Button> rows = new List<Button>();

        public IReadOnlyList<string> Options => options;
        public UnityEvent<int> OnValueChanged => onValueChanged;
        public bool IsOpen => popover != null;

        /// <summary>显示当前选项的文本；为空时自动找子节点 TMP。</summary>
        public TMP_Text Label
        {
            get
            {
                if (label == null) label = GetComponentInChildren<TMP_Text>(true);
                return label;
            }
            set => label = value;
        }

        /// <summary>触发下拉的按钮；为空时自动找自身/子节点的 Button。</summary>
        public Button Trigger
        {
            get
            {
                if (button == null) button = GetComponentInChildren<Button>(true);
                return button;
            }
            set
            {
                if (button == value) return;
                Unbind();
                button = value;
                Bind();
            }
        }

        /// <summary>当前选中序号（-1 表示未选中）。</summary>
        public int Value
        {
            get => value;
            set => SetValue(value, true);
        }

        private void OnEnable()
        {
            Bind();
            RefreshLabel();
        }

        private void OnDisable()
        {
            Unbind();
            Close();
        }

        private void Bind()
        {
            Button trigger = Trigger;
            if (trigger == null) return;
            trigger.onClick.RemoveListener(Toggle);
            trigger.onClick.AddListener(Toggle);
        }

        private void Unbind()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
        }

        /// <summary>替换选项；尽量保留当前序号。</summary>
        public void SetOptions(IList<string> items, bool keepValue = true)
        {
            options.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++) options.Add(items[i] ?? string.Empty);
            }

            if (!keepValue) value = -1;
            if (value >= options.Count) value = options.Count - 1;
            RefreshLabel();
        }

        /// <summary>写入选中值；notify=true 时派发事件。</summary>
        public void SetValue(int index, bool notify)
        {
            index = options.Count == 0 ? -1 : Mathf.Clamp(index, -1, options.Count - 1);
            if (value == index)
            {
                RefreshLabel();
                return;
            }

            value = index;
            RefreshLabel();
            if (notify) onValueChanged?.Invoke(value);
        }

        /// <summary>写入选中值但不派发事件。</summary>
        public void SetValueWithoutNotify(int index) => SetValue(index, false);

        /// <summary>打开 / 关闭下拉。</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>打开选项列表。</summary>
        public void Open()
        {
            if (options.Count == 0) return;

            Close();
            float height = Mathf.Min(maxHeight, options.Count * itemHeight + 12f);
            var anchor = (RectTransform)transform;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(anchor.lossyScale.y));
            float halfHeight = anchor.rect.height * 0.5f + 6f;
            Vector2 offset = new Vector2(0f, -halfHeight * scaleY);

            // 下方空间不足时改为向上展开（覆盖层根按世界坐标判断）
            RectTransform root = GearOverlayLayer.Root;
            if (root != null)
            {
                Vector3[] anchorCorners = new Vector3[4];
                anchor.GetWorldCorners(anchorCorners);
                Vector3[] rootCorners = new Vector3[4];
                root.GetWorldCorners(rootCorners);
                float needed = height * scaleY + 8f;
                if (anchorCorners[0].y - needed < rootCorners[0].y)
                    offset.y = halfHeight * scaleY;
            }

            popover = GearPopover.Show(anchor, new Vector2(popupWidth, height), BuildRows, offset);
            popover.SetOnClose(OnPopoverClosed);
        }

        /// <summary>关闭选项列表。</summary>
        public void Close()
        {
            if (popover == null) return;
            GearPopover current = popover;
            popover = null;
            rows.Clear();
            current.Close();
        }

        /// <summary>按序号选中并关闭列表。</summary>
        public void Select(int index)
        {
            SetValue(index, true);
            Close();
        }

        private void OnPopoverClosed()
        {
            popover = null;
            rows.Clear();
        }

        private void RefreshLabel()
        {
            TMP_Text target = Label;
            if (target == null) return;
            target.text = value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        private void BuildRows(RectTransform content)
        {
            rows.Clear();
            if (content == null) return;

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                Color background = index == value
                    ? GearOverlayTheme.Get("OverlayAccent", GearOverlayTheme.Accent)
                    : GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised);

                ImageEx row = GearOverlayLayer.CreateRoundedPanel(
                    content, "Option " + index, background, 6f, true);
                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = itemHeight;
                element.minHeight = itemHeight;

                TextMeshProUGUI text = GearOverlayLayer.CreateText(
                    row.transform, "Label", 14f,
                    index == value ? Color.white : GearOverlayTheme.TextPrimary, false,
                    TextAlignmentOptions.Left);
                text.text = options[index];
                text.raycastTarget = false;
                GearOverlayLayer.Stretch(text.rectTransform);
                RectTransform textRect = text.rectTransform;
                textRect.offsetMin = new Vector2(10f, 0f);

                Button rowButton = row.gameObject.AddComponent<Button>();
                rowButton.transition = Selectable.Transition.None;
                rowButton.onClick.AddListener(() => Select(index));
                rows.Add(rowButton);
            }

            if (EventSystem.current != null && rows.Count > 0)
            {
                int selected = Mathf.Clamp(value, 0, rows.Count - 1);
                EventSystem.current.SetSelectedGameObject(rows[selected].gameObject);
            }
        }
    }
}
