using UnityEditor;
using Evo.EditorTools;
using Evo.Tween.Editor;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(Dropdown), true)]
    public class DropdownEditor : UnityEditor.Editor
    {
        protected Dropdown dTarget;

        // Object
        protected SerializedProperty selectedIndex;
        protected SerializedProperty items;
        protected ReorderableListOptions itemsListOptions;

        // Item Layout
        protected SerializedProperty itemSpacing;
        protected SerializedProperty itemHeight;
        protected SerializedProperty padding;

        // Settings
        protected SerializedProperty scrollPosition;
        protected SerializedProperty maxHeight;
        protected SerializedProperty blockUIWhileOpen;
        protected SerializedProperty closeOnItemSelect;
        protected SerializedProperty closeOnClickOutside;

        // Animation
        protected SerializedProperty openAnimation;
        protected SerializedProperty closeAnimation;
        protected TweenClipGUI openAnimationGUI;
        protected TweenClipGUI closeAnimationGUI;
        protected TweenSequencePreviewGUI openPreviewGUI;
        protected TweenSequencePreviewGUI closePreviewGUI;

        // References
        protected SerializedProperty itemPrefab;
        protected SerializedProperty itemParent;
        protected SerializedProperty headerButton;
        protected SerializedProperty scrollRect;

        // Events
        protected SerializedProperty onItemSelected;
        protected SerializedProperty onOpen;
        protected SerializedProperty onClose;

        protected virtual string ItemsControlName => "Dropdown.Items";
        protected virtual string EmptyItemsMessage => "Add an item to populate this dropdown.";
        protected virtual bool ShowCloseOnItemSelect => true;

        protected virtual void OnEnable() => InitializeEditorState();

        protected virtual void OnDisable()
        {
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ItemsFoldout();
            SettingsFoldout();
            AnimationsFoldout();
            NavigationFoldout();
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();
            ApplyPendingChanges(controlsChanged);
        }

        protected void InitializeEditorState()
        {
            dTarget = (Dropdown)target;

            selectedIndex = serializedObject.FindProperty(nameof(selectedIndex));
            items = serializedObject.FindProperty(nameof(items));
            itemsListOptions = ReorderableListOptions.WithControlName(ItemsControlName)
                .WithTitle("Items")
                .WithIcon("UI_Object")
                .WithEmptyMessage(EmptyItemsMessage)
                .WithAddLabel("Add Item")
                .WithItemInitializer(InitializeItem)
                .WithDeleteTooltip("Delete item")
                .WithContentHeader(DrawItemsHeader);

            itemSpacing = serializedObject.FindProperty(nameof(itemSpacing));
            itemHeight = serializedObject.FindProperty(nameof(itemHeight));
            padding = serializedObject.FindProperty(nameof(padding));

            scrollPosition = serializedObject.FindProperty(nameof(scrollPosition));
            maxHeight = serializedObject.FindProperty(nameof(maxHeight));
            blockUIWhileOpen = serializedObject.FindProperty(nameof(blockUIWhileOpen));
            closeOnItemSelect = serializedObject.FindProperty(nameof(closeOnItemSelect));
            closeOnClickOutside = serializedObject.FindProperty(nameof(closeOnClickOutside));

            openAnimation = serializedObject.FindProperty(nameof(openAnimation));
            closeAnimation = serializedObject.FindProperty(nameof(closeAnimation));
            openAnimationGUI = new TweenClipGUI("Open Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            closeAnimationGUI = new TweenClipGUI("Close Animation", compactFoldouts: true,
                showHeaderIcon: false, showItemIcons: true, invertColors: true, boldHeader: false);
            openPreviewGUI?.Dispose();
            closePreviewGUI?.Dispose();
            openPreviewGUI = new TweenSequencePreviewGUI(this, dTarget, () => dTarget.openAnimation, openAnimation.propertyPath);
            closePreviewGUI = new TweenSequencePreviewGUI(this, dTarget, () => dTarget.closeAnimation, closeAnimation.propertyPath);
            openPreviewGUI.BeforePreview = closePreviewGUI.StopPreview;
            closePreviewGUI.BeforePreview = openPreviewGUI.StopPreview;

            itemPrefab = serializedObject.FindProperty(nameof(itemPrefab));
            itemParent = serializedObject.FindProperty(nameof(itemParent));
            headerButton = serializedObject.FindProperty(nameof(headerButton));
            scrollRect = serializedObject.FindProperty(nameof(scrollRect));

            onItemSelected = serializedObject.FindProperty(nameof(onItemSelected));
            onOpen = serializedObject.FindProperty(nameof(onOpen));
            onClose = serializedObject.FindProperty(nameof(onClose));

            InitializeAdditionalState();
        }

        protected virtual void InitializeAdditionalState() { }

        void ApplyPendingChanges(bool controlsChanged)
        {
            bool hasChanges = controlsChanged || serializedObject.hasModifiedProperties;
            if (hasChanges)
                BeginLiveEdit();

            serializedObject.ApplyModifiedProperties();

            bool isDragging = openAnimationGUI?.IsDragging == true || closeAnimationGUI?.IsDragging == true;
            if (hasChanges && !isDragging)
                EndLiveEdit();
        }

        protected void BeginLiveEdit()
        {
            openPreviewGUI?.BeginLiveEdit();
            closePreviewGUI?.BeginLiveEdit();
        }

        protected void EndLiveEdit()
        {
            openPreviewGUI?.EndLiveEdit();
            closePreviewGUI?.EndLiveEdit();
        }

        protected virtual void DrawItemsHeader()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                int index = selectedIndex.intValue;

                if (selectedIndex.hasMultipleDifferentValues) { EvoEditorGUI.Label(" Selected Item: Mixed"); }
                else if (index < 0 || index >= items.arraySize) { EvoEditorGUI.Label(" Selected Item: None"); }
                else
                {
                    SerializedProperty label = items.GetArrayElementAtIndex(index).FindPropertyRelative(nameof(Dropdown.Item.label));
                    EvoEditorGUI.Label(string.IsNullOrEmpty(label.stringValue)
                        ? $" Selected Item: #{index} (no label)"
                        : $" Selected Item: {label.stringValue}");
                }

                int maxIndex = items.arraySize > 0 ? items.arraySize - 1 : 0;
                EvoEditorGUI.Slider(selectedIndex, -1, maxIndex, "Index", FieldOptions.WithoutBackground().WithoutSpace().WithLabelWidth(40));
            }
            EvoEditorGUI.EndColumn();
            EvoEditorGUI.AddPropertySpace();
        }

        protected virtual void ItemsFoldout()
        {
            EvoEditorGUI.ReorderableList(serializedObject, items, GetItemTitle, DrawItem, itemsListOptions, compactItems: true);
            EvoEditorGUI.AddFoldoutSpace();
        }

        protected virtual void DrawItem(SerializedProperty item)
        {
            EvoEditorGUI.BeginContainer(compactHeader: true);
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Dropdown.Item.label)));
            EvoEditorGUI.Property(item.FindPropertyRelative(nameof(Dropdown.Item.icon)), FieldOptions.WithoutSpace());

