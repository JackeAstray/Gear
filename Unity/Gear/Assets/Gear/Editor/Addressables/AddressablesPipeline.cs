using System;
using System.IO;
using System.Text;
using Gear.Common;
using UnityEditor;
using UnityEngine;

namespace Gear.EditorTools.Addressables
{
    /// <summary>
    /// Addressables 一键流水线：配置 → 迁移（UI/音频/图片）→ 构建 Content（version.json 含 catalogHash/remoteCatalogFolder）
    /// → 上传 OSS（含远程 catalog_*.bin/.hash，增量）。
    ///
    /// 两种触发方式：
    /// 1. 编辑器菜单：Gear → Addressables → 一键流水线（迁移+构建+上传 OSS）
    /// 2. 批处理自动化（CI / 命令行）：
    ///    Unity.exe -batchmode -projectPath &lt;项目路径&gt; -executeMethod Gear.EditorTools.Addressables.AddressablesPipeline.RunPipeline -quit -logFile Logs/addressables_pipeline.log
    ///
    /// 目标平台固定 WebGL（CDN 部署平台）。批处理下全程无弹窗；结果写入
    /// Build/Addressables/pipeline_result.txt，便于退出后核对。
    /// 注意：OSS 上传依赖本机 EditorPrefs 中的凭据（RM.OSS.*）；未配置时跳过上传、不阻断构建。
    /// </summary>
    public static class AddressablesPipeline
    {
        /// <summary>流水线结果输出文件（相对项目根）</summary>
        private const string ResultFile = "Build/Addressables/pipeline_result.txt";

        /// <summary>未安装包或未启用 Addressables 时灰掉流水线菜单。</summary>
        [MenuItem("Gear/Addressables/一键流水线（迁移+构建+上传 OSS）", true)]
        private static bool PipelineMenuValidate() => AddressablesSetup.CanUseAddressables;

        /// <summary>编辑器菜单入口（目标平台 WebGL）</summary>
        [MenuItem("Gear/Addressables/一键流水线（迁移+构建+上传 OSS）", false, 405)]
        public static void RunPipelineMenu()
        {
            RunPipeline(BuildTarget.WebGL);
        }

        /// <summary>批处理入口（-executeMethod）：默认 WebGL 平台（CDN 部署平台）</summary>
        public static void RunPipeline()
        {
            RunPipeline(BuildTarget.WebGL);
        }

        /// <summary>执行完整流水线（幂等：迁移跳过已存在，上传增量对比）</summary>
        private static void RunPipeline(BuildTarget target)
        {
            var sb = new StringBuilder();
            bool ok = false;
            sb.AppendLine("===== Addressables 一键流水线开始 =====");
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 目标平台: {target}");

            try
            {
                // 0. 确保配置（分组 / Label / Profile / 激活 DevLocal）
                if (!AddressablesSetup.EnsureSetup())
                {
                    throw new Exception("Addressables 配置初始化失败");
                }

                // 发布流水线必须显式切到 Publish：EnsureSetup 会把活动 Profile 固定回 DevLocal，
                // 不切就会把 http://localhost:8080 烘进 catalog，上传后所有客户端都指向本机。
                if (!AddressablesSetup.TrySetActiveProfile("Publish", out string profileError))
                {
                    throw new Exception("切换发布 Profile 失败: " + profileError);
                }
                sb.AppendLine("[1/4] 配置就绪（分组 / Label / Profile=Publish）");

                // 1. 资源迁移（统一入口，幂等：目标已存在则跳过复制，只补齐标记）
                AddressablesMigrator.MigrateAll();
                sb.AppendLine("[2/4] 资源迁移完成（UI + 音频 + 图片）");

                // 2. 构建 Content（远程 Catalog 自动开启；version.json 含 catalogHash / remoteCatalogFolder）
                if (!AddressablesBuildWindow.BuildContentForTargetBatch(target, out string buildError))
                {
                    throw new Exception("构建 Content 失败: " + buildError);
                }
                sb.AppendLine($"[3/4] 构建完成（{target}），version.json 已更新");

                // 3. 上传 OSS（增量；含 catalog_*.bin/.hash）
                bool uploaded = AddressablesCdnUploader.UploadToOSSBatch(out string uploadMessage);
                if (uploaded)
                {
                    sb.AppendLine("[4/4] OSS 上传成功：" + uploadMessage);
                }
                else if (!AddressablesCdnUploader.IsConfigured)
                {
                    // 未配置凭据是"跳过"，不是失败：CI 在没配 OSS 时仍应能只做构建。
                    sb.AppendLine("[4/4] OSS 未配置，跳过上传（构建产物已生成）：" + uploadMessage);
                }
                else
                {
                    // 配了凭据但上传失败：必须让批处理退出码非 0，否则 CI 绿而 CDN 没更新。
                    throw new Exception("OSS 上传失败: " + uploadMessage);
                }

                ok = true;
                sb.AppendLine("===== 流水线结束（成功） =====");
                Log.Debug(sb.ToString(), channel: LogChannel.Resource);
            }
            catch (Exception ex)
            {
                sb.AppendLine("===== 流水线失败 =====");
                sb.AppendLine(ex.ToString());
                Log.Error(sb.ToString(), channel: LogChannel.Resource);
            }

            // 结果落盘，便于批处理退出后核对
            try
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "Addressables");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "pipeline_result.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Warning("[AddressablesPipeline] 写入结果文件失败: " + ex.Message, channel: LogChannel.Resource);
            }

            // 批处理没有"人工看日志"的机会：退出码是 CI 唯一能看到的信号。
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
