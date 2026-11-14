using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>五态交互模型，语义参考通用 UI 组件状态机（Gear 自研实现）。</summary>
    public enum GearInteractionState
    {
        Disabled = 0,
        Normal = 1,
        Highlighted = 2,
        Pressed = 3,
        Selected = 4,
    }

    [Serializable]
    public sealed class GearInteractionStateEvent : UnityEvent<GearInteractionState> { }

    /// <summary>
    /// 给任意 Graphic / Selectable 提供统一的五态模型：Disabled / Normal / Highlighted / Pressed / Selected。
    /// <para>
    /// 只负责“状态和事件”，不直接改颜色或缩放；视觉由 <c>GearInteractiveTint</c>、
    /// ButtonAni、BorderGlow 等效果订阅 <see cref="StateChanged"/> 实现。
    /// </para>
    /// <para>
    /// 与 <c>ButtonAni</c> 的关系：ButtonAni 是 Button 专用、重写 DoStateTransition 的完整视觉实现；
    /// 本组件用于非 Button 的 ImageEx/TMP/自绘 Graphic，避免为了 hover/press 状态强行挂 Button。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Interactive")]
    [DisallowMultipleComponent]
    public sealed class GearInteractive : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Selectable target;
        [SerializeField] private GearInteractionStateEvent onStateChanged = new GearInteractionStateEvent();

        private bool pointerInside;
        private bool pointerDown;
        private bool selected;
        private bool? lastInteractable;

        public GearInteractionState State { get; private set; } = GearInteractionState.Normal;

        public Selectable Target
        {
            get
            {
                if (target == null) target = GetComponent<Selectable>();
                return target;
            }
            set
            {
                target = value;
                Refresh(true);
            }
        }

        public bool IsInteractable => target == null || target.IsInteractable();

        public event Action<GearInteractionState> StateChanged;

        private void OnEnable()
        {
            pointerInside = false;
            pointerDown = false;
            selected = false;
            lastInteractable = null;
            Refresh(true);
        }

        private void OnDisable()
        {
            pointerInside = false;
            pointerDown = false;
            selected = false;
        }

        private void Update()
        {
            bool interactable = IsInteractable;
            if (lastInteractable != interactable)
            {
                lastInteractable = interactable;
                Refresh();
            }
        }

        /// <summary>重新计算状态；force=true 时即使状态相同也派发一次事件。</summary>
        public void Refresh(bool force = false)
        {
            GearInteractionState next = ResolveState();
            if (!force && next == State) return;

            State = next;
            lastInteractable = IsInteractable;
            onStateChanged?.Invoke(next);
            StateChanged?.Invoke(next);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerDown = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            pointerDown = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerDown = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            Refresh();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Refresh();
        }

        private GearInteractionState ResolveState()
        {
            if (!IsInteractable) return GearInteractionState.Disabled;
            if (pointerDown) return GearInteractionState.Pressed;
            if (selected) return GearInteractionState.Selected;
            if (pointerInside) return GearInteractionState.Highlighted;
            return GearInteractionState.Normal;
        }
    }
}
