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
    /// 多选下拉：点击字段弹出 <see cref="GearPopover"/> 选项列表，勾选集合后点「确认」提交。
    /// <para>
    /// 对外给掩码（前 31 项）+ 选中序号列表；未点确认前关闭弹层不会回滚已勾选项，
    /// 需要「取消即回滚」时先 <see cref="SetSelected"/> 记录快照再恢复。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Multi Select")]
    [DisallowMultipleComponent]
    public sealed class GearMultiSelect : MonoBehaviour
    {
        private const int MaskLimit = 31;

        private struct RowVisual
        {
            public ImageEx background;
            public ImageEx check;
        }

        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;
        [SerializeField] private List<string> options = new List<string>();
        [SerializeField] private List<bool> selected = new List<bool>();
        [SerializeField] private int maxSelection;
        [SerializeField, Min(12f)] private float itemHeight = 34f;
        [SerializeField, Min(40f)] private float popupWidth = 220f;
        [SerializeField, Min(40f)] private float maxHeight = 280f;
        [SerializeField] private string labelFormat = "{0} 项已选";
        [SerializeField] private string emptyText = "请选择";
        [SerializeField] private UnityEvent<int> onSelectionChanged = new UnityEvent<int>();
        [SerializeField] private UnityEvent<int> onConfirmed = new UnityEvent<int>();

        private GearPopover popover;
        private readonly List<RowVisual> rows = new List<RowVisual>();

        public UnityEvent<int> OnSelectionChanged => onSelectionChanged;
        public UnityEvent<int> OnConfirmed => onConfirmed;
        public event Action<int> SelectionChanged;
        public event Action<int> Confirmed;

        public IReadOnlyList<string> Options => options;
        public bool IsOpen => popover != null;

        /// <summary>选中项掩码（前 31 项）。</summary>
        public int Mask
        {
            get
            {
                int mask = 0;
                int limit = Mathf.Min(options.Count, MaskLimit);
                for (int i = 0; i < limit; i++)
                {
                    if (IsSelected(i)) mask |= 1 << i;
                }

                return mask;
            }
        }

        public int SelectedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < selected.Count; i++) if (selected[i]) count++;
                return count;
            }
        }

        /// <summary>选中序号（升序）。</summary>
        public List<int> SelectedIndices
        {
            get
            {
                var result = new List<int>();
                for (int i = 0; i < selected.Count; i++) if (selected[i]) result.Add(i);
                return result;
            }
        }

        /// <summary>当前应显示在字段上的文本。</summary>
        public string LabelText
        {
            get
            {
                int count = SelectedCount;
                if (count == 0) return emptyText;
                return string.IsNullOrEmpty(labelFormat) ? count.ToString() : string.Format(labelFormat, count);
            }
        }

        public TMP_Text Label
        {
            get
            {
                if (label == null) label = GetComponentInChildren<TMP_Text>(true);
                return label;
            }
            set { label = value; RefreshLabel(); }
        }

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

        public int MaxSelection { get => maxSelection; set => maxSelection = Mathf.Max(0, value); }

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
            trigger.onClick.RemoveListener(TogglePopover);
            trigger.onClick.AddListener(TogglePopover);
        }

        private void Unbind()
        {
            if (button != null) button.onClick.RemoveListener(TogglePopover);
        }

        /// <summary>替换选项；clearSelection=false 时尽量保留原勾选。</summary>
        public void SetOptions(IList<string> items, bool clearSelection = true)
        {
            options.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++) options.Add(items[i] ?? string.Empty);
            }

            while (selected.Count < options.Count) selected.Add(false);
            while (selected.Count > options.Count) selected.RemoveAt(selected.Count - 1);

            if (clearSelection)
            {
                for (int i = 0; i < selected.Count; i++) selected[i] = false;
            }

            RefreshLabel();
            RefreshRows();
        }

        public bool IsSelected(int index)
        {
            return index >= 0 && index < selected.Count && selected[index];
        }

        /// <summary>设置某一项；超上限或越界返回 false。</summary>
        public bool SetSelected(int index, bool value, bool notify = true)
        {
            if (index < 0 || index >= options.Count) return false;
            if (value && maxSelection > 0 && SelectedCount >= maxSelection && !IsSelected(index)) return false;

            while (selected.Count < options.Count) selected.Add(false);
            if (selected[index] == value)
            {
                RefreshRows();
                return true;
            }

            selected[index] = value;
            RefreshLabel();
            RefreshRows();
            if (notify) NotifySelection();
            return true;
        }

        public void Toggle(int index, bool notify = true)
        {
            SetSelected(index, !IsSelected(index), notify);
        }

        public void SelectAll(bool notify = true)
        {
            int limit = maxSelection > 0 ? Mathf.Min(maxSelection, options.Count) : options.Count;
            for (int i = 0; i < options.Count; i++) selected[i] = i < limit;
            RefreshLabel();
            RefreshRows();
            if (notify) NotifySelection();
        }

        public void ClearSelection(bool notify = true)
        {
            for (int i = 0; i < selected.Count; i++) selected[i] = false;
            RefreshLabel();
            RefreshRows();
            if (notify) NotifySelection();
        }

        /// <summary>确认当前选择：派发掩码并关闭弹层。</summary>
        public void Confirm()
        {
            Close();
            onConfirmed?.Invoke(Mask);
            Confirmed?.Invoke(Mask);
        }

        public void TogglePopover()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (options.Count == 0 || popover != null) return;

            float height = Mathf.Min(maxHeight, options.Count * itemHeight + 52f);
            var anchor = (RectTransform)transform;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(anchor.lossyScale.y));
            float halfWidth = anchor.rect.width * 0.5f;
            float halfHeight = anchor.rect.height * 0.5f + 6f;

            Vector2 offset = new Vector2(-halfWidth, -halfHeight);

            RectTransform root = GearOverlayLayer.Root;
            if (root != null)
            {
                Vector3[] anchorCorners = new Vector3[4];
                anchor.GetWorldCorners(anchorCorners);
                Vector3[] rootCorners = new Vector3[4];
                root.GetWorldCorners(rootCorners);
                float needed = height * scaleY + 8f;
                if (anchorCorners[0].y - needed < rootCorners[0].y)
                    offset.y = (halfHeight + height) * scaleY;
            }

            popover = GearPopover.Show(anchor, new Vector2(Mathf.Max(popupWidth, anchor.rect.width), height),
                BuildRows, offset);
            popover.SetOnClose(OnPopoverClosed);
        }

        public void Close()
        {
            if (popover == null) return;
            GearPopover current = popover;
            popover = null;
            rows.Clear();
            current.Close();
        }

        private void OnPopoverClosed()
        {
            popover = null;
            rows.Clear();
        }

        private void RefreshLabel()
        {
            TMP_Text target = Label;
            if (target != null) target.text = LabelText;
        }

        private void NotifySelection()
        {
            int mask = Mask;
            onSelectionChanged?.Invoke(mask);
            SelectionChanged?.Invoke(mask);
        }

        private void RefreshRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].background == null) continue;
                bool on = IsSelected(i);
                rows[i].background.color = on
                    ? GearOverlayTheme.Get("OverlayAccent", GearOverlayTheme.Accent)
                    : GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised);
                if (rows[i].check != null) rows[i].check.gameObject.SetActive(on);
            }
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
                ImageEx row = GearOverlayLayer.CreateRoundedPanel(
                    content, "Option " + index,
                    IsSelected(index)
                        ? GearOverlayTheme.Get("OverlayAccent", GearOverlayTheme.Accent)
                        : GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised),
                    6f, true);

                LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = itemHeight;
                element.minHeight = itemHeight;

                TextMeshProUGUI text = GearOverlayLayer.CreateText(
                    row.transform, "Label", 14f, Color.white, false, TextAlignmentOptions.Left);
                text.text = options[index];
                text.raycastTarget = false;
                GearOverlayLayer.Stretch(text.rectTransform);
                RectTransform textRect = text.rectTransform;
                textRect.offsetMin = new Vector2(10f, 0f);
                textRect.offsetMax = new Vector2(-30f, 0f);

                ImageEx check = GearOverlayLayer.CreateRoundedPanel(
                    row.transform, "Check", Color.white, 4f, false);
                RectTransform checkRect = check.rectTransform;
                checkRect.anchorMin = new Vector2(1f, 0.5f);
                checkRect.anchorMax = new Vector2(1f, 0.5f);
                checkRect.sizeDelta = new Vector2(14f, 14f);
                checkRect.anchoredPosition = new Vector2(-14f, 0f);
                check.gameObject.SetActive(IsSelected(index));

                Button rowButton = row.gameObject.AddComponent<Button>();
                rowButton.transition = Selectable.Transition.None;
                rowButton.onClick.AddListener(() => Toggle(index));

                rows.Add(new RowVisual { background = row, check = check });
            }

            BuildFooter(content);

            if (EventSystem.current != null && rows.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(rows[0].background.gameObject);
            }
        }

        private void BuildFooter(RectTransform content)
        {
            ImageEx confirmRow = GearOverlayLayer.CreateRoundedPanel(
                content, "Confirm", GearOverlayTheme.Get("OverlayAccent", GearOverlayTheme.Accent), 6f, true);
            LayoutElement element = confirmRow.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;
            element.minHeight = 34f;

            TextMeshProUGUI text = GearOverlayLayer.CreateText(
                confirmRow.transform, "Label", 14f, Color.white, true, TextAlignmentOptions.Center);
            text.text = "确认";
            GearOverlayLayer.Stretch(text.rectTransform);

            Button confirm = confirmRow.gameObject.AddComponent<Button>();
            confirm.transition = Selectable.Transition.None;
            confirm.onClick.AddListener(Confirm);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            itemHeight = Mathf.Max(12f, itemHeight);
            popupWidth = Mathf.Max(40f, popupWidth);
            maxHeight = Mathf.Max(40f, maxHeight);
            maxSelection = Mathf.Max(0, maxSelection);
            while (selected.Count < options.Count) selected.Add(false);
            while (selected.Count > options.Count) selected.RemoveAt(selected.Count - 1);
            RefreshLabel();
        }
#endif
    }
}
