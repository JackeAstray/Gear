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
                float y = position.y;

                SerializedProperty radiusMode = property.FindPropertyRelative("radiusMode");
                SerializedProperty uniformCornerRadius = property.FindPropertyRelative("uniformCornerRadius");
                SerializedProperty cornerRadius = property.FindPropertyRelative("cornerRadius");
                SerializedProperty syncMode = property.FindPropertyRelative("syncMode");
                SerializedProperty syncTarget = property.FindPropertyRelative("syncTarget");
                SerializedProperty syncInset = property.FindPropertyRelative("syncInset");

                EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), radiusMode, new GUIContent("圆角单位"));
                y += lineHeight + spacing;

                bool percentage = radiusMode != null
                    && radiusMode.enumValueIndex == (int)CornerRadiusMode.Percentage;
                bool syncing = syncMode != null
                    && syncMode.enumValueIndex != (int)CornerRadiusSyncMode.None;

                Rect labelRect = new Rect(position.x, y, position.width, lineHeight);
                y += lineHeight + spacing;

                // 同步开启时自身半径不再参与计算，置灰避免误导（字段仍保留以便关闭同步后恢复）
                EditorGUI.BeginDisabledGroup(syncing);
                {
                    EditorUtility.CornerRadiusModeGUI(labelRect, ref uniformCornerRadius, new[] { "自由", "统一" });

                    Rect radiusRect = new Rect(position.x, y, position.width, lineHeight);
                    float floatVal = cornerRadius.vector4Value.x;
                    Vector4 vectorValue = cornerRadius.vector4Value;
                    float[] zw = new[] { vectorValue.w, vectorValue.z };
                    float[] xy = new[] { vectorValue.x, vectorValue.y };

                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUI.showMixedValue = cornerRadius.hasMultipleDifferentValues;
                        if (uniformCornerRadius.boolValue)
                        {
                            floatVal = EditorGUI.FloatField(radiusRect,
                                percentage ? "均匀百分比" : "均匀半径", floatVal);
                            y += lineHeight + spacing;
                        }
                        else
                        {
                            EditorGUI.MultiFloatField(radiusRect, new[] {
                                new GUIContent("W"), new GUIContent("Z")}, zw);
                            radiusRect.y += lineHeight + spacing;
                            EditorGUI.MultiFloatField(radiusRect, new[] {
                                new GUIContent("X "), new GUIContent("Y")}, xy);

                            y += (lineHeight + spacing) * 2f;
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
                EditorGUI.EndDisabledGroup();

                EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight),
                    syncMode, new GUIContent("圆角同步"));
                y += lineHeight + spacing;

                if (syncing)
                {
                    if (syncMode.enumValueIndex == (int)CornerRadiusSyncMode.MatchTarget)
                    {
                        EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight),
                            syncTarget, new GUIContent("同步目标"));
                        y += lineHeight + spacing;
                    }

                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight),
                        syncInset, new GUIContent("同步内缩"));
                }
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // 圆角单位 + 统一开关 + 半径字段（统一时 1 行，自由时 2 行）
            int lines = property.FindPropertyRelative("uniformCornerRadius").boolValue ? 3 : 4;

            // 圆角同步
            lines += 1;

            SerializedProperty syncMode = property.FindPropertyRelative("syncMode");
            if (syncMode != null && syncMode.enumValueIndex != (int)CornerRadiusSyncMode.None)
            {
                lines += 1; // 同步内缩
                if (syncMode.enumValueIndex == (int)CornerRadiusSyncMode.MatchTarget)
                    lines += 1; // 同步目标
            }

            return lineHeight * lines + spacing * (lines - 1);
        }
    }
}
