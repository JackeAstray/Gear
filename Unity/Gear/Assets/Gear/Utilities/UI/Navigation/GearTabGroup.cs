using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Navigation
{
    /// <summary>
    /// Tab 组：一组按钮切换一组页面，可选把焦点移到新页面的第一个可选元素。
    /// <para>
    /// 不依赖 <c>UISystem</c> 的具体窗口实现；只依赖 uGUI Button 与 EventSystem，
    /// 因此既能放在普通窗口里，也能放在 UI Example 的演示卡里。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Tab Group")]
    [DisallowMultipleComponent]
    public sealed class GearTabGroup : MonoBehaviour
    {
        [Serializable]
        public sealed class Tab
        {
            public string id;
            public Button button;
            public GameObject page;
            [Tooltip("切换后要选中的对象；为空时自动找页面里的第一个 Selectable")]
            public GameObject firstSelected;
        }

        [SerializeField] private List<Tab> tabs = new List<Tab>();
        [SerializeField] private string defaultTab;
        [SerializeField] private bool autoSelectOnEnable = true;
        [SerializeField] private bool moveFocusToPage = true;

        [SerializeField] private UnityEvent<string> onTabChanged = new UnityEvent<string>();

        private readonly List<(Button button, UnityAction action)> bindings =
            new List<(Button, UnityAction)>();

        /// <summary>当前 Tab id；未选中时为 null。</summary>
        public string Current { get; private set; }

        public IReadOnlyList<Tab> Tabs => tabs;

        public event Action<string> TabChanged;

        private void OnEnable()
        {
            Bind();
            if (autoSelectOnEnable)
                Select(string.IsNullOrEmpty(defaultTab) ? (tabs.Count > 0 ? tabs[0].id : null) : defaultTab);
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>运行时配置 Tab 列表；会重新绑定按钮并尽量保留当前 Tab。</summary>
        public void Configure(IList<Tab> items)
        {
            string previous = Current;
            Unbind();
            tabs.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                        tabs.Add(items[i]);
                }
            }

            if (isActiveAndEnabled)
                Bind();

            if (!string.IsNullOrEmpty(previous) && Find(previous) != null)
                Select(previous);
        }

        /// <summary>按 id 选中 Tab；不存在时忽略并告警。</summary>
        public void Select(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            Tab target = Find(id);
            if (target == null)
            {
                Debug.LogWarning($"[GearTabGroup] 找不到 Tab id：{id}", this);
                return;
            }

            bool changed = Current != id;
            Current = id;

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab.page != null && tab.page.activeSelf != (tab.id == id))
                    tab.page.SetActive(tab.id == id);
            }

            if (moveFocusToPage)
                MoveFocus(target);

            if (changed)
            {
                onTabChanged?.Invoke(id);
                TabChanged?.Invoke(id);
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= tabs.Count) return;
            Select(tabs[index].id);
        }

        /// <summary>重新绑定按钮并刷新当前页。</summary>
        public void Refresh()
        {
            Unbind();
            Bind();
            if (!string.IsNullOrEmpty(Current))
                Select(Current);
        }

        private void Bind()
        {
            Unbind();
            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab.button == null || string.IsNullOrEmpty(tab.id)) continue;

                string id = tab.id;
                UnityAction action = () => Select(id);
                tab.button.onClick.AddListener(action);
                bindings.Add((tab.button, action));
            }
        }

        private void Unbind()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].button != null)
                    bindings[i].button.onClick.RemoveListener(bindings[i].action);
            }

            bindings.Clear();
        }

        private Tab Find(string id)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] != null && tabs[i].id == id)
                    return tabs[i];
            }

            return null;
        }

        private void MoveFocus(Tab tab)
        {
            if (EventSystem.current == null || tab.page == null) return;

            GameObject focus = tab.firstSelected;
            if (focus == null)
            {
                Selectable selectable = tab.page.GetComponentInChildren<Selectable>(true);
                if (selectable != null) focus = selectable.gameObject;
            }

            if (focus != null)
                EventSystem.current.SetSelectedGameObject(focus);
        }
    }
}
