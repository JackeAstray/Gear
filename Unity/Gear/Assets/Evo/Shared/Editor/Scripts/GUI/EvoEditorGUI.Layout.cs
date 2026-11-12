using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public static partial class EvoEditorGUI
    {
        // Cache keys only; EditorPrefs remains the source of truth for every inspector.
        static readonly Dictionary<(System.Type, string), string> foldoutKeys = new();

        // One animation per keyed foldout, released together with its target object
        static readonly ConditionalWeakTable<Object, Dictionary<string, ExpandAnimation>> foldoutAnimations = new();

        // Background foldouts awaiting EndFoldout, with the animation wrapping their content (null if none) and whether
        // they began on a Layout event
        static readonly List<(ExpandAnimation animation, bool layout)> openFoldouts = new();

        /// <summary>
        /// Sets a shared foldout state, for example before selecting a related component.
        /// </summary>
        public static void SetFoldoutState(Object target, string key, bool expanded)
        {
            EditorPrefs.SetBool(GetFoldoutKey(target, key), expanded);
        }

        static string GetFoldoutKey(Object target, string key)
        {
            var type = target.GetType();
            var cacheKey = (type, key);
            if (!foldoutKeys.TryGetValue(cacheKey, out string preferenceKey))
            {
                preferenceKey = $"EvoEditorGUI.Foldout.{type.Assembly.GetName().Name}.{type.FullName}.{key}";
                foldoutKeys.Add(cacheKey, preferenceKey);
            }
            return preferenceKey;
        }

        public static bool BeginFoldout(Object target, string key, string title, bool defaultExpanded = false,
            FoldoutOptions options = default, bool compact = false)
        {
            bool expanded = EditorPrefs.GetBool(GetFoldoutKey(target, key), defaultExpanded);
            bool previousExpanded = expanded;
            bool previousChanged = GUI.changed;

            // Only background foldouts are closed by EndFoldout, which ends their animated content
            ExpandAnimation animation = options.Background ? GetFoldoutAnimation(target, key, expanded) : null;
            bool visible = BeginFoldout(ref expanded, title, options, compact, animation);

            // Expanding UI is not a data edit; preserve changes made by earlier controls
            GUI.changed = previousChanged;

            if (expanded != previousExpanded)
                SetFoldoutState(target, key, expanded);

            return visible;
        }

        /// <summary>
        /// Begins a compact foldout whose header also switches <paramref name="toggle"/>: the On/Off end of the header
        /// toggles it, the rest expands and collapses. Switching it on expands the foldout, and the content is drawn
        /// disabled while it's off. The expanded state is shared like keyed foldouts. Always call EndToggleFoldout.
        /// </summary>
        /// <returns>True while the content is visible, which includes the closing animation.</returns>
        public static bool BeginToggleFoldout(SerializedProperty toggle, string title = null, bool defaultExpanded = false,
            FoldoutOptions options = default)
        {
            Object target = toggle.serializedObject.targetObject;
            string key = toggle.propertyPath;
            bool expanded = EditorPrefs.GetBool(GetFoldoutKey(target, key), defaultExpanded);
            bool previousExpanded = expanded;
            ExpandAnimation animation = GetFoldoutAnimation(target, key, expanded);

            BeginColumn(LayoutOptions.WithBackground(options.Background).WithInvertedColor(options.InvertColor));
            PushOpenFoldout(animation);

            Rect headerRect = GetFoldoutHeaderRect(true);
            GUIContent content = GetContent(string.IsNullOrEmpty(title) ? toggle.displayName : title, toggle.tooltip);

            // The On/Off end keeps one width for both states, so the two click areas don't shift when it's switched
            GUIStyle stateStyle = ToggleStateStyle;
            float stateWidth = Mathf.Max(stateStyle.CalcSize(GetContent(ToggleOnLabel)).x, stateStyle.CalcSize(GetContent(ToggleOffLabel)).x);
            float splitX = GetToggleBoxRect(headerRect).x - Metrics.ToggleContentSpacing - stateWidth - Metrics.PropertyLabelPadding;
            Rect foldRect = new(headerRect.x, headerRect.y, Mathf.Max(0f, splitX - headerRect.x), headerRect.height);
            Rect switchRect = new(foldRect.xMax, headerRect.y, headerRect.xMax - foldRect.xMax, headerRect.height);

            // Expanding UI is not a data edit; preserve changes made by earlier controls
            bool previousChanged = GUI.changed;
            DrawFoldoutHeader(foldRect, ref expanded, content.text, options, true);
            GUI.changed = previousChanged;

            using (var propertyScope = new EditorGUI.PropertyScope(headerRect, content, toggle))
            {
                EditorGUI.BeginChangeCheck();
                bool value = ToggleButton(switchRect, toggle.boolValue, propertyScope.content.tooltip, Styles.FoldoutBackgroundCompact);
                DrawToggleState(headerRect, value, ToggleOnLabel, ToggleOffLabel, switchRect.x);

                if (EditorGUI.EndChangeCheck())
                {
                    toggle.boolValue = value;
                    expanded |= value;
                }
            }

            if (expanded != previousExpanded)
                SetFoldoutState(target, key, expanded);

            bool visible = BeginFoldoutContent(expanded, expanded != previousExpanded, animation);
            EditorGUI.BeginDisabledGroup(!toggle.hasMultipleDifferentValues && !toggle.boolValue);
            return visible;
        }

        /// <summary>
        /// Ends a foldout begun with BeginToggleFoldout, whether or not its content was drawn.
        /// </summary>
        public static void EndToggleFoldout(bool addSpace = true)
        {
            EditorGUI.EndDisabledGroup();
            EndFoldout(addSpace);
        }

        static ExpandAnimation GetFoldoutAnimation(Object target, string key, bool expanded)
        {
            var animations = foldoutAnimations.GetOrCreateValue(target);
            if (!animations.TryGetValue(key, out ExpandAnimation animation))
                animations.Add(key, animation = new ExpandAnimation(expanded, Metrics.FoldoutAnimationDuration));

            return animation;
        }

        public static bool BeginFoldout(Object target, string title, bool defaultExpanded = false,
            FoldoutOptions options = default, bool compact = false)
        {
            return BeginFoldout(target, title, title, defaultExpanded, options, compact);
        }

        public static bool BeginFoldout(ref bool foldout, string title, FoldoutOptions options = default, bool compact = false)
        {
            return BeginFoldout(ref foldout, title, options, compact, null);
        }

        /// <returns>True while the content is visible, which includes the closing animation.</returns>
        static bool BeginFoldout(ref bool foldout, string title, FoldoutOptions options, bool compact, ExpandAnimation animation)
        {
            if (options.Background)
            {
                BeginColumn(LayoutOptions.WithInvertedColor(options.InvertColor));
                PushOpenFoldout(animation);
            }

            bool expanded = foldout;
            DrawFoldoutHeader(ref foldout, title, options, compact);
            return BeginFoldoutContent(foldout, foldout != expanded, animation);
        }

        /// <summary>
        /// Draws a foldout header and toggles <paramref name="foldout"/> when it's clicked.
        /// </summary>
        static Rect DrawFoldoutHeader(ref bool foldout, string title, FoldoutOptions options, bool compact,
            Vector2 previewSize = default, System.Action<Rect> drawPreview = null)
        {
            Rect backgroundRect = GetFoldoutHeaderRect(compact);
            DrawFoldoutHeader(backgroundRect, ref foldout, title, options, compact, previewSize, drawPreview);
            return backgroundRect;
        }

        static Rect GetFoldoutHeaderRect(bool compact)
        {
            Styles.Ensure();

            // Let the header fit its parent instead of imposing the title's preferred width
            return compact
                ? GUILayoutUtility.GetRect(0f, Metrics.FoldoutHeightCompact, Styles.FoldoutBackgroundCompact, GUILayout.ExpandWidth(true))
                : GUILayoutUtility.GetRect(0f, Metrics.FoldoutHeight, Styles.FoldoutBackground, GUILayout.ExpandWidth(true));
        }

        static void DrawFoldoutHeader(Rect backgroundRect, ref bool foldout, string title, FoldoutOptions options, bool compact,
            Vector2 previewSize = default, System.Action<Rect> drawPreview = null)
        {
            PrepareInteractionRepaint();
            Styles.Ensure();

            GUIContent titleContent = GetContent(title);
            GUIStyle backgroundStyle = compact ? Styles.FoldoutBackgroundCompact : Styles.FoldoutBackground;
            float iconSize = compact ? Metrics.FoldoutIconSizeCompact : Metrics.FoldoutIconSize;
            float arrowSize = compact ? Metrics.FoldoutArrowSizeCompact : Metrics.FoldoutArrowSize;
            float arrowLeft = compact ? Metrics.FoldoutPaddingLeftCompact : Metrics.FoldoutPaddingLeft;
            float iconLeft = compact ? Metrics.FoldoutIconLeftCompact : Metrics.FoldoutIconLeft;
            float textLeft = compact ? Metrics.FoldoutTextLeftCompact : Metrics.FoldoutTextLeft;
            float textLeftWithIcon = compact ? Metrics.FoldoutTextLeftWithIconCompact : Metrics.FoldoutTextLeftWithIcon;

            foldout = LeftClickToggle(backgroundRect, foldout, GUIContent.none, backgroundStyle);

            float contentY = backgroundRect.y + backgroundStyle.padding.top;
            float contentHeight = Mathf.Max(0f, backgroundRect.height - backgroundStyle.padding.vertical);
            GUIStyle textStyle = Styles.GetFoldoutLabel(options.TextColor, options.BoldText, compact);
            float contentLeft = backgroundRect.x + textLeft;
            float previewSpacing = compact ? 5f : 6f;

            if (options.Icon != null)
            {
                Rect iconRect = new(backgroundRect.x + iconLeft, contentY + (contentHeight - iconSize) * 0.5f, iconSize, iconSize);
                Color cachedColor = GUI.color;
                GUI.color = textStyle.normal.textColor;
                GUI.DrawTexture(iconRect, options.Icon, ScaleMode.ScaleToFit);
                GUI.color = cachedColor;
                contentLeft = backgroundRect.x + textLeftWithIcon;
            }

            if (drawPreview != null && previewSize.x > 0f && previewSize.y > 0f)
            {
                float previewLeft = options.Icon != null ? contentLeft : backgroundRect.x + iconLeft + 1;
                Rect previewRect = new(previewLeft, contentY + (contentHeight - previewSize.y) * 0.5f, previewSize.x, previewSize.y);
                drawPreview(previewRect);
                contentLeft = previewRect.xMax + previewSpacing;
            }

            // Use the full header height so the font's descenders are not clipped by background padding.
            float textWidth = Mathf.Max(0f, backgroundRect.xMax - backgroundStyle.padding.right - contentLeft);
            Rect textRect = new(contentLeft, backgroundRect.y, textWidth, backgroundRect.height);
            GUI.Label(textRect, titleContent, textStyle);

            Rect foldoutRect = new(backgroundRect.xMin + arrowLeft, contentY + (contentHeight - arrowSize) * 0.5f, arrowSize, arrowSize);
            if (Event.current.type == EventType.Repaint)
                EditorStyles.foldout.Draw(foldoutRect, GUIContent.none, false, false, foldout, false);
        }

        /// <summary>
        /// Begins the content under a foldout header and returns whether to draw it. An animation eases it open and closed.
        /// </summary>
        static bool BeginFoldoutContent(bool expanded, bool clicked, ExpandAnimation animation)
        {
            bool visible = animation != null ? animation.BeginContent(expanded, clicked) : expanded;

            // A tiny space when it's open
            if (visible)
                GUILayout.Space(1);

            return visible;
        }

        /// <summary>
        /// Ends the foldout. No need to call unless BeginFoldout is drawn with a background.
        /// </summary>
        public static void EndFoldout(bool addSpace = true)
        {
            int last = openFoldouts.Count - 1;
            if (last >= 0)
            {
                openFoldouts[last].animation?.EndContent();
                openFoldouts.RemoveAt(last);
            }

            EndColumn(addSpace ? LayoutOptions.WithSpace(Metrics.FoldoutSpacing) : default);
        }

        static void PushOpenFoldout(ExpandAnimation animation)
        {
            // An exception (such as ExitGUI) that skips EndFoldout leaves its pass's entries below this one's. A pass handles
            // one event and Unity sends Layout before every other event, so they show up as entries begun on the other kind of
            // event, or as this foldout itself (it can't contain itself). They go along with everything below them
            bool layout = Event.current.type == EventType.Layout;
            int stale = -1;
            for (int i = 0; i < openFoldouts.Count; i++)
            {
                (ExpandAnimation openAnimation, bool openOnLayout) = openFoldouts[i];
                if (openOnLayout != layout || (animation != null && openAnimation == animation))
                    stale = i;
            }

            if (stale >= 0)
                openFoldouts.RemoveRange(0, stale + 1);

            openFoldouts.Add((animation, layout));
        }

        public static void BeginRow(LayoutOptions rowOptions, params GUILayoutOption[] options)
        {
            if (!rowOptions.Background)
            {
                GUILayout.BeginHorizontal(options);
                return;
            }

            Styles.Ensure();
            Color cachedColor = GUI.color;
            GUI.color = Palette.GetBackgroundColor(rowOptions.InvertColor);
            GUILayout.BeginHorizontal(Styles.Background, options);
            GUI.color = cachedColor;
        }

        public static void BeginRow(params GUILayoutOption[] options) => BeginRow(default, options);

        public static void EndRow(LayoutOptions options = default)
        {
            GUILayout.EndHorizontal();

            if (options.HasSpace)
                GUILayout.Space(options.Space);
        }

        public static void BeginColumn(LayoutOptions columnOptions, params GUILayoutOption[] options)
        {
            if (!columnOptions.Background)
            {
                GUILayout.BeginVertical(options);
                return;
            }

            Styles.Ensure();
            Color cachedColor = GUI.color;
            GUI.color = Palette.GetBackgroundColor(columnOptions.InvertColor);
            GUILayout.BeginVertical(Styles.Background, options);
            GUI.color = cachedColor;
        }

        public static void BeginColumn(params GUILayoutOption[] options) => BeginColumn(default, options);

        public static void EndColumn(LayoutOptions options = default)
        {
            GUILayout.EndVertical();

            if (options.HasSpace)
                GUILayout.Space(options.Space);
        }

        public static void BeginCenteredInspector(bool addMoreSpace = false)
        {
            Styles.Ensure();

            GUILayout.BeginHorizontal(addMoreSpace ? Styles.CenteredInspectorSpacious : Styles.CenteredInspector);
            GUILayout.Space(Metrics.CenteredInspectorOffset);
            GUILayout.BeginVertical();
        }

        public static void EndCenteredInspector()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        public static void BeginContainer(string label, int padding = Metrics.ContainerPadding, bool compactHeader = true, int nestDepth = 0)
        {
            Styles.Ensure();
            GUILayout.BeginVertical(Styles.GetContainer(padding - nestDepth, compactHeader));

            if (!string.IsNullOrEmpty(label))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Metrics.ContainerLabelOffset);
                Label(label);
                GUILayout.EndHorizontal();
                GUILayout.Space(Metrics.ContainerLabelSpacing);
            }
        }

        public static void BeginContainer(int padding = Metrics.ContainerPadding, bool compactHeader = true, int nestDepth = 0)
        {
            BeginContainer("", padding, compactHeader, nestDepth);
        }

        public static void EndContainer() => GUILayout.EndVertical();

        public static void Separator(float height = Metrics.SeparatorHeight)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(rect, Palette.Separator);
        }

        public static void AddFoldoutSpace() => GUILayout.Space(Metrics.FoldoutSpacing);

        public static void AddLayoutSpace() => GUILayout.Space(Metrics.LayoutSpacing);

        public static void AddPropertySpace() => GUILayout.Space(Metrics.PropertySpacing);
    }
}