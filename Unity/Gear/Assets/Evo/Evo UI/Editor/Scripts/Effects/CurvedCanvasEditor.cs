using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(CurvedCanvas)), CanEditMultipleObjects]
    public class CurvedCanvasEditor : UnityEditor.Editor
    {
        SerializedProperty curveAngle;
        SerializedProperty segments;
        SerializedProperty curveDirection;

        SerializedProperty rescanIntervalFrames;
        SerializedProperty previewInEditor;


        void OnEnable()
        {
            curveAngle = serializedObject.FindProperty(nameof(curveAngle));
            segments = serializedObject.FindProperty(nameof(segments));
            curveDirection = serializedObject.FindProperty(nameof(curveDirection));
            
            rescanIntervalFrames = serializedObject.FindProperty(nameof(rescanIntervalFrames));
            previewInEditor = serializedObject.FindProperty(nameof(previewInEditor));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            CurveFoldout();
            SettingsFoldout();
            EvoEditorGUI.Callout("Curves visual geometry only. Layout, raycasts, RectMask2D clipping remain planar.");
           
            EvoEditorGUI.EndCenteredInspector();
            if (serializedObject.ApplyModifiedProperties())
            {
                // Defer actual discovery and rebuilding to the normal canvas lifecycle
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        void CurveFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Curve", true, FoldoutOptions.WithIcon("UI_Graphic")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(curveAngle, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(segments, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(curveDirection, "Direction", FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(rescanIntervalFrames, "Discovery Interval", FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(previewInEditor, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}