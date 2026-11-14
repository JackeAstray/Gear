using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Tests
{
    /// <summary>GearContextMenu 模型、打开/关闭与子菜单的 EditMode 测试。</summary>
    public class GearContextMenuTests
    {
        [TearDown]
        public void TearDown()
        {
            GearContextMenu[] menus = Object.FindObjectsByType<GearContextMenu>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < menus.Length; i++)
            {
                if (menus[i] != null) menus[i].Close();
            }

            GameObject canvas = GameObject.Find("GearOverlayCanvas");
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Item_WithChildren_ExposesSubmenu()
        {
            GearContextMenuItem child = new GearContextMenuItem("Rename", () => { });
            GearContextMenuItem parent = new GearContextMenuItem("More", new[] { child });

            Assert.IsTrue(parent.HasChildren);
            Assert.AreEqual(1, parent.children.Count);
        }

        [Test]
        public void Show_ThenClose_DestroysMenu()
        {
            GearContextMenu menu = GearContextMenu.Show(new Vector2(120f, 120f), new[]
            {
                new GearContextMenuItem("Rename", () => { }),
                GearContextMenuItem.Separator(),
                new GearContextMenuItem("Delete", () => { }, danger: true),
            });

            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.gameObject.activeSelf);

            menu.Close();
            Assert.IsTrue(menu == null);
        }

        [Test]
        public void CustomRow_BuildsContent()
        {
            GearContextMenu menu = GearContextMenu.Show(Vector2.zero, new[]
            {
                GearContextMenuItem.Custom(row =>
                {
                    GameObject child = new GameObject("CustomChild", typeof(RectTransform));
                    child.transform.SetParent(row, false);
                }, 44f),
            });

            Assert.IsNotNull(menu);
            Assert.IsNotNull(GameObject.Find("CustomChild"));
            menu.Close();
        }

        [Test]
        public void Submenu_OpensAndClosesWithParent()
        {
            GearContextMenuItem parent = new GearContextMenuItem("More", new[]
            {
                new GearContextMenuItem("Child", () => { }),
            });

            GearContextMenu menu = GearContextMenu.Show(Vector2.zero, new[] { parent });
            Assert.IsNotNull(menu);

            UnityEngine.UI.Button row = menu.GetComponentInChildren<UnityEngine.UI.Button>(true);
            Assert.IsNotNull(row);
            row.onClick.Invoke();

            GearContextMenu[] menus = Object.FindObjectsByType<GearContextMenu>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(2, menus.Length);

            menu.Close();
            Assert.AreEqual(0, Object.FindObjectsByType<GearContextMenu>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }
    }
}
