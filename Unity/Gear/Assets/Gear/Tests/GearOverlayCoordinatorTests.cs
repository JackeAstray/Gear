using Gear.Utilities.UI.ImageExtensions.Overlays;
using NUnit.Framework;

namespace Gear.Tests
{
    public sealed class GearOverlayCoordinatorTests
    {
        [SetUp]
        public void SetUp() => GearOverlayCoordinator.Reset();

        [TearDown]
        public void TearDown() => GearOverlayCoordinator.Reset();

        [Test]
        public void Escape_ClosesOnlyTopOwner()
        {
            object a = new object();
            object b = new object();
            int closedA = 0;
            int closedB = 0;
            GearOverlayCoordinator.Open(a, () => closedA++);
            GearOverlayCoordinator.Open(b, () => closedB++);

            Assert.IsFalse(GearOverlayCoordinator.TryHandleEscape(a), "非栈顶不消费 Esc");
            Assert.AreEqual(2, GearOverlayCoordinator.Count);

            Assert.IsTrue(GearOverlayCoordinator.TryHandleEscape(b));
            Assert.AreEqual(1, closedB);
            Assert.AreEqual(0, closedA);
            Assert.AreEqual(1, GearOverlayCoordinator.Count);
        }

        [Test]
        public void SameOwner_ReplacesPreviousEntry()
        {
            object owner = new object();
            int closed = 0;
            GearOverlayCoordinator.Open(owner, () => closed++);
            GearOverlayCoordinator.Open(owner, () => closed++);

            Assert.AreEqual(1, GearOverlayCoordinator.Count, "同一 owner 只保留一个");
            Assert.AreEqual(1, closed, "旧项应先关闭");
        }

        [Test]
        public void CloseAll_ClosesInReverseOrder()
        {
            var order = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                GearOverlayCoordinator.Open(new object(), () => order.Add(index));
            }

            GearOverlayCoordinator.CloseAll();

            Assert.AreEqual(0, GearOverlayCoordinator.Count);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, order);
        }
    }
}
