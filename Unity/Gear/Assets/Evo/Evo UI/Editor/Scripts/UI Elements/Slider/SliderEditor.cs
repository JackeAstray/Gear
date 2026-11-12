using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Slider)), CanEditMultipleObjects]
    public class SliderEditor : SelectableEditor
    {
        // Object
        SerializedProperty wholeNumbers;
        SerializedProperty value;
        SerializedProperty minValue;
        SerializedProperty maxValue;
        SerializedProperty direction;

        // Settings
        SerializedProperty interactable;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // Animation
        SerializedProperty animationCurve;
        SerializedProperty highlightedScale;
        SerializedProperty pressedScale;
        SerializedProperty transitionDuration;

        // Navigation
        SerializedProperty navigation;

        // References
        SerializedProperty fillRect;
        SerializedProperty handleRect;
        SerializedProperty valueText;
        SerializedProperty valueInput;

        // Events
        SerializedProperty onValueChanged;

        protected override void OnEnable()
        {
            wholeNumbers = serializedObject.FindProperty("m_WholeNumbers");
            value = serializedObject.FindProperty("m_Value");
            minValue = serializedObject.FindProperty("m_MinValue");
            maxValue = serializedObject.FindProperty("m_MaxValue");
            direction = serializedObject.FindProperty("m_Direction");

            interactable = serializedObject.FindProperty("m_Interactable");
            displayMultiplier = serializedObject.FindProperty("displayMultiplier");
            displayFormat = serializedObject.FindProperty("displayFormat");
            textFormat = serializedObject.FindProperty("textFormat");

            animationCurve = serializedObject.FindProperty("animationCurve");
            highlightedScale = serializedObject.FindProperty("highlightedScale");
            pressedScale = serializedObject.FindProperty("pressedScale");
            transitionDuration = serializedObject.FindProperty("transitionDuration");

            navigation = serializedObject.FindProperty("m_Navigation");

            fillRect = serializedObject.FindProperty("m_FillRect");
            handleRect = serializedObject.FindProperty("m_HandleRect");
            valueText = serializedObject.FindProperty("valueText");
            valueInput = serializedObject.FindProperty("valueInput");

            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            ObjectFoldout();
            SettingsFoldout();
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void ObjectFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Object", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EditorGUI.BeginChangeCheck();
                    EvoEditorGUI.Slider(value, minValue.floatValue, maxValue.floatValue, "Value", FieldOptions.WithInvertedColor());
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();

                        foreach (var obj in targets)
                        {
                            var slider = (Slider)obj;
                            slider.SetValueWithoutNotify(slider.value);
                            slider.onValueChanged?.Invoke(slider.value);
                        }
                    }

                    EvoEditorGUI.Property(minValue, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(maxValue, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(wholeNumbers, FieldOptions.WithInvertedColor());

                    EditorGUI.BeginChangeCheck();
                    EvoEditorGUI.Property(direction, FieldOptions.WithInvertedColor());
                    if (EditorGUI.EndChangeCheck())
                        ChangeDirection();

                    if (valueText.objectReferenceValue != null || valueInput.objectReferenceValue != null ||
                        valueText.hasMultipleDifferentValues || valueInput.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        EvoEditorGUI.BeginContainer("Text & Input");
                        {
                            EvoEditorGUI.Property(textFormat);
                            EvoEditorGUI.Property(displayMultiplier);
                            EvoEditorGUI.Property(displayFormat, FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndColumn();
                        EvoEditorGUI.AddLayoutSpace();
                    }

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Handle Animation");
                    {
                        EvoEditorGUI.Property(transitionDuration);
                        EvoEditorGUI.Property(highlightedScale);
                        EvoEditorGUI.Property(pressedScale);
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
                    EvoEditorGUI.Property(fillRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(handleRect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(valueText, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(valueInput, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutSpace().WithoutBackground());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void ChangeDirection()
        {
            var newDirection = (Slider.Direction)direction.enumValueIndex;
            foreach (var obj in serializedObject.targetObjects)
            {
                var slider = (Slider)obj;
                if (slider.direction == newDirection)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(slider.gameObject, "Change Slider Direction");
                slider.SetDirection(newDirection, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(slider);

                foreach (var rect in slider.GetComponentsInChildren<UnityEngine.RectTransform>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
        }
    }
}