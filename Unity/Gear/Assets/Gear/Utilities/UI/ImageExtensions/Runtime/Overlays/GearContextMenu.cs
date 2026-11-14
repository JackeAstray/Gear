using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>上下文菜单的一项。</summary>
    [Serializable]
    public sealed class GearContextMenuItem
    {
        public string label;
        public Action action;
        public bool enabled = true;
        public bool separator;
        public bool danger;
        public Sprite icon;
        public List<GearContextMenuItem> children;

        /// <summary>自定义行构建器；不为空时忽略 label/icon，由调用方填充行内容。</summary>
        public Action<RectTransform> customRow;
        public float customHeight = ItemHeightDefault;

        private const float ItemHeightDefault = 34f;

        public bool HasChildren => children != null && children.Count > 0;
        public bool IsCustomRow => customRow != null;

        public GearContextMenuItem() { }

        public GearContextMenuItem(string label, Action action, bool enabled = true, bool danger = false, Sprite icon = null)
        {
            this.label = label;
            this.action = action;
            this.enabled = enabled;
            this.danger = danger;
            this.icon = icon;
        }

        /// <summary>带子菜单的菜单项。</summary>
        public GearContextMenuItem(string label, IReadOnlyList<GearContextMenuItem> submenu, bool enabled = true, Sprite icon = null)
        {
            this.label = label;
            this.enabled = enabled;
            this.icon = icon;
            children = new List<GearContextMenuItem>();
            if (submenu != null)
            {
                for (int i = 0; i < submenu.Count; i++)
                {
                    if (submenu[i] != null) children.Add(submenu[i]);
                }
            }
        }

        public static GearContextMenuItem Separator()
        {
            return new GearContextMenuItem { separator = true };
        }

        /// <summary>自定义行：由调用方往 row 里添加任意子节点。</summary>
        public static GearContextMenuItem Custom(Action<RectTransform> build, float height = 34f)
        {
            return new GearContextMenuItem
            {
                customRow = build,
                customHeight = Mathf.Max(1f, height),
                enabled = true,
            };
        }
    }

    /// <summary>
    /// 轻量上下文菜单：点击空白/按 Esc 关闭，按屏幕坐标定位，自动贴边翻转。
    /// <para>
    /// 用法：<c>GearContextMenu.Show(Input.mousePosition, new[] { new GearContextMenuItem("重命名", Rename), GearContextMenuItem.Separator(), new GearContextMenuItem("删除", Delete, danger: true) });</c>
    /// </para>
    /// </summary>
    public sealed class GearContextMenu : MonoBehaviour
    {
        private const float PanelWidth = 232f;
        private const float ItemHeight = 34f;

        private static GearContextMenu s_instance;

        private RectTransform panel;
        private RectTransform blocker;
        private RectTransform rectTransform;
        private GearContextMenu parentMenu;
        private GearContextMenu childMenu;

        /// <summary>显示菜单。</summary>
        public static GearContextMenu Show(Vector2 screenPosition, IReadOnlyList<GearContextMenuItem> items)
        {
            if (items == null || items.Count == 0) return null;

            if (s_instance != null)
            {
                s_instance.Close();
            }

            GameObject go = CreateMenuObject();
            GearContextMenu menu = go.AddComponent<GearContextMenu>();
            menu.Build(items, true);
            go.SetActive(true);
            menu.SetScreenPosition(screenPosition);
            s_instance = menu;
            return menu;
        }

        private static GameObject CreateMenuObject()
        {
            GameObject go = new GameObject("GearContextMenu", typeof(RectTransform), typeof(CanvasGroup));
            go.SetActive(false);
            GearOverlayLayer.ApplyUILayer(go);
            go.transform.SetParent(GearOverlayLayer.Root, false);
            return go;
        }

        private void Build(IReadOnlyList<GearContextMenuItem> items, bool createBlocker)
        {
            rectTransform = (RectTransform)transform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = new Vector2(PanelWidth, 0f);

            CanvasGroup group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            if (createBlocker)
                CreateBlocker();

            ImageEx background = GearOverlayLayer.CreateRoundedPanel(
                transform, "Background", GearOverlayTheme.Get("OverlaySurfaceRaised", GearOverlayTheme.SurfaceRaised), 10f, true, shadow: true);
            GearOverlayLayer.Stretch(background.transform as RectTransform);
            LayoutElement backgroundLayout = background.gameObject.AddComponent<LayoutElement>();
            backgroundLayout.ignoreLayout = true;

            VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < items.Count; i++)
            {
                GearContextMenuItem item = items[i];
                if (item == null) continue;
                if (item.separator)
                {
                    CreateSeparator();
                }
                else
                {
                    CreateItem(item);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private void CreateBlocker()
        {
            // 全屏透明遮挡：点击外部关闭菜单
            GameObject blockerObject = new GameObject("Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            GearOverlayLayer.ApplyUILayer(blockerObject);
            blockerObject.transform.SetParent(transform.parent, false);
            blocker = (RectTransform)blockerObject.transform;
            GearOverlayLayer.Stretch(blocker);
            // 遮挡层必须在菜单之前绘制，否则会盖住菜单项、吃掉点击
            blocker.SetSiblingIndex(transform.GetSiblingIndex());
            Image blockerImage = blockerObject.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0f);
            blockerImage.raycastTarget = true;
            Button blockerButton = blockerObject.GetComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.AddListener(Close);
        }

        private void CreateSeparator()
        {
            GameObject line = new GameObject("Separator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GearOverlayLayer.ApplyUILayer(line);
            line.transform.SetParent(transform, false);
            Image image = line.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);
            image.raycastTarget = false;
            LayoutElement layout = line.AddComponent<LayoutElement>();
            layout.preferredHeight = 9f;
            layout.minHeight = 9f;
        }

        private void CreateItem(GearContextMenuItem item)
        {
            if (item.IsCustomRow)
            {
                CreateCustomItem(item);
                return;
            }

            GameObject row = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            GearOverlayLayer.ApplyUILayer(row);
            row.transform.SetParent(transform, false);

            ImageEx background = row.GetComponent<ImageEx>();
            background.DrawShape = DrawShape.Rectangle;
            background.Rectangle = new RectangleImg { CornerRadius = new Vector4(7f, 7f, 7f, 7f) };
            background.color = new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = ItemHeight;
            layout.minHeight = ItemHeight;

            Button button = row.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.interactable = item.enabled;
            if (item.enabled)
            {
                if (item.HasChildren)
                {
                    RectTransform rowRect = (RectTransform)row.transform;
                    button.onClick.AddListener(() => OpenSubmenu(item, rowRect));
                }
                else if (item.action != null)
                {
                    button.onClick.AddListener(() =>
                    {
                        Action action = item.action;
                        Close();
                        action?.Invoke();
                    });
                }
            }

            float left = 14f;
            if (item.icon != null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                GearOverlayLayer.ApplyUILayer(iconObject);
                iconObject.transform.SetParent(row.transform, false);
                Image iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = item.icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(12f, 0f);
                iconRect.sizeDelta = new Vector2(16f, 16f);
                left = 36f;
            }

            Color textColor = item.danger
                ? GearOverlayTheme.Get("OverlayDanger", GearOverlayTheme.Danger)
                : GearOverlayTheme.Get("OverlayText", GearOverlayTheme.TextPrimary);
            if (!item.enabled) textColor.a *= 0.4f;

            TextMeshProUGUI label = GearOverlayLayer.CreateText(
                row.transform, "Label", 14f, textColor, false, TextAlignmentOptions.Left);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(left, 0f);
            labelRect.offsetMax = new Vector2(-12f, 0f);
            label.text = item.label ?? string.Empty;
            label.alignment = TextAlignmentOptions.Left;

            if (item.HasChildren)
            {
                TextMeshProUGUI arrow = GearOverlayLayer.CreateText(
                    row.transform, "Arrow", 14f, textColor, false, TextAlignmentOptions.Right);
                RectTransform arrowRect = (RectTransform)arrow.transform;
                arrowRect.anchorMin = Vector2.zero;
                arrowRect.anchorMax = Vector2.one;
                arrowRect.offsetMin = new Vector2(0f, 0f);
                arrowRect.offsetMax = new Vector2(-12f, 0f);
                arrow.text = "›";
                arrow.alignment = TextAlignmentOptions.Right;
            }

            GearContextMenuEntry entry = row.AddComponent<GearContextMenuEntry>();
            entry.Initialize(background, item.enabled);
        }

        private void CreateCustomItem(GearContextMenuItem item)
        {
            GameObject row = new GameObject("CustomItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            GearOverlayLayer.ApplyUILayer(row);
            row.transform.SetParent(transform, false);

            ImageEx background = row.GetComponent<ImageEx>();
            background.DrawShape = DrawShape.Rectangle;
            background.Rectangle = new RectangleImg { CornerRadius = new Vector4(7f, 7f, 7f, 7f) };
            background.color = new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;

            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = item.customHeight;
            layout.minHeight = item.customHeight;

            Button button = row.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            button.interactable = item.enabled;
            if (item.enabled && item.action != null)
            {
                button.onClick.AddListener(() =>
                {
                    Action action = item.action;
                    Close();
                    action?.Invoke();
                });
            }

            GearContextMenuEntry entry = row.AddComponent<GearContextMenuEntry>();
            entry.Initialize(background, item.enabled);

            item.customRow?.Invoke((RectTransform)row.transform);
        }

        private void SetScreenPosition(Vector2 screenPosition)
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            RectTransform root = GearOverlayLayer.Root;
            if (root == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, null, out Vector2 local))
            {
                local = Vector2.zero;
            }

            Vector2 size = rectTransform.rect.size;
            Vector2 position = local;
            Rect rootRect = root.rect;

            if (position.x + size.x > rootRect.xMax - 8f) position.x = local.x - size.x;
            if (position.y - size.y < rootRect.yMin + 8f) position.y = local.y + size.y;

            position.x = Mathf.Clamp(position.x, rootRect.xMin + 8f, rootRect.xMax - size.x - 8f);
            position.y = Mathf.Clamp(position.y, rootRect.yMin + size.y + 8f, rootRect.yMax - 8f);
            rectTransform.anchoredPosition = position;
        }

        private void OpenSubmenu(GearContextMenuItem item, RectTransform row)
        {
            if (item == null || !item.HasChildren) return;

            if (childMenu != null)
            {
                GearContextMenu old = childMenu;
                childMenu = null;
                old.Close();
            }

            GameObject go = CreateMenuObject();
            GearContextMenu submenu = go.AddComponent<GearContextMenu>();
            submenu.parentMenu = this;
            submenu.Build(item.children, false);
            go.SetActive(true);
            submenu.PlaceBeside(row);
            childMenu = submenu;
        }

        private void PlaceBeside(RectTransform row)
        {
            if (row == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            Vector3[] corners = new Vector3[4];
            row.GetWorldCorners(corners);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            SetScreenPosition(screen);
        }

        private void Update()
        {
            // 子菜单不单独处理 Esc；由顶层菜单统一关闭整条链
            if (parentMenu != null) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
#endif
        }

        /// <summary>关闭并销毁菜单（包含子菜单）。</summary>
        public void Close()
        {
            if (childMenu != null)
            {
                GearContextMenu child = childMenu;
                childMenu = null;
                child.Close();
            }

            if (parentMenu != null && parentMenu.childMenu == this)
                parentMenu.childMenu = null;
            parentMenu = null;

            if (s_instance == this) s_instance = null;

            if (blocker != null)
            {
                DestroyObject(blocker.gameObject);
                blocker = null;
            }

            DestroyObject(gameObject);
        }

        private void OnDestroy()
        {
            if (childMenu != null)
            {
                GearContextMenu child = childMenu;
                childMenu = null;
                child.Close();
            }

            if (parentMenu != null && parentMenu.childMenu == this)
                parentMenu.childMenu = null;
            parentMenu = null;

            if (s_instance == this) s_instance = null;
            if (blocker != null)
            {
                DestroyObject(blocker.gameObject);
                blocker = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }

    /// <summary>菜单项悬停高亮。</summary>
    [DisallowMultipleComponent]
    public sealed class GearContextMenuEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ImageEx background;
        private Color normal;
        private Color hover;
        private bool enabledItem;

        internal void Initialize(ImageEx target, bool isEnabled)
        {
            background = target;
            enabledItem = isEnabled;
            normal = new Color(1f, 1f, 1f, 0f);
            hover = new Color(1f, 1f, 1f, 0.08f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null && enabledItem) background.color = hover;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null) background.color = normal;
        }
    }
}
