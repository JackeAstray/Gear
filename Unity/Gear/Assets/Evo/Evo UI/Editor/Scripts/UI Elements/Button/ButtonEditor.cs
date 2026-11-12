using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CanEditMultipleObjects, CustomEditor(typeof(Button))]
    public class ButtonEditor : SelectableEditor
    {
        // References
        SerializedProperty textObject;
        SerializedProperty textContainer;
        SerializedProperty imageObject;
        SerializedProperty iconElement;
        SerializedProperty contentFitter;
        SerializedProperty contentLayout;

        // Settings
        SerializedProperty interactable;
        SerializedProperty resolveRaycast;
        SerializedProperty allowDoubleClick;
        SerializedProperty doubleClickDuration;
        SerializedProperty transitionDuration;
        SerializedProperty interactionState;

        // Navigation
        SerializedProperty navigation;

        // Events
        SerializedProperty onClick;
        SerializedProperty onDoubleClick;
        SerializedProperty onPointerEnter;
        SerializedProperty onPointerExit;

        // Content
        SerializedObject[] buttonObjects;
        ContentSource[] contentSources;
        ContentSource iconSource;
        ContentSource iconSizeSource;
        ContentSource iconActiveSource;
        ContentSource textSource;
        ContentSource textActiveSource;
        ContentSource fitterSource;
        ContentSource layoutSource;

        protected override void OnEnable()
        {
            InitializeEditorState();
            UINavigationEditor.PrepareForVisualize(this);
        }

        void InitializeEditorState()
        {
            contentFitter = serializedObject.FindProperty(nameof(contentFitter));
            textObject = serializedObject.FindProperty(nameof(textObject));
            textContainer = serializedObject.FindProperty(nameof(textContainer));
            imageObject = serializedObject.FindProperty(nameof(imageObject));
            iconElement = serializedObject.FindProperty(nameof(iconElement));
            contentLayout = serializedObject.FindProperty(nameof(contentLayout));

            interactable = serializedObject.FindProperty("m_Interactable");
            resolveRaycast = serializedObject.FindProperty(nameof(resolveRaycast));
            allowDoubleClick = serializedObject.FindProperty(nameof(allowDoubleClick));
            transitionDuration = serializedObject.FindProperty(nameof(transitionDuration));
            doubleClickDuration = serializedObject.FindProperty(nameof(doubleClickDuration));
            interactionState = serializedObject.FindProperty(nameof(interactionState));

            navigation = serializedObject.FindProperty("m_Navigation");

            onClick = serializedObject.FindProperty(nameof(onClick));
            onDoubleClick = serializedObject.FindProperty(nameof(onDoubleClick));
            onPointerEnter = serializedObject.FindProperty(nameof(onPointerEnter));
            onPointerExit = serializedObject.FindProperty(nameof(onPointerExit));

            buttonObjects = new SerializedObject[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                buttonObjects[i] = new SerializedObject(targets[i]);

            iconSource = new ContentSource(buttonObjects, false, nameof(imageObject));
            iconSizeSource = new ContentSource(buttonObjects, false, nameof(iconElement));
            iconActiveSource = new ContentSource(buttonObjects, true, nameof(imageObject));
            textSource = new ContentSource(buttonObjects, false, nameof(textObject));
            textActiveSource = new ContentSource(buttonObjects, true, nameof(textObject), nameof(textContainer));
            fitterSource = new ContentSource(buttonObjects, false, nameof(contentFitter));
            layoutSource = new ContentSource(buttonObjects, false, nameof(contentLayout));
            contentSources = new[] { iconSource, iconSizeSource, iconActiveSource, textSource, textActiveSource, fitterSource, layoutSource };
        }

        protected override void OnDisable()
        {
            UINavigationEditor.RemoveFromVisualize(this);

            if (contentSources != null)
            {
                foreach (var source in contentSources)
                    source.Dispose();
            }

            if (buttonObjects != null)
            {
                foreach (var obj in buttonObjects)
                    obj.Dispose();
            }
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            foreach (var obj in buttonObjects)
                obj.Update();
            foreach (var source in contentSources)
                source.Update();

            EvoEditorGUI.BeginCenteredInspector();

            EditorGUI.BeginChangeCheck();
            ContentFoldout();
            SettingsFoldout();
            InteractiveEditor.EffectsFoldout(serializedObject);
            InteractiveEditor.NavigationFoldout(navigation);
            ReferencesFoldout();
            EventsFoldout();
            bool controlsChanged = EditorGUI.EndChangeCheck();

            EvoEditorGUI.EndCenteredInspector();

            foreach (var source in contentSources)
                source.serializedObject?.ApplyModifiedProperties();

            if (controlsChanged || serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }

        void ContentFoldout()
        {
            bool hasIconObject = iconSource.serializedObject != null;
            bool hasTextObject = textSource.serializedObject != null;
            bool hasLayout = layoutSource.serializedObject != null || fitterSource.serializedObject != null;
            if (!hasIconObject && !hasTextObject && !hasLayout)
                return;

            if (EvoEditorGUI.BeginFoldout(target, "Content", true, FoldoutOptions.WithIcon("UI_Object")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    if (hasIconObject)
                        IconContent();

                    if (hasTextObject)
                    {
                        if (hasIconObject)
                            EvoEditorGUI.AddPropertySpace();

                        TextContent();
#if EVO_LOCALIZATION
                        EvoEditorGUI.AddPropertySpace();
                        Localization.Editor.IntegrationGUI.LocalizationContainer(serializedObject, ((Button)target).gameObject, ((Button)target).textObject as TMPro.TMP_Text);
#endif
                    }

                    if (hasLayout)
                    {
                        if (hasIconObject || hasTextObject)
                            EvoEditorGUI.AddPropertySpace();

                        LayoutContent();
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout();
        }

        void IconContent()
        {
            var enableIcon = iconActiveSource.serializedObject.FindProperty("m_IsActive");
            var icon = iconSource.serializedObject.FindProperty("m_Sprite");
            bool hasIconElement = iconSizeSource.serializedObject != null;

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(enableIcon, "Icon", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (enableIcon.hasMultipleDifferentValues || enableIcon.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    EvoEditorGUI.Property(icon, "Source Sprite", hasIconElement ? default : FieldOptions.WithoutSpace());

                    if (hasIconElement)
                    {
                        var iconSize = iconSizeSource.serializedObject.FindProperty("m_PreferredWidth");
                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.Property(iconSize, "Icon Size", FieldOptions.WithoutSpace());
                        if (EditorGUI.EndChangeCheck())
                        {
                            iconSize.floatValue = Mathf.Max(0f, iconSize.floatValue);
                            iconSizeSource.serializedObject.FindProperty("m_PreferredHeight").floatValue = iconSize.floatValue;
                        }
                    }

                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn();
        }

        void TextContent()
        {
            // Text properties are named by the text provider; a selection mixing providers has none in common
            TextBackend backend = TextProvider.Find(((Button)target).textObject) ?? TextProvider.Active;
            var enableText = textActiveSource.serializedObject.FindProperty("m_IsActive");
            var text = textSource.serializedObject.FindProperty(backend.TextProperty);
            var textSize = textSource.serializedObject.FindProperty(backend.FontSizeProperties[0]);
            if (text == null || textSize == null)
                return;

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            {
                EvoEditorGUI.Toggle(enableText, "Text", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (enableText.hasMultipleDifferentValues || enableText.boolValue)
                {
                    EvoEditorGUI.BeginContainer(nestDepth: 1);
                    {
                        EvoEditorGUI.BeginRow();
                        {
                            GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);

                            Rect rect = EditorGUILayout.GetControlRect();
                            GUIContent content = new("Button Text");
                            using var propertyScope = new EditorGUI.PropertyScope(rect, content, text);
                            bool previousMixedValue = EditorGUI.showMixedValue;
                            EditorGUI.showMixedValue = text.hasMultipleDifferentValues;
                            EditorGUI.BeginChangeCheck();
                            string value = EditorGUI.TextField(rect, propertyScope.content, text.stringValue);
                            if (EditorGUI.EndChangeCheck())
                                text.stringValue = value;
                            EditorGUI.showMixedValue = previousMixedValue;
                        }
                        EvoEditorGUI.EndRow(LayoutOptions.WithSpace());

                        EditorGUI.BeginChangeCheck();
                        EvoEditorGUI.Property(textSize, "Text Size", FieldOptions.WithoutSpace());
                        if (EditorGUI.EndChangeCheck())
                        {
                            textSize.floatValue = Mathf.Max(0f, textSize.floatValue);
                            for (int i = 1; i < backend.FontSizeProperties.Count; i++)
                                textSource.serializedObject.FindProperty(backend.FontSizeProperties[i]).floatValue = textSize.floatValue;
                        }
                    }
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn();
        }

        void LayoutContent()
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor());
            EvoEditorGUI.BeginContainer("Layout", nestDepth: 1);
            {
                if (fitterSource.serializedObject != null)
                {
                    var dynamicScale = fitterSource.serializedObject.FindProperty("m_Enabled");
                    EvoEditorGUI.Toggle(dynamicScale, "Dynamic Scale", FieldOptions.WithSpace(layoutSource.serializedObject != null)
                        .WithTooltip("Sets the button size based on its content. Disable if parent has a layout group component."));
                }

                if (layoutSource.serializedObject != null)
                {
                    var reverseArrangement = layoutSource.serializedObject.FindProperty("m_ReverseArrangement");
                    var spacing = layoutSource.serializedObject.FindProperty("m_Spacing");
                    var padding = layoutSource.serializedObject.FindProperty("m_Padding");

                    EvoEditorGUI.Toggle(reverseArrangement, FieldOptions.WithTooltip("Change the content arrangement."));
                    EvoEditorGUI.Property(spacing, FieldOptions.WithTooltip("Add spacing between the icon and text."));
                    EvoEditorGUI.ArrayProperty(padding, FieldOptions.WithoutSpace().WithTooltip("Set the content padding."));
                }
            }
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();
        }

        void SettingsFoldout()
        {
            if (EvoEditorGUI.BeginFoldout(target, "Settings", true, FoldoutOptions.WithIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.Toggle(interactable, FieldOptions.WithInvertedColor().WithTooltip("Sets whether the button is interactable or not."));
                    EvoEditorGUI.Toggle(resolveRaycast, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Toggle(allowDoubleClick, FieldOptions.WithInvertedColor());

                    if (allowDoubleClick.hasMultipleDifferentValues || allowDoubleClick.boolValue)
                        EvoEditorGUI.Property(doubleClickDuration, FieldOptions.WithInvertedColor());

                    EvoEditorGUI.Property(transitionDuration, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(interactionState, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(imageObject, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(iconElement, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(textObject, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(textContainer, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(contentFitter, FieldOptions.WithInvertedColor());
                    EvoEditorGUI.Property(contentLayout, FieldOptions.WithInvertedColor().WithoutSpace());
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
                    EvoEditorGUI.Property(onClick, FieldOptions.WithoutBackground());

                    if (allowDoubleClick.hasMultipleDifferentValues || allowDoubleClick.boolValue)
                        EvoEditorGUI.Property(onDoubleClick, FieldOptions.WithoutBackground());

                    EvoEditorGUI.Property(onPointerEnter, FieldOptions.WithoutBackground());
                    EvoEditorGUI.Property(onPointerExit, FieldOptions.WithoutBackground().WithoutSpace());
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndFoldout(false);
        }

        sealed class ContentSource
        {
            readonly SerializedProperty[] references;
            readonly Object[] referenceObjects;
            readonly List<Object> sourceObjects = new();
            readonly bool useGameObjects;
            public SerializedObject serializedObject;

            public ContentSource(SerializedObject[] buttons, bool useGameObjects, params string[] referenceNames)
            {
                this.useGameObjects = useGameObjects;
                references = new SerializedProperty[buttons.Length * referenceNames.Length];
                referenceObjects = new Object[references.Length];
                int index = 0;
              
                foreach (var button in buttons)
                {
                    foreach (string referenceName in referenceNames)
                        references[index++] = button.FindProperty(referenceName);
                }
            }

            public void Update()
            {
                bool referencesChanged = false;

                for (int i = 0; i < references.Length; i++)
                {
                    Object obj = references[i].objectReferenceValue;
                    obj = obj == null ? null : useGameObjects ? ((Component)obj).gameObject : obj;
                    if (ReferenceEquals(referenceObjects[i], obj))
                        continue;

                    referenceObjects[i] = obj;
                    referencesChanged = true;
                }

                if (referencesChanged)
                {
                    Dispose();
                    sourceObjects.Clear();

                    foreach (Object obj in referenceObjects)
                    {
                        if (obj != null && !sourceObjects.Contains(obj))
                            sourceObjects.Add(obj);
                    }

                    if (sourceObjects.Count > 0)
                        serializedObject = new SerializedObject(sourceObjects.ToArray());
                }

                serializedObject?.Update();
            }

            public void Dispose()
            {
                serializedObject?.Dispose();
                serializedObject = null;
            }
        }
    }
}