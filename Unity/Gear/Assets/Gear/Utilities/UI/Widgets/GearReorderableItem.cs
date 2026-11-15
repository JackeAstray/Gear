using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>可重排列表的子项适配器：把拖拽事件转发给 owner。</summary>
    [AddComponentMenu("UI/Gear/Reorderable Item")]
    [DisallowMultipleComponent]
    public sealed class GearReorderableItem : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private GearReorderableList owner;

        public GearReorderableList Owner
        {
            get => owner;
            set => owner = value;
        }

        private void Awake()
        {
            if (owner == null) owner = GetComponentInParent<GearReorderableList>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner != null) owner.BeginDrag((RectTransform)transform);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (owner != null) owner.DragTo(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (owner != null) owner.EndDrag();
        }
    }
}
