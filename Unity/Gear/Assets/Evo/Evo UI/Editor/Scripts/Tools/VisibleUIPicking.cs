using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Scene view clicks on UI select the topmost graphic that is drawn under the cursor,
    /// instead of whichever RectTransform Unity's rect picking returns first.
    /// Clicks that Unity resolves to anything other than UI are left untouched.
    /// </summary>
    [InitializeOnLoad, NoAutoStaticsCleanup]
    public static class VisibleUIPicking
    {
        const string MenuPath = "Tools/Evo UI/Pick Visible UI in Scene View";
        const string PrefKey = "Evo.UI.VisibleUIPicking";

        // Unity's public pick keeps the caller's ignore and filter lists in shared fields and clears them when it returns.
        // Our pick runs inside the caller's, so it gets lists of its own and the caller's are put back after; otherwise a
        // caller that picks with an ignore list until nothing is left (ProBuilder's face selection) loops forever.
        static readonly FieldInfo[] SharedFields = Array.ConvertAll(new[] { "s_IgnorePickResultsCache", "s_FilterPickResultsCache", "s_PickingExclude", "s_PickingInclude" },
            name => typeof(HandleUtility).GetField(name, BindingFlags.Static | BindingFlags.NonPublic));
        static readonly object[] ownLists = Array.ConvertAll(SharedFields, field => field == null ? null : Activator.CreateInstance(field.FieldType));
        static readonly object[] callerLists = new object[SharedFields.Length];

        // Unity asks this internal delegate before its own picking (the Physics Debugger uses it too).
        // If a future version removes or changes it, the feature stays off and Unity picks as usual.
        static readonly FieldInfo PickField = typeof(HandleUtility).GetField("pickClosestGameObjectDelegate", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly Delegate PickDelegate = PickField == null || !typeof(Delegate).IsAssignableFrom(PickField.FieldType) || Array.IndexOf(SharedFields, null) >= 0 ? null
            : Delegate.CreateDelegate(PickField.FieldType, typeof(VisibleUIPicking).GetMethod(nameof(Pick), BindingFlags.Static | BindingFlags.NonPublic), false);

        static readonly List<Graphic> graphics = new();
        static bool enabled = EditorPrefs.GetBool(PrefKey, true);
        static bool picking;
        static bool circling;

        static VisibleUIPicking()
        {
            Attach();
            SceneView.beforeSceneGui += OnBeforeSceneGui;
        }

        public static bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                circling = false;
                EditorPrefs.SetBool(PrefKey, value);

                if (value)
                    Attach();
                else if (PickDelegate != null && Equals(PickField.GetValue(null), PickDelegate))
                    PickField.SetValue(null, null);
            }
        }

        [MenuItem(MenuPath, false, 25)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, enabled);
            return PickDelegate != null;
        }

        // The Physics Debugger clears the slot when it closes, so take it back on the next click
        static void OnBeforeSceneGui(SceneView sceneView)
        {
            if (Event.current.type == EventType.MouseDown)
                Attach();
        }

        static void Attach()
        {
            if (enabled && PickDelegate != null && PickField.GetValue(null) == null)
                PickField.SetValue(null, PickDelegate);
        }

        static GameObject Pick(Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter, out int materialIndex)
        {
            // Unity 6.0 treats a null result with a nonzero index as a hit
            materialIndex = 0;

            // A caller's ignore list only repeats an object when its picks go in circles. Stay out of the way until
            // the menu is toggled or scripts reload, so its next pick is Unity's alone and its loop can end.
            if (ignore != null && ignore.Length > 1 && Array.LastIndexOf(ignore, ignore[ignore.Length - 1], ignore.Length - 2) >= 0)
                circling = true;

            // Our own call below lands here too: let Unity pick normally
            if (picking || circling)
                return null;

            GameObject picked;
            picking = true;

            for (int i = 0; i < SharedFields.Length; i++)
            {
                callerLists[i] = SharedFields[i].GetValue(null);
                SharedFields[i].SetValue(null, ownLists[i]);
            }

            try
            {
                picked = HandleUtility.PickGameObject(ToGUIPoint(position), false, ignore, filter, out materialIndex);
            }
            finally
            {
                picking = false;

                for (int i = 0; i < SharedFields.Length; i++)
                {
                    SharedFields[i].SetValue(null, callerLists[i]);
                    callerLists[i] = null;
                }
            }

            if (picked == null)
            {
                materialIndex = 0;
                return null;
            }

            // Only UI results are replaced
            if (picked.transform is not RectTransform || picked.GetComponentInParent<Canvas>() == null)
                return picked;

            try
            {
                Graphic top = FindTopGraphic(picked, camera, layers, position, ignore, filter);
                if (top == null)
                    return picked;

                materialIndex = 0;
                return top.gameObject;
            }
            catch (Exception exception)
            {
                // A failure here must not cost the click, so Unity's own pick stands
                Debug.LogException(exception);
                return picked;
            }
        }

        // Unity converts a GUI point to pixels with an offset and a scale per axis, so two samples invert it
        static Vector2 ToGUIPoint(Vector2 pixel)
        {
            Vector2 origin = HandleUtility.GUIPointToScreenPixelCoordinate(Vector2.zero);
            Vector2 scale = HandleUtility.GUIPointToScreenPixelCoordinate(Vector2.one) - origin;
            return new Vector2((pixel.x - origin.x) / scale.x, (pixel.y - origin.y) / scale.y);
        }

        static Graphic FindTopGraphic(GameObject picked, Camera camera, int layers, Vector2 position, GameObject[] ignore, GameObject[] filter)
        {
            // Preview scenes from other tools belong to no stage, so those clicks stay Unity's
            Stage stage = StageUtility.GetStage(picked);
            if (stage == null)
                return null;

            Graphic top = null;
            Vector3 cameraPosition = camera.transform.position;

            foreach (Canvas canvas in stage.FindComponentsOfType<Canvas>())
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled || (layers & (1 << canvas.gameObject.layer)) == 0)
                    continue;

                canvas.GetComponentsInChildren(graphics);

                foreach (Graphic graphic in graphics)
                {
                    if ((top == null || DrawsAbove(graphic, top, cameraPosition)) && IsDrawnAt(graphic, camera, position, ignore, filter))
                        top = graphic;
                }
            }

            graphics.Clear();
            return top;
        }

        static bool DrawsAbove(Graphic graphic, Graphic other, Vector3 cameraPosition)
        {
            Canvas canvas = graphic.canvas;
            Canvas otherCanvas = other.canvas;
            int layer = SortingLayer.GetLayerValueFromID(canvas.sortingLayerID);
            int otherLayer = SortingLayer.GetLayerValueFromID(otherCanvas.sortingLayerID);

            if (layer != otherLayer)
                return layer > otherLayer;
            if (canvas.sortingOrder != otherCanvas.sortingOrder)
                return canvas.sortingOrder > otherCanvas.sortingOrder;

            // Separate canvases on the same order draw back to front, like other transparent objects
            Transform root = canvas.rootCanvas.transform;
            Transform otherRoot = otherCanvas.rootCanvas.transform;
            if (root != otherRoot)
            {
                float distance = (root.position - cameraPosition).sqrMagnitude;
                float otherDistance = (otherRoot.position - cameraPosition).sqrMagnitude;
                if (distance != otherDistance)
                    return distance < otherDistance;
            }

            return graphic.depth > other.depth;
        }

        static bool IsDrawnAt(Graphic graphic, Camera camera, Vector2 position, GameObject[] ignore, GameObject[] filter)
        {
            // Depth stays -1 until the canvas draws the graphic
            CanvasRenderer canvasRenderer = graphic.canvasRenderer;
            if (!graphic.enabled || graphic.depth == -1 || canvasRenderer.cull)
                return false;
            if (graphic is RaycastArea || graphic is ProceduralShape { OperandOnly: true })
                return false;

            // Helpers hidden from the Hierarchy (TMP sub-meshes, Counter's roll layer) and locked layers can't be selected
            GameObject gameObject = graphic.gameObject;
            if ((gameObject.hideFlags & HideFlags.HideInHierarchy) != 0 || (Tools.lockedLayers & (1 << gameObject.layer)) != 0)
                return false;
            if (graphic.color.a <= 0 || canvasRenderer.GetAlpha() <= 0 || canvasRenderer.GetInheritedAlpha() <= 0)
                return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, position, camera))
                return false;
            if (ignore != null && Array.IndexOf(ignore, gameObject) >= 0)
                return false;
            if (filter != null && Array.IndexOf(filter, gameObject) < 0)
                return false;
            if (SceneVisibilityManager.instance.IsHidden(gameObject))
                return false;
            if (graphic is MaskableGraphic { maskable: false })
                return true;

            // Masks stop at the canvas that sorts the graphic, like uGUI's own clipping
            for (Transform parent = graphic.transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent(out RectMask2D rectMask) && !rectMask.IsRaycastLocationValid(position, camera))
                    return false;
                if (parent.TryGetComponent(out Mask mask) && !mask.IsRaycastLocationValid(position, camera))
                    return false;
                if (parent.TryGetComponent(out SoftMask softMask) && !softMask.IsRaycastLocationValid(position, camera))
                    return false;
                if (parent.TryGetComponent(out Canvas canvas) && (canvas.isRootCanvas || canvas.overrideSorting))
                    break;
            }

            return true;
        }
    }
}
