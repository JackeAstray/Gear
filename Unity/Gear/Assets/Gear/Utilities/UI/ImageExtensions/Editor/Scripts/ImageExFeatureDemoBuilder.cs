using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 删除之前生成的 ImageEx 示例卡片（保留场景里原有的内容）。
    /// <para>
    /// 只按"示例卡片名"精确匹配删除，走 Undo，可 Ctrl+Z；不再提供生成入口，
    /// 避免演示参数与 Evo 的完成度混淆。
    /// </para>
    /// </summary>
    internal static class ImageExDemoCleanup
    {
        private static readonly HashSet<string> DemoObjectNames = new HashSet<string>
        {
            "ImageEx 新特性示例",
            "玻璃组合卡片",
            "内阴影卡片",
            "内阴影",
            "单 quad 轮廓阴影",
            "轮廓阴影",
            "进度裁剪 65%",
            "进度裁剪 50%",
            "渐变旋转（运行时）",
            "渐变旋转",
            "虚线描边流动（运行时）",
            "虚线描边",
            "过渡循环（运行时）",
            "过渡循环",
            "背景模糊",
        };

        [MenuItem("Gear/UI 示例/删除 ImageEx 示例卡片", false, 60)]
        private static void RemoveDemoCards()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<GameObject> targets = new List<GameObject>();
            foreach (GameObject go in all)
            {
                if (go != null && DemoObjectNames.Contains(go.name)) targets.Add(go);
            }

            if (targets.Count == 0)
            {
                Debug.Log("[ImageEx 示例] 场景里没有找到示例卡片。");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("删除 ImageEx 示例卡片");

            foreach (GameObject go in targets)
            {
                if (go != null) Undo.DestroyObjectImmediate(go);
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[ImageEx 示例] 已删除 {targets.Count} 个示例对象（可 Ctrl+Z 撤销）。");
        }
    }
}
