using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Context Menu Preset"), HelpURL(Constants.HelpUrl + "ui-elements/context-menu")]
    public class ContextMenuPreset : MonoBehaviour
    {
        // References
        public Transform itemContainer;
        public GameObject buttonPrefab;
        public GameObject separatorPrefab;
        [FormerlySerializedAs("sectionPrefab")]
        public GameObject submenuItemPrefab;

        // Clips
        public TweenSequence openAnimation = CreateAnimation(true, new Vector2(0f, -20f));
        public TweenSequence closeAnimation = CreateAnimation(false, new Vector2(0f, -20f));

        // Cache
        ContextMenu sourceMenu;
        CanvasGroup canvasGroup;
        RectTransform rectTransform;

        // State
        int nestingDepth;
        bool isClosing;
        TweenHandle currentAnimation;

        // Runtime
        readonly List<ContextMenuSubmenu> submenuList = new();

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            
            if (itemContainer == null)
                itemContainer = transform;

            // Destroy waits for the end of the frame, so hide the placeholders before the menu is measured
            for (int i = itemContainer.childCount - 1; i >= 0; i--)
            {
                GameObject placeholder = itemContainer.GetChild(i).gameObject;
                placeholder.SetActive(false);
                Destroy(placeholder);
            }
        }

        void OnDestroy()
        {
            currentAnimation.Kill();
            submenuList.Clear();
        }

        // Also supplies the submenu defaults, which slide from the side instead
        internal static TweenSequence CreateAnimation(bool open, Vector2 offset)
        {
            TweenSequence sequence = new() { TimeMode = TimeMode.Unscaled };

            TweenClip fade = new();
            fade.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = !open, from = 0f, to = open ? 1f : 0f };
            fade.Settings.Duration = 0.1f;
            fade.Settings.Ease = Ease.InOutSine;

            TweenClip slide = new();
            slide.Settings.Module = new RectTransformSlideModule
            {
                fromCurrent = false,
                from = open ? offset : Vector2.zero,
                to = open ? Vector2.zero : offset
            };
            slide.Settings.Duration = open ? 0.15f : 0.1f;
            slide.Settings.Ease = open ? Ease.OutCubic : Ease.InCubic;

            sequence.InlineClips.Add(fade);
            sequence.InlineClips.Add(slide);
            return sequence;
        }

        public void Setup(ContextMenu source, List<ContextMenu.Item> items) => Setup(source, items, 0);

        internal void Setup(ContextMenu source, List<ContextMenu.Item> items, int depth)
        {
            sourceMenu = source;
            nestingDepth = Mathf.Max(0, depth);

            CreateMenuItems(items);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void CollapseAllSubmenus(bool immediate = false)
        {
            foreach (var submenu in submenuList)
            {
                if (submenu != null && submenu.IsExpanded)
                    submenu.CollapseSubmenu(immediate);
            }
        }

        public void Open()
        {
            if (isClosing)
                return;

            currentAnimation.Kill();
            SetInteraction(true);
            currentAnimation = openAnimation.Trigger(gameObject);
        }

        public void Close()
        {
            if (isClosing)
                return;

            isClosing = true;
            CollapseAllSubmenus(true);
            currentAnimation.Complete(); // Finish the entry slide so the exit slide captures the positioned rest state
            SetInteraction(false);
            currentAnimation = closeAnimation.Trigger(gameObject).OnComplete(DestroySelf);

            if (!currentAnimation.IsActive)
                DestroySelf();
        }

        public void CloseImmediate()
        {
            isClosing = true;
            currentAnimation.Kill();
            CollapseAllSubmenus(true);
            SetInteraction(false);
            Destroy(gameObject);
        }

        internal TweenHandle PlaySubmenuAnimation(TweenSequence source, bool mirrorHorizontally)
        {
            if (source == null)
                return default;

            TweenSequence runtimeAnimation = source.Clone();
            runtimeAnimation.DetachPreset();

            List<TweenClip> clips = runtimeAnimation.InlineClips;
            for (int i = 0; i < clips.Count; i++)
            {
                TweenClip clip = clips[i];
                if (clip == null)
                    continue;

                switch (clip.Settings.Module)
                {
                    case CanvasGroupAlphaModule:
                        clip.Target = canvasGroup;
                        break;

                    case RectTransformSlideModule slideModule:
                        clip.Target = rectTransform;
                        if (mirrorHorizontally)
                        {
                            slideModule.from = new Vector2(-slideModule.from.x, slideModule.from.y);
                            slideModule.to = new Vector2(-slideModule.to.x, slideModule.to.y);
                        }
                        break;
                }
            }

            return runtimeAnimation.Trigger(gameObject);
        }

        internal void SetInteraction(bool value)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        void DestroySelf()
        {
            currentAnimation = default;

            if (this != null)
                Destroy(gameObject);
        }

        void CreateMenuItems(List<ContextMenu.Item> items)
        {
            foreach (var item in items)
            {
                if (!item.IsActive)
                    continue;

                CreateMenuItem(item);
            }
        }

        GameObject CreateMenuItem(ContextMenu.Item item)
        {
            GameObject itemGO = null;

            switch (item.type)
            {
                case ContextMenu.Item.ItemType.Button:
                    itemGO = CreateButtonItem(item);
                    break;
                case ContextMenu.Item.ItemType.Separator:
                    itemGO = CreateSeparatorItem();
                    break;
                case ContextMenu.Item.ItemType.Submenu:
                    itemGO = CreateSubmenuItem(item);
                    break;
                case ContextMenu.Item.ItemType.CustomObject:
                    itemGO = CreateCustomItem(item);
                    break;
            }

            if (itemGO != null)
                item.GeneratedObject = itemGO;

            return itemGO;
        }

        GameObject CreateButtonItem(ContextMenu.Item item)
        {
            GameObject prefab = item.buttonPrefab != null ? item.buttonPrefab : buttonPrefab;
            if (prefab == null)
                return null;

            GameObject buttonGO = Instantiate(prefab, itemContainer);
            if (buttonGO.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.AddListener(() =>
                {
                    item.onClick?.Invoke();

                    if (sourceMenu != null)
                        sourceMenu.OnItemClicked(item);
                });
#if EVO_LOCALIZATION
                btn.enableLocalization = false;
#endif
                btn.Text = item.name;
                btn.Icon = item.icon;
            }
            return buttonGO;
        }

        GameObject CreateSeparatorItem()
        {
            if (separatorPrefab == null)
                return null;

            GameObject separatorGO = Instantiate(separatorPrefab, itemContainer);
            return separatorGO;
        }

        GameObject CreateSubmenuItem(ContextMenu.Item item)
        {
            if (submenuItemPrefab == null)
                return null;

            GameObject submenuGO = Instantiate(submenuItemPrefab, itemContainer);
            if (submenuGO.TryGetComponent<ContextMenuSubmenu>(out var submenuComponent))
            {
                submenuComponent.Setup(sourceMenu, item, nestingDepth);
                submenuList.Add(submenuComponent);
            }
            return submenuGO;
        }

        GameObject CreateCustomItem(ContextMenu.Item item)
        {
            if (item.customObject == null)
                return null;

            GameObject customGO = Instantiate(item.customObject, itemContainer);
            if (customGO.TryGetComponent<Button>(out var customButton))
            {
                customButton.onClick.AddListener(() =>
                {
                    item.onClick?.Invoke();

                    if (sourceMenu != null)
                        sourceMenu.OnItemClicked(item);
                });
            }
            return customGO;
        }

        #region Obsolete API
        [System.Obsolete("Use submenuItemPrefab instead.")]
        public GameObject sectionPrefab { get => submenuItemPrefab; set => submenuItemPrefab = value; }

        [System.Obsolete("Use CollapseAllSubmenus(immediate) instead.")]
        public void CollapseAllSections(bool immediate = false) => CollapseAllSubmenus(immediate);
        #endregion
    }
}