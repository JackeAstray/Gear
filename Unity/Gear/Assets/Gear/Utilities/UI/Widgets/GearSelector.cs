using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 分段选择器：一组按钮只选一个，指示器在选项之间滑动，颜色随选中态切换。
    /// </summary>
    [AddComponentMenu("UI/Gear/Selector")]
    [DisallowMultipleComponent]
    public sealed class GearSelector : MonoBehaviour
    {
        [Serializable]
        public sealed class Option
        {
            public Button button;
            public Graphic visual;
            public RectTransform indicatorTarget;
        }

        [SerializeField] private List<Option> options = new List<Option>();
        [SerializeField] private RectTransform indicator;
        [SerializeField] private int selectedIndex;
        [SerializeField, Min(0f)] private float indicatorDuration = 0.18f;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.25f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField] private UnityEvent<int> onValueChanged = new UnityEvent<int>();

        private readonly List<UnityAction> handlers = new List<UnityAction>();
        private bool bound;
        private Vector2 indicatorFrom;
        private Vector2 indicatorTo;
        private float indicatorElapsed;
        private bool indicatorMoving;

        public IReadOnlyList<Option> Options => options;
        public int SelectedIndex => selectedIndex;
        public UnityEvent<int> OnValueChanged => onValueChanged;

        public RectTransform Indicator
        {
            get => indicator;
            set { indicator = value; SnapIndicator(); }
        }

        private void OnEnable()
        {
            Bind();
            ApplyVisual();
            SnapIndicator();
        }

        private void OnDisable()
        {
            Unbind();
        }

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

            selectedIndex = options.Count == 0 ? -1 : Mathf.Clamp(selection, 0, options.Count - 1);
            Bind();
            ApplyVisual();
            SnapIndicator();
            if (notify) onValueChanged?.Invoke(selectedIndex);
        }

        public void Select(int index, bool notify = true)
        {
            if (options.Count == 0) return;
            index = Mathf.Clamp(index, 0, options.Count - 1);
            if (index == selectedIndex)
            {
                ApplyVisual();
                return;
            }

            selectedIndex = index;
            ApplyVisual();
            MoveIndicator();
            if (notify) onValueChanged?.Invoke(selectedIndex);
        }

        /// <summary>手动推进指示器动画，测试/外部时钟可用。</summary>
        public void Tick(float delta)
        {
            if (!indicatorMoving || indicator == null) return;
            indicatorElapsed += Mathf.Max(0f, delta);
            float t = indicatorDuration <= 0f ? 1f : Mathf.Clamp01(indicatorElapsed / indicatorDuration);
            indicator.anchoredPosition = Vector2.Lerp(indicatorFrom, indicatorTo, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) indicatorMoving = false;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
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
                if (option?.visual != null)
                    option.visual.color = i == selectedIndex ? selectedColor : normalColor;
            }
        }

        private void MoveIndicator()
        {
            if (indicator == null) return;
            indicatorFrom = indicator.anchoredPosition;
            indicatorTo = GetTargetPosition();
            indicatorElapsed = 0f;
            indicatorMoving = true;
            if (indicatorDuration <= 0f) Tick(0f);
        }

        private void SnapIndicator()
        {
            if (indicator == null) return;
            indicator.anchoredPosition = GetTargetPosition();
            indicatorMoving = false;
        }

        private Vector2 GetTargetPosition()
        {
            if (indicator == null || selectedIndex < 0 || selectedIndex >= options.Count) return Vector2.zero;
            RectTransform target = options[selectedIndex].indicatorTarget;
            if (target == null && options[selectedIndex].button != null)
                target = (RectTransform)options[selectedIndex].button.transform;
            if (target == null || indicator.parent == null) return Vector2.zero;

            Vector3 world = target.TransformPoint(target.rect.center);
            Vector3 local = indicator.parent.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            selectedIndex = options.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, options.Count - 1);
            ApplyVisual();
        }
#endif
    }
}
