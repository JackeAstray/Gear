using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(RectDragger)), CanEditMultipleObjects]
    public class RectDraggerEditor : UnityEditor.Editor
    {
        // Properties
        SerializedProperty isDraggable;
        SerializedProperty axisConstraint;
        SerializedProperty boundaryType;
        SerializedProperty boundaryRect;
        SerializedProperty allowOutOfBounds;
        SerializedProperty returnDuration;
        SerializedProperty useUnscaledTime;
        SerializedProperty returnCurve;
        SerializedProperty dragSources;

        void OnEnable()
        {
            isDraggable = serializedObject.FindProperty("isDraggable");
            axisConstraint = serializedObject.FindProperty("axisConstraint");
            boundaryType = serializedObject.FindProperty("boundaryType");
            boundaryRect = serializedObject.FindProperty("boundaryRect");
            allowOutOfBounds = serializedObject.FindProperty("allowOutOfBounds");
            returnDuration = serializedObject.FindProperty("returnDuration");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            returnCurve = serializedObject.FindProperty("returnCurve");
            dragSources = serializedObject.FindProperty("dragSources");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            DrawReferences();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(isDraggable, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(allowOutOfBounds, "Allow Going Out Of Bounds", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (allowOutOfBounds.boolValue || allowOutOfBounds.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Toggle(useUnscaledTime);
                            EvoEditorGUI.Property(returnDuration);
                            EvoEditorGUI.Property(returnCurve, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Property(axisConstraint, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(boundaryType, FieldOptions.WithoutBackground().WithoutSpace());
                        if (boundaryType.enumValueIndex == (int)RectDragger.BoundaryType.RectBounds || boundaryType.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(boundaryRect, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void DrawReferences()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.ArrayProperty(dragSources, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}