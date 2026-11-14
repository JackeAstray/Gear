using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Tests
{
    /// <summary>
    /// GearPopover 的 EditMode 测试：创建、内容构建与关闭。
    /// </summary>
    public class GearPopoverTests
    {
        private GameObject anchor;

        [SetUp]
        public void SetUp()
        {
            anchor = new GameObject("PopoverAnchor", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            GearPopover.CloseOpen();
            if (anchor != null)
                Object.DestroyImmediate(anchor);

            // EditMode 下覆盖层 Canvas 会留在当前场景，测试结束清理，避免污染其他用例
            GameObject overlay = GameObject.Find("GearOverlayCanvas");
            if (overlay != null)
                Object.DestroyImmediate(overlay);
        }

        [Test]
        public void Show_BuildsContentAndCanClose()
        {
            int built = 0;
            GearPopover popover = GearPopover.Show((RectTransform)anchor.transform, new Vector2(240f, 120f), content =>
            {
                built++;
                new GameObject("Child", typeof(RectTransform)).transform.SetParent(content, false);
            });

            Assert.IsNotNull(popover);
            Assert.IsTrue(popover.IsOpen);
            Assert.IsNotNull(popover.Content);
            Assert.AreEqual(1, popover.Content.childCount);
            Assert.AreEqual(1, built);

            popover.Close();
            Assert.IsTrue(popover == null);
        }
    }
}
