using Gear.UI.Showcase;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase.Editor
{
    /// <summary>一次性补 M2.2 ColorWheel 展卡；生成后卡是普通 prefab。</summary>
    public static class ShowcaseColorWheelCardBuilder
    {
        private const string Source = "Assets/Gear/UI/Showcase/Cards/overlays/098-覆盖层---Popover.prefab";
        private const string Target = "Assets/Gear/UI/Showcase/Cards/widgets/159-控件---ColorWheel.prefab";
        private const string CatalogPath = "Assets/Gear/Resources/UI/Showcase/ShowcaseCatalog.asset";

        [MenuItem("Gear/UI 展厅/补 ColorWheel 展卡（一次性）", false, 131)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Target) == null)
                AssetDatabase.CopyAsset(Source, Target);

            var root = PrefabUtility.LoadPrefabContents(Target);
            if (root == null)
            {
                Debug.LogError("[ShowcaseColorWheelCardBuilder] 无法载入 " + Target);
                return;
            }

            root.name = "159-控件---ColorWheel";
            var title = root.transform.Find("CardTitle")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "控件 · ColorWheel";
            var desc = root.transform.Find("CardDescription")?.GetComponent<TMP_Text>();
            if (desc != null) desc.text = "程序化 HSV 色轮：角度取 Hue，半径取 Saturation；亮度可独立调。";

            var button = root.transform.Find("PreviewClip/OpenPopover");
            if (button != null) Object.DestroyImmediate(button.gameObject);

            var preview = root.transform.Find("PreviewClip/Preview") as RectTransform;
            if (preview != null)
            {
                preview.sizeDelta = new Vector2(236f, 236f);
                var image = preview.GetComponent<ImageEx>();
                if (image != null) image.color = Color.white;

                var wheel = root.GetComponent<GearColorWheel>();
                if (wheel == null) wheel = root.AddComponent<GearColorWheel>();
                wheel.Area = preview;
                wheel.Preview = preview.GetComponent<Graphic>();
                wheel.TextureSize = 256;
            }

            PrefabUtility.SaveAsPrefabAsset(root, Target);
            PrefabUtility.UnloadPrefabContents(root);

            var catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(CatalogPath);
            var page = catalog != null ? catalog.FindPage("widgets") : null;
            if (page != null)
            {
                bool exists = false;
                for (int i = 0; i < page.entries.Count; i++)
                {
                    if (page.entries[i].id == "widgets/extras-colorwheel") exists = true;
                }

                if (!exists)
                {
                    catalog.EditorAddEntry("widgets", new ShowcaseEntry
                    {
                        id = "widgets/extras-colorwheel",
                        title = "控件 · ColorWheel",
                        description = "程序化 HSV 色轮：角度取 Hue，半径取 Saturation；亮度可独立调。",
                        pageId = "widgets",
                        tags = new[] { "控件", "ColorWheel" },
                        cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Target),
                    });
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                }
            }

            Debug.Log("[ShowcaseColorWheelCardBuilder] 已生成 " + Target);
        }
    }
}
