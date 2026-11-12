using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    /// <summary>
    /// Retained for source compatibility. EvoEditorGUI controls no longer require a repaint handler.
    /// </summary>
    public interface IEvoEditorGUIHandler { }

    public static partial class EvoEditorGUI
    {
        /// <summary>
        /// Legacy button alignment enum.
        /// </summary>
        public enum ButtonAlignment
        {
            Normal = 0,    // Next to text
            Left = 1,      // Left side of button
            Right = 2      // Right side of button
        }

        /// <summary>
        /// Legacy info box enum.
        /// </summary>
        public enum InfoBoxType
        {
            Default = 0,
            Warning = 1,
            Error = 2
        }

        #region Legacy Layout Methods
        public static void BeginHorizontalBackground(bool revertColor = false, params GUILayoutOption[] options)
        {
            BeginRow(LayoutOptions.WithInvertedColor(revertColor), options);
        }

        public static void EndHorizontalBackground(bool addSpace = true)
        {
            EndRow(addSpace ? LayoutOptions.WithSpace() : default);
        }

        public static void BeginVerticalBackground(bool isContainerItem = false, params GUILayoutOption[] options)
        {
            BeginColumn(LayoutOptions.WithInvertedColor(isContainerItem), options);
        }

        public static void EndVerticalBackground(bool addSpace = false)
        {
            EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }
        #endregion

        #region Legacy Draw Methods
        public static void DrawProperty(SerializedProperty property, string label, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, bool hasFoldout = false, int labelWidth = 0)
        {
            Property(property, label, GetLegacyFieldOptions(tooltip, addSpace, customBackground, revertColor, labelWidth));
        }

        public static T DrawObject<T>(T obj, string label, string tooltip = null, bool allowSceneObjects = true,
            bool addSpace = true, bool customBackground = true, bool revertColor = false, int labelWidth = 0) where T : UnityEngine.Object
        {
            return ObjectField(obj, label, allowSceneObjects, GetLegacyFieldOptions(tooltip, addSpace,
                customBackground, revertColor, labelWidth));
        }

        public static int DrawDropdown(int selectedIndex, string[] options, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            return Dropdown(selectedIndex, options, label,
                GetLegacyFieldOptions(null, addSpace, customBackground, revertColor, labelWidth));
        }

        public static void DrawSlider(SerializedProperty intValue, int leftValue, int rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            Slider(intValue, leftValue, rightValue, label,
                GetLegacyFieldOptions(null, addSpace, customBackground, revertColor, labelWidth));
        }

        public static int DrawSlider(int value, int leftValue, int rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            return Slider(value, leftValue, rightValue, label,
                GetLegacyFieldOptions(null, addSpace, customBackground, revertColor));
        }

        public static void DrawSlider(SerializedProperty floatValue, float leftValue, float rightValue, string label = null,
            bool addSpace = true, bool customBackground = true, bool revertColor = false, int labelWidth = 0)
        {
            Slider(floatValue, leftValue, rightValue, label,
                GetLegacyFieldOptions(null, addSpace, customBackground, revertColor, labelWidth));
        }

        public static float DrawSlider(float value, float leftValue, float rightValue, string label, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            return Slider(value, leftValue, rightValue, label,
                GetLegacyFieldOptions(null, addSpace, customBackground, revertColor));
        }

        public static void DrawArrayProperty(SerializedProperty property, string label, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false)
        {
            ArrayProperty(property, label,
                GetLegacyFieldOptions(tooltip, addSpace, customBackground, revertColor));
        }

        public static void DrawToggle(SerializedProperty property, string title, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, string onLabel = "On", string offLabel = "Off",
            bool bypassNormalBackground = false)
        {
            Toggle(property, title, GetLegacyFieldOptions(tooltip, addSpace, customBackground, revertColor),
                onLabel, offLabel, !bypassNormalBackground);
        }

        public static bool DrawToggle(bool value, string title, string tooltip = null, bool addSpace = true,
            bool customBackground = true, bool revertColor = false, string onLabel = "On", string offLabel = "Off",
            bool bypassNormalBackground = false)
        {
            return Toggle(value, title, GetLegacyFieldOptions(tooltip, addSpace, customBackground, revertColor),
                onLabel, offLabel, !bypassNormalBackground);
        }

        public static void DrawLabel(string text, params GUILayoutOption[] options) => Label(text, options);

        public static void DrawIcon(string iconName, string tooltip = null, float width = Metrics.IconWidth,
            float height = Metrics.IconHeight, bool expandHeight = false)
        {
            Icon(iconName, tooltip, width, height, expandHeight);
        }

        public static void DrawIcon(Texture2D icon, string tooltip = null, float width = Metrics.IconWidth,
            float height = Metrics.IconHeight, bool expandHeight = false)
        {
            Icon(icon, tooltip, width, height, expandHeight);
        }

        public static void DrawHeader(string title, Texture2D icon = null, bool addSpaceBefore = false)
        {
            Header(title, icon, addSpaceBefore);
        }

        public static void DrawHeader(string title, string iconName, bool addSpaceBefore = false)
        {
            Header(title, iconName, addSpaceBefore);
        }

        public static void DrawInfoBox(string message, InfoBoxType type = InfoBoxType.Default,
            string customIcon = null, bool revertBackgroundColor = false)
        {
            Callout(message, CalloutType.Default, revertBackgroundColor, 0, 0);
        }

        public static void DrawInfoBox(string message, string customIcon, bool revertColor = false)
        {
            Callout(message, CalloutType.Default, revertColor, 0, 0);
        }

        public static bool DrawButton(string text, string icon, string tooltip = null, float width = 0f, float height = 0f,
            int fontSize = Metrics.ButtonFontSize, int iconSize = Metrics.ButtonIconSize,
            ButtonAlignment iconAlignment = ButtonAlignment.Normal, TextAnchor textAlignment = TextAnchor.MiddleCenter,
            bool reverseAlignment = false, float contentSpacing = Metrics.ButtonContentSpacing,
            Color? normalColor = null, Color? hoverColor = null, Color? pressColor = null,
            bool bypassAdaptiveColor = false, bool revertBackgroundColor = false,
            bool bypassNormalBackground = false)
        {
            ButtonOptions options = GetLegacyButtonOptions(tooltip, width, height, fontSize, iconSize,
                contentSpacing, normalColor, hoverColor, pressColor, revertBackgroundColor, bypassNormalBackground);
            Texture2D resolvedIcon = string.IsNullOrEmpty(icon) ? null : GetIcon(icon);
            return LegacyButton(text, resolvedIcon, options, iconAlignment, textAlignment, reverseAlignment);
        }

        public static bool DrawButton(string text, Texture2D iconSrc = null, string tooltip = null, float width = 0f, float height = 0f,
            int fontSize = Metrics.ButtonFontSize, int iconSize = Metrics.ButtonIconSize,
            ButtonAlignment iconAlignment = ButtonAlignment.Normal, TextAnchor textAlignment = TextAnchor.MiddleCenter,
            bool reverseAlignment = false, float contentSpacing = Metrics.ButtonContentSpacing,
            Color? normalColor = null, Color? hoverColor = null, Color? pressColor = null,
            bool bypassAdaptiveColor = false, bool revertBackgroundColor = false,
            bool bypassNormalBackground = false)
        {
            ButtonOptions options = GetLegacyButtonOptions(tooltip, width, height, fontSize, iconSize,
                contentSpacing, normalColor, hoverColor, pressColor, revertBackgroundColor, bypassNormalBackground);
            return LegacyButton(text, iconSrc, options, iconAlignment, textAlignment, reverseAlignment);
        }

        public static bool DrawFoldout(ref bool foldout, string title, Texture2D icon = null,
            Texture2D customArrow = null, bool useBoldText = true)
        {
            return BeginFoldout(ref foldout, title, FoldoutOptions.WithIcon(icon).WithoutBackground().WithBoldText(useBoldText));
        }

        public static void DrawSeparator(float height = Metrics.SeparatorHeight) => Separator(height);
        #endregion

        #region Legacy Option Mapping
        static FieldOptions GetLegacyFieldOptions(string tooltip, bool addSpace,
            bool customBackground, bool revertColor, int labelWidth = 0)
        {
            return FieldOptions.WithTooltip(tooltip).WithSpace(addSpace)
                .WithBackground(customBackground).WithInvertedColor(revertColor).WithLabelWidth(labelWidth);
        }

        static ButtonOptions GetLegacyButtonOptions(string tooltip, float width, float height,
            int fontSize, int iconSize, float contentSpacing, Color? normalColor, Color? hoverColor,
            Color? pressColor, bool revertBackgroundColor, bool bypassNormalBackground)
        {
            return ButtonOptions.WithTooltip(tooltip).WithSize(width, height).WithFontSize(fontSize)
                .WithIconSize(iconSize).WithContentSpacing(contentSpacing)
                .WithColors(normalColor, hoverColor, pressColor).WithInvertedColor(revertBackgroundColor)
                .WithIdleBackground(!bypassNormalBackground);
        }

        static bool LegacyButton(string text, Texture2D icon, ButtonOptions options,
            ButtonAlignment iconAlignment, TextAnchor textAlignment, bool reverseAlignment)
        {
            ContentAlignment contentAlignment = icon == null ? textAlignment switch
            {
                TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft => ContentAlignment.Left,
                TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => ContentAlignment.Right,
                _ => ContentAlignment.Center
            } : iconAlignment == ButtonAlignment.Normal ? ContentAlignment.Center : ContentAlignment.SpaceBetween;
            IconPosition iconPosition = iconAlignment == ButtonAlignment.Right ||
                (iconAlignment == ButtonAlignment.Normal && reverseAlignment)
                ? IconPosition.AfterText
                : IconPosition.BeforeText;

            return Button(text, icon, options.WithContentAlignment(contentAlignment)
                .WithIconPosition(iconPosition), textAlignment);
        }
        #endregion

        #region Legacy Resources
        // Evo UI v1 keeps its inspector icons in its own Resources folder
        const string LegacyIconResourcesPath = "Editor Textures/Icon-";

        static Texture2D LoadLegacyIcon(string iconName) => Resources.Load<Texture2D>(LegacyIconResourcesPath + iconName);
        #endregion

        #region Deprecated Methods
        public static void RegisterEditor(Editor editor) { }

        public static void UnregisterEditor(Editor editor) { }

        public static void HandleInspectorGUI() => PrepareInteractionRepaint();

        public static Font GetFont(string fontName, bool bypassPrefix = false) => EditorStyles.label.font;
        #endregion
    }
}