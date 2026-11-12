using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Evo/UI/UI Elements/Palette Color Picker"), HelpURL(Constants.HelpUrl + "ui-elements/color-picker")]
    public class PaletteColorPicker : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler,
        IMoveHandler, ISubmitHandler, ICancelHandler
    {
        // Interaction
        [SerializeField] private bool interactable = true;
        public bool inputNavigation = true;

        // Palette
        [SerializeField] private int selectedIndex = -1;
        [Tooltip("Rings are drawn in list order, from back to front. The outermost nonempty ring supplies the border gradient.")]
        public List<Ring> rings = CreateDefaultRings();
        public bool allowDeselection = true;
        public bool clearOnRightClick = true;

        // Appearance
        public Color backgroundColor = new(0.035f, 0.045f, 0.05f, 0.96f);
        [Range(0.05f, 0.35f)] public float swatchRadius = 0.22f;
        [Min(0f)] public float borderWidth = 10f;
        [Min(0f)] public float glowWidth = 32f;
        [Range(0f, 1f)] public float idleGlow = 0.08f;
        [Range(0f, 1f)] public float hoverGlow = 0.3f;
        [Range(1f, 1.5f)] public float hoverScale = 1.3f;
        [Range(1f, 1.5f)] public float selectedScale = 1.1f;

        // Animation
        public bool useUnscaledTime = true;
        [Min(0f)] public float animationDuration = 0.3f;
        [Range(0f, 0.2f), Tooltip("Maximum pointer repulsion, relative to the picker radius.")]
        public float pushDistance = 0.06f;
        [Range(0.1f, 2f), Tooltip("Pointer influence radius, relative to the picker radius.")]
        public float pushRadius = 1f;
        [Min(0f)] public float selectedBorderWidth = 20f;

        // Events
        public UnityEvent<Color> onColorChanged = new();
        public UnityEvent<int> onSelectionChanged = new();

        // Constants
        internal const int MaxEntries = 512;
        const int BorderSegments = 128;
        const int CircleSegments = 40;
        const int GlowSteps = 16;
        static readonly Vector2[] borderDirections = CreateDirections(BorderSegments);
        static readonly Vector2[] circleDirections = CreateDirections(CircleSegments);
        static readonly float[] glowFalloff = CreateGlowFalloff();

        // Cache
        Color tint;
        Vector2 pointerPosition;
        TweenHandle borderAnimation, feedbackAnimation, pushAnimation, ringAnimation;
        int hoveredIndex = -1, focusedIndex = -1, pressedIndex = -1;
        int activePointerId = int.MinValue, clickPointerId = int.MinValue;
        float glow, glowFrom, glowTo;
        float ringWidth, ringWidthFrom, ringWidthTo, ringKick, pushDuration, pixelWidth;
        bool isPointerOn, hasPointerPosition, keyboardFocus;
        bool groupsAllowInteraction = true;
        readonly List<CanvasGroup> canvasGroups = new();
        readonly Color[] borderColors = new Color[BorderSegments + 1];
        readonly Color[] gradientColors = new Color[BorderSegments + 1];
        readonly Color[] borderFrom = new Color[BorderSegments + 1];
        readonly Color32[] borderVertexColors = new Color32[BorderSegments + 1];
        readonly Entry[] entries = new Entry[MaxEntries];

        protected PaletteColorPicker()
        {
#if !UNITY_6000_6_OR_NEWER
            // Before Unity 6.6, Graphic defaults to the allocating legacy Mesh path
            useLegacyMeshGeneration = false;
#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCanvasGroups();
            Refresh();
        }

        protected override void OnDisable()
        {
            StopAnimations();
            ResetPointer();
            focusedIndex = -1;
            keyboardFocus = hasPointerPosition = isPointerOn = false;
            base.OnDisable();

#if UNITY_EDITOR
            if (refreshPending)
            {
                UnityEditor.EditorApplication.delayCall -= DelayedRefresh;
                refreshPending = false;
            }
#endif
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            UpdateCanvasGroups();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            UpdateCanvasGroups();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            UpdateInteraction();
        }

        #region Types
        [Serializable]
        public class Ring
        {
            [Range(0f, 1f)] public float radius = 0.6f;
            [Tooltip("Clockwise degrees from the top.")]
            public float rotation;
            public List<Color> colors = new();
        }

        struct Entry
        {
            public Vector2 position;
            public Vector2 offset;
            public Vector2 offsetFrom;
            public Vector2 offsetTo;
            public Vector2 velocity;
            public Vector2 velocityFrom;
            public Color color;
            public float scale;
            public float from;
            public float to;
        }

        static List<Ring> CreateDefaultRings()
        {
            Ring outer = new() { radius = 0.59f };
            Ring inner = new() { radius = 0.29f, rotation = 30f };
            for (int i = 0; i < 12; i++)
                outer.colors.Add(Color.HSVToRGB(i / 12f, 0.78f, 1f));
            for (int i = 0; i < 6; i++)
                inner.colors.Add(Color.HSVToRGB((i + 0.5f) / 6f, 0.22f, 1f));

            return new() { outer, inner, new Ring { radius = 0f, colors = new() { Color.white } } };
        }
        #endregion

        #region Pointer Events
        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOn = true;
            UpdateHover(eventData);
            AnimateFeedback();
        }

        public void OnPointerMove(PointerEventData eventData) => UpdateHover(eventData);

        public void OnPointerExit(PointerEventData eventData)
        {
            hoveredIndex = -1;
            hasPointerPosition = false;
            isPointerOn = false;
            AnimateInteraction();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract || eventData.button != PointerEventData.InputButton.Left || activePointerId != int.MinValue)
                return;

            activePointerId = eventData.pointerId;
            clickPointerId = eventData.pointerId;
            keyboardFocus = false;
            hasPointerPosition = TryGetPointerPosition(eventData.position, eventData.pressEventCamera, out pointerPosition);
            pressedIndex = hasPointerPosition ? HitTest(pointerPosition) : -1;
            hoveredIndex = pressedIndex;

            if (pressedIndex >= 0)
                focusedIndex = pressedIndex;
            if (inputNavigation && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject, eventData);

            AnimateInteraction();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId || eventData.button != PointerEventData.InputButton.Left)
                return;

            activePointerId = int.MinValue;
            // Keep the press index until OnPointerClick so dragging across dots cannot select another dot.
            AnimateFeedback();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanInteract)
                return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (clearOnRightClick && allowDeselection)
                    ClearSelection();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left || eventData.pointerId != clickPointerId)
                return;

            int hit = HitTest(eventData.position, eventData.pressEventCamera);
            int pressed = pressedIndex;
            pressedIndex = -1;
            clickPointerId = int.MinValue;
            if (hit < 0 || hit != pressed)
                return;

            ToggleSelection(hit);
        }
        #endregion

        #region Navigation
        public void OnSelect(BaseEventData eventData)
        {
            if (!CanInteract || !inputNavigation)
                return;

            keyboardFocus = activePointerId == int.MinValue;
            if (focusedIndex < 0 && EntryCount > 0)
                focusedIndex = HasSelection ? selectedIndex : EntryCount - 1;

            AnimateInteraction();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            keyboardFocus = false;
            AnimateInteraction();
        }

        public void OnMove(AxisEventData eventData)
        {
            if (!CanInteract || EntryCount == 0 || !inputNavigation)
                return;

            Vector2 origin = entries[Mathf.Clamp(focusedIndex, 0, EntryCount - 1)].position;
            int next = -1;
            float best = float.PositiveInfinity;
            Vector2 direction = eventData.moveVector.normalized;
            for (int i = 0; i < EntryCount; i++)
            {
                Vector2 delta = entries[i].position - origin;
                float forward = Vector2.Dot(direction, delta);
                if (forward <= 0.001f)
                    continue;

                float score = delta.sqrMagnitude / forward;
                if (score < best)
                {
                    best = score;
                    next = i;
                }
            }

            if (next < 0)
                return;

            focusedIndex = next;
            keyboardFocus = true;
            hasPointerPosition = false;
            hoveredIndex = -1;
            AnimateInteraction();
            eventData.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!CanInteract || !inputNavigation || focusedIndex < 0)
                return;

            ToggleSelection(focusedIndex);
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (CanInteract && allowDeselection && HasSelection)
            {
                ClearSelection();
                eventData.Use();
            }
        }
        #endregion

        #region Interaction
        int HighlightedIndex => keyboardFocus ? focusedIndex : hoveredIndex;
        bool CanInteract => IsActive() && IsInteractable();

        void UpdateCanvasGroups()
        {
            bool allowed = true;
            Transform current = transform;
            while (current != null)
            {
                current.GetComponents(canvasGroups);
                bool ignoreParents = false;
                foreach (CanvasGroup group in canvasGroups)
                {
                    if (!group.isActiveAndEnabled)
                        continue;

                    allowed &= group.interactable;
                    ignoreParents |= group.ignoreParentGroups;
                }

                if (!allowed || ignoreParents)
                    break;

                current = current.parent;
            }

            canvasGroups.Clear();
            if (groupsAllowInteraction == allowed)
                return;

            groupsAllowInteraction = allowed;
            UpdateInteraction();
        }

        void UpdateInteraction()
        {
            if (!IsInteractable())
            {
                ResetPointer();
                keyboardFocus = false;
            }

            AnimateInteraction();
            SetVerticesDirty();
        }

        void ResetPointer()
        {
            hoveredIndex = pressedIndex = -1;
            activePointerId = clickPointerId = int.MinValue;
        }

        void UpdateHover(PointerEventData eventData)
        {
            if (!CanInteract || (activePointerId != int.MinValue && activePointerId != eventData.pointerId))
                return;

            bool hadKeyboardFocus = keyboardFocus;
            keyboardFocus = false;
            bool valid = TryGetPointerPosition(eventData.position, eventData.enterEventCamera, out Vector2 point);
            if (hadKeyboardFocus || valid != hasPointerPosition || (valid && (point - pointerPosition).sqrMagnitude > 0.000001f))
            {
                hasPointerPosition = valid;
                pointerPosition = point;
                AnimatePush();
            }

            int hit = valid ? HitTest(point) : -1;
            if (hoveredIndex != hit || hadKeyboardFocus)
            {
                hoveredIndex = hit;
                AnimateFeedback();
            }
        }

        // Local point relative to the picker center, and the radius of the swatch area
        bool TryGetLocalPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 point, out float radius)
        {
            radius = 0f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out point))
                return false;

            Rect rect = GetPixelAdjustedRect();
            radius = GetRadius(rect);
            point -= rect.center;
            return true;
        }

        bool TryGetPointerPosition(Vector2 screenPoint, Camera eventCamera, out Vector2 point)
        {
            if (!TryGetLocalPoint(screenPoint, eventCamera, out point, out float radius) || radius <= 0f)
                return false;

            point /= radius;
            return true;
        }

        int HitTest(Vector2 screenPoint, Camera eventCamera) => TryGetPointerPosition(screenPoint, eventCamera, out Vector2 point) ? HitTest(point) : -1;

        int HitTest(Vector2 point)
        {
            // Match the fixed mesh order, including the currently animated position and size
            for (int i = EntryCount - 1; i >= 0; i--)
            {
                float radius = swatchRadius * entries[i].scale;
                if ((point - entries[i].position - entries[i].offset).sqrMagnitude <= radius * radius)
                    return i;
            }

            return -1;
        }

        void ToggleSelection(int index) => SetSelectedIndex(allowDeselection && selectedIndex == index ? -1 : index);

        #endregion

        #region Animation
        void StopAnimations()
        {
            borderAnimation.Kill();
            feedbackAnimation.Kill();
            pushAnimation.Kill();
            ringAnimation.Kill();
            borderAnimation = feedbackAnimation = pushAnimation = ringAnimation = default;
        }

        // Tweens progress from 0 to 1 into an update method, or applies the end state at once
        void Animate(ref TweenHandle animation, bool animate, float duration, Ease ease, Action<PaletteColorPicker, float> update, int property)
        {
            if (animate && Application.IsPlaying(gameObject) && isActiveAndEnabled && animationDuration > 0f)
            {
                animation = EvoTween.Create(0f, 1f, duration)
                    .WithEase(ease)
                    .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                    .Bind(this, update, property);
            }
            else { update(this, 1f); }
        }

        void AnimateInteraction()
        {
            AnimateFeedback();
            AnimatePush();
        }

        void AnimateBorder(bool animate)
        {
            borderAnimation.Kill();
            Array.Copy(borderColors, borderFrom, borderColors.Length);
            Animate(ref borderAnimation, animate, animationDuration, Ease.OutCubic, static (picker, value) => picker.UpdateBorder(value), 0);
        }

        void UpdateBorder(float progress)
        {
            bool selected = HasSelection;
            Color selectedColor = SelectedColor;
            for (int i = 0; i < borderColors.Length; i++)
                borderColors[i] = Color.LerpUnclamped(borderFrom[i], selected ? selectedColor : gradientColors[i], progress);

            SetVerticesDirty();
        }

        void AnimateFeedback(bool animate = true)
        {
            bool interactive = IsInteractable();
            int highlighted = interactive ? HighlightedIndex : -1;
            bool changed = false;
            for (int i = 0; i < EntryCount; i++)
            {
                float scale = i == highlighted ? hoverScale : i == selectedIndex ? selectedScale : 1f;
                if (interactive && i == pressedIndex && activePointerId != int.MinValue)
                    scale *= 0.9f;

                changed |= entries[i].to != scale;
                entries[i].to = scale;
            }

            float targetGlow = interactive && (isPointerOn || keyboardFocus) ? hoverGlow : idleGlow;
            changed |= glowTo != targetGlow;
            if (!changed && animate)
                return;

            feedbackAnimation.Kill();
            for (int i = 0; i < EntryCount; i++)
                entries[i].from = entries[i].scale;

            glowFrom = glow;
            glowTo = targetGlow;
            Animate(ref feedbackAnimation, animate, animationDuration, Ease.OutCubic, static (picker, value) => picker.UpdateFeedback(value), 1);
        }

        void UpdateFeedback(float progress)
        {
            for (int i = 0; i < EntryCount; i++)
                entries[i].scale = Mathf.LerpUnclamped(entries[i].from, entries[i].to, progress);

            glow = Mathf.LerpUnclamped(glowFrom, glowTo, progress);
            SetVerticesDirty();
        }

        void AnimatePush(bool animate = true)
        {
            bool interactive = IsInteractable();
            bool pointer = interactive && isPointerOn && hasPointerPosition && pointerPosition.sqrMagnitude <= 1f;
            bool keyboard = interactive && keyboardFocus && focusedIndex >= 0 && focusedIndex < EntryCount;
            Vector2 origin = pointer ? pointerPosition : keyboard ? entries[focusedIndex].position : Vector2.zero;
            bool changed = false;
            for (int i = 0; i < EntryCount; i++)
            {
                Vector2 offset = Vector2.zero;
                if (pointer || keyboard)
                {
                    Vector2 delta = entries[i].position - origin;
                    float distance = delta.magnitude;
                    float strength = Mathf.Clamp01(1f - distance / pushRadius);
                    offset = delta / Mathf.Max(distance, swatchRadius * 0.5f) * (strength * pushDistance);
                }

                changed |= (entries[i].offsetTo - offset).sqrMagnitude > 0.00000001f;
                entries[i].offsetTo = offset;
            }

            if (!changed && animate)
                return;

            pushAnimation.Kill();
            for (int i = 0; i < EntryCount; i++)
            {
                entries[i].offsetFrom = entries[i].offset;
                entries[i].velocityFrom = entries[i].velocity;
            }

            pushDuration = Mathf.Max(0.01f, animationDuration * 2.5f);
            Animate(ref pushAnimation, animate, pushDuration, Ease.Linear, static (picker, value) => picker.UpdatePush(value), 2);
        }

        void UpdatePush(float progress)
        {
            // Closed-form critically damped spring. Retargeting preserves both position and velocity.
            float time = progress * pushDuration;
            float frequency = 10f / pushDuration;
            float decay = Mathf.Exp(-frequency * time);
            for (int i = 0; i < EntryCount; i++)
            {
                Vector2 displacement = entries[i].offsetFrom - entries[i].offsetTo;
                Vector2 impulse = entries[i].velocityFrom + frequency * displacement;
                entries[i].offset = progress >= 1f ? entries[i].offsetTo : entries[i].offsetTo + (displacement + impulse * time) * decay;
                entries[i].velocity = progress >= 1f ? Vector2.zero : (entries[i].velocityFrom - frequency * time * impulse) * decay;
            }

            SetVerticesDirty();
        }

        void AnimateRing(bool animate)
        {
            ringAnimation.Kill();
            ringWidthFrom = ringWidth;
            ringWidthTo = HasSelection ? Mathf.Max(borderWidth, selectedBorderWidth) : borderWidth;
            ringKick = HasSelection && Mathf.Abs(ringWidthFrom - ringWidthTo) < 0.1f ? ringWidthTo * 0.2f : 0f;
            Animate(ref ringAnimation, animate, animationDuration * 2.5f, Ease.Linear, static (picker, value) => picker.UpdateRing(value), 3);
        }

        void UpdateRing(float progress)
        {
            float eased = TweenEase.Evaluate(HasSelection ? Ease.OutElastic : Ease.OutCubic, progress);
            float pulse = ringKick * Mathf.Sin(progress * Mathf.PI * 2f) * (1f - progress) * (1f - progress);
            ringWidth = Mathf.Max(0f, Mathf.LerpUnclamped(ringWidthFrom, ringWidthTo, eased) + pulse);
            SetVerticesDirty();
        }
        #endregion

        #region Rendering
        float GetRadius(Rect rect) => Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f - glowWidth - borderWidth);

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled || !base.Raycast(screenPoint, eventCamera) || !TryGetLocalPoint(screenPoint, eventCamera, out Vector2 point, out float radius))
                return false;

            radius += borderWidth * 0.5f;
            return point.sqrMagnitude <= radius * radius;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!isActiveAndEnabled)
                return;

            Rect rect = GetPixelAdjustedRect();
            float radius = GetRadius(rect);
            if (radius <= 0f)
                return;

            Vector2 center = rect.center;
            pixelWidth = Mathf.Min(radius * 0.1f, ChartMesh.PixelWidth(rectTransform, canvas));
            tint = color;
            DrawDisc(vh, center, radius, backgroundColor);

            bool drawGlow = glowWidth > 0f && glow > 0f;
            if (drawGlow || ringWidth > 0f)
            {
                // Tint the gradient once per mesh instead of once per vertex
                for (int i = 0; i < borderVertexColors.Length; i++)
                    borderVertexColors[i] = Tinted(borderColors[i]);
            }

            if (drawGlow)
                DrawGlow(vh, center, radius);

            if (ringWidth > 0f)
                DrawBorder(vh, center, radius);

            for (int i = 0; i < EntryCount; i++)
                DrawEntry(vh, center, radius, i);
        }

        void DrawEntry(VertexHelper vh, Vector2 center, float radius, int index)
        {
            ref readonly Entry entry = ref entries[index];
            Vector2 position = center + (entry.position + entry.offset) * radius;
            float size = swatchRadius * radius * entry.scale;

            DrawDisc(vh, position, size, entry.color);
            if (index == SelectedIndex)
            {
                // A small contrasting center mark keeps selection readable, including white and transparent swatches
                float luminance = entry.color.r * 0.2126f + entry.color.g * 0.7152f + entry.color.b * 0.0722f;
                Color mark = luminance > 0.6f ? new Color(0.08f, 0.1f, 0.12f, 0.8f) : new Color(1f, 1f, 1f, 0.95f);
                DrawDisc(vh, position, size * 0.13f, mark);
            }
        }

        void DrawDisc(VertexHelper vh, Vector2 center, float radius, Color value)
        {
            if (value.a <= 0f)
                return;

            int first = vh.currentVertCount;
            float inner = Mathf.Max(0f, radius - pixelWidth);
            Color32 solid = Tinted(value);
            Color32 clear = Tinted(WithAlpha(value, 0f));
            AddVertex(vh, center, solid);
            for (int i = 0; i <= CircleSegments; i++)
            {
                AddVertex(vh, center + circleDirections[i] * inner, solid);
                AddVertex(vh, center + circleDirections[i] * radius, clear);
                if (i == 0)
                    continue;

                int a = first + 1 + (i - 1) * 2;
                vh.AddTriangle(first, a, a + 2);
                ChartMesh.Quad(vh, a, a + 1, a + 3, a + 2);
            }
        }

        void DrawGlow(VertexHelper vh, Vector2 center, float radius)
        {
            int first = vh.currentVertCount;
            int stride = BorderSegments + 1;
            for (int band = 0; band <= GlowSteps * 2; band++)
            {
                float distance = (band - GlowSteps) / (float)GlowSteps;
                float alpha = glowFalloff[band] * glow * 0.45f;
                float size = Mathf.Max(0f, radius + distance * glowWidth * 1.65f);

                // Bands only change alpha. Gradients are mostly opaque, so a band converts each distinct alpha once.
                float sourceAlpha = 0f;
                byte vertexAlpha = 0;
                for (int i = 0; i < stride; i++)
                {
                    float source = borderColors[i].a;
                    if (i == 0 || source != sourceAlpha)
                    {
                        sourceAlpha = source;
                        vertexAlpha = Tinted(WithAlpha(borderColors[i], source * alpha)).a;
                    }

                    Color32 vertexColor = borderVertexColors[i];
                    vertexColor.a = vertexAlpha;
                    AddVertex(vh, center + borderDirections[i] * size, vertexColor);
                    if (band > 0 && i > 0)
                    {
                        int a = first + (band - 1) * stride + i - 1;
                        ChartMesh.Quad(vh, a, a + stride, a + stride + 1, a + 1);
                    }
                }
            }
        }

        void DrawBorder(VertexHelper vh, Vector2 center, float radius)
        {
            int first = vh.currentVertCount;
            float outer = radius + borderWidth * 0.5f;
            float width = Mathf.Clamp(ringWidth, 0f, outer);
            float inner = outer - width;
            float feather = Mathf.Min(pixelWidth, width * 0.5f);
            for (int i = 0; i <= BorderSegments; i++)
            {
                Color32 value = borderVertexColors[i];
                Color32 clear = Tinted(WithAlpha(borderColors[i], 0f));
                Vector2 direction = borderDirections[i];
                AddVertex(vh, center + direction * Mathf.Max(0f, inner - feather), clear);
                AddVertex(vh, center + direction * Mathf.Max(0f, inner + feather), value);
                AddVertex(vh, center + direction * (outer - feather), value);
                AddVertex(vh, center + direction * outer, clear);
                if (i == 0)
                    continue;

                int a = first + (i - 1) * 4;
                for (int band = 0; band < 3; band++)
                    ChartMesh.Quad(vh, a + band, a + band + 1, a + band + 5, a + band + 4);
            }
        }

        // VertexHelper's defaults, passed to the full overload to skip two forwarding calls per vertex
        static readonly Vector3 vertexNormal = Vector3.back;
        static readonly Vector4 vertexTangent = new(1f, 0f, 0f, -1f);

        static void AddVertex(VertexHelper vh, Vector2 position, Color32 value) =>
            vh.AddVert(position, value, default, default, default, default, vertexNormal, vertexTangent);

        Color32 Tinted(Color value) => value * tint;

        static Color WithAlpha(Color value, float alpha)
        {
            value.a = alpha;
            return value;
        }

        static Vector2[] CreateDirections(int segments)
        {
            Vector2[] directions = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                directions[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            }
            return directions;
        }

        static float[] CreateGlowFalloff()
        {
            float[] falloff = new float[GlowSteps * 2 + 1];
            float edge = Mathf.Exp(-4.5f);
            for (int i = 0; i < falloff.Length; i++)
            {
                float distance = (i - GlowSteps) / (float)GlowSteps;
                falloff[i] = (Mathf.Exp(-4.5f * distance * distance) - edge) / (1f - edge);
            }
            return falloff;
        }
        #endregion

        #region Public API
        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value)
                    return;

                interactable = value;
                UpdateInteraction();
            }
        }
        public int EntryCount { get; set; }
        public int SelectedIndex => selectedIndex;
        public bool HasSelection => selectedIndex >= 0 && selectedIndex < EntryCount;
        public Color SelectedColor => HasSelection ? entries[selectedIndex].color : Color.clear;

        public bool IsInteractable() => interactable && groupsAllowInteraction;

        /// <summary>
        /// Selects a flattened ring/color index. -1 clears the selection. Invalid indices are ignored.
        /// </summary>
        public void SetSelectedIndex(int index, bool notify = true, bool animate = true)
        {
            if (index < -1 || index >= EntryCount || index == selectedIndex)
                return;

            selectedIndex = index;
            if (index >= 0)
                focusedIndex = index;

            AnimateBorder(animate);
            AnimateRing(animate);
            AnimateFeedback(animate);
            Color selectedColor = SelectedColor;

            if (notify)
            {
                // Notify the color first; selection listeners can explicitly handle the empty state
                if (index >= 0)
                    onColorChanged?.Invoke(selectedColor);

                onSelectionChanged?.Invoke(index);
            }
        }

        public void ClearSelection() => SetSelectedIndex(-1);

        public void SetSelectedIndexWithoutNotify(int index) => SetSelectedIndex(index, false);

        /// <summary>
        /// Rebuilds cached geometry and gradient colors after editing the palette or appearance from code.
        /// </summary>
        public void Refresh()
        {
            StopAnimations();

            swatchRadius = Mathf.Clamp(swatchRadius, 0.05f, 0.35f);
            borderWidth = Mathf.Max(0f, borderWidth);
            glowWidth = Mathf.Max(0f, glowWidth);
            hoverScale = Mathf.Clamp(hoverScale, 1f, 1.5f);
            selectedScale = Mathf.Clamp(selectedScale, 1f, 1.5f);
            animationDuration = Mathf.Max(0f, animationDuration);
            pushDistance = Mathf.Clamp(pushDistance, 0f, 0.2f);
            pushRadius = Mathf.Clamp(pushRadius, 0.1f, 2f);
            selectedBorderWidth = Mathf.Max(0f, selectedBorderWidth);

            int count = 0;
            Ring outer = null;
            float maxRadius = Mathf.Max(0f, 1f - swatchRadius * Mathf.Max(hoverScale, selectedScale) - pushDistance);
            if (rings != null)
            {
                foreach (Ring ring in rings)
                {
                    if (ring?.colors == null || ring.colors.Count == 0)
                        continue;

                    if (outer == null || ring.radius > outer.radius)
                        outer = ring;

                    float radius = Mathf.Clamp(ring.radius, 0f, maxRadius);
                    for (int i = 0; i < ring.colors.Count && count < MaxEntries; i++)
                    {
                        float angle = (ring.rotation + i * 360f / ring.colors.Count) * Mathf.Deg2Rad;
                        entries[count++] = new Entry
                        {
                            position = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius,
                            color = ring.colors[i],
                            scale = 1f
                        };
                    }
                }
            }

            EntryCount = count;

            for (int i = 0; i < gradientColors.Length; i++)
            {
                Color value = Color.white;
                if (outer != null)
                {
                    float position = Mathf.Repeat(i / (float)BorderSegments - outer.rotation / 360f, 1f) * outer.colors.Count;
                    int first = Mathf.FloorToInt(position) % outer.colors.Count;
                    value = Color.Lerp(outer.colors[first], outer.colors[(first + 1) % outer.colors.Count], position - Mathf.Floor(position));
                }
                gradientColors[i] = value;
            }

            selectedIndex = selectedIndex >= 0 && selectedIndex < count ? selectedIndex : -1;
            ResetPointer();
            focusedIndex = HasSelection ? selectedIndex : count - 1;
            UpdateBorder(1f);
            AnimateRing(false);
            AnimateFeedback(false);
            AnimatePush(false);
        }
        #endregion

#if UNITY_EDITOR
        [NonSerialized] bool refreshPending;

        protected override void Reset()
        {
            base.Reset();
            Refresh();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += DelayedRefresh;
        }

        void DelayedRefresh()
        {
            refreshPending = false;
            if (this != null && isActiveAndEnabled)
            {
                Refresh();
                UpdateInteraction();
            }
        }
#endif
    }
}
