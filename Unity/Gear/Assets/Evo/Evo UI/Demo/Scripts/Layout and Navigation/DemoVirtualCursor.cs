using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Virtual Cursor tab: a launcher screen driven by keyboard and gamepad, so the demo's own mouse keeps working.
    /// Mouse hands the mouse to the cursor too, for the whole demo while the tab is open, and hides the system pointer.
    /// Selection movement keeps the selection on the screen: clicking an option takes it away, the screen takes it back.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoVirtualCursor : MonoBehaviour
    {
        [SerializeField] private VirtualCursor cursor;
        [SerializeField] private RectTransform screen;
        [Tooltip("The root canvas, for its event camera.")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Selectable firstSelection;

        [Header("Options")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private CanvasGroup speedOption;
        [SerializeField] private CanvasGroup smoothingOption;
        [SerializeField] private CanvasGroup mouseOption;
        [SerializeField] private CanvasGroup dashedOption;

        const VirtualCursor.InputDevices PadDevices = VirtualCursor.InputDevices.Keyboard | VirtualCursor.InputDevices.Gamepad;
        static readonly VirtualCursor.CursorPreset[] Presets = { VirtualCursor.CursorPreset.Ring, VirtualCursor.CursorPreset.Outline, VirtualCursor.CursorPreset.Corners };

        GameObject lastSelection;
        bool useMouse;

        bool Follow => cursor.movementMode == VirtualCursor.MovementMode.FollowSelection;

        void Awake()
        {
            DemoUtils.SetLocked(speedOption, Follow, false);
            DemoUtils.SetLocked(smoothingOption, Follow, false);
            DemoUtils.SetLocked(mouseOption, Follow, false);
            DemoUtils.SetLocked(dashedOption, cursor.preset == VirtualCursor.CursorPreset.Corners, false);
        }

        void OnEnable()
        {
            // Every visit starts in the middle of the screen
            var rect = (RectTransform)cursor.transform;
            rect.anchoredPosition = Vector2.zero;
            if (cursor.isActiveAndEnabled)
            {
                Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                cursor.SetScreenPosition(RectTransformUtility.WorldToScreenPoint(eventCamera, screen.TransformPoint(screen.rect.center)));
            }

            ApplyDevices();
            UpdateInfo();
            if (Follow)
                SelectOnScreen();
        }

        void OnDisable()
        {
            Cursor.visible = true;
            lastSelection = null;
        }

        void LateUpdate()
        {
            if (!Follow || EventSystem.current == null)
                return;

            // The header search keeps its focus while someone types
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(screen)) { lastSelection = selected; }
            else if (selected == null || !selected.TryGetComponent(out TMP_InputField _)) { SelectOnScreen(); }
        }

        void SelectOnScreen()
        {
            if (EventSystem.current == null)
                return;

            GameObject target = lastSelection != null && lastSelection.activeInHierarchy ? lastSelection : firstSelection.gameObject;
            EventSystem.current.SetSelectedGameObject(target);
            lastSelection = target;
        }

        public void SetMovement(int index)
        {
            cursor.movementMode = index == 0 ? VirtualCursor.MovementMode.Free : VirtualCursor.MovementMode.FollowSelection;
            DemoUtils.SetLocked(speedOption, Follow);
            DemoUtils.SetLocked(smoothingOption, Follow);
            DemoUtils.SetLocked(mouseOption, Follow);
            if (!isActiveAndEnabled)
                return;

            ApplyDevices();
            UpdateInfo();
            if (Follow) { SelectOnScreen(); }
            else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(screen))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SetPreset(int index)
        {
            cursor.preset = Presets[index];
            DemoUtils.SetLocked(dashedOption, cursor.preset == VirtualCursor.CursorPreset.Corners);
        }

        public void SetDashed(bool value) => cursor.dashMode = value ? VirtualCursor.DashMode.OnTarget : VirtualCursor.DashMode.None;

        public void SetMouse(bool value)
        {
            useMouse = value;
            if (!isActiveAndEnabled)
                return;

            ApplyDevices();
            UpdateInfo();
        }

        public void SetSpeed(float value) => cursor.movementSpeed = value;

        public void SetSmoothing(float value) => cursor.smoothing = value;

        public void SetPadding(float value) => cursor.targetPadding = new Vector2(value, value);

        // With the mouse it roams the whole demo, so the rest of the page stays reachable through it
        void ApplyDevices()
        {
            bool mouse = useMouse && !Follow;
            cursor.inputDevices = mouse ? PadDevices | VirtualCursor.InputDevices.Mouse : PadDevices;
            cursor.movementBounds = mouse ? (RectTransform)screen.root : screen;
            Cursor.visible = !mouse;
        }

        void UpdateInfo()
        {
            if (info == null)
                return;

            if (Follow) { info.SetText("Arrow keys or the D-pad move the selection. Enter or A clicks."); }
            else if (useMouse) { info.SetText("The mouse drives the cursor for the whole demo. Clicks land under its center dot."); }
            else { info.SetText("Move with WASD, the arrow keys or a stick. Enter or A clicks."); }
        }
    }
}
