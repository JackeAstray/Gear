using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Radial Layout Group tab: an item wheel. The slots are the group's children, so the count, arc and radius
    /// re-space them right away. Hovering a slot previews it in the middle (its icon and a name bar); clicking equips it.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRadialLayout : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public Button button;
            public Sprite icon;
            [Tooltip("Width of the name bar in the middle.")]
            public float nameWidth = 110f;
        }

        [SerializeField] private RadialLayoutGroup group;
        [SerializeField] private Slot[] slots;
        [SerializeField] private int[] counts = { 4, 6, 8 };
        [SerializeField] private int equipped = 1;

        [Header("Ring")]
        [SerializeField] private ProceduralShape track;
        [SerializeField] private ProceduralShape arc;

        [Header("Center")]
        [SerializeField] private Image centerIcon;
        [SerializeField] private StylerObject centerIconColor;
        [SerializeField] private RectTransform centerName;
        [Tooltip("A pill under the name: long and accent while the equipped item shows, short while another is previewed.")]
        [SerializeField] private ProceduralShape centerState;
        [SerializeField] private Vector2 stateWidths = new(64f, 40f);

        [Header("Options")]
        [SerializeField] private Slider startOption;
        [SerializeField] private Slider endOption;

        int count;
        int hovered = -1;

        void Awake()
        {
            count = group.transform.childCount;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].button.onClick.AddListener(() => Equip(index));
                slots[i].button.onPointerEnter.AddListener(() => Hover(index));
                slots[i].button.onPointerExit.AddListener(() => Unhover(index));
            }

            RefreshSlots();
            UpdateRing();
        }

        void Hover(int index)
        {
            hovered = index;
            UpdateCenter();
        }

        void Unhover(int index)
        {
            if (hovered == index)
                hovered = -1;

            UpdateCenter();
        }

        void Equip(int index)
        {
            equipped = index;
            RefreshSlots();
        }

        void RefreshSlots()
        {
            if (equipped >= count)
                equipped = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].button.gameObject.SetActive(i < count);
                slots[i].button.SetState(i == equipped ? InteractionState.Selected : InteractionState.Normal);
            }

            UpdateCenter();
        }

        void UpdateCenter()
        {
            int shown = hovered >= 0 ? hovered : equipped;
            bool isEquipped = shown == equipped;
            centerIcon.sprite = slots[shown].icon;
            centerIconColor.colorID = isEquipped ? "Accent" : "Primary";
            centerIconColor.UpdateStyler();
            centerName.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, slots[shown].nameWidth);

            centerState.fillStyler.color.stylerID = isEquipped ? "Accent" : "Primary (Soft)";
            centerState.UpdateStyler();
            centerState.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, isEquipped ? stateWidths.x : stateWidths.y);
        }

        // The track runs through the slot centers; the arc marks the covered sweep when it's less than a circle
        void UpdateRing()
        {
            float radius = group.Radius;
            var ring = (RectTransform)track.transform;
            ring.sizeDelta = Vector2.one * radius * 2f;
            ((RectTransform)arc.transform).sizeDelta = ring.sizeDelta;

            float sweep = group.EndAngle - group.StartAngle;
            bool partial = sweep > 0.1f && sweep < 359.9f;
            arc.gameObject.SetActive(partial);
            if (!partial)
                return;

            // Radial clipping starts at the top and runs clockwise or counterclockwise
            float first = group.Clockwise ? -group.StartAngle : group.StartAngle;
            arc.ClipClockwise = group.Clockwise;
            arc.ClipAmount = sweep / 360f;
            arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, first - 90f);
        }

        public void SetCount(int index)
        {
            count = counts[index];
            RefreshSlots();
        }

        public void SetStartAngle(float value)
        {
            group.StartAngle = value;
            DemoUtils.Show(startOption, group.StartAngle);
            UpdateRing();
        }

        public void SetEndAngle(float value)
        {
            group.EndAngle = value;
            DemoUtils.Show(endOption, group.EndAngle);
            UpdateRing();
        }

        public void SetRadius(float value)
        {
            // Slot centers sit on the group's edge, so its size is twice the radius
            var rect = (RectTransform)group.transform;
            rect.sizeDelta = Vector2.one * value * 2f;
            LayoutRebuilder.MarkLayoutForRebuild(rect);
            UpdateRing();
        }

        public void SetClockwise(bool value)
        {
            group.Clockwise = value;
            UpdateRing();
        }
    }
}
