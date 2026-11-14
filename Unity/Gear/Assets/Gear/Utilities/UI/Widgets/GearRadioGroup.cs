using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 单选组：一组按钮里只允许一个选中；选中项可以换色（同类控件优先于 uGUI ToggleGroup 的地方）。
    /// <para>
    /// 只负责状态、事件与可选配色；按钮的点击、焦点、键盘导航仍走各自 Selectable。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Radio Group")]
    [DisallowMultipleComponent]
    public sealed class GearRadioGroup : MonoBehaviour
    {
        [Serializable]
        public sealed class Option
        {
            public Button button;
            public ImageEx visual;
        }

        [SerializeField] private List<Option> options = new List<Option>();
        [SerializeField] private int selectedIndex = -1;
        [SerializeField] private bool allowDeselect;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.25f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();

        private readonly List<UnityAction> handlers = new List<UnityAction>();
        private bool bound;

        public IReadOnlyList<Option> Options => options;
        public int SelectedIndex => selectedIndex;
        public UnityEvent<int> OnValueChanged => onValueChanged;

        private void OnEnable()
        {
            Bind();
            ApplyVisual();
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>重新绑定一组选项；selectedIndex 会被裁剪到有效范围。</summary>
        public void Configure(IList<Option> items, int selection = 0, bool notify = false)
        {
            Unbind();
            options.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null) options.Add(items[i]);
                }
            }

            selectedIndex = options.Count == 0 ? -1 : Mathf.Clamp(selection, -1, options.Count - 1);
            Bind();
            ApplyVisual();
            if (notify) onValueChanged?.Invoke(selectedIndex);
        }

        /// <summary>选中某一项；notify=true 时派发事件。</summary>
        public void Select(int index, bool notify = true)
        {
            if (options.Count == 0) return;
            index = Mathf.Clamp(index, -1, options.Count - 1);

            if (index == selectedIndex)
            {
                if (allowDeselect)
                {
                    index = -1;
                }
                else
                {
                    ApplyVisual();
                    return;
                }
            }

            selectedIndex = index;
            ApplyVisual();
            if (notify)
            {
                onValueChanged?.Invoke(selectedIndex);
                if (selectedIndex >= 0 && options[selectedIndex].button != null)
                    options[selectedIndex].button.Select();
            }
        }

        private void Bind()
        {
            if (bound) return;
            handlers.Clear();

            for (int i = 0; i < options.Count; i++)
            {
                Option option = options[i];
                if (option?.button == null)
                {
                    handlers.Add(null);
                    continue;
                }

                int index = i;
                UnityAction handler = () => Select(index);
                handlers.Add(handler);
                option.button.onClick.AddListener(handler);
            }

            bound = true;
        }

        private void Unbind()
        {
            if (!bound) return;

            for (int i = 0; i < options.Count && i < handlers.Count; i++)
            {
                Option option = options[i];
                if (option?.button != null && handlers[i] != null)
                    option.button.onClick.RemoveListener(handlers[i]);
            }

            handlers.Clear();
            bound = false;
        }

        private void ApplyVisual()
        {
            for (int i = 0; i < options.Count; i++)
            {
                Option option = options[i];
                if (option?.visual == null) continue;
                option.visual.color = i == selectedIndex ? selectedColor : normalColor;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (options.Count == 0) selectedIndex = -1;
            else selectedIndex = Mathf.Clamp(selectedIndex, -1, options.Count - 1);
            ApplyVisual();
        }
#endif
    }
}
