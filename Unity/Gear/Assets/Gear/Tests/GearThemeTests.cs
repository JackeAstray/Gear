using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    /// <summary>
    /// 主题注册表 / 颜色目标的纯数据用例。
    /// </summary>
    public sealed class GearThemeTests
    {
        private ImageExThemeAsset theme;
        private ImageExThemeAsset original;

        [SetUp]
        public void SetUp()
        {
            original = ImageExThemeService.Current;
            theme = ScriptableObject.CreateInstance<ImageExThemeAsset>();
            theme.EditorSetColor("Text", Color.red);
            theme.EditorSetColor("Background", Color.green);
        }

        [TearDown]
        public void TearDown()
        {
            if (theme != null) Object.DestroyImmediate(theme);
            ImageExThemeService.SetTheme(original);
        }

        private sealed class RecordingTarget : IGearThemeTarget
        {
            public ImageExThemeAsset AppliedTheme;
            public float AppliedSeconds;
            public int Calls;

            public void ApplyTheme(ImageExThemeAsset targetTheme, float transitionSeconds)
            {
                AppliedTheme = targetTheme;
                AppliedSeconds = transitionSeconds;
                Calls++;
            }
        }

        [Test]
        public void Registry_BroadcastsToRegisteredTargetsAndUnregisters()
        {
            RecordingTarget target = new RecordingTarget();
            GearThemeRegistry.Register(target);
            GearThemeRegistry.Apply(theme, 0.2f);

            Assert.AreEqual(1, target.Calls);
            Assert.AreSame(theme, target.AppliedTheme);
            Assert.AreEqual(0.2f, target.AppliedSeconds, 0.0001f);

            GearThemeRegistry.Unregister(target);
            GearThemeRegistry.Apply(theme, 0.2f);
            Assert.AreEqual(1, target.Calls, "注销后不应再收到广播");
        }

        [Test]
        public void Registry_TracksRegisteredTargetCount()
        {
            int before = GearThemeRegistry.TargetCount;
            RecordingTarget target = new RecordingTarget();
            GearThemeRegistry.Register(target);
            Assert.AreEqual(before + 1, GearThemeRegistry.TargetCount);

            GearThemeRegistry.Unregister(target);
            Assert.AreEqual(before, GearThemeRegistry.TargetCount);
        }

        [Test]
        public void ColorTarget_AppliesTokenImmediately()
        {
            GameObject go = new GameObject("ThemeColorTarget", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            try
            {
                Image image = go.GetComponent<Image>();
                GearThemeColorTarget target = go.AddComponent<GearThemeColorTarget>();
                target.Configure("Text", image);
                target.ApplyTheme(theme, 0f);

                Assert.AreEqual(Color.red, image.color);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
