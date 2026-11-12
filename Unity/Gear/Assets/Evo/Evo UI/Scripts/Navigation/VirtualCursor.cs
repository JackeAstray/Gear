using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// A navigation-driven UI pointer with an optional target frame. The center marker is the raycast origin.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Navigation/Virtual Cursor"), HelpURL(Constants.HelpUrl)]
    public class VirtualCursor : MonoBehaviour, IStylerHandler
    {
        // Input
        [Tooltip("Uses the current Event System when left empty. One virtual cursor can own each UI input module.")]
        public InputSystemUIInputModule inputModule;
        public MovementMode movementMode = MovementMode.Free;
        public InputDevices inputDevices = InputDevices.Keyboard | InputDevices.Gamepad | InputDevices.Mouse;
        [Min(0f), Tooltip("Movement speed in canvas units per second. Uses unscaled time.")]
        public float movementSpeed = 900f;
        [Min(0f), Tooltip("Position smoothing time for all Free-mode input devices. Zero gives immediate movement.")]
        public float smoothing = 0.04f;
        [Range(0f, 0.95f)] public float deadZone = 0.1f;
        [Tooltip("Clamp the cursor to this rect, or its parent rect when empty.")]
        public RectTransform movementBounds;
        [Tooltip("Fade out when there is no hovered target in Free mode or no selection in Follow Selection mode.")]
        public bool hideWithoutSelection = false;

        // Interaction
        [Tooltip("Dispatch hover, down, up, click, and drag events at the center marker. Submit is used as the pointer button.")]
        public bool enableRaycast = true;
        [Tooltip("When raycast is disabled, the target frame can still identify hovered elements without sending pointer events.")]
        public bool fitToTarget = true;
        public Vector2 targetPadding = new(10f, 10f);

        // Animation
        [Min(0f), Tooltip("Time to move between selected elements. Zero snaps immediately.")]
        public float selectionDuration = 0.2f;
        [Min(0f), Tooltip("Duration of frame, color, and visibility transitions. Zero snaps immediately.")]
        public float transitionDuration = 0.2f;
        public Ease transitionEase = Ease.OutCubic;
        public AnimationCurve customEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // Appearance
        public CursorPreset preset = CursorPreset.Outline;
        [Tooltip("A prefab or scene object to clone as the cursor visual. Its root should be centered; its original is never modified.")]
        public RectTransform customVisual;
        [Min(0f)] public float outlineWidth = 4f;
        [Min(0f)] public float cornerLength = 10f;
        [Min(0f)] public float centerSize = 10f;
        [Range(0f, 32f)] public float edgeSoftness = 0f;

        // Style
        public StyleProvider styleProvider = StyleProvider.Custom;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        public StylerPreset stylerPreset;
        public Color normalColor = Color.white;
        public Color targetColor = new(0.4f, 0.8f, 1f, 1f);
        public Color pressedColor = new(1f, 0.8f, 0.3f, 1f);
        public ColorMapping normalStyle = new() { stylerID = "Primary" };
        public ColorMapping targetStyle = new() { stylerID = "Primary" };
        public ColorMapping pressedStyle = new() { stylerID = "Accent" };

        // Frame
        [Min(0f)] public float cornerRadius = 12f;
        [Tooltip("Target Only checks the target object itself. Target And Children checks active children only if the target has no Procedural Shape.")]
        public RadiusSyncMode radiusSyncMode = RadiusSyncMode.TargetAndChildren;
        public DashMode dashMode = DashMode.None;
        [Min(0.1f)] public float dashLength = 20f;
        [Min(0f)] public float dashGap = 4f;
        public float dashOffset = 0f;
        public bool roundedDashes = true;
        [Tooltip("When to animate visible dashes. On Target includes hover and selection.")]
        public DashMode dashAnimation = DashMode.Always;
        [Tooltip("Dash offset speed in canvas units per second. Negative values reverse the direction.")]
        public float dashSpeed = 2f;

        // Enums
        [Flags]
        public enum InputDevices { None = 0, Keyboard = 1, Gamepad = 2, Mouse = 8, Other = 4, All = Keyboard | Gamepad | Mouse | Other }
        public enum MovementMode { Free, FollowSelection }
        public enum CursorPreset { Custom = 0, Ring = 1, Outline = 2, Corners = 3 }
        public enum RadiusSyncMode { None, TargetOnly, TargetAndChildren }
        public enum DashMode { None, Always, OnTarget, WhilePressed }

        // Cache
        RectTransform rectTransform;
        RectTransform parentRect;
        Canvas canvas;
        EventSystem eventSystem;
        Vector2 velocity;
        Vector2 movementPosition;
        readonly Vector3[] worldCorners = new Vector3[4];
        readonly List<RaycastResult> raycastResults = new(16);
        PointerEventData pointerData;
        RectTransform targetRect;
        ProceduralShape targetShape;
        Selectable targetSelectable;
        Graphic targetGraphic;
        VirtualCursorOverride targetOverride;
        RadiusSyncMode resolvedRadiusMode;
        GameObject targetObject;
        RectTransform selectionTarget;
        Vector2 selectionStart;
        Vector2 selectionDestination;
        float selectionTime;
        bool focused = true;
        bool pointerHeld;
        bool releasing;

        // Properties
        public Vector2 ScreenPosition { get; private set; }
        public GameObject CurrentTarget => targetObject;
        public bool IsPressed => pointerHeld;
        Camera CanvasCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        void OnEnable()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            rectTransform = (RectTransform)transform;
            CacheCanvas();
            ResetMovement();
            focused = true;
            BuildVisual();
            Styler.Register(this);
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            Styler.Unregister(this);
            CancelPointer();
            ReleaseInput();
            SetTarget(null);
            velocity = Vector2.zero;
            selectionTarget = null;

            if (visualRoot != null)
                visualRoot.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (visualRoot != null)
                Destroy(visualRoot.gameObject);
        }

        void OnTransformParentChanged()
        {
            CacheCanvas();
            ResetMovement();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            targetPadding = Vector2.Max(Vector2.zero, targetPadding);
            deadZone = Mathf.Clamp(deadZone, 0, 0.95f);
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }

        void Reset()
        {
            useDefaultStylerPreset = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }
