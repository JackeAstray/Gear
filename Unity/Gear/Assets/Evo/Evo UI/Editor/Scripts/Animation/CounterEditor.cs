using UnityEditor;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Counter)), CanEditMultipleObjects]
    public class CounterEditor : UnityEditor.Editor
    {
        SerializedProperty textObject;
        SerializedProperty value;
        SerializedProperty textFormat;
        SerializedProperty displayFormat;
        SerializedProperty useUniformDigitWidth;
        SerializedProperty digitWidthScale;
        SerializedProperty animateOnEnable;
        SerializedProperty useUnscaledTime;
        SerializedProperty counterDuration;
        SerializedProperty rollThroughDigits;
        SerializedProperty minimumStepDuration;
        SerializedProperty delay;
        SerializedProperty digitStagger;
        SerializedProperty rollDistance;
        SerializedProperty fadeAmount;
        SerializedProperty animationEase;
        SerializedProperty animationCurve;

        void OnEnable()
        {
            textObject = serializedObject.FindProperty("textObject");
            value = serializedObject.FindProperty("value");
            textFormat = serializedObject.FindProperty("textFormat");
            displayFormat = serializedObject.FindProperty("displayFormat");
            useUniformDigitWidth = serializedObject.FindProperty("useUniformDigitWidth");
            digitWidthScale = serializedObject.FindProperty("digitWidthScale");
            animateOnEnable = serializedObject.FindProperty("animateOnEnable");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            counterDuration = serializedObject.FindProperty("counterDuration");
            rollThroughDigits = serializedObject.FindProperty("rollThroughDigits");
            minimumStepDuration = serializedObject.FindProperty("minimumStepDuration");
            delay = serializedObject.FindProperty("delay");
            digitStagger = serializedObject.FindProperty("digitStagger");
            rollDistance = serializedObject.FindProperty("rollDistance");
            fadeAmount = serializedObject.FindProperty("fadeAmount");
            animationEase = serializedObject.FindProperty("animationEase");
            animationCurve = serializedObject.FindProperty("animationCurve");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DataFoldout();
            SettingsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DataFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Data", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(value, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(textFormat, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(displayFormat, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(textObject, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(useUniformDigitWidth, "Uniform Digit Width", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (useUniformDigitWidth.boolValue)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(digitWidthScale, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Rolling Animation");
                    {
                        EvoEditorGUI.Toggle(animateOnEnable);
                        EvoEditorGUI.Toggle(useUnscaledTime);
                        EvoEditorGUI.Toggle(rollThroughDigits);
                        EvoEditorGUI.Property(counterDuration, "Roll Duration");
                       
                        if (rollThroughDigits.boolValue || rollThroughDigits.hasMultipleDifferentValues)
                            EvoEditorGUI.Property(minimumStepDuration);
                       
                        EvoEditorGUI.Property(delay, "Start Delay");
                        EvoEditorGUI.Property(digitStagger);
                        EvoEditorGUI.Property(rollDistance);
                        EvoEditorGUI.Property(fadeAmount);

                        bool showCurve = animationEase.hasMultipleDifferentValues || animationEase.intValue == (int)Ease.Custom;
                        EvoEditorGUI.Property(animationEase, "Ease", FieldOptions.WithSpace(showCurve));

                        if (showCurve)
                            EvoEditorGUI.Property(animationCurve, "Curve", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}