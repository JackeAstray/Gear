using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Marquee Selectable"), HelpURL(Constants.HelpUrl)]
    public class MarqueeSelectable : MonoBehaviour, IMarqueeSelectable, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter, ILayoutSelfController
    {
        // Settings
        [SerializeField] private bool interactable = true;
        public bool draggable = true;

        // Selection Visual
        public VisualType selectionVisual = VisualType.ProceduralShape;
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private ProceduralShape targetShape;
        [SerializeField] private Color selectedColor = new(0.22f, 0.56f, 1f, 0.25f);
        [SerializeField] private Color selectedOutlineColor = new(0.35f, 0.68f, 1f, 1f);
        [SerializeField, Min(0f)] private float selectedOutlineWidth = 2f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;

        // Events
        public UnityEvent onSelected = new();
        public UnityEvent onDeselected = new();

        // Enums
        public enum VisualType { None, Graphic, ProceduralShape }

        // Public Properties
        public bool IsSelected => selectionIndex >= 0;
        public bool Interactable => interactable && groupsInteractable && isActiveAndEnabled;
        public RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;
        public MarqueeSelection Container { get; private set; }
        public bool IsDragging { get; internal set; }

        // State
        RectTransform rectTransform;
        internal int selectionIndex = -1;
        internal Vector3 dragPosition;
        bool groupsInteractable = true;
        bool hasVisualCache;
        VisualType cachedVisual;
        Color normalColor;
        Color normalOutlineColor;
        float normalOutlineWidth;
        float visualAmount;
        ProceduralShape.ColorMode normalFillMode;
        ProceduralShape.ColorMode normalOutlineMode;
        TweenHandle visualTween;
        Action<float> updateVisual;
        readonly List<CanvasGroup> groups = new(4);

        void Awake()
        {
            rectTransform = (RectTransform)transform;
            updateVisual = UpdateVisual;

            if (targetShape == null)
                TryGetComponent(out targetShape);
            if (targetGraphic == null)
                TryGetComponent(out targetGraphic);

            RaycastArea.ResolveRaycast(gameObject);
        }

        void OnEnable()
        {
            RefreshContainer();
            OnCanvasGroupChanged();
        }

        void OnDisable()
        {
            if (Container != null)
                Container.Unregister(this);

            Container = null;
            visualTween.Kill();
            UpdateVisual(0f);
        }

        void OnTransformParentChanged() => RefreshContainer();

        void OnCanvasGroupChanged()
        {
            groupsInteractable = true;
            Transform current = transform;
            while (current != null)
            {
                current.GetComponents(groups);
                bool stop = false;

                for (int i = 0; i < groups.Count; i++)
                {
                    if (!groups[i].enabled)
                        continue;
                    if (!groups[i].interactable)
                        groupsInteractable = false;

                    stop |= groups[i].ignoreParentGroups;
                }

                if (stop || !groupsInteractable)
                    break;

                current = current.parent;
            }
            groups.Clear();

            if (!Interactable)
                Deselect();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Container != null && Interactable)
                Container.BeginPointer(eventData, this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Container != null)
                Container.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Container != null)
                Container.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Container != null)
                Container.OnPointerUp(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Container != null)
                Container.OnEndDrag(eventData);
        }

        internal void SelectionChanged(bool selected)
        {
            visualTween.Kill();

            if (selected && !hasVisualCache)
                CacheVisual();

            float target = selected ? 1f : 0f;
        
            if (hasVisualCache && transitionDuration > 0f && Application.isPlaying && isActiveAndEnabled)
            {
                visualTween = EvoTween.Create(visualAmount, target, transitionDuration)
                    .WithTimeMode(TimeMode.Unscaled).WithEase(Ease.OutQuad).Bind(updateVisual);
            }
            else { UpdateVisual(target); }

            if (selected)
                onSelected?.Invoke();
            else
                onDeselected?.Invoke();
        }

        void CacheVisual()
        {
            cachedVisual = selectionVisual;
            if (cachedVisual == VisualType.ProceduralShape && targetShape != null)
            {
                normalColor = targetShape.FillColor;
                normalOutlineColor = targetShape.OutlineColor;
                normalOutlineWidth = targetShape.OutlineWidth;
                normalFillMode = targetShape.fillColorMode;
                normalOutlineMode = targetShape.outlineColorMode;
                targetShape.fillColorMode = ProceduralShape.ColorMode.Custom;
                targetShape.outlineColorMode = ProceduralShape.ColorMode.Custom;
                // Color-mode fields take effect on a material update; color setters skip unchanged values
                targetShape.SetMaterialDirty();
                hasVisualCache = true;
            }
            else if (cachedVisual == VisualType.Graphic && targetGraphic != null)
            {
                normalColor = targetGraphic.color;
                hasVisualCache = true;
            }
        }

        void UpdateVisual(float amount)
        {
            visualAmount = amount;

            if (!hasVisualCache)
                return;

            if (cachedVisual == VisualType.ProceduralShape && targetShape != null)
            {
                Color fill = normalFillMode == ProceduralShape.ColorMode.Base ? targetShape.color : normalColor;
                targetShape.FillColor = Color.LerpUnclamped(fill, selectedColor, amount);
                targetShape.OutlineColor = Color.LerpUnclamped(normalOutlineColor, selectedOutlineColor, amount);
                targetShape.OutlineWidth = Mathf.LerpUnclamped(normalOutlineWidth, selectedOutlineWidth, amount);
              
                if (amount <= 0f)
                {
                    targetShape.FillColor = normalColor;
                    targetShape.fillColorMode = normalFillMode;
                    targetShape.outlineColorMode = normalOutlineMode;
                    targetShape.SetMaterialDirty();
                }
            }
            else if (cachedVisual == VisualType.Graphic && targetGraphic != null)
                targetGraphic.color = Color.LerpUnclamped(normalColor, selectedColor, amount);

            if (amount <= 0f && !IsSelected)
                hasVisualCache = false;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => !IsDragging;

        // A layout pass keeps the dragged item's slot but leaves the item where the drag put it
        public void SetLayoutHorizontal() => HoldDragPosition();
        public void SetLayoutVertical() => HoldDragPosition();

        void HoldDragPosition()
        {
            if (IsDragging)
                RectTransform.position = dragPosition;
        }

        public void Select()
        {
            if (Container != null)
                Container.Select(this);
        }

        public void Deselect()
        {
            if (Container != null)
                Container.Deselect(this);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value)
                Deselect();
        }

        public void RefreshContainer()
        {
            if (!isActiveAndEnabled)
                return;

            MarqueeSelection parent = GetComponentInParent<MarqueeSelection>();
            if (parent == Container)
                return;

            if (Container != null)
                Container.Unregister(this);

            Container = parent;    
            if (Container != null)
                Container.Register(this);
        }
    }

    /// <summary>
    /// Public selection contract. The owning container keeps selection and visuals in sync.
    /// </summary>
    public interface IMarqueeSelectable
    {
        bool IsSelected { get; }
        bool Interactable { get; }
        RectTransform RectTransform { get; }
        MarqueeSelection Container { get; }

        void Select();
        void Deselect();
    }
}