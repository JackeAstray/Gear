using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A shape on the Menu Bar tab's canvas: click to select it, drag to move it. Snap to grid rounds the drag.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSceneObject : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private ProceduralShape shape;
        [SerializeField] private GameObject selection;

        DemoMenuBar owner;
        RectTransform rect;
        Vector2 dragStart, pointerStart;
        TweenHandle moveTween;

        public DemoMenuBar.SceneObject Data { get; private set; }
        public Vector2 Position => new(rect.anchoredPosition.x, -rect.anchoredPosition.y);

        public void Setup(DemoMenuBar menuBar, DemoMenuBar.SceneObject data)
        {
            owner = menuBar;
            Data = data;
            rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = data.size;
            rect.anchoredPosition = new Vector2(data.position.x, -data.position.y);
            name = data.name;

            shape.UniformCornerRadius = data.radius;
            shape.fillStyler.color.stylerID = data.accent ? "Accent (Soft)" : "Background (Tint)";
            shape.UpdateStyler();
            SetSelected(false);
        }

        public void SetSelected(bool value) => selection.SetActive(value);

        public void MoveTo(Vector2 position, bool animate)
        {
            moveTween.Kill();
            Vector2 target = new(position.x, -position.y);
            if (!animate)
            {
                rect.anchoredPosition = target;
                return;
            }

            moveTween = EvoTween.Create(rect.anchoredPosition, target, 0.2f).WithEase(Ease.OutCubic).BindToAnchoredPosition(rect);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                owner.Select(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            moveTween.Kill();
            dragStart = Position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.World, eventData.position, eventData.pressEventCamera, out pointerStart);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.World, eventData.position, eventData.pressEventCamera, out Vector2 pointer);
            Vector2 position = dragStart + new Vector2(pointer.x - pointerStart.x, pointerStart.y - pointer.y);

            if (owner.Snap)
                position = new Vector2(Mathf.Round(position.x / owner.SnapStep), Mathf.Round(position.y / owner.SnapStep)) * owner.SnapStep;

            // Keep at least half of the shape on the canvas
            Rect bounds = owner.World.rect;
            position.x = Mathf.Clamp(position.x, -Data.size.x * 0.5f, bounds.width - Data.size.x * 0.5f);
            position.y = Mathf.Clamp(position.y, -Data.size.y * 0.5f, bounds.height - Data.size.y * 0.5f);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 end = Position;
            if (end != dragStart)
                owner.RecordMove(this, dragStart, end);
        }
    }
}
