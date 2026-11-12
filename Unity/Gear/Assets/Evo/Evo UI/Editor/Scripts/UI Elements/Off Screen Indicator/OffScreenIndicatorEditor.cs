using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(OffScreenIndicator)), CanEditMultipleObjects]
    public class OffScreenIndicatorEditor : UnityEditor.Editor
    {
        // Customization
        SerializedProperty indicatorPreset;
        SerializedProperty indicatorIcon;

        // Settings
        SerializedProperty hideWhenOnScreen;
        SerializedProperty checkDistance;
        SerializedProperty borderOffset;
        SerializedProperty fadeDuration;
        SerializedProperty transitionEase;

        // Distance Settings
        SerializedProperty distanceUnit;
        SerializedProperty distanceFormat;
        SerializedProperty distanceSource;
        SerializedProperty enableDistanceFade;
        SerializedProperty fadeStartDistance;
        SerializedProperty fadeEndDistance;

        // References
        SerializedProperty targetTransform;
        SerializedProperty targetCamera;
        SerializedProperty targetCanvas;
        SerializedProperty rectBoundary;

        void OnEnable()
        {
            indicatorPreset = serializedObject.FindProperty("indicatorPreset");
            indicatorIcon = serializedObject.FindProperty("indicatorIcon");

            hideWhenOnScreen = serializedObject.FindProperty("hideWhenOnScreen");
            checkDistance = serializedObject.FindProperty("checkDistance");
            borderOffset = serializedObject.FindProperty("borderOffset");
            fadeDuration = serializedObject.FindProperty("fadeDuration");
            transitionEase = serializedObject.FindProperty("transitionEase");

            distanceUnit = serializedObject.FindProperty("distanceUnit");
            distanceFormat = serializedObject.FindProperty("distanceFormat");
            distanceSource = serializedObject.FindProperty("distanceSource");
            enableDistanceFade = serializedObject.FindProperty("enableDistanceFade");
            fadeStartDistance = serializedObject.FindProperty("fadeStartDistance");
            fadeEndDistance = serializedObject.FindProperty("fadeEndDistance");

            targetTransform = serializedObject.FindProperty("targetTransform");
            targetCamera = serializedObject.FindProperty("targetCamera");
            targetCanvas = serializedObject.FindProperty("targetCanvas");
            rectBoundary = serializedObject.FindProperty("rectBoundary");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ObjectFoldout();
            SettingsFoldout();
            ReferencesFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void ObjectFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Indicator", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(indicatorPreset, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(indicatorIcon, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Toggle(hideWhenOnScreen, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(checkDistance, "Check Distance (m)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(borderOffset, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(fadeDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(transitionEase, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(distanceSource, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableDistanceFade, "Distance Fade", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableDistanceFade.boolValue || enableDistanceFade.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(fadeStartDistance, "Start Distance (m)");
                            EvoEditorGUI.Property(fadeEndDistance, "End Distance (m)", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();

                    if (!hideWhenOnScreen.boolValue || hideWhenOnScreen.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.AddPropertySpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            EvoEditorGUI.Property(distanceUnit, FieldOptions.WithoutSpace().WithoutBackground());
                            if (distanceUnit.enumValueIndex != 0 || distanceUnit.hasMultipleDifferentValues)
                            {
                                EvoEditorGUI.BeginContainer();
                                EvoEditorGUI.Property(distanceFormat, FieldOptions.WithoutSpace());
                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndColumn();
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
                    EvoEditorGUI.Property(targetTransform, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetCamera, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(targetCanvas, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(rectBoundary, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}