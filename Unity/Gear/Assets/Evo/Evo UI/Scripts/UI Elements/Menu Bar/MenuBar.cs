using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace Evo.UI
{
    [DisallowMultipleComponent, ExecuteAlways]
    [AddComponentMenu("Evo/UI/UI Elements/Menu Bar"), HelpURL(Constants.HelpUrl + "ui-elements/menu-bar")]
    public class MenuBar : MonoBehaviour
    {
        // Content
        public List<Item> items = new();

        // Settings
        [Tooltip("Opens dropdowns on hover without requiring an initial click.")]
        public bool openMenuOnHover;
        [Tooltip("Blocks interaction with other UI while a dropdown is open.")]
        public bool blockUIWhileOpen;
        [Tooltip("Screen-pixel offset from the bottom-left corner of each bar item.")]
        public Vector2 itemMenuOffset = new(0f, -5f);

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        public Transform itemParent;
        public GameObject itemPreset;
        public GameObject contextMenuPreset;

        // Serialized Ownership
        [SerializeField, HideInInspector] private List<GameObject> generatedObjects = new();
        [SerializeField, HideInInspector] private GameObject generatedItemPreset;

        // Constants
        const int SortOrder = 30002;

        // Cache
        Canvas menuCanvas;
        Item currentBarItem;

        // State
        readonly List<Item> initializedItems = new();
        readonly GeneratedNames generatedNames = new();
        bool previousOverrideSorting;
        int previousSortingOrder;

        // Properties
        public bool IsOpen => currentBarItem != null;

        [Serializable]
        [MovedFrom(true, sourceNamespace: "Evo.UI", sourceAssembly: "Evo.UI", sourceClassName: "MenuBar/BarItem")]
        public class Item
        {
            public string label = "Menu Item";
            public Sprite icon;
#if EVO_LOCALIZATION
            public string tableKey;
#endif

            public List<ContextMenu.Item> menuBarItems = new();
            public UnityEvent onClick = new();

            // Properties
            public GameObject GeneratedObject { get; set; }
            public Button GeneratedButton { get; set; }
            public ContextMenu GeneratedMenu { get; set; }

            [Obsolete("Use GeneratedButton instead.")]
            public Button generatedButton { get => GeneratedButton; set => GeneratedButton = value; }

            [Obsolete("Use GeneratedMenu instead.")]
            public ContextMenu generatedMenu { get => GeneratedMenu; set => GeneratedMenu = value; }

            /// <summary>
            /// Whether the item is shown. Call MenuBar.Refresh after changing this value.
            /// </summary>
            public bool IsActive { get; set; } = true;

            MenuBar owner;
            Button boundButton;
            ContextMenu boundMenu;

            internal void Bind(MenuBar menuBar)
            {
                // Items that keep their objects stay bound, so a refresh doesn't register listeners again
                if (owner == menuBar && boundButton == GeneratedButton && boundMenu == GeneratedMenu)
                    return;

                RemoveListeners();
                owner = menuBar;
                boundButton = GeneratedButton;
                boundMenu = GeneratedMenu;
                boundButton.onClick.AddListener(OnClick);
                boundButton.onPointerEnter.AddListener(OnPointerEnter);

                if (boundMenu != null)
                    boundMenu.onHide.AddListener(owner.CloseMenu);
            }

            internal void Unbind()
            {
                RemoveListeners();
                owner = null;
                boundButton = null;
                boundMenu = null;
                GeneratedObject = null;
                GeneratedButton = null;
                GeneratedMenu = null;
            }

            void RemoveListeners()
            {
                if (boundButton != null)
                {
                    boundButton.onClick.RemoveListener(OnClick);
                    boundButton.onPointerEnter.RemoveListener(OnPointerEnter);
                }

                if (boundMenu != null && owner != null)
                    boundMenu.onHide.RemoveListener(owner.CloseMenu);
            }

            void OnClick()
            {
                MenuBar menuBar = owner;
                onClick?.Invoke();
                if (menuBar != null)
                    menuBar.OpenMenu(this);
            }

            void OnPointerEnter()
            {
                if (owner == null)
                    return;

                if (menuBarItems == null || menuBarItems.Count == 0)
                    owner.CloseMenu();
                else if (owner.openMenuOnHover || owner.IsOpen)
                    owner.OpenMenu(this);
            }

            /// <summary>
            /// Finds a direct child item by its entry name.
            /// </summary>
            public ContextMenu.Item GetChildItem(string itemName) => menuBarItems?.Find(x => x != null && x.name == itemName);
        }

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
            {
#if UNITY_EDITOR
                // Unity skips OnValidate for objects created in the editor, such as menu copies
                OnValidate();
#endif
                return;
            }

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization, refreshImmediately: false);
#endif
            Initialize();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
            refreshPending = false;
#endif

            CloseMenu();
            UnbindItems();

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
        }

        /// <summary>
        /// Audits the preview and initializes runtime interaction without rebuilding valid items.
        /// </summary>
        public void Initialize() => Refresh();

        /// <summary>
        /// Updates content and repairs generated items. Interaction is registered only in Play mode.
        /// </summary>
        public void Refresh()
        {
#if UNITY_EDITOR
            if (refreshPending)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
                refreshPending = false;
            }

            if (UnityEditor.EditorUtility.IsPersistent(this) || (Application.isPlaying && !Application.IsPlaying(gameObject)))
                return;
