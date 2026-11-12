using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(MarqueeSelection)), CanEditMultipleObjects]
    public class MarqueeSelectionEditor : UnityEditor.Editor
    {
        SerializedProperty content;
        SerializedProperty allowDrag;
        SerializedProperty allowModifiers;
        SerializedProperty hitTest;
        SerializedProperty bringToFront;
        SerializedProperty constrainMovement;
        SerializedProperty movementBounds;
        SerializedProperty transferMode;
        SerializedProperty allowedTargets;
        SerializedProperty acceptTransfers;
        SerializedProperty marqueeShape;
        SerializedProperty dashSpeed;
        SerializedProperty fadeDuration;
        SerializedProperty onSelectionChanged;
        SerializedProperty onDragStarted;
        SerializedProperty onDragEnded;

        void OnEnable()
        {
            content = serializedObject.FindProperty("content");
            allowDrag = serializedObject.FindProperty("allowDrag");
            allowModifiers = serializedObject.FindProperty("allowModifiers");
            hitTest = serializedObject.FindProperty("hitTest");
            bringToFront = serializedObject.FindProperty("bringToFront");
            constrainMovement = serializedObject.FindProperty("constrainMovement");
            movementBounds = serializedObject.FindProperty("movementBounds");
            transferMode = serializedObject.FindProperty("transferMode");
            allowedTargets = serializedObject.FindProperty("allowedTargets");
            acceptTransfers = serializedObject.FindProperty("acceptTransfers");
            marqueeShape = serializedObject.FindProperty("marqueeShape");
            dashSpeed = serializedObject.FindProperty("dashSpeed");
            fadeDuration = serializedObject.FindProperty("fadeDuration");
            onSelectionChanged = serializedObject.FindProperty("onSelectionChanged");
            onDragStarted = serializedObject.FindProperty("onDragStarted");
            onDragEnded = serializedObject.FindProperty("onDragEnded");
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
                    EvoEditorGUI.Property(content, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(hitTest, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(allowModifiers, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Toggle(allowDrag, FieldOptions.WithInvertedColor().WithoutSpace());
                    if (allowDrag.boolValue || allowDrag.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.Toggle(bringToFront, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Toggle(acceptTransfers, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Toggle(constrainMovement, FieldOptions.WithInvertedColor());
                        if (constrainMovement.boolValue)
                            EvoEditorGUI.Property(movementBounds, FieldOptions.WithInvertedColor());

                        bool showTargets = transferMode.enumValueIndex == (int)MarqueeSelection.TransferMode.AllowedTargets;
                        EvoEditorGUI.Property(transferMode, FieldOptions.WithInvertedColor().WithSpace(showTargets));
                        if (showTargets)
                            EvoEditorGUI.ArrayProperty(allowedTargets, FieldOptions.WithInvertedColor().WithoutSpace());
                    }

                    if (targets.Length == 1)
                    {
                        MarqueeSelection container = (MarqueeSelection)target;
                        if (container.Content != container.RectTransform && !container.Content.IsChildOf(container.RectTransform))
                            EvoEditorGUI.Callout("Content must be this container or one of its descendants.", CalloutType.Warning, spaceAfter: 0);
                    }
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
                    EvoEditorGUI.Property(marqueeShape, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dashSpeed, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(fadeDuration, FieldOptions.WithInvertedColor().WithoutSpace());
                  
                    EvoEditorGUI.Callout("Leave the shape empty for the default dashed rectangle.", invertColor: true, spaceAfter: 0);
                    if (targets.Length == 1 && marqueeShape.objectReferenceValue is ProceduralShape shape && shape.transform.parent != ((MarqueeSelection)target).transform)
                        EvoEditorGUI.Callout("The marquee shape must be a direct child of this container.", CalloutType.Warning, spaceAfter: 0);
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
                    EvoEditorGUI.Property(onSelectionChanged, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onDragStarted, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onDragEnded, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}