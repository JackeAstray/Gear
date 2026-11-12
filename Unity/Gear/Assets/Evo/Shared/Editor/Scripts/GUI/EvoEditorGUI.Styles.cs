using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public static partial class EvoEditorGUI
    {
        public static class Metrics
        {
            // Caching
            public const int ContentCacheCapacity = 512;
            public const int IconCacheCapacity = 128;

            // Global
            public const float LayoutSpacing = 3f;
            public const int ContainerPadding = 5;
            public const float PropertySpacing = 5f;
            public const float PropertyLabelPadding = 5f;
            public const float PropertyFoldoutPadding = 16f;
            public const float ControlHeightOffset = 4f;
            public const float RoundedCornerRadius = 4f;
            public const int RoundedTextureSize = 32;
            public const int RoundedTextureSupersampling = 4;
            public const int RoundedTextureBorder = 6;
            public const float InteractionBorderThickness = 1f;
            public const double InteractionRepaintInterval = 1d / 120d;

            // Animation (seconds)
            public const double FoldoutAnimationDuration = 0.16;
            public const double ReorderAnimationDuration = 0.14;

            // Centered Inspector
            public const int CenteredInspectorLeft = -8;
            public const int CenteredInspectorRight = 3;
            public const int CenteredInspectorTop = 3;
            public const int CenteredInspectorSpaciousLeft = 6;
            public const int CenteredInspectorSpaciousRight = 8;
            public const int CenteredInspectorSpaciousTop = 7;
            public const float CenteredInspectorOffset = -10f;

            // Container
            public const float ContainerLabelOffset = 2f;
            public const float ContainerLabelSpacing = 1f;

            // Button
            public const int ButtonFontSize = 12;
            public const int ButtonIconSize = 12;
            public const float ButtonContentSpacing = 6f;
            public const int ButtonPaddingLeft = 7;
            public const int ButtonPaddingRight = 7;
            public const int ButtonPaddingTop = 4;
            public const int ButtonPaddingBottom = 4;
            public const int ButtonStyleCacheCapacity = 32;
            public const int ButtonLabelStyleCacheCapacity = 32;

            // Header
            public const float HeaderIconSize = 14.5f;
            public const int HeaderFontSize = 14;
            public const float HeaderSpaceBefore = 18f;
            public const int HeaderMarginTop = 2;
            public const float HeaderIconSpacing = 6f;
            public const float HeaderSpaceAfter = 5f;

            // Foldout
            public const float FoldoutHeight = 28f;
            public const float FoldoutIconSize = 14.5f;
            public const int FoldoutFontSize = 14;
            public const float FoldoutArrowSize = FoldoutIconSize;
            public const int FoldoutPaddingLeft = 8;
            public const int FoldoutPaddingRight = 8;
            public const int FoldoutPaddingTop = 7;
            public const int FoldoutPaddingBottom = 7;
            public const float FoldoutIconLeft = FoldoutPaddingLeft + FoldoutArrowSize + 4f;
            public const float FoldoutTextLeft = FoldoutPaddingLeft + FoldoutArrowSize + 3f;
            public const float FoldoutTextLeftWithIcon = FoldoutIconLeft + FoldoutIconSize + 6f;
            public const float FoldoutTextRight = FoldoutPaddingRight + 36f;
            public const float FoldoutSpacing = 4f;
            public const int FoldoutLabelStyleCacheCapacity = 32;

            // Foldout Compact
            public const float FoldoutHeightCompact = 22f;
            public const float FoldoutIconSizeCompact = 12f;
            public const int FoldoutFontSizeCompact = 12;
            public const float FoldoutArrowSizeCompact = FoldoutIconSizeCompact;
            public const int FoldoutPaddingLeftCompact = 6;
            public const int FoldoutPaddingRightCompact = 6;
            public const int FoldoutPaddingTopCompact = 2;
            public const int FoldoutPaddingBottomCompact = 2;
            public const float FoldoutIconLeftCompact = FoldoutPaddingLeftCompact + FoldoutArrowSizeCompact + 4f;
            public const float FoldoutTextLeftCompact = FoldoutPaddingLeftCompact + FoldoutArrowSizeCompact + 4f;
            public const float FoldoutTextLeftWithIconCompact = FoldoutIconLeftCompact + FoldoutIconSizeCompact + 5f;
            public const float FoldoutTextRightCompact = FoldoutPaddingRightCompact + 26f;

            // Toggle
            public const float ToggleHeight = 14f;
            public const float ToggleWidth = 14f;
            public const int ToggleCheckmarkPadding = 3;
            public const float ToggleDashWidth = 6f;
            public const float ToggleDashHeight = 2f;
            public const float ToggleDashRadius = 1f;
            public const float ToggleRightPadding = 4f;
            public const float ToggleContentSpacing = PropertyLabelPadding;

            // Callout
            public const float CalloutIconSize = 21f;
            public const int CalloutFontSize = 11;
            public const float CalloutContentSpacing = 4f;
            public const float CalloutMinHeight = 36f;
            public const int CalloutPaddingLeft = 8;
            public const int CalloutPaddingRight = 6;
            public const int CalloutPaddingTop = 5;
            public const int CalloutPaddingBottom = 4;

            // Icon
            public const float IconWidth = 18f;
            public const float IconHeight = 18f;

            // Separator
            public const float SeparatorHeight = 1.5f;

            // Drag border
            public const float DragBorderThickness = 2f;
        }

        public static class Palette
        {
            /// <summary>
            /// Used in labels, icons, and semi-interactable components, such as foldout.
            /// </summary>
            public static Color Content => GUI.skin.label.normal.textColor;

            /// <summary>
            /// Used in fully-interactable elements, such as button.
            /// </summary>
            public static Color InteractableContent => GUI.skin.button.normal.textColor;

            /// <summary>
            /// Used in highlighted elements and borders. 
            /// </summary>
            public static Color Selection => GUI.skin.settings.selectionColor;

            public static Color BorderHover => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.15f)
                : new Color(0f, 0f, 0f, 0.15f);

            public static Color BorderPress => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.25f)
                : new Color(0f, 0f, 0f, 0.25f);

            public static Color Background => EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.2f)
                : new Color(0f, 0f, 0f, 0.1f);

            public static Color BackgroundHover => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.1f)
                : new Color(0f, 0f, 0f, 0.06f);

            public static Color BackgroundPress => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.1f);

            public static Color BackgroundInverse => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(1f, 1f, 1f, 0.2f);

            public static Color BackgroundInverseHover => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.16f)
                : new Color(0f, 0f, 0f, 0.04f);

            public static Color BackgroundInversePress => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0f, 0f, 0f, 0.08f);

            public static Color CalloutWarning => EditorGUIUtility.isProSkin
                ? new Color(0.8f, 0.6f, 0.1f, 0.2f)
                : new Color(1f, 0.8f, 0.2f, 0.3f);

            public static Color CalloutError => EditorGUIUtility.isProSkin
                ? new Color(0.8f, 0.2f, 0.2f, 0.2f)
                : new Color(1f, 0.3f, 0.3f, 0.3f);

            public static Color Separator => EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.1f)
                : new Color(0f, 0f, 0f, 0.2f);

            public static Color GetBackgroundColor(bool invert = false, bool hover = false, bool pressed = false)
            {
                if (pressed)
                    return invert ? BackgroundInversePress : BackgroundPress;

                if (hover)
                    return invert ? BackgroundInverseHover : BackgroundHover;

                return invert ? BackgroundInverse : Background;
            }
        }

        [NoAutoStaticsCleanup]
        static class Styles
        {
            static bool initialized;
            static bool initializedForDarkSkin;

            // The fixed styles' textures come first, followed by those made for button and callout colors
            static readonly List<Texture2D> generatedTextures = new();
            static int fixedTextureCount;

            static readonly Dictionary<(Color32 fill, Color32 border), Texture2D> fillTextures = new();
            static readonly List<(Color32 fill, Color32 border)> releasedFillKeys = new();
            static readonly Dictionary<(int padding, bool compactHeader), GUIStyle> containerStyles = new();
            static readonly Dictionary<ButtonStyleKey, GUIStyle> buttonStyles = new();
            static readonly Dictionary<(int fontSize, TextAnchor alignment, Color32 color), GUIStyle> buttonLabelStyles = new();
            static readonly Dictionary<(Color32 color, bool bold, bool compact), GUIStyle> foldoutLabelStyles = new();
            static readonly Dictionary<Color32, GUIStyle> infoBoxStyles = new();

            public static GUIStyle Background { get; private set; }
            public static GUIStyle CenteredInspector { get; private set; }
            public static GUIStyle CenteredInspectorSpacious { get; private set; }
            public static GUIStyle Header { get; private set; }
            public static GUIStyle FoldoutLabel { get; private set; }
            public static GUIStyle FoldoutLabelCompact { get; private set; }
            public static GUIStyle FoldoutLabelSimple { get; private set; }
            public static GUIStyle FoldoutLabelSimpleCompact { get; private set; }
            public static GUIStyle FoldoutBackground { get; private set; }
            public static GUIStyle FoldoutBackgroundCompact { get; private set; }
            public static GUIStyle ToggleIndicator { get; private set; }
            public static GUIContent ToggleCheckmark { get; private set; }
            public static GUIStyle CalloutText { get; private set; }
            public static GUIStyle DragBorder { get; private set; }

            static Styles()
            {
                AssemblyReloadEvents.beforeAssemblyReload += Release;
            }

            public static void Ensure()
            {
                bool darkTheme = EditorGUIUtility.isProSkin;

                if (initialized && initializedForDarkSkin == darkTheme)
                    return;

                Release();
                initialized = true;
                initializedForDarkSkin = darkTheme;

                Texture2D roundedMask = CreateRoundedTexture("EvoRoundedBackground", Color.white);
                Texture2D dragBorder = CreateRoundedTexture("EvoRoundedBorder", Color.clear, Color.white, Metrics.DragBorderThickness);

                Background = new GUIStyle
                {
                    normal = { background = roundedMask },
                    border = CreateRoundedBorder()
                };

                CenteredInspector = new GUIStyle
                {
                    padding = new RectOffset(Metrics.CenteredInspectorLeft, Metrics.CenteredInspectorRight, Metrics.CenteredInspectorTop, 0)
                };

                CenteredInspectorSpacious = new GUIStyle
                {
                    padding = new RectOffset(Metrics.CenteredInspectorSpaciousLeft, Metrics.CenteredInspectorSpaciousRight,
                        Metrics.CenteredInspectorSpaciousTop, 0)
                };

                Header = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = Metrics.HeaderFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    margin = new RectOffset(0, 0, Metrics.HeaderMarginTop, 0)
                };

                FoldoutLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = Metrics.FoldoutFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };

                FoldoutLabelCompact = new GUIStyle(FoldoutLabel)
                {
                    fontSize = Metrics.FoldoutFontSizeCompact
                };

                FoldoutLabelSimple = new GUIStyle(EditorStyles.label)
                {
                    fontSize = Metrics.FoldoutFontSize,
                    alignment = TextAnchor.MiddleLeft
                };

                FoldoutLabelSimpleCompact = new GUIStyle(FoldoutLabelSimple)
                {
                    fontSize = Metrics.FoldoutFontSizeCompact
                };

                FoldoutBackground = CreateStateStyle(null, Palette.BackgroundHover, Palette.BackgroundPress);
                FoldoutBackground.padding = new RectOffset(Metrics.FoldoutPaddingLeft, Metrics.FoldoutPaddingRight,
                    Metrics.FoldoutPaddingTop, Metrics.FoldoutPaddingBottom);

                FoldoutBackgroundCompact = CreateStateStyle(null, Palette.BackgroundHover, Palette.BackgroundPress);
                FoldoutBackgroundCompact.padding = new RectOffset(Metrics.FoldoutPaddingLeftCompact,
                    Metrics.FoldoutPaddingRightCompact, Metrics.FoldoutPaddingTopCompact, Metrics.FoldoutPaddingBottomCompact);

                // Use the light checkmark as a tintable mask in either editor skin.
                ToggleCheckmark = new GUIContent(GetIcon("d_checkmark", false));
                ToggleIndicator = new GUIStyle
                {
                    border = CreateRoundedBorder(),
                    padding = new RectOffset(Metrics.ToggleCheckmarkPadding, Metrics.ToggleCheckmarkPadding,
                        Metrics.ToggleCheckmarkPadding, Metrics.ToggleCheckmarkPadding),
                    alignment = TextAnchor.MiddleCenter,
                    imagePosition = ImagePosition.ImageOnly,
                    normal = { background = GetFillTexture(Palette.Background, Palette.BorderPress) },
                    onNormal = { background = GetFillTexture(Palette.Content) }
                };

                CalloutText = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = Metrics.CalloutFontSize,
                    wordWrap = true,
                    richText = true,
                    alignment = TextAnchor.MiddleLeft,
                    margin = new RectOffset()
                };

                DragBorder = new GUIStyle
                {
                    normal = { background = dragBorder },
                    border = CreateRoundedBorder()
                };

                fixedTextureCount = generatedTextures.Count;
            }

            public static GUIStyle GetContainer(int padding, bool compactHeader)
            {
                Ensure();

                var key = (padding, compactHeader);
                if (containerStyles.TryGetValue(key, out GUIStyle style))
                    return style;

                style = new GUIStyle
                {
                    padding = new RectOffset(padding, padding, compactHeader ? 0 : padding, padding)
                };
                containerStyles.Add(key, style);
                return style;
            }

            public static GUIStyle GetButton(Color normal, Color hover, Color active)
            {
                Ensure();

                ButtonStyleKey key = new(normal, hover, active);
                if (buttonStyles.TryGetValue(key, out GUIStyle style))
                    return style;

                if (buttonStyles.Count >= Metrics.ButtonStyleCacheCapacity)
                    ReleaseColorStyles();

                style = CreateStateStyle(normal, hover, active);
                style.padding = new RectOffset(Metrics.ButtonPaddingLeft, Metrics.ButtonPaddingRight,
                    Metrics.ButtonPaddingTop, Metrics.ButtonPaddingBottom);
                style.alignment = TextAnchor.MiddleCenter;
                buttonStyles.Add(key, style);
                return style;
            }

            public static GUIStyle GetButtonLabel(int fontSize, TextAnchor alignment, Color textColor)
            {
                Ensure();

                var key = (fontSize, alignment, (Color32)textColor);
                if (buttonLabelStyles.TryGetValue(key, out GUIStyle style))
                    return style;

                if (buttonLabelStyles.Count >= Metrics.ButtonLabelStyleCacheCapacity)
                    buttonLabelStyles.Clear();

                style = new GUIStyle(EditorStyles.label)
                {
                    fontSize = fontSize,
                    alignment = alignment,
                    padding = new RectOffset()
                };
                SetTextColor(style, textColor);
                buttonLabelStyles.Add(key, style);
                return style;
            }

            public static GUIStyle GetFoldoutLabel(Color textColor, bool compact = false)
            {
                return GetFoldoutLabel(textColor, true, compact);
            }

            public static GUIStyle GetFoldoutLabel(Color textColor, bool bold, bool compact)
            {
                Ensure();

                var key = ((Color32)textColor, bold, compact);
                if (foldoutLabelStyles.TryGetValue(key, out GUIStyle style))
                    return style;

                if (foldoutLabelStyles.Count >= Metrics.FoldoutLabelStyleCacheCapacity)
                    foldoutLabelStyles.Clear();

                style = new GUIStyle(bold
                    ? compact ? FoldoutLabelCompact : FoldoutLabel
                    : compact ? FoldoutLabelSimpleCompact : FoldoutLabelSimple);
                SetTextColor(style, textColor);
                foldoutLabelStyles.Add(key, style);
                return style;
            }

            public static GUIStyle GetCallout(Color color)
            {
                Ensure();

                Color32 key = color;
                if (infoBoxStyles.TryGetValue(key, out GUIStyle style))
                    return style;

                style = new GUIStyle
                {
                    normal = { background = GetFillTexture(color) },
                    border = CreateRoundedBorder(),
                    padding = new RectOffset(Metrics.CalloutPaddingLeft, Metrics.CalloutPaddingRight,
                        Metrics.CalloutPaddingTop, Metrics.CalloutPaddingBottom),
                    wordWrap = true
                };
                infoBoxStyles.Add(key, style);
                return style;
            }

            static Texture2D GetFillTexture(Color color, Color borderColor = default)
            {
                var key = ((Color32)color, (Color32)borderColor);
                if (fillTextures.TryGetValue(key, out Texture2D texture))
                    return texture;

                texture = CreateRoundedTexture("EvoEditorGUI-RoundedFill", color, borderColor, Metrics.InteractionBorderThickness);
                fillTextures.Add(key, texture);
                return texture;
            }

            static GUIStyle CreateStateStyle(Color? normal, Color hover, Color active)
            {
                GUIStyle style = new();
                style.normal.background = normal.HasValue ? GetFillTexture(normal.Value) : null;
                style.hover.background = GetFillTexture(hover, Palette.BorderHover);
                style.active.background = GetFillTexture(active, Palette.BorderPress);
                style.focused.background = style.normal.background;
                style.onNormal.background = style.normal.background;
                style.onHover.background = style.hover.background;
                style.onActive.background = style.active.background;
                style.onFocused.background = style.normal.background;
                style.border = CreateRoundedBorder();
                return style;
            }

            static Texture2D CreateRoundedTexture(string name, Color color, Color borderColor = default, float borderThickness = 0f)
            {
                int size = Metrics.RoundedTextureSize;
                int samples = Metrics.RoundedTextureSupersampling;
                float halfSize = size * 0.5f;
                float radius = Metrics.RoundedCornerRadius;
                bool hasBorder = borderThickness > 0f && borderColor.a > 0f;
                Color32[] pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int coveredSamples = 0;
                        int borderSamples = 0;

                        for (int sampleY = 0; sampleY < samples; sampleY++)
                        {
                            for (int sampleX = 0; sampleX < samples; sampleX++)
                            {
                                float positionX = x + (sampleX + 0.5f) / samples;
                                float positionY = y + (sampleY + 0.5f) / samples;
                                bool insideOuter = IsInsideRoundedRect(positionX, positionY, halfSize, radius);
                                if (!insideOuter)
                                    continue;

                                coveredSamples++;
                                if (hasBorder && !IsInsideRoundedRect(positionX, positionY,
                                    halfSize - borderThickness, Mathf.Max(0f, radius - borderThickness)))
                                    borderSamples++;
                            }
                        }

                        float coverage = coveredSamples / (float)(samples * samples);
                        Color pixel = color.a > 0f ? color : borderColor;
                        pixel.a = color.a * coverage;
                        if (borderSamples > 0)
                        {
                            // Composite the border over the fill before averaging sample coverage
                            float borderAlpha = borderColor.a * borderSamples / (samples * samples);
                            float fillAlpha = color.a * (coverage - borderAlpha);
                            float alpha = borderAlpha + fillAlpha;
                            pixel = (borderColor * borderAlpha + color * fillAlpha) / alpha;
                            pixel.a = alpha;
                        }
                        pixels[y * size + x] = pixel;
                    }
                }

                Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                generatedTextures.Add(texture);
                return texture;
            }

            static RectOffset CreateRoundedBorder()
            {
                return new RectOffset(Metrics.RoundedTextureBorder, Metrics.RoundedTextureBorder,
                    Metrics.RoundedTextureBorder, Metrics.RoundedTextureBorder);
            }

            static void SetTextColor(GUIStyle style, Color color)
            {
                style.normal.textColor = color;
                style.hover.textColor = color;
                style.active.textColor = color;
                style.focused.textColor = color;
                style.onNormal.textColor = color;
                style.onHover.textColor = color;
                style.onActive.textColor = color;
                style.onFocused.textColor = color;
            }

            static bool IsInsideRoundedRect(float x, float y, float halfSize, float radius)
            {
                float distanceX = Mathf.Abs(x - Metrics.RoundedTextureSize * 0.5f);
                float distanceY = Mathf.Abs(y - Metrics.RoundedTextureSize * 0.5f);
                float straightExtent = halfSize - radius;

                if (distanceX > halfSize || distanceY > halfSize)
                    return false;

                if (distanceX <= straightExtent || distanceY <= straightExtent)
                    return true;

                float cornerX = distanceX - straightExtent;
                float cornerY = distanceY - straightExtent;
                return cornerX * cornerX + cornerY * cornerY <= radius * radius;
            }

            static void Release()
            {
                for (int i = 0; i < generatedTextures.Count; i++)
                {
                    if (generatedTextures[i] != null)
                        UnityEngine.Object.DestroyImmediate(generatedTextures[i]);
                }

                generatedTextures.Clear();
                fillTextures.Clear();
                containerStyles.Clear();
                buttonStyles.Clear();
                buttonLabelStyles.Clear();
                foldoutLabelStyles.Clear();
                infoBoxStyles.Clear();
                fixedTextureCount = 0;
                initialized = false;
            }

            static void ReleaseColorStyles()
            {
                // Button and callout styles go together with the textures made for their colors, so callers passing
                // ever-new colors don't pile up textures. The fixed styles keep theirs
                buttonStyles.Clear();
                infoBoxStyles.Clear();

                for (int i = fixedTextureCount; i < generatedTextures.Count; i++)
                {
                    if (generatedTextures[i] != null)
                        UnityEngine.Object.DestroyImmediate(generatedTextures[i]);
                }

                generatedTextures.RemoveRange(fixedTextureCount, generatedTextures.Count - fixedTextureCount);

                releasedFillKeys.Clear();
                foreach (KeyValuePair<(Color32 fill, Color32 border), Texture2D> entry in fillTextures)
                {
                    if (entry.Value == null)
                        releasedFillKeys.Add(entry.Key);
                }

                for (int i = 0; i < releasedFillKeys.Count; i++)
                    fillTextures.Remove(releasedFillKeys[i]);
            }

            readonly struct ButtonStyleKey : IEquatable<ButtonStyleKey>
            {
                readonly Color32 normal;
                readonly Color32 hover;
                readonly Color32 active;

                public ButtonStyleKey(Color normal, Color hover, Color active)
                {
                    this.normal = normal;
                    this.hover = hover;
                    this.active = active;
                }

                public bool Equals(ButtonStyleKey other)
                {
                    return normal.Equals(other.normal) && hover.Equals(other.hover) && active.Equals(other.active);
                }

                public override bool Equals(object obj) => obj is ButtonStyleKey other && Equals(other);

                public override int GetHashCode()
                {
                    unchecked
                    {
                        int hash = normal.GetHashCode();
                        hash = (hash * 397) ^ hover.GetHashCode();
                        return (hash * 397) ^ active.GetHashCode();
                    }
                }
            }
        }
    }
}