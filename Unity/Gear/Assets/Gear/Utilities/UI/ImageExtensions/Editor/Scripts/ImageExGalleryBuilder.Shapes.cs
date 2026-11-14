using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gear.EditorTools.ImageExtensions
{
    internal static partial class ImageExGalleryBuilder
    {
        /// <summary>
        /// 重建设置形状卡（形状 · *）：删除旧卡后按当前预设重建，用于批量改预设时避免全量重建。
        /// </summary>
        [MenuItem("Gear/UI Example/重建形状卡（形状 · *）", false, 42)]
        public static void RebuildShapeCards()
        {
            const string scenePath = "Assets/Gear/Scenes/Example/UI Example.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogError("[ImageEx] 找不到 UI Example 场景：" + scenePath);
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path != scenePath)
            {
                active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            RectTransform content = FindContent();
            if (content == null)
            {
                Debug.LogError("[ImageEx] UI Example 场景里找不到 Scroll View/Viewport/Content");
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child.name.StartsWith("形状 ·")) Undo.DestroyObjectImmediate(child.gameObject);
            }

            ImageExGalleryCatalog.AddShapes(content);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log("[ImageEx] 已重建形状卡");
        }

    }
}
