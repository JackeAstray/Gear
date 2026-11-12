using UnityEditor;
using UnityEngine;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(TabManager))]
    public class TabManagerEditor : UnityEditor.Editor
    {
        TabManager tmTarget;

        // Tabs
        SerializedProperty currentTabIndex;
        SerializedProperty tabs;
        ReorderableListOptions tabsListOptions;

        // Settings
        SerializedProperty disableInvisibleTabs;
        SerializedProperty useUnscaledTime;
        SerializedProperty activationTiming;

        // Animations
        SerializedProperty animateDirectionally;
        SerializedProperty inAnimation;
        SerializedProperty outAnimation;
        TweenClipGUI inAnimationGUI;
        TweenClipGUI outAnimationGUI;
        TweenSequencePreviewGUI inPreviewGUI;
        TweenSequencePreviewGUI outPreviewGUI;

        // Title Display
        SerializedProperty animateTitleDirectionally;
        SerializedProperty titleSlideCurve;
        SerializedProperty titleSlideDuration;
        SerializedProperty titleChangeDelay;
        SerializedProperty titleSlideOffset;

        // Indicator
        SerializedProperty indicatorDirection;
        SerializedProperty indicatorAutoSize;
        SerializedProperty indicatorStretch;
        SerializedProperty indicatorCurve;
        SerializedProperty indicatorDuration;

        // References
        SerializedProperty titleObject;
        SerializedProperty indicatorObject;

        // Events
        SerializedProperty onTabChanged;

        // Tab animations play on the current tab, so previews and the timeline should too
        [InitializeOnLoadMethod]
        static void RegisterPreviewRoot() => TweenPreviewRoot.Register<TabManager>(manager =>
            manager.CurrentTab?.tabObject is RectTransform tab && tab != null ? tab.gameObject : null);

        void OnEnable()
        {
            tmTarget = (TabManager)target;

            currentTabIndex = serializedObject.FindProperty(nameof(currentTabIndex));
            tabs = serializedObject.FindProperty(nameof(tabs));
            tabsListOptions = ReorderableListOptions.WithControlName("TabManager.Tabs")
                .WithTitle("Tabs")
                .WithIcon("UI_Group")
                .WithEmptyMessage("Add a tab to populate this tab manager.")
                .WithAddLabel("Add Tab")
                .WithItemInitializer(InitializeTab)
                .WithDeleteTooltip("Delete tab")
                .WithChangeCallbacks(BeginTabsEdit, EndTabsEdit)
                .WithContentHeader(DrawTabsHeader);

            disableInvisibleTabs = serializedObject.FindProperty(nameof(disableInvisibleTabs));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            activationTiming = serializedObject.FindProperty(nameof(activationTiming));

            animateDirectionally = serializedObject.FindProperty(nameof(animateDirectionally));
            inAnimation = serializedObject.FindProperty(nameof(inAnimation));
            outAnimation = serializedObject.FindProperty(nameof(outAnimation));
            inAnimationGUI = new TweenClipGUI("In Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            outAnimationGUI = new TweenClipGUI("Out Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            inPreviewGUI?.Dispose();
            outPreviewGUI?.Dispose();
            inPreviewGUI = new TweenSequencePreviewGUI(this, tmTarget, () => tmTarget.inAnimation, inAnimation.propertyPath);
            outPreviewGUI = new TweenSequencePreviewGUI(this, tmTarget, () => tmTarget.outAnimation, outAnimation.propertyPath);
            inPreviewGUI.BeforePreview = outPreviewGUI.StopPreview;
            outPreviewGUI.BeforePreview = inPreviewGUI.StopPreview;

            animateTitleDirectionally = serializedObject.FindProperty(nameof(animateTitleDirectionally));
            titleSlideCurve = serializedObject.FindProperty(nameof(titleSlideCurve));
            titleSlideDuration = serializedObject.FindProperty(nameof(titleSlideDuration));
            titleChangeDelay = serializedObject.FindProperty(nameof(titleChangeDelay));
            titleSlideOffset = serializedObject.FindProperty(nameof(titleSlideOffset));

            indicatorDirection = serializedObject.FindProperty(nameof(indicatorDirection));
            indicatorAutoSize = serializedObject.FindProperty(nameof(indicatorAutoSize));
            indicatorStretch = serializedObject.FindProperty(nameof(indicatorStretch));
            indicatorCurve = serializedObject.FindProperty(nameof(indicatorCurve));
            indicatorDuration = serializedObject.FindProperty(nameof(indicatorDuration));

            titleObject = serializedObject.FindProperty(nameof(titleObject));
            indicatorObject = serializedObject.FindProperty(nameof(indicatorObject));
            onTabChanged = serializedObject.FindProperty(nameof(onTabChanged));
        }

        void OnDisable()
        {
            inPreviewGUI?.Dispose();
            outPreviewGUI?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            TabsFoldout();
            bool tabsChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            SettingsFoldout();
            AnimationsFoldout();
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();
            ApplyPendingChanges(tabsChanged, controlsChanged);
        }

        void ApplyPendingChanges(bool tabsChanged, bool controlsChanged)
        {
            bool applyTabs = tabsChanged && serializedObject.hasModifiedProperties;
            bool hasChanges = controlsChanged || serializedObject.hasModifiedProperties;

            if (applyTabs)
                BeginTabsEdit();
            if (hasChanges)
                BeginLiveEdit();

            serializedObject.ApplyModifiedProperties();

            if (applyTabs)
                EndTabsEdit();

            bool isDragging = inAnimationGUI?.IsDragging == true || outAnimationGUI?.IsDragging == true;
            if (hasChanges && !isDragging)
                EndLiveEdit();
        }

        void BeginTabsEdit()
        {
            // Restore previewed values first, including the timeline's, otherwise restoring them later would undo the new tab visibility
            TweenSequencePreviewGUI.StopPreviews(tmTarget);
            tmTarget.BeginTabsEdit();
        }

        void EndTabsEdit() => tmTarget.EndTabsEdit();

        void BeginLiveEdit()
        {
            inPreviewGUI?.BeginLiveEdit();
            outPreviewGUI?.BeginLiveEdit();
        }

        void EndLiveEdit()
        {
            inPreviewGUI?.EndLiveEdit();
            outPreviewGUI?.EndLiveEdit();
        }

        void TabsFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, tabs, GetTabTitle, DrawTab, tabsListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(disableInvisibleTabs, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(activationTiming, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void AnimationsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(animateDirectionally, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Callout("Leave clip targets empty to animate each tab. " +
                        "Use Start From Current for a smooth exit when switching rapidly.", invertColor: true, spaceBefore: 2);
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                    {
                        inAnimationGUI.DrawSequence(serializedObject, inAnimation, BeginLiveEdit, EndLiveEdit, inPreviewGUI.DrawControls);
                        EvoEditorGUI.AddPropertySpace();
                        outAnimationGUI.DrawSequence(serializedObject, outAnimation, BeginLiveEdit, EndLiveEdit, outPreviewGUI.DrawControls);
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
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(titleObject, FieldOptions.WithoutBackground().WithoutSpace());
                    if (titleObject.objectReferenceValue != null)
                    {
                        EvoEditorGUI.BeginContainer();
                        {
                            EvoEditorGUI.Toggle(animateTitleDirectionally, "Animate Directionally");
                            EvoEditorGUI.Property(titleSlideCurve, "Animation Curve");
                            EvoEditorGUI.Property(titleSlideDuration, "Animation Duration");
                            EvoEditorGUI.Property(titleChangeDelay, "Change Delay");
                            EvoEditorGUI.Property(titleSlideOffset, "Animation Offset", FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();

                    EvoEditorGUI.AddLayoutSpace();
                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.Property(indicatorObject, FieldOptions.WithoutBackground().WithoutSpace());
                    if (indicatorObject.objectReferenceValue != null)
                    {
                        EvoEditorGUI.BeginContainer();
                        {
                            EvoEditorGUI.Toggle(indicatorAutoSize, "Auto Size");
                            EvoEditorGUI.Property(indicatorDirection, "Direction");
                            EvoEditorGUI.Property(indicatorStretch, "Elastic Stretch");
                            EvoEditorGUI.Property(indicatorCurve, "Animation Curve");
                            EvoEditorGUI.Property(indicatorDuration, "Animation Duration", FieldOptions.WithoutSpace());
                        }
                        EvoEditorGUI.EndContainer();
                    }
                    EvoEditorGUI.EndColumn();
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
                EvoEditorGUI.Property(onTabChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void DrawTabsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int index = currentTabIndex.intValue;
                if (index < 0 || index >= tabs.arraySize) { EvoEditorGUI.Label(" Current Tab: None"); }
                else
                {
                    string tabID = tabs.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(TabManager.Tab.tabID)).stringValue;
                    EvoEditorGUI.Label(string.IsNullOrEmpty(tabID) ? $" Current Tab: #{index} (no ID)" : $" Current Tab: {tabID}");
                }

                EvoEditorGUI.Slider(currentTabIndex, tabs.arraySize > 0 ? 0 : -1, tabs.arraySize - 1,
                    "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawTab(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(TabManager.Tab.tabID)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(TabManager.Tab.tabObject)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(TabManager.Tab.tabButton)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(TabManager.Tab.firstSelected)), FieldOptions.WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        void InitializeTab(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(TabManager.Tab.tabID)).stringValue = "Tab";
            item.FindPropertyRelative(nameof(TabManager.Tab.tabObject)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(TabManager.Tab.tabButton)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(TabManager.Tab.firstSelected)).objectReferenceValue = null;
            item.isExpanded = true;
        }

        string GetTabTitle(SerializedProperty item, int index)
        {
            string tabID = item.FindPropertyRelative(nameof(TabManager.Tab.tabID)).stringValue;
            return string.IsNullOrEmpty(tabID) ? $"Tab {index + 1}" : tabID;
        }
    }
}