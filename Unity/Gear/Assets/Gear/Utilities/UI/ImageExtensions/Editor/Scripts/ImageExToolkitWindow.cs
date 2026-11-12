using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// ImageEx 工具台：预设库 / 批量 / 诊断 三个分区。
    /// <para>
    /// 设计原则（参考通用 UI 工具链的组织方式，代码为 Gear 自研）：
    /// 1. 只读信息默认可见，修改操作必须显式点击，且全部走 Undo；
    /// 2. 预设库解决"效果不可发现"，批量解决"多对象手工同步"，诊断解决"材质/draw call 不可见"；
    /// 3. 不依赖任何第三方 UI 插件的资产或程序集。
    /// </para>
    /// </summary>
    public sealed class ImageExToolkitWindow : EditorWindow
    {
        private enum Tab
        {
            Presets = 0,
            Batch = 1,
            Diagnose = 2,
            Batching = 3,
        }

        private static readonly string[] TabLabels = { "预设库", "批量", "诊断", "合批" };

        private readonly List<ImageExPreset> presets = new List<ImageExPreset>();
        private List<List<ImageEx>> batchingGroups;
        private ImageEx[] sceneImages;
        private bool batchingFromScene;

        // 预设缩略图：单个预览场景 + 每个预设一张缓存纹理
        private PreviewRenderUtility previewUtility;
        private ImageEx previewImage;
        private readonly Dictionary<ImageExPreset, Texture> previewTextures = new Dictionary<ImageExPreset, Texture>();
        private bool previewFailed;

        private Tab tab;
        private Vector2 scroll;
        private string search = string.Empty;
        private string materialFolder = ImageExBatchingTools.DefaultMaterialFolder;

        [MenuItem("Gear/ImageEx 工具台", false, 20)]
        private static void Open()
        {
            ImageExToolkitWindow window = GetWindow<ImageExToolkitWindow>();
            window.titleContent = new GUIContent("ImageEx 工具台");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            tab = (Tab)Mathf.Clamp(EditorPrefs.GetInt("Gear.ImageExToolkit.Tab", 0), 0, TabLabels.Length - 1);
            materialFolder = EditorPrefs.GetString("Gear.ImageExToolkit.MaterialFolder", ImageExBatchingTools.DefaultMaterialFolder);
            RefreshPresets();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt("Gear.ImageExToolkit.Tab", (int)tab);
            EditorPrefs.SetString("Gear.ImageExToolkit.MaterialFolder", materialFolder);
            ReleasePreview();
        }

        private void OnGUI()
        {
            tab = (Tab)GUILayout.Toolbar((int)tab, TabLabels, GUILayout.Height(24f));
            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case Tab.Presets: DrawPresetsTab(); break;
                case Tab.Batch: DrawBatchTab(); break;
                case Tab.Diagnose: DrawDiagnoseTab(); break;
                case Tab.Batching: DrawBatchingTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // 预设库
        // ============================================================

        private void DrawPresetsTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("搜索", search, GUILayout.MinWidth(160f));
                if (GUILayout.Button("刷新", GUILayout.Width(56f))) RefreshPresets();
                if (GUILayout.Button("从选中创建", GUILayout.Width(96f))) CreatePresetFromSelection();
            }

            EditorGUILayout.HelpBox(
                "预设只覆盖 ImageExPreset 已声明的字段（阴影开关/颜色、采样宽度等少数字段不在预设内）；" +
                "应用预设会记录 Undo，可 Ctrl+Z 撤销。",
                MessageType.Info);

            if (presets.Count == 0)
            {
                EditorGUILayout.HelpBox("工程里还没有 ImageExPreset 资产。\n右键 Assets > Create > Gear > ImageEx Preset 新建，或在下方用「从选中创建」。",
                    MessageType.Warning);
                return;
            }

            int shown = 0;
            foreach (ImageExPreset preset in presets)
            {
                if (!Matches(preset, search)) continue;
                shown++;
                DrawPresetRow(preset);
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的预设。", MessageType.None);
            }
        }

        private void DrawPresetRow(ImageExPreset preset)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect previewRect = GUILayoutUtility.GetRect(56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
                    Texture previewTexture = GetPreviewTexture(preset);
                    if (previewTexture != null)
                    {
                        GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, true);
                    }
                    else
                    {
                        EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                    }

                    EditorGUILayout.ObjectField(preset, typeof(ImageExPreset), false, GUILayout.Width(200f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("定位", GUILayout.Width(48f)))
                    {
                        Selection.activeObject = preset;
                        EditorGUIUtility.PingObject(preset);
                    }
                    if (GUILayout.Button("应用到选中", GUILayout.Width(88f)))
                    {
                        ApplyPreset(preset);
                    }
                }

                GUILayout.Label(BuildBadges(preset), EditorStyles.miniLabel);
            }
        }

        private static string BuildBadges(ImageExPreset preset)
        {
            List<string> parts = new List<string>(8);
            if (preset.enableGradientTex) parts.Add("渐变纹理");
            if (preset.gradientEnabled) parts.Add("渐变效果·" + preset.gradientType);
            if (preset.transitionMode != ImageEx.TransitionMode.None) parts.Add("过渡·" + preset.transitionMode);
            if (preset.blurType != ImageEx.BlurType.None) parts.Add("模糊·" + preset.blurType);
            if (preset.edgeMode != ImageEx.EdgeMode.None) parts.Add("边缘·" + preset.edgeMode);
            if (preset.enableInnerShadow) parts.Add("内阴影");
            if (preset.clipMode != ImageEx.ClipMode.None) parts.Add("裁剪·" + preset.clipMode);
            if (preset.appendShadow) parts.Add("阴影");
            if (preset.detailMode != ImageEx.DetailFilter.None) parts.Add("细节·" + preset.detailMode);
            if (preset.toneFilter != ImageEx.ToneFilter.None) parts.Add("色调·" + preset.toneFilter);
            if (preset.targetMode != ImageEx.TargetMode.None) parts.Add("目标·" + preset.targetMode);
            if (preset.samplingMode != ImageEx.SamplingFilter.None) parts.Add("采样·" + preset.samplingMode);
            return parts.Count == 0 ? "（该预设没有覆盖任何效果，等同纯形状）" : string.Join(" · ", parts);
        }

        private static bool Matches(ImageExPreset preset, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return true;
            return preset != null && preset.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ApplyPreset(ImageExPreset preset)
        {
            if (preset == null) return;

            List<ImageEx> targets = GetSelectedImageEx();
            if (targets.Count == 0)
            {
                ShowNotification(new GUIContent("请先在场景/层级里选中至少一个 ImageEx"));
                return;
            }

            foreach (ImageEx target in targets)
            {
                Undo.RecordObject(target, "应用 ImageEx 预设");
                preset.ApplyTo(target);
                UnityEditor.EditorUtility.SetDirty(target);
            }

            MarkScenesDirty(targets);
            ShowNotification(new GUIContent($"已把「{preset.name}」应用到 {targets.Count} 个 ImageEx"));
        }

        private void CreatePresetFromSelection()
        {
            ImageEx source = ResolveSource();
            if (source == null)
            {
                ShowNotification(new GUIContent("请先选中一个 ImageEx 作为预设来源"));
                return;
            }

            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "保存 ImageEx 预设", source.name + "_Preset", "asset", "选择预设资产保存位置");
            if (string.IsNullOrEmpty(path)) return;

            ImageExPreset preset = ScriptableObject.CreateInstance<ImageExPreset>();
            preset.ReadFrom(source);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            RefreshPresets();
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private void RefreshPresets()
        {
            ClearPreviewTextures();
            presets.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:ImageExPreset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ImageExPreset preset = AssetDatabase.LoadAssetAtPath<ImageExPreset>(path);
                if (preset != null) presets.Add(preset);
            }

            presets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        // ============================================================
        // 预设缩略图
        // ============================================================

        private Texture GetPreviewTexture(ImageExPreset preset)
        {
            if (previewFailed || preset == null) return null;
            if (previewTextures.TryGetValue(preset, out Texture cached) && cached != null) return cached;

            try
            {
                EnsurePreviewUtility();
                if (previewUtility == null || previewImage == null)
                {
                    previewFailed = true;
                    return null;
                }

                const int size = 64;
                Texture source = null;
                previewUtility.BeginPreview(new Rect(0f, 0f, size, size), GUIStyle.none);
                try
                {
                    preset.ApplyTo(previewImage);
                    previewImage.DrawShape = DrawShape.Rectangle;
                    previewImage.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    previewUtility.camera.Render();
                }
                finally
                {
                    source = previewUtility.EndPreview();
                }

                if (source == null) return null;

                Texture2D snapshot = CopyToTexture(source, size);
                previewTextures[preset] = snapshot;
                return snapshot;
            }
            catch (Exception exception)
            {
                previewFailed = true;
                Debug.LogWarning("[ImageEx 工具台] 预设缩略图渲染失败，已回退为占位色块：" + exception.Message);
                return null;
            }
        }

        private static Texture2D CopyToTexture(Texture source, int size)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                result.Apply();
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null) return;

            previewUtility = new PreviewRenderUtility();
            Camera camera = previewUtility.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 0.5f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.transform.position = new Vector3(0f, 0f, -2f);

            GameObject canvasObject = new GameObject("ImageExPreviewCanvas", typeof(Canvas));
            previewUtility.AddSingleGO(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            GameObject imageObject = new GameObject("ImageExPreview", typeof(RectTransform), typeof(ImageEx));
            previewUtility.AddSingleGO(imageObject);
            imageObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0.6f, 0.36f);
            rectTransform.anchoredPosition = Vector2.zero;

            previewImage = imageObject.GetComponent<ImageEx>();
            previewImage.raycastTarget = false;
        }

        private void ClearPreviewTextures()
        {
            foreach (KeyValuePair<ImageExPreset, Texture> pair in previewTextures)
            {
                if (pair.Value != null) UnityEngine.Object.DestroyImmediate(pair.Value);
            }

            previewTextures.Clear();
        }

        private void ReleasePreview()
        {
            ClearPreviewTextures();
            previewImage = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            previewFailed = false;
        }

        // ============================================================
        // 批量
        // ============================================================

        private void DrawBatchTab()
        {
            List<ImageEx> selected = GetSelectedImageEx();
            ImageEx source = ResolveSource();

            EditorGUILayout.HelpBox(
                $"选中 ImageEx：{selected.Count} 个\n源对象：{(source != null ? source.name : "无（先选中一个 ImageEx）")}",
                MessageType.None);

            using (new EditorGUI.DisabledScope(source == null))
            {
                if (GUILayout.Button("把源的完整设置同步到其余选中对象", GUILayout.Height(26f)))
                {
                    SyncFromSource(source, selected);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("材质模式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Dynamic：每实例一份材质，互不串扰，但会各自占用一段 draw call。\n" +
                "Shared：共用材质可合批，但要求各实例的特效设置完全一致，否则由最后写入者决定。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部设为 Dynamic")) SetMaterialMode(selected, MaterialMode.Dynamic);
                if (GUILayout.Button("全部设为 Shared")) SetMaterialMode(selected, MaterialMode.Shared);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("清理", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "只清理「效果开关已关闭、但参数仍保留旧值」的冗余字段，不改动任何已启用的效果。",
                MessageType.None);
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button("清理选中对象的冗余效果参数"))
                {
                    CleanupRedundantValues(selected);
                }
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("全选当前场景的 ImageEx"))
            {
                SelectAllInScene();
            }
        }

        private void SyncFromSource(ImageEx source, List<ImageEx> selected)
        {
            if (source == null) return;

            int changed = 0;
            SerializedObject sourceObject = new SerializedObject(source);
            foreach (ImageEx target in selected)
            {
                if (target == source) continue;

                Undo.RecordObject(target, "同步 ImageEx 设置");
                SerializedObject targetObject = new SerializedObject(target);

                SerializedProperty iterator = sourceObject.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (IsExcludedProperty(iterator.propertyPath)) continue;
                    SerializedProperty counterpart = targetObject.FindProperty(iterator.propertyPath);
                    if (counterpart == null) continue;
                    targetObject.CopyFromSerializedProperty(iterator);
                }

                targetObject.ApplyModifiedProperties();
                UnityEditor.EditorUtility.SetDirty(target);
                changed++;
            }

            MarkScenesDirty(selected);
            ShowNotification(new GUIContent($"已同步 {changed} 个 ImageEx（Sprite / 材质 / 相机纹理不参与同步）"));
        }

        private static bool IsExcludedProperty(string propertyPath)
        {
            return propertyPath.IndexOf("m_Sprite", StringComparison.Ordinal) >= 0
                || propertyPath.IndexOf("m_OverrideSprite", StringComparison.Ordinal) >= 0
                || propertyPath.IndexOf("m_Material", StringComparison.Ordinal) >= 0
                || propertyPath.IndexOf("m_CameraTexture", StringComparison.Ordinal) >= 0
                || propertyPath.IndexOf("m_Script", StringComparison.Ordinal) >= 0
                || propertyPath.IndexOf("materialMode", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetMaterialMode(List<ImageEx> targets, MaterialMode mode)
        {
            if (targets.Count == 0)
            {
                ShowNotification(new GUIContent("请先选中 ImageEx"));
                return;
            }

            foreach (ImageEx target in targets)
            {
                Undo.RecordObject(target, "切换 ImageEx 材质模式");
                target.MaterialMode = mode;
                UnityEditor.EditorUtility.SetDirty(target);
            }

            MarkScenesDirty(targets);
            ShowNotification(new GUIContent($"已把 {targets.Count} 个 ImageEx 设为 {mode}"));
        }

        private void CleanupRedundantValues(List<ImageEx> targets)
        {
            int touched = 0;
            foreach (ImageEx target in targets)
            {
                Undo.RecordObject(target, "清理 ImageEx 冗余参数");

                if (target.Blur == ImageEx.BlurType.None && !Mathf.Approximately(target.BlurIntensity, 0f))
                {
                    target.BlurIntensity = 0f;
                    touched++;
                }

                if (target.Transition == ImageEx.TransitionMode.None)
                {
                    if (!Mathf.Approximately(target.TransitionRate, 0f)) { target.TransitionRate = 0f; touched++; }
                    if (!Mathf.Approximately(target.TransitionSoftness, 0f)) { target.TransitionSoftness = 0f; touched++; }
                }

                if (target.Edge == ImageEx.EdgeMode.None && !Mathf.Approximately(target.EdgeWidth, 0.5f))
                {
                    target.EdgeWidth = 0.5f;
                    touched++;
                }

                if (target.Target == ImageEx.TargetMode.None && !Mathf.Approximately(target.TargetRange, 0.1f))
                {
                    target.TargetRange = 0.1f;
                    touched++;
                }

                if (target.Detail == ImageEx.DetailFilter.None && !Mathf.Approximately(target.DetailIntensity, 1f))
                {
                    target.DetailIntensity = 1f;
                    touched++;
                }

                UnityEditor.EditorUtility.SetDirty(target);
            }

            MarkScenesDirty(targets);
            ShowNotification(new GUIContent($"清理完成，共规范化 {touched} 个字段"));
        }

        // ============================================================
        // 诊断
        // ============================================================

        private void DrawDiagnoseTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("分析选中")) sceneImages = null;
                if (GUILayout.Button("分析整个场景"))
                {
                    sceneImages = UnityEngine.Object.FindObjectsByType<ImageEx>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);
                }
                if (GUILayout.Button("全选场景 ImageEx")) SelectAllInScene();
            }

            List<ImageEx> scope = sceneImages != null ? new List<ImageEx>(sceneImages) : GetSelectedImageEx();
            if (scope.Count == 0)
            {
                EditorGUILayout.HelpBox("选中场景里的 ImageEx 后，这里会给出材质模式、效果启用与合批估算。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(sceneImages != null ? "范围：整个场景" : "范围：当前选中", EditorStyles.boldLabel);
            DrawStats(scope);
        }

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
