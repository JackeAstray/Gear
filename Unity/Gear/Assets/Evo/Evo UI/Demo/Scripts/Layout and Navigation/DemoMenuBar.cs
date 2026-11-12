using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Menu Bar tab: a small shape canvas driven by its menus. File starts a blank canvas or opens a layout, Edit steps
    /// through the history and duplicates or deletes the selected shape, Arrange restacks and centers it, and View holds
    /// custom menu objects (two switches) for the grid.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoMenuBar : MonoBehaviour
    {
        [Serializable]
        public class SceneObject
        {
            public string name;
            [Tooltip("Top-left corner on the canvas, in pixels from its top-left.")]
            public Vector2 position;
            public Vector2 size;
            public float radius = 12f;
            public bool accent;
        }

        [Serializable]
        public class SceneLayout
        {
            public string name;
            public List<SceneObject> objects = new();
        }

        [SerializeField] private MenuBar menuBar;
        [SerializeField] private RectTransform world;
        [SerializeField] private ProceduralShape grid;
        [SerializeField] private DemoSceneObject objectPreset;
        [SerializeField] private List<SceneLayout> scenes = new();

        [Header("Grid")]
        [SerializeField] private float snapStep = 25f;

        class Change
        {
            public Action undo;
            public Action redo;
        }

        readonly List<DemoSceneObject> objects = new();
        readonly List<Change> undoStack = new();
        readonly List<Change> redoStack = new();
        DemoSceneObject selected;
        bool showGrid = true;
        bool snapToGrid;

        public float SnapStep => snapStep;
        public RectTransform World => world;

        void Start()
        {
            if (scenes.Count > 0)
                OpenScene(scenes[0].name);

            ApplyView();
        }

        // Menu items are generated on open: grey out the edits that have nothing to work on,
        // and hand the custom View objects their current values
        void LateUpdate()
        {
            if (!menuBar.IsOpen)
                return;

            foreach (MenuBar.Item item in menuBar.items)
            {
                foreach (ContextMenu.Item entry in item.menuBarItems)
                {
                    if (entry.GeneratedObject == null || !entry.GeneratedObject.activeInHierarchy)
                        continue;

                    if (entry.type == ContextMenu.Item.ItemType.CustomObject && entry.GeneratedObject.TryGetComponent(out DemoMenuControl control))
                        control.Bind(this, entry.name);
                    else if (entry.type == ContextMenu.Item.ItemType.Button && entry.GeneratedObject.TryGetComponent(out Interactive button))
                        button.interactable = IsAvailable(entry.name);
                }
            }
        }

        bool IsAvailable(string entry) => entry switch
        {
            "Undo" => undoStack.Count > 0,
            "Redo" => redoStack.Count > 0,
            "Duplicate" or "Delete" or "Bring to front" or "Send to back" or "Center on canvas" => selected != null,
            _ => true,
        };

        #region File
        public void NewCanvas()
        {
            Clear();
            Select(null);
        }

        public void OpenScene(string scene)
        {
            SceneLayout layout = null;
            for (int i = 0; i < scenes.Count && layout == null; i++)
            {
                if (scenes[i].name == scene)
                    layout = scenes[i];
            }

            if (layout == null)
                return;

            Clear();
            foreach (SceneObject data in layout.objects)
                Spawn(data);

            Select(objects.Count > 0 ? objects[^1] : null);
        }
        #endregion

        #region Edit
        public void Undo()
        {
            if (undoStack.Count == 0)
                return;

            Change change = undoStack[^1];
            undoStack.RemoveAt(undoStack.Count - 1);
            change.undo();
            redoStack.Add(change);
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
                return;

            Change change = redoStack[^1];
            redoStack.RemoveAt(redoStack.Count - 1);
            change.redo();
            undoStack.Add(change);
        }

        public void Duplicate()
        {
            if (selected == null)
                return;

            DemoSceneObject source = selected;
            SceneObject data = source.Data;
            Vector2 position = Vector2.Min(source.Position + new Vector2(30f, 30f), world.rect.size - data.size);
            DemoSceneObject copy = Spawn(new SceneObject { name = data.name, position = position, size = data.size, radius = data.radius, accent = data.accent });
            Select(copy);
            Record(() => Remove(copy), () => { Restore(copy, objects.Count); Select(copy); });
        }

        public void Delete()
        {
            if (selected == null)
                return;

            DemoSceneObject target = selected;
            int index = objects.IndexOf(target);
            Remove(target);
            Record(() => { Restore(target, index); Select(target); }, () => Remove(target));
        }
        #endregion

        #region Arrange
        // Draw order follows the sibling order under the world, which also holds deleted shapes kept for Undo
        public void BringToFront()
        {
            if (selected == null)
                return;

            objects.Remove(selected);
            objects.Add(selected);
            selected.transform.SetAsLastSibling();
        }

        public void SendToBack()
        {
            if (selected == null)
                return;

            objects.Remove(selected);
            objects.Insert(0, selected);
            selected.transform.SetAsFirstSibling();
        }

        public void CenterOnCanvas()
        {
            if (selected == null)
                return;

            DemoSceneObject target = selected;
            Vector2 from = target.Position;
            Vector2 to = (world.rect.size - target.Data.size) * 0.5f;
            target.MoveTo(to, true);
            RecordMove(target, from, to);
        }
        #endregion

        #region View
        public bool ShowGrid
        {
            get => showGrid;
            set
            {
                showGrid = value;
                ApplyView();
            }
        }

        public bool Snap
        {
            get => snapToGrid;
            set => snapToGrid = value;
        }

        void ApplyView() => grid.FillPattern = showGrid;
        #endregion

        #region Options
        public void SetOpenOnHover(bool value) => menuBar.openMenuOnHover = value;

        public void SetBlockUI(bool value)
        {
            menuBar.CloseMenu();
            menuBar.blockUIWhileOpen = value;
        }

        public void SetMenuOffset(float value)
        {
            menuBar.CloseMenu();
            menuBar.itemMenuOffset = new Vector2(0f, -value);
        }
        #endregion

        #region Objects
        public void Select(DemoSceneObject target)
        {
            selected = target;
            foreach (DemoSceneObject sceneObject in objects)
                sceneObject.SetSelected(sceneObject == target);
        }

        public void RecordMove(DemoSceneObject target, Vector2 from, Vector2 to)
        {
            Record(() => target.MoveTo(from, true), () => target.MoveTo(to, true));
        }

        DemoSceneObject Spawn(SceneObject data)
        {
            DemoSceneObject instance = Instantiate(objectPreset, world);
            instance.Setup(this, data);
            objects.Add(instance);
            return instance;
        }

        void Remove(DemoSceneObject target)
        {
            objects.Remove(target);
            target.gameObject.SetActive(false);
            if (selected == target)
                Select(null);
        }

        void Restore(DemoSceneObject target, int index)
        {
            target.gameObject.SetActive(true);
            objects.Insert(Mathf.Clamp(index, 0, objects.Count), target);
            target.transform.SetSiblingIndex(Mathf.Clamp(index, 0, world.childCount - 1));
        }

        void Clear()
        {
            for (int i = 0; i < world.childCount; i++)
                Destroy(world.GetChild(i).gameObject);

            objects.Clear();
            undoStack.Clear();
            redoStack.Clear();
            selected = null;
        }

        void Record(Action undo, Action redo)
        {
            undoStack.Add(new Change { undo = undo, redo = redo });
            redoStack.Clear();
        }
        #endregion
    }
}
