using UnityEditor;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(PageManager))]
    public class PageManagerEditor : UnityEditor.Editor
    {
        // Pages
        SerializedProperty currentPageIndex;
        SerializedProperty pages;
        ReorderableListOptions pagesListOptions;

        // References
        SerializedProperty container;
        SerializedProperty indicator;

        // Settings
        SerializedProperty disableInvisiblePages;
        SerializedProperty useUnscaledTime;
        SerializedProperty interruptTransitions;
        SerializedProperty handleNestedScrolling;

        // Swipe Settings
        SerializedProperty swipeThreshold;
        SerializedProperty elasticResistance;
        SerializedProperty velocityThreshold;
        SerializedProperty swipeDirection;

        // Animation Settings
        SerializedProperty transitionCurve;
        SerializedProperty transitionDuration;
        SerializedProperty pageSpacing;

        // Events
        SerializedProperty onPageChanged;

        void OnEnable()
        {
            currentPageIndex = serializedObject.FindProperty(nameof(currentPageIndex));
            pages = serializedObject.FindProperty(nameof(pages));
            pagesListOptions = ReorderableListOptions.WithControlName("PageManager.Pages")
                .WithTitle("Pages")
                .WithIcon("UI_Group")
                .WithEmptyMessage("Add a page to populate this page manager.")
                .WithAddLabel("Add Page")
                .WithItemInitializer(InitializePage)
                .WithDeleteTooltip("Delete page")
                .WithChangeCallbacks(BeginPagesEdit, EndPagesEdit)
                .WithContentHeader(DrawPagesHeader);

            container = serializedObject.FindProperty(nameof(container));
            indicator = serializedObject.FindProperty(nameof(indicator));
            disableInvisiblePages = serializedObject.FindProperty(nameof(disableInvisiblePages));
            useUnscaledTime = serializedObject.FindProperty(nameof(useUnscaledTime));
            interruptTransitions = serializedObject.FindProperty(nameof(interruptTransitions));
            handleNestedScrolling = serializedObject.FindProperty(nameof(handleNestedScrolling));
            swipeThreshold = serializedObject.FindProperty(nameof(swipeThreshold));
            elasticResistance = serializedObject.FindProperty(nameof(elasticResistance));
            velocityThreshold = serializedObject.FindProperty(nameof(velocityThreshold));
            swipeDirection = serializedObject.FindProperty(nameof(swipeDirection));
            transitionCurve = serializedObject.FindProperty(nameof(transitionCurve));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            pageSpacing = serializedObject.FindProperty(nameof(pageSpacing));
            onPageChanged = serializedObject.FindProperty(nameof(onPageChanged));
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();
            EditorGUI.BeginChangeCheck();
            PagesFoldout();
            SettingsFoldout();
            AnimationFoldout();
            ReferencesFoldout();
            bool changed = EditorGUI.EndChangeCheck();
            EventsFoldout();
            EvoEditorGUI.EndCenteredInspector();

            bool applyPages = changed && serializedObject.hasModifiedProperties;
            if (applyPages)
                BeginPagesEdit();

            serializedObject.ApplyModifiedProperties();

            if (applyPages)
                EndPagesEdit();
        }

        void BeginPagesEdit() => ((PageManager)target).BeginPagesEdit();

        void EndPagesEdit() => ((PageManager)target).EndPagesEdit();

        void PagesFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, pages, GetPageTitle, DrawPage, pagesListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(useUnscaledTime, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(disableInvisiblePages, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(interruptTransitions, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(handleNestedScrolling, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Swipe Settings");
                    {
                        EvoEditorGUI.Property(swipeDirection);
                        EvoEditorGUI.Property(swipeThreshold);
                        EvoEditorGUI.Property(elasticResistance);
                        EvoEditorGUI.Property(velocityThreshold, FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();
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
                EvoEditorGUI.Property(pageSpacing, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(transitionCurve, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(container, FieldOptions.WithInvertedColor());
                EvoEditorGUI.Property(indicator, FieldOptions.WithInvertedColor().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                EvoEditorGUI.Property(onPageChanged, FieldOptions.WithoutBackground().WithoutSpace());
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        void DrawPagesHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int index = currentPageIndex.intValue;
                if (index < 0 || index >= pages.arraySize) { EvoEditorGUI.Label(" Current Page: None"); }
                else
                {
                    string pageID = pages.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(PageManager.Page.pageID)).stringValue;
                    EvoEditorGUI.Label(string.IsNullOrEmpty(pageID) ? $" Current Page: #{index} (no ID)" : $" Current Page: {pageID}");
                }

                EvoEditorGUI.Slider(currentPageIndex, pages.arraySize > 0 ? 0 : -1, pages.arraySize - 1,
                    "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        void DrawPage(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true, nestDepth: 1);
            {
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(PageManager.Page.pageID)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(PageManager.Page.pageObject)));
                EvoEditorGUI.Property(item.FindPropertyRelative(nameof(PageManager.Page.pageButton)), FieldOptions.WithoutSpace());
            }
            EvoEditorGUI.EndContainer();
        }

        void InitializePage(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(PageManager.Page.pageID)).stringValue = "Page";
            item.FindPropertyRelative(nameof(PageManager.Page.pageObject)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(PageManager.Page.pageButton)).objectReferenceValue = null;
            item.isExpanded = true;
        }

        string GetPageTitle(SerializedProperty item, int index)
        {
            string pageID = item.FindPropertyRelative(nameof(PageManager.Page.pageID)).stringValue;
            return string.IsNullOrEmpty(pageID) ? $"Page {index + 1}" : pageID;
        }
    }
}