#endif

            if (itemPreset == null || !itemPreset.TryGetComponent<Button>(out _))
            {
                Debug.LogWarning("[Menu Bar] Assign an Item Preset with an Evo UI Button before refreshing.", this);
                return;
            }

            bool runtime = Application.IsPlaying(gameObject);
            if (runtime)
            {
                CloseMenu();

                // Items still listed stay bound, and the loop rebinds only those whose objects changed
                UnbindItems(isActiveAndEnabled);
            }

            bool changed = itemParent == null || items == null || generatedObjects == null || generatedItemPreset != itemPreset;

            if (itemParent == null)
                itemParent = transform;

            items ??= new List<Item>();
            generatedObjects ??= new List<GameObject>();
            bool presetChanged = generatedItemPreset != itemPreset;
            string linkPath = GeneratedObjects.GetLinkPath(this, itemPreset);
            changed |= generatedObjects.Count != items.Count;

            for (int i = generatedObjects.Count - 1; i >= 0; i--)
            {
                GameObject instance = generatedObjects[i];
                if (presetChanged || i >= items.Count || items[i] == null || instance == null || !instance.TryGetComponent<Button>(out _)
                    || GeneratedObjects.NeedsRelink(instance, itemPreset, linkPath))
                {
                    changed |= instance != null;
                    DestroyGeneratedObject(instance);
                    generatedObjects[i] = null;
                }
            }

            if (generatedObjects.Count > items.Count)
                generatedObjects.RemoveRange(items.Count, generatedObjects.Count - items.Count);
           
            while (generatedObjects.Count < items.Count)
                generatedObjects.Add(null);

            if (linkPath != null)
                changed |= GeneratedObjects.ClaimOrphans(generatedObjects, items.Count, itemParent, itemPreset, linkPath, DestroyGeneratedObject);

            generatedItemPreset = itemPreset;

            if (runtime)
            {
                if (!itemParent.TryGetComponent(out menuCanvas))
                    menuCanvas = itemParent.gameObject.AddComponent<Canvas>();
                if (!itemParent.TryGetComponent<GraphicRaycaster>(out _))
                    itemParent.gameObject.AddComponent<GraphicRaycaster>();
              
                menuCanvas.vertexColorAlwaysGammaSpace = true;
            }

            int siblingIndex = 0;
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                GameObject instance = generatedObjects[i];
                if (instance == null)
                {
                    generatedObjects[i] = instance = GeneratedObjects.Create(itemPreset, itemParent, linkPath);
                    changed = true;
                }
                else if (instance.transform.parent != itemParent)
                {
                    instance.transform.SetParent(itemParent, false);
                    changed = true;
                }

                changed |= generatedNames.Apply(i, instance, item.label);

                if (instance.transform.GetSiblingIndex() != siblingIndex)
                {
                    instance.transform.SetSiblingIndex(siblingIndex);
                    changed = true;
                }

                siblingIndex++;
                item.GeneratedObject = instance;
                item.GeneratedButton = instance.GetComponent<Button>();
              
                if (runtime || item.GeneratedButton.Text != item.label)
                {
                    item.GeneratedButton.Text = item.label;
                    changed = true;
                }

                // Visibility can lag behind the sprite, e.g. when the sprite was assigned while hidden
                if (runtime || item.GeneratedButton.Icon != item.icon
                    || (item.GeneratedButton.imageObject != null && item.GeneratedButton.EnableIcon != (item.icon != null)))
                {
                    item.GeneratedButton.Icon = item.icon;
                    changed = true;
                }

#if EVO_LOCALIZATION
                if (runtime)
                    item.GeneratedButton.enableLocalization = false;
#endif

                item.GeneratedMenu = null;

                if (instance.activeSelf != item.IsActive)
                {
                    instance.SetActive(item.IsActive);
                    changed = true;
                }

                if (runtime)
                {
                    ConfigureMenu(item);

                    if (isActiveAndEnabled)
                    {
                        item.Bind(this);
                        initializedItems.Add(item);
                    }
                }
            }

