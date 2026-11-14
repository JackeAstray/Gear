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
    public sealed partial class ImageExToolkitWindow : EditorWindow
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
            if (preset.patternMode != ImageEx.PatternMode.None) parts.Add("图案·" + preset.patternMode);
            if (preset.enableBorderGlow) parts.Add("边缘辉光");
            if (preset.enableDashedOutline != 0) parts.Add("虚线");
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

    }
}
