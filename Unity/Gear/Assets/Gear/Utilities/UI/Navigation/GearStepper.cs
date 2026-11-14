using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Navigation
{
    /// <summary>
    /// 步骤流：一组页面按顺序前进/后退，可挂 Next/Back 按钮与步骤指示器，并支持校验门。
    /// <para>
    /// 适合首次启动向导、分步设置、结算流程。不依赖具体窗口实现，只依赖 uGUI Button 与 EventSystem。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Stepper")]
    [DisallowMultipleComponent]
    public sealed class GearStepper : MonoBehaviour
    {
        [Serializable]
        public sealed class Step
        {
            public string id;
            public string title;
            public GameObject page;
            public Button indicator;
            [Tooltip("进入该步后要选中的对象；为空时自动找页面里的第一个 Selectable")]
            public GameObject firstSelected;
        }

        [SerializeField] private List<Step> steps = new List<Step>();
        [SerializeField][Min(0)] private int startIndex;
        [SerializeField] private bool allowBack = true;
        [SerializeField] private bool allowIndicatorNavigation = true;
        [SerializeField] private bool moveFocusToPage = true;
        [SerializeField] private bool canContinue = true;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;

        [SerializeField] private UnityEvent<int> onStepChanged = new UnityEvent<int>();
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private readonly List<(Button button, UnityAction action)> bindings =
            new List<(Button, UnityAction)>();

        public int CurrentIndex { get; private set; } = -1;
        public Step Current => CurrentIndex >= 0 && CurrentIndex < steps.Count ? steps[CurrentIndex] : null;
        public IReadOnlyList<Step> Steps => steps;

        /// <summary>校验门：false 时 Next 不前进；可随时由表单校验/业务状态切换。</summary>
        public bool CanContinue
        {
            get => canContinue;
            set => canContinue = value;
        }

        public Button NextButton { get => nextButton; set => nextButton = value; }
        public Button BackButton { get => backButton; set => backButton = value; }

        public event Action<int> StepChanged;
        public event Action Completed;

        private void OnEnable()
        {
            Bind();
            if (CurrentIndex < 0 && steps.Count > 0)
                GoTo(Mathf.Clamp(startIndex, 0, steps.Count - 1));
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>运行时配置步骤；会重新绑定按钮并跳到起始步骤。</summary>
        public void Configure(IList<Step> items)
        {
            Unbind();
            steps.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                        steps.Add(items[i]);
                }
            }

            CurrentIndex = -1;
            if (isActiveAndEnabled)
            {
                Bind();
                if (steps.Count > 0)
                    GoTo(Mathf.Clamp(startIndex, 0, steps.Count - 1));
            }
        }

        /// <summary>前进一步；校验门未通过时不动，最后一步触发完成事件。</summary>
        public void Next()
        {
            if (!canContinue) return;

            if (CurrentIndex + 1 < steps.Count)
            {
                GoTo(CurrentIndex + 1);
                return;
            }

            onCompleted?.Invoke();
            Completed?.Invoke();
        }

        /// <summary>后退一步；第一步或禁止后退时不动。</summary>
        public void Back()
        {
            if (!allowBack || CurrentIndex <= 0) return;
            GoTo(CurrentIndex - 1);
        }

        /// <summary>跳到指定步骤；越界忽略。</summary>
        public void GoTo(int index)
        {
            if (index < 0 || index >= steps.Count) return;

            bool changed = CurrentIndex != index;
            CurrentIndex = index;

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                if (step.page != null && step.page.activeSelf != (i == index))
                    step.page.SetActive(i == index);
                if (step.indicator != null)
                    step.indicator.interactable = allowIndicatorNavigation;
            }

            if (moveFocusToPage)
                MoveFocus(steps[index]);

            if (changed)
            {
                onStepChanged?.Invoke(index);
                StepChanged?.Invoke(index);
            }
        }

        public void GoTo(string id)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].id == id)
                {
                    GoTo(i);
                    return;
                }
            }
        }

        /// <summary>重新绑定按钮并刷新当前步骤。</summary>
        public void Refresh()
        {
            Unbind();
            Bind();
            if (CurrentIndex >= 0)
                GoTo(CurrentIndex);
        }

        private void Bind()
        {
            Unbind();

            if (nextButton != null)
            {
                UnityAction next = Next;
                nextButton.onClick.AddListener(next);
                bindings.Add((nextButton, next));
            }

            if (backButton != null)
            {
                UnityAction back = Back;
                backButton.onClick.AddListener(back);
                bindings.Add((backButton, back));
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                if (step.indicator == null || !allowIndicatorNavigation) continue;
                int index = i;
                UnityAction action = () => GoTo(index);
                step.indicator.onClick.AddListener(action);
                bindings.Add((step.indicator, action));
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

        private void MoveFocus(Step step)
        {
            if (EventSystem.current == null || step == null || step.page == null) return;

            GameObject focus = step.firstSelected;
            if (focus == null)
            {
                Selectable selectable = step.page.GetComponentInChildren<Selectable>(true);
                if (selectable != null) focus = selectable.gameObject;
            }

            if (focus != null)
                EventSystem.current.SetSelectedGameObject(focus);
        }
    }
}
