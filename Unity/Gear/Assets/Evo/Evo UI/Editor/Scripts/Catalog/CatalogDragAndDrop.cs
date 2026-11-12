using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Lets catalog items be dragged into the Hierarchy and Scene view.
    /// Drags carry no asset references, so other windows never receive the prefab itself.
    /// </summary>
    [InitializeOnLoad]
    internal static class CatalogDragAndDrop
    {
        const string DataKey = "Evo.UI.CatalogItem";

        static CatalogDragAndDrop()
        {
#if UNITY_6000_3_OR_NEWER
            DragAndDrop.AddDropHandlerV2(OnHierarchyDrop);
            DragAndDrop.AddDropHandlerV2(OnSceneDrop);
#else
            DragAndDrop.AddDropHandler(OnHierarchyDrop);
            DragAndDrop.AddDropHandler(OnSceneDrop);
#endif
        }

        public static void StartDrag(CatalogItem item)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = Array.Empty<Object>();
            DragAndDrop.paths = Array.Empty<string>();
            DragAndDrop.SetGenericData(DataKey, item);
            DragAndDrop.StartDrag(item.Name);
        }

        static CatalogItem DraggedItem => DragAndDrop.GetGenericData(DataKey) as CatalogItem;

#if UNITY_6000_3_OR_NEWER
        static DragAndDropVisualMode OnHierarchyDrop(EntityId dropTargetEntityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            return OnHierarchyDrop(EditorUtility.EntityIdToObject(dropTargetEntityId) as GameObject, dropMode, parentForDraggedObjects, perform);
        }
#else
        static DragAndDropVisualMode OnHierarchyDrop(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            return OnHierarchyDrop(EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject, dropMode, parentForDraggedObjects, perform);
        }
#endif

        static DragAndDropVisualMode OnHierarchyDrop(GameObject target, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            CatalogItem item = DraggedItem;
            if (item == null)
                return DragAndDropVisualMode.None;

            if (perform)
            {
                Transform parent = parentForDraggedObjects;
                int siblingIndex = -1;

                if (parent == null && target != null)
                {
                    if ((dropMode & HierarchyDropFlags.DropUpon) != 0) { parent = target.transform; }
                    else
                    {
                        // Dropped between rows: become a sibling of the row, or of its parent when outdented
                        Transform anchor = target.transform;
                        if ((dropMode & HierarchyDropFlags.DropAfterParent) != 0 && anchor.parent != null)
                            anchor = anchor.parent;

                        parent = anchor.parent;
                        siblingIndex = anchor.GetSiblingIndex() + ((dropMode & HierarchyDropFlags.DropAbove) != 0 ? 0 : 1);
                    }
                }

                // UI at the scene root wouldn't render, so root drops go to the canvas
                if (parent == null)
                {
                    parent = CreateMenu.GetRootParent();
                    siblingIndex = -1;
                }

                Create(item, parent, siblingIndex);
            }

            return DragAndDropVisualMode.Copy;
        }

        static DragAndDropVisualMode OnSceneDrop(Object dropUpon, Vector3 worldPosition, Vector2 viewportPosition, Transform parentForDraggedObjects, bool perform)
        {
            CatalogItem item = DraggedItem;
            if (item == null)
                return DragAndDropVisualMode.None;

            if (perform)
            {
                // Same Hierarchy placement as the catalog's Create button
                Transform parent = parentForDraggedObjects;
                int siblingIndex = -1;
                if (parent == null)
                    parent = CatalogWindow.ResolveParent(CatalogWindow.CurrentPlacement, true, out siblingIndex);

                GameObject obj = Create(item, parent, siblingIndex);
                if (obj != null)
                    MoveToCursor(obj.transform, viewportPosition);
            }

            return DragAndDropVisualMode.Copy;
        }

        static GameObject Create(CatalogItem item, Transform parent, int siblingIndex)
        {
            GameObject obj = item.Create(parent, siblingIndex);
            DragAndDrop.AcceptDrag();
            return obj;
        }

        // worldPosition falls back to the Scene view pivot when nothing is under the cursor, which is always the case for UI.
        // viewportPosition is the GUI mouse position, with the Scene view camera still active.
        static void MoveToCursor(Transform target, Vector2 mousePosition)
        {
            if (target.parent == null || Camera.current == null)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var plane = new Plane(target.parent.forward, target.parent.position);
            if (!plane.Raycast(ray, out float distance))
                return;

            target.position = ray.GetPoint(distance);
            Vector3 localPosition = target.localPosition;
            target.localPosition = new Vector3(localPosition.x, localPosition.y, 0);
        }
    }
}