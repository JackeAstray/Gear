using UnityEditor;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Calendar))]
    public class CalendarEditor : UnityEditor.Editor
    {
        // Settings
        SerializedProperty highlightToday;
        SerializedProperty yearRangeFromCurrent;

        // Initial Date
        SerializedProperty initialDateMode;
        SerializedProperty customYear;
        SerializedProperty customMonth;
        SerializedProperty customDay;

        // Animation
        SerializedProperty transitionDuration;
        SerializedProperty transitionEase;
        SerializedProperty customEase;
        SerializedProperty zoomScale;
        SerializedProperty slideAxis;
        SerializedProperty slideDistance;
        SerializedProperty useUnscaledTime;

        // References
        SerializedProperty dateLabel;
        SerializedProperty headerButton;
        SerializedProperty monthDropdown;
        SerializedProperty yearDropdown;
        SerializedProperty previousMonthButton;
        SerializedProperty nextMonthButton;
        SerializedProperty dayView;
        SerializedProperty daysContainer;
        SerializedProperty dayButtonPrefab;
        SerializedProperty monthView;
        SerializedProperty monthContainer;
        SerializedProperty yearView;
        SerializedProperty yearContainer;
        SerializedProperty pickerButtonPrefab;

        // Month Data
        SerializedProperty january;
        SerializedProperty february;
        SerializedProperty march;
        SerializedProperty april;
        SerializedProperty may;
        SerializedProperty june;
        SerializedProperty july;
        SerializedProperty august;
        SerializedProperty september;
        SerializedProperty october;
        SerializedProperty november;
        SerializedProperty december;

        // Events
        SerializedProperty onDateSelected;

        void OnEnable()
        {
            highlightToday = serializedObject.FindProperty(nameof(highlightToday));
            yearRangeFromCurrent = serializedObject.FindProperty(nameof(yearRangeFromCurrent));

            initialDateMode = serializedObject.FindProperty(nameof(initialDateMode));
            customYear = serializedObject.FindProperty(nameof(customYear));
            customMonth = serializedObject.FindProperty(nameof(customMonth));
            customDay = serializedObject.FindProperty(nameof(customDay));

            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            transitionEase = serializedObject.FindProperty(nameof(transitionEase));
            customEase = serializedObject.FindProperty(nameof(customEase));
            zoomScale = serializedObject.FindProperty(nameof(zoomScale));
            slideAxis = serializedObject.FindProperty(nameof(slideAxis));
            slideDistance = serializedObject.FindProperty(nameof(slideDistance));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));

            dateLabel = serializedObject.FindProperty(nameof(dateLabel));
            headerButton = serializedObject.FindProperty(nameof(headerButton));
            monthDropdown = serializedObject.FindProperty(nameof(monthDropdown));
            yearDropdown = serializedObject.FindProperty(nameof(yearDropdown));
            previousMonthButton = serializedObject.FindProperty(nameof(previousMonthButton));
            nextMonthButton = serializedObject.FindProperty(nameof(nextMonthButton));
            dayView = serializedObject.FindProperty(nameof(dayView));
            daysContainer = serializedObject.FindProperty(nameof(daysContainer));
            dayButtonPrefab = serializedObject.FindProperty(nameof(dayButtonPrefab));
            monthView = serializedObject.FindProperty(nameof(monthView));
            monthContainer = serializedObject.FindProperty(nameof(monthContainer));
            yearView = serializedObject.FindProperty(nameof(yearView));
            yearContainer = serializedObject.FindProperty(nameof(yearContainer));
            pickerButtonPrefab = serializedObject.FindProperty(nameof(pickerButtonPrefab));

            january = serializedObject.FindProperty(nameof(january));
            february = serializedObject.FindProperty(nameof(february));
            march = serializedObject.FindProperty(nameof(march));
            april = serializedObject.FindProperty(nameof(april));
            may = serializedObject.FindProperty(nameof(may));
            june = serializedObject.FindProperty(nameof(june));
            july = serializedObject.FindProperty(nameof(july));
            august = serializedObject.FindProperty(nameof(august));
            september = serializedObject.FindProperty(nameof(september));
            october = serializedObject.FindProperty(nameof(october));
            november = serializedObject.FindProperty(nameof(november));
            december = serializedObject.FindProperty(nameof(december));

            onDateSelected = serializedObject.FindProperty(nameof(onDateSelected));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            AnimationFoldout();
            ReferencesFoldout();
            MonthDataFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(highlightToday, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(yearRangeFromCurrent, "Year Range", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(initialDateMode, FieldOptions.WithoutBackground().WithoutSpace());
                    if (initialDateMode.hasMultipleDifferentValues || initialDateMode.enumValueIndex == (int)Calendar.InitialDateMode.Custom)
                    {
                        EvoEditorGUI.BeginContainer(nestDepth: 1);
                        EvoEditorGUI.Property(customYear);
                        EvoEditorGUI.Property(customMonth);
                        EvoEditorGUI.Property(customDay, FieldOptions.WithoutSpace());
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((Calendar)target).gameObject);
#endif
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
                    EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Property(transitionEase, "Ease", FieldOptions.WithoutBackground().WithoutSpace());
                        if (transitionEase.hasMultipleDifferentValues || transitionEase.intValue == (int)Ease.Custom)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(customEase, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn(LayoutOptions.WithSpace());

                    EvoEditorGUI.Property(slideAxis, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(slideDistance, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(zoomScale, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(dateLabel, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(headerButton, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(monthDropdown, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(yearDropdown, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(previousMonthButton, "Previous Button", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(nextMonthButton, "Next Button", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dayView, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(daysContainer, "Day Container", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(dayButtonPrefab, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(monthView, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(monthContainer, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(yearView, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(yearContainer, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(pickerButtonPrefab, FieldOptions.WithInvertedColor().WithoutSpace());

                    if (HasInvalidTemplate(dayButtonPrefab) || HasInvalidTemplate(pickerButtonPrefab))
                    {
                        EvoEditorGUI.Callout("Day and picker prefabs require CalendarDay on their root and a linked Button.",
                            invertColor: true, spaceAfter: 0);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        static bool HasInvalidTemplate(SerializedProperty property)
        {
            return !property.hasMultipleDifferentValues && property.objectReferenceValue is UnityEngine.GameObject prefab
                && (!prefab.TryGetComponent<CalendarDay>(out var day) || day.button == null);
        }

        void MonthDataFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Month Data", false, FoldoutOptions.WithIcon("UI_Time")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.ArrayProperty(january, "January (1)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(february, "February (2)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(march, "March (3)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(april, "April (4)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(may, "May (5)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(june, "June (6)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(july, "July (7)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(august, "August (8)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(september, "September (9)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(october, "October (10)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(november, "November (11)", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.ArrayProperty(december, "December (12)", FieldOptions.WithInvertedColor().WithoutSpace());
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
                EvoEditorGUI.Property(onDateSelected, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}