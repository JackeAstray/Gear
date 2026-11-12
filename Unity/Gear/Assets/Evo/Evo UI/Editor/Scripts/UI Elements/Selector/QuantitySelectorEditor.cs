using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(QuantitySelector)), CanEditMultipleObjects]
    public class QuantitySelectorEditor : UnityEditor.Editor
    {
        // UI References
        SerializedProperty inputField;
        SerializedProperty increaseButton;
        SerializedProperty decreaseButton;

        // Settings
        SerializedProperty minQuantity;
        SerializedProperty maxQuantity;
        SerializedProperty startQuantity;

        // Animation
        SerializedProperty useUnscaledTime;
        SerializedProperty slideOffset;
        SerializedProperty animationDuration;
        SerializedProperty animationCurve;

        // Events
        SerializedProperty onValueChanged;

        void OnEnable()
        {
            inputField = serializedObject.FindProperty("inputField");
            increaseButton = serializedObject.FindProperty("increaseButton");
            decreaseButton = serializedObject.FindProperty("decreaseButton");

            minQuantity = serializedObject.FindProperty("minQuantity");
            maxQuantity = serializedObject.FindProperty("maxQuantity");
            startQuantity = serializedObject.FindProperty("startQuantity");

            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            slideOffset = serializedObject.FindProperty("slideOffset");
            animationDuration = serializedObject.FindProperty("animationDuration");
            animationCurve = serializedObject.FindProperty("animationCurve");

            onValueChanged = serializedObject.FindProperty("onValueChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            SettingsFoldout();
            AnimationFoldout();
            ReferencesFoldout();
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
                    EvoEditorGUI.Property(startQuantity, "Start Quantity", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(minQuantity, "Min Quantity", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(maxQuantity, "Max Quantity", FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animation", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(slideOffset, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(animationDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(animationCurve, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(inputField, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(increaseButton, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(decreaseButton, FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onValueChanged, "On Value Changed", FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}