using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(UINavigationContainer)), CanEditMultipleObjects]
    public class UINavigationContainerEditor : UnityEditor.Editor
    {
        SerializedProperty autoFetchOnEnable;
        SerializedProperty interactiveElements;
        SerializedProperty restrictNavigation;
        SerializedProperty tabNavigation;
        SerializedProperty tabOrder;
        SerializedProperty wrapAround;
        SerializedProperty excludedFromTabOrder;
        SerializedProperty selectOnEnable;
        SerializedProperty ensureSelection;
        SerializedProperty recoveryMode;
        SerializedProperty restoreSelectionOnDisable;
        SerializedProperty defaultSelection;
        SerializedProperty fallbackSelection;

        void OnEnable()
        {
            autoFetchOnEnable = serializedObject.FindProperty("autoFetchOnEnable");
            interactiveElements = serializedObject.FindProperty("interactiveElements");
            restrictNavigation = serializedObject.FindProperty("restrictNavigation");
            tabNavigation = serializedObject.FindProperty("tabNavigation");
            tabOrder = serializedObject.FindProperty("tabOrder");
            wrapAround = serializedObject.FindProperty("wrapAround");
            excludedFromTabOrder = serializedObject.FindProperty("excludedFromTabOrder");
            selectOnEnable = serializedObject.FindProperty("selectOnEnable");
            ensureSelection = serializedObject.FindProperty("ensureSelection");
            recoveryMode = serializedObject.FindProperty("recoveryMode");
            restoreSelectionOnDisable = serializedObject.FindProperty("restoreSelectionOnDisable");
            defaultSelection = serializedObject.FindProperty("defaultSelection");
            fallbackSelection = serializedObject.FindProperty("fallbackSelection");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ElementsFoldout();
            NavigationFoldout();
            SelectionFoldout();

            EvoEditorGUI.EndCenteredInspector();
            if (serializedObject.ApplyModifiedProperties() && Application.isPlaying)
            {
                foreach (Object item in targets)
                    ((UINavigationContainer)item).RefreshNavigation();
            }
        }

        void ElementsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Elements", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(autoFetchOnEnable, "Collect Children", FieldOptions.WithInvertedColor().WithoutSpace());
                    if (!autoFetchOnEnable.boolValue || autoFetchOnEnable.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.ArrayProperty(interactiveElements, "Manual Order", FieldOptions.WithInvertedColor());
                        if (EvoEditorGUI.Button("Collect Children Into List", ButtonOptions.WithInvertedColor()))
                        {
                            serializedObject.ApplyModifiedProperties();
                            foreach (Object item in targets)
                            {
                                Undo.RecordObject(item, "Collect Navigation Elements");
                                ((UINavigationContainer)item).FetchInteractables();
                                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                                EditorUtility.SetDirty(item);
                            }
                            serializedObject.Update();
                        }
                    }

                    EvoEditorGUI.Callout("Nested containers own Tab and focus recovery for their controls.", invertColor: true, spaceAfter: 0);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void NavigationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Navigation", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(restrictNavigation, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(tabNavigation, "Tab Navigation", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (tabNavigation.boolValue || tabNavigation.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(tabOrder);
                            EvoEditorGUI.Toggle(wrapAround);
                            EvoEditorGUI.ArrayProperty(excludedFromTabOrder, "Excluded Controls", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SelectionFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Selection", true, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(selectOnEnable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(defaultSelection, FieldOptions.WithInvertedColor());
  
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(ensureSelection, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (ensureSelection.boolValue || ensureSelection.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(recoveryMode, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(restoreSelectionOnDisable, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (restoreSelectionOnDisable.boolValue || restoreSelectionOnDisable.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(fallbackSelection, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}