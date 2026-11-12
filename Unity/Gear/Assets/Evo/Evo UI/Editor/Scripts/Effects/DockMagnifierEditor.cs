using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(DockMagnifier)), CanEditMultipleObjects]
    public class DockMagnifierEditor : UnityEditor.Editor
    {
        SerializedProperty baseScale;
        SerializedProperty maxScale;
        SerializedProperty scaleDirection;
        SerializedProperty influenceRadius;
        SerializedProperty axisBias;
        SerializedProperty smoothTime;
        SerializedProperty orientation;
        SerializedProperty alwaysUpdateOnHover;
        SerializedProperty requirePointerInsideForRefresh;
        SerializedProperty ignoredRects;


        void OnEnable()
        {
            baseScale = serializedObject.FindProperty(nameof(baseScale));
            maxScale = serializedObject.FindProperty(nameof(maxScale));
            scaleDirection = serializedObject.FindProperty(nameof(scaleDirection));
            influenceRadius = serializedObject.FindProperty(nameof(influenceRadius));
            axisBias = serializedObject.FindProperty(nameof(axisBias));
            smoothTime = serializedObject.FindProperty(nameof(smoothTime));
            orientation = serializedObject.FindProperty(nameof(orientation));
            alwaysUpdateOnHover = serializedObject.FindProperty(nameof(alwaysUpdateOnHover));
            requirePointerInsideForRefresh = serializedObject.FindProperty(nameof(requirePointerInsideForRefresh));
            ignoredRects = serializedObject.FindProperty(nameof(ignoredRects));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            ReferencesFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Scale", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(baseScale);
                        EvoEditorGUI.Property(maxScale);
                        EvoEditorGUI.Property(scaleDirection, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Magnification", nestDepth: 1);
                    {
                        EvoEditorGUI.Property(influenceRadius);
                        EvoEditorGUI.Property(orientation);
                        if (orientation.hasMultipleDifferentValues || orientation.enumValueIndex != (int)DockMagnifier.Orientation.Free)
                            EvoEditorGUI.Property(axisBias);
                        EvoEditorGUI.Property(smoothTime, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Behavior", nestDepth: 1);
                    {
                        bool showRefreshRestriction = alwaysUpdateOnHover.hasMultipleDifferentValues || alwaysUpdateOnHover.boolValue;
                        EvoEditorGUI.Toggle(alwaysUpdateOnHover, FieldOptions.WithSpace(showRefreshRestriction));
                        if (showRefreshRestriction)
                            EvoEditorGUI.Toggle(requirePointerInsideForRefresh, "Require Pointer Inside", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.ArrayProperty(ignoredRects, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}