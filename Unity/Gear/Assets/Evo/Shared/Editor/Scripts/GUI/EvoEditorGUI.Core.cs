using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    [NoAutoStaticsCleanup]
    public static partial class EvoEditorGUI
    {
        public const string Version = "2.0.0";
        public const string IconResourcesPath = "GUI/";

        #region Content and Get
        static readonly Dictionary<(string text, string tooltip), GUIContent> contentCache = new();
        static readonly Dictionary<(string iconName, bool isCustom), Texture2D> iconCache = new();

        static GUIContent GetContent(string text, string tooltip = null)
        {
            text ??= string.Empty;
            var key = (text, tooltip);

            if (contentCache.TryGetValue(key, out GUIContent content))
                return content;

            if (contentCache.Count >= Metrics.ContentCacheCapacity)
                contentCache.Clear();

            content = new GUIContent(text, tooltip);
            contentCache.Add(key, content);
            return content;
        }

        public static Texture2D GetIcon(IconDefinition icon) => GetIcon(icon.Name, icon.IsCustom);

        public static Texture2D GetIcon(string iconName, bool isCustom = true)
        {
            if (string.IsNullOrEmpty(iconName))
                return null;

            var key = (iconName, isCustom);

            if (iconCache.TryGetValue(key, out Texture2D texture))
                return texture;

            if (iconCache.Count >= Metrics.IconCacheCapacity)
                iconCache.Clear();

            texture = isCustom
                ? Resources.Load<Texture2D>(IconResourcesPath + iconName)
                : EditorGUIUtility.IconContent(iconName).image as Texture2D;

            if (texture == null && isCustom)
                texture = LoadLegacyIcon(iconName);

            iconCache.Add(key, texture);
            return texture;
        }
        #endregion

        #region Repaint
        static double lastInteractionRepaintTime = double.NegativeInfinity;

        static void PrepareInteractionRepaint()
        {
            EditorWindow window = EditorWindow.mouseOverWindow != null
                ? EditorWindow.mouseOverWindow
                : EditorWindow.focusedWindow;

            if (window == null)
                return;

            window.wantsMouseMove = true;
            window.wantsMouseEnterLeaveWindow = true;

            Event currentEvent = Event.current;
            if (currentEvent == null)
                return;

            bool immediate = currentEvent.type == EventType.MouseDown ||
                             currentEvent.type == EventType.MouseUp ||
                             currentEvent.type == EventType.MouseEnterWindow ||
                             currentEvent.type == EventType.MouseLeaveWindow;
            bool mouseMove = currentEvent.type == EventType.MouseMove;

            if (!immediate && !mouseMove)
                return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (!immediate && currentTime - lastInteractionRepaintTime < Metrics.InteractionRepaintInterval)
                return;

            lastInteractionRepaintTime = currentTime;
            window.Repaint();
        }
        #endregion

        #region Common and Shared
        readonly struct LabelWidthScope : IDisposable
        {
            readonly float previousWidth;

            public LabelWidthScope(int labelWidth)
            {
                previousWidth = EditorGUIUtility.labelWidth;

                if (labelWidth > 0)
                    EditorGUIUtility.labelWidth = labelWidth;
            }

            public void Dispose() => EditorGUIUtility.labelWidth = previousWidth;
        }

        readonly struct WideModeScope : IDisposable
        {
            readonly bool previousValue;

            public WideModeScope(bool value)
            {
                previousValue = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = value;
            }

            public void Dispose() => EditorGUIUtility.wideMode = previousValue;
        }

        static bool LeftClickButton(Rect rect, GUIContent content, GUIStyle style)
        {
            Event currentEvent = Event.current;
            EventType eventType = currentEvent.type;

            bool ignoreEvent = currentEvent.isMouse && currentEvent.button != 0 && rect.Contains(currentEvent.mousePosition);
            if (ignoreEvent)
                currentEvent.type = EventType.Ignore;

            bool clicked = GUI.Button(rect, content, style);
            if (ignoreEvent)
                currentEvent.type = eventType;

            return clicked;
        }

        static bool LeftClickToggle(Rect rect, bool value, GUIContent content, GUIStyle style)
        {
            Event currentEvent = Event.current;
            EventType eventType = currentEvent.type;

            bool ignoreEvent = currentEvent.isMouse && currentEvent.button != 0 && rect.Contains(currentEvent.mousePosition);
            if (ignoreEvent)
                currentEvent.type = EventType.Ignore;

            bool toggledValue = GUI.Toggle(rect, value, content, style);
            if (ignoreEvent)
                currentEvent.type = eventType;

            return toggledValue;
        }
        #endregion
    }
}