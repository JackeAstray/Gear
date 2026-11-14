using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>图表共用的 UGUI 网格工具；只负责 quad/line，不依赖第三方库。</summary>
    internal static class GearChartMesh
    {
        public static void AddQuad(VertexHelper vh, Rect rect, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        public static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.001f) return;

            Vector2 normal = new Vector2(-delta.y, delta.x) / length * (width * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.up);
            vh.AddVert(b + normal, color, Vector2.one);
            vh.AddVert(b - normal, color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
