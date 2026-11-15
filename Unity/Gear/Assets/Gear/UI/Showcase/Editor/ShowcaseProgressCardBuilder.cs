using Gear.UI.Showcase;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.UI.Showcase.Editor
{
    /// <summary>一次性补 M2.1 ProgressButton 展卡；生成后卡就是普通 prefab。</summary>
    public static class ShowcaseProgressCardBuilder
    {
        private const string Source = "Assets/Gear/UI/Showcase/Cards/overlays/098-覆盖层---Popover.prefab";
        private const string Target = "Assets/Gear/UI/Showcase/Cards/widgets/158-控件---ProgressButton.prefab";
        private const string CatalogPath = "Assets/Gear/Resources/UI/Showcase/ShowcaseCatalog.asset";

        [MenuItem("Gear/UI 展厅/补 ProgressButton 展卡（一次性）", false, 130)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Target) == null)
                AssetDatabase.CopyAsset(Source, Target);

            var root = PrefabUtility.LoadPrefabContents(Target);
            if (root == null)
            {
                Debug.LogError("[ShowcaseProgressCardBuilder] 无法载入 " + Target);
                return;
            }

            root.name = "158-控件---ProgressButton";
            var title = root.transform.Find("CardTitle")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "控件 · ProgressButton";
            var desc = root.transform.Find("CardDescription")?.GetComponent<TMP_Text>();
            if (desc != null) desc.text = "按住 1.2s 完成；松手回退，完成触发 Toast。";

            var buttonGo = root.transform.Find("PreviewClip/OpenPopover");
            if (buttonGo == null)
            {
                Debug.LogError("[ShowcaseProgressCardBuilder] 找不到按钮节点");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            var oldTrigger = buttonGo.GetComponent<Gear.Utilities.UI.ImageExtensions.Overlays.GearPopoverTrigger>();
            if (oldTrigger != null) Object.DestroyImmediate(oldTrigger);
            var oldButton = buttonGo.GetComponent<Button>();
            var graphic = oldButton != null ? oldButton.targetGraphic : buttonGo.GetComponent<Graphic>();
            if (oldButton != null) Object.DestroyImmediate(oldButton);

            var progress = buttonGo.GetComponent<GearProgressButton>();
            if (progress == null) progress = buttonGo.gameObject.AddComponent<GearProgressButton>();
            progress.targetGraphic = graphic;
            progress.transition = Selectable.Transition.None;
            progress.HoldDuration = 1.2f;
            progress.ResetDuration = 0.25f;

            var image = graphic as ImageEx;
            if (image != null) image.color = new Color(0.486f, 0.361f, 0.988f, 1f);

            var label = buttonGo.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Hold 1.2s";
            var demo = root.GetComponent<ShowcaseProgressDemo>();
            if (demo == null) demo = root.AddComponent<ShowcaseProgressDemo>();
            demo.Configure(progress, label);

            PrefabUtility.SaveAsPrefabAsset(root, Target);
            PrefabUtility.UnloadPrefabContents(root);

            var catalog = AssetDatabase.LoadAssetAtPath<ShowcaseCatalog>(CatalogPath);
            var page = catalog != null ? catalog.FindPage("widgets") : null;
            if (page != null)
            {
                bool exists = false;
                for (int i = 0; i < page.entries.Count; i++)
                {
                    if (page.entries[i].id == "widgets/extras-progress") exists = true;
                }

                if (!exists)
                {
                    catalog.EditorAddEntry("widgets", new ShowcaseEntry
                    {
                        id = "widgets/extras-progress",
                        title = "控件 · ProgressButton",
                        description = "按住 1.2s 完成；松手回退，完成触发 Toast。",
                        pageId = "widgets",
                        tags = new[] { "控件", "ProgressButton" },
                        cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Target),
                    });
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                }
            }

            Debug.Log("[ShowcaseProgressCardBuilder] 已生成 " + Target);
        }
    }
}
