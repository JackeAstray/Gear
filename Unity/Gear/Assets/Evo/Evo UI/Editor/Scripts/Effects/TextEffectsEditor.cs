using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(TextEffects)), CanEditMultipleObjects]
    public class TextEffectsEditor : UnityEditor.Editor
    {
        SerializedProperty useUnscaledTime;
        EffectProperties typewriter;
        EffectProperties scramble;
        EffectProperties[] colorEffects;
        EffectProperties[] motionEffects;
        EffectProperties[] emphasisEffects;
        TextEffects[] components;
    
        bool previewing;
        double lastPreviewTime;

        void OnEnable()
        {
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            typewriter = new(serializedObject.FindProperty("typewriter"), "Typewriter", false);
            scramble = CreateEffect("scramble", "Scramble");
            colorEffects = new[]
            {
                CreateEffect("palette", "Palette"),
                CreateEffect("alphaPulse", "Alpha Pulse"),
                CreateEffect("shimmer", "Shimmer")
            };
            motionEffects = new[]
            {
                CreateEffect("wave", "Wave"),
                CreateEffect("bounce", "Bounce"),
                CreateEffect("shake", "Shake"),
                CreateEffect("glitch", "Glitch")
            };
            emphasisEffects = new[] {
                CreateEffect("pulse", "Pulse"),
                CreateEffect("swing", "Swing"),
                CreateEffect("wiggle", "Wiggle")
            };

            components = new TextEffects[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                components[i] = (TextEffects)targets[i];

            Undo.undoRedoPerformed += RefreshTargets;
        }

        void OnDisable()
        {
            StopPreview();
            Undo.undoRedoPerformed -= RefreshTargets;
        }

        EffectProperties CreateEffect(string property, string label) => new(serializedObject.FindProperty(property), label, true);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            PlaybackFoldout();
            EffectsFoldout("Color & Overlay", colorEffects);
            EffectsFoldout("Motion", motionEffects);
            EffectsFoldout("Emphasis", emphasisEffects);
            PreviewControls();

            EvoEditorGUI.EndCenteredInspector();
            if (serializedObject.ApplyModifiedProperties())
                RefreshTargets();
        }

        void PlaybackFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Playback", true, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor().WithoutSpace());
                    EvoEditorGUI.AddPropertySpace();
                    DrawEffect(typewriter);
                    EvoEditorGUI.AddPropertySpace();
                    DrawEffect(scramble);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EffectsFoldout(string title, EffectProperties[] effects)
        {
            if (EvoEditorGUI.BeginFoldout(target, title, true))
            {
                EvoEditorGUI.BeginContainer();
                for (int i = 0; i < effects.Length; i++)
                {
                    DrawEffect(effects[i]);
                    if (i < effects.Length - 1)
                        EvoEditorGUI.AddPropertySpace();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawEffect(EffectProperties effect)
        {
            if (EvoEditorGUI.BeginToggleFoldout(effect.Enabled, effect.Label, options: FoldoutOptions.WithInvertedColor().WithoutBoldText()))
            {
                EvoEditorGUI.BeginContainer(nestDepth: 1);
                {
                    if (effect.Scope != null)
                    {
                        EvoEditorGUI.Property(effect.Scope);

                        if (!effect.Scope.hasMultipleDifferentValues
                            && effect.Scope.enumValueIndex == (int)TextEffects.EffectScope.TaggedCharacters)
                            EvoEditorGUI.Callout(effect.TagHint, spaceBefore: 0, spaceAfter: 3);
                    }

                    for (int i = 0; i < effect.Fields.Length; i++)
                    {
                        SerializedProperty field = effect.Fields[i];
                        if (effect.ScrambleMode != null && !effect.ScrambleMode.hasMultipleDifferentValues)
                        {
                            bool continuous = effect.ScrambleMode.enumValueIndex == (int)TextEffects.ScrambleMode.Continuous;
                            bool nonstop = continuous && !effect.RestDuration.hasMultipleDifferentValues && effect.RestDuration.floatValue <= 0f;
                            if (nonstop && (field.name == "duration" || field.name == "revealOrder" || field.name == "stagger")
                                || !continuous && (field.name == "intensity" || field.name == "restDuration"))
                                continue;
                        }

                        if (field.name == "gradient" && effect.ColorMode != null && !effect.ColorMode.hasMultipleDifferentValues
                            && effect.ColorMode.enumValueIndex == (int)TextEffects.ColorMode.Base)
                            continue;

                        if ((field.name == "audioSource" || field.name == "volume") && effect.AudioClip != null
                            && effect.AudioClip.objectReferenceValue == null && !effect.AudioClip.hasMultipleDifferentValues)
                        {
                            GUILayout.Space(-1f);
                            continue;
                        }

                        FieldOptions options = i == effect.Fields.Length - 1 ? FieldOptions.WithoutSpace() : default;
                        if (field.propertyType == SerializedPropertyType.Boolean)
                            EvoEditorGUI.Toggle(field, options);
                        else
                            EvoEditorGUI.Property(field, options);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndToggleFoldout(false);
        }

        void PreviewControls()
        {
            GUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!CanPreview() || Application.isPlaying))
            {
                Texture2D icon = EvoEditorGUI.GetIcon(previewing ? EditorTools.IconLibrary.Refresh : EditorTools.IconLibrary.Play);
                if (EvoEditorGUI.Button(previewing ? "Restart Preview" : "Play Preview", icon))
                {
                    serializedObject.ApplyModifiedProperties();
                    StartPreview();
                }

                if (previewing)
                    EvoEditorGUI.AddFoldoutSpace();
            }

            if (previewing && EvoEditorGUI.Button("Stop Preview", EditorTools.IconLibrary.Stop))
                StopPreview();

            GUILayout.EndHorizontal();
        }

        bool CanPreview()
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].isActiveAndEnabled && !EditorUtility.IsPersistent(components[i]))
                    return true;
            }

            return false;
        }

        void StartPreview()
        {
            StopPreview();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && !EditorUtility.IsPersistent(components[i]))
                    components[i].StartPreview();
            }

            previewing = true;
            lastPreviewTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.quitting += StopPreview;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        void StopPreview()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.quitting -= StopPreview;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
        
            if (!previewing)
                return;

            previewing = false;

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    components[i].StopPreview();
            }
          
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }

        void UpdatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !CanPreview())
            {
                StopPreview();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)Math.Min(now - lastPreviewTime, 0.1d);
            lastPreviewTime = now;

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    components[i].UpdatePreview(delta);
            }
         
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void RefreshTargets()
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    components[i].Refresh();
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        void OnPlayModeChanged(PlayModeStateChange state) => StopPreview();
     
        void OnSceneSaving(Scene scene, string path) => StopPreview();
     
        void OnPrefabSaving(GameObject prefab) => StopPreview();

        sealed class EffectProperties
        {
            public readonly string Label;
            public readonly string TagHint;
            public readonly SerializedProperty Enabled;
            public readonly SerializedProperty Scope;
            public readonly SerializedProperty AudioClip;
            public readonly SerializedProperty ColorMode;
            public readonly SerializedProperty ScrambleMode;
            public readonly SerializedProperty RestDuration;
            public readonly SerializedProperty[] Fields;

            public EffectProperties(SerializedProperty property, string label, bool scoped)
            {
                Label = label;
                string tag = property.name.ToLowerInvariant();
                TagHint = "Use <" + tag + ">Text</" + tag + ">. Tags can overlap or nest.";
                Enabled = property.FindPropertyRelative("enabled");
                Scope = scoped ? property.FindPropertyRelative("scope") : null;
                AudioClip = property.FindPropertyRelative("audioClip");
                ColorMode = property.FindPropertyRelative("colorMode");
                ScrambleMode = property.name == "scramble" ? property.FindPropertyRelative("mode") : null;
                RestDuration = property.FindPropertyRelative("restDuration");
                List<SerializedProperty> fields = new();
                SerializedProperty iterator = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iterator, end))
                            break;
                        if (iterator.name != "enabled" && iterator.name != "scope")
                            fields.Add(iterator.Copy());
                    }
                    while (iterator.NextVisible(false));
                }
                Fields = fields.ToArray();
            }
        }
    }
}