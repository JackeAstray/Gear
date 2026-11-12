using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(InputFieldEnhancer)), CanEditMultipleObjects]
    public class InputFieldEnhancerEditor : UnityEditor.Editor
    {
        // Settings
        SerializedProperty clearAfterSubmit;
        SerializedProperty deselectOnEndEdit;
        SerializedProperty handleShiftEnter;
        SerializedProperty slideOffset;
        SerializedProperty scaleMultiplier;
        SerializedProperty fadeAlpha;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // References
        SerializedProperty source;
        SerializedProperty interactableObject;

        // Events
        SerializedProperty onSubmit;

        void OnEnable()
        {
            clearAfterSubmit = serializedObject.FindProperty("clearAfterSubmit");
            deselectOnEndEdit = serializedObject.FindProperty("deselectOnEndEdit");
            handleShiftEnter = serializedObject.FindProperty("handleShiftEnter");
            slideOffset = serializedObject.FindProperty("slideOffset");
            scaleMultiplier = serializedObject.FindProperty("scaleMultiplier");
            fadeAlpha = serializedObject.FindProperty("fadeAlpha");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            source = serializedObject.FindProperty("source");
            interactableObject = serializedObject.FindProperty("interactableObject");

            onSubmit = serializedObject.FindProperty("onSubmit");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            if (!source.hasMultipleDifferentValues && source.objectReferenceValue != null)
                SourceFoldout();

            SettingsFoldout();
            ReferencesFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SourceFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Input Field", true, FoldoutOptions.WithIcon("UI_Text")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Callout("Select the input field object to customize the base input field options.", invertColor: true);
                    if (EvoEditorGUI.Button("Configure Input Field", options: ButtonOptions.WithInvertedColor()))
                        Selection.activeObject = source.objectReferenceValue;
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(clearAfterSubmit, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(deselectOnEndEdit, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(handleShiftEnter, "Handle Shift+Enter", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Placeholder Animation");
                    {
                        EvoEditorGUI.Property(slideOffset);
                        EvoEditorGUI.Property(scaleMultiplier);
                        EvoEditorGUI.Property(fadeAlpha);
                        EvoEditorGUI.Property(animationDuration);
                        EvoEditorGUI.Property(animationCurve, FieldOptions.WithoutSpace());
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
                {
                    EvoEditorGUI.Property(source, "Source Input", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(interactableObject, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onSubmit, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}