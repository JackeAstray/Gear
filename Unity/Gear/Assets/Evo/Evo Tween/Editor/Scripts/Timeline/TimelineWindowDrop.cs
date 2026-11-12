using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Objects dragged in from the Hierarchy or the Inspector. Dropped on a track, an object becomes the clip's target;
    /// dropped below the tracks, it gets a new clip for one of its properties, starting at the drop time.
    /// </summary>
    public sealed partial class TimelineWindow
    {
        void HandleTargetDrop(Rect headersRect, Rect canvasRect)
        {
            Event current = Event.current;
            if (current.type == EventType.DragExited)
            {
                ClearDropState();
                return;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;

            ClearDropState();
            Rect area = Rect.MinMaxRect(headersRect.x, headersRect.y, canvasRect.xMax, canvasRect.yMax);
            if (clips == null || !area.Contains(current.mousePosition) || !TryGetDroppedObject(out GameObject gameObject, out Component component))
                return;

            int track = GetTrackAt(current.mousePosition.y - headersRect.y);
            if (track >= 0 && track < clips.arraySize)
            {
                Component target = FindDropTarget(GetClipModule(track)?.TargetType, gameObject, component);
                dropTrack = track;
                dropAccepted = target != null;
                DragAndDrop.visualMode = target != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;

                if (current.type == EventType.DragPerform && target != null)
                {
                    DragAndDrop.AcceptDrag();
                    ClearDropState();
                    SetClipTarget(track, target);
                }
            }
            else if (current.mousePosition.x >= canvasRect.x && HasDropModules(gameObject, component))
            {
                float time = SnapTime(GetTimelineTimeAt(current.mousePosition.x, canvasRect.x));
                dropInsertIndex = FindGroupSlot(time, out float groupStart);
                dropTime = Mathf.Max(time, groupStart);
                dropLabel = $"{gameObject.name} · new clip at {dropTime:0.###}s";
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    int index = dropInsertIndex;
                    float clipTime = dropTime;
                    ClearDropState();
                    ShowDropMenu(gameObject, component, index, clipTime - groupStart, clipTime);
                }
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }

            Repaint();
            current.Use();
        }

        void ClearDropState()
        {
            if (dropTrack < 0 && dropInsertIndex < 0)
                return;

            dropTrack = -1;
            dropAccepted = false;
            dropInsertIndex = -1;
            dropLabel = null;
            Repaint();
        }

        // Objects from the owner's scene, or from the owner's own prefab when the prefab asset is edited directly
        bool TryGetDroppedObject(out GameObject gameObject, out Component component)
        {
            UnityEngine.Object[] objects = DragAndDrop.objectReferences;
            for (int i = 0; owner != null && objects != null && i < objects.Length; i++)
            {
                component = objects[i] as Component;
                gameObject = component != null ? component.gameObject : objects[i] as GameObject;
                if (gameObject != null && gameObject.scene == owner.gameObject.scene
                    && (gameObject.scene.IsValid() || gameObject.transform.root == owner.transform.root))
                    return true;
            }

            gameObject = null;
            component = null;
            return false;
        }

        // A dragged component wins when it fits, otherwise the object's first component of the module's target type
        static Component FindDropTarget(Type targetType, GameObject gameObject, Component preferred)
        {
            if (targetType == null || gameObject == null)
                return null;

            return preferred != null && targetType.IsInstanceOfType(preferred) ? preferred : gameObject.GetComponent(targetType);
        }

        static bool HasDropModules(GameObject gameObject, Component component)
        {
            IReadOnlyList<TweenClipGUI.ModuleTypeInfo> types = TweenClipGUI.GetModuleTypes();
            for (int i = 0; i < types.Count; i++)
            {
                if (FindDropTarget(types[i].TargetType, gameObject, component) != null)
                    return true;
            }
            return false;
        }

        // Lists only the modules the dropped object has a target for, in the Add Clip order
        void ShowDropMenu(GameObject gameObject, Component component, int index, float delay, float time)
        {
            GenericMenu menu = new();
            // Menu paths treat a slash as a submenu
            menu.AddDisabledItem(new GUIContent($"Add a clip for {gameObject.name.Replace('/', '∕')} at {time:0.###}s"));
            menu.AddSeparator(string.Empty);

            IReadOnlyList<TweenClipGUI.ModuleTypeInfo> types = TweenClipGUI.GetModuleTypes();
            for (int i = 0; i < types.Count; i++)
            {
                Component target = FindDropTarget(types[i].TargetType, gameObject, component);
                if (target == null)
                    continue;

                Type moduleType = types[i].Type;
                menu.AddItem(new GUIContent(types[i].MenuPath), false, () => AddDroppedClip(moduleType, target, index, delay));
            }

            menu.ShowAsContext();
        }

        // Joins the flow group under the drop time as its last clip, so earlier groups keep their timing
        void AddDroppedClip(Type moduleType, Component target, int index, float delay)
        {
            if (clips == null || target == null)
                return;

            PerformEdit("Add Tween Clip", () => InsertClip(Mathf.Min(index, clips.arraySize), target,
                Activator.CreateInstance(moduleType) as TweenModule, ClipFlow.Parallel, delay, DefaultClipDuration), structural: true);
        }

        void SetClipTarget(int index, Component target)
        {
            PerformEdit("Set Tween Clip Target", () =>
            {
                SerializedProperty property = GetTargetProperty(index);
                if (property != null)
                    property.objectReferenceValue = GetStoredTarget(target);
                SelectTrack(index);
            });
        }

        void DrawDropHighlight(Rect rowRect)
        {
            DrawHighlight(new Rect(rowRect.x + 1f, rowRect.y + 1f, rowRect.width - 2f, rowRect.height - 2f),
                dropAccepted ? AccentColor : DropRejectedColor);
        }

        // The new clip's start as a labeled pill on the line where its row will be inserted
        void DrawDropGhost(Rect canvasRect)
        {
            DrawInsertionLine(dropInsertIndex, canvasRect);
            float lineY = dropInsertIndex * Layout.TrackHeight - verticalScroll;
            float pillY = Mathf.Clamp(lineY - Layout.DragTimePillHeight * 0.5f, 0f, Mathf.Max(0f, canvasRect.height - Layout.DragTimePillHeight));
            DrawTimePill(TimeToX(dropTime), pillY, dropLabel, canvasRect.width, AccentColor);
        }
    }
}
