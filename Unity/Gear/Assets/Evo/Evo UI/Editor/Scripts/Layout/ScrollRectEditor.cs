using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScrollRect), true)]
    public class ScrollRectEditor : UnityEditor.Editor
    {
        // Native properties
        SerializedProperty content;
        SerializedProperty horizontal;
        SerializedProperty vertical;
        SerializedProperty movementType;
        SerializedProperty elasticity;
        SerializedProperty inertia;
        SerializedProperty decelerationRate;
        SerializedProperty scrollSensitivity;
        SerializedProperty viewport;
        SerializedProperty horizontalScrollbar;
        SerializedProperty horizontalScrollbarVisibility;
        SerializedProperty horizontalScrollbarSpacing;
        SerializedProperty verticalScrollbar;
        SerializedProperty verticalScrollbarVisibility;
        SerializedProperty verticalScrollbarSpacing;
        SerializedProperty onValueChanged;

        // Smooth Scrolling
        SerializedProperty enableSmoothScroll;
        SerializedProperty smoothScrollTime;

        // Snapping
        SerializedProperty enableSnapping;
        SerializedProperty startingIndex;
        SerializedProperty snapDuration;
        SerializedProperty snapCurve;
        SerializedProperty disableUnfocused;

        // Infinite Scrolling
        SerializedProperty enableInfiniteScroll;

        // Virtualization
        SerializedProperty enableVirtualization;
        SerializedProperty virtualizationOverscan;

        // Scaling
        SerializedProperty enableScaling;
        SerializedProperty scaleDistance;
        SerializedProperty minScale;

        // Fading
        SerializedProperty enableFading;
        SerializedProperty fadeDistance;
        SerializedProperty minAlpha;

        // Events
        SerializedProperty onItemFocused;

        void OnEnable()
        {
            content = serializedObject.FindProperty("m_Content");
            horizontal = serializedObject.FindProperty("m_Horizontal");
            vertical = serializedObject.FindProperty("m_Vertical");
            movementType = serializedObject.FindProperty("m_MovementType");
            elasticity = serializedObject.FindProperty("m_Elasticity");
            inertia = serializedObject.FindProperty("m_Inertia");
            decelerationRate = serializedObject.FindProperty("m_DecelerationRate");
            scrollSensitivity = serializedObject.FindProperty("m_ScrollSensitivity");
            viewport = serializedObject.FindProperty("m_Viewport");
            horizontalScrollbar = serializedObject.FindProperty("m_HorizontalScrollbar");
            horizontalScrollbarVisibility = serializedObject.FindProperty("m_HorizontalScrollbarVisibility");
            horizontalScrollbarSpacing = serializedObject.FindProperty("m_HorizontalScrollbarSpacing");
            verticalScrollbar = serializedObject.FindProperty("m_VerticalScrollbar");
            verticalScrollbarVisibility = serializedObject.FindProperty("m_VerticalScrollbarVisibility");
            verticalScrollbarSpacing = serializedObject.FindProperty("m_VerticalScrollbarSpacing");
            onValueChanged = serializedObject.FindProperty("m_OnValueChanged");

            enableSmoothScroll = serializedObject.FindProperty(nameof(enableSmoothScroll));
            smoothScrollTime = serializedObject.FindProperty(nameof(smoothScrollTime));

            enableInfiniteScroll = serializedObject.FindProperty(nameof(enableInfiniteScroll));
            enableVirtualization = serializedObject.FindProperty(nameof(enableVirtualization));
            virtualizationOverscan = serializedObject.FindProperty(nameof(virtualizationOverscan));

            enableSnapping = serializedObject.FindProperty(nameof(enableSnapping));
            startingIndex = serializedObject.FindProperty(nameof(startingIndex));
            snapDuration = serializedObject.FindProperty(nameof(snapDuration));
            snapCurve = serializedObject.FindProperty(nameof(snapCurve));
            disableUnfocused = serializedObject.FindProperty(nameof(disableUnfocused));

            enableScaling = serializedObject.FindProperty(nameof(enableScaling));
            scaleDistance = serializedObject.FindProperty(nameof(scaleDistance));
            minScale = serializedObject.FindProperty(nameof(minScale));

            enableFading = serializedObject.FindProperty(nameof(enableFading));
            fadeDistance = serializedObject.FindProperty(nameof(fadeDistance));
            minAlpha = serializedObject.FindProperty(nameof(minAlpha));

            onItemFocused = serializedObject.FindProperty(nameof(onItemFocused));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            ReferencesFoldout();
            EffectsFoldout();
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
                    EvoEditorGUI.Property(scrollSensitivity, FieldOptions.WithInvertedColor());
                    MovementTypeField(movementType, elasticity);
                    EvoEditorGUI.Toggle(horizontal, "Is Horizontal", FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(vertical, "Is Vertical", FieldOptions.WithInvertedColor());
                    InertiaField(inertia, decelerationRate);
                    SmoothScrollField(enableSmoothScroll, smoothScrollTime);
                    EvoEditorGUI.Toggle(enableInfiniteScroll, "Infinite Scroll", FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableVirtualization, "Virtualization", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableVirtualization.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(virtualizationOverscan, "Overscan", FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        // Also drawn by editors of elements that generate a Scroll Rect, such as List View
        internal static void MovementTypeField(SerializedProperty movementType, SerializedProperty elasticity, bool addSpace = true)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Property(movementType, FieldOptions.WithoutBackground().WithoutSpace());
                if (movementType.hasMultipleDifferentValues || movementType.enumValueIndex == (int)ScrollRect.MovementType.Elastic)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.Property(elasticity, FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }

        internal static void InertiaField(SerializedProperty inertia, SerializedProperty decelerationRate, bool addSpace = true)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(inertia, FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (inertia.hasMultipleDifferentValues || inertia.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.Property(decelerationRate, FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }

        internal static void SmoothScrollField(SerializedProperty enableSmoothScroll, SerializedProperty smoothScrollTime, bool addSpace = true)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(enableSmoothScroll, "Smooth Scroll", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (enableSmoothScroll.hasMultipleDifferentValues || enableSmoothScroll.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.Property(smoothScrollTime, "Response Time", FieldOptions.WithoutSpace());
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }

        void EffectsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Effects", true, FoldoutOptions.WithIcon("UI_Effect")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableSnapping, "Snapping", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableSnapping.hasMultipleDifferentValues || enableSnapping.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(startingIndex);
                            EvoEditorGUI.Property(snapDuration);
                            EvoEditorGUI.Property(snapCurve);
                            EvoEditorGUI.Toggle(disableUnfocused, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddPropertySpace();
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableFading, "Fading", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableFading.hasMultipleDifferentValues || enableFading.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(minAlpha);
                            EvoEditorGUI.Property(fadeDistance, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddPropertySpace();
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    {
                        EvoEditorGUI.Toggle(enableScaling, "Scaling", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                        if (enableScaling.hasMultipleDifferentValues || enableScaling.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(nestDepth: 1);
                            EvoEditorGUI.Property(minScale);
                            EvoEditorGUI.Property(scaleDistance, FieldOptions.WithoutSpace());
                            EvoEditorGUI.EndContainer();
                        }
                    }
                    EvoEditorGUI.EndColumn();
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", true, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(content, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(viewport, FieldOptions.WithInvertedColor().WithoutSpace());
                    if (horizontal.hasMultipleDifferentValues || horizontal.boolValue)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            EvoEditorGUI.Property(horizontalScrollbar, FieldOptions.WithoutBackground().WithoutSpace());
                            if (horizontalScrollbar.hasMultipleDifferentValues || horizontalScrollbar.objectReferenceValue != null)
                            {
                                EvoEditorGUI.BeginContainer(nestDepth: 1);
                                bool showSpacing = horizontalScrollbarVisibility.hasMultipleDifferentValues ||
                                    horizontalScrollbarVisibility.enumValueIndex == (int)ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                                EvoEditorGUI.Property(horizontalScrollbarVisibility, "Visibility", showSpacing ? default : FieldOptions.WithoutSpace());

                                if (showSpacing)
                                    EvoEditorGUI.Property(horizontalScrollbarSpacing, "Spacing", FieldOptions.WithoutSpace());

                                EvoEditorGUI.EndContainer();
                            }
                        }
                        EvoEditorGUI.EndColumn();
                    }
                    if (vertical.hasMultipleDifferentValues || vertical.boolValue)
                    {
                        EvoEditorGUI.AddLayoutSpace();
                        EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                        {
                            EvoEditorGUI.Property(verticalScrollbar, FieldOptions.WithoutBackground().WithoutSpace());
                            if (verticalScrollbar.hasMultipleDifferentValues || verticalScrollbar.objectReferenceValue != null)
                            {
                                EvoEditorGUI.BeginContainer(nestDepth: 1);
                                bool showSpacing = verticalScrollbarVisibility.hasMultipleDifferentValues ||
                                    verticalScrollbarVisibility.enumValueIndex == (int)ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                                EvoEditorGUI.Property(verticalScrollbarVisibility, "Visibility", showSpacing ? default : FieldOptions.WithoutSpace());

                                if (showSpacing)
                                    EvoEditorGUI.Property(verticalScrollbarSpacing, "Spacing", FieldOptions.WithoutSpace());

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

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(onValueChanged, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onItemFocused, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }
    }
}