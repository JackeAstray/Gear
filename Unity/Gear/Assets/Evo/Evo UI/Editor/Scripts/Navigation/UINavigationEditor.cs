using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [CustomEditor(typeof(UINavigation)), NoAutoStaticsCleanup]
    public class UINavigationEditor : SelectableEditor
    {
        // Target
        UINavigation navTarget;

        // Properties
        SerializedProperty navigation;

        // Constants
        const float navArrowThickness = 2.5f;
        const float navArrowHeadSize = 1.2f;
        public const string showNavigationKey = "SelectableEditor.ShowNavigation";

        // Helpers
        public static List<SelectableEditor> uinEditors = new();
        public static bool showNavigation = false;

        protected override void OnEnable()
        {
            navTarget = (UINavigation)target;
            navigation = serializedObject.FindProperty("m_Navigation");
            PrepareForVisualize(this);
        }

        protected override void OnDisable()
        {
            RemoveFromVisualize(this);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EvoEditorGUI.BeginCenteredInspector();
            EvoEditorGUI.BeginVerticalBackground();
            EvoEditorGUI.DrawProperty(navigation, "Navigation", null, false, false);
            DrawVisualizeButton();
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.EndCenteredInspector();

            serializedObject.ApplyModifiedProperties();
        }

        public static void DrawVisualizeButton()
        {
            Rect toggleRect = EditorGUILayout.GetControlRect();
            toggleRect.xMin += EditorGUIUtility.labelWidth + 4;
            EditorGUI.BeginChangeCheck();
            showNavigation = GUI.Toggle(toggleRect, showNavigation,
                new GUIContent("Visualize", "Show navigation flows between selectable UI elements."), EditorStyles.miniButton);
          
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(showNavigationKey, showNavigation);
                SceneView.RepaintAll();
            }
        }

        public static void PrepareForVisualize(SelectableEditor target)
        {
            uinEditors.Add(target);
            RegisterStaticOnSceneGUI();
            showNavigation = EditorPrefs.GetBool(showNavigationKey);
        }

        public static void RemoveFromVisualize(SelectableEditor target)
        {
            uinEditors.Remove(target);
            RegisterStaticOnSceneGUI();
        }

        static void RegisterStaticOnSceneGUI()
        {
            SceneView.duringSceneGui -= StaticOnSceneGUI;
            if (uinEditors.Count > 0)
                SceneView.duringSceneGui += StaticOnSceneGUI;
        }

        static void StaticOnSceneGUI(SceneView view)
        {
            if (!showNavigation)
                return;

            Selectable[] selectables = Selectable.allSelectablesArray;

            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable s = selectables[i];
                if (UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(s.gameObject, Camera.current))
                    DrawNavigationForSelectable(s);
            }
        }

        static void DrawNavigationForSelectable(Selectable sel)
        {
            if (sel == null)
                return;

            Transform transform = sel.transform;
            bool active = Selection.transforms.Any(e => e == transform);

            Handles.color = new Color(1.0f, 0.6f, 0.2f, active ? 1.0f : 0.4f);
            DrawNavigationArrow(-Vector2.right, sel, sel.FindSelectableOnLeft());
            DrawNavigationArrow(Vector2.up, sel, sel.FindSelectableOnUp());

            Handles.color = new Color(1.0f, 0.9f, 0.1f, active ? 1.0f : 0.4f);
            DrawNavigationArrow(Vector2.right, sel, sel.FindSelectableOnRight());
            DrawNavigationArrow(-Vector2.up, sel, sel.FindSelectableOnDown());
        }

        static void DrawNavigationArrow(Vector2 direction, Selectable fromObj, Selectable toObj)
        {
            if (fromObj == null || toObj == null)
                return;

            Transform fromTransform = fromObj.transform;
            Transform toTransform = toObj.transform;

            Vector2 sideDir = new(direction.y, -direction.x);
            Vector3 fromPoint = fromTransform.TransformPoint(GetPointOnRectEdge(fromTransform as RectTransform, direction));
            Vector3 toPoint = toTransform.TransformPoint(GetPointOnRectEdge(toTransform as RectTransform, -direction));
            float fromSize = HandleUtility.GetHandleSize(fromPoint) * 0.05f;
            float toSize = HandleUtility.GetHandleSize(toPoint) * 0.05f;
            fromPoint += fromTransform.TransformDirection(sideDir) * fromSize;
            toPoint += toTransform.TransformDirection(sideDir) * toSize;
            float length = Vector3.Distance(fromPoint, toPoint);
            Vector3 fromTangent = fromTransform.rotation * direction * length * 0.3f;
            Vector3 toTangent = toTransform.rotation * -direction * length * 0.3f;

            Handles.DrawBezier(fromPoint, toPoint, fromPoint + fromTangent, toPoint + toTangent, Handles.color, null, navArrowThickness);
            Handles.DrawAAPolyLine(navArrowThickness, toPoint, toPoint + toTransform.rotation * (-direction - sideDir) * toSize * navArrowHeadSize);
            Handles.DrawAAPolyLine(navArrowThickness, toPoint, toPoint + toTransform.rotation * (-direction + sideDir) * toSize * navArrowHeadSize);
        }

        static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        {
            if (rect == null)
                return Vector3.zero;

            if (dir != Vector2.zero)
                dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));

            dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
            return dir;
        }
    }
}