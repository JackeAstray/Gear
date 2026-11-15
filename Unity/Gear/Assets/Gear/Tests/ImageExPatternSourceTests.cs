using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class ImageExPatternSourceTests
    {
        private GameObject go;
        private ImageEx image;
        private Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Pattern", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            image = go.GetComponent<ImageEx>();
            image.rectTransform.sizeDelta = new Vector2(200f, 100f);
            texture = new Texture2D(8, 8);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        [Test]
        public void SpriteSource_WritesTextureAndMode()
        {
            image.Pattern = ImageEx.PatternMode.Dots;
            image.PatternSourceType = ImageEx.PatternSource.Sprite;
            image.PatternTexture = texture;

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1f, modified.GetFloat("_PatternSource"), 0.001f);
                Assert.AreSame(texture, modified.GetTexture("_PatternTex"));
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void ShapeSource_WritesShapeAndGradientMode()
        {
            image.Pattern = ImageEx.PatternMode.Dots;
            image.PatternSourceType = ImageEx.PatternSource.Shape;
            image.PatternShape = DrawShape.Rectangle;
            image.PatternColorType = ImageEx.PatternColorMode.Gradient;
            image.PatternGradient = texture;

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = image.GetModifiedMaterial(baseMat);
                Assert.AreEqual(2f, modified.GetFloat("_PatternSource"), 0.001f);
                Assert.AreEqual((float)DrawShape.Rectangle, modified.GetFloat("_PatternShape"), 0.001f);
                Assert.AreEqual(1f, modified.GetFloat("_PatternColorMode"), 0.001f);
                Assert.AreSame(texture, modified.GetTexture("_PatternGradient"));
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }
    }
}
