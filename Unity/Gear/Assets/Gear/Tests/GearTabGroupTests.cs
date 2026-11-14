using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.Navigation;

namespace Gear.Tests
{
    /// <summary>
    /// GearTabGroup 的 EditMode 测试：页面切换、事件与未知 id 保护。
    /// </summary>
    public class GearTabGroupTests
    {
        private GameObject root;
        private GearTabGroup group;
        private GameObject pageA;
        private GameObject pageB;
        private Button buttonA;
        private Button buttonB;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TabGroupRoot", typeof(RectTransform), typeof(GearTabGroup));
            group = root.GetComponent<GearTabGroup>();

            pageA = new GameObject("PageA", typeof(RectTransform));
            pageA.transform.SetParent(root.transform, false);
            pageB = new GameObject("PageB", typeof(RectTransform));
            pageB.transform.SetParent(root.transform, false);

            buttonA = new GameObject("TabA", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            buttonA.transform.SetParent(root.transform, false);
            buttonB = new GameObject("TabB", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            buttonB.transform.SetParent(root.transform, false);

            group.Configure(new List<GearTabGroup.Tab>
            {
                new GearTabGroup.Tab { id = "a", button = buttonA, page = pageA },
                new GearTabGroup.Tab { id = "b", button = buttonB, page = pageB },
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Select_SwitchesPageAndRaisesEvent()
        {
            int changes = 0;
            group.TabChanged += _ => changes++;
            group.Select("a");
            Assert.AreEqual("a", group.Current);
            Assert.IsTrue(pageA.activeSelf);
            Assert.IsFalse(pageB.activeSelf);

            group.Select("b");
            Assert.AreEqual("b", group.Current);
            Assert.IsFalse(pageA.activeSelf);
            Assert.IsTrue(pageB.activeSelf);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Select_SameTab_DoesNotRaiseDuplicateEvent()
        {
            int changes = 0;
            group.TabChanged += _ => changes++;
            group.Select("a");
            group.Select("a");
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Select_UnknownId_KeepsCurrentTab()
        {
            group.Select("a");
            group.Select("missing");
            Assert.AreEqual("a", group.Current);
            Assert.IsTrue(pageA.activeSelf);
            Assert.IsFalse(pageB.activeSelf);
        }

        [Test]
        public void ButtonClick_SwitchesTab()
        {
            group.Select("a");
            buttonB.onClick.Invoke();
            Assert.AreEqual("b", group.Current);
            Assert.IsTrue(pageB.activeSelf);
        }
    }
}
