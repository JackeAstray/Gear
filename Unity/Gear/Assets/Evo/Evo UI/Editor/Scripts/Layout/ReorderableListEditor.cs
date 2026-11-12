using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(ReorderableList))]
    public class ReorderableListEditor : UnityEditor.Editor
    {
        SerializedProperty listContainer;
        SerializedProperty canvas;
        SerializedProperty instantSnap;
        SerializedProperty itemSpacing;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;
        SerializedProperty dragAlpha;
        SerializedProperty dragScale;
        SerializedProperty allowListTransfer;
        SerializedProperty transferGroup;
        SerializedProperty dropArea; 
        SerializedProperty onOrderChanged;
        SerializedProperty onListChanged;

        void OnEnable()
        {
            listContainer = serializedObject.FindProperty(nameof(listContainer));
            canvas = serializedObject.FindProperty(nameof(canvas));
            instantSnap = serializedObject.FindProperty(nameof(instantSnap));
            itemSpacing = serializedObject.FindProperty(nameof(itemSpacing));
            animationDuration = serializedObject.FindProperty(nameof(animationDuration));
            animationCurve = serializedObject.FindProperty(nameof(animationCurve));
            dragAlpha = serializedObject.FindProperty(nameof(dragAlpha));
            dragScale = serializedObject.FindProperty(nameof(dragScale));
            allowListTransfer = serializedObject.FindProperty(nameof(allowListTransfer));
            transferGroup = serializedObject.FindProperty(nameof(transferGroup));
            dropArea = serializedObject.FindProperty(nameof(dropArea));
            onOrderChanged = serializedObject.FindProperty(nameof(onOrderChanged));
            onListChanged = serializedObject.FindProperty(nameof(onListChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(allowListTransfer, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (allowListTransfer.boolValue)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(transferGroup);
                            EvoEditorGUI.Property(dropArea, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Toggle(instantSnap, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(itemSpacing, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dragAlpha, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dragScale, FieldOptions.WithInvertedColor());

                    using (new EditorGUI.DisabledScope(instantSnap.boolValue))
                    {
                        EvoEditorGUI.Property(animationDuration, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(animationCurve, FieldOptions.WithInvertedColor().WithoutSpace());
                    }
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
                {
                    EvoEditorGUI.Property(listContainer, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(canvas, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(onOrderChanged, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onListChanged, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}