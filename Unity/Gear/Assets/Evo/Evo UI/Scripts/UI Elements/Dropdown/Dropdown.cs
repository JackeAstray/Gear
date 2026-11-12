using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Displays a single-selection dropdown whose items are generated from a reusable button prefab.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Dropdown"), HelpURL(Constants.HelpUrl + "ui-elements/dropdown")]
    public class Dropdown : MonoBehaviour
    {
        // Item List
        [SerializeField, Tooltip("Index of the selected item. Use -1 for no selection.")]
        private int selectedIndex = 0;
        [Tooltip("Items displayed in the dropdown.")]
        public List<Item> items = new();

        // Item Layout
        [Range(0, 100), Tooltip("Vertical spacing between generated items.")]
        public int itemSpacing = 2;
        [Range(20f, 200f), Tooltip("Height assigned to each generated item.")]
        public float itemHeight = 40f;
        [Tooltip("Padding applied around the generated items.")]
        public RectOffset padding = new();

        // Settings
        [UnityEngine.Serialization.FormerlySerializedAs("scrollbarPosition")]
        [Tooltip("Position to which the scrollbar moves when the dropdown opens.")]
        public ScrollPosition scrollPosition = ScrollPosition.Top;
        [Range(0, 2000), Tooltip("Maximum height of the open dropdown panel.")]
        public float maxHeight = 240f;
        [Tooltip("Blocks interaction with UI behind the dropdown while it is open.")]
        public bool blockUIWhileOpen = true;
        [Tooltip("Closes the dropdown after an item is selected.")]
        public bool closeOnItemSelect = true;
        [Tooltip("Closes the dropdown when the user clicks outside it.")]
        public bool closeOnClickOutside = true;

        // Animations
        public TweenSequence openAnimation = new();
        public TweenSequence closeAnimation = new();

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        [Tooltip("Button prefab instantiated for every dropdown item.")]
        public GameObject itemPrefab;
        [Tooltip("Parent transform that receives generated item instances.")]
        public Transform itemParent;
        [Tooltip("Button that displays the current selection and toggles the dropdown.")]
        public Button headerButton;
        [Tooltip("Scroll view that contains the generated dropdown items.")]
        public ScrollRect scrollRect;

        // Events
        [Tooltip("Invoked with the selected item index after a selection changes.")]
        public UnityEvent<int> onItemSelected = new();
        [Tooltip("Invoked when the dropdown begins opening.")]
        public UnityEvent onOpen = new();
        [Tooltip("Invoked when the dropdown begins closing.")]
        public UnityEvent onClose = new();

        // Constants
        const int SortingOrder = 30000;
        const float BoundsEpsilon = 0.01f;

        // Properties
        bool HasItemList => virtualizedItems != null && !virtualizedItems.IsDisposed;
        public bool IsVirtualized => HasItemList && scrollRect != null && scrollRect.EnableVirtualization;
        public int VisibleItemCount => filteredIndices.Count;
        public bool IsOpen => dropdownState == DropdownState.Open;
        public Item SelectedItem { get; private set; }
        protected virtual int ScrollTargetIndex => selectedIndex; // Helper for ScrollPosition.SelectedItem

        // Runtime State
        bool isDragging, hasCachedScrollPosition;
        Canvas rootCanvas, blocker, scrollRectCanvas;
        Vector2 originalScrollAnchorMin, originalScrollAnchorMax;
        Vector2 originalScrollPivot, originalScrollPosition;
        DropdownState dropdownState = DropdownState.Closed;
        Navigation.Mode previousHeaderNavigation;
        TweenHandle currentAnimation;
        UnityEngine.UI.Scrollbar suspendedScrollbar;
        readonly List<Button> itemButtons = new();
        readonly Dictionary<Button, Navigation> previousItemNavigations = new();

        // Cache
        RectTransform scrollRectRT, canvasRT;
        GameObject virtualItemPrefab;
        VirtualizedList<Button> virtualizedItems;
        string itemFilter = string.Empty;
        readonly List<int> filteredIndices = new();
        readonly Dictionary<Button, ItemBinding> virtualBindings = new();
        readonly VirtualizedListSettings virtualLayout = new() { loopNavigation = true };
        readonly Vector3[] dropdownCorners = new Vector3[4];

        protected virtual void Awake()
        {
            Initialize();
            GenerateItems();
        }

        protected virtual void Start() => InitializeSelection();

        protected virtual void OnEnable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif

            UpdateUI();
            SetState(DropdownState.Closed);
        }

        protected virtual void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif

            StopCurrentAnimation();
            ResumeScrollbar();

            if (dropdownState != DropdownState.Closed)
            {
                RestoreOriginalPosition();
                RestoreNavigation();
            }

            SetState(DropdownState.Closed);
            DestroyBlocker();
        }

        protected virtual void OnDestroy()
        {
            virtualizedItems?.Dispose();
            StopCurrentAnimation();
            DestroyBlocker();

            if (headerButton != null)
            {
                headerButton.onClick.RemoveListener(Toggle);
                headerButton.onSubmit.RemoveListener(OnHeaderSubmit);
            }

            if (scrollRect != null && scrollRect.gameObject.TryGetComponent<DropdownScrollDragHandler>(out var handler))
            {
                handler.onBeginDrag.RemoveListener(OnScrollBeginDrag);
                handler.onEndDrag.RemoveListener(OnScrollEndDrag);
            }
        }

        #region Initialization and Selection
        void Initialize()
        {
            items ??= new List<Item>();
            padding ??= new RectOffset();

            if (scrollRect != null)
            {
                scrollRectRT = scrollRect.GetComponent<RectTransform>();

                // Setup scroll rect drag detection
                if (!scrollRect.gameObject.TryGetComponent<DropdownScrollDragHandler>(out var scrollDragHandler))
                    scrollDragHandler = scrollRect.gameObject.AddComponent<DropdownScrollDragHandler>();

                scrollDragHandler.onBeginDrag.RemoveListener(OnScrollBeginDrag);
                scrollDragHandler.onEndDrag.RemoveListener(OnScrollEndDrag);
                scrollDragHandler.onBeginDrag.AddListener(OnScrollBeginDrag);
                scrollDragHandler.onEndDrag.AddListener(OnScrollEndDrag);
            }

            // Setup header button
            if (headerButton != null)
            {
#if EVO_LOCALIZATION
                headerButton.enableLocalization = false;
#endif
                headerButton.onClick.AddListener(Toggle);
                headerButton.onSubmit.AddListener(OnHeaderSubmit);
            }
        }

        protected virtual void InitializeSelection()
        {
            // Auto-select valid index or clear selection
            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                UpdateHeader(items[selectedIndex]);
                SelectedItem = items[selectedIndex];
                if (SelectedItem?.generatedButton != null)
                    SelectedItem.generatedButton.SetState(InteractionState.Selected);
            }
            else
            {
                selectedIndex = -1;
                SelectedItem = null;
                UpdateHeader();
            }
        }

        protected virtual void SetScrollPosition()
        {
            if (scrollRect == null)
                return;

            scrollRect.StopMovement();

            if (scrollPosition == ScrollPosition.Bottom) { scrollRect.verticalNormalizedPosition = 0; }
            else if (scrollPosition == ScrollPosition.Top) { scrollRect.verticalNormalizedPosition = 1; }
            else if (scrollPosition == ScrollPosition.SelectedItem && ScrollTargetIndex >= 0)
            {
                if (HasItemList)
                    virtualizedItems.ScrollToIndex(filteredIndices.IndexOf(ScrollTargetIndex), 0.5f);
                else
                    scrollRect.SnapToElementInstant(filteredIndices.IndexOf(ScrollTargetIndex));
            }

            virtualizedItems?.UpdateVisible();
        }

        protected virtual void UpdateHeader(Item item = null)
        {
            if (headerButton == null)
                return;

            if (item == null && selectedIndex >= 0 && selectedIndex < items.Count)
                item = items[selectedIndex];

            if (item == null)
                return;

            headerButton.Text = item.label;
            headerButton.Icon = item.icon;
        }

        protected virtual void SetupItemButton(Button button, Item item)
        {
            ConfigureItemButton(button, item);
            button.onClick.AddListener(() => SelectItem(item.index));
        }

        protected void ConfigureItemButton(Button button, Item item)
        {
#if EVO_LOCALIZATION
            button.enableLocalization = false;
#endif
            item.generatedButton = button;
            button.onSubmit.AddListener(OnItemSubmit);

            RectTransform rt = button.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, itemHeight);

            button.Text = item.label;
            button.Icon = item.icon;
        }

        protected int FindItemIndex(string label)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i]?.label == label)
                    return i;
            }
            return -1;
        }
        #endregion

        #region Types
        /// <summary>
        /// Runtime transition state of the dropdown panel.
        /// </summary>
        public enum DropdownState { Closed, Opening, Open, Closing }

        /// <summary>
        /// Scrollbar position applied after the dropdown opens.
        /// </summary>
        public enum ScrollPosition { LastPosition = 0, SelectedItem = 1, Top = 2, Bottom = 3 }

        /// <summary>
        /// Data used to generate one dropdown item.
        /// </summary>
        [Serializable]
        public class Item
        {
            [Tooltip("Text displayed for this item.")]
            public string label = "Item";

            [Tooltip("Optional icon displayed for this item.")]
            public Sprite icon;

            [HideInInspector, Tooltip("Whether a multi-select dropdown selects this item during initialization.")]
            public bool defaultSelected;

            // Runtime
            [HideInInspector] public int index;
            [HideInInspector] public Button generatedButton;
            [HideInInspector] public DropdownMultiSelectItem multiSelectItem;

#if EVO_LOCALIZATION
            [Tooltip("Localization table key used for this item's label.")]
            public string tableKey;
#endif

            /// <summary>
            /// Creates an item with the supplied label and optional icon.
            /// </summary>
            public Item(string label, Sprite icon = null)
            {
                this.label = label;
                this.icon = icon;
            }
        }

        class ItemBinding
        {
            public Dropdown owner;
            public Item item;
            public DropdownMultiSelectItem multiSelectItem;

            public void OnClick()
            {
                if (item != null)
                    owner.OnVirtualItemClick(item.index);
            }
        }
        #endregion

        #region Navigation
        void RestrictNavigation()
        {
            // Save and disable header button navigation
            if (headerButton != null)
            {
                previousHeaderNavigation = headerButton.navigation.mode;
                Navigation nav = headerButton.navigation;
                nav.mode = Navigation.Mode.None;
                headerButton.navigation = nav;
            }

            previousItemNavigations.Clear();

            if (HasItemList)
                return;

            // Save item navigations and set them up in a loop
            for (int i = 0; i < itemButtons.Count; i++)
            {
                Button btn = itemButtons[i];
                previousItemNavigations[btn] = btn.navigation;
                Navigation nav = new() { mode = Navigation.Mode.Explicit };

                // Set up navigation
                if (i > 0)
                    nav.selectOnUp = itemButtons[i - 1];
                if (i < itemButtons.Count - 1)
                    nav.selectOnDown = itemButtons[i + 1];

                // Loop around
                if (i == 0)
                    nav.selectOnUp = itemButtons[^1];
                if (i == itemButtons.Count - 1)
                    nav.selectOnDown = itemButtons[0];

                btn.navigation = nav;
            }
        }

        void RestoreNavigation()
        {
            // Restore header navigation
            if (headerButton != null)
            {
                Navigation nav = headerButton.navigation;
                nav.mode = previousHeaderNavigation;
                headerButton.navigation = nav;
            }

            // Restore item navigations
            foreach (var kvp in previousItemNavigations)
            {
                if (kvp.Key != null)
                    kvp.Key.navigation = kvp.Value;
            }
            previousItemNavigations.Clear();
        }
        #endregion

        #region Panel Layout and Transitions
        void UpdateLayout()
        {
            UpdateVirtualLayout();

            if (itemParent != null && itemParent.TryGetComponent<VerticalLayoutGroup>(out var vlg))
            {
                vlg.spacing = itemSpacing;
                vlg.padding = padding;
            }

            if (scrollRect != null)
            {
                float contentHeight = CalculateContentHeight();
                float clampedHeight = Mathf.Min(maxHeight, contentHeight);
                if (dropdownState != DropdownState.Closed)
                    scrollRectRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, clampedHeight);
            }
        }

        void SetState(DropdownState state)
        {
            dropdownState = state;

            if (scrollRect != null)
            {
                bool shouldBeActive = state != DropdownState.Closed;
                if (scrollRect.gameObject.activeSelf != shouldBeActive)
                    scrollRect.gameObject.SetActive(shouldBeActive);
            }
        }

        void CreateScrollRectCanvas()
        {
            if (scrollRect == null || scrollRectCanvas != null)
                return;

            if (!scrollRect.TryGetComponent(out scrollRectCanvas))
                scrollRectCanvas = scrollRect.gameObject.AddComponent<Canvas>();
            if (!scrollRect.TryGetComponent<GraphicRaycaster>(out _))
                scrollRect.gameObject.AddComponent<GraphicRaycaster>();

            scrollRectCanvas.vertexColorAlwaysGammaSpace = true;
            scrollRectCanvas.overrideSorting = true;
            scrollRectCanvas.sortingOrder = SortingOrder;
        }

        void CreateBlocker()
        {
            if (!blockUIWhileOpen || blocker != null || !TryCacheRootCanvas())
                return;

            GameObject blockerGO = new("DropdownBlocker");
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

            blockerGO.AddComponent<RaycastArea>();

            UnityEngine.UI.Button button = blockerGO.AddComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(OnBlockerClick);

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        void DestroyBlocker()
        {
            if (blocker == null)
                return;

            Destroy(blocker.gameObject);
            blocker = null;
        }

        void RestoreOriginalPosition()
        {
            if (scrollRect == null || !hasCachedScrollPosition)
                return;

            scrollRectRT.anchorMin = originalScrollAnchorMin;
            scrollRectRT.anchorMax = originalScrollAnchorMax;
            scrollRectRT.pivot = originalScrollPivot;
            scrollRectRT.anchoredPosition = originalScrollPosition;
            hasCachedScrollPosition = false;
        }

        void CacheCurrentPosition()
        {
            if (scrollRect == null || hasCachedScrollPosition)
                return;

            originalScrollAnchorMin = scrollRectRT.anchorMin;
            originalScrollAnchorMax = scrollRectRT.anchorMax;
            originalScrollPivot = scrollRectRT.pivot;
            originalScrollPosition = scrollRectRT.anchoredPosition;
            hasCachedScrollPosition = true;
        }

        void CheckAndAdjustPosition()
        {
            if (scrollRect == null || !TryCacheRootCanvas())
                return;

            float dropdownHeight = Mathf.Min(maxHeight, CalculateContentHeight());
            Vector2 originalSize = scrollRectRT.sizeDelta;

            ApplyDownwardPosition();
            scrollRectRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dropdownHeight);
            float downwardOverflow = CalculateVerticalOverflow();

            ApplyUpwardPosition();
            scrollRectRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dropdownHeight);
            float upwardOverflow = CalculateVerticalOverflow();

            scrollRectRT.sizeDelta = originalSize;

            bool shouldOpenUpward = downwardOverflow > BoundsEpsilon && upwardOverflow + BoundsEpsilon < downwardOverflow;
            if (shouldOpenUpward)
                ApplyUpwardPosition();
            else
                ApplyDownwardPosition();
        }

        bool TryCacheRootCanvas()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            Canvas candidate = parentCanvas != null ? parentCanvas.rootCanvas : null;
            if (candidate == null)
            {
                rootCanvas = null;
                canvasRT = null;
                return false;
            }

            if (rootCanvas != candidate || canvasRT == null)
            {
                rootCanvas = candidate;
                canvasRT = rootCanvas.GetComponent<RectTransform>();
            }

            return canvasRT != null;
        }

        void ApplyDownwardPosition()
        {
            scrollRectRT.anchorMin = originalScrollAnchorMin;
            scrollRectRT.anchorMax = originalScrollAnchorMax;
            scrollRectRT.pivot = originalScrollPivot;
            scrollRectRT.anchoredPosition = originalScrollPosition;
        }

        void ApplyUpwardPosition()
        {
            scrollRectRT.anchorMin = new Vector2(originalScrollAnchorMin.x, 1f - originalScrollAnchorMax.y);
            scrollRectRT.anchorMax = new Vector2(originalScrollAnchorMax.x, 1f - originalScrollAnchorMin.y);
            scrollRectRT.pivot = new Vector2(originalScrollPivot.x, 1f - originalScrollPivot.y);
            scrollRectRT.anchoredPosition = new Vector2(originalScrollPosition.x, -originalScrollPosition.y);
        }

        float CalculateVerticalOverflow()
        {
            scrollRectRT.GetWorldCorners(dropdownCorners);

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < dropdownCorners.Length; i++)
            {
                float canvasY = canvasRT.InverseTransformPoint(dropdownCorners[i]).y;
                minY = Mathf.Min(minY, canvasY);
                maxY = Mathf.Max(maxY, canvasY);
            }

            Rect canvasRect = canvasRT.rect;
            float below = Mathf.Max(0f, canvasRect.yMin - minY);
            float above = Mathf.Max(0f, maxY - canvasRect.yMax);
            return below + above;
        }

        float CalculateContentHeight()
        {
            int count = Application.isPlaying ? filteredIndices.Count : items.Count;
            float totalHeight = count * itemHeight + Mathf.Max(0, count - 1) * itemSpacing;
            totalHeight += padding.top + padding.bottom;
            return totalHeight;
        }

        void CompleteOpen()
        {
            currentAnimation = default;
            SetState(DropdownState.Open);
            UpdateLayout();
            ResumeScrollbar();
        }

        void CompleteClose()
        {
            currentAnimation = default;
            SetState(DropdownState.Closed);
            RestoreOriginalPosition();
            ResumeScrollbar();
        }

        void SuspendScrollbar()
        {
            // Resizing animations overflow the content until the panel is fully open, so detach
            // an auto-hiding scrollbar the open panel won't need instead of flashing it
            if (scrollRect == null || suspendedScrollbar != null || scrollRect.verticalScrollbar == null
                || scrollRect.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent
                || CalculateContentHeight() > maxHeight + BoundsEpsilon)
                return;

            suspendedScrollbar = scrollRect.verticalScrollbar;
            scrollRect.verticalScrollbar = null;
            suspendedScrollbar.gameObject.SetActive(false);
        }

        void ResumeScrollbar()
        {
            if (suspendedScrollbar == null)
                return;

            if (scrollRect != null && scrollRect.verticalScrollbar == null)
                scrollRect.verticalScrollbar = suspendedScrollbar;

            suspendedScrollbar = null;
        }

        void StopCurrentAnimation()
        {
            currentAnimation.Kill();
            currentAnimation = default;
        }
        #endregion

        #region Item Generation and Input
        void DestroyGeneratedItems()
        {
            virtualizedItems?.Dispose();
            virtualizedItems = null;
            virtualBindings.Clear();
            itemButtons.Clear();
            foreach (Item item in items)
            {
                if (item == null)
                    continue;
                item.generatedButton = null;
                item.multiSelectItem = null;
            }
            if (itemParent == null)
                return;

            for (int i = itemParent.childCount - 1; i >= 0; i--)
            {
                GameObject child = itemParent.GetChild(i).gameObject;
                child.SetActive(false);
                if (child == itemPrefab)
                    continue;
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }

        void OnScrollBeginDrag() => isDragging = true;

        void OnScrollEndDrag() => isDragging = false;

        void OnHeaderSubmit()
        {
            if (HasItemList)
            {
                int index = filteredIndices.IndexOf(ScrollTargetIndex);
                if (index < 0) { index = 0; }
                virtualizedItems.EnsureVisible(index);
                if (virtualizedItems.TryGetItem(index, out Button button))
                    Utilities.SetSelectedObject(button.gameObject);
                return;
            }
            if (itemParent == null)
                return;

            for (int i = 0; i < itemParent.childCount; i++)
            {
                GameObject itemObject = itemParent.GetChild(i).gameObject;
                if (!itemObject.activeInHierarchy)
                    continue;

                Utilities.SetSelectedObject(itemObject);
                return;
            }
        }

        void OnBlockerClick()
        {
            if (!isDragging && closeOnClickOutside)
                Close();
        }

        protected void GenerateItems()
        {
            if (itemPrefab == null || itemParent == null)
                return;

            BuildFilteredIndices();
            if (TryGenerateVirtualItems())
                return;

            DestroyGeneratedItems();

            // Generate new items
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i] ??= new Item("Item");
                item.index = i;
                GenerateItem(item);
            }

            ApplyItemFilter();

            // Do a scroll rect refresh with new items
            if (scrollRect != null)
                scrollRect.RefreshItems();
        }

        protected void OnItemSubmit()
        {
            if (headerButton != null)
                Utilities.SetSelectedObject(headerButton.gameObject);
        }

        protected virtual void OnItemRemoved(int index)
        {
            if (selectedIndex > index)
            {
                selectedIndex--;
                return;
            }

            if (selectedIndex == index)
            {
                selectedIndex = -1;
                SelectedItem = null;
            }
        }

        protected virtual void OnItemsCleared() { }

        Button GenerateItem(Item item)
        {
            item.generatedButton = null;
            item.multiSelectItem = null;

            GameObject itemObject = Instantiate(itemPrefab, itemParent);
            itemObject.name = item.label;
            if (!itemObject.TryGetComponent<Button>(out var button))
            {
                Debug.LogWarning($"Dropdown item prefab '{itemPrefab.name}' requires an {nameof(Button)} component.", this);
                Destroy(itemObject);
                return null;
            }

            SetupItemButton(button, item);
            itemButtons.Add(button);
            return button;
        }
        #endregion

        #region Virtualization and Filtering
        void BuildFilteredIndices()
        {
            filteredIndices.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i] ??= new Item("Item");
                item.index = i;

                if (MatchesFilter(item))
                    filteredIndices.Add(i);
            }
        }

        bool MatchesFilter(Item item) => itemFilter.Length == 0 || (item.label ?? string.Empty).IndexOf(itemFilter, StringComparison.OrdinalIgnoreCase) >= 0;

        bool TryGenerateVirtualItems()
        {
            if (scrollRect == null || scrollRect.content != itemParent || !scrollRect.vertical || scrollRect.horizontal ||
                !itemPrefab.TryGetComponent<Button>(out var prefabButton))
                return false;

            Transform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform;
            if (itemParent.parent != viewport || itemParent.localScale != Vector3.one || itemParent.localRotation != Quaternion.identity)
                return false;

            if (!HasItemList || virtualItemPrefab != itemPrefab)
            {
                DestroyGeneratedItems();
                CopyVirtualLayout();
                virtualizedItems = scrollRect.CreateVirtualizedList(prefabButton, BindVirtualItem, virtualLayout,
                    UnbindVirtualItem, CreateVirtualItem, DestroyVirtualItem);
                virtualItemPrefab = itemPrefab;
            }

            virtualizedItems.SetItemCount(filteredIndices.Count);
            UpdateLayout();
            return true;
        }

        void CopyVirtualLayout()
        {
            virtualLayout.itemSize = Mathf.Max(1f, itemHeight);
            virtualLayout.spacing = Mathf.Max(0, itemSpacing);
            virtualLayout.padding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        }

        void UpdateVirtualLayout()
        {
            if (!HasItemList)
                return;
            if (Mathf.Approximately(virtualLayout.itemSize, itemHeight) && Mathf.Approximately(virtualLayout.spacing, itemSpacing)
                && virtualLayout.padding.left == padding.left && virtualLayout.padding.right == padding.right
                && virtualLayout.padding.top == padding.top && virtualLayout.padding.bottom == padding.bottom)
                return;

            CopyVirtualLayout();
            virtualizedItems.SetLayout(virtualLayout);
        }

        void CreateVirtualItem(Button button)
        {
            PrepareVirtualItemButton(button);
            var binding = new ItemBinding
            {
                owner = this,
                multiSelectItem = button.GetComponent<DropdownMultiSelectItem>()
            };
            virtualBindings.Add(button, binding);
            button.onClick.AddListener(binding.OnClick);
            button.onSubmit.AddListener(OnItemSubmit);
        }

        void BindVirtualItem(Button button, int index)
        {
#if EVO_LOCALIZATION
            button.enableLocalization = false;
#endif
            Item item = items[filteredIndices[index]];
            ItemBinding binding = virtualBindings[button];
            binding.item = item;
            item.generatedButton = button;
            item.multiSelectItem = binding.multiSelectItem;
            button.Text = item.label;
            button.Icon = item.icon;
            ApplyVirtualItemState(button, item);
        }

        void DestroyVirtualItem(Button button)
        {
            if (!virtualBindings.TryGetValue(button, out ItemBinding binding))
                return;

            button.onClick.RemoveListener(binding.OnClick);
            button.onSubmit.RemoveListener(OnItemSubmit);
            virtualBindings.Remove(button);
        }

        void UnbindVirtualItem(Button button, int index)
        {
            // Use the bound object: the source list may already have been sorted, filtered, or edited
            ItemBinding binding = virtualBindings[button];
            if (binding.item != null && binding.item.generatedButton == button)
            {
                binding.item.generatedButton = null;
                binding.item.multiSelectItem = null;
            }
            binding.item = null;
        }

        /// <summary>
        /// Called once for each pooled button. Add permanent components and listeners here.
        /// </summary>
        protected virtual void PrepareVirtualItemButton(Button button) { }

        /// <summary>
        /// Called for every binding. Overwrite all item-dependent visuals here.
        /// </summary>
        protected virtual void ApplyVirtualItemState(Button button, Item item)
        {
            button.SetState(item == SelectedItem ? InteractionState.Selected : InteractionState.Normal, true);
        }

        protected virtual void OnVirtualItemClick(int index) => SelectItem(index);

        protected void RefreshVirtualItemStates()
        {
            foreach (var pair in virtualBindings)
            {
                if (pair.Value.item != null)
                    ApplyVirtualItemState(pair.Key, pair.Value.item);
            }
        }

        void ApplyItemFilter()
        {
            itemButtons.Clear();
            foreach (Item item in items)
            {
                if (item?.generatedButton == null)
                    continue;

                bool visible = MatchesFilter(item);
                item.generatedButton.gameObject.SetActive(visible);

                if (visible)
                    itemButtons.Add(item.generatedButton);
            }
        }

        void RefreshGeneratedLayout()
        {
            BuildFilteredIndices();
            ApplyItemFilter();

            if (scrollRect != null)
                scrollRect.RefreshItems();

            UpdateLayout();

            if (dropdownState == DropdownState.Open || dropdownState == DropdownState.Opening)
            {
                RestoreNavigation();
                RestrictNavigation();
            }
        }
        #endregion

        #region Sorting
        static readonly Comparison<Item> ascendingComparison =
            (a, b) => string.Compare(a?.label, b?.label, StringComparison.OrdinalIgnoreCase);

        static readonly Comparison<Item> descendingComparison =
            (a, b) => string.Compare(b?.label, a?.label, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Sorts items by label and reorders their generated buttons.
        /// </summary>
        public virtual void SortAlphabetically(bool ascending = true)
        {
            items.Sort(ascending ? ascendingComparison : descendingComparison);
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i] ??= new Item("Item");
                item.index = i;
                if (!HasItemList && item.generatedButton != null)
                    item.generatedButton.transform.SetSiblingIndex(i);
            }

            if (SelectedItem != null)
                selectedIndex = items.IndexOf(SelectedItem);

            if (HasItemList) { RefreshItems(); }
            else { RefreshGeneratedLayout(); }
        }
        #endregion

        #region Public API
        public int SelectedIndex
        {
            get => selectedIndex;
            set => SelectItem(value, true);
        }

        /// <summary>
        /// Refreshes the header and item layout from the current configuration.
        /// </summary>
        public virtual void UpdateUI()
        {
            UpdateHeader();
            UpdateLayout();
        }

        /// <summary>
        /// Opens a closed dropdown or closes an open dropdown.
        /// </summary>
        public void Toggle()
        {
            if (dropdownState == DropdownState.Closed || dropdownState == DropdownState.Closing)
                Open();
            else
                Close();
        }

        /// <summary>
        /// Opens the dropdown if it is closed or closing.
        /// </summary>
        public void Open()
        {
            if (dropdownState == DropdownState.Open || dropdownState == DropdownState.Opening)
                return;

            StopCurrentAnimation();

            CacheCurrentPosition(); // Cache current positioning values right before opening
            CheckAndAdjustPosition(); // Check if dropdown would be off-screen and adjust position

            SetState(DropdownState.Opening);
            UpdateLayout();
            virtualizedItems?.UpdateVisible();

            CreateScrollRectCanvas();
            CreateBlocker();
          
            if (scrollRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectRT);
                scrollRect.Rebuild(CanvasUpdate.PostLayout);
                if (!HasItemList)
                    scrollRect.RefreshItems();

                SetScrollPosition();
            }

            RestrictNavigation();
            SuspendScrollbar();
            onOpen?.Invoke();
            currentAnimation = openAnimation.Trigger(gameObject).OnComplete(CompleteOpen);

            if (!currentAnimation.IsActive)
                CompleteOpen();
        }

        /// <summary>
        /// Closes the dropdown if it is open or opening.
        /// </summary>
        public void Close()
        {
            if (dropdownState == DropdownState.Closed || dropdownState == DropdownState.Closing)
                return;

            StopCurrentAnimation();
            SetState(DropdownState.Closing);
            onClose?.Invoke();
            DestroyBlocker();
            RestoreNavigation();
            SuspendScrollbar();

            currentAnimation = closeAnimation.Trigger(gameObject).OnComplete(CompleteClose);
            if (!currentAnimation.IsActive)
                CompleteClose();
        }

        /// <summary>
        /// Selects an item by index.
        /// </summary>
        public virtual void SelectItem(int index, bool invokeEvents)
        {
            if (index < 0 || index >= items.Count)
                return;

            // Check for currently selected item
            if (SelectedItem != null && SelectedItem.generatedButton != null)
                SelectedItem.generatedButton.SetState(InteractionState.Normal);

            // Set indexes
            selectedIndex = index;
            SelectedItem = items[index];

            if (SelectedItem?.generatedButton != null)
                SelectedItem.generatedButton.SetState(InteractionState.Selected);

            UpdateHeader(SelectedItem);

            if (invokeEvents)
                onItemSelected?.Invoke(index);

            if (closeOnItemSelect)
                Close();
        }

        /// <summary>
        /// Selects an item by index and invokes selection events.
        /// </summary>
        public void SelectItem(int index) => SelectItem(index, true);

        /// <summary>
        /// Selects the first item with the supplied label and invokes selection events.
        /// </summary>
        public bool SelectItem(string label) => SelectItem(label, true);

        /// <summary>
        /// Selects the first item whose label exactly matches the supplied value.
        /// </summary>
        public virtual bool SelectItem(string label, bool invokeEvents)
        {
            int index = FindItemIndex(label);
            if (index < 0)
                return false;

            SelectItem(index, invokeEvents);
            return true;
        }

        /// <summary>
        /// Selects the supplied item if it belongs to this dropdown.
        /// </summary>
        public void SelectItem(Item item, bool invokeEvents = true)
        {
            int index = items.IndexOf(item);
            if (index >= 0)
                SelectItem(index, invokeEvents);
        }

        /// <summary>
        /// Clears the current single-item selection.
        /// </summary>
        public virtual void ClearSelection()
        {
            if (SelectedItem?.generatedButton != null)
                SelectedItem.generatedButton.SetState(InteractionState.Normal);

            selectedIndex = -1;
            SelectedItem = null;
        }

        /// <summary>
        /// Removes all items and clears the current selection.
        /// </summary>
        public void ClearAllItems()
        {
            if (HasItemList)
                virtualizedItems.SetItemCount(0, true);

            items.Clear();
            filteredIndices.Clear();

            selectedIndex = -1;
            SelectedItem = null;

            OnItemsCleared();

            if (!HasItemList)
                DestroyGeneratedItems();

            if (scrollRect != null)
                scrollRect.RefreshItems();

            UpdateHeader();
            UpdateLayout();
        }

        /// <summary>
        /// Adds an item to the dropdown.
        /// </summary>
        public void AddItem(Item item, bool generate = true)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            items.Add(item);
            item.index = items.Count - 1;

            if (generate && itemPrefab != null && itemParent != null)
            {
                if (HasItemList) { RefreshItems(); }
                else
                {
                    GenerateItem(item);
                    RefreshGeneratedLayout();
                }
            }
        }

        /// <summary>
        /// Adds an item from a label and optional icon.
        /// </summary>
        public void AddItem(string label, Sprite icon = null, bool generate = true) => AddItem(new Item(label, icon), generate);

        /// <summary>
        /// Adds multiple items and rebuilds the generated item list.
        /// </summary>
        public void AddItems(params Item[] newItems)
        {
            items.AddRange(newItems);
            GenerateItems();
        }

        /// <summary>
        /// Adds multiple label-only items and rebuilds the generated item list.
        /// </summary>
        public void AddItems(params string[] labels)
        {
            foreach (var label in labels)
                items.Add(new Item(label));

            GenerateItems();
        }

        /// <summary>
        /// Removes an item by index.
        /// </summary>
        public void RemoveItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return;

            Button generatedButton = items[index]?.generatedButton;
            if (!HasItemList && generatedButton != null)
            {
                itemButtons.Remove(generatedButton);
                generatedButton.gameObject.SetActive(false);
                Destroy(generatedButton.gameObject);
            }

            items.RemoveAt(index);

            for (int i = index; i < items.Count; i++)
                (items[i] ??= new Item("Item")).index = i;

            OnItemRemoved(index);

            if (HasItemList) { RefreshItems(); }
            else { RefreshGeneratedLayout(); }

            UpdateHeader();
        }

        /// <summary>
        /// Removes the first item whose label exactly matches the supplied value.
        /// </summary>
        public bool RemoveItem(string label)
        {
            int index = FindItemIndex(label);
            if (index < 0)
                return false;

            RemoveItem(index);
            return true;
        }
        /// <summary>
        /// Refreshes views after direct edits to items, or after AddItem with generate set to false.
        /// Virtualized dropdowns retain their existing pool.
        /// </summary>
        public void RefreshItems()
        {
            GenerateItems();
            UpdateLayout();

            if (!HasItemList && dropdownState != DropdownState.Closed)
            {
                RestoreNavigation();
                RestrictNavigation();
            }
        }

        /// <summary>
        /// Filters item labels without changing source indices or selection. Empty text shows every item.
        /// </summary>
        public void SetFilter(string value)
        {
            value ??= string.Empty;
            if (itemFilter == value)
                return;

            itemFilter = value;
            BuildFilteredIndices();

            if (HasItemList) { virtualizedItems.SetItemCount(filteredIndices.Count, true); }
            else
            {
                ApplyItemFilter();

                if (scrollRect != null)
                {
                    scrollRect.StopMovement();
                    scrollRect.verticalNormalizedPosition = 1f;
                    scrollRect.RefreshItems();
                }

                if (dropdownState != DropdownState.Closed)
                {
                    RestoreNavigation();
                    RestrictNavigation();
                }
            }

            UpdateLayout();
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || items == null)
                return;

            foreach (Item item in items)
            {
                if (item != null && !string.IsNullOrEmpty(item.tableKey))
                {
                    item.label = localizedObject.GetString(item.tableKey) ?? item.label;
                    if (item.generatedButton != null)
                        item.generatedButton.Text = item.label;
                }
            }

            UpdateHeader(SelectedItem);

            if (!HasItemList) { RefreshGeneratedLayout(); }
            else
            {
                BuildFilteredIndices();
                virtualizedItems.SetItemCount(filteredIndices.Count, true);
                UpdateLayout();
            }
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            items ??= new List<Item>();
            padding ??= new RectOffset();

            selectedIndex = items.Count > 0 ? Mathf.Clamp(selectedIndex, -1, items.Count - 1) : 0;

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                UpdateUI();
        }
#endif
    }

    /// <summary>
    /// Forwards scroll drag callbacks to events used by <see cref="Dropdown"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class DropdownScrollDragHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public UnityEvent onBeginDrag = new();
        public UnityEvent onEndDrag = new();

        public void OnBeginDrag(PointerEventData eventData) => onBeginDrag?.Invoke();

        public void OnEndDrag(PointerEventData eventData) => onEndDrag?.Invoke();
    }
}