using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Stepper)), NoAutoStaticsCleanup]
    public class StepperEditor : UnityEditor.Editor
    {
        // Content
        SerializedProperty currentIndex;
        SerializedProperty items;
        ReorderableListOptions itemListOptions;
        Action<SerializedProperty> itemDrawer;
        static readonly Func<SerializedProperty, int, string> ItemTitleGetter = GetItemTitle;

        // Layout
        SerializedProperty orientation;
        SerializedProperty[] containerLayout;
        SerializedProperty[] indicatorLayout;
        SerializedProperty[] contentLayout;

        // Settings
        SerializedProperty allowIndicatorNavigation;
        SerializedProperty allowStepSkipping;
        SerializedProperty allowBackwardNavigation;
        SerializedProperty nextText;
        SerializedProperty completeText;

        // Animation
        SerializedProperty useUnscaledTime;
        SerializedProperty animationDuration;
        SerializedProperty slideDistance;
        SerializedProperty animationEase;
        SerializedProperty animationCurve;
        SerializedProperty indicatorDuration;
        SerializedProperty indicatorEase;
        SerializedProperty indicatorCurve;

        // Foldouts
        SerializedProperty[] references;
        SerializedProperty[] events;

        void OnEnable()
        {
            currentIndex = serializedObject.FindProperty("currentIndex");
            items = serializedObject.FindProperty(nameof(Stepper.items));
            orientation = serializedObject.FindProperty(nameof(Stepper.orientation));
            containerLayout = FindProperties(nameof(Stepper.padding), nameof(Stepper.spacing));
            indicatorLayout = FindProperties(nameof(Stepper.indicatorSize), nameof(Stepper.indicatorSpacing), nameof(Stepper.titleAlignment),
                nameof(Stepper.titleSpacing), nameof(Stepper.maxTitleWidth), nameof(Stepper.indicatorPadding));
            contentLayout = FindProperties(nameof(Stepper.contentPadding), nameof(Stepper.navigationHeight));
            allowIndicatorNavigation = serializedObject.FindProperty(nameof(Stepper.allowIndicatorNavigation));
            allowStepSkipping = serializedObject.FindProperty(nameof(Stepper.allowStepSkipping));
            allowBackwardNavigation = serializedObject.FindProperty(nameof(Stepper.allowBackwardNavigation));
            nextText = serializedObject.FindProperty(nameof(Stepper.nextText));
            completeText = serializedObject.FindProperty(nameof(Stepper.completeText));
            useUnscaledTime = serializedObject.FindProperty(nameof(Stepper.useUnscaledTime));
            animationDuration = serializedObject.FindProperty(nameof(Stepper.animationDuration));
            slideDistance = serializedObject.FindProperty(nameof(Stepper.slideDistance));
            animationEase = serializedObject.FindProperty(nameof(Stepper.animationEase));
            animationCurve = serializedObject.FindProperty(nameof(Stepper.animationCurve));
            indicatorDuration = serializedObject.FindProperty(nameof(Stepper.indicatorDuration));
            indicatorEase = serializedObject.FindProperty(nameof(Stepper.indicatorEase));
            indicatorCurve = serializedObject.FindProperty(nameof(Stepper.indicatorCurve));
            references = FindProperties(nameof(Stepper.indicatorParent), nameof(Stepper.contentParent),
                nameof(Stepper.indicatorPreset), nameof(Stepper.navigationParent),
                nameof(Stepper.backButton), nameof(Stepper.nextButton), nameof(Stepper.completionContent));
            events = FindProperties(nameof(Stepper.onStepChanged), nameof(Stepper.onCompleted));

            itemDrawer = DrawItem;
            itemListOptions = ReorderableListOptions.WithControlName("Stepper.Items")
                .WithTitle("Steps")
                .WithIcon("UI_Group")
                .WithEmptyMessage("Add a step, then place your UI inside its Content container.")
                .WithAddLabel("Add Step")
                .WithItemInitializer(InitializeItem)
                .WithDeleteTooltip("Remove step (content is preserved in the hierarchy)")
                .WithChangeCallbacks(BeginEdit, EndEdit)
                .WithContentHeader(DrawItemsHeader);
            Undo.undoRedoPerformed += RefreshAfterUndo;
        }

        void OnDisable() => Undo.undoRedoPerformed -= RefreshAfterUndo;

        SerializedProperty[] FindProperties(params string[] names)
        {
            var properties = new SerializedProperty[names.Length];
            for (int i = 0; i < names.Length; i++)
                properties[i] = serializedObject.FindProperty(names[i]);
            
            return properties;
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;
            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EvoEditorGUI.ReorderableList(serializedObject, items, ItemTitleGetter, itemDrawer, itemListOptions, compactItems: true, compactHeader: false);
            EvoEditorGUI.AddFoldoutSpace();
            SettingsFoldout();
            LayoutFoldout();
            AnimationFoldout();
            DrawFoldout("References", "UI_Reference", references);
            DrawFoldout("Events", "UI_Event", events);

            var stepper = (Stepper)target;
            if (stepper.indicatorPreset == null)
                EvoEditorGUI.Callout("Assign a Stepper Indicator prefab to display the step rail.");
            if (EditorUtility.IsPersistent(stepper))
                EvoEditorGUI.Callout("Open this prefab in Prefab Mode to edit its live preview.");
            if (stepper.contentParent != null && stepper.contentParent.parent != stepper.transform
                || stepper.indicatorParent != null && stepper.indicatorParent.parent != stepper.transform
                || stepper.navigationParent != null && stepper.navigationParent.parent != stepper.transform)
                EvoEditorGUI.Callout("Layout containers must be distinct direct children of the Stepper. The component drives their placement.", CalloutType.Warning);

            EvoEditorGUI.EndCenteredInspector();
            if (serializedObject.hasModifiedProperties)
            {
                BeginEdit();
                serializedObject.ApplyModifiedProperties();
                EndEdit();
            }
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(allowIndicatorNavigation, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(allowStepSkipping, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(allowBackwardNavigation, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Fallback Button Text");
                    {
                        EvoEditorGUI.Property(nextText);
                        EvoEditorGUI.Property(completeText, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void LayoutFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Layout", false, FoldoutOptions.WithIcon("UI_Row")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(orientation, FieldOptions.WithInvertedColor());
                    DrawLayoutSection("Container", containerLayout);
                    EvoEditorGUI.AddPropertySpace();
                    DrawLayoutSection("Indicators", indicatorLayout);
                    EvoEditorGUI.AddPropertySpace();
                    DrawLayoutSection("Content", contentLayout);
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
                EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                DrawAnimationSection("Content", animationDuration, slideDistance, animationEase, animationCurve);
                EvoEditorGUI.AddPropertySpace();
                DrawAnimationSection("Indicators", indicatorDuration, null, indicatorEase, indicatorCurve);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        static void DrawAnimationSection(string title, SerializedProperty duration, SerializedProperty distance,
            SerializedProperty ease, SerializedProperty curve)
        {
            bool showCurve = ease.intValue == (int)Ease.Custom;
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.BeginContainer(title);
            {
                EvoEditorGUI.Property(duration, "Duration");
                if (distance != null)
                    EvoEditorGUI.Property(distance);

                EvoEditorGUI.Property(ease, "Ease", FieldOptions.WithSpace(showCurve));
                if (showCurve)
                    EvoEditorGUI.Property(curve, "Curve", FieldOptions.WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        void DrawLayoutSection(string title, SerializedProperty[] properties)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.BeginContainer(title);
            DrawProperties(properties, false);
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        void DrawFoldout(string title, string icon, SerializedProperty[] properties)
        {
            if (EvoEditorGUI.BeginFoldout(target, title, false, FoldoutOptions.WithIcon(icon)))
            {
                EvoEditorGUI.BeginContainer();
                DrawProperties(properties, true);
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        static void DrawProperties(SerializedProperty[] properties, bool inverted)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                FieldOptions options = inverted ? FieldOptions.WithInvertedColor() : default;
                if (i == properties.Length - 1)
                    options = options.WithoutSpace();

                EvoEditorGUI.Property(properties[i], options);
            }
        }

        void DrawItemsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            var stepper = (Stepper)target;
            EvoEditorGUI.Label(stepper.IsCompleted ? " Completed" : $" Selected Step: {currentIndex.intValue + 1} / {items.arraySize}");
            using (new EditorGUI.DisabledScope(Application.IsPlaying(stepper.gameObject)))
            {
                EvoEditorGUI.Slider(currentIndex, items.arraySize > 0 ? 0 : -1, Mathf.Max(-1, items.arraySize - 1),
                    "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawItem(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Stepper.Item.title)));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Stepper.Item.content)));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Stepper.Item.nextText)));
            EvoEditorGUI.Toggle(item.FindPropertyRelative(nameof(Stepper.Item.canContinue)));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Stepper.Item.onEnter)), FieldOptions.WithoutBackground().WithoutSpace());
            EvoEditorGUI.EndContainer();
        }

        static void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(Stepper.Item.title)).stringValue = "Step";
            item.FindPropertyRelative(nameof(Stepper.Item.content)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Stepper.Item.nextText)).stringValue = string.Empty;
            item.FindPropertyRelative(nameof(Stepper.Item.canContinue)).boolValue = true;
            item.FindPropertyRelative(nameof(Stepper.Item.onEnter)).FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
            item.isExpanded = true;
        }

        static string GetItemTitle(SerializedProperty item, int index)
        {
            string title = item.FindPropertyRelative(nameof(Stepper.Item.title)).stringValue;
            return string.IsNullOrEmpty(title) ? $"Step {index + 1}" : title;
        }

        void BeginEdit()
        {
            if (!Application.IsPlaying(((Stepper)target).gameObject))
                Undo.RegisterFullObjectHierarchyUndo(((Stepper)target).gameObject, "Edit Stepper");
        }

        void EndEdit()
        {
            var stepper = (Stepper)target;
            stepper.Refresh();

            // Undo reverts a group in reverse order. Recording the stepper again after Refresh lets redo
            // restore its references to created objects, which the BeginEdit record can't.
            if (!Application.IsPlaying(stepper.gameObject))
                Undo.RegisterCompleteObjectUndo(stepper, "Edit Stepper");

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            serializedObject.Update();
        }

        void RefreshAfterUndo()
        {
            if (target != null)
                ((Stepper)target).Refresh();
        }
    }
}