#if EVO_LOCALIZATION
            if (runtime && enableLocalization && localizedObject != null)
                UpdateLocalization();
#endif
            if ((runtime || changed) && itemParent is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

#if UNITY_EDITOR
            if (!runtime && changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i]?.GeneratedButton != null)
                        items[i].GeneratedButton.RecordContentChanges();
                }

                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        void ConfigureMenu(Item item)
        {
            item.menuBarItems ??= new List<ContextMenu.Item>();
            bool hasMenu = item.menuBarItems.Count > 0;
            item.GeneratedObject.TryGetComponent<ContextMenu>(out var menu);
           
            if (menu == null && hasMenu)
                menu = item.GeneratedObject.AddComponent<ContextMenu>();
            if (menu == null)
                return;

            menu.enabled = hasMenu;
            menu.menuPreset = contextMenuPreset;
            menu.triggerButton = ContextMenu.MouseButton.None;
            menu.offsetPosition = OffsetPosition.Custom;
            menu.customOffset = itemMenuOffset;
            menu.targetRectAnchor = Vector2.zero;
            menu.blockUIWhileOpen = blockUIWhileOpen;
            menu.closeOnOutsideClick = true;
            menu.usePointerPosition = false;
            menu.is3DObject = false;
            menu.targetCanvas = menuCanvas.rootCanvas;
            menu.menuItems = item.menuBarItems;
#if EVO_LOCALIZATION
            // The bar owns localization for its items, including nested menu entries.
            menu.enableLocalization = false;
#endif
            item.GeneratedMenu = hasMenu ? menu : null;
        }

        void UnbindItems(bool keepListed = false)
        {
            for (int i = 0; i < initializedItems.Count; i++)
            {
                if (!keepListed || items == null || !items.Contains(initializedItems[i]))
                    initializedItems[i].Unbind();
            }

            initializedItems.Clear();
        }

        static void DestroyGeneratedObject(GameObject instance)
        {
            if (instance == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(instance); }
            else
            {
                instance.SetActive(false);
                Destroy(instance);
            }
        }

        public void OpenMenu(Item targetItem)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || targetItem == null || !targetItem.IsActive
                || !items.Contains(targetItem) || targetItem.GeneratedObject == null || !targetItem.GeneratedObject.activeInHierarchy)
                return;

            if (targetItem == currentBarItem && targetItem.GeneratedMenu != null && targetItem.GeneratedMenu.IsVisible)
                return;

            CloseMenu();

            if (targetItem.GeneratedMenu == null || targetItem.menuBarItems == null || targetItem.menuBarItems.Count == 0)
                return;

            ContextMenu menu = targetItem.GeneratedMenu;
            menu.customOffset = itemMenuOffset;
            menu.blockUIWhileOpen = blockUIWhileOpen;
            menu.Show();
            
            if (!menu.IsVisible)
                return;

            currentBarItem = targetItem;

            if (targetItem.GeneratedButton != null)
                targetItem.GeneratedButton.SetState(InteractionState.Selected);
           
            previousOverrideSorting = menuCanvas.overrideSorting;
            previousSortingOrder = menuCanvas.sortingOrder;
            menuCanvas.overrideSorting = true;
            menuCanvas.sortingOrder = SortOrder;
        }

        /// <summary>
        /// Closes the menu of current item.
        /// </summary>
        public void CloseMenu()
        {
            if (currentBarItem == null)
                return;

            Item item = currentBarItem;
            currentBarItem = null; // Clear first: Hide invokes onHide synchronously.
            
            if (menuCanvas != null)
            {
                menuCanvas.overrideSorting = previousOverrideSorting;
                menuCanvas.sortingOrder = previousSortingOrder;
            }
          
            if (item.GeneratedButton != null)
                item.GeneratedButton.SetState(InteractionState.Normal);
         
            if (item.GeneratedMenu != null)
                item.GeneratedMenu.Hide();
        }

        /// <summary>
        /// Finds a top-level menu item by its label.
        /// </summary>
        public Item GetItem(string label) => items?.Find(x => x != null && x.label == label);

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || items == null)
                return;

            foreach (Item item in items)
            {
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(item.tableKey))
                    item.label = localizedObject.GetString(item.tableKey) ?? item.label;
                if (item.GeneratedButton != null)
                    item.GeneratedButton.Text = item.label;

                ContextMenu.LocalizeItems(item.menuBarItems, localizedObject);
            }
        }
#endif

#if UNITY_EDITOR
        bool refreshPending;

        void OnValidate()
        {
            GeneratedObjects.Watch(this, OnValidate);
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        void RefreshDelayed()
        {
            refreshPending = false;

            if (this == null || UnityEditor.EditorApplication.isCompiling
                || (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying))
                return;

            Refresh();
        }
#endif
    }
}