using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>GearThemeBinder 的字体/Sprite/渐变 token 绑定测试。</summary>
    public class GearThemeBinderTests
    {
        private ImageExThemeAsset theme;

        [SetUp]
        public void SetUp()
        {
            theme = ScriptableObject.CreateInstance<ImageExThemeAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (theme != null)
                Object.DestroyImmediate(theme);
        }

        [Test]
        public void SpriteToken_AppliesToImage()
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            theme.EditorSetSprite("Icon", sprite);

            GameObject go = new GameObject("ImageTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GearThemeBinder binder = go.AddComponent<GearThemeBinder>();
            binder.AddBinding("Icon", GearThemeBinder.Target.Sprite);
            binder.Apply(theme);

            Assert.AreEqual(sprite, go.GetComponent<Image>().sprite);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void GradientToken_EnablesImageExGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            theme.EditorSetGradient("AccentGradient", gradient);

            GameObject go = new GameObject("ImageExTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            GearThemeBinder binder = go.AddComponent<GearThemeBinder>();
            binder.AddBinding("AccentGradient", GearThemeBinder.Target.Gradient);
            binder.Apply(theme);

            GradientEffect effect = go.GetComponent<ImageEx>().GradientEffect;
            Assert.IsTrue(effect.Enabled);
            Assert.AreEqual(gradient, effect.Gradient);

            Object.DestroyImmediate(go);
        }
    }
}
