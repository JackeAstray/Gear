using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// UI 渲染诊断：统计当前场景的 Canvas / Graphic / ImageEx 材质实例与可合批分组，
    /// 输出 Markdown 报告，给“是否值得做 Style Page”提供依据。
    /// </summary>
    internal static class GearUIRenderDiagnostics
    {
        private const string ReportPath = "../../../Docs/design/Gear-UI-渲染诊断.md";

        [MenuItem("Gear/UI Example/UI 渲染诊断（材质/Canvas）", false, 60)]
        public static void Run()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ImageEx[] images = Object.FindObjectsByType<ImageEx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Graphic[] graphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int dynamicCount = 0, sharedCount = 0;
            HashSet<int> imageMaterials = new HashSet<int>();
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].MaterialMode == MaterialMode.Shared) sharedCount++;
                else dynamicCount++;
                if (images[i].material != null) imageMaterials.Add(images[i].material.GetInstanceID());
            }

            List<List<ImageEx>> groups = ImageExBatchingTools.GroupBySettings(images);
            int shareableGroups = 0, shareableInstances = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Count < 2) continue;
                shareableGroups++;
                shareableInstances += groups[i].Count;
            }

            int projectedMaterialInstances = images.Length - shareableInstances + shareableGroups;

            StringBuilder report = new StringBuilder();
            report.AppendLine("# Gear UI 渲染诊断");
            report.AppendLine();
            report.AppendLine($"场景：`{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}`");
            report.AppendLine();
            report.AppendLine("| 指标 | 数值 |");
            report.AppendLine("|---|---|");
            report.AppendLine($"| Canvas | {canvases.Length} |");
            report.AppendLine($"| Graphic | {graphics.Length} |");
            report.AppendLine($"| ImageEx | {images.Length} |");
            report.AppendLine($"| ImageEx Dynamic / Shared | {dynamicCount} / {sharedCount} |");
            report.AppendLine($"| ImageEx 材质实例 | {imageMaterials.Count} |");
            report.AppendLine($"| 可合批组（≥2） | {shareableGroups} 组 / {shareableInstances} 实例 |");
            report.AppendLine($"| 共享材质后预计材质实例 | {projectedMaterialInstances} |");
            report.AppendLine();

            report.AppendLine("## Canvas 材质分组（估算）");
            report.AppendLine();
            report.AppendLine("| Canvas | Graphic | 唯一材质 | 估算批次组 |");
            report.AppendLine("|---|---:|---:|---:|");
            for (int i = 0; i < canvases.Length; i++)
            {
                Graphic[] canvasGraphics = canvases[i].GetComponentsInChildren<Graphic>(true);
                HashSet<int> materials = new HashSet<int>();
                HashSet<long> groupsKey = new HashSet<long>();
                for (int g = 0; g < canvasGraphics.Length; g++)
                {
                    Graphic graphic = canvasGraphics[g];
                    if (graphic == null) continue;
                    Material material = graphic.material;
                    Texture texture = graphic.mainTexture;
                    int materialId = material != null ? material.GetInstanceID() : 0;
                    int textureId = texture != null ? texture.GetInstanceID() : 0;
                    materials.Add(materialId);
                    groupsKey.Add(((long)materialId << 32) ^ (uint)textureId);
                }

                report.AppendLine($"| {canvases[i].name} | {canvasGraphics.Length} | {materials.Count} | {groupsKey.Count} |");
            }

            report.AppendLine();
            report.AppendLine("## 可合批分组（≥2）");
            report.AppendLine();
            int shown = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Count < 2) continue;
                if (shown++ >= 10) break;
                report.AppendLine($"- {groups[i].Count} 个实例 · 代表：`{groups[i][0].name}`");
            }

            report.AppendLine();
            report.AppendLine("## 结论");
            report.AppendLine();
            report.AppendLine($"- 本次 ImageEx 材质实例：{images.Length} → 预计 {projectedMaterialInstances}（按可合批组估算，不是实际 draw call）。");
            report.AppendLine("- 若「可合批组」很少，优先做材质/样式微调，不要上 Style Page。");
            report.AppendLine("- 若同一 Canvas 的唯一材质接近 Graphic 数，说明每实例 Dynamic 材质正在拆批，值得先做共享材质。");
            report.AppendLine("- Style Page 是架构级改造；只有 draw call 实测成为瓶颈时才排期。");
            report.AppendLine();
            report.AppendLine("## 建议下一步");
            report.AppendLine();
            report.AppendLine("1. 先选可合批组中最大的 1–2 组做共享材质试点。");
            report.AppendLine("2. 记录试点前后的 Batches / SetPass Calls。");
            report.AppendLine("3. 若收益明显，再批量应用到同设置组；不要直接改 Style Page。");

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, ReportPath));
            File.WriteAllText(path, report.ToString());

            Debug.Log($"[UI 诊断] Canvas={canvases.Length}, Graphic={graphics.Length}, ImageEx={images.Length}, " +
                      $"Dynamic={dynamicCount}, Shared={sharedCount}, 可合批组={shareableGroups}, 报告={path}");
            AssetDatabase.Refresh();
        }
    }
}
