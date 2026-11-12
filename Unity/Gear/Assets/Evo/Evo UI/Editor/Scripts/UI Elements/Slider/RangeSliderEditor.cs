using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(RangeSlider)), CanEditMultipleObjects]
    public class RangeSliderEditor : SelectableEditor
    {
        // Range
        SerializedProperty lowerValue;
        SerializedProperty upperValue;
        SerializedProperty minValue;
        SerializedProperty maxValue;
        SerializedProperty minGap;
        SerializedProperty wholeNumbers;
        SerializedProperty direction;
        SerializedProperty pushOtherHandle;

        // Settings
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty interactionState;
        SerializedProperty displayMultiplier;
        SerializedProperty displayFormat;
        SerializedProperty textFormat;

        // Animation
        SerializedProperty transitionDuration;
        SerializedProperty animationCurve;
        SerializedProperty highlightedScale;
        SerializedProperty pressedScale;

        // References
        SerializedProperty fillRect;
        SerializedProperty lowerHandleRect;
        SerializedProperty upperHandleRect;
        SerializedProperty lowerValueText;
        SerializedProperty upperValueText;
        SerializedProperty minValueText;
        SerializedProperty maxValueText;
        SerializedProperty lowerValueInput;
        SerializedProperty upperValueInput;

        // Navigation & Events
        SerializedProperty navigation;
        SerializedProperty onValueChanged;
        SerializedProperty onPointerDown;
        SerializedProperty onPointerUp;
        SerializedProperty onPointerEnter;
        SerializedProperty onPointerExit;
        SerializedProperty onSelect;
        SerializedProperty onDeselect;
        SerializedProperty onSubmit;
        SerializedProperty onClick;

        protected override void OnEnable()
        {
            lowerValue = serializedObject.FindProperty(nameof(lowerValue));
            upperValue = serializedObject.FindProperty(nameof(upperValue));
            minValue = serializedObject.FindProperty(nameof(minValue));
            maxValue = serializedObject.FindProperty(nameof(maxValue));
            minGap = serializedObject.FindProperty(nameof(minGap));
            wholeNumbers = serializedObject.FindProperty(nameof(wholeNumbers));
            direction = serializedObject.FindProperty(nameof(direction));
            pushOtherHandle = serializedObject.FindProperty(nameof(pushOtherHandle));

            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty(nameof(resolveRaycast));
            interactionState = serializedObject.FindProperty(nameof(interactionState));
            displayMultiplier = serializedObject.FindProperty(nameof(displayMultiplier));
            displayFormat = serializedObject.FindProperty(nameof(displayFormat));
            textFormat = serializedObject.FindProperty(nameof(textFormat));

            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            animationCurve = serializedObject.FindProperty(nameof(animationCurve));
            highlightedScale = serializedObject.FindProperty(nameof(highlightedScale));
            pressedScale = serializedObject.FindProperty(nameof(pressedScale));

            fillRect = serializedObject.FindProperty(nameof(fillRect));
            lowerHandleRect = serializedObject.FindProperty(nameof(lowerHandleRect));
            upperHandleRect = serializedObject.FindProperty(nameof(upperHandleRect));
            lowerValueText = serializedObject.FindProperty(nameof(lowerValueText));
            upperValueText = serializedObject.FindProperty(nameof(upperValueText));
            minValueText = serializedObject.FindProperty(nameof(minValueText));
            maxValueText = serializedObject.FindProperty(nameof(maxValueText));
            lowerValueInput = serializedObject.FindProperty(nameof(lowerValueInput));
            upperValueInput = serializedObject.FindProperty(nameof(upperValueInput));

            navigation = serializedObject.FindProperty("m_Navigation");
            onValueChanged = serializedObject.FindProperty(nameof(onValueChanged));
            onPointerDown = serializedObject.FindProperty(nameof(onPointerDown));
            onPointerUp = serializedObject.FindProperty(nameof(onPointerUp));
            onPointerEnter = serializedObject.FindProperty(nameof(onPointerEnter));
            onPointerExit = serializedObject.FindProperty(nameof(onPointerExit));
            onSelect = serializedObject.FindProperty(nameof(onSelect));
            onDeselect = serializedObject.FindProperty(nameof(onDeselect));
            onSubmit = serializedObject.FindProperty(nameof(onSubmit));
            onClick = serializedObject.FindProperty(nameof(onClick));
           
            UINavigationEditor.PrepareForVisualize(this);
        }

        protected override void OnDisable() => UINavigationEditor.RemoveFromVisualize(this);

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();
            EditorGUI.BeginChangeCheck();
            
            ObjectFoldout();
            SettingsFoldout();
            InteractiveEditor.EffectsFoldout(serializedObject);
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();
           
            bool changed = EditorGUI.EndChangeCheck();
            EvoEditorGUI.EndCenteredInspector();
            if (changed || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void ObjectFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Object", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                DrawValueSlider(lowerValue, true);
                DrawValueSlider(upperValue, false);
                EvoEditorGUI.Property(minValue, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(maxValue, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(minGap, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(wholeNumbers, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Toggle(pushOtherHandle, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(interactionState, FieldOptions.WithInvertedColor());

                EditorGUI.BeginChangeCheck();
                EvoEditorGUI.Property(direction, FieldOptions.WithInvertedColor());
                if (EditorGUI.EndChangeCheck())
                    ChangeDirection();

                if (HasReference(lowerValueText) || HasReference(upperValueText) || HasReference(minValueText) || HasReference(maxValueText)
                    || HasReference(lowerValueInput) || HasReference(upperValueInput))
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Text & Input");
                    EvoEditorGUI.Property(textFormat);
                    EvoEditorGUI.Property(displayMultiplier);
                    EvoEditorGUI.Property(displayFormat, FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddLayoutSpace();
                }

                EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                EvoEditorGUI.BeginContainer("Handle Animation");
                EvoEditorGUI.Property(transitionDuration);
                EvoEditorGUI.Property(highlightedScale);
                EvoEditorGUI.Property(pressedScale);
                EvoEditorGUI.Property(animationCurve, FieldOptions.WithoutSpace());
                EvoEditorGUI.EndContainer();
                EvoEditorGUI.EndColumn();
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(fillRect, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(lowerHandleRect, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(upperHandleRect, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(lowerValueText, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(upperValueText, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(minValueText, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(maxValueText, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(lowerValueInput, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(upperValueInput, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onPointerDown, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onPointerUp, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onPointerEnter, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onPointerExit, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onSelect, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onDeselect, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onSubmit, FieldOptions.WithoutBackground());
                EvoEditorGUI.Property(onClick, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void DrawValueSlider(SerializedProperty property, bool lower)
        {
            float minimum = float.NegativeInfinity;
            float maximum = float.PositiveInfinity;
            foreach (Object obj in targets)
            {
                var slider = (RangeSlider)obj;
                minimum = Mathf.Max(minimum, lower ? slider.MinValue : slider.MinUpperValue);
                maximum = Mathf.Min(maximum, lower ? slider.MaxLowerValue : slider.MaxValue);
            }

            // A shared edit must be valid for every selected slider, including mixed ranges
            bool hasCommonRange = minimum <= maximum;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!hasCommonRange))
                EvoEditorGUI.Slider(property, minimum, hasCommonRange ? maximum : minimum, FieldOptions.WithInvertedColor());
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                foreach (Object obj in targets)
                {
                    var slider = (RangeSlider)obj;
                    slider.Refresh();
                    if (Application.IsPlaying(slider.gameObject))
                        slider.onValueChanged?.Invoke(slider.LowerValue, slider.UpperValue);
                }
                serializedObject.Update();
            }

            if (!hasCommonRange)
                EvoEditorGUI.Callout("The selected sliders have no shared value range. Edit them separately.", spaceAfter: 3);
        }

        static bool HasReference(SerializedProperty property) => property.hasMultipleDifferentValues || property.objectReferenceValue != null;

        void ChangeDirection()
        {
            var newDirection = (UnityEngine.UI.Slider.Direction)direction.enumValueIndex;
            foreach (Object obj in targets)
            {
                var slider = (RangeSlider)obj;
                if (slider.Direction == newDirection)
                    continue;
                
                Undo.RegisterFullObjectHierarchyUndo(slider.gameObject, "Change Range Slider Direction");
                slider.SetDirection(newDirection, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(slider);
              
                foreach (RectTransform rect in slider.GetComponentsInChildren<RectTransform>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}