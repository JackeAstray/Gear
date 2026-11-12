using System.Collections.Generic;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Marquee Selection tab: a backpack and a stash, each a Marquee Selection over a grid of items.
    /// Transfer lets a dragged selection move into the other container; the capacity dots follow when the drag ends.
    /// Items fill the slots in order, and after a drag they glide from where they were let go into their slots.
    /// While a selection is dragged, its container comes to the front and the container under the pointer that takes it lights up.
    /// A press outside a container clears its selection, so picks don't stay behind after clicking somewhere else.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoMarquee : MonoBehaviour
    {
        [SerializeField] private MarqueeSelection[] containers;
        [Tooltip("Each container's panel, in the same order. It lights up while it can take a dragged selection.")]
        [SerializeField] private ProceduralShape[] panels;
        [Tooltip("One row of capacity dots per container, in the same order.")]
        [SerializeField] private DemoDotRow[] capacityDots;
        [Tooltip("The root canvas, for its event camera.")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private int capacity = 9;
        [SerializeField] private int columns = 3;
        [SerializeField] private float cellSize = 104f;
        [SerializeField] private float spacing = 12f;
        [SerializeField] private float glideDuration = 0.3f;
        [SerializeField] private string dropOutline = "Accent (Tint)";

        [Header("Options")]
        [SerializeField] private CanvasGroup transferOption;
        [SerializeField] private CanvasGroup keepInsideOption;

        readonly Dictionary<RectTransform, Vector3> released = new();
        readonly List<TweenHandle> glides = new();
        string idleOutline;
        MarqueeSelection dropTarget;

        void Awake()
        {
            idleOutline = panels[0].outlineStyler.color.stylerID;

            foreach (MarqueeSelection container in containers)
            {
                MarqueeSelection source = container;
                container.onDragStarted.AddListener(() => OnDragStarted(source));
                container.onDragEnded.AddListener(OnDragEnded);
                Arrange(container, false);
            }

            DemoUtils.SetLocked(keepInsideOption, containers[0].transferMode != MarqueeSelection.TransferMode.None, false);
            UpdateCounts();
        }

        // A cancelled drag puts the items back before its end event, so the glide starts from the last dragged position
        void LateUpdate()
        {
            if (Utilities.WasPointerPressed())
                ClearOutside();

            MarqueeSelection target = null;
            foreach (MarqueeSelection container in containers)
            {
                if (!container.IsDragging)
                    continue;

                for (int i = 0; i < container.SelectionCount; i++)
                {
                    RectTransform item = container.Selection[i].RectTransform;
                    released[item] = item.position;
                }

                foreach (MarqueeSelection other in containers)
                {
                    if (container.CanTransferTo(other) && IsUnderPointer(other))
                        target = other;
                }
            }

            SetDropTarget(target);
        }

        // A press inside a container is its own to handle: it picks an item there, or clears it from the empty space
        void ClearOutside()
        {
            foreach (MarqueeSelection container in containers)
            {
                if (container.SelectionCount > 0 && !IsUnderPointer(container))
                    container.ClearSelection();
            }
        }

        bool IsUnderPointer(MarqueeSelection container)
        {
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(container.RectTransform, Utilities.GetPointerPosition(), eventCamera);
        }

        void SetDropTarget(MarqueeSelection target)
        {
            if (target == dropTarget)
                return;

            dropTarget = target;
            for (int i = 0; i < containers.Length; i++)
            {
                panels[i].outlineStyler.color.stylerID = containers[i] == target ? dropOutline : idleOutline;
                panels[i].UpdateStyler();
            }
        }

        // A full container turns the drop down, and the selection goes back where it came from
        void OnDragStarted(MarqueeSelection source)
        {
            foreach (MarqueeSelection container in containers)
            {
                if (container != source)
                    container.acceptTransfers = container.Content.childCount + source.SelectionCount <= capacity;
            }

            foreach (TweenHandle glide in glides)
                glide.Kill();

            glides.Clear();
            // The dragged items stay inside their container, so it draws over the other one while they cross it
            source.transform.SetAsLastSibling();
        }

        void OnDragEnded()
        {
            SetDropTarget(null);
            foreach (MarqueeSelection container in containers)
                Arrange(container, true);

            released.Clear();
            UpdateCounts();
        }

        void Arrange(MarqueeSelection container, bool animate)
        {
            RectTransform content = container.Content;
            float step = cellSize + spacing;

            for (int i = 0; i < content.childCount; i++)
            {
                var item = (RectTransform)content.GetChild(i);
                Vector3 from = released.TryGetValue(item, out Vector3 position) ? position : item.position;
                item.anchorMin = item.anchorMax = new Vector2(0f, 1f);
                item.position = from;

                Vector2 start = item.anchoredPosition;
                Vector2 slot = new(cellSize * 0.5f + i % columns * step, -(cellSize * 0.5f + i / columns * step));
                if (!animate || (start - slot).sqrMagnitude < 0.25f)
                {
                    item.anchoredPosition = slot;
                    continue;
                }

                glides.Add(EvoTween.Create(start, slot, glideDuration).WithEase(Ease.OutCubic).BindToAnchoredPosition(item));
            }
        }

        void UpdateCounts()
        {
            for (int i = 0; i < containers.Length; i++)
                DemoUtils.SetDots(capacityDots[i], containers[i].Content.childCount);
        }

        public void SetModifiers(bool value)
        {
            foreach (MarqueeSelection container in containers)
                container.allowModifiers = value;
        }

        public void SetDragItems(bool value)
        {
            foreach (MarqueeSelection container in containers)
                container.allowDrag = value;

            DemoUtils.SetLocked(transferOption, !value);
            DemoUtils.SetLocked(keepInsideOption, !value || containers[0].transferMode != MarqueeSelection.TransferMode.None);
        }

        // Keep Inside only holds a group that can't leave for the other container
        public void SetTransfer(bool value)
        {
            foreach (MarqueeSelection container in containers)
                container.transferMode = value ? MarqueeSelection.TransferMode.AnyContainer : MarqueeSelection.TransferMode.None;

            DemoUtils.SetLocked(keepInsideOption, value || !containers[0].allowDrag);
        }

        public void SetKeepInside(bool value)
        {
            foreach (MarqueeSelection container in containers)
                container.constrainMovement = value;
        }

        public void SetDashSpeed(float value)
        {
            foreach (MarqueeSelection container in containers)
                container.dashSpeed = value;
        }

        public void SetFadeOut(float value)
        {
            foreach (MarqueeSelection container in containers)
                container.fadeDuration = value;
        }
    }
}
