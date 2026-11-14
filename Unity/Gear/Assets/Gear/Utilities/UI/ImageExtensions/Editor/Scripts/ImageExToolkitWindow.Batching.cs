using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    public sealed partial class ImageExToolkitWindow
    {
        private void DrawStats(List<ImageEx> selected)
        {
            int dynamic = 0, shared = 0;
            int gradientOn = 0, transitionOn = 0, edgeOn = 0, shadowOn = 0, blurOn = 0, toneOn = 0, targetOn = 0, detailOn = 0, dashedOn = 0;
            Dictionary<DrawShape, int> shapes = new Dictionary<DrawShape, int>();

            foreach (ImageEx item in selected)
            {
                if (item.MaterialMode == MaterialMode.Shared) shared++;
                else dynamic++;

                if (item.GradientEffect.Enabled) gradientOn++;
                if (item.Transition != ImageEx.TransitionMode.None) transitionOn++;
                if (item.Edge != ImageEx.EdgeMode.None) edgeOn++;
                if (item.AppendShadow) shadowOn++;
                if (item.Blur != ImageEx.BlurType.None) blurOn++;
                if (item.Tone != ImageEx.ToneFilter.None) toneOn++;
                if (item.Target != ImageEx.TargetMode.None) targetOn++;
                if (item.Detail != ImageEx.DetailFilter.None) detailOn++;
                if (item.EnableDashedOutline != 0) dashedOn++;

                shapes.TryGetValue(item.DrawShape, out int count);
                shapes[item.DrawShape] = count + 1;
            }

            EditorGUILayout.LabelField("规模", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("选中 ImageEx", selected.Count.ToString());
            EditorGUILayout.LabelField("材质模式", $"Dynamic {dynamic} / Shared {shared}");

            int estimatedSegments = dynamic + shared;
            EditorGUILayout.LabelField("材质实例估算", estimatedSegments.ToString() + "（粗略下限）");

            if (dynamic > 6)
            {
                EditorGUILayout.HelpBox(
                    $"有 {dynamic} 个实例走 Dynamic（每实例一份材质）。同款样式的按钮/卡片如果外观完全一致，" +
                    "可以改 Shared 共用一份材质来合批；外观不一致时不要强改，Shared 模式会被最后一个写入者覆盖。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("当前实例数量下，材质模式对 draw call 的影响有限。", MessageType.None);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("效果使用", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("渐变", gradientOn.ToString());
            EditorGUILayout.LabelField("过渡", transitionOn.ToString());
            EditorGUILayout.LabelField("边缘", edgeOn.ToString());
            EditorGUILayout.LabelField("阴影", shadowOn.ToString());
            EditorGUILayout.LabelField("模糊", blurOn.ToString());
            EditorGUILayout.LabelField("色调", toneOn.ToString());
            EditorGUILayout.LabelField("目标高亮", targetOn.ToString());
            EditorGUILayout.LabelField("细节纹理", detailOn.ToString());
            EditorGUILayout.LabelField("虚线描边", dashedOn.ToString());

            if (transitionOn > 0 && gradientOn > 0)
            {
                EditorGUILayout.HelpBox(
                    "过渡与渐变的组合最贵（着色器会走完整程序）。全屏卡片建议单独 Canvas，或只在焦点元素上开。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("形状分布", EditorStyles.boldLabel);
            foreach (KeyValuePair<DrawShape, int> pair in shapes)
            {
                EditorGUILayout.LabelField(pair.Key.ToString(), pair.Value.ToString());
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("对象列表（前 40 个）", EditorStyles.boldLabel);
            int shown = 0;
            foreach (ImageEx item in selected)
            {
                if (shown++ >= 40) break;
                GUILayout.Label($"{item.name}  ·  {item.DrawShape}  ·  {item.MaterialMode}", EditorStyles.miniLabel);
            }
        }

        // ============================================================
        // 合批（共享材质）
        // ============================================================

        private void DrawBatchingTab()
        {
            EditorGUILayout.HelpBox(
                "把「材质设置完全一致」的多个 ImageEx 分到一组并共用一份材质，让它们在同一 Canvas 内合批。\n" +
                "只有组内 ≥2 个实例时才有收益；Sprite 不同不影响分组，但不同贴图本身仍然不会合批。\n\n" +
                "注意：分组后同一组相当于一个「样式单元」——再单独改其中某个实例的特效参数，会写进共享材质并影响整组；" +
                "要单独调参请先「还原为 Dynamic」。",
                MessageType.Info);

            materialFolder = EditorGUILayout.TextField("材质目录", materialFolder);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("分析选中")) AnalyzeBatching();
                if (GUILayout.Button("分析整个场景")) AnalyzeSceneBatching();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成并分配共享材质")) ApplyBatching();
                if (GUILayout.Button("还原为 Dynamic")) RestoreBatching();
            }

            using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(materialFolder)))
            {
                if (GUILayout.Button("删除本工具生成的材质资产")) DeleteGeneratedMaterials();
            }

            if (batchingGroups == null)
            {
                EditorGUILayout.HelpBox("先选中若干 ImageEx，再点「分析选中」。", MessageType.None);
                return;
            }

            int total = 0, shareable = 0, shareableGroups = 0;
            foreach (List<ImageEx> group in batchingGroups)
            {
                total += group.Count;
                if (group.Count >= 2)
                {
                    shareable += group.Count;
                    shareableGroups++;
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("分析结果" + (batchingFromScene ? "（整个场景）" : "（当前选中）"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(batchingFromScene ? "场景实例" : "选中实例", total.ToString());
            EditorGUILayout.LabelField("可合批组", $"{shareableGroups} 组，共 {shareable} 个实例");
            EditorGUILayout.LabelField("材质实例估算", $"当前 {total} → 分组后 {shareableGroups + (total - shareable)}");

            foreach (List<ImageEx> group in batchingGroups)
            {
                if (group.Count < 2) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"{group.Count} 个实例 · 代表：{group[0].name}", EditorStyles.boldLabel);
                    int shown = 0;
                    foreach (ImageEx image in group)
                    {
                        if (shown++ >= 8) break;
                        GUILayout.Label("· " + image.name, EditorStyles.miniLabel);
                    }
                }
            }
        }

        private void AnalyzeBatching()
        {
            batchingFromScene = false;
            batchingGroups = ImageExBatchingTools.GroupBySettings(GetSelectedImageEx());
            Repaint();
        }

        private void AnalyzeSceneBatching()
        {
            batchingFromScene = true;
            batchingGroups = ImageExBatchingTools.GroupBySettings(GetAllSceneImageEx());
            Repaint();
        }

        private static List<ImageEx> GetAllSceneImageEx()
        {
            ImageEx[] all = UnityEngine.Object.FindObjectsByType<ImageEx>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return new List<ImageEx>(all);
        }

        private void ApplyBatching()
        {
            List<ImageEx> scope = batchingFromScene ? GetAllSceneImageEx() : GetSelectedImageEx();
            if (scope.Count == 0)
            {
                ShowNotification(new GUIContent("没有可处理的 ImageEx"));
                return;
            }

            if (batchingFromScene && !UnityEditor.EditorUtility.DisplayDialog(
                    "全场景合批",
                    $"将对场景内 {scope.Count} 个 ImageEx 按设置分组并分配共享材质，是否继续？",
                    "继续", "取消")) return;

            int applied = ImageExBatchingTools.ApplySharedMaterials(scope, materialFolder, out int groups);
            if (batchingFromScene) AnalyzeSceneBatching();
            else AnalyzeBatching();
            ShowNotification(new GUIContent($"已分配 {applied} 个实例，{groups} 组共享材质"));
        }

        private void RestoreBatching()
        {
            List<ImageEx> scope = batchingFromScene ? GetAllSceneImageEx() : GetSelectedImageEx();
            if (scope.Count == 0)
            {
                ShowNotification(new GUIContent("没有可处理的 ImageEx"));
                return;
            }

            ImageExBatchingTools.RestoreDynamic(scope);
            if (batchingFromScene) AnalyzeSceneBatching();
            else AnalyzeBatching();
            ShowNotification(new GUIContent($"已还原 {scope.Count} 个实例为 Dynamic"));
        }

        private void DeleteGeneratedMaterials()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "删除生成材质",
                    "将删除目录下所有以 ImageExShared_ 开头的材质资产；仍引用它们的实例会回退为默认材质外观。",
                    "删除", "取消")) return;

            int deleted = ImageExBatchingTools.DeleteGeneratedMaterials(materialFolder);
            AnalyzeBatching();
            ShowNotification(new GUIContent($"已删除 {deleted} 个生成材质"));
        }

        // ============================================================
        // 公共工具
        // ============================================================

        private static List<ImageEx> GetSelectedImageEx()
        {
            // 每次调用都重新解析，避免 Selection 变化后读到旧缓存
            List<ImageEx> result = new List<ImageEx>(16);
            foreach (GameObject go in Selection.gameObjects)
            {
                ImageEx image = go.GetComponent<ImageEx>();
                if (image != null && !result.Contains(image)) result.Add(image);
            }
            return result;
        }

        private static ImageEx ResolveSource()
        {
            if (Selection.activeGameObject != null)
            {
                ImageEx active = Selection.activeGameObject.GetComponent<ImageEx>();
                if (active != null) return active;
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                ImageEx image = go.GetComponent<ImageEx>();
                if (image != null) return image;
            }

            return null;
        }

        private void SelectAllInScene()
        {
            ImageEx[] all = UnityEngine.Object.FindObjectsByType<ImageEx>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEngine.Object[] objects = new UnityEngine.Object[all.Length];
            for (int i = 0; i < all.Length; i++) objects[i] = all[i];
            Selection.objects = objects;
            ShowNotification(new GUIContent($"已选中场景内 {all.Length} 个 ImageEx"));
        }

        private static void MarkScenesDirty(List<ImageEx> targets)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);

            foreach (ImageEx target in targets)
            {
                if (target == null || UnityEditor.EditorUtility.IsPersistent(target)) continue;
                Scene scene = target.gameObject.scene;
                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
