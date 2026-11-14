using UnityEditor;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

using Gear.Common;
namespace Gear.EditorTools.ImageExtensions
{
    [CustomPropertyDrawer(typeof(RectangleImg))]
    public class RectanglePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            {
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;

                Rect modeRect = new Rect(position.x, position.y, position.width, lineHeight);
                Rect labelRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
                Rect radiusVectorRect = new Rect(position.x,
                    position.y + (lineHeight + spacing) * 2f,
                    position.width, lineHeight);

                SerializedProperty radiusMode = property.FindPropertyRelative("radiusMode");
                SerializedProperty uniformCornerRadius = property.FindPropertyRelative("uniformCornerRadius");
                SerializedProperty cornerRadius = property.FindPropertyRelative("cornerRadius");

                EditorGUI.PropertyField(modeRect, radiusMode, new GUIContent("圆角单位"));
                EditorUtility.CornerRadiusModeGUI(labelRect, ref uniformCornerRadius, new[] { "自由", "统一" });

                bool percentage = radiusMode != null
                    && radiusMode.enumValueIndex == (int)CornerRadiusMode.Percentage;

                float floatVal = cornerRadius.vector4Value.x;
                Vector4 vectorValue = cornerRadius.vector4Value;
                float[] zw = new[] { vectorValue.w, vectorValue.z };
                float[] xy = new[] { vectorValue.x, vectorValue.y };

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.showMixedValue = cornerRadius.hasMultipleDifferentValues;
                    if (uniformCornerRadius.boolValue)
                    {
                        floatVal = EditorGUI.FloatField(radiusVectorRect,
                            percentage ? "均匀百分比" : "均匀半径", floatVal);
                    }
                    else
                    {
                        EditorGUI.MultiFloatField(radiusVectorRect, new[] {
                            new GUIContent("W"), new GUIContent("Z")}, zw);
                        radiusVectorRect.y += lineHeight + spacing;
                        EditorGUI.MultiFloatField(radiusVectorRect, new[] {
                            new GUIContent("X "), new GUIContent("Y")}, xy);

                    }
                    EditorGUI.showMixedValue = false;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    cornerRadius.vector4Value = uniformCornerRadius.boolValue
                        ? new Vector4(floatVal, floatVal, floatVal, floatVal)
                        : new Vector4(xy[0], xy[1], zw[1], zw[0]);
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            if (property.FindPropertyRelative("uniformCornerRadius").boolValue)
            {
                return lineHeight * 3f + spacing * 2f;
            }
            return lineHeight * 4f + spacing * 3f;
        }
    }
}