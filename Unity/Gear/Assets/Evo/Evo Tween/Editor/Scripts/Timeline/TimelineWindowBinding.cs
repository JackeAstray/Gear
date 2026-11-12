using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    public sealed partial class TimelineWindow
    {
        /// <summary>
        /// Owner discovery and serialized binding for inline and preset-backed sequences.
        /// </summary>
        void Bind(Component value, string requestedPropertyPath = null, bool force = false, bool reportMissingPath = false)
        {
            if (value is TweenAnimator selectedAnimator && string.IsNullOrEmpty(requestedPropertyPath))
                requestedPropertyPath = TweenAnimatorEditor.GetSequencePath(selectedAnimator.EditorStateIndex);

            bool sameProperty = string.IsNullOrEmpty(requestedPropertyPath) || requestedPropertyPath == sequencePropertyPath;
            if (!force && owner == value && sameProperty && sequenceOwner != null && sequenceOwner.targetObject != null)
                return;

            if (owner != value)
                frameTimelineOnNextLayout = true;

            StopRecording();
            ResetPointerState();
            previewGUI?.Dispose();
            previewGUI = null;
            owner = value;
            sequenceOwner = null;
            definitionOwner = null;
            sequence = null;
            sequencePreset = null;
            inlineClips = null;
            targetBindings = null;
            playbackSpeed = null;
            sequenceField = null;
            clips = null;
            trackToReveal = -1;
            activePreset = null;
            sequencePropertyPaths.Clear();
            sequenceDisplayNames = Array.Empty<string>();
            clipDragMode = ClipDragMode.None;
            draggedClips.Clear();
            clipTimingsDirty = true;

            if (owner != null)
            {
                sequenceOwner = new SerializedObject(owner);
                CollectSequenceProperties();

                string nextPropertyPath = ResolveSequencePropertyPath(requestedPropertyPath, reportMissingPath);
                sequencePropertyPath = nextPropertyPath;
                sequence = string.IsNullOrEmpty(nextPropertyPath) ? null : sequenceOwner.FindProperty(nextPropertyPath);
                sequenceField = string.IsNullOrEmpty(nextPropertyPath) ? null : FindField(owner.GetType(), nextPropertyPath);
                sequencePreset = sequence?.FindPropertyRelative("preset");
                inlineClips = sequence?.FindPropertyRelative("clips");
                targetBindings = sequence?.FindPropertyRelative("presetTargets");
                int stateIndex = owner is TweenAnimator ? sequencePropertyPaths.IndexOf(nextPropertyPath) : -1;
                playbackSpeed = stateIndex >= 0 ? sequenceOwner.FindProperty($"states.Array.data[{stateIndex}].playbackSpeed") : null;
                detailsGUI ??= new TweenClipGUI(showHeaderIcon: false, showItemIcons: false, invertColors: true);
                BindDefinitionSource(force: true);

                if (owner is TweenAnimator animator && stateIndex >= 0)
                {
                    animator.EditorStateIndex = stateIndex;
                    previewGUI = new TweenSequencePreviewGUI(this, animator, stateIndex);
                }
                else if (sequenceField != null)
                    previewGUI = new TweenSequencePreviewGUI(this, owner, GetBoundSequence);

                if (previewGUI != null)
                    previewGUI.Loop = loopPreview;
            }

            ClampSelection();
            Repaint();
        }

        void CollectSequenceProperties()
        {
            sequencePropertyPaths.Clear();
            List<string> displayNames = new();
            if (sequenceOwner == null)
            {
                sequenceDisplayNames = Array.Empty<string>();
                return;
            }

            if (owner is TweenAnimator animator)
            {
                for (int i = 0; i < animator.States.Count; i++)
                {
                    sequencePropertyPaths.Add(TweenAnimatorEditor.GetSequencePath(i));
                    displayNames.Add($"{i + 1}: {animator.GetState(i)?.id}");
                }
                sequenceDisplayNames = displayNames.ToArray();
                return;
            }

            foreach (SerializedProperty property in GetSequenceProperties(sequenceOwner))
            {
                sequencePropertyPaths.Add(property.propertyPath);
                displayNames.Add(property.displayName);
            }

            sequenceDisplayNames = displayNames.ToArray();
        }

        // Only directly serialized fields: nested sequences can't be opened by a top-level property path
        static IEnumerable<SerializedProperty> GetSequenceProperties(SerializedObject serializedObject)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.depth == 0 && iterator.type == nameof(TweenSequence))
                    yield return iterator;
            }
        }

        string ResolveSequencePropertyPath(string requestedPropertyPath, bool reportMissingPath)
        {
            if (sequencePropertyPaths.Count == 0)
                return string.Empty;
            if (string.IsNullOrEmpty(requestedPropertyPath))
                return sequencePropertyPaths[0];
            if (sequencePropertyPaths.Contains(requestedPropertyPath))
                return requestedPropertyPath;

            if (reportMissingPath)
            {
                Debug.LogWarning($"{owner.GetType().Name} does not contain a directly serialized " +
                    $"{nameof(TweenSequence)} field at '{requestedPropertyPath}'. Opening " +
                    $"'{sequenceDisplayNames[0]}' instead.", owner);
            }

            return sequencePropertyPaths[0];
        }

        TweenSequence GetBoundSequence()
        {
            if (owner is TweenAnimator animator)
                return animator.GetState(sequencePropertyPaths.IndexOf(sequencePropertyPath))?.sequence;

            return owner != null && sequenceField != null ? sequenceField.GetValue(owner) as TweenSequence : null;
        }

        static FieldInfo FindField(Type type, string fieldName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            while (type != null && type != typeof(MonoBehaviour))
            {
                FieldInfo field = type.GetField(fieldName, Flags);
                if (field != null)
                    return field;

                type = type.BaseType;
            }
            return null;
        }

        void BindDefinitionSource(bool force = false)
        {
            if (sequenceOwner == null || sequence == null)
                return;

            sequenceOwner.UpdateIfRequiredOrScript();
            TweenSequencePreset nextPreset = sequencePreset?.objectReferenceValue as TweenSequencePreset;
            if (!force && nextPreset == activePreset && definitionOwner != null && clips != null)
                return;

            activePreset = nextPreset;
            if (activePreset == null)
            {
                definitionOwner = sequenceOwner;
                clips = inlineClips;
            }
            else
            {
                definitionOwner = new SerializedObject(activePreset);
                definitionOwner.UpdateIfRequiredOrScript();
                clips = definitionOwner.FindProperty("clips");

                if (SynchronizeTargetBindings())
                    sequenceOwner.ApplyModifiedProperties();
            }

            clipTimingsDirty = true;
            ClampSelection();
        }

        void UpdateSerializedSources()
        {
            if (sequenceOwner?.targetObject == null)
                return;

            bool sourcesChanged = sequenceOwner.UpdateIfRequiredOrScript();
            if (owner is TweenAnimator animator)
            {
                string selectedPath = TweenAnimatorEditor.GetSequencePath(animator.EditorStateIndex);
                if (sequencePropertyPath != selectedPath || sequencePropertyPaths.Count != animator.States.Count)
                {
                    Bind(owner, selectedPath, force: true);
                    return;
                }

                if (sourcesChanged)
                    CollectSequenceProperties();
            }

            TweenSequencePreset serializedPreset = sequencePreset?.objectReferenceValue as TweenSequencePreset;
            if (serializedPreset != activePreset)
            {
                BindDefinitionSource(force: true);
                sourcesChanged = true;
            }

            if (definitionOwner != null && definitionOwner != sequenceOwner)
                sourcesChanged |= definitionOwner.UpdateIfRequiredOrScript();

            if (SynchronizeTargetBindings())
            {
                sequenceOwner.ApplyModifiedProperties();
                sequenceOwner.UpdateIfRequiredOrScript();
                sourcesChanged = true;
            }

            clipTimingsDirty |= sourcesChanged;
            if (clipTimingsDirty)
            {
                BuildClipTimings();
                ClampSelection();
            }
        }

        bool SynchronizeTargetBindings() => activePreset != null && clips != null
            && TweenClipGUI.SynchronizeTargetBindings(targetBindings, inlineClips, clips.arraySize);

        static Component FindSelectedOwner()
        {
            if (Selection.activeObject is Component component && component != null)
                return FindOwner(component.gameObject, component);

            return FindOwner(Selection.activeGameObject);
        }

        static Component FindOwner(UnityEngine.Object[] objects)
        {
            if (objects == null)
                return null;

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is Component component && component != null)
                {
                    Component componentOwner = FindOwner(component.gameObject, component);
                    if (componentOwner != null)
                        return componentOwner;
                }
                else if (objects[i] is GameObject gameObject)
                {
                    Component gameObjectOwner = FindOwner(gameObject);
                    if (gameObjectOwner != null)
                        return gameObjectOwner;
                }
            }
            return null;
        }

        static Component FindOwner(GameObject gameObject, Component preferred = null)
        {
            if (gameObject == null)
                return null;
            if (preferred != null && HasSequence(preferred))
                return preferred;

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i] != preferred && HasSequence(components[i]))
                    return components[i];
            }
            return null;
        }

        static bool HasSequence(Component component)
        {
            if (component == null)
                return false;
            if (component is TweenAnimator)
                return true;

            foreach (SerializedProperty _ in GetSequenceProperties(new SerializedObject(component)))
                return true;

            return false;
        }
    }
}