using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The resize handle on the Flexible Grid panel. Its Interactive lights the pill while hovered or held.
    /// The panel grows from its center, so the width changes by twice the pointer's movement and the handle stays under it.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoGridHandle : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private DemoFlexibleGrid owner;

        float startWidth;
        Vector2 startPointer;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            startWidth = owner.Width;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.Panel, eventData.position, eventData.pressEventCamera, out startPointer);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.Panel, eventData.position, eventData.pressEventCamera, out Vector2 pointer);
            owner.SetWidth(startWidth + (pointer.x - startPointer.x) * 2f);
        }
    }
}
