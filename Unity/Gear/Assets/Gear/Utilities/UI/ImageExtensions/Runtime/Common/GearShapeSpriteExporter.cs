#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 把 ImageEx 的圆/圆角矩形烘焙成可染色 Sprite；支持 1x–4x 与 9-slice border。
    /// </summary>
    public static class GearShapeSpriteExporter
    {
        [MenuItem("Tools/Gear/Export ImageEx Shape Sprite %#e")]
        private static void ExportSelected()
        {
            ImageEx image = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<ImageEx>()
                : null;
            if (image == null)
            {
                EditorUtility.DisplayDialog("Gear Shape Export", "请选中带 ImageEx 的物体。", "OK");
                return;
            }

            string path = Export(image, 2);
            if (!string.IsNullOrEmpty(path))
                EditorUtility.DisplayDialog("Gear Shape Export", "已导出：\n" + path, "OK");
        }

        /// <summary>烘焙并导入 Sprite；path 为空时写到 Assets/Gear/Generated/ShapeExports。</summary>
        public static string Export(ImageEx image, int scale, string path = null, bool nineSlice = true)
        {
            if (image == null) return null;
            scale = Mathf.Clamp(scale, 1, 4);

            Rect rect = image.rectTransform.rect;
            int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 4, 2048);
            int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 4, 2048);

            Texture2D texture = Bake(image, width, height, scale);
            if (path == null)
            {
                string directory = "Assets/Gear/Generated/ShapeExports";
                Directory.CreateDirectory(directory);
                path = directory + "/" + Sanitize(image.gameObject.name) + "_" + width + "x" + height + ".png";
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = nineSlice ? ResolveBorder(image, scale) : Vector4.zero;
                importer.SaveAndReimport();
            }

            return path;
        }

        private static Texture2D Bake(ImageEx image, int width, int height, int scale)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "GearShape_" + image.gameObject.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            bool circle = image.DrawShape == DrawShape.Circle;
            float radius = circle ? Mathf.Min(width, height) * 0.5f : ResolveRadius(image, scale);
            var pixels = new Color32[width * height];
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float distance;
                    if (circle)
                    {
                        distance = Mathf.Sqrt((px - halfW) * (px - halfW) + (py - halfH) * (py - halfH)) - radius;
                    }
                    else
                    {
                        float dx = Mathf.Abs(px - halfW) - (halfW - radius);
                        float dy = Mathf.Abs(py - halfH) - (halfH - radius);
                        float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                            + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                        distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                    }

                    float alpha = 1f - Mathf.SmoothStep(-1f, 1f, distance);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float ResolveRadius(ImageEx image, int scale)
        {
            if (image.Rectangle == null) return 0f;
            Vector4 corners = image.Rectangle.CornerRadius;
            float max = Mathf.Max(Mathf.Max(corners.x, corners.y), Mathf.Max(corners.z, corners.w));
            return Mathf.Max(0f, max * scale);
        }

        private static Vector4 ResolveBorder(ImageEx image, int scale)
        {
            float radius = ResolveRadius(image, scale);
            return new Vector4(radius, radius, radius, radius);
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return string.IsNullOrEmpty(value) ? "Shape" : value;
        }
    }
}
#endif
