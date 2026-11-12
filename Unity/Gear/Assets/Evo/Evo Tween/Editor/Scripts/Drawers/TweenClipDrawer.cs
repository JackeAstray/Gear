using System;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    [CustomPropertyDrawer(typeof(TweenClip))]
    public sealed class TweenClipDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            if (!property.isExpanded)
                return line;

            float height = line;
            height += PropertyHeight(property.FindPropertyRelative("target")) + spacing;
            height += PropertyHeight(property.FindPropertyRelative("flow")) + spacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("settings"), true) + spacing;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new(position.x, position.y, position.width, line);
            GUIContent displayLabel = label.text.StartsWith("Element ", StringComparison.Ordinal)
                ? new GUIContent(TweenClipGUI.GetDefaultInspectorTitle(property), label.tooltip)
                : label;
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, displayLabel, true);
            HandleContextMenu(row, property);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                DrawTargetRow(ref row, property, spacing);
                DrawPropertyRow(ref row, property.FindPropertyRelative("flow"), spacing);

                row.y += row.height + spacing;
                SerializedProperty settings = property.FindPropertyRelative("settings");
                row.height = EditorGUI.GetPropertyHeight(settings, true);
                EditorGUI.PropertyField(row, settings, true);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        static void DrawTargetRow(ref Rect row, SerializedProperty clip, float spacing)
        {
            SerializedProperty target = clip.FindPropertyRelative("target");
            row.y += row.height + spacing;
            row.height = EditorGUI.GetPropertyHeight(target, true);
            TweenClipGUI.DrawTargetField(row, target, TweenClipGUI.GetEffectiveTargetType(clip));
        }

        static void DrawPropertyRow(ref Rect row, SerializedProperty property, float spacing)
        {
            row.y += row.height + spacing;
            row.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(row, property, true);
        }

        static float PropertyHeight(SerializedProperty property)
        {
            return property != null ? EditorGUI.GetPropertyHeight(property, true) : 0f;
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
                TweenClipGUI.CopyClip(owner.FindProperty(propertyPath));
            });

            if (TweenClipGUI.CanPasteClip)
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    owner.UpdateIfRequiredOrScript();
                    Undo.RecordObject(owner.targetObject, "Paste Tween Clip");
                    TweenClipGUI.PasteClip(owner.FindProperty(propertyPath));
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
}