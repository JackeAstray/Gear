using UnityEditor;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 材质池试点工具：把选中层级里的 ImageEx 切到 <see cref="MaterialMode.Pooled"/> 或还原 Dynamic。
    /// <para>
    /// 只处理"设置稳定"的 ImageEx：带过渡循环 / 渐变旋转 / 指针辉光的实例会逐帧改 uniform，
    /// 切 Pooled 会不断产生新签名，这里直接跳过并给出提示。
    /// </para>
    /// </summary>
    internal static class ImageExPooledTool
    {
        [MenuItem("Gear/ImageEx 材质池/选中切 Pooled（同设置共享）", false, 20)]
        private static void ApplyPooled()
        {
            ImageEx[] images = Selection.GetFiltered<ImageEx>(SelectionMode.Editable | SelectionMode.Deep);
            int applied = 0;
            int skipped = 0;

            foreach (ImageEx image in images)
            {
                if (image == null) continue;
                if (HasAnimatedUniforms(image))
                {
                    skipped++;
                    continue;
                }

                Undo.RecordObject(image, "ImageEx Pooled");
                image.MaterialMode = MaterialMode.Pooled;
                UnityEditor.EditorUtility.SetDirty(image);
                applied++;
            }

            LogResult("切 Pooled", applied, skipped);
        }

        [MenuItem("Gear/ImageEx 材质池/选中还原 Dynamic", false, 21)]
        private static void ApplyDynamic()
        {
            ImageEx[] images = Selection.GetFiltered<ImageEx>(SelectionMode.Editable | SelectionMode.Deep);
            int applied = 0;

            foreach (ImageEx image in images)
            {
                if (image == null) continue;
                Undo.RecordObject(image, "ImageEx Dynamic");
                image.MaterialMode = MaterialMode.Dynamic;
                UnityEditor.EditorUtility.SetDirty(image);
                applied++;
            }

            Debug.Log($"[ImageEx] 已把 {applied} 个实例还原为 Dynamic。");
        }

        /// <summary>逐帧改 uniform 的组件：切 Pooled 会让签名不断变化，跳过。</summary>
        private static bool HasAnimatedUniforms(ImageEx image)
        {
            return image.GetComponent<ImageExTransitionLooper>() != null
                || image.GetComponent<ImageExGradientSpinner>() != null
                || image.GetComponent<ImageExPointerGlow>() != null;
        }

        private static void LogResult(string action, int applied, int skipped)
        {
            if (applied == 0 && skipped == 0)
            {
                Debug.Log("[ImageEx] 选中层级里没有 ImageEx。");
                return;
            }

            Debug.Log($"[ImageEx] {action}：{applied} 个完成"
                + (skipped > 0 ? $"，跳过 {skipped} 个（含过渡循环/渐变旋转/指针辉光，逐帧改 uniform）。" : "。"));
        }
    }
}
