using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 样例用：把右键点击转成回调，便于在没有额外输入模块的情况下打开 ContextMenu。
    /// </summary>
    public sealed class ShowcaseContextClick : MonoBehaviour, IPointerClickHandler
    {
        public event Action<PointerEventData> RightClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke(eventData);
        }
    }
}
