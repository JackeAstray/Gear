using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions.Overlays;

namespace Gear.Tests
{
    /// <summary>GearTooltip 预热复用与最大宽配置的 EditMode 测试。</summary>
    public class GearTooltipTests
    {
        [TearDown]
        public void TearDown()
        {
            GearTooltip.Hide();
            GearTooltip.MaxWidth = 320f;
            GameObject canvas = GameObject.Find("GearOverlayCanvas");
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Prewarm_CreatesReusableViewWithConfiguredWidth()
        {
            GearTooltip.MaxWidth = 420f;
            GearTooltip.Prewarm();

            GearTooltipView[] views = Object.FindObjectsByType<GearTooltipView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, views.Length);
            RectTransform rect = (RectTransform)views[0].transform;
            Assert.AreEqual(420f, rect.sizeDelta.x, 0.01f);
        }

        [Test]
        public void ShowCustom_BuildsRichContent()
        {
            GearTooltip.ShowCustom(root =>
            {
                GameObject child = new GameObject("TooltipCustomChild", typeof(RectTransform));
                child.transform.SetParent(root, false);
            });

            Assert.IsNotNull(GameObject.Find("TooltipCustomChild"));
        }
    }
}