#if EVO_LOCALIZATION
            if (Localization.Editor.IntegrationGUI.IsLocalizationEnabled(serializedObject))
            {
                EvoEditorGUI.AddLayoutSpace();
                Localization.Editor.IntegrationGUI.KeyField(item.FindPropertyRelative(nameof(Dropdown.Item.tableKey)), "Localization Key");
            }
#endif
            DrawAdditionalItemProperties(item);
            EvoEditorGUI.EndContainer();
        }

        protected virtual void DrawAdditionalItemProperties(SerializedProperty item) { }

        protected virtual void InitializeItem(SerializedProperty item)
        {
            item.FindPropertyRelative(nameof(Dropdown.Item.label)).stringValue = "Item";
            item.FindPropertyRelative(nameof(Dropdown.Item.icon)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Dropdown.Item.defaultSelected)).boolValue = false;
            item.FindPropertyRelative(nameof(Dropdown.Item.index)).intValue = 0;
            item.FindPropertyRelative(nameof(Dropdown.Item.generatedButton)).objectReferenceValue = null;
            item.FindPropertyRelative(nameof(Dropdown.Item.multiSelectItem)).objectReferenceValue = null;
#if EVO_LOCALIZATION
            item.FindPropertyRelative(nameof(Dropdown.Item.tableKey)).stringValue = string.Empty;
