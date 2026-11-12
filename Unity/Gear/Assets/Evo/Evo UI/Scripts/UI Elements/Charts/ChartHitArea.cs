using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Runtime hit target of an interactable chart. Generates no geometry and only
    /// accepts the pointer over a chart point, so gaps pass through to the UI behind.
    /// </summary>
    [AddComponentMenu(""), RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChartHitArea : Graphic, ICanvasRaycastFilter,
        IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
    {
        // Set by the chart on every draw
        internal ChartBase chart;

        protected override void OnPopulateMesh(VertexHelper vertexHelper) => vertexHelper.Clear();

        protected override void UpdateMaterial() { }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => chart != null && chart.IsPointHit(screenPoint, eventCamera);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (chart != null)
                chart.MovePointer(eventData.position, eventData.enterEventCamera);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (chart != null)
                chart.MovePointer(eventData.position, eventData.enterEventCamera);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (chart != null)
                chart.ExitPointer();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (chart != null)
                chart.ClickPointer(eventData);
        }
    }
}
