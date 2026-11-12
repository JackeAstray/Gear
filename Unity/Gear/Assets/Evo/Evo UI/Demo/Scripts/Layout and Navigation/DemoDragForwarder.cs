using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Passes drags on this rect to a Rect Dragger while enabled, so it can join the dragger's Drag Sources at runtime.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoDragForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectDragger dragger;

        // A disabled component gets no pointer events, so turning it off is all it takes to stop forwarding
        public void OnPointerDown(PointerEventData eventData) => dragger.HandlePointerDown(eventData);

        public void OnDrag(PointerEventData eventData) => dragger.HandleDrag(eventData);

        public void OnPointerUp(PointerEventData eventData) => dragger.HandlePointerUp(eventData);

        void OnDisable() => dragger.StopDrag();
    }
}
