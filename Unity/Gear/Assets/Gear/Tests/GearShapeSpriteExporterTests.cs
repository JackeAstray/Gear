using System.IO;
using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearShapeSpriteExporterTests
    {
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ShapeExportTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            var image = go.GetComponent<ImageEx>();
            image.rectTransform.sizeDelta = new Vector2(64f, 32f);
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg { CornerRadius = new Vector4(8f, 8f, 8f, 8f) };
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Export_WritesTintableSprite()
        {
            ImageEx image = go.GetComponent<ImageEx>();
            string path = GearShapeSpriteExporter.Export(image, 2);
            try
            {
                Assert.IsTrue(File.Exists(path), path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite);
                Assert.AreEqual(128, sprite.texture.width);
                Assert.AreEqual(64, sprite.texture.height);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
