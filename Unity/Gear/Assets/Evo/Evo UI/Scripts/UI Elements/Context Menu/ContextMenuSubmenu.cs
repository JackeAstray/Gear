using System.Collections;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Context Menu Submenu"), HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Evo.UI", sourceAssembly: "Evo.UI", sourceClassName: "ContextMenuSection")]
    public class ContextMenuSubmenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // References
        [FormerlySerializedAs("sectionButton")]
        [SerializeField] private Button submenuButton;
        [SerializeField] private GameObject submenuPrefab;

        // Settings
        [SerializeField, Range(0f, 2f)] private float expandDelay = 0.2f;
        [SerializeField] private Vector2 submenuOffset = new(5f, 0f);

        // Animations
        [SerializeField, Tooltip("Timeline played when the spawned submenu opens.")]
        private TweenSequence openAnimation = ContextMenuPreset.CreateAnimation(true, new Vector2(-20f, 0f));
        [SerializeField, Tooltip("Timeline played before the spawned submenu is destroyed.")]
        private TweenSequence closeAnimation = ContextMenuPreset.CreateAnimation(false, new Vector2(-20f, 0f));

        // Constants
        const int SortingOrder = 30001;

        // Instance variables
        bool submenuOnLeft = false;
        int parentMenuDepth;
        ContextMenu sourceMenu;
        ContextMenu.Item submenuItem;
        Coroutine expandCoroutine;
        Coroutine hoverCheckCoroutine;
        TweenHandle currentAnimation;
        [HideInInspector] public ContextMenuPreset submenuInstance;

        // Cache
        RectTransform rectTransform;
        RectTransform submenuRect;
        WaitForSecondsRealtime expandWait;

        // Per-instance, since a stopped wait keeps its deadline and would cut other submenus' grace periods short
        readonly WaitForSecondsRealtime shortCloseWait = new(0.15f);
        readonly WaitForSecondsRealtime longCloseWait = new(0.25f);

        // Shared raycast state
        static readonly List<RaycastResult> raycastResults = new();
        static PointerEventData sharedPointerData;

        // Shared corner buffer
        static readonly Vector3[] worldCorners = new Vector3[4];

        // Static tracking for hovered submenus
        static ContextMenuSubmenu currentHoveredSubmenu;

        // Properties
        public bool IsExpanded { get; private set; }
        public TweenSequence OpenAnimation => openAnimation ??= new();
        public TweenSequence CloseAnimation => closeAnimation ??= new();

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            // Unscaled like the close checks and animations, so hover expand also works while paused
            expandWait = new WaitForSecondsRealtime(expandDelay);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            currentAnimation.Kill();
            DestroySubmenuImmediate();

            if (currentHoveredSubmenu == this)
                currentHoveredSubmenu = null;
        }

        public void Setup(ContextMenu source, ContextMenu.Item item) => Setup(source, item, 0);

        internal void Setup(ContextMenu source, ContextMenu.Item item, int menuDepth)
        {
            sourceMenu = source;
            submenuItem = item;
            parentMenuDepth = Mathf.Max(0, menuDepth);

            if (submenuButton != null)
            {
                submenuButton.onClick.AddListener(OnSubmenuClicked);
#if EVO_LOCALIZATION
                submenuButton.enableLocalization = false;
#endif
                submenuButton.Text = item.name;
                submenuButton.Icon = item.icon;
            }
        }

#if EVO_LOCALIZATION
        internal void RefreshLabel()
        {
            if (submenuButton != null && submenuItem != null)
                submenuButton.Text = submenuItem.name;
        }
