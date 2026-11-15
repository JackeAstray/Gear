using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearRenderBudgetProbeTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Budget", typeof(RectTransform), typeof(Canvas), typeof(GearRenderBudgetProbe));
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Graphic" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root.transform, false);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Report_ContainsMaterialCount()
        {
            var probe = root.GetComponent<GearRenderBudgetProbe>();
            Assert.GreaterOrEqual(probe.UniqueMaterials(), 1);
            Assert.IsTrue(probe.Report().Contains("materials="));
        }
    }
}
