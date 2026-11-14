using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 共享材质受控试点：只对当前场景最大的一个“同设置组”切 Shared，支持一键还原。
    /// <para>
    /// 目的：在动 Style Page 之前，用最小风险验证“共享材质是否真的减少材质实例”，
    /// 不修改不同设置组，也不改 shader 架构。
    /// </para>
    /// </summary>
    internal static class ImageExSharedPilot
    {
        private const string PilotFolder = "Assets/Gear/Resources/Materials/UI/ImageExSharedPilot";

        [MenuItem("Gear/UI Example/共享材质试点 - 应用最大同设置组", false, 61)]
        public static void ApplyLargestGroup()
        {
            ImageEx[] images = Object.FindObjectsByType<ImageEx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<List<ImageEx>> groups = ImageExBatchingTools.GroupBySettings(images);

            List<ImageEx> target = null;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Count < 2) continue;
                if (target == null || groups[i].Count > target.Count) target = groups[i];
            }

            if (target == null)
            {
                Debug.LogWarning("[共享材质试点] 没有找到可合批组。");
                return;
            }

            int before = CountMaterials(images);
            int applied = ImageExBatchingTools.ApplySharedMaterials(target, PilotFolder, out int groupCount);
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            int after = CountMaterials(Object.FindObjectsByType<ImageEx>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Debug.Log($"[共享材质试点] 组={groupCount}, 实例={applied}, 材质实例 {before}→{after}, " +
                      $"代表={target[0].name}, 目录={PilotFolder}");

            GearUIRenderDiagnostics.Run();
        }

        [MenuItem("Gear/UI Example/共享材质试点 - 还原", false, 62)]
        public static void Revert()
        {
            ImageEx[] images = Object.FindObjectsByType<ImageEx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<ImageEx> pilot = new List<ImageEx>();
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].material == null) continue;
                string path = AssetDatabase.GetAssetPath(images[i].material);
                if (!string.IsNullOrEmpty(path) && path.StartsWith(PilotFolder, System.StringComparison.Ordinal))
                    pilot.Add(images[i]);
            }

            if (pilot.Count == 0)
            {
                Debug.Log("[共享材质试点] 没有找到试点实例，可能已经还原。");
                return;
            }

            ImageExBatchingTools.RestoreDynamic(pilot);
            ImageExBatchingTools.DeleteGeneratedMaterials(PilotFolder);
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[共享材质试点] 已还原 {pilot.Count} 个实例，并删除试点材质。");
            GearUIRenderDiagnostics.Run();
        }

        private static int CountMaterials(ImageEx[] images)
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].material != null) ids.Add(images[i].material.GetInstanceID());
            }
            return ids.Count;
        }
    }
}
