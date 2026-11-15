using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearImageExQualityTests
    {
        [TearDown]
        public void TearDown() => GearImageExQuality.SetMode(GearImageExQuality.Mode.Full);

        [Test]
        public void LiteMode_TogglesGlobalKeyword()
        {
            GearImageExQuality.SetLite(true);
            Assert.IsTrue(Shader.IsKeywordEnabled(GearImageExQuality.LiteKeyword));
            Assert.AreEqual(GearImageExQuality.Mode.Lite, GearImageExQuality.Current);

            GearImageExQuality.SetLite(false);
            Assert.IsFalse(Shader.IsKeywordEnabled(GearImageExQuality.LiteKeyword));
        }
    }

    public sealed class GearStylePageBudgetTests
    {
        private GameObject root;
        private GearStylePageBudget budget;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("StylePage", typeof(RectTransform), typeof(Canvas), typeof(GearStylePageBudget));
            budget = root.GetComponent<GearStylePageBudget>();
            for (int i = 0; i < 3; i++)
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
        public void Analyze_CountsGraphicsAndMaterials()
        {
            GearStylePageBudget.Report report = budget.Analyze();
            Assert.AreEqual(3, report.Graphics);
            Assert.GreaterOrEqual(report.Materials, 1);
        }
    }
}
