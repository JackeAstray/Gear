using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Context Menu"), HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    public class ContextMenu : MonoBehaviour, IPointerClickHandler
    {
        // Content
        public List<Item> menuItems = new();

        // Settings
        public bool is3DObject = false;
        public bool usePointerPosition = true;
        public bool blockUIWhileOpen = false;
        public bool closeOnItemClick = true;
        public bool closeOnOutsideClick = true;
        public MouseButton triggerButton = MouseButton.Right;
        [Tooltip("Maximum distance in pixels the pointer can move between down and up to still register as a click.")]
        [Min(0f)] public float dragThreshold = 0f;

        // Position & Offset
        public OffsetPosition offsetPosition = OffsetPosition.BottomRight;
        public Vector2 customOffset = new(10f, 10f);
        public float offsetDistance = 10f;
        [Tooltip("Keeps the tooltip away from the canvas edges.")]
        public float screenEdgePadding = 10f;
        [Tooltip("Point on the target used when pointer positioning is disabled. (0, 0) is bottom-left; (0.5, 0.5) is the center.")]
        public Vector2 targetRectAnchor = new(0.5f, 0.5f);

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        public GameObject menuPreset;
        public Canvas targetCanvas;
        [Tooltip("Used for 3D object casting. If 'Is 3D Object' is enabled and camera is null, MainCamera will be used.")]
        public Camera targetCamera;

        // Events
        public UnityEvent onShow = new();
        public UnityEvent onHide = new();

        // Enums
        public enum MouseButton { Left = 0, Right = 1, Middle = 2, None = 9 }

        // Constants
        const int SortingOrder = 30000;

        // Instance variables
        ContextMenuPreset menuInstance;
        Coroutine outsideCoroutine;
        Vector3 lastClickPosition;
        Vector3 targetPosition;
        Canvas rootCanvas;
        Canvas blocker;
        Canvas menuCanvas;
        readonly static List<ContextMenu> activeMenus = new();

        // Cache
        PointerEventData pointerDataCache;
        readonly Vector3[] cornersCache = new Vector3[4];
        readonly List<RaycastResult> raycastResultsCache = new();
        readonly List<ContextMenuSubmenu> submenusCache = new();

        // Properties
        public bool IsVisible => menuInstance != null;
        public Canvas ActiveCanvas
        {
            get
            {
                if (targetCanvas != null) { return targetCanvas; }
                else
                {
                    var tempCanvas = GetComponentInParent<Canvas>();
                    if (tempCanvas != null)
                    {
                        targetCanvas = tempCanvas;
                        return targetCanvas;
                    }
                    return Utilities.ResolveCanvas();
                }
            }
        }

        [Serializable]
        public class Item
        {
            [UnityEngine.Serialization.FormerlySerializedAs("itemName")]
            public string name = "Menu Item";
            public Sprite icon;
#if EVO_LOCALIZATION
            public string tableKey;
#endif
            [UnityEngine.Serialization.FormerlySerializedAs("itemType")]
            public ItemType type = ItemType.Button;
            public UnityEvent onClick = new();

            [Tooltip("Replaces the preset's button prefab for this item. Leave empty to use the preset's.")]
            public GameObject buttonPrefab;

            [Tooltip("Attach an object (preferably a prefab) here.")]
            [UnityEngine.Serialization.FormerlySerializedAs("customPrefab")]
            public GameObject customObject;

            [Header("Submenu Settings")]
            public bool expandOnHover = true;

            [SerializeReference] 
            public List<Item> subItems = new();

            /// <summary>
            /// Specifies entry's item type.
            /// </summary>
            public enum ItemType { Button = 0, Separator = 1, Submenu = 2, CustomObject = 3 }

            /// <summary>
            /// Returns the generated object for this context menu item.
            /// </summary>
            public GameObject GeneratedObject { get; set; }

            /// <summary>
            /// Sets the active state of the item.
            /// </summary>
            public bool IsActive { get; set; } = true;

            /// <summary>
            /// Returns a direct child item by its entry name.
            /// </summary>
            public Item GetSubItem(string itemName)
            {
                for (int i = 0; i < subItems.Count; i++)
                {
                    if (subItems[i] != null && subItems[i].name == itemName)
                        return subItems[i];
                }
                return null;
            }

            /// <summary>
            /// Recursively finds a child item by its entry name.
            /// </summary>
            public Item FindSubItem(string itemName)
            {
                for (int i = 0; i < subItems.Count; i++)
                {
                    Item item = subItems[i];
                    if (item == null)
                        continue;

                    if (item.name == itemName)
                        return item;

                    Item nestedItem = item.FindSubItem(itemName);
                    if (nestedItem != null)
                        return nestedItem;
                }
                return null;
            }

            [Obsolete("Use name instead.")]
            public string itemName { get => name; set => name = value; }

            [Obsolete("Use type instead.")]
            public ItemType itemType { get => type; set => type = value; }

            [Obsolete("Use customObject instead.")]
            public GameObject customPrefab { get => customObject; set => customObject = value; }

            [Obsolete("Use GeneratedObject instead.")]
            public GameObject generatedObject { get => GeneratedObject; set => GeneratedObject = value; }

            [Obsolete("Use GeneratedObject.GetComponent<Button>() instead.")]
            public Button GetButton() => GeneratedObject != null ? GeneratedObject.GetComponent<Button>() : null;
        }

        void Start()
        {
            if (menuPreset == null)
            {
                Debug.LogError("[Context Menu] 'Menu Preset' is missing. " +
                    "Please select the object and assign a valid context menu preset via the References tab.", this);
                return;
            }

            if (is3DObject)
                EnsurePhysicsRaycaster();
        }

#if EVO_LOCALIZATION
        void OnEnable()
        {
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
        }
#endif

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
            Hide();
        }

        void OnDestroy()
        {
            activeMenus.Remove(this);
            DestroyBlocker();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsCorrectMouseButton(eventData))
            {
                // If threshold is 0 or less, disable the check and always treat it as valid.
                // Otherwise, check if the drag distance is within our defined threshold.
                bool isValidClick = dragThreshold <= 0f || Vector2.Distance(eventData.pressPosition, eventData.position) <= dragThreshold;
                if (isValidClick)
                {
                    lastClickPosition = eventData.position;
                    Show();
                }
            }
        }

        void EnsurePhysicsRaycaster()
        {
            Camera cam = targetCamera ? targetCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Context Menu] 'Is 3D Object' is enabled, but no camera found. " +
                    "Assign 'Target Camera' in the References tab.", this);
                return;
            }

            if (cam.GetComponent<PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<PhysicsRaycaster>();
        }

        void SetupMenu()
        {
            if (menuInstance == null)
                return;

            menuInstance.Setup(this, menuItems);
        }

        void PositionMenu()
        {
            if (menuInstance == null)
                return;

            if (!menuInstance.TryGetComponent<RectTransform>(out var menuRect))
                return;

            // Setup initial pivot and layout using the configured settings
            SetMenuPivot(menuRect, offsetPosition);

            // Force layout update to get accurate size for calculations
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);

            // Calculate base position (Screen Space) without offsets
            Vector3 basePosition = GetBasePosition();

            // Determine final offset and handle flipping
            // Check if the default configuration would push the menu off-screen
            Vector3 finalOffset = GetOffsetVector(offsetPosition);
            Vector3 projectedPos = basePosition + finalOffset;

            if (ShouldFlipVertical(projectedPos, menuRect))
            {
                if (offsetPosition == OffsetPosition.Custom)
                {
                    // Handle Custom Flip: Invert pivot Y and Y offset
                    menuRect.pivot = new Vector2(menuRect.pivot.x, 1f - menuRect.pivot.y);
                    finalOffset.y = -finalOffset.y;
                }
                else
                {
                    // Handle Standard Flip: Switch to opposite type (e.g., Bottom -> Top)
                    OffsetPosition flippedType = GetOppositeOffset(offsetPosition);
                    SetMenuPivot(menuRect, flippedType);
                    finalOffset = GetOffsetVector(flippedType);
                }
            }

            // Calculate final target position
            targetPosition = basePosition + finalOffset;

            // This ensures it never goes off-screen even after flipping
            targetPosition = ClampToScreen(targetPosition, menuRect);
            menuRect.position = GetWorldPositionFromScreen(menuRect, targetPosition);
        }

        Vector3 GetBasePosition()
        {
            if (menuInstance == null)
                return Vector3.zero;

            Vector3 basePosition;

            if (usePointerPosition) { basePosition = (Vector3)Utilities.GetPointerPosition(); }
            else
            {
                Canvas canvas = ActiveCanvas;

                if (TryGetComponent<RectTransform>(out var rectTransform))
                {
                    // Transform the actual anchor point before projecting it, so pivots,
                    // rotation and camera/world-space canvas scaling are handled correctly.
                    Rect rect = rectTransform.rect;
                    Vector3 anchor = rectTransform.TransformPoint(new Vector3(
                        Mathf.LerpUnclamped(rect.xMin, rect.xMax, targetRectAnchor.x),
                        Mathf.LerpUnclamped(rect.yMin, rect.yMax, targetRectAnchor.y), 0f));
                    Canvas root = canvas.rootCanvas;
                    Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null : root.worldCamera != null ? root.worldCamera : Camera.main;
                    basePosition = RectTransformUtility.WorldToScreenPoint(cam, anchor);
                }
                else if (is3DObject)
                {
                    Camera cam = targetCamera ? targetCamera : Camera.main;
                    basePosition = cam != null ? cam.WorldToScreenPoint(transform.position) : Vector3.zero;
                }
                else
                {
                    basePosition = lastClickPosition;
                }
            }

            return basePosition;
        }

        Vector3 GetWorldPositionFromScreen(RectTransform target, Vector3 screenPos)
        {
            Canvas canvas = ActiveCanvas;

            // If overlay, World Space == Screen Space
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return screenPos;

            // For Camera/World modes, we must convert
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam == null)
                return screenPos; // Fallback

            // Convert screen point to world point on the plane of the RectTransform
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                target.parent as RectTransform,
                screenPos,
                cam,
                out Vector3 worldPos
            );

            return worldPos;
        }

        void SetMenuPivot(RectTransform menuRect, OffsetPosition type)
        {
            Vector2 pivot = Vector2.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    pivot = new Vector2(1, 0); // Menu grows from bottom-right corner
                    break;
                case OffsetPosition.TopRight:
                    pivot = new Vector2(0, 0); // Menu grows from bottom-left corner
                    break;
                case OffsetPosition.BottomLeft:
                    pivot = new Vector2(1, 1); // Menu grows from top-right corner
                    break;
                case OffsetPosition.BottomRight:
                    pivot = new Vector2(0, 1); // Menu grows from top-left corner
                    break;
                case OffsetPosition.Top:
                    pivot = new Vector2(0.5f, 0); // Menu grows from bottom-center
                    break;
                case OffsetPosition.Bottom:
                    pivot = new Vector2(0.5f, 1); // Menu grows from top-center
                    break;
                case OffsetPosition.Left:
                    pivot = new Vector2(1, 0.5f); // Menu grows from right-center
                    break;
                case OffsetPosition.Right:
                    pivot = new Vector2(0, 0.5f); // Menu grows from left-center
                    break;
                case OffsetPosition.Custom:
                    pivot = new Vector2(0, 1); // Default to top-left for custom
                    break;
            }

            menuRect.pivot = pivot;
        }

        bool ShouldFlipVertical(Vector3 targetScreenPos, RectTransform menuRect)
        {
            Vector2 size = GetScreenSize(menuRect);

            // Pivot Y: 1 = Top (Grows Down), 0 = Bottom (Grows Up)
            if (menuRect.pivot.y > 0.5f) // Currently growing down (e.g. BottomRight)
            {
                // Check if the bottom edge is off-screen
                float bottomY = targetScreenPos.y - (size.y * menuRect.pivot.y);
                if (bottomY < screenEdgePadding)
                    return true;
            }
            else if (menuRect.pivot.y < 0.5f) // Currently Growing Up (e.g. TopRight)
            {
                // Check if the top edge is off-screen
                float topY = targetScreenPos.y + (size.y * (1f - menuRect.pivot.y));
                if (topY > Screen.height - screenEdgePadding)
                    return true;
            }

            return false;
        }

        OffsetPosition GetOppositeOffset(OffsetPosition current)
        {
            return current switch
            {
                OffsetPosition.TopLeft => OffsetPosition.BottomLeft,
                OffsetPosition.TopRight => OffsetPosition.BottomRight,
                OffsetPosition.BottomLeft => OffsetPosition.TopLeft,
                OffsetPosition.BottomRight => OffsetPosition.TopRight,
                OffsetPosition.Top => OffsetPosition.Bottom,
                OffsetPosition.Bottom => OffsetPosition.Top,
                _ => current,
            };
        }

        void CreateMenuCanvas()
        {
            if (menuInstance == null || menuCanvas != null)
                return;

            menuCanvas = menuInstance.gameObject.AddComponent<Canvas>();
            menuInstance.gameObject.AddComponent<GraphicRaycaster>();
            menuCanvas.vertexColorAlwaysGammaSpace = true;
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = SortingOrder;
        }

        void CreateBlocker()
        {
            if (blocker != null)
                return;

            if (rootCanvas == null)
            {
                var parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                    rootCanvas = parentCanvas.rootCanvas;
                else
                    rootCanvas = ActiveCanvas.rootCanvas; // fallback for 3D objects
            }

            if (rootCanvas == null)
                return;

            GameObject blockerGO = new("Context Menu Blocker");
            blocker = blockerGO.AddComponent<Canvas>();
            blocker.overrideSorting = true;
            blocker.sortingOrder = SortingOrder - 1;
            blockerGO.AddComponent<GraphicRaycaster>();

            RectTransform blockerRT = blockerGO.GetComponent<RectTransform>();
            blockerRT.SetParent(rootCanvas.transform, false);
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.sizeDelta = Vector2.zero;
            blockerRT.anchoredPosition = Vector2.zero;

            Image blockerImage = blockerGO.AddComponent<Image>();
            blockerImage.color = Color.clear;

            UnityEngine.UI.Button button = blockerGO.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(OnBlockerClicked);

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void OnBlockerClicked()
        {
            if (closeOnOutsideClick)
                Hide();
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
            blocker = null;
        }

        public void Show()
        {
            if (menuPreset == null || IsVisible)
                return;

            HideAllActiveMenus(); // Hide other active menus first
            HideImmediate(); // Hide any existing menu

            // Instantiate menu
            GameObject menuGO = Instantiate(menuPreset, ActiveCanvas.transform);
            menuInstance = menuGO.GetComponent<ContextMenuPreset>();
            if (menuInstance == null)
            {
                Destroy(menuGO);
                return;
            }

            onShow?.Invoke();
            SetupMenu();
            PositionMenu();

            if (blockUIWhileOpen)
            {
                CreateMenuCanvas();
                CreateBlocker();
            }

            activeMenus.Add(this);

            if (closeOnOutsideClick)
            {
                if (outsideCoroutine != null)
                {
                    StopCoroutine(outsideCoroutine);
                    outsideCoroutine = null;
                }
                outsideCoroutine = StartCoroutine(DetectOutsideClick());
            }

            menuInstance.Open();
        }

        public void Hide()
        {
            if (menuInstance == null)
                return;

            if (outsideCoroutine != null)
            {
                StopCoroutine(outsideCoroutine);
                outsideCoroutine = null;
            }

            DestroyBlocker();
            activeMenus.Remove(this);

            ContextMenuPreset closingInstance = menuInstance;
            menuInstance = null;
            menuCanvas = null;

            onHide?.Invoke();
            closingInstance.Close();
        }

        public void HideImmediate()
        {
            if (outsideCoroutine != null)
            {
                StopCoroutine(outsideCoroutine);
                outsideCoroutine = null;
            }

            DestroyBlocker();
            activeMenus.Remove(this);

            if (menuInstance != null)
            {
                menuInstance.CloseImmediate();
                menuInstance = null;
            }

            menuCanvas = null;
        }

        public void OnItemClicked(Item item)
        {
            if (closeOnItemClick && item.type != Item.ItemType.Submenu)
                Hide();
        }

        public void SetTriggerButton(int index) => triggerButton = (MouseButton)index;

        bool IsCorrectMouseButton(PointerEventData eventData)
        {
            return triggerButton switch
            {
                MouseButton.Left => eventData.button == PointerEventData.InputButton.Left,
                MouseButton.Right => eventData.button == PointerEventData.InputButton.Right,
                MouseButton.Middle => eventData.button == PointerEventData.InputButton.Middle,
                _ => false,
            };
        }

        bool IsPointerOverMenuOrSubmenus()
        {
            if (menuInstance == null)
                return false;

            // Get all UI elements under the mouse
            pointerDataCache ??= new PointerEventData(EventSystem.current);
            pointerDataCache.position = Utilities.GetPointerPosition();

            raycastResultsCache.Clear();
            EventSystem.current.RaycastAll(pointerDataCache, raycastResultsCache);

            // Check if any of the hit elements belong to our menu or any submenu
            for (int i = 0; i < raycastResultsCache.Count; i++)
            {
                Transform current = raycastResultsCache[i].gameObject.transform;

                // Check if it's part of the main menu
                if (current.IsChildOf(menuInstance.transform))
                    return true;

                // Check all nested submenus. Each panel is parented to the root canvas, so walk the whole tree.
                menuInstance.GetComponentsInChildren(submenusCache);
                for (int j = 0; j < submenusCache.Count; j++)
                {
                    if (submenusCache[j].IsMouseOverSubmenuHierarchy(current))
                        return true;
                }
            }
            return false;
        }

        Vector3 GetOffsetVector(OffsetPosition type)
        {
            if (type == OffsetPosition.Custom)
                return (Vector3)customOffset;

            Vector3 offset = Vector3.zero;

            switch (type)
            {
                case OffsetPosition.TopLeft:
                    offset = new Vector3(-offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.TopRight:
                    offset = new Vector3(offsetDistance, offsetDistance, 0);
                    break;
                case OffsetPosition.BottomLeft:
                    offset = new Vector3(-offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.BottomRight:
                    offset = new Vector3(offsetDistance, -offsetDistance, 0);
                    break;
                case OffsetPosition.Top:
                    offset = new Vector3(0, offsetDistance, 0);
                    break;
                case OffsetPosition.Bottom:
                    offset = new Vector3(0, -offsetDistance, 0);
                    break;
                case OffsetPosition.Left:
                    offset = new Vector3(-offsetDistance, 0, 0);
                    break;
                case OffsetPosition.Right:
                    offset = new Vector3(offsetDistance, 0, 0);
                    break;
            }

            return offset;
        }

        Vector3 ClampToScreen(Vector3 position, RectTransform menuRect)
        {
            // Get menu size in pixels
            Vector2 size = GetScreenSize(menuRect);
            float width = size.x;
            float height = size.y;

            // Get pivot offsets
            float pivotOffsetX = width * menuRect.pivot.x;
            float pivotOffsetY = height * menuRect.pivot.y;

            // Calculate actual bounds of the menu in Screen Space
            float minX = position.x - pivotOffsetX;
            float maxX = position.x + (width - pivotOffsetX);
            float minY = position.y - pivotOffsetY;
            float maxY = position.y + (height - pivotOffsetY);

            // Adjust position if menu goes out of screen bounds
            if (minX < screenEdgePadding)
                position.x += screenEdgePadding - minX;
            if (maxX > Screen.width - screenEdgePadding)
                position.x -= (maxX - (Screen.width - screenEdgePadding));
            if (minY < screenEdgePadding)
                position.y += (screenEdgePadding - minY);
            if (maxY > Screen.height - screenEdgePadding)
                position.y -= (maxY - (Screen.height - screenEdgePadding));

            return position;
        }

        Vector2 GetScreenSize(RectTransform rect)
        {
            Canvas canvas = ActiveCanvas;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return Vector2.Scale(rect.rect.size, rect.lossyScale);

            // For Camera/World modes, calculate screen projection
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            if (cam == null)
                return Vector2.Scale(rect.rect.size, rect.lossyScale); // Fallback

            rect.GetWorldCorners(cornersCache);

            // Calculate screen bounds from world corners
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, cornersCache[i]);
                if (screenPos.x < minX)
                    minX = screenPos.x;
                if (screenPos.x > maxX)
                    maxX = screenPos.x;
                if (screenPos.y < minY)
                    minY = screenPos.y;
                if (screenPos.y > maxY)
                    maxY = screenPos.y;
            }

            return new Vector2(maxX - minX, maxY - minY);
        }

        IEnumerator DetectOutsideClick()
        {
            // Skip the frame the menu was opened on to avoid the opening click being treated as an outside click
            yield return null;

            while (true)
            {
                // Check for ESC key to close menu
                if (IsVisible && Utilities.WasEscapeKeyPressed())
                {
                    Hide();
                    yield return null;
                    continue;
                }

                // Check for any mouse button clicks
                if (IsVisible && Utilities.WasPointerPressed())
                {
                    // Wait one frame to let UI events process
                    yield return null;

                    // Check if the click was outside the menu
                    if (!IsPointerOverMenuOrSubmenus())
                        Hide();
                }

                yield return null;
            }
        }

        static void HideAllActiveMenus()
        {
            for (int i = activeMenus.Count - 1; i >= 0; i--)
            {
                if (activeMenus[i] != null)
                    activeMenus[i].Hide();
                else
                    activeMenus.RemoveAt(i);
            }
        }

        /// <summary>
        /// Finds a top-level menu item by its entry name.
        /// </summary>
        public Item GetItem(string itemName)
        {
            for (int i = 0; i < menuItems.Count; i++)
            {
                if (menuItems[i].name == itemName)
                    return menuItems[i];
            }
            return null;
        }

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (enableLocalization && localizedObject != null)
                LocalizeItems(menuItems, localizedObject);
        }

        internal static void LocalizeItems(List<Item> items, Localization.LocalizedObject source)
        {
            if (items == null || source == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(item.tableKey))
                    item.name = source.GetString(item.tableKey) ?? item.name;

                if (item.GeneratedObject != null)
                {
                    if (item.type == Item.ItemType.Button && item.GeneratedObject.TryGetComponent<Button>(out var button))
                        button.Text = item.name;
                    else if (item.type == Item.ItemType.Submenu && item.GeneratedObject.TryGetComponent<ContextMenuSubmenu>(out var submenu))
                        submenu.RefreshLabel();
                }

                if (item.subItems != null && item.subItems.Count > 0)
                    LocalizeItems(item.subItems, source);
            }
        }
#endif
    }
}