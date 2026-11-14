using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using Gear.Utilities.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Navigation;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 把 ImageEx 画廊直接生成到 <c>UI Example</c> 场景的
    /// <c>UI示例/Scroll View/Viewport/Content</c> 下（清空旧示例卡片，可 Undo）。
    /// 具体卡片清单位于 <see cref="ImageExGalleryCatalog"/>。
    /// </summary>
    internal static class ImageExGalleryBuilder
    {
        private const string RootCanvasName = "UI示例";
        private const string GalleryCanvasName = "ImageEx 画廊";
        private const string ContentPath = "Scroll View/Viewport/Content";
        internal const float CardWidth = 400f;
        internal const float CardHeight = 340f;

        /// <summary>清空 Content 并按目录生成全部卡片（由自动化入口调用）。</summary>
        internal static int BuildInto(RectTransform content)
        {
            if (content == null) return 0;

            // 旧 UI 示例的 Image / ImageEx 兄弟节点一并删掉，只保留承载画廊的 Scroll View
            GameObject canvas = GameObject.Find(RootCanvasName);
            Transform scrollView = content.parent != null ? content.parent.parent : null;
            if (canvas != null)
            {
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = canvas.transform.GetChild(i);
                    if (child == scrollView) continue;
                    Undo.DestroyObjectImmediate(child.gameObject);
                }

                // 旧 demo 的其余 UI 已删除，根节点改名为画廊，避免继续叫“UI示例”
                if (canvas.name == RootCanvasName) canvas.name = "ImageEx 画廊";
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(content.GetChild(i).gameObject);
            }

            // 4 列 400×300：与场景当前布局一致，给效果与控件留出可读尺寸
            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(CardWidth, CardHeight);
                grid.spacing = new Vector2(20f, 20f);
                grid.padding = new RectOffset(12, 12, 12, 12);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.childAlignment = TextAnchor.UpperCenter;
            }

            ImageExGalleryCatalog.Populate(content);

            // 立刻重排，避免保存时 Content 还是旧高度
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            // 布局结算后再刷一次所有 ImageEx，保证 SDF 形状/材质参数按最终尺寸写入变体
            ImageEx[] images = content.GetComponentsInChildren<ImageEx>(true);
            for (int i = 0; i < images.Length; i++)
            {
                images[i].SetVerticesDirty();
                images[i].SetMaterialDirty();
            }

            Canvas.ForceUpdateCanvases();
            return content.childCount;
        }

        /// <summary>定位 UI Example 场景里的滚动内容容器。</summary>
        internal static RectTransform FindContent()
        {
            GameObject canvas = GameObject.Find(RootCanvasName);
            if (canvas == null) canvas = GameObject.Find(GalleryCanvasName);
            if (canvas != null)
            {
                Transform content = canvas.transform.Find(ContentPath);
                if (content is RectTransform rect) return rect;
            }

            // 兜底：场景根已改名或路径变化时，取“子节点最多”的 Content。
            // 不能直接取第一个：滚动渐隐演示卡内部也有 Viewport/Content，会误把卡片加进去。
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RectTransform best = null;
            int bestCount = -1;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != "Content" || all[i].transform.parent == null || all[i].transform.parent.name != "Viewport")
                    continue;

                RectTransform candidate = all[i].transform as RectTransform;
                int count = candidate != null ? candidate.childCount : -1;
                if (count > bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            return best;
        }

        /// <summary>
        /// 一键重建 UI Example 场景里的 ImageEx 画廊。
        /// <para><b>警告：会清空 Content 并重建所有卡片，场景里手工调过的效果会丢失。</b>
        /// 只想同步新功能时请用 <see cref="AppendNewCardsOnly"/>。</para>
        /// </summary>
        [MenuItem("Gear/UI Example/全量重建 ImageEx 画廊（会清空手工修改）", false, 40)]
        public static void RebuildUIExampleGallery()
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
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            RectTransform content = FindContent();
            if (content == null)
            {
                Debug.LogError("[ImageEx] UI Example 场景里找不到 Scroll View/Viewport/Content");
                return;
            }

            int count = BuildInto(content);
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log($"[ImageEx] 已重建 UI Example 画廊，共 {count} 张卡片");
        }

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

        /// <summary>
        /// 只补齐新增卡片，不删除/不重建任何已有卡片。适合在 UI Example 里手工调过效果后同步新功能。
        /// </summary>
        [MenuItem("Gear/UI Example/仅补充新增 ImageEx 卡片", false, 41)]
        public static void AppendNewCardsOnly()
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
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            RectTransform content = FindContent();
            if (content == null)
            {
                Debug.LogError("[ImageEx] UI Example 场景里找不到 Scroll View/Viewport/Content");
                return;
            }

            int added = ImageExGalleryCatalogExtra.AppendNewCards(content);
            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);
            Debug.Log($"[ImageEx] 已补充 {added} 张新卡片（已有卡片保持不变）");
        }

        internal static ImageEx AddCard(RectTransform content, string title, string description, System.Action<ImageEx> configurePreview)
        {
            GameObject card = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            Undo.RegisterCreatedObjectUndo(card, "Create Card");
            card.transform.SetParent(content, false);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) card.layer = uiLayer;

            RectTransform rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            ImageEx background = card.GetComponent<ImageEx>();
            background.DrawShape = DrawShape.Rectangle;
            background.Rectangle = new RectangleImg { CornerRadius = new Vector4(18f, 18f, 18f, 18f) };
            background.color = new Color(0.075f, 0.083f, 0.11f, 1f);
            background.OutlineWidth = 1f;
            background.OutlineColor = new Color(1f, 1f, 1f, 0.07f);

            // 卡片背景不挂外阴影：画廊里只在“投影”示例的 Preview 上加软阴影，
            // 避免每个示例都出现一圈阴影。
            background.AppendShadow = false;

            CreateHighlight(card.transform, uiLayer);
            CreateLabel(card.transform, "CardTitle", title, 21f,
                new Color(0.94f, 0.95f, 0.98f), new Vector2(20f, -18f), new Vector2(360f, 30f),
                TextAlignmentOptions.Left);
            CreateLabel(card.transform, "CardDescription", description, 13f,
                new Color(0.62f, 0.65f, 0.73f), new Vector2(20f, -48f), new Vector2(360f, 22f),
                TextAlignmentOptions.Left);

            ImageEx preview = CreatePreview(card.transform, uiLayer);
            configurePreview?.Invoke(preview);

            // 批量重建时布局还没结算，形状/材质参数容易停留在默认值；先标脏，
            // 等布局完成后再统一刷新一次（见 BuildInto 末尾）。
            preview.SetVerticesDirty();
            preview.SetMaterialDirty();

            // 投影示例的阴影必须画到 Preview 外侧，不能套 RectMask2D；其余示例保留裁剪。
            if (title.StartsWith("投影") || title.Contains("Glass Card"))
            {
                RectMask2D clipMask = preview.transform.parent.GetComponent<RectMask2D>();
                if (clipMask != null) clipMask.enabled = false;
            }

            return preview;
        }

        /// <summary>
        /// 在画廊里生成一张 Tab 组演示卡：三个按钮切换三张页面。
        /// </summary>
        internal static void AddTabGroupCard(RectTransform content, string title, string description)
        {
            // 标题里可能带 "/"，不能使用 Transform.Find
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == title)
                    return;
            }

            ImageEx preview = AddCard(content, title, description, p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null || preview.transform.parent == null)
                return;

            Transform clip = preview.transform.parent;
            GameObject groupGo = new GameObject("TabGroup", typeof(RectTransform), typeof(GearTabGroup));
            groupGo.transform.SetParent(clip, false);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) groupGo.layer = uiLayer;

            RectTransform groupRect = (RectTransform)groupGo.transform;
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;

            GearTabGroup group = groupGo.GetComponent<GearTabGroup>();
            string[] ids = { "A", "B", "C" };
            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 0.9f),
                new Color(0.14f, 0.75f, 0.95f, 0.9f),
                new Color(0.25f, 0.78f, 0.52f, 0.9f),
            };

            List<GearTabGroup.Tab> tabs = new List<GearTabGroup.Tab>();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];

                GameObject page = new GameObject("Page " + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
                page.transform.SetParent(groupGo.transform, false);
                if (uiLayer >= 0) page.layer = uiLayer;

                RectTransform pageRect = (RectTransform)page.transform;
                pageRect.anchorMin = new Vector2(0f, 0f);
                pageRect.anchorMax = new Vector2(1f, 1f);
                pageRect.offsetMin = new Vector2(8f, 8f);
                pageRect.offsetMax = new Vector2(-8f, -36f);

                ImageEx pageImage = page.GetComponent<ImageEx>();
                pageImage.DrawShape = DrawShape.Rectangle;
                pageImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(12f, 12f, 12f, 12f) };
                pageImage.color = colors[i];
                pageImage.raycastTarget = false;
                CreateCenteredLabel(page.transform, id, 42f, new Color(1f, 1f, 1f, 0.95f));

                GameObject buttonGo = new GameObject("Tab " + id,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
                buttonGo.transform.SetParent(groupGo.transform, false);
                if (uiLayer >= 0) buttonGo.layer = uiLayer;

                RectTransform buttonRect = (RectTransform)buttonGo.transform;
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(0f, 1f);
                buttonRect.pivot = new Vector2(0f, 1f);
                buttonRect.sizeDelta = new Vector2(52f, 24f);
                buttonRect.anchoredPosition = new Vector2(8f + i * 58f, -6f);

                ImageEx buttonImage = buttonGo.GetComponent<ImageEx>();
                buttonImage.DrawShape = DrawShape.Rectangle;
                buttonImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(8f, 8f, 8f, 8f) };
                buttonImage.color = colors[i];

                Button button = buttonGo.GetComponent<Button>();
                button.targetGraphic = buttonImage;
                button.transition = Selectable.Transition.None;
                CreateCenteredLabel(buttonGo.transform, id, 15f, Color.white);

                tabs.Add(new GearTabGroup.Tab
                {
                    id = id,
                    button = button,
                    page = page,
                    firstSelected = buttonGo,
                });
            }

            group.Configure(tabs);
            group.Select(ids[0]);
        }

        internal static TextMeshProUGUI CreateCenteredLabel(Transform parent, string text, float fontSize, Color color)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 在画廊里生成一张 Stepper 演示卡：3 个步骤、指示器、Next/Back。
        /// </summary>
        internal static void AddStepperCard(RectTransform content, string title, string description)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name == title)
                    return;
            }

            ImageEx preview = AddCard(content, title, description, p =>
            {
                p.color = new Color(0f, 0f, 0f, 0f);
                p.OutlineWidth = 0f;
                p.raycastTarget = false;
            });

            if (preview == null || preview.transform.parent == null)
                return;

            Transform clip = preview.transform.parent;
            GameObject stepperGo = new GameObject("Stepper", typeof(RectTransform), typeof(GearStepper));
            stepperGo.transform.SetParent(clip, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) stepperGo.layer = uiLayer;

            RectTransform stepperRect = (RectTransform)stepperGo.transform;
            stepperRect.anchorMin = Vector2.zero;
            stepperRect.anchorMax = Vector2.one;
            stepperRect.offsetMin = Vector2.zero;
            stepperRect.offsetMax = Vector2.zero;

            GearStepper stepper = stepperGo.GetComponent<GearStepper>();
            Color[] colors =
            {
                new Color(0.49f, 0.36f, 0.99f, 0.9f),
                new Color(0.14f, 0.75f, 0.95f, 0.9f),
                new Color(0.25f, 0.78f, 0.52f, 0.9f),
            };

            Button next = CreateButton(stepperGo.transform, "Next", "Next", new Vector2(196f, -5f), new Vector2(52f, 24f), colors[0]);
            Button back = CreateButton(stepperGo.transform, "Back", "Back", new Vector2(254f, -5f), new Vector2(52f, 24f), colors[1]);

            List<GearStepper.Step> steps = new List<GearStepper.Step>();
            for (int i = 0; i < 3; i++)
            {
                string id = (i + 1).ToString();
                Button indicator = CreateButton(stepperGo.transform, "Indicator " + id, id,
                    new Vector2(8f + i * 58f, -5f), new Vector2(50f, 24f), colors[i]);

                GameObject page = new GameObject("Step " + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
                page.transform.SetParent(stepperGo.transform, false);
                if (uiLayer >= 0) page.layer = uiLayer;

                RectTransform pageRect = (RectTransform)page.transform;
                pageRect.anchorMin = Vector2.zero;
                pageRect.anchorMax = Vector2.one;
                pageRect.offsetMin = new Vector2(8f, 38f);
                pageRect.offsetMax = new Vector2(-8f, -38f);

                ImageEx pageImage = page.GetComponent<ImageEx>();
                pageImage.DrawShape = DrawShape.Rectangle;
                pageImage.Rectangle = new RectangleImg { CornerRadius = new Vector4(12f, 12f, 12f, 12f) };
                pageImage.color = colors[i];
                pageImage.raycastTarget = false;
                CreateCenteredLabel(page.transform, id, 42f, new Color(1f, 1f, 1f, 0.95f));

                steps.Add(new GearStepper.Step
                {
                    id = id,
                    title = id,
                    page = page,
                    indicator = indicator,
                    firstSelected = next.gameObject,
                });
            }

            stepper.NextButton = next;
            stepper.BackButton = back;
            stepper.Configure(steps);
            stepper.GoTo(0);
        }

        internal static Button CreateButton(Transform parent, string name, string label,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx), typeof(Button));
            go.transform.SetParent(parent, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            ImageEx image = go.GetComponent<ImageEx>();
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg { CornerRadius = new Vector4(8f, 8f, 8f, 8f) };
            image.color = color;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            CreateCenteredLabel(go.transform, label, 14f, Color.white);
            return button;
        }

        internal static Gradient MakeGradient(Color a, Color b)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static void CreateHighlight(Transform card, int uiLayer)
        {
            GameObject highlight = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            highlight.transform.SetParent(card, false);
            if (uiLayer >= 0) highlight.layer = uiLayer;

            ImageEx image = highlight.GetComponent<ImageEx>();
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg { CornerRadius = new Vector4(18f, 18f, 0f, 0f) };
            image.color = new Color(1f, 1f, 1f, 0.05f);
            image.raycastTarget = false;

            RectTransform rect = (RectTransform)highlight.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(1f, -1f);
            rect.offsetMax = new Vector2(-1f, 0f);
        }

        private static ImageEx CreatePreview(Transform card, int uiLayer)
        {
            // PreviewClip 用 RectMask2D 把 BlobbyCross / NTriangleRounded 这类
            // 会超出 rect 的 SDF 形状裁在 320x140 的预览范围内。
            GameObject clip = new GameObject("PreviewClip", typeof(RectTransform), typeof(RectMask2D));
            clip.transform.SetParent(card, false);
            if (uiLayer >= 0) clip.layer = uiLayer;

            // 底部锚定：保持预览中心在原位置（卡片中心下方 32px），只向上加高，
            // 既有卡片的内容坐标不会整体上移，也不会压到标题/描述。
            RectTransform clipRect = (RectTransform)clip.transform;
            clipRect.anchorMin = new Vector2(0.5f, 0f);
            clipRect.anchorMax = new Vector2(0.5f, 0f);
            clipRect.pivot = new Vector2(0.5f, 0f);
            clipRect.anchoredPosition = new Vector2(0f, 20f);
            clipRect.sizeDelta = new Vector2(320f, 236f);

            GameObject preview = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            preview.transform.SetParent(clip.transform, false);
            if (uiLayer >= 0) preview.layer = uiLayer;

            RectTransform rect = (RectTransform)preview.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            ImageEx image = preview.GetComponent<ImageEx>();
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg { CornerRadius = new Vector4(14f, 14f, 14f, 14f) };
            image.raycastTarget = true;
            return image;
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent, string name, string text, float size, Color color,
            Vector2 position, Vector2 rectSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            return label;
        }
    }
}
