using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    [CustomPropertyDrawer(typeof(TweenClipSettings))]
    public sealed class TweenClipSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            if (!property.isExpanded)
                return line;

            float height = line + (line + spacing) * 5f;
            height += GetHeaderHeight(spacing);
            SerializedProperty ease = property.FindPropertyRelative("ease");
            if (ease != null && (Ease)ease.enumValueIndex == Ease.Custom)
                height += line + spacing;

            SerializedProperty loop = property.FindPropertyRelative("loop");
            if (loop != null && (LoopMode)loop.enumValueIndex != LoopMode.None)
                height += line + spacing;

            SerializedProperty module = property.FindPropertyRelative("module");
            TweenClipGUI.ModuleLayout layout = module?.managedReferenceValue is TweenModule moduleValue
                ? TweenClipGUI.GetModuleLayout(moduleValue.GetType())
                : null;

            if (layout != null)
            {
                height += GetValuesHeight(property, module, layout, spacing);
                if (TweenClipGUI.HasVisibleFields(module, property, layout.Settings, true))
                {
                    height += GetHeaderHeight(spacing);
                    height += GetFieldsHeight(module, property, layout.Settings, true, spacing);
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new(position.x, position.y, position.width, line);
            GUIContent displayLabel = label.text.StartsWith("Element ", StringComparison.Ordinal)
                ? new GUIContent(TweenClipGUI.GetDefaultSettingsTitle(property), label.tooltip)
                : label;
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, displayLabel, true);
            HandleContextMenu(row, property);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                SerializedProperty target = FindSiblingTarget(property);
                Component owner = property.serializedObject.targetObject as Component;

                NextRow(ref row, line, spacing);
                TweenClipGUI.DrawTypePopup(row, property, target, owner);

                DrawHeader(ref row, "Clip", spacing);
                DrawPropertyRow(ref row, property.FindPropertyRelative("ease"), spacing);
                SerializedProperty ease = property.FindPropertyRelative("ease");
                if (ease != null && (Ease)ease.enumValueIndex == Ease.Custom)
                    DrawPropertyRow(ref row, property.FindPropertyRelative("customEase"), spacing);

                DrawPropertyRow(ref row, property.FindPropertyRelative("duration"), spacing);
                DrawPropertyRow(ref row, property.FindPropertyRelative("delay"), spacing);
                SerializedProperty loop = property.FindPropertyRelative("loop");
                DrawPropertyRow(ref row, loop, spacing);
                if (loop != null && (LoopMode)loop.enumValueIndex != LoopMode.None)
                {
                    EditorGUI.indentLevel++;
                    DrawPropertyRow(ref row, property.FindPropertyRelative("iterations"), spacing);
                    EditorGUI.indentLevel--;
                }

                SerializedProperty module = property.FindPropertyRelative("module");
                TweenClipGUI.ModuleLayout layout = module?.managedReferenceValue is TweenModule moduleValue
                    ? TweenClipGUI.GetModuleLayout(moduleValue.GetType())
                    : null;

                if (layout != null)
                {
                    DrawValues(ref row, module, property, layout, spacing);
                    if (TweenClipGUI.HasVisibleFields(module, property, layout.Settings, true))
                    {
                        DrawHeader(ref row, "Settings", spacing);
                        DrawFields(ref row, module, property, layout.Settings, true, spacing);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        static void DrawValues(ref Rect row, SerializedProperty module, SerializedProperty settings,
            TweenClipGUI.ModuleLayout layout, float spacing)
        {
            bool showFrom = TweenClipGUI.ShouldShowFrom(module, layout.FromValues);

            if (TweenClipGUI.HasVisibleFields(module, settings, layout.FromValues, showFrom))
            {
                DrawHeader(ref row, "From", spacing);
                DrawFields(ref row, module, settings, layout.FromValues, showFrom, spacing);

                if (TweenClipGUI.ShowsHoldBeforeStart(module, layout))
                    DrawPropertyRow(ref row, settings.FindPropertyRelative("holdBeforeStart"), spacing);
            }

            if (TweenClipGUI.HasVisibleFields(module, settings, layout.ToValues, true))
            {
                DrawHeader(ref row, "To", spacing);
                DrawFields(ref row, module, settings, layout.ToValues, true, spacing);
            }

            DrawFields(ref row, module, settings, layout.OtherValues, true, spacing);
        }

        static void DrawFields(ref Rect row, SerializedProperty module, SerializedProperty settings,
            List<TweenClipGUI.FieldLayout> fields, bool showFrom, float spacing)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                TweenClipGUI.FieldLayout field = fields[i];
                if (!TweenClipGUI.IsVisibleField(module, settings, field, showFrom))
                    continue;

                SerializedProperty child = module.FindPropertyRelative(field.Name);
                row.y += row.height + spacing;
                row.height = EditorGUI.GetPropertyHeight(child, true);

                GUIContent childLabel = new(field.Label, child.tooltip);
                if (TweenClipGUI.TryApplyFieldRange(module, settings, field, child, out float minimum, out float maximum))
                {
                    if (child.propertyType == SerializedPropertyType.Integer)
                    {
                        child.intValue = EditorGUI.IntSlider(row, childLabel, child.intValue,
                            Mathf.CeilToInt(minimum), Mathf.FloorToInt(maximum));
                    }
                    else
                    {
                        child.floatValue = EditorGUI.Slider(row, childLabel, child.floatValue, minimum, maximum);
                    }
                }
                else
                {
                    EditorGUI.PropertyField(row, child, childLabel, true);
                }
            }
        }

        static float GetValuesHeight(SerializedProperty settings, SerializedProperty module,
            TweenClipGUI.ModuleLayout layout, float spacing)
        {
            float height = 0f;
            bool showFrom = TweenClipGUI.ShouldShowFrom(module, layout.FromValues);

            if (TweenClipGUI.HasVisibleFields(module, settings, layout.FromValues, showFrom))
            {
                height += GetHeaderHeight(spacing);
                height += GetFieldsHeight(module, settings, layout.FromValues, showFrom, spacing);

                if (TweenClipGUI.ShowsHoldBeforeStart(module, layout))
                    height += EditorGUIUtility.singleLineHeight + spacing;
            }

            if (TweenClipGUI.HasVisibleFields(module, settings, layout.ToValues, true))
            {
                height += GetHeaderHeight(spacing);
                height += GetFieldsHeight(module, settings, layout.ToValues, true, spacing);
            }

            height += GetFieldsHeight(module, settings, layout.OtherValues, true, spacing);
            return height;
        }

        static float GetFieldsHeight(SerializedProperty module, SerializedProperty settings,
            List<TweenClipGUI.FieldLayout> fields, bool showFrom, float spacing)
        {
            float height = 0f;
            for (int i = 0; i < fields.Count; i++)
            {
                TweenClipGUI.FieldLayout field = fields[i];
                if (!TweenClipGUI.IsVisibleField(module, settings, field, showFrom))
                    continue;

                height += EditorGUI.GetPropertyHeight(module.FindPropertyRelative(field.Name), true) + spacing;
            }

            return height;
        }

        static void DrawHeader(ref Rect row, string label, float spacing)
        {
            float line = EditorGUIUtility.singleLineHeight;
            NextRow(ref row, line, spacing + line * 0.5f);
            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
        }

        static float GetHeaderHeight(float spacing) => EditorGUIUtility.singleLineHeight * 1.5f + spacing;

        static SerializedProperty FindSiblingTarget(SerializedProperty settings)
        {
            const string suffix = ".settings";
            if (!settings.propertyPath.EndsWith(suffix, StringComparison.Ordinal))
                return null;

            string animationPath = settings.propertyPath[..^suffix.Length];
            return settings.serializedObject.FindProperty($"{animationPath}.target");
        }

        static void NextRow(ref Rect row, float height, float spacing)
        {
            row.y += row.height + spacing;
            row.height = height;
        }

        static void DrawPropertyRow(ref Rect row, SerializedProperty property, float spacing)
        {
            row.y += row.height + spacing;
            row.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(row, property, true);
        }

        static void HandleContextMenu(Rect header, SerializedProperty property)
        {
            Event current = Event.current;
            if (current.type != EventType.ContextClick || !header.Contains(current.mousePosition))
                return;

            SerializedObject owner = property.serializedObject;
            string propertyPath = property.propertyPath;
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                owner.UpdateIfRequiredOrScript();
                TweenClipGUI.CopySettings(owner.FindProperty(propertyPath));
            });

            if (TweenClipGUI.CanPasteSettings)
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    owner.UpdateIfRequiredOrScript();
                    Undo.RecordObject(owner.targetObject, "Paste Tween Settings");
                    TweenClipGUI.PasteSettings(owner.FindProperty(propertyPath));
                    owner.ApplyModifiedProperties();
                    EditorUtility.SetDirty(owner.targetObject);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.ShowAsContext();
            current.Use();
        }
    }

    [CustomPropertyDrawer(typeof(TweenClipSettings<>), true)]
    public sealed class TweenClipSettingsGenericDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line;

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = line;

            height += GetHeaderHeight(spacing);
            height += GetPropertyHeight(property.FindPropertyRelative("from"), spacing);
            height += GetPropertyHeight(property.FindPropertyRelative("to"), spacing);

            height += GetHeaderHeight(spacing);
            SerializedProperty ease = property.FindPropertyRelative("ease");
            height += GetPropertyHeight(ease, spacing);
            if (ease != null && ease.intValue == (int)Ease.Custom)
                height += GetPropertyHeight(property.FindPropertyRelative("customEase"), spacing);

            height += GetPropertyHeight(property.FindPropertyRelative("duration"), spacing);
            height += GetPropertyHeight(property.FindPropertyRelative("delay"), spacing);
            SerializedProperty loop = property.FindPropertyRelative("loop");
            height += GetPropertyHeight(loop, spacing);
            if (loop != null && (LoopMode)loop.enumValueIndex != LoopMode.None)
                height += GetPropertyHeight(property.FindPropertyRelative("iterations"), spacing);
            height += GetPropertyHeight(property.FindPropertyRelative("timeMode"), spacing);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                DrawPropertyRow(ref row, property.FindPropertyRelative("from"), spacing);
                DrawPropertyRow(ref row, property.FindPropertyRelative("to"), spacing);

                DrawHeader(ref row, "Clip", spacing);
                SerializedProperty ease = property.FindPropertyRelative("ease");
                DrawPropertyRow(ref row, ease, spacing);
                if (ease != null && ease.intValue == (int)Ease.Custom)
                    DrawPropertyRow(ref row, property.FindPropertyRelative("customEase"), spacing);

                DrawPropertyRow(ref row, property.FindPropertyRelative("duration"), spacing);
                DrawPropertyRow(ref row, property.FindPropertyRelative("delay"), spacing);
                SerializedProperty loop = property.FindPropertyRelative("loop");
                DrawPropertyRow(ref row, loop, spacing);
                if (loop != null && (LoopMode)loop.enumValueIndex != LoopMode.None)
                {
                    EditorGUI.indentLevel++;
                    DrawPropertyRow(ref row, property.FindPropertyRelative("iterations"), spacing);
                    EditorGUI.indentLevel--;
                }
                DrawPropertyRow(ref row, property.FindPropertyRelative("timeMode"), spacing);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        static void DrawHeader(ref Rect row, string label, float spacing)
        {
            float line = EditorGUIUtility.singleLineHeight;
            NextRow(ref row, line, spacing + line * 0.5f);
            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
        }

        static float GetHeaderHeight(float spacing) => EditorGUIUtility.singleLineHeight * 1.5f + spacing;

        static float GetPropertyHeight(SerializedProperty property, float spacing)
        {
            return property != null ? EditorGUI.GetPropertyHeight(property, true) + spacing : 0f;
        }

        static void NextRow(ref Rect row, float height, float spacing)
        {
            row.y += row.height + spacing;
            row.height = height;
        }

        static void DrawPropertyRow(ref Rect row, SerializedProperty property, float spacing)
        {
            if (property == null)
                return;

            row.y += row.height + spacing;
            row.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(row, property, true);
        }
    }
}