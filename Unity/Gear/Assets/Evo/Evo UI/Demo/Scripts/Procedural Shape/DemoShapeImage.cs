using UnityEngine;
using UnityEngine.EventSystems;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Image tab: a stack of photos. Drag the front photo away or tap it, and it tucks in at the back while the
    /// others move up. Each photo is one shape: the sprite, its outline as the border, and a shadow.
    /// The photos' places in the scene are the stack's slots, and the options start from the front photo's values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeImage : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [System.Serializable]
        struct Photo
        {
            public ProceduralShape shape;
            [Tooltip("Label chip in the lower left, shown on the front photo only.")]
            public CanvasGroup tag;
        }

        [Tooltip("Front to back.")]
        [SerializeField] private Photo[] photos;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup scale;
        [SerializeField] private Slider radius;
        [SerializeField] private Slider border;

        const float TapDistance = 6f;
        const float ThrowDistance = 130f;
        const float ThrowSpeed = 900f;
        const float ThrowDuration = 0.2f;
        const float ThrowRate = 18f;
        const float TagInset = 14f;
        const float TagRate = 14f;
        const float DragTilt = 0.05f;
        static readonly Vector3 ThrowOffset = new(400f, 50f, 18f);
        const int None = -1;

        struct State
        {
            public DemoSpring x, y, angle;
            public bool moved;
        }

        State[] states;
        Vector3[] slots;
        int[] order;
        Vector2 grab, pressPoint;
        float speed, lastX, lastTime, throwTime;
        int held = None, thrown = None, throwSide;
        bool awake;

        void Awake()
        {
            states = new State[photos.Length];
            slots = new Vector3[photos.Length];
            order = new int[photos.Length];

            for (int i = 0; i < photos.Length; i++)
            {
                RectTransform rect = photos[i].shape.rectTransform;
                slots[i] = new Vector3(rect.anchoredPosition.x, rect.anchoredPosition.y, Mathf.DeltaAngle(0f, rect.localEulerAngles.z));
                order[i] = i;
                states[i].x.Set(170f, 0.84f);
                states[i].y.Set(170f, 0.84f);
                states[i].angle.Set(170f, 0.84f);
            }

            ProceduralShape front = photos[0].shape;
            DemoUtils.Show(scale, (int)front.ImageScaleMode);
            DemoUtils.Show(radius, front.UniformCornerRadius);
            DemoUtils.Show(border, front.OutlineWidth);
            PlaceTags();
        }

        // The stack keeps its order between visits; everything else lands in place
        void OnEnable()
        {
            held = None;
            if (thrown != None)
                TuckIn();

            for (int slot = 0; slot < order.Length; slot++)
            {
                int i = order[slot];
                states[i].x.Snap(slots[slot].x);
                states[i].y.Snap(slots[slot].y);
                states[i].angle.Snap(slots[slot].z);
                states[i].moved = false;
                Apply(i);
                photos[i].tag.alpha = slot == 0 ? 1f : 0f;
            }

            awake = false;
        }

        void OnDisable() => held = None;

        void Update()
        {
            if (!awake)
                return;

            float dt = Time.unscaledDeltaTime;
            bool busy = held != None;

            if (thrown != None)
            {
                // A short flick out to the side, then it goes to the back and springs into the last slot
                ref State flick = ref states[thrown];
                float k = 1f - Mathf.Exp(-ThrowRate * dt);
                flick.x.Snap(Mathf.Lerp(flick.x.value, slots[0].x + throwSide * ThrowOffset.x, k));
                flick.y.Snap(Mathf.Lerp(flick.y.value, slots[0].y + ThrowOffset.y, k));
                flick.angle.Snap(Mathf.Lerp(flick.angle.value, slots[0].z - throwSide * ThrowOffset.z, k));
                Apply(thrown);

                if (Time.unscaledTime - throwTime >= ThrowDuration)
                    TuckIn();

                busy = true;
            }

            for (int slot = 0; slot < order.Length; slot++)
            {
                int i = order[slot];
                if (i == held || i == thrown)
                    continue;

                ref State state = ref states[i];
                state.x.target = slots[slot].x;
                state.y.target = slots[slot].y;
                state.angle.target = slots[slot].z;

                // Only moving photos are written, so settled ones aren't rebuilt; each writes once more as it lands
                bool moving = state.x.Advance(dt) | state.y.Advance(dt) | state.angle.Advance(dt);
                if (moving || state.moved)
                    Apply(i);

                state.moved = moving;
                busy |= moving;
            }

            for (int slot = 0; slot < order.Length; slot++)
            {
                CanvasGroup tag = photos[order[slot]].tag;
                float target = slot == 0 && thrown == None ? 1f : 0f;
                if (tag.alpha == target)
                    continue;

                float alpha = Mathf.Lerp(tag.alpha, target, 1f - Mathf.Exp(-TagRate * dt));
                tag.alpha = Mathf.Abs(alpha - target) < 0.002f ? target : alpha;
                busy = true;
            }

            awake = busy;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            held = None;
            int front = order[0];
            if (thrown != None || eventData.pointerPressRaycast.gameObject != photos[front].shape.gameObject || !TryGetLocal(eventData, out Vector2 local))
                return;

            held = front;
            grab = new Vector2(states[front].x.value, states[front].y.value) - local;
            pressPoint = local;
            lastX = local.x;
            lastTime = Time.unscaledTime;
            speed = 0f;
            awake = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (held == None || !TryGetLocal(eventData, out Vector2 local))
                return;

            // The photo follows the pointer and leans the way it's pulled
            ref State state = ref states[held];
            Vector2 position = local + grab;
            state.x.Snap(position.x);
            state.y.Snap(position.y);
            state.angle.Snap(slots[0].z - (position.x - slots[0].x) * DragTilt);
            Apply(held);

            float dt = Time.unscaledTime - lastTime;
            if (dt > 0f)
                speed = Mathf.Lerp(speed, (local.x - lastX) / dt, 0.4f);

            lastX = local.x;
            lastTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (held == None)
                return;

            int photo = held;
            held = None;
            awake = true;

            float offset = states[photo].x.value - slots[0].x;
            bool tapped = !TryGetLocal(eventData, out Vector2 local) || Vector2.Distance(local, pressPoint) < TapDistance;
            if (tapped) { Throw(photo, 1); }
            else if (Mathf.Abs(offset) > ThrowDistance || Mathf.Abs(speed) > ThrowSpeed) { Throw(photo, offset >= 0f ? 1 : -1); }
        }

        public void SetScaleMode(int index)
        {
            foreach (Photo photo in photos)
                photo.shape.ImageScaleMode = (ProceduralShape.ScaleMode)index;

            PlaceTags();
        }

        public void SetCornerRadius(float value)
        {
            foreach (Photo photo in photos)
                photo.shape.UniformCornerRadius = value;
        }

        public void SetBorder(float value)
        {
            foreach (Photo photo in photos)
                photo.shape.OutlineWidth = value;

            PlaceTags();
        }

        void Throw(int photo, int side)
        {
            thrown = photo;
            throwSide = side;
            throwTime = Time.unscaledTime;
        }

        // The thrown photo goes behind the others, and every photo moves up a slot
        void TuckIn()
        {
            for (int slot = 0; slot < order.Length - 1; slot++)
                order[slot] = order[slot + 1];

            order[order.Length - 1] = thrown;
            photos[thrown].shape.rectTransform.SetAsFirstSibling();
            thrown = None;
        }

        // Tags sit inside the border at the lower left of what the photo draws, which Fit makes smaller than its rect
        void PlaceTags()
        {
            foreach (Photo photo in photos)
            {
                Rect drawn = photo.shape.GetDrawingRect();
                RectTransform tag = (RectTransform)photo.tag.transform;
                float inset = photo.shape.OutlineWidth + TagInset;
                tag.anchoredPosition = new Vector2(drawn.xMin + inset, drawn.yMin + inset) + tag.sizeDelta * 0.5f;
            }
        }

        void Apply(int i)
        {
            RectTransform rect = photos[i].shape.rectTransform;
            rect.anchoredPosition = new Vector2(states[i].x.value, states[i].y.value);
            rect.localRotation = Quaternion.Euler(0f, 0f, states[i].angle.value);
        }

        bool TryGetLocal(PointerEventData eventData, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out local);
        }
    }
}
