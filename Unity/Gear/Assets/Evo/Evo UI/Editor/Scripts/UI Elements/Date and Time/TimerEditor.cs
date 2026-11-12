using UnityEditor;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Timer)), CanEditMultipleObjects]
    public class TimerEditor : UnityEditor.Editor
    {
        // Timer
        SerializedProperty duration;
        SerializedProperty currentTime;
        SerializedProperty timeMultiplier;
        SerializedProperty textFormat;
        SerializedProperty displayFormat;
        SerializedProperty useUniformDigitWidth;
        SerializedProperty digitWidthScale;

        // Settings
        SerializedProperty autoStart;
        SerializedProperty countDown;
        SerializedProperty loop;
        SerializedProperty useSystemClock;
        SerializedProperty useUnscaledTime;
        SerializedProperty invertFill;

        // Animation
        SerializedProperty enableSmoothing;
        SerializedProperty updateBarOnSecondsOnly;
        SerializedProperty smoothingDuration;
        SerializedProperty enableRolling;
        SerializedProperty animationDuration;
        SerializedProperty slideDistance;
        SerializedProperty animationEase;
        SerializedProperty animationCurve;

        // References
        SerializedProperty fillRect;
        SerializedProperty valueText;

        // Events
        SerializedProperty onValueChanged;
        SerializedProperty onTimerComplete;
        SerializedProperty onTimerStart;
        SerializedProperty onTimerStop;
        SerializedProperty onTimerReset;

        void OnEnable()
        {
            duration = serializedObject.FindProperty("duration");
            currentTime = serializedObject.FindProperty("currentTime");
            timeMultiplier = serializedObject.FindProperty("timeMultiplier");
            textFormat = serializedObject.FindProperty("textFormat");
            displayFormat = serializedObject.FindProperty("displayFormat");
            
            useUniformDigitWidth = serializedObject.FindProperty("useUniformDigitWidth");
            digitWidthScale = serializedObject.FindProperty("digitWidthScale");
            autoStart = serializedObject.FindProperty("autoStart");
            countDown = serializedObject.FindProperty("countDown");
            loop = serializedObject.FindProperty("loop");
            useSystemClock = serializedObject.FindProperty("useSystemClock");
            useUnscaledTime = serializedObject.FindProperty("useUnscaledTime");
            invertFill = serializedObject.FindProperty("invertFill");
            enableSmoothing = serializedObject.FindProperty("enableSmoothing");
            updateBarOnSecondsOnly = serializedObject.FindProperty("updateBarOnSecondsOnly");
            smoothingDuration = serializedObject.FindProperty("smoothingDuration");
            enableRolling = serializedObject.FindProperty("enableRolling");
            animationDuration = serializedObject.FindProperty("animationDuration");
            slideDistance = serializedObject.FindProperty("slideDistance");
            animationEase = serializedObject.FindProperty("animationEase");
            animationCurve = serializedObject.FindProperty("animationCurve");
            
            fillRect = serializedObject.FindProperty("fillRect");
            valueText = serializedObject.FindProperty("valueText");
           
            onValueChanged = serializedObject.FindProperty("onValueChanged");
            onTimerComplete = serializedObject.FindProperty("onTimerComplete");
            onTimerStart = serializedObject.FindProperty("onTimerStart");
            onTimerStop = serializedObject.FindProperty("onTimerStop");
            onTimerReset = serializedObject.FindProperty("onTimerReset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            TimeFoldout();
            SettingsFoldout();
            AnimationFoldout();
            ReferencesFoldout();
            EventsFoldout();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void TimeFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Time", true, FoldoutOptions.WithIcon("UI_Time")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useSystemClock, FieldOptions.WithInvertedColor());
                    if (!useSystemClock.boolValue || useSystemClock.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.Property(currentTime, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(duration, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Property(timeMultiplier, FieldOptions.WithInvertedColor());
                    }
                    EvoEditorGUI.Property(textFormat, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(displayFormat, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Toggle(autoStart, FieldOptions.WithInvertedColor());
                    if (!useSystemClock.boolValue || useSystemClock.hasMultipleDifferentValues)
                    {
                        EvoEditorGUI.Toggle(countDown, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Toggle(loop, FieldOptions.WithInvertedColor());
                        EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    }

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(useUniformDigitWidth, "Uniform Digit Width", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (useUniformDigitWidth.boolValue || useUniformDigitWidth.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.BeginContainer();
                            EvoEditorGUI.Property(digitWidthScale, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Bar Animation");
                    {
                        EvoEditorGUI.Toggle(invertFill);
                        EvoEditorGUI.Toggle(updateBarOnSecondsOnly);
                        EvoEditorGUI.Toggle(enableSmoothing, "Smoothing", FieldOptions.WithoutSpace());
                        if (enableSmoothing.boolValue || enableSmoothing.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.AddPropertySpace();
                            EvoEditorGUI.Property(smoothingDuration, "Smoothing Duration", FieldOptions.WithoutSpace());
                        }
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                    EvoEditorGUI.AddPropertySpace();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Rolling Animation");
                    {
                        EvoEditorGUI.Toggle(enableRolling, "Enable Rolling", FieldOptions.WithoutSpace());
                        if (enableRolling.boolValue || enableRolling.hasMultipleDifferentValues)
                        {
                            EvoEditorGUI.AddPropertySpace();
                            EvoEditorGUI.Property(animationDuration, "Roll Duration");
                            EvoEditorGUI.Property(slideDistance, "Slide Distance");

                            bool showCurve = animationEase.hasMultipleDifferentValues || animationEase.intValue == (int)Ease.Custom;
                            EvoEditorGUI.Property(animationEase, "Ease", FieldOptions.WithSpace(showCurve));

                            if (showCurve)
                                EvoEditorGUI.Property(animationCurve, "Curve", FieldOptions.WithoutSpace());
                        }
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
                    EvoEditorGUI.Property(valueText, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onTimerComplete, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onTimerStart, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onTimerStop, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onTimerReset, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}