using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Tooltip), true)]
    public class TooltipEditor : UnityEditor.Editor
    {
        // Content
        SerializedProperty tooltipPreset;
        SerializedProperty icon;
        SerializedProperty title;
        SerializedProperty description;
        SerializedProperty customContent;

        // Settings
        SerializedProperty showDelay;
        SerializedProperty followCursor;
        SerializedProperty is3DObject;
        SerializedProperty maxWidth;
        SerializedProperty movementSmoothing;

        // Position & Offset
        SerializedProperty offsetPosition;
        SerializedProperty customOffset;
        SerializedProperty offsetDistance;
        SerializedProperty screenEdgePadding;

        // References
        SerializedProperty tooltipCanvas;

        // Events
        SerializedProperty onShow;
        SerializedProperty onHide;

        void OnEnable() => InitializeEditorState();

        void InitializeEditorState()
        {
            tooltipPreset = serializedObject.FindProperty(nameof(tooltipPreset));
            icon = serializedObject.FindProperty(nameof(icon));
            title = serializedObject.FindProperty(nameof(title));
            description = serializedObject.FindProperty(nameof(description));
            customContent = serializedObject.FindProperty(nameof(customContent));

            showDelay = serializedObject.FindProperty(nameof(showDelay));
            followCursor = serializedObject.FindProperty(nameof(followCursor));
            is3DObject = serializedObject.FindProperty(nameof(is3DObject));
            maxWidth = serializedObject.FindProperty(nameof(maxWidth));
            movementSmoothing = serializedObject.FindProperty(nameof(movementSmoothing));

            offsetPosition = serializedObject.FindProperty(nameof(offsetPosition));
            customOffset = serializedObject.FindProperty(nameof(customOffset));
            offsetDistance = serializedObject.FindProperty(nameof(offsetDistance));
            screenEdgePadding = serializedObject.FindProperty(nameof(screenEdgePadding));

            tooltipCanvas = serializedObject.FindProperty(nameof(tooltipCanvas));

            onShow = serializedObject.FindProperty(nameof(onShow));
            onHide = serializedObject.FindProperty(nameof(onHide));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ContentSection();
            SettingsFoldout();
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void ContentSection()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Content", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(customContent, "Custom Content", FieldOptions.WithInvertedColor());

                    if (customContent.objectReferenceValue != null)
                    {
                        EvoEditorGUI.Callout("Custom Content is assigned; preset content will not be managed by this component.",
                            invertColor: true, spaceBefore: 0, spaceAfter: 0);
                    }
                    else
                    {
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Tooltip", nestDepth: 1);
                        {
                            EvoEditorGUI.Property(tooltipPreset, "Preset");
                            EvoEditorGUI.Property(icon);
                            EvoEditorGUI.Property(title);
                            EvoEditorGUI.Property(description, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                        EvoEditorGUI.AddLayoutSpace();
                        string[] props = new string[] { "titleKey", "descriptionKey" };
                        Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((Tooltip)target).gameObject, null, props);
#endif
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(followCursor, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(is3DObject, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(maxWidth, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(showDelay, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(movementSmoothing, "Smoothing", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Position & Offset");
                    {
                        EvoEditorGUI.Property(offsetPosition);

                        if (offsetPosition.enumValueIndex == (int)OffsetPosition.Custom)
                            EvoEditorGUI.Property(customOffset);
                        else
                            EvoEditorGUI.Property(offsetDistance, "Distance");

                        EvoEditorGUI.Property(screenEdgePadding, FieldOptions.WithoutSpace());
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
                EvoEditorGUI.Property(tooltipCanvas, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(onShow, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onHide, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}