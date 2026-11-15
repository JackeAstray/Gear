using System.Collections.Generic;
using Gear.Utilities.UI.ImageExtensions.Overlays;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// M1.7 覆盖层展卡的小演示：Toast / Tooltip / ContextMenu。
    /// prefab 里只挂这一个组件并指向按钮，不在运行时拼 UI。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShowcaseExtrasDemo : MonoBehaviour
    {
        public enum Mode
        {
            Toast = 0,
            Tooltip = 1,
            ContextMenu = 2,
        }

        [SerializeField] private Mode mode;
        [SerializeField] private Button action;
        [SerializeField] private RectTransform tooltipTarget;

        public void Configure(Mode value, Button button, RectTransform target = null)
        {
            mode = value;
            action = button;
            tooltipTarget = target;
        }

        private void OnEnable()
        {
            if (action == null) action = GetComponentInChildren<Button>(true);
            if (action != null)
            {
                action.onClick.RemoveListener(Act);
                action.onClick.AddListener(Act);
            }

            if (mode == Mode.Tooltip)
            {
                Transform target = tooltipTarget != null ? tooltipTarget : action != null ? action.transform : null;
                if (target != null)
                {
                    GearTooltip.Attach(target.gameObject,
                        "GearTooltip 跟随指针显示；支持标题、图标与延迟。",
                        "Tooltip", 0.2f);
                }
            }
        }

        private void OnDisable()
        {
            if (action != null) action.onClick.RemoveListener(Act);
        }

        private void Act()
        {
            switch (mode)
            {
                case Mode.Toast:
                    GearToast.Show("Toast · 来自展厅", "M1.7", GearToastKind.Success);
                    break;
                case Mode.ContextMenu:
                    RectTransform anchor = action != null ? action.GetComponent<RectTransform>() : null;
                    if (anchor == null) return;
                    GearContextMenu.Show(anchor, new List<GearContextMenuItem>
                    {
                        new GearContextMenuItem("Copy", () => GearToast.Show("Copy", "ContextMenu")),
                        new GearContextMenuItem("Rename", () => GearToast.Show("Rename", "ContextMenu")),
                        GearContextMenuItem.Separator(),
                        new GearContextMenuItem("Delete", () => GearToast.Show("Delete", "ContextMenu", GearToastKind.Warning), true, true),
                    });
                    break;
            }
        }
    }
}
