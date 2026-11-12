using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Comparison Slider")]
    public class ComparisonSlider : Interactive, IDragHandler, IInitializePotentialDragHandler
    {
        // Content
        [SerializeField] private Sprite firstSprite;
        [SerializeField] private Sprite secondSprite;
        [SerializeField, Range(0f, 1f)] private float value = 0.5f;

        // Settings
        public Orientation orientation;
        public bool reverse = false;
        public bool clickToMove = true;
        public bool dragOnHover = false;
        [Range(0.001f, 1f)] public float inputStep = 0.05f;
        [SerializeField, Tooltip("Set height from the first sprite's aspect ratio. Leave off when a layout controls height.")]
        private bool autoHeight = false;
        [SerializeField, Min(0f), Tooltip("Spacing on all sides of the comparison area, in UI units.")]
        private float padding = 0f;

        // References
        public ProceduralShape firstImage;
        public ProceduralShape secondImage;
        public RectTransform handleRect;
        public RectTransform dividerRect;

        // Events
        public UnityEvent<float> onValueChanged = new();

        // Cache
        RectTransform cachedRect;
        Rect firstBounds;
        Rect secondBounds;
        bool updatingGeometry;
        bool hasPointer;
        int pointerId;
        float pointerOffset;

        // Enums
        public enum Orientation { Horizontal, Vertical }

        // Properties
        RectTransform Rect => cachedRect != null ? cachedRect : cachedRect = (RectTransform)transform;
        int Axis => orientation == Orientation.Horizontal ? 0 : 1;
        float Position => reverse ? 1f - value : value;
        float Inset => Mathf.Clamp(padding, 0f, Mathf.Max(0f, Mathf.Min(Rect.rect.width, Rect.rect.height) * 0.5f));

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        protected override void OnDisable()
        {
            ReleasePointer();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
#endif
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateGeometry();
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            if (!IsInteractable())
                ReleasePointer();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (state == SelectionState.Disabled)
                ReleasePointer();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            Refresh();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract() || hasPointer || eventData.button != PointerEventData.InputButton.Left
                || !TryGetPosition(eventData, eventData.pressEventCamera, out float position))
                return;

            bool onHandle = ValidChild(handleRect) && handleRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(handleRect, eventData.position, eventData.pressEventCamera);
            bool onDivider = ValidChild(dividerRect) && dividerRect.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(dividerRect, eventData.position, eventData.pressEventCamera);

            if (!clickToMove && !onHandle && !onDivider)
                return;

            hasPointer = true;
            pointerId = eventData.pointerId;
            pointerOffset = onHandle || onDivider ? position - Position : 0f;
            base.OnPointerDown(eventData);

            if (hasPointer && CanInteract())
                SetPosition(position - pointerOffset);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!hasPointer || eventData.pointerId != pointerId || eventData.button != PointerEventData.InputButton.Left)
                return;

            ReleasePointer();
            base.OnPointerUp(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (!hasPointer || eventData.pointerId != pointerId)
                return;

            if (!CanInteract())
            {
                ReleasePointer();
                return;
            }

            if (TryGetPosition(eventData, eventData.pressEventCamera, out float position))
                SetPosition(position - pointerOffset);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            UpdateHover(eventData);
        }

        public override void OnPointerMove(PointerEventData eventData)
        {
            base.OnPointerMove(eventData);
            UpdateHover(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!CanInteract())
                return;

            bool horizontal = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right;
            bool vertical = eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down;
            if ((Axis == 0 && horizontal) || (Axis == 1 && vertical))
            {
                float step = eventData.moveDir == MoveDirection.Right || eventData.moveDir == MoveDirection.Up ? inputStep : -inputStep;
                Value += reverse ? -step : step;
                eventData.Use();
            }
            else { base.OnMove(eventData); }
        }

        void ConfigureImage(ProceduralShape image, Sprite sprite, int origin)
        {
            image.Sprite = sprite;
            image.enabled = sprite != null;
            image.ClippingMethod = Axis == 0 ? ProceduralShape.ClipMethod.Horizontal : ProceduralShape.ClipMethod.Vertical;
            image.ClipOrigin = origin;
            image.ImageScaleMode = ProceduralShape.ScaleMode.Cover;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        void UpdateGeometry()
        {
            if (!IsActive() || updatingGeometry)
                return;

            updatingGeometry = true;

            try
            {
                Sprite source = firstSprite != null ? firstSprite : secondSprite;
                if (autoHeight && source != null && source.rect.width > 0f)
                {
                    float inset = Mathf.Clamp(padding, 0f, Mathf.Max(0f, Rect.rect.width * 0.5f));
                    float height = Mathf.Max(0f, Rect.rect.width - inset * 2f) * source.rect.height / source.rect.width + inset * 2f;
                    if (!Mathf.Approximately(Rect.rect.height, height))
                        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }

                if (!ValidImages())
                    return;

                Vector2 offsets = Vector2.one * Inset;
                firstImage.rectTransform.offsetMin = secondImage.rectTransform.offsetMin = offsets;
                firstImage.rectTransform.offsetMax = secondImage.rectTransform.offsetMax = -offsets;
                firstBounds = GetImageBounds(firstImage);
                secondBounds = GetImageBounds(secondImage);

                if (ValidChild(dividerRect))
                {
                    int oldAxis = dividerRect.anchorMin.x != dividerRect.anchorMax.x ? 1 : 0;
                    float thickness = dividerRect.rect.size[oldAxis];
                    Vector2 min = Vector2.zero;
                    Vector2 max = Vector2.one;
                    min[Axis] = max[Axis] = Position;
                    dividerRect.anchorMin = min;
                    dividerRect.anchorMax = max;
                    dividerRect.pivot = new Vector2(0.5f, 0.5f);
                    dividerRect.anchoredPosition = Vector2.zero;
                    dividerRect.sizeDelta = Axis == 0 ? new Vector2(thickness, -offsets.y * 2f) : new Vector2(-offsets.x * 2f, thickness);
                }

                UpdateBoundary();
            }
            finally { updatingGeometry = false; }
        }

        Rect GetImageBounds(ProceduralShape image)
        {
            Rect bounds = image.GetPixelAdjustedRect();
            Sprite sprite = image.Sprite;
            if (sprite != null)
            {
                // Filled Images clip their rendered bounds, including sprite-atlas padding
                Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                Vector2 sizeInPixels = new(Mathf.Max(1f, Mathf.Round(sprite.rect.width)), Mathf.Max(1f, Mathf.Round(sprite.rect.height)));
                bounds = UnityEngine.Rect.MinMaxRect(bounds.xMin + bounds.width * padding.x / sizeInPixels.x,
                    bounds.yMin + bounds.height * padding.y / sizeInPixels.y,
                    bounds.xMax - bounds.width * padding.z / sizeInPixels.x,
                    bounds.yMax - bounds.height * padding.w / sizeInPixels.y);
            }

            bounds.position += (Vector2)image.rectTransform.localPosition;
            return bounds;
        }

        void UpdateBoundary()
        {
            if (!ValidImages())
                return;

            float inset = Inset;
            float boundary = Mathf.Lerp(Rect.rect.min[Axis] + inset, Rect.rect.max[Axis] - inset, Position);
            Vector2 offset = Vector2.zero;
            offset[Axis] = Mathf.Lerp(inset, -inset, Position);
            firstImage.ClipAmount = GetFill(firstBounds, boundary, reverse);
            secondImage.ClipAmount = GetFill(secondBounds, boundary, !reverse);

            if (ValidChild(handleRect))
            {
                Vector2 anchor = new(0.5f, 0.5f);
                anchor[Axis] = Position;
                handleRect.anchorMin = handleRect.anchorMax = anchor;
                handleRect.pivot = new Vector2(0.5f, 0.5f);
                handleRect.anchoredPosition = offset;
            }

            if (ValidChild(dividerRect))
            {
                Vector2 min = dividerRect.anchorMin;
                Vector2 max = dividerRect.anchorMax;
                min[Axis] = max[Axis] = Position;
                dividerRect.anchorMin = min;
                dividerRect.anchorMax = max;
                dividerRect.anchoredPosition = offset;
            }
        }

        float GetFill(Rect bounds, float boundary, bool fromEnd)
        {
            if (bounds.size[Axis] <= Mathf.Epsilon)
                return 0f;

            float amount = Mathf.Clamp01((boundary - bounds.min[Axis]) / bounds.size[Axis]);
            return fromEnd ? 1f - amount : amount;
        }

        void SetValue(float input, bool notify)
        {
            float next = Safe01(input);
            if (value == next)
                return;

            value = next;
            UpdateBoundary();

            if (notify)
                onValueChanged?.Invoke(value);
        }

        void UpdateHover(PointerEventData eventData)
        {
            if (!dragOnHover || hasPointer || !CanInteract() || eventData.dragging
                || eventData.eligibleForClick || eventData.pointerPress != null)
                return;

            if (RectTransformUtility.RectangleContainsScreenPoint(Rect, eventData.position, eventData.enterEventCamera)
                && TryGetPosition(eventData, eventData.enterEventCamera, out float position))
                SetPosition(position);
        }

        bool TryGetPosition(PointerEventData eventData, Camera camera, out float position)
        {
            position = 0f;
            float inset = Inset;
            float length = Rect.rect.size[Axis] - inset * 2f;
            if (length <= Mathf.Epsilon
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position, camera, out Vector2 local))
                return false;

            position = (local[Axis] - Rect.rect.min[Axis] - inset) / length;
            return !float.IsNaN(position) && !float.IsInfinity(position);
        }

        void SetPosition(float position) => Value = reverse ? 1f - position : position;

        void ReleasePointer()
        {
            bool wasPressed = hasPointer;
            hasPointer = false;
            pointerOffset = 0f;
            isPressedDown = false;

            if (wasPressed && IsActive() && IsInteractable() && interactionState == InteractionState.Pressed)
                SetState(isPointerOn ? InteractionState.Highlighted : InteractionState.Normal);
        }

        bool ValidImages()
        {
            return firstImage != null && secondImage != null && firstImage != secondImage
                && firstImage.transform.parent == transform && secondImage.transform.parent == transform;
        }

        bool ValidChild(RectTransform child)
        {
            return child != null && child.parent == transform && (firstImage == null || child != firstImage.rectTransform)
                && (secondImage == null || child != secondImage.rectTransform);
        }

        bool CanInteract() => IsActive() && IsInteractable() && ValidImages();

        static float Safe01(float input) => float.IsNaN(input) ? 0f : Mathf.Clamp01(input);

        public void SetReferences(ProceduralShape first, ProceduralShape second, RectTransform handle = null, RectTransform divider = null)
        {
            if (first == null || second == null || first == second || first.transform.parent != transform
                || second.transform.parent != transform || (handle != null && (handle.parent != transform
                || handle == first.rectTransform || handle == second.rectTransform))
                || (divider != null && (divider.transform.parent != transform || divider == first || divider == second || divider == handle)))
                throw new System.ArgumentException("Comparison visuals must be distinct direct children of the slider.");

            firstImage = first;
            secondImage = second;
            handleRect = handle;
            dividerRect = divider;

            ReleasePointer();
            Refresh();
        }

        #region Public API
        public float Value
        {
            get => value;
            set => SetValue(value, true);
        }

        public Sprite FirstSprite
        {
            get => firstSprite;
            set
            {
                firstSprite = value;
                Refresh();
            }
        }

        public Sprite SecondSprite
        {
            get => secondSprite;
            set
            {
                secondSprite = value;
                Refresh();
            }
        }

        public Orientation SliderOrientation
        {
            get => orientation;
            set
            {
                orientation = value;
                ReleasePointer();
                Refresh();
            }
        }

        public bool Reverse
        {
            get => reverse;
            set
            {
                reverse = value;
                ReleasePointer();
                Refresh();
            }
        }

        public bool AutoHeight
        {
            get => autoHeight;
            set
            {
                autoHeight = value;
                UpdateGeometry();
            }
        }

        public float Padding
        {
            get => padding;
            set
            {
                padding = Mathf.Max(0f, value);
                UpdateGeometry();
            }
        }

        public void SetValueWithoutNotify(float input) => SetValue(input, false);

        public void Refresh()
        {
            padding = Mathf.Max(0f, padding);
            value = Safe01(value);
            inputStep = Mathf.Clamp(Safe01(inputStep), 0.001f, 1f);

            if (ValidImages())
            {
                ConfigureImage(firstImage, firstSprite, reverse ? 1 : 0);
                ConfigureImage(secondImage, secondSprite, reverse ? 0 : 1);
            }

            UpdateGeometry();
        }
        #endregion

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            transition = Transition.None;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            value = Safe01(value);
            inputStep = Mathf.Clamp(Safe01(inputStep), 0.001f, 1f);
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
            UnityEditor.EditorApplication.delayCall += RefreshEditor;
        }

        void RefreshEditor()
        {
            if (this != null && isActiveAndEnabled)
                Refresh();
        }
#endif
    }
}