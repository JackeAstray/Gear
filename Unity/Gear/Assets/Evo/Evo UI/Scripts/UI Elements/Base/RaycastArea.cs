using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// A rectangular hit target for the standard uGUI GraphicRaycaster.
    /// Generates no geometry while retaining Graphic's raycast filtering.
    /// </summary>
    [AddComponentMenu("Evo/UI/UI Elements/Raycast Area")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RaycastArea : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper) => vertexHelper.Clear();

        protected override void UpdateMaterial() { }

        /// <summary>
        /// Adds a non-rendering hit target when the object has no Graphic.
        /// Existing graphics and their raycast settings are preserved.
        /// </summary>
        public static Graphic ResolveRaycast(GameObject targetObject)
        {
            if (targetObject.TryGetComponent<Graphic>(out var graphicObj))
                return graphicObj;

            return targetObject.AddComponent<RaycastArea>();
        }
    }
}