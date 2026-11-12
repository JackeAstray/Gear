using System.Collections.Generic;
using Evo.Tween;
using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Context Menu tab: a folder of file tiles, each with its own menu (Open, Duplicate, Share, Delete).
    /// A deleted file leaves an empty slot that adds a new file when clicked; Duplicate only shows while a slot is free.
    /// Options apply to every tile's menu, including tiles added later (they copy the template or the tile they came from).
    /// A menu needs a way to close, so Close on click and Click outside each lock on while the other is off.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoContextMenu : MonoBehaviour
    {
        const int DuplicateItem = 1;

        [SerializeField] private List<DemoFileTile> tiles = new();
        [Tooltip("Inactive tile that new files are copied from.")]
        [SerializeField] private DemoFileTile template;
        [Tooltip("One per position, left to right. A slot shows while no tile sits on it.")]
        [SerializeField] private CanvasGroup[] slots;
        [SerializeField] private Sprite[] newFileIcons;
        [SerializeField] private float glideDuration = 0.3f;

        [Header("Options")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private CanvasGroup closeOnClickOption;
        [SerializeField] private CanvasGroup outsideClickOption;

        static readonly string[] Infos = { "Left-click a file to open its menu.", "Right-click a file to open its menu." };

        int nextIcon;

        void Awake()
        {
            Layout(false);
            DemoUtils.SetLocked(closeOnClickOption, !template.Menu.closeOnOutsideClick, false);
            DemoUtils.SetLocked(outsideClickOption, !template.Menu.closeOnItemClick, false);
        }

        void OnEnable() => UpdateInfo();

        public void OnMenuShow(DemoFileTile tile) => tile.Menu.menuItems[DuplicateItem].IsActive = tiles.Count < slots.Length;

        public void Duplicate(DemoFileTile source)
        {
            if (tiles.Count >= slots.Length)
                return;

            DemoFileTile copy = Instantiate(source, source.transform.parent);
            copy.Clear();
            int index = tiles.IndexOf(source) + 1;
            tiles.Insert(index, copy);
            copy.Rect.anchoredPosition = SlotPosition(index);
            copy.PopIn();
            Layout(true);
        }

        // Called by the empty slots
        public void AddFile()
        {
            if (tiles.Count >= slots.Length)
                return;

            DemoFileTile tile = Instantiate(template, template.transform.parent);
            tile.gameObject.SetActive(true);
            tile.Clear();
            tile.SetIcon(newFileIcons[nextIcon]);
            nextIcon = (nextIcon + 1) % newFileIcons.Length;
            tiles.Add(tile);
            tile.Rect.anchoredPosition = SlotPosition(tiles.Count - 1);
            tile.PopIn();
            Layout(true);
        }

        public void Remove(DemoFileTile tile)
        {
            tiles.Remove(tile);
            Layout(true);
        }

        // Left or Right
        public void SetTrigger(int index)
        {
            for (int i = 0; i < MenuCount; i++)
                MenuAt(i).SetTriggerButton(index);

            UpdateInfo();
        }

        // At the pointer, or beside the file (its anchor point is set on each menu)
        public void SetPosition(int index)
        {
            for (int i = 0; i < MenuCount; i++)
                MenuAt(i).usePointerPosition = index == 0;
        }

        public void SetCloseOnClick(bool value)
        {
            for (int i = 0; i < MenuCount; i++)
                MenuAt(i).closeOnItemClick = value;

            DemoUtils.SetLocked(outsideClickOption, !value);
        }

        public void SetOutsideClick(bool value)
        {
            for (int i = 0; i < MenuCount; i++)
                MenuAt(i).closeOnOutsideClick = value;

            DemoUtils.SetLocked(closeOnClickOption, !value);
        }

        public void SetHoverSubmenus(bool value)
        {
            for (int i = 0; i < MenuCount; i++)
            {
                foreach (ContextMenu.Item item in MenuAt(i).menuItems)
                {
                    if (item.type == ContextMenu.Item.ItemType.Submenu)
                        item.expandOnHover = value;
                }
            }
        }

        public void SetBlockUI(bool value)
        {
            for (int i = 0; i < MenuCount; i++)
                MenuAt(i).blockUIWhileOpen = value;
        }

        // The template's menu first, then every tile's
        int MenuCount => tiles.Count + 1;
        ContextMenu MenuAt(int index) => index == 0 ? template.Menu : tiles[index - 1].Menu;

        Vector2 SlotPosition(int index) => ((RectTransform)slots[index].transform).anchoredPosition;

        // Tiles glide to their slots; the slots left over show as empty
        void Layout(bool animate)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                bool free = i >= tiles.Count;
                slots[i].alpha = free ? 1 : 0;
                slots[i].blocksRaycasts = free;

                if (free)
                    continue;

                RectTransform rect = tiles[i].Rect;
                if (!animate) { rect.anchoredPosition = SlotPosition(i); }
                else
                {
                    EvoTween.Create(rect.anchoredPosition, SlotPosition(i), glideDuration)
                        .WithEase(Ease.OutCubic)
                        .WithTimeMode(TimeMode.Unscaled)
                        .BindToAnchoredPosition(rect);
                }
            }
        }

        void UpdateInfo() => info.text = Infos[(int)template.Menu.triggerButton];
    }
}
