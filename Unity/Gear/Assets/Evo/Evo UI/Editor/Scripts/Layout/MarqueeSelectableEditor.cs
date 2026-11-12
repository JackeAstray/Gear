using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(MarqueeSelectable)), CanEditMultipleObjects]
    public class MarqueeSelectableEditor : UnityEditor.Editor
    {
        SerializedProperty interactable;
        SerializedProperty draggable;
        SerializedProperty selectionVisual;
        SerializedProperty targetGraphic;
        SerializedProperty targetShape;
        SerializedProperty selectedColor;
        SerializedProperty selectedOutlineColor;
        SerializedProperty selectedOutlineWidth;
        SerializedProperty transitionDuration;
        SerializedProperty onSelected;
        SerializedProperty onDeselected;

        void OnEnable()
        {
            interactable = serializedObject.FindProperty("interactable");
            draggable = serializedObject.FindProperty("draggable");
            selectionVisual = serializedObject.FindProperty("selectionVisual");
            targetGraphic = serializedObject.FindProperty("targetGraphic");
            targetShape = serializedObject.FindProperty("targetShape");
            selectedColor = serializedObject.FindProperty("selectedColor");
            selectedOutlineColor = serializedObject.FindProperty("selectedOutlineColor");
            selectedOutlineWidth = serializedObject.FindProperty("selectedOutlineWidth");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            onSelected = serializedObject.FindProperty("onSelected");
            onDeselected = serializedObject.FindProperty("onDeselected");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            VisualsFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(draggable, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void VisualsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Visuals", true, FoldoutOptions.WithIcon("UI_Style")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(selectionVisual, FieldOptions.WithInvertedColor().WithoutSpace());

                    if (selectionVisual.enumValueIndex == (int)MarqueeSelectable.VisualType.Graphic)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Property(targetGraphic, FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                  
                    if (selectionVisual.enumValueIndex == (int)MarqueeSelectable.VisualType.ProceduralShape)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Property(targetShape, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(selectedOutlineColor, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(selectedOutlineWidth, FieldOptions.WithInvertedColor().WithoutSpace());
                    }
                   
                    if (selectionVisual.enumValueIndex != (int)MarqueeSelectable.VisualType.None)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Property(selectedColor, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor().WithoutSpace());
                    }

                    if (selectionVisual.enumValueIndex == (int)MarqueeSelectable.VisualType.None)
                        EvoEditorGUI.Callout("Use the selection events to drive Interactive, Styler or custom visuals.", invertColor: true, spaceAfter: 0);
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
                    EvoEditorGUI.Property(onSelected, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onDeselected, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}