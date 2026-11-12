using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI.Editor
{
    [NoAutoStaticsCleanup]
    public static class StylerEditor
    {
        // Style
        const float ColorPreviewSize = 12f;

        // Cache
        static GUIStyle alphaOverrideButtonStyle;
        static Gradient alphaPreviewGradient;

        /// <summary>
        /// Draws the default preset toggle and a preset field that stays locked while the toggle is on.
        /// </summary>
        public static void PresetFields(SerializedProperty useDefaultStylerPreset, SerializedProperty preset,
            string presetLabel = "Styler Preset", bool addSpace = false, bool invertColor = false)
        {
            DefaultPresetToggle(useDefaultStylerPreset, preset, FieldOptions.WithInvertedColor(invertColor));
            PresetField(useDefaultStylerPreset, preset, presetLabel, FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor));
        }

        public static void DefaultPresetToggle(SerializedProperty useDefaultStylerPreset, SerializedProperty preset,
            FieldOptions options = default, bool drawIdleBackground = true)
        {
            EditorGUI.BeginChangeCheck();
            EvoEditorGUI.Toggle(useDefaultStylerPreset, "Use Default Preset", options, drawIdleBackground: drawIdleBackground);

            // Show the default right away rather than after the targets validate
            if (EditorGUI.EndChangeCheck() && useDefaultStylerPreset.boolValue)
                preset.objectReferenceValue = Styler.GetDefaultPreset(false);
        }

        public static void PresetField(SerializedProperty useDefaultStylerPreset, SerializedProperty preset,
            string label = "Styler Preset", FieldOptions options = default)
        {
            using (new EditorGUI.DisabledScope(useDefaultStylerPreset.hasMultipleDifferentValues || useDefaultStylerPreset.boolValue))
                EvoEditorGUI.Property(preset, label, options);
        }

        public static void AudioDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.audioItems,
                "None", label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void ColorDropdown(SerializedProperty preset, SerializedProperty mapping, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ColorDropdown(preset,
                mapping.FindPropertyRelative(nameof(ColorMapping.stylerID)),
                mapping.FindPropertyRelative(nameof(ColorMapping.overrideAlpha)),
                mapping.FindPropertyRelative(nameof(ColorMapping.alphaOverride)),
                label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void ColorDropdown(SerializedProperty preset, SerializedProperty stylerID,
            SerializedProperty overrideAlpha, SerializedProperty alphaOverride, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = true, int labelWidth = 0)
        {
            AlphaOverrideDropdown(overrideAlpha, alphaOverride, "Override preset color alpha",
                addSpace, addBackground, invertColor, labelWidth,
                () => ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.colorItems,
                    "None (Transparent)", label, false, false, invertColor, labelWidth,
                    rect => DrawColorPreview(rect, preset, stylerID, overrideAlpha, alphaOverride)));
        }

        static void AlphaOverrideDropdown(SerializedProperty overrideAlpha, SerializedProperty alphaOverride,
            string enableTooltip, bool addSpace, bool addBackground, bool invertColor, int labelWidth, Action drawDropdown)
        {
            EvoEditorGUI.BeginColumn(LayoutOptions.WithBackground(addBackground).WithInvertedColor(invertColor));
            
            // Main property
            EvoEditorGUI.BeginRow(LayoutOptions.WithoutBackground());
            {
                drawDropdown();

                // Override alpha toggle
                bool isEnabled = !overrideAlpha.hasMultipleDifferentValues && overrideAlpha.boolValue;
                string tooltip = isEnabled ? "Disable alpha override" : enableTooltip;
                Texture2D icon = EvoEditorGUI.GetIcon(EditorTools.IconLibrary.Opacity);
                alphaOverrideButtonStyle ??= new GUIStyle(GUI.skin.button) { padding = new RectOffset(3, 3, 3, 3) };

                GUILayout.Space(-2f);

                bool newValue = GUILayout.Toggle(isEnabled, new GUIContent(icon, tooltip), alphaOverrideButtonStyle, 
                    GUILayout.Width(19f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (newValue != isEnabled)
                    overrideAlpha.boolValue = newValue;
            }
            EvoEditorGUI.EndRow();

            // Alpha property
            if (!overrideAlpha.hasMultipleDifferentValues && overrideAlpha.boolValue)
            {
                EvoEditorGUI.BeginContainer(nestDepth: invertColor ? 1 : 0);
                EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor(!invertColor));
                EvoEditorGUI.Slider(alphaOverride, 0f, 1f, "Alpha", FieldOptions.WithTooltip(alphaOverride.tooltip)
                    .WithoutSpace().WithoutBackground().WithInvertedColor(invertColor).WithLabelWidth(labelWidth));
                EvoEditorGUI.EndRow();
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndColumn(addSpace ? LayoutOptions.WithSpace() : default);
        }

        public static void FontDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.fontItems,
                null, label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void GradientDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.gradientItems,
                "None (Local)", label, addSpace, addBackground, invertColor, labelWidth,
                rect => DrawGradientPreview(rect, preset, stylerID), 24f);
        }

        public static void GradientDropdown(SerializedProperty preset, SerializedProperty stylerID,
            SerializedProperty overrideAlpha, SerializedProperty alphaOverride, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            AlphaOverrideDropdown(overrideAlpha, alphaOverride, "Override preset gradient alpha",
                addSpace, addBackground, invertColor, labelWidth,
                () => ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.gradientItems,
                    "None (Local)", label, false, false, invertColor, labelWidth,
                    rect => DrawGradientPreview(rect, preset, stylerID, overrideAlpha, alphaOverride), 24f));
        }

        public static void ShapeStyleDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.shapeStyleItems,
                "None", label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void SpriteDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.spriteItems,
                "None", label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void TextStyleDropdown(SerializedProperty preset, SerializedProperty stylerID, string label,
            bool addSpace = true, bool addBackground = true, bool invertColor = false, int labelWidth = 0)
        {
            ItemDropdown(preset, stylerID, stylerPreset => stylerPreset.textStyleItems,
                "None", label, addSpace, addBackground, invertColor, labelWidth);
        }

        public static void ColorStyle(SerializedProperty preset, SerializedProperty style,
            SerializedProperty interactableObject, bool invertColor = false, SerializedProperty customColor = null)
        {
            LayerStyle(preset, style, interactableObject, invertColor, customColor, false);
        }

        public static void GradientStyle(SerializedProperty preset, SerializedProperty style,
            SerializedProperty interactableObject, bool invertColor = false)
        {
            LayerStyle(preset, style, interactableObject, invertColor, null, true);
        }

        static void LayerStyle(SerializedProperty preset, SerializedProperty style, SerializedProperty interactableObject,
            bool invertColor, SerializedProperty customColor, bool gradient)
        {
            bool usePreset = customColor == null;
            if (usePreset && preset.objectReferenceValue == null)
            {
                EvoEditorGUI.Callout("No preset assigned. Please assign a valid Styler Preset to use this color mode.", spaceBefore: 2, spaceAfter: 0);
                return;
            }

            SerializedProperty baseColor = style.FindPropertyRelative(gradient ? nameof(Styler.GradientStyle.gradientID) : nameof(Styler.ColorStyle.color));
            SerializedProperty enableInteraction = style.FindPropertyRelative(nameof(Styler.ColorStyle.enableInteraction));
            bool hasInteractionTarget = interactableObject.objectReferenceValue is IStylerInteractable;
            bool interactionEnabled = hasInteractionTarget && enableInteraction.boolValue;
            bool previousGUIState = GUI.enabled;

            GUI.enabled = previousGUIState && !interactionEnabled;
            if (gradient)
                GradientDropdown(preset, baseColor, style.FindPropertyRelative(nameof(Styler.GradientStyle.overrideAlpha)),
                    style.FindPropertyRelative(nameof(Styler.GradientStyle.alphaOverride)), "Gradient ID",
                    addSpace: hasInteractionTarget, invertColor: invertColor);
            else if (usePreset)
                ColorDropdown(preset, baseColor, "Color ID", addSpace: hasInteractionTarget, invertColor: invertColor);
            else
                EvoEditorGUI.Property(customColor, "Color", FieldOptions.WithSpace(hasInteractionTarget).WithInvertedColor(invertColor));
            GUI.enabled = previousGUIState;

            if (!hasInteractionTarget)
                return;

            EvoEditorGUI.BeginColumn(LayoutOptions.WithInvertedColor(invertColor));
            {
                EvoEditorGUI.Toggle(enableInteraction, "Interaction", FieldOptions.WithoutSpace(), drawIdleBackground: false);
                if (enableInteraction.boolValue)
                {
                    EvoEditorGUI.BeginContainer();
                    {
                        if (gradient) { InteractionGradients(preset, style, !invertColor); }
                        else
                        {
                            InteractionColors(preset, usePreset,
                                style.FindPropertyRelative(nameof(Styler.ColorStyle.disabledColor)),
                                style.FindPropertyRelative(nameof(Styler.ColorStyle.normalColor)),
                                style.FindPropertyRelative(nameof(Styler.ColorStyle.highlightedColor)),
                                style.FindPropertyRelative(nameof(Styler.ColorStyle.pressedColor)),
                                style.FindPropertyRelative(nameof(Styler.ColorStyle.selectedColor)),
                                invertColor: !invertColor);
                        }
                    }
                    EvoEditorGUI.EndContainer();
                }
            }
            EvoEditorGUI.EndColumn();
        }

        public static void InteractionColors(SerializedProperty preset, bool usePreset, SerializedProperty disabled, SerializedProperty normal,
            SerializedProperty highlighted, SerializedProperty pressed, SerializedProperty selected, bool invertColor = false)
        {
            string[] stateNames = InteractiveEditor.InteractionStateIDs;
            InteractionColor(preset, usePreset, disabled, stateNames[0], true, invertColor);
            InteractionColor(preset, usePreset, normal, stateNames[1], true, invertColor);
            InteractionColor(preset, usePreset, highlighted, stateNames[2], true, invertColor);
            InteractionColor(preset, usePreset, pressed, stateNames[3], true, invertColor);
            InteractionColor(preset, usePreset, selected, stateNames[4], false, invertColor);
        }

        static void InteractionGradients(SerializedProperty preset, SerializedProperty style, bool invertColor)
        {
            string[] stateNames = InteractiveEditor.InteractionStateIDs;
            InteractionGradient(preset, style, nameof(Styler.GradientStyle.disabledGradientID),
                nameof(Styler.GradientStyle.disabledOverrideAlpha), nameof(Styler.GradientStyle.disabledAlphaOverride), stateNames[0], true, invertColor);
            InteractionGradient(preset, style, nameof(Styler.GradientStyle.normalGradientID),
                nameof(Styler.GradientStyle.normalOverrideAlpha), nameof(Styler.GradientStyle.normalAlphaOverride), stateNames[1], true, invertColor);
            InteractionGradient(preset, style, nameof(Styler.GradientStyle.highlightedGradientID),
                nameof(Styler.GradientStyle.highlightedOverrideAlpha), nameof(Styler.GradientStyle.highlightedAlphaOverride), stateNames[2], true, invertColor);
            InteractionGradient(preset, style, nameof(Styler.GradientStyle.pressedGradientID),
                nameof(Styler.GradientStyle.pressedOverrideAlpha), nameof(Styler.GradientStyle.pressedAlphaOverride), stateNames[3], true, invertColor);
            InteractionGradient(preset, style, nameof(Styler.GradientStyle.selectedGradientID),
                nameof(Styler.GradientStyle.selectedOverrideAlpha), nameof(Styler.GradientStyle.selectedAlphaOverride), stateNames[4], false, invertColor);
        }

        static void InteractionGradient(SerializedProperty preset, SerializedProperty style, string gradientID,
            string overrideAlpha, string alphaOverride, string label, bool addSpace, bool invertColor)
        {
            GradientDropdown(preset, style.FindPropertyRelative(gradientID), style.FindPropertyRelative(overrideAlpha),
                style.FindPropertyRelative(alphaOverride), label, addSpace, invertColor: invertColor);
        }

        static void DrawGradientPreview(Rect rect, SerializedProperty preset, SerializedProperty stylerID,
            SerializedProperty overrideAlpha = null, SerializedProperty alphaOverride = null)
        {
            if (preset.hasMultipleDifferentValues || stylerID.hasMultipleDifferentValues || string.IsNullOrEmpty(stylerID.stringValue))
                return;

            // Mixed alpha overrides have no single gradient to preview
            bool hasAlpha = overrideAlpha != null;
            if (hasAlpha && (overrideAlpha.hasMultipleDifferentValues || (overrideAlpha.boolValue && alphaOverride.hasMultipleDifferentValues)))
                return;

            var stylerPreset = preset.objectReferenceValue as StylerPreset;
            if (stylerPreset == null || !stylerPreset.TryGetGradient(stylerID.stringValue, out Gradient gradient) || gradient == null)
                return;

            if (hasAlpha && overrideAlpha.boolValue)
                gradient = GetAlphaPreview(gradient, Mathf.Clamp01(alphaOverride.floatValue));

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.GradientField(rect, GUIContent.none, gradient);
        }

        // Matches the runtime ramp: the peak alpha becomes the override and fades keep their shape
        static Gradient GetAlphaPreview(Gradient gradient, float alpha)
        {
            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
            float peak = 0f;
            for (int i = 0; i < alphaKeys.Length; i++)
                peak = Mathf.Max(peak, alphaKeys[i].alpha);
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i].alpha = peak > 0f ? alphaKeys[i].alpha / peak * alpha : alpha;

            alphaPreviewGradient ??= new Gradient();
            alphaPreviewGradient.mode = gradient.mode;
            alphaPreviewGradient.colorSpace = gradient.colorSpace;
            alphaPreviewGradient.SetKeys(gradient.colorKeys, alphaKeys);
            return alphaPreviewGradient;
        }

        static void DrawColorPreview(Rect rect, SerializedProperty preset, SerializedProperty stylerID,
            SerializedProperty overrideAlpha, SerializedProperty alphaOverride)
        {
            // Empty, missing and mixed selections have no single preset color to preview
            if (Event.current.type != EventType.Repaint || preset.hasMultipleDifferentValues
                || stylerID.hasMultipleDifferentValues || overrideAlpha.hasMultipleDifferentValues
                || string.IsNullOrEmpty(stylerID.stringValue))
                return;

            if (overrideAlpha.boolValue && alphaOverride.hasMultipleDifferentValues)
                return;

            var stylerPreset = preset.objectReferenceValue as StylerPreset;
            if (stylerPreset == null || !stylerPreset.TryGetColor(stylerID.stringValue, out Color color))
                return;

            if (overrideAlpha.boolValue)
                color.a = alphaOverride.floatValue;

            EvoEditorGUI.DrawRoundedBackground(rect, color);
        }

        static void InteractionColor(SerializedProperty preset, bool usePreset, SerializedProperty mapping,
            string label, bool addSpace, bool invertColor)
        {
            if (usePreset && preset.objectReferenceValue != null)
            {
                ColorDropdown(preset, mapping, label, addSpace, invertColor: invertColor);
                return;
            }

            EvoEditorGUI.Property(mapping.FindPropertyRelative(nameof(ColorMapping.color)), label,
                FieldOptions.WithSpace(addSpace).WithInvertedColor(invertColor));
        }

        static void ItemDropdown(SerializedProperty preset, SerializedProperty stylerProperty,
            Func<StylerPreset, IReadOnlyList<Styler.Item>> itemsGetter, string emptyOptionLabel, string label, bool addSpace, bool addBackground,
            bool invertColor, int labelWidth, Action<Rect> drawPreview = null, float previewWidth = ColorPreviewSize)
        {
            var stylerPresetValue = preset.objectReferenceValue as StylerPreset;
            if (stylerPresetValue == null)
            {
                EvoEditorGUI.Property(stylerProperty, label, FieldOptions.WithSpace(addSpace)
                    .WithBackground(addBackground).WithInvertedColor(invertColor).WithLabelWidth(labelWidth));
                return;
            }

            IReadOnlyList<Styler.Item> items = itemsGetter(stylerPresetValue);
            var baseOptions = new string[items.Count];

            for (int i = 0; i < baseOptions.Length; i++)
                baseOptions[i] = items[i]?.itemID;

            string currentValue = stylerProperty.stringValue;
            int itemIndex = Array.IndexOf(baseOptions, currentValue);
            bool hasEmptyOption = emptyOptionLabel != null;
            bool isMissing = !string.IsNullOrEmpty(currentValue) && itemIndex < 0;
            int itemOffset = (hasEmptyOption ? 1 : 0) + (isMissing ? 1 : 0);

            // A renamed item still resolves its former IDs, in the same order as the runtime lookup
            int formerIndex = -1;

            for (int i = 0; isMissing && formerIndex < 0 && i < items.Count; i++)
            {
                if (items[i]?.formerIDs != null && items[i].formerIDs.Contains(currentValue))
                    formerIndex = i;
            }

            var optionLabels = new string[baseOptions.Length + itemOffset];
            var optionValues = new string[optionLabels.Length];
            int nextIndex = 0;

            if (hasEmptyOption)
            {
                optionLabels[nextIndex] = emptyOptionLabel;
                optionValues[nextIndex] = string.Empty;
                nextIndex++;
            }

            if (isMissing)
            {
                optionLabels[nextIndex] = formerIndex >= 0 ? $"{baseOptions[formerIndex]} (was {currentValue})" : currentValue + " (Missing)";
                optionValues[nextIndex] = currentValue;
                nextIndex++;
            }

            Array.Copy(baseOptions, 0, optionLabels, nextIndex, baseOptions.Length);
            Array.Copy(baseOptions, 0, optionValues, nextIndex, baseOptions.Length);

            int selectedIndex = isMissing ? (hasEmptyOption ? 1 : 0) : itemIndex >= 0 ? itemIndex + itemOffset : 0;
            if (drawPreview != null)
            {
                ItemDropdownWithPreview(stylerProperty, selectedIndex, optionLabels, optionValues, label,
                    addSpace, addBackground, invertColor, labelWidth, drawPreview, previewWidth);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                int newSelectedIndex = EvoEditorGUI.Dropdown(selectedIndex, optionLabels, label, FieldOptions.WithSpace(addSpace)
                    .WithBackground(addBackground).WithInvertedColor(invertColor).WithLabelWidth(labelWidth));
                if (EditorGUI.EndChangeCheck() && newSelectedIndex >= 0 && newSelectedIndex < optionValues.Length)
                    stylerProperty.stringValue = optionValues[newSelectedIndex];
            }

            // A picked ID may now be in use, so renaming its item keeps the old name working
            if (stylerProperty.stringValue != currentValue)
                StylerIDMigration.MarkUsed(stylerPresetValue, stylerProperty.stringValue);
        }

        static void ItemDropdownWithPreview(SerializedProperty stylerProperty, int selectedIndex,
            string[] optionLabels, string[] optionValues, string label, bool addSpace, bool addBackground,
            bool invertColor, int labelWidth, Action<Rect> drawPreview, float previewWidth)
        {
            LayoutOptions layoutOptions = LayoutOptions.WithBackground(addBackground).WithInvertedColor(invertColor);
            
            if (addSpace)
                layoutOptions = layoutOptions.WithSpace();

            EvoEditorGUI.BeginRow(layoutOptions);
            float previousLabelWidth = EditorGUIUtility.labelWidth;

            try
            {
                if (labelWidth > 0)
                    EditorGUIUtility.labelWidth = labelWidth;

                GUILayout.Space(EvoEditorGUI.Metrics.PropertyLabelPadding);
                Rect popupRect = EditorGUILayout.GetControlRect();
                using var propScope = new EditorGUI.PropertyScope(popupRect, new GUIContent(label, stylerProperty.tooltip), stylerProperty);
                popupRect = EditorGUI.PrefixLabel(popupRect, propScope.content);

                Rect previewRect = new(popupRect.x, popupRect.y + (popupRect.height - ColorPreviewSize) * 0.5f, previewWidth, ColorPreviewSize);
                popupRect.xMin += previewWidth + 4f;

                EditorGUI.BeginChangeCheck();
                int newSelectedIndex = EditorGUI.Popup(popupRect, selectedIndex, optionLabels, EditorStyles.popup);
                if (EditorGUI.EndChangeCheck() && newSelectedIndex >= 0 && newSelectedIndex < optionValues.Length)
                    stylerProperty.stringValue = optionValues[newSelectedIndex];

                drawPreview(previewRect);
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EvoEditorGUI.EndRow(layoutOptions);
            }
        }
    }
}