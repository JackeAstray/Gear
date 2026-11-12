using UnityEngine;
using UnityEngine.EventSystems;
using ShapeType = Evo.UI.ProceduralShape.ShapeType;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Combine tab: a gooey menu in an app window. The button is one shape that unions its bubble operands, the bar
    /// subtracts a circle that follows the button, and the canvas combines a shape with the operand picked from the menu.
    /// Tap the button to open the menu and a bubble to pick a shape; drag the bubbles or pull the button out of its cradle.
    /// The options start from the shapes' own operations.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeCombine : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
    {
        [System.Serializable]
        struct Bubble
        {
            [Tooltip("Operand of the button, hidden while the menu is closed.")]
            public ProceduralShape shape;
            [Tooltip("Dashed outline that also catches the pointer.")]
            public ProceduralShape handle;
            public CanvasGroup content;
            [Tooltip("Shown on the bubble whose shape the canvas uses.")]
            public GameObject dot;
        }

        [System.Serializable]
        struct OperandShape
        {
            public ShapeType shape;
            public int sides;
            public Vector2 size;
            public float radius;
        }

        [Header("Menu")]
        [Tooltip("The app window. The button and bubbles move inside it.")]
        [SerializeField] private RectTransform area;
        [SerializeField] private ProceduralShape button;
        [SerializeField] private RectTransform icon;
        [SerializeField] private ProceduralShape bar;
        [Tooltip("The bar's operand. It sits on the button, sized to the button plus the gap.")]
        [SerializeField] private RectTransform notch;
        [SerializeField] private ProceduralShape notchHandle;
        [SerializeField] private Bubble[] bubbles;
        [Tooltip("Distance from the button's center to each bubble's.")]
        [SerializeField] private float radius = 146f;
        [Tooltip("Angle of the first bubble in degrees; the rest fan out clockwise, evenly spaced.")]
        [SerializeField] private float firstAngle = 160f;
        [SerializeField] private float spread = 140f;
        [Tooltip("Dimmed while the menu is open.")]
        [SerializeField] private CanvasGroup content;

        [Header("Canvas")]
        [SerializeField] private ProceduralShape shape;
        [SerializeField] private ProceduralShape operand;
        [SerializeField] private ProceduralShape handle;
        [Tooltip("One per bubble, in the same order.")]
        [SerializeField] private OperandShape[] operands;

        [Header("Options")]
        [SerializeField] private Slider menuSmoothing;
        [SerializeField] private Switch open;
        [SerializeField] private Switch showOperands;
        [SerializeField] private Slider cradleSmoothing;
        [SerializeField] private Slider gap;
        [SerializeField] private RadioButtonGroup operation;
        [SerializeField] private Slider canvasSmoothing;

        const float OpenDelay = 0.5f;
        const float Stagger = 0.05f;
        const float OpenAngle = 45f;
        const float DimAlpha = 0.32f;
        const float PressScale = 0.92f;
        const float HandleWidth = 2f;
        const float ButtonMargin = 14f;
        const float BubbleMargin = 10f;
        const int None = -1;

        struct BubbleState
        {
            public DemoSpring x, y, size;
            public bool shown, visible, hidden, moved;
            public float at;
        }

        DemoSpring buttonX, buttonY, turn, dim, press, jelly, pop;
        BubbleState[] states;
        Vector2 home, grab, dragTarget;
        float buttonRadius, bubbleRadius, openAt;
        int current, pressed = None;
        bool menuOpen, dragging, awake, buttonMoved, popMoved;

        void Awake()
        {
            home = button.rectTransform.anchoredPosition;
            bubbleRadius = bubbles[0].shape.rectTransform.sizeDelta.x * 0.5f;
            states = new BubbleState[bubbles.Length];
            current = FindOperand();

            ProceduralShape.ShapeOperation canvasOperation = shape.GetShapeOperation(0);
            DemoUtils.Show(menuSmoothing, button.GetShapeOperation(0).smoothing);
            DemoUtils.Show(showOperands, notchHandle.OutlineWidth > 0f);
            DemoUtils.Show(cradleSmoothing, bar.GetShapeOperation(0).smoothing);
            DemoUtils.Show(gap, notch.sizeDelta.x * 0.5f - ButtonRadius);
            DemoUtils.Show(operation, (int)canvasOperation.operation);
            DemoUtils.Show(canvasSmoothing, canvasOperation.smoothing);
        }

        // Each time the tab opens the bubbles start inside the button; with Open Menu on, they blob out shortly after
        void OnEnable()
        {
            buttonX.Set(260f, 0.5f);
            buttonY.Set(260f, 0.5f);
            turn.Set(240f, 0.65f);
            dim.Set(120f, 1f, 0.001f);
            press.Set(600f, 0.6f, 0.001f);
            jelly.Set(330f, 0.28f, 0.001f);
            pop.Set(320f, 0.45f, 0.001f);
            buttonX.Snap(home.x);
            buttonY.Snap(home.y);
            turn.Snap(0f);
            dim.Snap(1f);
            press.Snap(1f);
            jelly.Snap(0f);
            pop.Snap(1f);

            menuOpen = open == null || open.IsOn;
            openAt = Time.unscaledTime + OpenDelay;
            pressed = None;
            dragging = false;

            for (int i = 0; i < bubbles.Length; i++)
            {
                states[i] = new BubbleState { hidden = true, shown = menuOpen, at = openAt + i * Stagger };
                states[i].size.Set(300f, 0.7f);
                bubbles[i].shape.gameObject.SetActive(false);
                bubbles[i].dot.SetActive(i == current);
            }

            awake = true;
            buttonMoved = popMoved = false;
            ApplyButton();
            operand.rectTransform.sizeDelta = operands[current].size;
        }

        void OnDisable()
        {
            pressed = None;
            dragging = false;
        }

        void Update()
        {
            if (!awake)
                return;

            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            bool opened = menuOpen && now >= openAt, held = pressed == ButtonIndex && dragging;
            if (!held)
            {
                buttonX.target = home.x;
                buttonY.target = home.y;
            }

            turn.target = opened ? OpenAngle : 0f;
            dim.target = opened ? DimAlpha : 1f;
            press.target = pressed == ButtonIndex && !dragging ? PressScale : 1f;

            // Only moving parts are written, so settled graphics aren't rebuilt; each writes once more as it lands
            bool buttonMoving = buttonX.Advance(dt) | buttonY.Advance(dt) | turn.Advance(dt) | dim.Advance(dt)
                | press.Advance(dt) | jelly.Advance(dt) | held;
            if (buttonMoving || buttonMoved)
                ApplyButton();

            bool popping = pop.Advance(dt);
            if (popping || popMoved)
                operand.rectTransform.sizeDelta = operands[current].size * pop.value;

            buttonMoved = buttonMoving;
            popMoved = popping;
            bool busy = buttonMoving || popping || pressed != None;

            for (int i = 0; i < bubbles.Length; i++)
                busy |= UpdateBubble(i, dt, now);

            awake = busy;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            GameObject hit = eventData.pointerPressRaycast.gameObject;
            pressed = None;
            if (hit == button.gameObject)
                pressed = ButtonIndex;

            for (int i = 0; i < bubbles.Length; i++)
            {
                if (menuOpen && states[i].visible && !states[i].hidden && hit == bubbles[i].handle.gameObject)
                    pressed = i;
            }

            if (pressed == None || !TryGetLocal(eventData, out Vector2 local))
            {
                pressed = None;
                return;
            }

            dragging = false;
            if (pressed == ButtonIndex) { grab = new Vector2(buttonX.value, buttonY.value) - local; }
            else
            {
                ref BubbleState bubble = ref states[pressed];
                dragTarget = new Vector2(bubble.x.value, bubble.y.value);
                grab = dragTarget - local;
                bubble.x.Set(700f, 0.85f);
                bubble.y.Set(700f, 0.85f);
            }

            awake = true;
        }

        public void OnBeginDrag(PointerEventData eventData) => dragging = pressed != None;

        public void OnDrag(PointerEventData eventData)
        {
            if (pressed == None || !TryGetLocal(eventData, out Vector2 local))
                return;

            Rect bounds = area.rect;
            Vector2 position = local + grab;
            if (pressed == ButtonIndex)
            {
                // The button stays in the window, and never below its cradle
                float margin = ButtonRadius + ButtonMargin;
                buttonX.Snap(Mathf.Clamp(position.x, bounds.xMin + margin, bounds.xMax - margin));
                buttonY.Snap(Mathf.Clamp(position.y, home.y, bounds.yMax - margin));
            }
            else
            {
                float margin = bubbleRadius + BubbleMargin;
                dragTarget.x = Mathf.Clamp(position.x, bounds.xMin + margin, bounds.xMax - margin);
                dragTarget.y = Mathf.Clamp(position.y, bar.rectTransform.anchoredPosition.y - BubbleMargin, bounds.yMax - margin);
            }

            awake = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed == None)
                return;

            if (pressed == ButtonIndex)
            {
                if (!dragging)
                    SetMenu(!menuOpen);
            }
            else
            {
                // Released bubbles spring back to their place with a wobble; a tap picks that shape for the canvas
                states[pressed].x.Set(150f, 0.4f);
                states[pressed].y.Set(150f, 0.4f);
                if (!dragging)
                {
                    SetOperandShape(pressed);
                    SetMenu(false);
                }
            }

            pressed = None;
            dragging = false;
            awake = true;
        }

        public void SetMenuSmoothing(float value)
        {
            for (int i = 0; i < button.ShapeOperationCount; i++)
            {
                ProceduralShape.ShapeOperation menuOperation = button.GetShapeOperation(i);
                menuOperation.smoothing = value;
                button.SetShapeOperation(i, menuOperation);
            }
        }

        public void SetOpen(bool value)
        {
            // Before the page wakes up, its first enable reads the switch instead
            if (menuOpen == value || states == null)
                return;

            menuOpen = value;
            openAt = Time.unscaledTime;
            jelly.velocity -= 2.2f;

            for (int i = 0; i < bubbles.Length; i++)
            {
                states[i].at = openAt + (value ? i : bubbles.Length - 1 - i) * Stagger;
                states[i].shown = value;
            }

            awake = true;
        }

        // Draws nothing at zero width but still catches the pointer, so the bubbles stay draggable
        public void SetShowOperands(bool value)
        {
            float width = value ? HandleWidth : 0f;
            notchHandle.OutlineWidth = width;

            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].handle.OutlineWidth = width;
        }

        public void SetCradleSmoothing(float value)
        {
            ProceduralShape.ShapeOperation cradle = bar.GetShapeOperation(0);
            cradle.smoothing = value;
            bar.SetShapeOperation(0, cradle);
        }

        public void SetGap(float value) => notch.sizeDelta = Vector2.one * ((ButtonRadius + value) * 2f);

        public void SetOperation(int index)
        {
            ProceduralShape.ShapeOperation canvasOperation = shape.GetShapeOperation(0);
            canvasOperation.operation = (ProceduralShape.OperationType)index;
            shape.SetShapeOperation(0, canvasOperation);
        }

        public void SetCanvasSmoothing(float value)
        {
            ProceduralShape.ShapeOperation canvasOperation = shape.GetShapeOperation(0);
            canvasOperation.smoothing = value;
            shape.SetShapeOperation(0, canvasOperation);
        }

        public void SetOperandShape(int index)
        {
            current = index;
            OperandShape choice = operands[index];
            operand.Shape = handle.Shape = choice.shape;
            operand.PolygonSides = handle.PolygonSides = choice.sides;
            operand.CornerRadius = handle.CornerRadius = Vector4.one * choice.radius;
            pop.velocity += 3.2f;

            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].dot.SetActive(i == index);

            awake = true;
        }

        void SetMenu(bool value)
        {
            DemoUtils.Show(open, value);
            SetOpen(value);
        }

        // The button's pressed index comes after the bubbles'
        int ButtonIndex => bubbles.Length;

        // Read once at rest: sliders can call in before Awake, and the button's size moves while it animates
        float ButtonRadius => buttonRadius > 0f ? buttonRadius : buttonRadius = button.rectTransform.sizeDelta.x * 0.5f;

        int FindOperand()
        {
            for (int i = 0; i < operands.Length; i++)
            {
                if (operands[i].shape == operand.Shape && (operands[i].shape != ShapeType.Polygon || operands[i].sides == operand.PolygonSides))
                    return i;
            }

            return 0;
        }

        // Returns whether the bubble still moves
        bool UpdateBubble(int i, float dt, float now)
        {
            ref BubbleState bubble = ref states[i];
            ProceduralShape target = bubbles[i].shape;
            if (bubble.visible != bubble.shown && now >= bubble.at)
            {
                bubble.visible = bubble.shown;
                if (bubble.visible && bubble.hidden)
                {
                    bubble.hidden = false;
                    target.gameObject.SetActive(true);
                    bubble.x.Snap(buttonX.value);
                    bubble.y.Snap(buttonY.value);
                    bubble.size.Snap(bubbleRadius * 0.5f);
                }

                // Slow to leave the button, quick to settle back in
                float stiffness = bubble.visible ? 150f : 240f, zeta = bubble.visible ? 0.5f : 0.8f;
                bubble.x.Set(stiffness, zeta);
                bubble.y.Set(stiffness, zeta);
            }

            if (bubble.hidden)
                return bubble.visible != bubble.shown;

            Vector2 goal = new(buttonX.value, buttonY.value);
            if (pressed == i) { goal = dragTarget; }
            else if (bubble.visible)
            {
                float angle = (firstAngle - i * spread / Mathf.Max(1, bubbles.Length - 1)) * Mathf.Deg2Rad;
                goal += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            bubble.x.target = goal.x;
            bubble.y.target = goal.y;
            float distance = Vector2.Distance(new Vector2(bubble.x.value, bubble.y.value), new Vector2(buttonX.value, buttonY.value));
            bubble.size.target = bubble.visible ? bubbleRadius : distance < 30f ? 0f : bubbleRadius * 0.7f;
            bool moving = bubble.x.Advance(dt) | bubble.y.Advance(dt) | bubble.size.Advance(dt);

            // Back inside the button: no geometry at all, so it can't peek out while the button moves
            if (!bubble.visible && bubble.size.value < 1f && distance < 12f)
            {
                bubble.hidden = true;
                target.gameObject.SetActive(false);
                return false;
            }

            bool write = moving || bubble.moved;
            bubble.moved = moving;
            if (!write)
                return bubble.visible != bubble.shown;

            // Squash and stretch along the velocity
            Vector2 velocity = new(bubble.x.velocity, bubble.y.velocity);
            float stretch = Mathf.Clamp(velocity.magnitude / 4000f, 0f, 0.14f), diameter = Mathf.Max(bubble.size.value, 0f) * 2f;
            target.rectTransform.anchoredPosition = new Vector2(bubble.x.value, bubble.y.value);
            target.rectTransform.sizeDelta = new Vector2(diameter * (1f + stretch), diameter * (1f - 0.45f * stretch));
            target.ShapeAngle = bubbles[i].handle.ShapeAngle = velocity.sqrMagnitude > 1f ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0f;

            CanvasGroup glyph = bubbles[i].content;
            glyph.alpha = Mathf.SmoothStep(0f, 1f, (distance - 34f) / 50f);
            glyph.transform.localScale = Vector3.one * (bubble.size.value / bubbleRadius);
            return moving || bubble.visible != bubble.shown;
        }

        void ApplyButton()
        {
            float buttonSize = ButtonRadius * 2f * press.value * (1f + jelly.value * 0.06f);
            button.rectTransform.anchoredPosition = new Vector2(buttonX.value, buttonY.value);
            button.rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize * (1f - jelly.value * 0.05f));
            icon.localRotation = Quaternion.Euler(0f, 0f, -turn.value);
            icon.localScale = Vector3.one * press.value;
            content.alpha = dim.value;
        }

        bool TryGetLocal(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
