using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public static partial class EvoEditorGUI
    {
        #region Property
        public static void Property(SerializedProperty property, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                EditorGUILayout.PropertyField(property, GetContent(finalLabel, options.Tooltip));
            }

            EndRow(layoutOptions);
        }

        public static void Property(SerializedProperty property, FieldOptions options = default)
        {
            Property(property, "", options: options);
        }

        public static void ArrayProperty(SerializedProperty property, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            using (new LabelWidthScope(options.LabelWidth))
            using (new WideModeScope(true))
            {
                GUILayout.Space(Metrics.PropertyFoldoutPadding);
                EditorGUILayout.PropertyField(property, GetContent(finalLabel, options.Tooltip), includeChildren: true);
            }

            EndRow(layoutOptions);
        }

        public static void ArrayProperty(SerializedProperty property, FieldOptions options = default)
        {
            ArrayProperty(property, "", options: options);
        }

        public static T ObjectField<T>(T obj, string label, bool allowSceneObj = true, FieldOptions options = default) where T : Object
        {
            return (T)ObjectField(obj, typeof(T), label, allowSceneObj, options);
        }

        public static Object ObjectField(Object obj, System.Type objType, string label, bool allowSceneObj = true, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);
            Object result;

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                result = EditorGUILayout.ObjectField(GetContent(label, options.Tooltip), obj, objType, allowSceneObj);
            }

            EndRow(layoutOptions);
            return result;
        }

        public static float FloatField(float value, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                value = EditorGUILayout.FloatField(GetContent(label, options.Tooltip), value);
            }

            EndRow(layoutOptions);
            return value;
        }

        public static int IntField(int value, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                value = EditorGUILayout.IntField(GetContent(label, options.Tooltip), value);
            }

            EndRow(layoutOptions);
            return value;
        }
        #endregion

        #region Dropdown
        public static int Dropdown(int selectedIndex, string[] dropdownOptions, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                Rect popupRect = EditorGUILayout.GetControlRect();
                popupRect = EditorGUI.PrefixLabel(popupRect, GetContent(label, options.Tooltip));
                selectedIndex = EditorGUI.Popup(popupRect, selectedIndex, dropdownOptions, EditorStyles.popup);
            }

            EndRow(layoutOptions);
            return selectedIndex;
        }

        public static void Dropdown(SerializedProperty property, string[] dropdownOptions, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                Rect popupRect = EditorGUILayout.GetControlRect();
                GUIContent content = GetContent(finalLabel, options.Tooltip);

                using var propertyScope = new EditorGUI.PropertyScope(popupRect, content, property);
                popupRect = EditorGUI.PrefixLabel(popupRect, propertyScope.content);
                EditorGUI.BeginChangeCheck();
                int value = EditorGUI.Popup(popupRect, property.intValue, dropdownOptions, EditorStyles.popup);

                if (EditorGUI.EndChangeCheck())
                    property.intValue = value;
            }

            EndRow(layoutOptions);
        }

        public static void Dropdown(SerializedProperty property, string[] dropdownOptions, FieldOptions options = default)
        {
            Dropdown(property, dropdownOptions, "", options: options);
        }
        #endregion

        #region Slider
        public static void Slider(SerializedProperty property, int leftValue, int rightValue, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                EditorGUILayout.IntSlider(property, leftValue, rightValue, GetContent(finalLabel, options.Tooltip));
            }

            EndRow(layoutOptions);
        }

        public static void Slider(SerializedProperty property, int leftValue, int rightValue, FieldOptions options = default)
        {
            Slider(property, leftValue, rightValue, "", options: options);
        }

        public static int Slider(int value, int leftValue, int rightValue, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                value = EditorGUILayout.IntSlider(GetContent(label, options.Tooltip), value, leftValue, rightValue);
            }

            EndRow(layoutOptions);
            return value;
        }

        public static void Slider(SerializedProperty property, float leftValue, float rightValue, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                EditorGUILayout.Slider(property, leftValue, rightValue, GetContent(finalLabel, options.Tooltip));
            }

            EndRow(layoutOptions);
        }

        public static void Slider(SerializedProperty property, float leftValue, float rightValue, FieldOptions options = default)
        {
            Slider(property, leftValue, rightValue, "", options: options);
        }

        public static float Slider(float value, float leftValue, float rightValue, string label, FieldOptions options = default)
        {
            LayoutOptions layoutOptions = options.Layout;
            BeginRow(layoutOptions);

            using (new LabelWidthScope(options.LabelWidth))
            {
                GUILayout.Space(Metrics.PropertyLabelPadding);
                value = EditorGUILayout.Slider(GetContent(label, options.Tooltip), value, leftValue, rightValue);
            }

            EndRow(layoutOptions);
            return value;
        }
        #endregion

        #region Toggle
        const string ToggleOnLabel = "On";
        const string ToggleOffLabel = "Off";

        public static bool Toggle(bool value, string label, FieldOptions options = default,
            string onLabel = ToggleOnLabel, string offLabel = ToggleOffLabel, bool drawIdleBackground = true)
        {
            PrepareInteractionRepaint();
            Styles.Ensure();

            GUIContent content = GetContent(label, options.Tooltip);
            Rect backgroundRect = GUILayoutUtility.GetRect(content, GUIStyle.none, GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight + Metrics.ControlHeightOffset));

            value = DrawToggle(backgroundRect, value, content, options, onLabel, offLabel, drawIdleBackground);

            if (options.AddSpace)
                AddLayoutSpace();

            return value;
        }

        public static void Toggle(SerializedProperty property, string label, FieldOptions options = default,
            string onLabel = ToggleOnLabel, string offLabel = ToggleOffLabel, bool drawIdleBackground = true)
        {
            PrepareInteractionRepaint();
            Styles.Ensure();

            string finalLabel = string.IsNullOrEmpty(label) ? property.displayName : label;
            string tooltip = string.IsNullOrEmpty(options.Tooltip) ? property.tooltip : options.Tooltip;
            GUIContent content = GetContent(finalLabel, tooltip);
            Rect backgroundRect = GUILayoutUtility.GetRect(content, GUIStyle.none, GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight + Metrics.ControlHeightOffset));

            using (var propertyScope = new EditorGUI.PropertyScope(backgroundRect, content, property))
            {
                EditorGUI.BeginChangeCheck();
                bool value = DrawToggle(backgroundRect, property.boolValue, propertyScope.content, options,
                    onLabel, offLabel, drawIdleBackground);

                if (EditorGUI.EndChangeCheck())
                    property.boolValue = value;
            }

            if (options.AddSpace)
                AddLayoutSpace();
        }

        public static void Toggle(SerializedProperty property, FieldOptions options = default,
            string onLabel = ToggleOnLabel, string offLabel = ToggleOffLabel, bool drawIdleBackground = true)
        {
            Toggle(property, "", options, onLabel, offLabel, drawIdleBackground);
        }

        static bool DrawToggle(Rect backgroundRect, bool value, GUIContent content, FieldOptions options,
            string onLabel, string offLabel, bool drawIdleBackground)
        {
            GUIStyle backgroundStyle = options.Background
                ? Styles.GetButton(drawIdleBackground ? Palette.GetBackgroundColor(options.InvertColor) : Color.clear,
                    Palette.GetBackgroundColor(options.InvertColor, hover: true),
                    Palette.GetBackgroundColor(options.InvertColor, pressed: true))
                : GUIStyle.none;

            value = ToggleButton(backgroundRect, value, content.tooltip, backgroundStyle);

            float labelX = backgroundRect.x + Metrics.PropertyLabelPadding;
            float stateX = DrawToggleState(backgroundRect, value, onLabel, offLabel, labelX);
            GUI.Label(new Rect(labelX, backgroundRect.y, Mathf.Max(0f, stateX - labelX - Metrics.ToggleContentSpacing),
                backgroundRect.height), content, EditorStyles.label);

            return value;
        }

        /// <summary>
        /// Switches the value when <paramref name="rect"/> is clicked; drawing the state is up to the caller.
        /// </summary>
        static bool ToggleButton(Rect rect, bool value, string tooltip, GUIStyle style)
        {
            // Mixed values, as when several objects are edited at once, show no state.
            // Like Unity's toggle, a click switches them all on. Until then the passed value is returned as is
            bool shownValue = value && !EditorGUI.showMixedValue;

            Color cachedColor = GUI.color;
            GUI.color = Color.white;
            if (LeftClickToggle(rect, shownValue, GetContent(string.Empty, tooltip), style) != shownValue)
                value = !shownValue;
            GUI.color = cachedColor;

            return value;
        }

        /// <summary>
        /// Draws the state label and box at the right end of a toggle row, with a dash in the box for mixed values.
        /// </summary>
        /// <param name="minX">How far left the state label may grow.</param>
        /// <returns>The left edge of the state label.</returns>
        static float DrawToggleState(Rect rowRect, bool value, string onLabel, string offLabel, float minX)
        {
            bool mixed = EditorGUI.showMixedValue;
            Rect toggleRect = GetToggleBoxRect(rowRect);

            GUIContent stateContent = mixed ? GUIContent.none : GetContent(value ? onLabel : offLabel);
            GUIStyle stateStyle = ToggleStateStyle;

            float stateWidth = Mathf.Min(stateStyle.CalcSize(stateContent).x,
                Mathf.Max(0f, toggleRect.x - Metrics.ToggleContentSpacing - minX));
            Rect stateRect = new(toggleRect.x - Metrics.ToggleContentSpacing - stateWidth, rowRect.y, stateWidth, rowRect.height);
            GUI.Label(stateRect, stateContent, stateStyle);

            if (Event.current.type == EventType.Repaint)
            {
                Styles.ToggleIndicator.Draw(toggleRect, GUIContent.none, false, false, value && !mixed, false);

                float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
                Color cachedColor = GUI.color;

                if (mixed)
                {
                    // Whole-pixel insets on both sides keep the dash centered, like the checkmark
                    float insetX = Mathf.Round((toggleRect.width - Metrics.ToggleDashWidth) * 0.5f * pixelsPerPoint) / pixelsPerPoint;
                    float insetY = Mathf.Round((toggleRect.height - Metrics.ToggleDashHeight) * 0.5f * pixelsPerPoint) / pixelsPerPoint;
                    Rect dashRect = new(toggleRect.x + insetX, toggleRect.y + insetY,
                        toggleRect.width - insetX * 2f, toggleRect.height - insetY * 2f);

                    // DrawTexture skips the disabled fade that styles get, so fade the dash with the box
                    Color dashColor = cachedColor * GUI.contentColor * Palette.Content;
                    if (!GUI.enabled)
                        dashColor.a *= 0.5f;

                    GUI.DrawTexture(dashRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                        dashColor, 0f, Metrics.ToggleDashRadius);
                }
                else if (value && Styles.ToggleCheckmark.image != null)
                {
                    // Equal whole-pixel insets keep the icon centered without a second rounding step
                    float padding = Mathf.Round(Metrics.ToggleCheckmarkPadding * pixelsPerPoint) / pixelsPerPoint;
                    Rect checkmarkRect = new(toggleRect.x + padding, toggleRect.y + padding,
                        toggleRect.width - padding * 2f, toggleRect.height - padding * 2f);

                    GUI.color = cachedColor * GUI.contentColor * (EditorGUIUtility.isProSkin ? Color.black : Color.white);
                    GUI.DrawTexture(checkmarkRect, Styles.ToggleCheckmark.image, ScaleMode.StretchToFill);
                    GUI.color = cachedColor;
                }
            }

            return stateRect.x;
        }

        static GUIStyle ToggleStateStyle => Styles.GetButtonLabel(0, TextAnchor.MiddleRight, Palette.Content);

        static Rect GetToggleBoxRect(Rect rowRect)
        {
            float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            float toggleWidth = Mathf.Round(Metrics.ToggleWidth * pixelsPerPoint) / pixelsPerPoint;
            float toggleHeight = Mathf.Round(Metrics.ToggleHeight * pixelsPerPoint) / pixelsPerPoint;
            Rect toggleRect = new(rowRect.xMax - toggleWidth - Metrics.ToggleRightPadding,
                rowRect.y + (rowRect.height - toggleHeight) * 0.5f, toggleWidth, toggleHeight);

            // Snap the origin in device space, keeping the rounded size stable as the inspector moves
            toggleRect.position = GUIUtility.AlignRectToDevice(toggleRect).position;
            return toggleRect;
        }
        #endregion

        #region Text & Icon
        public static void Label(string text, params GUILayoutOption[] options)
        {
            EditorGUILayout.LabelField(GetContent(text), options);
        }

        public static void Header(string title, Texture2D icon = null, bool addSpaceBefore = false)
        {
            Styles.Ensure();

            if (addSpaceBefore)
                GUILayout.Space(Metrics.HeaderSpaceBefore);

            GUILayout.BeginHorizontal();

            Rect textRect = GUILayoutUtility.GetRect(GetContent(title), Styles.Header);
            if (icon != null)
            {
                Rect iconRect = new(textRect.x, textRect.y + (textRect.height - Metrics.HeaderIconSize) * 0.5f,
                    Metrics.HeaderIconSize, Metrics.HeaderIconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                textRect.x += Metrics.HeaderIconSize + Metrics.HeaderIconSpacing;
            }
            GUI.Label(textRect, GetContent(title), Styles.Header);

            GUILayout.EndHorizontal();
            GUILayout.Space(Metrics.HeaderSpaceAfter);
        }

        public static void Header(string title, string iconName, bool addSpaceBefore = false)
        {
            Header(title, GetIcon(iconName), addSpaceBefore);
        }

        public static void Icon(string iconName, string tooltip = null, float width = Metrics.IconWidth,
            float height = Metrics.IconHeight, bool expandHeight = false)
        {
            Icon(GetIcon(iconName), tooltip, width, height, expandHeight);
        }

        public static void Icon(Texture2D icon, string tooltip = null, float width = Metrics.IconWidth,
            float height = Metrics.IconHeight, bool expandHeight = false)
        {
            EditorGUILayout.LabelField(new GUIContent(icon, tooltip),
                GUILayout.Width(width), GUILayout.Height(height), GUILayout.ExpandHeight(expandHeight));
        }
        #endregion

        #region Rounded Shapes
        /// <summary>
        /// Draws a cached, skin-independent rounded rectangle fill tinted with <paramref name="color"/>.
        /// </summary>
        public static void DrawRoundedBackground(Rect rect, Color color)
        {
            Styles.Ensure();
            DrawRoundedStyle(rect, color, Styles.Background);
        }

        /// <summary>
        /// Draws a cached rounded outline tinted with <paramref name="color"/>.
        /// </summary>
        public static void DrawRoundedBorder(Rect rect, Color color)
        {
            Styles.Ensure();
            DrawRoundedStyle(rect, color, Styles.DragBorder);
        }

        static void DrawRoundedStyle(Rect rect, Color color, GUIStyle style)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color previousColor = GUI.color;
            GUI.color = previousColor * color;
            style.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previousColor;
        }
        #endregion

        #region Callout
        public static void Callout(string message, CalloutType type = CalloutType.Default, bool invertColor = false,
            float spaceBefore = Metrics.PropertySpacing, float spaceAfter = Metrics.PropertySpacing)
        {
            if (spaceBefore > 0)
                GUILayout.Space(spaceBefore);

            Styles.Ensure();
            Texture2D icon = type switch
            {
                CalloutType.Warning => GetIcon("Warning", false),
                CalloutType.Error => GetIcon("Error", false),
                _ => GetIcon("Info", false)
            };

            GUIContent textContent = GetContent(message);
            Color calloutColor = type switch
            {
                CalloutType.Warning => Palette.CalloutWarning,
                CalloutType.Error => Palette.CalloutError,
                _ => Palette.GetBackgroundColor(invertColor)
            };
            GUIStyle boxStyle = Styles.GetCallout(calloutColor);

            Rect boxRect = EditorGUILayout.BeginHorizontal(boxStyle, GUILayout.MinHeight(Metrics.CalloutMinHeight),
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));

            if (icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(Metrics.CalloutIconSize, Metrics.CalloutIconSize,
                    GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                iconRect.y = boxRect.y + (boxRect.height - Metrics.CalloutIconSize) * 0.5f;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUILayout.Space(Metrics.CalloutContentSpacing);
            }

            Rect textRect = GUILayoutUtility.GetRect(textContent, Styles.CalloutText,
                GUILayout.MinWidth(1f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
            textRect.y = boxRect.y + boxStyle.padding.top;
            textRect.height = Mathf.Max(0f, boxRect.height - boxStyle.padding.vertical);
            GUI.Label(textRect, textContent, Styles.CalloutText);

            EditorGUILayout.EndHorizontal();

            if (spaceAfter > 0)
                GUILayout.Space(spaceAfter);
        }
        #endregion

        #region Button
        public static bool Button(string text, ButtonOptions options) => Button(text, iconTexture: null, options);

        public static bool Button(string text, string icon, ButtonOptions options = default) => Button(text, GetIcon(icon), options);

        public static bool Button(string text, IconDefinition icon, ButtonOptions options = default) => Button(text, GetIcon(icon), options);

        public static bool Button(Rect rect, string text, IconDefinition icon, ButtonOptions options = default)
        {
            TextAnchor textAlignment = options.ContentAlignment == ContentAlignment.SpaceBetween &&
                options.IconPosition == IconPosition.BeforeText ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            return DrawButton(rect, text, GetIcon(icon), options, textAlignment);
        }

        public static bool Button(string text, Texture2D iconTexture = null, ButtonOptions options = default)
        {
            TextAnchor textAlignment = options.ContentAlignment == ContentAlignment.SpaceBetween &&
                options.IconPosition == IconPosition.BeforeText ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            return Button(text, iconTexture, options, textAlignment);
        }

        static bool Button(string text, Texture2D iconTexture, ButtonOptions options, TextAnchor textAlignment)
        {
            Styles.Ensure();

            GUIContent textContent = GetContent(text);
            bool hasText = !string.IsNullOrEmpty(text);
            GUIStyle labelStyle = Styles.GetButtonLabel(options.FontSize, textAlignment, Palette.InteractableContent);
            Vector2 textSize = hasText ? labelStyle.CalcSize(textContent) : Vector2.zero;
            bool hasIcon = iconTexture != null;
            float iconSize = hasIcon ? options.IconSize : 0f;
            float contentSpacing = hasText && hasIcon ? options.ContentSpacing : 0f;
            float contentWidth = textSize.x + iconSize + contentSpacing;
            float buttonHeight = options.Height > 0f ? options.Height : EditorGUIUtility.singleLineHeight + Metrics.ControlHeightOffset;
            float minimumWidth = contentWidth + Metrics.ButtonPaddingLeft + Metrics.ButtonPaddingRight;

            Rect buttonRect = options.Width > 0f
                ? GUILayoutUtility.GetRect(options.Width, buttonHeight, GUILayout.Width(options.Width), GUILayout.Height(buttonHeight))
                : GUILayoutUtility.GetRect(minimumWidth, buttonHeight, GUILayout.MinWidth(minimumWidth),
                    GUILayout.ExpandWidth(true), GUILayout.Height(buttonHeight));

            return DrawButton(buttonRect, text, iconTexture, options, textAlignment);
        }

        static bool DrawButton(Rect buttonRect, string text, Texture2D iconTexture, ButtonOptions options, TextAnchor textAlignment)
        {
            PrepareInteractionRepaint();
            Styles.Ensure();

            GUIContent textContent = GetContent(text);
            bool hasText = !string.IsNullOrEmpty(text);
            GUIStyle labelStyle = Styles.GetButtonLabel(options.FontSize, textAlignment, Palette.InteractableContent);
            Vector2 textSize = hasText ? labelStyle.CalcSize(textContent) : Vector2.zero;
            bool hasIcon = iconTexture != null;
            float iconSize = hasIcon ? options.IconSize : 0f;
            float contentSpacing = hasText && hasIcon ? options.ContentSpacing : 0f;
            float contentWidth = textSize.x + iconSize + contentSpacing;

            Color normal = options.NormalColor ?? Palette.GetBackgroundColor(options.InvertBackgroundColor);
            Color hover = options.HoverColor ?? Palette.GetBackgroundColor(options.InvertBackgroundColor, hover: true);
            Color press = options.PressColor ?? Palette.GetBackgroundColor(options.InvertBackgroundColor, pressed: true);

            if (!options.DrawIdleBackground)
                normal = Color.clear;

            Color cachedColor = GUI.color;
            GUI.color = Color.white;
            bool clicked = LeftClickButton(buttonRect, GetContent(string.Empty, options.Tooltip), Styles.GetButton(normal, hover, press));
            GUI.color = cachedColor;
            labelStyle = Styles.GetButtonLabel(options.FontSize, textAlignment, Palette.InteractableContent);

            if (hasText || hasIcon)
            {
                Rect contentRect = new(buttonRect.x + Metrics.ButtonPaddingLeft, buttonRect.y,
                    Mathf.Max(0f, buttonRect.width - Metrics.ButtonPaddingLeft - Metrics.ButtonPaddingRight), buttonRect.height);
                bool iconAfterText = options.IconPosition == IconPosition.AfterText;
                float startX = GetAlignedPositionX(contentRect, contentWidth, options.ContentAlignment);
                float iconX = iconAfterText ? startX + textSize.x + contentSpacing : startX;
                Rect labelRect = new(iconAfterText ? startX : iconX + iconSize + contentSpacing, contentRect.y, textSize.x, contentRect.height);

                if (hasText && hasIcon && options.ContentAlignment == ContentAlignment.SpaceBetween)
                {
                    iconX = iconAfterText ? contentRect.xMax - iconSize : contentRect.x;
                    float labelX = iconAfterText ? contentRect.x : iconX + iconSize + contentSpacing;
                    float labelWidth = iconAfterText ? iconX - contentSpacing - contentRect.x : contentRect.xMax - labelX;
                    labelRect = new Rect(labelX, contentRect.y, Mathf.Max(0f, labelWidth), contentRect.height);
                }

                if (hasIcon)
                {
                    Rect iconRect = new(iconX, contentRect.y + (contentRect.height - iconSize) * 0.5f, iconSize, iconSize);
                    GUI.color = labelStyle.normal.textColor;
                    GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
                    GUI.color = cachedColor;
                }

                if (hasText)
                    GUI.Label(labelRect, textContent, labelStyle);
            }

            GUI.color = cachedColor;
            return clicked;
        }

        static float GetAlignedPositionX(Rect contentRect, float contentWidth, ContentAlignment contentAlignment)
        {
            return contentAlignment switch
            {
                ContentAlignment.Left => contentRect.x,
                ContentAlignment.Right => contentRect.xMax - contentWidth,
                _ => contentRect.x + (contentRect.width - contentWidth) * 0.5f
            };
        }
        #endregion
    }
}