#endif

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Close other submenus (but not child submenus)
            if (currentHoveredSubmenu != null && currentHoveredSubmenu != this && !IsChildOfSubmenu(currentHoveredSubmenu))
                currentHoveredSubmenu.CollapseSubmenu();

            currentHoveredSubmenu = this;

            // Stop any pending close check
            if (hoverCheckCoroutine != null)
            {
                StopCoroutine(hoverCheckCoroutine);
                hoverCheckCoroutine = null;
            }

            // Start expand timer if hover expand is enabled
            if (submenuItem != null && submenuItem.expandOnHover && !IsExpanded)
            {
                if (expandCoroutine != null)
                    StopCoroutine(expandCoroutine);

                expandCoroutine = StartCoroutine(ExpandAfterDelay());
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Cancel any pending expand operation
            if (expandCoroutine != null)
            {
                StopCoroutine(expandCoroutine);
                expandCoroutine = null;
            }

            // Start checking if we should close the submenu
            if (IsExpanded && submenuItem != null && submenuItem.expandOnHover)
            {
                if (hoverCheckCoroutine != null)
                    StopCoroutine(hoverCheckCoroutine);

                hoverCheckCoroutine = StartCoroutine(CheckAndCloseSubmenu());
            }
        }

        public void OnPointerClick(PointerEventData eventData) => OnSubmenuClicked();

        void OnSubmenuClicked()
        {
            if (currentAnimation.IsActive)
                return;

            // Close other submenus (but not child submenus)
            if (currentHoveredSubmenu != null && currentHoveredSubmenu != this && !IsChildOfSubmenu(currentHoveredSubmenu))
                currentHoveredSubmenu.CollapseSubmenu();

            currentHoveredSubmenu = this;

            if (IsExpanded)
                CollapseSubmenu();
            else
                ExpandSubmenu();
        }

        void ExpandSubmenu()
        {
            if (submenuItem == null || submenuItem.subItems == null || submenuItem.subItems.Count == 0 || IsExpanded || !CreateSubmenu())
                return;

            IsExpanded = true;
            submenuButton.SetState(InteractionState.Selected);
        }

        public void CollapseSubmenu(bool immediate = false)
        {
            if (!IsExpanded)
                return;

            StopAllCoroutines();
            CollapseAllChildSubmenus(); // Also collapse any child submenus

            if (immediate || submenuInstance == null)
            {
                currentAnimation.Kill();
                DestroySubmenu();
            }
            else
            {
                // Finish the entry slide so the exit slide captures the positioned rest state.
                currentAnimation.Complete();
                submenuInstance.SetInteraction(false);
                currentAnimation = submenuInstance.PlaySubmenuAnimation(CloseAnimation, submenuOnLeft).OnComplete(DestroySubmenu);

                if (!currentAnimation.IsActive)
                    DestroySubmenu();
            }

            IsExpanded = false;

            if (currentHoveredSubmenu == this)
                currentHoveredSubmenu = null;

            submenuButton.SetState(InteractionState.Normal);
        }

        bool CreateSubmenu()
        {
            if (submenuPrefab == null)
                return false;

            // Hover can expand again before the close animation ends. Replacing the reference
            // and killing that animation would otherwise orphan the closing instance.
            if (submenuInstance != null)
                DestroySubmenuImmediate();

            // Parent to the root canvas, not the main menu.
            // This ensures position calculations work in absolute screen space.
            Canvas activeCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas : GetComponentInParent<Canvas>();
            Canvas rootCanvas = activeCanvas != null ? activeCanvas.rootCanvas : null;
            Transform parentTransform = rootCanvas != null ? rootCanvas.transform : transform.root;
            GameObject submenuGO = Instantiate(submenuPrefab, parentTransform);

            submenuInstance = submenuGO.GetComponent<ContextMenuPreset>();
            if (submenuInstance == null)
            {
                Destroy(submenuGO);
                return false;
            }

            // Cache the submenu RectTransform used by positioning and screen clamping
            submenuRect = submenuGO.GetComponent<RectTransform>();
            submenuInstance.Setup(sourceMenu, submenuItem.subItems, GetSubmenuDepth());

            if (submenuItem.expandOnHover)
                SetupSubmenuHoverHandlers();

            // If block UI is enabled, create a canvas for proper layering
            if (sourceMenu != null && sourceMenu.blockUIWhileOpen)
                CreateSubmenuCanvas();

            PositionSubmenu();

            currentAnimation.Kill();
            submenuInstance.SetInteraction(true);
            currentAnimation = submenuInstance.PlaySubmenuAnimation(OpenAnimation, submenuOnLeft);
            
            return true;
        }

        void SetupSubmenuHoverHandlers()
        {
            if (submenuInstance == null)
                return;

            if (!submenuInstance.gameObject.TryGetComponent<EventTrigger>(out var trigger))
                trigger = submenuInstance.gameObject.AddComponent<EventTrigger>();

            // Cancel close when entering submenu
            EventTrigger.Entry enterEntry = new() { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => 
            {
                if (hoverCheckCoroutine != null)
                {
                    StopCoroutine(hoverCheckCoroutine);
                    hoverCheckCoroutine = null;
                }
            });
            trigger.triggers.Add(enterEntry);

            // Check for close when leaving submenu
            EventTrigger.Entry exitEntry = new() { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((data) => 
            {
                if (hoverCheckCoroutine != null)
                    StopCoroutine(hoverCheckCoroutine);

                hoverCheckCoroutine = StartCoroutine(CheckAndCloseSubmenu());
            });
            trigger.triggers.Add(exitEntry);
        }

        void CreateSubmenuCanvas()
        {
            if (submenuInstance == null)
                return;

            // Add canvas to submenu for proper sorting
            Canvas submenuCanvas = submenuInstance.gameObject.AddComponent<Canvas>();
            submenuInstance.gameObject.AddComponent<GraphicRaycaster>();
            submenuCanvas.overrideSorting = true;

            // Get the depth level to calculate proper sorting order
            int depth = GetSubmenuDepth();
            submenuCanvas.sortingOrder = SortingOrder + Mathf.Max(0, depth - 1);
        }

        bool IsChildOfSubmenu(ContextMenuSubmenu otherSubmenu)
        {
            if (otherSubmenu == null || otherSubmenu.submenuInstance == null)
                return false;

            return transform.IsChildOf(otherSubmenu.submenuInstance.transform);
        }

        static void PrepareSharedPointerData()
        {
            sharedPointerData ??= new PointerEventData(EventSystem.current);
            sharedPointerData.position = Utilities.GetPointerPosition();
        }

        bool IsPointerOverSubmenuButton()
        {
            PrepareSharedPointerData();
            raycastResults.Clear();
            EventSystem.current.RaycastAll(sharedPointerData, raycastResults);

            bool found = false;
            foreach (var result in raycastResults)
            {
                // Check if the pointer is over this submenu button specifically
                if (result.gameObject.transform.IsChildOf(transform))
                { 
                    found = true;
                    break; 
                }
            }
            raycastResults.Clear();
            return found;
        }

        bool IsPointerOverSubmenuTree()
        {
            PrepareSharedPointerData();
            raycastResults.Clear();
            EventSystem.current.RaycastAll(sharedPointerData, raycastResults);

            bool found = false;
            foreach (var result in raycastResults)
            {
                // Check if the pointer is over this submenu button
                if (result.gameObject.transform.IsChildOf(transform))
                {
                    found = true;
                    break;
                }

                // Check if mouse is over this submenu or any nested submenus
                if (IsMouseOverSubmenuHierarchy(result.gameObject.transform))
                {
                    found = true;
                    break;
                }
            }
            raycastResults.Clear();
            return found;
        }

        internal bool IsMouseOverSubmenuHierarchy(Transform target)
        {
            if (submenuInstance == null)
                return false;

            if (target.IsChildOf(submenuInstance.transform))
                return true;

            ContextMenuSubmenu[] childSubmenus = submenuInstance.GetComponentsInChildren<ContextMenuSubmenu>();
            foreach (var childSubmenu in childSubmenus)
            {
                if (childSubmenu != this && childSubmenu.IsMouseOverSubmenuHierarchy(target))
                    return true;
            }
            return false;
        }

        void CollapseAllChildSubmenus()
        {
            if (submenuInstance == null)
                return;

            ContextMenuSubmenu[] childSubmenus = submenuInstance.GetComponentsInChildren<ContextMenuSubmenu>();
            foreach (var childSubmenu in childSubmenus)
            {
                if (childSubmenu != this)
                    childSubmenu.CollapseSubmenu(true);
            }
        }

        void PositionSubmenu()
        {
            if (submenuInstance == null)
                return;

            Canvas rootCanvas = sourceMenu != null ? sourceMenu.ActiveCanvas : GetComponentInParent<Canvas>().rootCanvas;

            // Determine render mode and camera once — passed down to GetScreenSize and ClampSubmenuToScreen
            bool isOverlay = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            Camera cam = (!isOverlay && rootCanvas.worldCamera != null) ? rootCanvas.worldCamera : Camera.main;
            if (isOverlay)
                cam = null;

            // Set anchors to bottom-left for absolute positioning
            submenuRect.anchorMin = Vector2.zero;
            submenuRect.anchorMax = Vector2.zero;
            submenuRect.pivot = new Vector2(0, 1); // Top-left pivot for easier positioning

            // Force rebuild to get accurate dimensions
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(submenuRect);
            float screenPadding = 10f;

            // Get the submenu item's world corners and convert them to Screen Space
            rectTransform.GetWorldCorners(worldCorners);

            // 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right
            // Need Screen Space points for clamping logic
            Vector3 topRightScreen, topLeftScreen;

            if (isOverlay)
            {
                topRightScreen = worldCorners[2];
                topLeftScreen = worldCorners[1];
            }
            else
            {
                topRightScreen = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[2]);
                topLeftScreen = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[1]);
            }

            // Get actual screen pixel size of the submenu
            // cam and isOverlay passed in to avoid re-resolving inside the helper
            Vector2 submenuSize = GetScreenSize(submenuRect, cam, isOverlay);
            float submenuWidth = submenuSize.x;

            // Calculate the position from the submenu item's top-right corner
            Vector3 targetPos = topRightScreen;
            targetPos.x += submenuOffset.x;
            targetPos.y += submenuOffset.y;

            // Check if it fits on the right
            if (targetPos.x + submenuWidth > Screen.width - screenPadding)
            {
                // Position on left side instead
                submenuRect.pivot = new Vector2(1, 1); // Top-right pivot
                targetPos = topLeftScreen;
                targetPos.x -= submenuOffset.x;
                targetPos.y += submenuOffset.y;
                submenuOnLeft = true;
            }
            else
            {
                submenuOnLeft = false;
            }

            // Clamp to screen (calculations in Screen Space)
            targetPos = ClampSubmenuToScreen(targetPos, submenuRect, cam, isOverlay);
            targetPos.z = 0;

            // Convert Screen Space targetPos back to World Space if needed
            if (isOverlay) { submenuRect.position = targetPos; }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    submenuRect.parent as RectTransform,
                    targetPos,
                    cam,
                    out Vector3 worldPos
                );
                submenuRect.position = worldPos;
            }
        }

        void DestroySubmenu()
        {
            currentAnimation.Kill();
            currentAnimation = default;

            if (submenuInstance != null)
            {
                Destroy(submenuInstance.gameObject);
                submenuInstance = null;
                submenuRect = null;
            }

            IsExpanded = false;
        }

        public void DestroySubmenuImmediate()
        {
            currentAnimation.Kill();
            currentAnimation = default;

            if (submenuInstance != null)
            {
                Destroy(submenuInstance.gameObject);
                submenuInstance = null;
                submenuRect = null;
                IsExpanded = false;
            }
        }

        Vector3 ClampSubmenuToScreen(Vector3 position, RectTransform rect, Camera cam, bool isOverlay)
        {
            Vector2 size = GetScreenSize(rect, cam, isOverlay);
            float width = size.x;
            float height = size.y;
            float padding = 10f;

            // Get pivot offsets
            float pivotOffsetX = width * rect.pivot.x;
            float pivotOffsetY = height * rect.pivot.y;

            // Calculate bounds
            float minX = position.x - pivotOffsetX;
            float maxX = position.x + (width - pivotOffsetX);
            float minY = position.y - pivotOffsetY;
            float maxY = position.y + (height - pivotOffsetY);

            // Adjust if out of bounds
            if (minX < padding)
                position.x += padding - minX;
            if (maxX > Screen.width - padding)
                position.x -= maxX - (Screen.width - padding);
            if (minY < padding)
                position.y += padding - minY;
            if (maxY > Screen.height - padding)
                position.y -= maxY - (Screen.height - padding);

            return position;
        }

        Vector2 GetScreenSize(RectTransform rect, Camera cam, bool isOverlay)
        {
            if (isOverlay)
                return Vector2.Scale(rect.rect.size, rect.lossyScale);

            // For Camera/World modes, calculate screen projection
            if (cam == null)
                return Vector2.Scale(rect.rect.size, rect.lossyScale); // Fallback

            rect.GetWorldCorners(worldCorners);

            // Calculate screen bounds from world corners
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
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

        // Root menu submenus create depth 1, each nested preset increments the parent depth
        int GetSubmenuDepth() => parentMenuDepth + 1;

        IEnumerator ExpandAfterDelay()
        {
            // A stopped wait keeps its old deadline. Restart it for every hover.
            expandWait.Reset();
            yield return expandWait;

            // Only expand if we're still being hovered and not already expanded
            if (!IsExpanded && IsPointerOverSubmenuButton())
                ExpandSubmenu();

            expandCoroutine = null;
        }

        // Close checking with longer delay for nested menus
        IEnumerator CheckAndCloseSubmenu()
        {
            // Use a longer delay for nested menus to give user time to navigate
            WaitForSecondsRealtime closeWait = GetSubmenuDepth() > 1 ? longCloseWait : shortCloseWait;
            closeWait.Reset();
            yield return closeWait;

            if (!IsPointerOverSubmenuTree())
                CollapseSubmenu();

            hoverCheckCoroutine = null;
        }

        #region Obsolete API
        [System.Obsolete("Configure the submenu button reference in the Inspector instead.")]
        public Button sectionButton { get => submenuButton; set => submenuButton = value; }
        #endregion
    }
}