#endif

        void CacheCanvas()
        {
            parentRect = transform.parent as RectTransform;
            canvas = GetComponentInParent<Canvas>();
            if (visualCanvas != null)
                UpdateCanvasSorting();
        }

        void UpdateCanvasSorting()
        {
            visualCanvas.overrideSorting = true;
            visualCanvas.sortingOrder = short.MaxValue;
            SortingLayer[] layers = SortingLayer.layers;
            int highest = int.MinValue;
            foreach (SortingLayer layer in layers)
            {
                if (layer.value <= highest)
                    continue;

                highest = layer.value;
                visualCanvas.sortingLayerID = layer.id;
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            ResetMovement();
            if (!focused)
            {
                CancelPointer();
                ReleaseInput();
            }
        }

        void Update()
        {
            if (!focused || parentRect == null || canvas == null)
                return;

            UpdateInput();

            if (movementMode != MovementMode.Free)
                return;

            selectionTarget = null;
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (deltaTime <= 0)
                return;

            if (!MoveFromMouse())
            {
                Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
                float magnitude = input.magnitude;
                input = magnitude <= deadZone ? Vector2.zero : input / magnitude * Mathf.InverseLerp(deadZone, 1f, magnitude);
                movementPosition += deltaTime * Mathf.Max(0, movementSpeed) * input;
            }

            movementPosition = ClampPosition(movementPosition);
            Vector2 position = smoothing <= 0 ? movementPosition : Vector2.SmoothDamp(GetLocalCenter(), movementPosition,
                ref velocity, smoothing, Mathf.Infinity, deltaTime);
            if (smoothing <= 0)
                velocity = Vector2.zero;

            SetLocalCenter(ClampPosition(position));
        }

        void LateUpdate()
        {
            if (!focused || parentRect == null || canvas == null)
            {
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);

                return;
            }

            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected != null && !selected.activeInHierarchy)
                selected = null;

            RectTransform selectionRect = selected != null ? selected.transform as RectTransform : null;
            if (movementMode == MovementMode.FollowSelection && selectionRect == null && hideWithoutSelection)
            {
                CancelPointer();
                SetTarget(null);
                selectionTarget = null;
                if (isActiveAndEnabled)
                    UpdateVisual(false);
                return;
            }

            if (movementMode == MovementMode.FollowSelection && selectionRect != null)
                FollowSelection(selectionRect);
            else
                selectionTarget = null;

            if (!isActiveAndEnabled)
                return;

            ScreenPosition = RectTransformUtility.WorldToScreenPoint(CanvasCamera, rectTransform.TransformPoint(rectTransform.rect.center));
            GameObject hit = null;
            if (eventSystem != null && (enableRaycast || movementMode == MovementMode.Free))
            {
                pointerData.Reset();
                pointerData.delta = ScreenPosition - pointerData.position;
                pointerData.position = ScreenPosition;
                pointerData.scrollDelta = Vector2.zero;
                pointerData.pointerCurrentRaycast = Raycast();
                hit = pointerData.pointerCurrentRaycast.gameObject;
            }

            SetTarget(movementMode == MovementMode.FollowSelection ? selected : FindTarget(hit));
            if (enableRaycast && eventSystem != null)
            {
                UpdateHover(hit);

                if (!isActiveAndEnabled)
                    return;

                if (mouseScrollAction != null && mouseScrollAction.WasPerformedThisFrame())
                {
                    Vector2 scroll = mouseScrollAction.ReadValue<Vector2>();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                        scroll /= 120f;
#endif
                    pointerData.scrollDelta = scroll * ownedModule.scrollDeltaPerTick;
                    if (pointerData.scrollDelta != Vector2.zero)
                        ExecuteEvents.ExecuteHierarchy(hit, pointerData, ExecuteEvents.scrollHandler);
                    if (!isActiveAndEnabled)
                        return;
                }

                bool pressed = (submitAction != null && submitAction.WasPressedThisFrame())
                    || (mouseClickAction != null && mouseClickAction.WasPressedThisFrame());
                // Never activate an unrelated control while crossing it or while the selection is occluded
                bool onSelection = movementMode != MovementMode.FollowSelection || selected == null
                    || (hit != null && hit.transform.IsChildOf(selected.transform));

                if (pressed && !pointerHeld && onSelection)
                    PressPointer(hit);
                if (!isActiveAndEnabled)
                    return;

                DragPointer();

                if (!isActiveAndEnabled)
                    return; 
                if (pointerHeld && (submitAction == null || !submitAction.IsPressed()) && (mouseClickAction == null || !mouseClickAction.IsPressed()))
                    ReleasePointer(hit, true);
            }
            else { CancelPointer(); }

            if (isActiveAndEnabled)
                UpdateVisual(!hideWithoutSelection || targetObject != null);
        }

        Vector2 GetLocalCenter() => parentRect.InverseTransformPoint(rectTransform.TransformPoint(rectTransform.rect.center));

        void ResetMovement()
        {
            velocity = Vector2.zero;
            if (rectTransform != null && parentRect != null)
                movementPosition = GetLocalCenter();
        }

        void FollowSelection(RectTransform selection)
        {
            Canvas selectionCanvas = selection.GetComponentInParent<Canvas>();
            Camera camera = selectionCanvas != null && selectionCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? selectionCanvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, selection.TransformPoint(selection.rect.center));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, CanvasCamera, out selectionDestination))
                return;

            if (selectionTarget != selection)
            {
                if (pointerHeld) { CancelPointer(); }
                selectionTarget = selection;
                selectionStart = GetLocalCenter();
                selectionTime = 0;
            }

            // Confirming completes the movement before raycasting, so the press still occurs at the marker
            if (enableRaycast && submitAction != null && submitAction.WasPressedThisFrame())
                selectionTime = selectionDuration;

            SetLocalCenter(Vector2.LerpUnclamped(selectionStart, selectionDestination, Animate(ref selectionTime, selectionDuration)));
        }

        float Animate(ref float elapsed, float duration)
        {
            if (duration <= 0)
                return 1f;

            elapsed = Mathf.Min(elapsed + Time.unscaledDeltaTime, duration);
            return elapsed >= duration ? 1f : TweenEase.Evaluate(transitionEase, elapsed / duration, customEase);
        }

        void SetLocalCenter(Vector2 position)
        {
            Vector3 offset = parentRect.TransformPoint(position) - rectTransform.TransformPoint(rectTransform.rect.center);
            rectTransform.position += offset;
        }

        public void SetScreenPosition(Vector2 position)
        {
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, position, CanvasCamera, out Vector2 local))
            {
                SetLocalCenter(local);
                ResetMovement();
            }
        }

        Vector2 ClampPosition(Vector2 position)
        {
            RectTransform bounds = movementBounds != null ? movementBounds : parentRect;
            Vector3 local = bounds.InverseTransformPoint(parentRect.TransformPoint(position));
            local.x = Mathf.Clamp(local.x, bounds.rect.xMin, bounds.rect.xMax);
            local.y = Mathf.Clamp(local.y, bounds.rect.yMin, bounds.rect.yMax);
            return parentRect.InverseTransformPoint(bounds.TransformPoint(local));
        }

        RaycastResult Raycast()
        {
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, raycastResults);
            for (int i = 0; i < raycastResults.Count; i++)
            {
                RaycastResult result = raycastResults[i];
                if (result.gameObject != null && !result.gameObject.transform.IsChildOf(transform))
                    return result;
            }
            return default;
        }

        static GameObject FindTarget(GameObject hit)
        {
            if (hit == null)
                return null;

            Selectable selectable = hit.GetComponentInParent<Selectable>();
            if (selectable != null)
                return selectable.IsActive() && selectable.IsInteractable() ? selectable.gameObject : null;

            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            if (handler == null)
                handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit);
            if (handler != null)
                return handler;

            VirtualCursorOverride cursorOverride = hit.GetComponentInParent<VirtualCursorOverride>();
            return cursorOverride != null && cursorOverride.isActiveAndEnabled ? cursorOverride.gameObject : null;
        }

        void SetTarget(GameObject value)
        {
            bool changed = targetObject != value || resolvedRadiusMode != radiusSyncMode;
            if (targetObject != value)
            {
                targetObject = value;
                targetSelectable = value != null ? value.GetComponent<Selectable>() : null;
                targetOverride = value != null ? value.GetComponent<VirtualCursorOverride>() : null;
            }
           
            Graphic graphic = targetSelectable != null ? targetSelectable.targetGraphic : null;
            if (changed || targetGraphic != graphic || (targetShape != null && !targetShape.isActiveAndEnabled))
            {
                targetGraphic = graphic;
                resolvedRadiusMode = radiusSyncMode;
                targetShape = value != null ? value.GetComponent<ProceduralShape>() : null;
                if (targetShape == null && value != null && radiusSyncMode == RadiusSyncMode.TargetAndChildren)
                    targetShape = value.GetComponentInChildren<ProceduralShape>();
            }

            targetRect = targetShape != null ? targetShape.rectTransform : value != null ? value.transform as RectTransform : null;
        }

        #region Input
        static readonly Dictionary<InputSystemUIInputModule, VirtualCursor> owners = new();
        // Pointer IDs are event tokens, not Unity object IDs. Keep them independent of EntityId's size.
        // Reserve negative values below the built-in mouse/touch pointer IDs (-1 through -4).
        static int nextPointerId = -99;
        readonly int pointerId = System.Threading.Interlocked.Decrement(ref nextPointerId);
        readonly List<InputDevice> allowedDevices = new();
        InputSystemUIInputModule ownedModule;
        InputActionAsset inputAsset;
        InputAction moveAction, submitAction;
        InputAction mousePointAction, mouseClickAction, mouseScrollAction;
        InputActionReference originalMove, originalSubmit;
        InputActionReference filteredMove, filteredSubmit;
        InputActionReference installedMove, installedSubmit;
        InputActionReference[] originalPointerActions, filteredPointerActions;
        MovementMode activeMode;
        InputDevices activeDevices;
        bool activeRaycast;
        bool inputDirty;

        void OnDeviceChange(InputDevice device, InputDeviceChange change) => inputDirty = true;

        void UpdateInput()
        {
            InputSystemUIInputModule module = inputModule;
            if (module == null && EventSystem.current != null)
            {
                module = EventSystem.current.currentInputModule as InputSystemUIInputModule;
                if (module == null)
                    module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            }

            if (module != null && (!module.isActiveAndEnabled || (module.GetComponent<EventSystem>().currentInputModule != null
                && module.GetComponent<EventSystem>().currentInputModule != module)))
                module = null;

            if (inputAsset != null && (ownedModule == null || ownedModule != module || inputDirty || activeMode != movementMode
                || activeDevices != inputDevices || activeRaycast != enableRaycast || ownedModule.move != installedMove
                || ownedModule.submit != installedSubmit || PointerActionsChanged()))
            {
                CancelPointer();
                ReleaseInput();
            }

            if (ownedModule != null || module == null)
                return;
            if (owners.TryGetValue(module, out VirtualCursor owner) && owner != null && owner != this)
                return;

            ownedModule = module;
            owners[module] = this;
            eventSystem = module.GetComponent<EventSystem>();
            pointerData = new PointerEventData(eventSystem)
            {
                pointerId = this.pointerId,
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(CanvasCamera, rectTransform.TransformPoint(rectTransform.rect.center))
            };
            originalMove = module.move;
            originalSubmit = module.submit;
            inputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            inputAsset.hideFlags = HideFlags.HideAndDontSave;
            moveAction = CopyAction(originalMove, "Move");
            submitAction = CopyAction(originalSubmit, "Submit");
           
            if (movementMode == MovementMode.Free && (inputDevices & InputDevices.Mouse) != 0)
            {
                mousePointAction = CopyAction(module.point, "Mouse Point", mouseOnly: true);
                mouseClickAction = CopyAction(module.leftClick, "Mouse Click", mouseOnly: true);
                mouseScrollAction = CopyAction(module.scrollWheel, "Mouse Scroll", mouseOnly: true);
                
                if (enableRaycast)
                {
                    // Keep other pointer devices working while the virtual cursor handles this mouse
                    originalPointerActions = new InputActionReference[5];
                    filteredPointerActions = new InputActionReference[5];

                    for (int i = 0; i < originalPointerActions.Length; i++)
                    {
                        originalPointerActions[i] = GetPointerAction(module, i);
                        InputAction action = CopyAction(originalPointerActions[i], "Native Pointer " + i, excludeMouse: true);
                        filteredPointerActions[i] = action != null ? InputActionReference.Create(action) : null;
                    }

                    // Assigning a reference enables it; finish building the asset before installing any action
                    for (int i = 0; i < filteredPointerActions.Length; i++)
                        SetPointerAction(module, i, filteredPointerActions[i]);
                }
            }

            filteredMove = moveAction != null ? InputActionReference.Create(moveAction) : null;
            filteredSubmit = submitAction != null ? InputActionReference.Create(submitAction) : null;
            installedMove = movementMode == MovementMode.FollowSelection ? filteredMove : null;
            installedSubmit = enableRaycast ? null : filteredSubmit;
            module.move = installedMove;
            module.submit = installedSubmit;
            moveAction?.Enable();
            submitAction?.Enable();
            mousePointAction?.Enable();
            mouseClickAction?.Enable();
            mouseScrollAction?.Enable();
            activeMode = movementMode;
            activeDevices = inputDevices;
            activeRaycast = enableRaycast;
            inputDirty = false;

            ResetMovement();
        }

        InputAction CopyAction(InputActionReference reference, string name, bool mouseOnly = false, bool excludeMouse = false)
        {
            InputAction source = reference != null ? reference.action : null;
            if (source == null)
                return null;

            InputActionMap map = inputAsset.AddActionMap(name);
            InputAction action = map.AddAction(name, source.type, interactions: source.interactions,
                processors: source.processors, expectedControlLayout: source.expectedControlType);
            action.wantsInitialStateCheck = source.wantsInitialStateCheck;
            action.bindingMask = source.bindingMask;
            map.bindingMask = source.actionMap?.bindingMask;

            InputActionAsset sourceAsset = source.actionMap?.asset;
            InputBinding? assetMask = sourceAsset != null ? sourceAsset.bindingMask : null;
            foreach (InputBinding sourceBinding in source.bindings)
            {
                InputBinding binding = new()
                {
                    name = sourceBinding.name,
                    path = sourceBinding.effectivePath,
                    interactions = sourceBinding.effectiveInteractions,
                    processors = sourceBinding.effectiveProcessors,
                    groups = sourceBinding.groups,
                    isComposite = sourceBinding.isComposite,
                    isPartOfComposite = sourceBinding.isPartOfComposite
                };

                // Unity's default navigation composites quantize sticks into eight directions.
                // Read their axes continuously in Free mode; explicit user composite settings are preserved.
                if (name == "Move" && movementMode == MovementMode.Free && binding.isComposite
                    && string.Equals(binding.path, "2DVector", System.StringComparison.OrdinalIgnoreCase))
                    binding.path = "2DVector(mode=2)";

                // Keep composite headers; the device/control-scheme restrictions apply to their parts
                if (assetMask.HasValue && !binding.isComposite && !assetMask.Value.Matches(binding))
                    binding.path = "";

                action.AddBinding(binding);
            }

            allowedDevices.Clear();
            ReadOnlyArray<InputDevice>? sourceDevices = source.actionMap?.devices;
            foreach (InputDevice device in InputSystem.devices)
            {
                InputDevices category = device is Keyboard ? InputDevices.Keyboard : device is Gamepad
                    ? InputDevices.Gamepad : device is Mouse ? InputDevices.Mouse : InputDevices.Other;

                if (!device.enabled || (mouseOnly && device is not Mouse))
                    continue;
                if (excludeMouse ? device is Mouse : (inputDevices & category) == 0)
                    continue;

                bool paired = !sourceDevices.HasValue;
                if (sourceDevices.HasValue)
                {
                    foreach (InputDevice sourceDevice in sourceDevices.Value)
                    {
                        if (sourceDevice == device)
                        {
                            paired = true;
                            break;
                        }
                    }
                }

                if (!paired)
                    continue;
             
                allowedDevices.Add(device);
            }

            map.devices = new ReadOnlyArray<InputDevice>(allowedDevices.ToArray());
            return action;
        }

        bool MoveFromMouse()
        {
            Vector2 screen;
            if (mousePointAction != null && mousePointAction.WasPerformedThisFrame())
                screen = mousePointAction.ReadValue<Vector2>();
            else if (mouseClickAction != null && mouseClickAction.WasPressedThisFrame() && mouseClickAction.activeControl?.device is Mouse mouse)
                screen = mouse.position.ReadValue();
            else
                return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, CanvasCamera, out Vector2 local))
                return false;

            movementPosition = local;
            return true;
        }

        // Point, left click, right click, middle click, and scroll share mouse ownership
        static InputActionReference GetPointerAction(InputSystemUIInputModule module, int index) => index switch
        {
            0 => module.point,
            1 => module.leftClick,
            2 => module.rightClick,
            3 => module.middleClick,
            _ => module.scrollWheel
        };

        static void SetPointerAction(InputSystemUIInputModule module, int index, InputActionReference action)
        {
            switch (index)
            {
                case 0: module.point = action; break;
                case 1: module.leftClick = action; break;
                case 2: module.rightClick = action; break;
                case 3: module.middleClick = action; break;
                default: module.scrollWheel = action; break;
            }
        }

        bool PointerActionsChanged()
        {
            if (filteredPointerActions == null)
                return false;

            for (int i = 0; i < filteredPointerActions.Length; i++)
            {
                if (GetPointerAction(ownedModule, i) != filteredPointerActions[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Call after changing action bindings, control schemes, or paired devices at runtime.
        /// </summary>
        public void RefreshInput() => inputDirty = true;

        void ReleaseInput()
        {
            if (ownedModule != null)
            {
                if (ownedModule.move == installedMove)
                    ownedModule.move = originalMove;

                if (ownedModule.submit == installedSubmit)
                    ownedModule.submit = originalSubmit;

                if (filteredPointerActions != null)
                {
                    for (int i = 0; i < filteredPointerActions.Length; i++)
                    {
                        if (GetPointerAction(ownedModule, i) == filteredPointerActions[i])
                            SetPointerAction(ownedModule, i, originalPointerActions[i]);
                    }
                }
            }

            if (!ReferenceEquals(ownedModule, null))
                owners.Remove(ownedModule);

            if (inputAsset != null)
            {
                inputAsset.Disable();
                
                foreach (InputActionMap map in inputAsset.actionMaps)
                    map.Dispose();
              
                Destroy(inputAsset);
            }
           
            if (filteredMove != null)
                Destroy(filteredMove);

            if (filteredSubmit != null)
                Destroy(filteredSubmit);

            if (filteredPointerActions != null)
            {
                foreach (InputActionReference reference in filteredPointerActions)
                {
                    if (reference != null)
                        Destroy(reference);
                }
            }

            originalPointerActions = filteredPointerActions = null;
            ownedModule = null;
            inputAsset = null;
            moveAction = null;
            submitAction = null;
            mousePointAction = mouseClickAction = mouseScrollAction = null;
            eventSystem = null;
            pointerData = null;
        }

        void UpdateHover(GameObject hit)
        {
            // Retain common ancestors when moving between children of the same control
            for (int i = pointerData.hovered.Count - 1; i >= 0; i--)
            {
                GameObject previous = pointerData.hovered[i];
                if (previous != null && hit != null && hit.transform.IsChildOf(previous.transform))
                    continue;

                pointerData.hovered.RemoveAt(i);
                pointerData.fullyExited = true;
                ExecuteEvents.Execute(previous, pointerData, ExecuteEvents.pointerExitHandler);
               
                if (!isActiveAndEnabled)
                    return;
            }

            pointerData.pointerEnter = hit;
            for (Transform current = hit != null ? hit.transform : null; current != null; current = current.parent)
            {
                if (!pointerData.hovered.Contains(current.gameObject))
                {
                    pointerData.hovered.Add(current.gameObject);
                    pointerData.fullyExited = false;
                    ExecuteEvents.Execute(current.gameObject, pointerData, ExecuteEvents.pointerEnterHandler);

                    if (!isActiveAndEnabled)
                        return;
                }

                if (pointerData.IsPointerMoving())
                    ExecuteEvents.Execute(current.gameObject, pointerData, ExecuteEvents.pointerMoveHandler);
               
                if (!isActiveAndEnabled)
                    return;
            }
        }

        void PressPointer(GameObject hit)
        {
            pointerHeld = true;
            pointerData.eligibleForClick = true;
            pointerData.dragging = false;
            pointerData.useDragThreshold = true;
            pointerData.pressPosition = pointerData.position;
            pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
           
            GameObject selection = ExecuteEvents.GetEventHandler<ISelectHandler>(hit);
            if (eventSystem.currentSelectedGameObject != selection)
                eventSystem.SetSelectedGameObject(null, pointerData);
          
            if (!isActiveAndEnabled)
                return;

            // Set press ownership before callbacks, which may disable this component or close a menu
            GameObject pressed = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit);
            GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            if (pressed == null)
                pressed = clicked;

            pointerData.clickCount = pressed == pointerData.lastPress && Time.unscaledTime - pointerData.clickTime < 0.3f ? pointerData.clickCount + 1 : 1;
            pointerData.clickTime = Time.unscaledTime;
            pointerData.pointerPress = pressed;
            pointerData.rawPointerPress = hit;
            pointerData.pointerClick = clicked;
            ExecuteEvents.Execute(pressed, pointerData, ExecuteEvents.pointerDownHandler);
            
            if (!isActiveAndEnabled)
                return;

            pointerData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hit);
            ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.initializePotentialDrag);
        }

        void DragPointer()
        {
            if (!pointerHeld || pointerData.pointerDrag == null || !pointerData.IsPointerMoving())
                return;

            if (!pointerData.dragging && (!pointerData.useDragThreshold || (pointerData.position- pointerData.pressPosition)
                .sqrMagnitude >= eventSystem.pixelDragThreshold * eventSystem.pixelDragThreshold))
            {
                pointerData.dragging = true;
                ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.beginDragHandler);
                if (!isActiveAndEnabled)
                    return;
            }

            if (!pointerData.dragging)
                return;

            if (pointerData.pointerPress != pointerData.pointerDrag)
            {
                GameObject pressed = pointerData.pointerPress;
                pointerData.pointerPress = null;
                pointerData.rawPointerPress = null;
                pointerData.eligibleForClick = false;
                ExecuteEvents.Execute(pressed, pointerData, ExecuteEvents.pointerUpHandler);
              
                if (!isActiveAndEnabled)
                    return;
            }

            ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.dragHandler);
        }

        void ReleasePointer(GameObject hit, bool allowClick)
        {
            if (pointerData == null || releasing)
                return;

            releasing = true;
            pointerHeld = false;
            // Local data survives a callback disabling the component and releasing input ownership
            PointerEventData data = pointerData;
            GameObject pressed = data.pointerPress;
            GameObject dragged = data.pointerDrag;
            bool wasDragging = data.dragging;
            bool click = allowClick && data.eligibleForClick && data.pointerClick != null
                && data.pointerClick == ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);

            try
            {
                ExecuteEvents.Execute(pressed, data, ExecuteEvents.pointerUpHandler);

                if (isActiveAndEnabled && click)
                    ExecuteEvents.Execute(data.pointerClick, data, ExecuteEvents.pointerClickHandler);
                else if (isActiveAndEnabled && allowClick && wasDragging)
                    ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dropHandler);

                if (wasDragging)
                    ExecuteEvents.Execute(dragged, data, ExecuteEvents.endDragHandler);
            }
            finally
            {
                data.eligibleForClick = false;
                data.dragging = false;
                data.pointerPress = null;
                data.rawPointerPress = null;
                data.pointerClick = null;
                data.pointerDrag = null;
                releasing = false;
            }
        }

        void CancelPointer()
        {
            if (pointerData == null)
                return;

            PointerEventData data = pointerData;
            if (pointerHeld || data.dragging)
                ReleasePointer(null, false);

            // Remove before notifying so callbacks can safely disable/destroy the cursor
            while (data.hovered.Count > 0)
            {
                int index = data.hovered.Count - 1;
                GameObject hovered = data.hovered[index];
                data.hovered.RemoveAt(index);
                data.fullyExited = true;
                ExecuteEvents.Execute(hovered, data, ExecuteEvents.pointerExitHandler);
            }

            data.pointerEnter = null;
        }
        #endregion

        #region Visuals
        RectTransform visualRoot;
        Canvas visualCanvas;
        CanvasGroup visualGroup;
        RectTransform frameRect;
        ProceduralShape frameShape;
        ProceduralShape centerShape;
        ProceduralShape[] cornerShapes;
        CursorPreset builtPreset;
        RectTransform builtCustomVisual;
        RectTransform frameTarget;
        MovementMode visualMode;
        Vector2 frameStartPosition, frameStartSize, idleSize;
        Vector4 frameStartRadii;
        Color colorStart, colorTarget;
        float frameTime, colorTime, dashPhase;
        float visibilityStart, visibilityTime;
        bool visibilityTarget;
        bool visualInitialized;

        Vector2 CursorSize => Vector2.Max(Vector2.zero, rectTransform.rect.size);

        void BuildVisual()
        {
            float alpha = visualGroup != null ? visualGroup.alpha : hideWithoutSelection ? 0f : 1f;
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
                Destroy(visualRoot.gameObject);
            }

            frameShape = null;
            cornerShapes = null;
            visualRoot = CreateRect("Cursor Visual", rectTransform);
            visualRoot.gameObject.hideFlags = HideFlags.DontSave;
            visualCanvas = visualRoot.gameObject.AddComponent<Canvas>();
            UpdateCanvasSorting();
            visualGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();
            visualGroup.blocksRaycasts = false;
            visualGroup.interactable = false;
            visualGroup.alpha = alpha;
            visibilityStart = alpha;
            visibilityTarget = alpha > 0f;
            visibilityTime = 0;
            LayoutElement layout = visualRoot.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            frameRect = CreateRect("Target Frame", visualRoot);
            frameRect.sizeDelta = CursorSize;
           
            if (preset == CursorPreset.Custom && customVisual != null && customVisual.GetComponentInChildren<VirtualCursor>(true) == null)
            {
                RectTransform instance = Instantiate(customVisual, frameRect);
                instance.gameObject.SetActive(true);
                instance.anchorMin = Vector2.zero;
                instance.anchorMax = Vector2.one;
                instance.offsetMin = Vector2.zero;
                instance.offsetMax = Vector2.zero;

                foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                foreach (Canvas childCanvas in instance.GetComponentsInChildren<Canvas>(true))
                    childCanvas.overrideSorting = false;
            }
            else if (preset == CursorPreset.Corners)
            {
                cornerShapes = new ProceduralShape[8];
                for (int i = 0; i < cornerShapes.Length; i++)
                    cornerShapes[i] = CreateShape("Corner " + i, frameRect);
            }
            else
            {
                frameShape = frameRect.gameObject.AddComponent<ProceduralShape>();
                frameShape.raycastTarget = false;
                frameShape.FillCenter = false;
                frameShape.outlineColorMode = ProceduralShape.ColorMode.Base;
                frameShape.IndependentCorners = true;
                frameShape.UniformCornerRadius = cornerRadius;
                frameShape.UpdateRect();
            }

            centerShape = CreateShape("Center", visualRoot);
            centerShape.Shape = ProceduralShape.ShapeType.Ellipse;
            builtPreset = preset;
            builtCustomVisual = customVisual;
            visualInitialized = false;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        static ProceduralShape CreateShape(string name, Transform parent)
        {
            ProceduralShape shape = CreateRect(name, parent).gameObject.AddComponent<ProceduralShape>();
            shape.raycastTarget = false;
            return shape;
        }

        void UpdateVisual(bool visible)
        {
            if (visualRoot == null || builtPreset != preset || builtCustomVisual != customVisual)
                BuildVisual();

            if (visibilityTarget != visible)
            {
                visibilityStart = visualGroup.alpha;
                visibilityTarget = visible;
                visibilityTime = 0;
            }

            visualGroup.alpha = Mathf.Lerp(visibilityStart, visible ? 1f : 0f, Animate(ref visibilityTime, transitionDuration));
            bool active = visible || visibilityTime < transitionDuration;
            if (visualRoot.gameObject.activeSelf != active)
                visualRoot.gameObject.SetActive(active);
            if (!active)
                return;

            VirtualCursorOverride settings = targetOverride != null && targetOverride.isActiveAndEnabled ? targetOverride : null;
            Vector2 padding = settings != null && settings.overridePadding ? settings.targetPadding : targetPadding;
            float width = settings != null && settings.overrideOutlineWidth ? settings.outlineWidth : outlineWidth;
            float radius = settings != null && settings.overrideRadius ? settings.cornerRadius : cornerRadius;
            bool fit = fitToTarget && targetRect != null && targetRect.gameObject.activeInHierarchy;
            Vector2 position = Vector2.zero;
            Vector2 size = CursorSize;
            Rect bounds = default;
            if (fit && TryGetTargetBounds(out bounds))
            {
                position = bounds.center;
                size = Vector2.Max(Vector2.one, bounds.size + Vector2.Max(Vector2.zero, padding) * 2f);
                if (movementMode == MovementMode.FollowSelection && selectionTarget != null)
                {
                    // Keep the frame with the animated center, rather than jumping ahead to the destination
                    Vector3 travel = parentRect.TransformVector(selectionDestination - GetLocalCenter());
                    position -= (Vector2)visualRoot.InverseTransformVector(travel);
                }
            }
            else { fit = false; }

            RectTransform nextTarget = fit ? targetRect : null;
            if (!visualInitialized || frameTarget != nextTarget || visualMode != movementMode || (!fit && idleSize != CursorSize))
            {
                frameTarget = nextTarget;
                visualMode = movementMode;
                idleSize = CursorSize;
                frameStartPosition = frameRect.anchoredPosition;
                frameStartSize = frameRect.sizeDelta;
                frameStartRadii = frameShape != null ? frameShape.CornerRadius : Vector4.zero;
                frameTime = 0;
            }
           
            float blend = Animate(ref frameTime, transitionDuration);
            frameRect.anchoredPosition = Vector2.LerpUnclamped(frameStartPosition, position, blend);
            frameRect.sizeDelta = Vector2.Max(Vector2.zero, Vector2.LerpUnclamped(frameStartSize, size, blend));   
            Color color = ResolveColor();
         
            if (!visualInitialized || colorTarget != color)
            {
                colorStart = centerShape.color;
                colorTarget = color;
                colorTime = 0;
            }
          
            Color nextColor = Color.Lerp(colorStart, colorTarget, Animate(ref colorTime, transitionDuration));
            visualInitialized = true;
            centerShape.color = nextColor;
            float markerSize = settings != null && settings.overrideCenterSize ? settings.centerSize : centerSize;
            centerShape.rectTransform.sizeDelta = Vector2.one * Mathf.Max(0, markerSize);
            centerShape.Softness = settings != null && settings.overrideEdgeSoftness ? settings.edgeSoftness : edgeSoftness;
    
            if (frameShape != null)
            {
                frameShape.color = nextColor;
                frameShape.OutlineWidth = width;
                frameShape.Shape = fit && targetShape != null
                    ? targetShape.Shape
                    : (fit || preset == CursorPreset.Outline ? ProceduralShape.ShapeType.Rectangle : ProceduralShape.ShapeType.Ellipse);
                frameShape.PolygonSides = fit && targetShape != null ? targetShape.PolygonSides : 4;
                frameShape.ShapeAngle = fit && targetShape != null ? targetShape.ShapeAngle : 0;
                frameShape.ShapeSkew = fit && targetShape != null ? targetShape.ShapeSkew : Vector2.zero;

                Vector4 radii = Vector4.one * Mathf.Max(0, radius);
                bool sync = fit && targetShape != null && radiusSyncMode != RadiusSyncMode.None && (settings == null || !settings.overrideRadius);
                if (sync)
                {
                    Rect drawingRect = targetShape.GetDrawingRect();
                    Vector4 resolved = targetShape.GetRadiiPixels(drawingRect);
                    float scale = Mathf.Min(bounds.width / Mathf.Max(0.0001f, drawingRect.width), bounds.height / Mathf.Max(0.0001f, drawingRect.height));
                    float radiusPadding = Mathf.Max(0, Mathf.Min(padding.x, padding.y));
                   
                    // GetRadiiPixels uses shader order (TR, BR, TL, BL); CornerRadius uses TL, TR, BR, BL
                    radii = new Vector4(resolved.z, resolved.x, resolved.y, resolved.w) * scale;
                    for (int i = 0; i < 4; i++)
                    {
                        if (radii[i] > 0)
                            radii[i] += radiusPadding;
                    }
                }

                frameShape.CornerRadius = Vector4.LerpUnclamped(frameStartRadii, radii, blend);
                frameShape.SquircleCorners = sync && targetShape.UsesSquircleCorners;
                frameShape.OutlineDashed = IsDashActive(dashMode);
                frameShape.OutlineDashLength = dashLength;
                frameShape.OutlineDashGap = dashGap;
                frameShape.OutlineDashRounded = roundedDashes;
                
                if (frameShape.OutlineDashed && IsDashActive(dashAnimation))
                    dashPhase = Mathf.Repeat(dashPhase + dashSpeed * Time.unscaledDeltaTime, Mathf.Max(0.1f, dashLength) + Mathf.Max(0, dashGap));
               
                frameShape.OutlineDashOffset = dashOffset + dashPhase;
            }

            if (cornerShapes != null)
            {
                Vector2 halfSize = frameRect.sizeDelta * 0.5f;
                width = Mathf.Max(0, width);
                float length = Mathf.Min(Mathf.Max(width, cornerLength), Mathf.Min(halfSize.x, halfSize.y));  
                for (int i = 0; i < 4; i++)
                {
                    Vector2 sign = new(i < 2 ? -1 : 1, i % 2 == 0 ? -1 : 1);
                    ProceduralShape horizontal = cornerShapes[i * 2];
                    ProceduralShape vertical = cornerShapes[i * 2 + 1];
                    horizontal.rectTransform.sizeDelta = new Vector2(length, width);
                    vertical.rectTransform.sizeDelta = new Vector2(width, length);
                    horizontal.rectTransform.anchoredPosition = Vector2.Scale(sign, halfSize - new Vector2(length * 0.5f, width * 0.5f));
                    vertical.rectTransform.anchoredPosition = Vector2.Scale(sign, halfSize - new Vector2(width * 0.5f, length * 0.5f));
                    horizontal.color = vertical.color = nextColor;
                }
            }
        }

        bool TryGetTargetBounds(out Rect bounds)
        {
            bounds = default;

            if (targetShape == null) { targetRect.GetWorldCorners(worldCorners); }
            else
            {
                Rect drawingRect = targetShape.GetDrawingRect();
                worldCorners[0] = targetRect.TransformPoint(new Vector2(drawingRect.xMin, drawingRect.yMin));
                worldCorners[1] = targetRect.TransformPoint(new Vector2(drawingRect.xMin, drawingRect.yMax));
                worldCorners[2] = targetRect.TransformPoint(new Vector2(drawingRect.xMax, drawingRect.yMax));
                worldCorners[3] = targetRect.TransformPoint(new Vector2(drawingRect.xMax, drawingRect.yMin));
            }

            Canvas targetCanvas = targetRect.GetComponentInParent<Canvas>();
            Camera camera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            
            for (int i = 0; i < worldCorners.Length; i++)
            {
                if (camera != null && camera.WorldToScreenPoint(worldCorners[i]).z <= 0)
                    return false;

                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, screen, CanvasCamera, out Vector2 local))
                    return false;

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        bool IsDashActive(DashMode mode)
        {
            return mode == DashMode.Always || (mode == DashMode.OnTarget && targetObject != null) 
                || (mode == DashMode.WhilePressed && pointerHeld);
        }
        #endregion

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            if (centerShape == null)
                return;

            colorStart = centerShape.color;
            colorTarget = ResolveColor();
            colorTime = 0;
        }

        Color ResolveColor()
        {
            if (targetOverride != null && targetOverride.isActiveAndEnabled && targetOverride.overrideColor)
                return targetOverride.color;

            Color color = pointerHeld ? pressedColor : targetObject != null ? targetColor : normalColor;
            if (styleProvider != StyleProvider.Styler || stylerPreset == null)
                return color;

            ColorMapping mapping = pointerHeld ? pressedStyle : targetObject != null ? targetStyle : normalStyle;
            if (mapping != null)
                mapping.color = color;

            return Styler.GetColor(styleProvider, mapping, stylerPreset);
        }
        #endregion
    }
}