using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(InkPanel)), CanEditMultipleObjects]
    public class InkPanelEditor : UnityEditor.Editor
    {
        // Graphic
        SerializedProperty color;
        SerializedProperty raycastTarget;
        SerializedProperty raycastPadding;
        SerializedProperty maskable;

        // Ink
        SerializedProperty reveal;
        SerializedProperty origin;
        SerializedProperty roundness;
        SerializedProperty roughness;
        SerializedProperty droplets;
        SerializedProperty dropletScale;
        SerializedProperty detailScale;
        SerializedProperty noiseTexture;

        // Animation
        SerializedProperty speed;
        SerializedProperty motion;
        SerializedProperty seed;

        // Preview
        InkPanel previewTarget;
        double previewStarted;
        const float RevealDuration = 1.1f;
        const float PreviewDuration = 3.5f;

        void OnEnable()
        {
            color = serializedObject.FindProperty("m_Color");
            raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            maskable = serializedObject.FindProperty("m_Maskable");

            reveal = serializedObject.FindProperty("reveal");
            origin = serializedObject.FindProperty("origin");
            roundness = serializedObject.FindProperty("roundness");
            roughness = serializedObject.FindProperty("roughness");
            droplets = serializedObject.FindProperty("droplets");
            dropletScale = serializedObject.FindProperty("dropletScale");
            detailScale = serializedObject.FindProperty("detailScale");
            noiseTexture = serializedObject.FindProperty("noiseTexture");

            speed = serializedObject.FindProperty("speed");
            motion = serializedObject.FindProperty("motion");
            seed = serializedObject.FindProperty("seed");
        }

        void OnDisable() => StopPreview();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            GraphicFoldout();
            SettingsFoldout();
            AnimationFoldout();
            PreviewControls();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void GraphicFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Graphic", true, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(color, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(maskable, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(raycastTarget, FieldOptions.WithInvertedColor());
                EvoEditorGUI.ArrayProperty(raycastPadding, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(origin, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(reveal, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(roundness, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(roughness, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(droplets, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(dropletScale, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(detailScale, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(noiseTexture, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animation", true, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(speed, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(motion, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(seed, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void PreviewControls()
        {
            using (new EditorGUI.DisabledScope(targets.Length != 1 || EditorApplication.isPlayingOrWillChangePlaymode
                || !((InkPanel)target).isActiveAndEnabled))
            {
                EvoEditorGUI.AddFoldoutSpace();

                if (!EvoEditorGUI.Button(previewTarget != null ? "Stop Preview" : "Preview Reveal"))
                    return;

                if (previewTarget != null) { StopPreview(); }
                else
                {
                    serializedObject.ApplyModifiedProperties();
                    previewTarget = (InkPanel)target;
                    previewStarted = EditorApplication.timeSinceStartup;
                    previewTarget.SetEditorRevealPreview(0f);
                    EditorApplication.update += PreviewTick;
                }
            }
        }

        void StopPreview()
        {
            EditorApplication.update -= PreviewTick;
            if (previewTarget != null)
                previewTarget.SetEditorRevealPreview(-1f);

            previewTarget = null;
        }

        void PreviewTick()
        {
            if (previewTarget == null || !previewTarget.isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode
                || previewTarget.material != previewTarget.defaultMaterial)
            {
                StopPreview();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - previewStarted;
            if (elapsed >= PreviewDuration)
                StopPreview();
            else
                previewTarget.SetEditorRevealPreview(Mathf.Clamp01((float)elapsed / RevealDuration));

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }
    }
}