#endif
            item.isExpanded = true;
        }

        protected virtual void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", false, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(scrollPosition, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(blockUIWhileOpen, FieldOptions.WithInvertedColor());
                    if (ShowCloseOnItemSelect)
                        EvoEditorGUI.Toggle(closeOnItemSelect, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(closeOnClickOutside, FieldOptions.WithInvertedColor());

                    DrawAdditionalSettings();

                    EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
                    EvoEditorGUI.BeginContainer("Layout");
                    {
                        EvoEditorGUI.Property(maxHeight);
                        EvoEditorGUI.Property(itemHeight);
                        EvoEditorGUI.Property(itemSpacing);
                        EvoEditorGUI.ArrayProperty(padding, "Item Padding", FieldOptions.WithoutSpace());
                    }
                    EvoEditorGUI.EndContainer();
                    EvoEditorGUI.EndColumn();

#if EVO_LOCALIZATION
                    EvoEditorGUI.AddLayoutSpace();
                    Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, dTarget.gameObject);
#endif

                    if (scrollRect.objectReferenceValue != null)
                    {
                        EvoEditorGUI.AddPropertySpace();
                        if (EvoEditorGUI.Button("Virtualization Settings", options: ButtonOptions.WithInvertedColor()))
                        {
                            EvoEditorGUI.SetFoldoutState(scrollRect.objectReferenceValue, "Settings", true);
                            Selection.activeObject = scrollRect.objectReferenceValue;
                        }
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        protected virtual void AnimationsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Animations", false, FoldoutOptions.WithIcon("UI_Animation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    openAnimationGUI.DrawSequence(serializedObject, openAnimation, BeginLiveEdit, EndLiveEdit, openPreviewGUI.DrawControls);
                    EvoEditorGUI.AddPropertySpace();
                    closeAnimationGUI.DrawSequence(serializedObject, closeAnimation, BeginLiveEdit, EndLiveEdit, closePreviewGUI.DrawControls);
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        protected virtual void NavigationFoldout()
        {
            if (headerButton.objectReferenceValue == null)
                return;

            if (EvoEditorGUI.BeginFoldout(target, "Navigation", false, FoldoutOptions.WithIcon("UI_Navigation")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Callout("UI Navigation is handled by the header button.", invertColor: true, spaceBefore: 0);

                    if (EvoEditorGUI.Button("Select Header Button", options: ButtonOptions.WithInvertedColor()))
                    {
                        EvoEditorGUI.SetFoldoutState(headerButton.objectReferenceValue, "Navigation", true);
                        Selection.activeObject = headerButton.objectReferenceValue;
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        protected virtual void ReferencesFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "References", false, FoldoutOptions.WithIcon("UI_Reference")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Property(itemPrefab, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(itemParent, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(headerButton, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(scrollRect, FieldOptions.WithInvertedColor().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        protected virtual void EventsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Events", false, FoldoutOptions.WithIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    DrawSelectionEvents();
                    EvoEditorGUI.Property(onOpen, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onClose, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        protected virtual void DrawAdditionalSettings() { }

        protected virtual void DrawSelectionEvents()
        {
            EvoEditorGUI.Property(onItemSelected, FieldOptions.WithoutBackground());
        }

        protected virtual string GetItemTitle(SerializedProperty item, int index)
        {
            string label = item.FindPropertyRelative(nameof(Dropdown.Item.label)).stringValue;
            return string.IsNullOrEmpty(label) ? $"Item {index + 1}" : label;
        }
    }
}