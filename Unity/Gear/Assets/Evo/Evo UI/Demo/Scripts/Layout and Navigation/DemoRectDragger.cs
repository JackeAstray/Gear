using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Rect Dragger tab: HUD pieces that move inside a screen frame (Rect Bounds). The Party window drags by its title bar,
    /// its Drag Source; with Title bar only off, its body forwards drags to the dragger as well.
    /// A piece comes to the front as soon as it starts moving.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoRectDragger : MonoBehaviour
    {
        [SerializeField] private RectDragger[] pieces;
        [SerializeField] private DemoDragForwarder partyBody;

        [Header("Options")]
        [SerializeField] private CanvasGroup returnOption;

        void Awake()
        {
            foreach (RectDragger piece in pieces)
            {
                Transform target = piece.transform;
                piece.OnDragUpdate += _ =>
                {
                    if (target.GetSiblingIndex() != target.parent.childCount - 1)
                        target.SetAsLastSibling();
                };
            }
        }

        public void SetAxis(int index)
        {
            foreach (RectDragger piece in pieces)
                piece.axisConstraint = (RectDragger.AxisConstraint)index;
        }

        public void SetOvershoot(bool value)
        {
            foreach (RectDragger piece in pieces)
                piece.allowOutOfBounds = value;

            DemoUtils.SetLocked(returnOption, !value);
        }

        public void SetTitleBarOnly(bool value) => partyBody.enabled = !value;

        public void SetReturnTime(float value)
        {
            foreach (RectDragger piece in pieces)
                piece.returnDuration = value;
        }

        public void ResetLayout()
        {
            foreach (RectDragger piece in pieces)
                piece.ResetToOriginalPosition();
        }
    }
}
