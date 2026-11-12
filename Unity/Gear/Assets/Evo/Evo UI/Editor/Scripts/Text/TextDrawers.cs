using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Evo.UI.Editor
{
    /// <summary>
    /// Draws text fields with the active provider's text type, so pickers only list usable components.
    /// Values from another provider are still shown until they are converted.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextComponentAttribute))]
    public class TextComponentDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => TextDrawers.ObjectField(position, property, label, TextProvider.Active.TextType, true);
    }

    /// <summary>
    /// Draws font fields with the active provider's font type.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextFontAttribute))]
    public class TextFontDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => TextDrawers.ObjectField(position, property, label, TextProvider.Active.FontType, false);
    }

    /// <summary>
    /// Draws input fields with the active provider's input type.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextInputAttribute))]
    public class TextInputDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => TextDrawers.ObjectField(position, property, label, TextProvider.Active.InputType ?? typeof(MonoBehaviour), true);
    }

    static class TextDrawers
    {
        internal static void ObjectField(Rect position, SerializedProperty property, GUIContent label, Type type, bool allowSceneObjects)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            Object value = EditorGUI.ObjectField(position, label, property.objectReferenceValue, type, allowSceneObjects);
            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = value;

            EditorGUI.EndProperty();
        }
    }
}