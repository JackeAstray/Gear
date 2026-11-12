using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.EditorTools.ImageExtensions
{
    /// <summary>
    /// 场景点选可见 UI 图形：把光标位置当作拾取点，选中"视觉上最上层"的 Graphic，
    /// 而不是点到透明的容器或空 RectTransform。
    /// <para>
    /// 用法：在 Scene 视图里把光标移到目标图形上 → 菜单 `Gear/UI/拾取光标下的 Graphic`（快捷键 Ctrl+Shift+G）。
    /// 说明：
    /// - 只处理 World Space / Screen Space - Camera 的 Canvas；Screen Space - Overlay 的 UI 不在场景视图里绘制，无法点选；
    /// - 排序按 Canvas.sortingOrder，其次按层级深度（越靠后的兄弟越优先）；
    /// - 不改 Unity 默认选择逻辑，只在执行菜单命令时覆盖一次 Selection，不会干扰正在用的工具。
    /// </para>
    /// </summary>
    internal static class UIVisibleGraphicPicker
    {
        private static SceneView lastSceneView;
        private static Vector2 lastMousePosition;
        private static bool hasMousePosition;

        static UIVisibleGraphicPicker()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView view)
        {
            Event current = Event.current;
            if (current == null) return;
            if (current.type != EventType.Repaint &&
                current.type != EventType.MouseMove &&
                current.type != EventType.MouseDrag)
            {
                return;
            }

            lastSceneView = view;
            lastMousePosition = current.mousePosition;
            hasMousePosition = true;
        }

        [MenuItem("Gear/UI/拾取光标下的 Graphic %#g", false, 41)]
        private static void PickGraphicUnderCursor()
        {
            if (!hasMousePosition || lastSceneView == null)
            {
                Debug.LogWarning("[UI 拾取] 先把鼠标在 Scene 视图里移动一下，再执行菜单。");
                return;
            }

            Graphic picked = PickGraphic(lastSceneView, lastMousePosition);
            if (picked == null)
            {
                Debug.Log("[UI 拾取] 光标下没有可拾取的 UI 图形。");
                return;
            }

            Selection.activeGameObject = picked.gameObject;
            SceneView.RepaintAll();
        }

        private static Graphic PickGraphic(SceneView view, Vector2 guiPoint)
        {
            if (view == null || view.camera == null) return null;

            Vector2 screenPoint = HandleUtility.GUIPointToScreenPixelCoordinate(guiPoint);
            Camera sceneCamera = view.camera;

            Graphic best = null;
            int bestSortingOrder = int.MinValue;
            long bestDepth = long.MinValue;

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) continue;

                Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(false);
                foreach (Graphic graphic in graphics)
                {
                    if (graphic == null || !graphic.isActiveAndEnabled) continue;

                    RectTransform rect = graphic.rectTransform;
                    if (rect == null) continue;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, sceneCamera)) continue;

                    int sortingOrder = canvas.sortingOrder;
                    long depth = HierarchyDepth(graphic.transform);

                    bool better = best == null
                        || sortingOrder > bestSortingOrder
                        || (sortingOrder == bestSortingOrder && depth > bestDepth);

                    if (!better) continue;

                    best = graphic;
                    bestSortingOrder = sortingOrder;
                    bestDepth = depth;
                }
            }

            return best;
        }

        /// <summary>
        /// 把层级路径压成一个可比较的"渲染先后"权重：兄弟索引越大越靠上，
        /// 且越深的层级权重越低（父级优先保证同层内比较）。base 取 8 避免溢出。
        /// </summary>
        private static long HierarchyDepth(Transform transform)
        {
            long depth = 0;
            long weight = 1;
            for (Transform current = transform; current != null; current = current.parent)
            {
                depth += current.GetSiblingIndex() * weight;
                weight *= 8;
                if (weight > 1_000_000_000L) break;
            }

            return depth;
        }
    }
}
