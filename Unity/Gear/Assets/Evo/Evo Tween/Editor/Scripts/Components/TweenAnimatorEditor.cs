using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    [CustomEditor(typeof(TweenAnimator))]
    public sealed class TweenAnimatorEditor : UnityEditor.Editor
    {
        TweenAnimator Animator => (TweenAnimator)target;

        SerializedProperty states;
        SerializedProperty selectedState;
        SerializedProperty sequence;
        TweenClipGUI clipListGUI;
        TweenSequencePreviewGUI previewGUI;
        int selectedIndex = -1;
        bool triggersChanged;

        enum StateAction { Add, Duplicate, Delete, Up, Down }

        public static string GetSequencePath(int index) => $"states.Array.data[{index}].sequence";

        void OnEnable()
        {
            clipListGUI = new TweenClipGUI();
            Animator.EditorStateChanged += OnStateChanged;
            Undo.undoRedoPerformed += OnStateChanged;
            BindState();
        }

        void OnDisable()
        {
            Animator.EditorStateChanged -= OnStateChanged;
            Undo.undoRedoPerformed -= OnStateChanged;
            previewGUI?.Dispose();
        }

        void OnStateChanged()
        {
            BindState();
            Repaint();
        }

        void BindState()
        {
            previewGUI?.Dispose();
            previewGUI = null;
            serializedObject.Update();
            states = serializedObject.FindProperty("states");
            selectedIndex = Animator.EditorStateIndex;
            selectedState = selectedIndex >= 0 && selectedIndex < states.arraySize ? states.GetArrayElementAtIndex(selectedIndex) : null;
            sequence = selectedState?.FindPropertyRelative("sequence");
         
            if (selectedState != null)
                previewGUI = new TweenSequencePreviewGUI(this, Animator, selectedIndex);
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            if (selectedIndex != Animator.EditorStateIndex)
                BindState();

            EvoEditorGUI.BeginCenteredInspector();
            StateSection();
            previewGUI?.DrawControls(addFoldoutSpace: true);
            triggersChanged = false;
            EditorGUI.BeginChangeCheck();
            if (selectedState != null)
            {
                TriggerFoldout();
                PlaybackFoldout();
                clipListGUI.DrawSequence(serializedObject, sequence, BeginLiveEdit, EndLiveEdit);
            }
            bool controlsChanged = EditorGUI.EndChangeCheck();
            EvoEditorGUI.EndCenteredInspector();
            ApplyPendingChanges(controlsChanged);
        }

        void StateSection()
        {
            EvoEditorGUI.BeginRow();
            EvoEditorGUI.BeginContainer("Animation States", padding: 6);
            {
                GUILayout.BeginHorizontal();
                {
                    int defaultIndex = serializedObject.FindProperty("defaultState").intValue;
                    using (new EditorGUI.DisabledScope(selectedState == null))
                    {
                        Rect stateRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + EvoEditorGUI.Metrics.ControlHeightOffset, GUIStyle.none);
                        string label = selectedState != null ? GetStateLabel(selectedIndex, defaultIndex) : "No States";
                        if (EvoEditorGUI.Button(stateRect, label, IconLibrary.Expand, ButtonOptions.WithInvertedColor()
                            .WithIconPosition(IconPosition.AfterText).WithContentAlignment(ContentAlignment.SpaceBetween)))
                        {
                            GenericMenu menu = new();
                            for (int i = 0; i < states.arraySize; i++)
                                menu.AddItem(new GUIContent(GetStateLabel(i, defaultIndex)), i == selectedIndex, SelectState, i);

                            menu.DropDown(stateRect);
                        }
                    }

                    EvoEditorGUI.AddFoldoutSpace();
                    if (EvoEditorGUI.Button("", EvoEditorGUI.GetIcon("Toolbar Plus", false), ButtonOptions.WithIconSize(14).WithWidth(26f).WithTooltip("Add a state").WithInvertedColor()))
                    {
                        ChangeStates(StateAction.Add);
                        GUIUtility.ExitGUI();
                    }

                    using (new EditorGUI.DisabledScope(selectedState == null))
                    {
                        EvoEditorGUI.AddFoldoutSpace();
                        if (EvoEditorGUI.Button("", IconLibrary.More, ButtonOptions.WithIconSize(14).WithWidth(26f).WithTooltip("Manage selected state").WithInvertedColor()))
                        {
                            GenericMenu menu = new();
                            menu.AddItem(new GUIContent("Set as Default"), selectedIndex == defaultIndex, SetDefault);
                            menu.AddItem(new GUIContent("Duplicate"), false, () => ChangeStates(StateAction.Duplicate));

                            if (selectedIndex > 0) { menu.AddItem(new GUIContent("Move Up"), false, () => ChangeStates(StateAction.Up)); }
                            else { menu.AddDisabledItem(new GUIContent("Move Up")); }

                            if (selectedIndex + 1 < states.arraySize) { menu.AddItem(new GUIContent("Move Down"), false, () => ChangeStates(StateAction.Down)); }
                            else { menu.AddDisabledItem(new GUIContent("Move Down")); }

                            menu.AddSeparator(string.Empty);
                            menu.AddItem(new GUIContent("Delete"), false, () => ChangeStates(StateAction.Delete));
                            menu.ShowAsContext();
                        }
                    }
                }
                GUILayout.EndHorizontal();

                if (Application.isPlaying && Animator.ActiveState != null)
                {
                    EditorGUILayout.LabelField($"Active: {Animator.ActiveState.id}  ·  {Animator.State}", EditorStyles.miniLabel);
                    GUILayout.Space(-3);
                }
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndRow();
            EvoEditorGUI.AddFoldoutSpace();
          
            if (selectedState == null)
                EvoEditorGUI.Callout("Add a state to author its triggers, playback, and clips.", spaceBefore: 0, spaceAfter: 0);
        }

        string GetStateLabel(int index, int defaultIndex)
        {
            string id = states.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue;
            return $"{index + 1}: {id}" + (index == defaultIndex ? " (Default)" : string.Empty);
        }

        void SelectState(object index)
        {
            if (target != null)
                Animator.EditorStateIndex = (int)index;
        }

        void TriggerFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "State", true, FoldoutOptions.WithIcon("Tween_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    SerializedProperty id = selectedState.FindPropertyRelative("id");
                    EvoEditorGUI.Property(id, "State ID", FieldOptions.WithInvertedColor());
                    bool duplicate = false;
                    for (int i = 0; i < states.arraySize; i++)
                    {
                        if (i != selectedIndex && states.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id.stringValue)
                            duplicate = true;
                    }

                    if (string.IsNullOrWhiteSpace(id.stringValue) || duplicate)
                    {
                        EvoEditorGUI.Callout(duplicate
                            ? "IDs must be unique. ID lookup selects the first match."
                            : "Enter a state ID for scripting and UnityEvents.", CalloutType.Warning, spaceBefore: 0, spaceAfter: 3);
                    }

                    EditorGUI.BeginChangeCheck();
                    SerializedProperty trigger = selectedState.FindPropertyRelative("trigger");
                    EvoEditorGUI.Property(trigger, "Trigger", FieldOptions.WithInvertedColor());
                    TweenAnimator.TriggerMode mode = (TweenAnimator.TriggerMode)trigger.enumValueIndex;
                    if (mode == TweenAnimator.TriggerMode.PointerEvent)
                    {
                        DrawFlags("pointerEvents", "Events", false);
                        EvoEditorGUI.AddLayoutSpace();
                        DrawStateProperty("pointerButton", "Mouse Button");
                    }
                    else if (mode == TweenAnimator.TriggerMode.Interaction)
                    {
                        SerializedProperty sourceProperty = serializedObject.FindProperty("interactionSource");
                        EvoEditorGUI.Property(sourceProperty, "Interaction Source", FieldOptions.WithInvertedColor());
                        DrawFlags("interactionStates", "Interaction States", true);
                        EvoEditorGUI.AddLayoutSpace();
                        Component source = sourceProperty.objectReferenceValue as Component;
                        if (source == null || source.GetType().GetInterface("Evo.UI.IStylerInteractable") == null)
                        {
                            EvoEditorGUI.Callout("Choose a component that implements IStylerInteractable, such as an Evo UI Button.",
                                CalloutType.Warning, spaceBefore: 0, spaceAfter: 3);
                        }
                    }
                    EvoEditorGUI.Toggle(selectedState.FindPropertyRelative("restartOnTrigger"), "Restart On Trigger", FieldOptions.WithInvertedColor().WithoutSpace());
                    triggersChanged |= EditorGUI.EndChangeCheck();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void PlaybackFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Playback", true, FoldoutOptions.WithIcon("Tween_Playback")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(serializedObject.FindProperty("playOnEnable"), FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(selectedState.FindPropertyRelative("restoreOnStop"), FieldOptions.WithInvertedColor());
                    DrawStateProperty("playbackSpeed", "Playback Speed");
                    DrawStateProperty("updatePhase", "Update Phase");
                    DrawStateProperty("loop", "Loop");
                    if ((LoopMode)selectedState.FindPropertyRelative("loop").enumValueIndex != LoopMode.None)
                        DrawStateProperty("iterations", "Iterations");
                    GUILayout.Space(-EvoEditorGUI.Metrics.LayoutSpacing);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(true);
        }

        void DrawStateProperty(string name, string label) => EvoEditorGUI.Property(selectedState.FindPropertyRelative(name), label, FieldOptions.WithInvertedColor());

        void DrawFlags(string name, string label, bool interaction)
        {
            SerializedProperty property = selectedState.FindPropertyRelative(name);
            EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.AddPropertySpace();
                Rect rect = EditorGUILayout.GetControlRect();
                GUIContent content = new(label);
                EditorGUI.BeginProperty(rect, content, property);
                if (interaction)
                    property.intValue = (int)(TweenAnimator.InteractionTrigger)EditorGUI.EnumFlagsField(rect, content, (TweenAnimator.InteractionTrigger)property.intValue);
                else
                    property.intValue = (int)(TweenAnimator.PointerTrigger)EditorGUI.EnumFlagsField(rect, content, (TweenAnimator.PointerTrigger)property.intValue);
                EditorGUI.EndProperty();
            }
            EvoEditorGUI.EndRow();
        }

        void SetDefault()
        {
            serializedObject.Update();
            serializedObject.FindProperty("defaultState").intValue = selectedIndex;
            serializedObject.ApplyModifiedProperties();
        }

        void ChangeStates(StateAction action)
        {
            previewGUI?.StopPreview();
            Undo.RecordObject(Animator, "Edit Tween Animator States");
            List<TweenAnimator.AnimationState> items = (List<TweenAnimator.AnimationState>)Animator.States;
            TweenAnimator.AnimationState defaultItem = Animator.GetState(Animator.DefaultStateIndex);
            int next = selectedIndex;
            if (action == StateAction.Add || action == StateAction.Duplicate)
            {
                TweenAnimator.AnimationState item = new();
                if (action == StateAction.Duplicate && selectedIndex >= 0)
                {
                    TweenAnimator.AnimationState source = items[selectedIndex];
                    item = new TweenAnimator.AnimationState
                    {
                        trigger = source.trigger, pointerEvents = source.pointerEvents, pointerButton = source.pointerButton,
                        interactionStates = source.interactionStates, restartOnTrigger = source.restartOnTrigger,
                        loop = source.loop, iterations = source.iterations, playbackSpeed = source.playbackSpeed,
                        updatePhase = source.updatePhase,
                        restoreOnStop = source.restoreOnStop, sequence = source.sequence.Clone()
                    };
                }
                int suffix = items.Count + 1;
                do { item.id = $"State {suffix++}"; } 
                while (Animator.GetStateIndex(item.id) >= 0);
                next = items.Count;
                items.Add(item);
            }
            else if (action == StateAction.Delete)
            {
                items.RemoveAt(selectedIndex);
                next = Mathf.Min(selectedIndex, items.Count - 1);
            }
            else
            {
                next += action == StateAction.Up ? -1 : 1;
                (items[selectedIndex], items[next]) = (items[next], items[selectedIndex]);
            }

            serializedObject.Update();
            serializedObject.FindProperty("defaultState").intValue = Mathf.Max(0, items.IndexOf(defaultItem));
            serializedObject.ApplyModifiedProperties();
            Animator.EditorStateIndex = next;
          
            EditorUtility.SetDirty(Animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Animator);
         
            if (Application.isPlaying)
            {
                Animator.PrepareStates();
                Animator.RebindTriggers();
            }

            BindState();
            Repaint();
        }

        void ApplyPendingChanges(bool controlsChanged)
        {
            bool hasChanges = controlsChanged || serializedObject.hasModifiedProperties;
            if (hasChanges)
                BeginLiveEdit();

            serializedObject.ApplyModifiedProperties();
            if (hasChanges && !clipListGUI.IsDragging)
            {
                EndLiveEdit();
                if (Application.isPlaying && triggersChanged)
                    Animator.RebindTriggers();
            }
        }

        void BeginLiveEdit() => previewGUI?.BeginLiveEdit();

        void EndLiveEdit() => previewGUI?.EndLiveEdit();
    }
}