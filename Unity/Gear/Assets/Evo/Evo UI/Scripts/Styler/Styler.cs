using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [NoAutoStaticsCleanup]
    public class Styler
    {
        #region Runtime State
        /// <summary>
        /// Caches only the configured default; runtime overrides never replace it.
        /// </summary>
        static StylerPreset CachedDefaultPreset;

        /// <summary>
        /// Preset inherited on registration. Set to null to stop overriding future objects.
        /// </summary>
        public static StylerPreset PersistentPreset { get; set; }

        /// <summary>
        /// Read-only list of registered IStylerHandler objects.
        /// </summary>
        public static readonly List<IStylerHandler> RegisteredHandlers = new();
        // Each handler's position in RegisteredHandlers, so enabling and disabling one doesn't scan thousands
        static readonly Dictionary<IStylerHandler, int> handlerIndices = new(new HandlerComparer());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            RegisteredHandlers.Clear();
            handlerIndices.Clear();
            CachedDefaultPreset = null;
            PersistentPreset = null;
        }
        #endregion

        #region Types
        /// <summary>
        /// Identify the item type.
        /// </summary>
        public enum ItemType
        {
            Audio,
            Color,
            Font,
            Sprite,
            Gradient,
            TextStyle,
            ShapeStyle
        }

        /// <summary>
        /// Used in StylerPreset to set the update mode.
        /// </summary>
        public enum UpdateMode
        {
            Always = 0,     // Updates every frame in both Editor and Play mode
            Adaptive = 1,   // Updates every frame in the Editor, but only once in Play mode
        }

        /// <summary>
        /// Procedural Shape feature groups that a shape style overrides.
        /// </summary>
        [Flags]
        public enum ShapeStyleFlags
        {
            None = 0,
            Graphic = 1 << 0,
            Shape = 1 << 1,
            Fill = 1 << 2,
            Outline = 1 << 3,
            InnerShadow = 1 << 4,
            OuterShadow = 1 << 5
        }

        /// <summary>
        /// Text feature groups that a text style overrides.
        /// </summary>
        [Flags]
        public enum TextStyleFlags
        {
            None = 0,
            Size = 1 << 0,
            Alignment = 1 << 1,
            WrappingAndOverflow = 1 << 2,
            Spacing = 1 << 3,
            Margin = 1 << 4,
            FontStyle = 1 << 5
        }

        /// <summary>
        /// Reusable Styler color configuration with optional interaction-state mappings.
        /// </summary>
        [Serializable]
        public class ColorStyle
        {
            public ColorMapping color = new() { stylerID = "Primary" };
            [Tooltip("Use interaction-state colors for this layer.")]
            public bool enableInteraction;
            public ColorMapping disabledColor = new() { stylerID = "Primary" };
            public ColorMapping normalColor = new() { stylerID = "Primary" };
            public ColorMapping highlightedColor = new() { stylerID = "Primary" };
            public ColorMapping pressedColor = new() { stylerID = "Primary" };
            public ColorMapping selectedColor = new() { stylerID = "Primary" };

            public ColorMapping GetMapping(InteractionState state)
            {
                return GetColorMappingForState(state, disabledColor, normalColor, highlightedColor, pressedColor, selectedColor);
            }

            public Color ResolveColor(StylerPreset preset, InteractionState state, bool useInteraction)
            {
                ColorMapping mapping = useInteraction ? GetMapping(state) : color;
                return GetColor(StyleProvider.Styler, mapping, preset);
            }
        }

        /// <summary>
        /// Styler gradient IDs with optional interaction-state mappings.
        /// </summary>
        [Serializable]
        public class GradientStyle
        {
            public string gradientID = "";
            public bool overrideAlpha;
            [Range(0f, 1f)] public float alphaOverride = 1f;
            [Tooltip("Use interaction-state gradients for this layer.")]
            public bool enableInteraction;
            public string disabledGradientID = "";
            public bool disabledOverrideAlpha;
            [Range(0f, 1f)] public float disabledAlphaOverride = 1f;
            public string normalGradientID = "";
            public bool normalOverrideAlpha;
            [Range(0f, 1f)] public float normalAlphaOverride = 1f;
            public string highlightedGradientID = "";
            public bool highlightedOverrideAlpha;
            [Range(0f, 1f)] public float highlightedAlphaOverride = 1f;
            public string pressedGradientID = "";
            public bool pressedOverrideAlpha;
            [Range(0f, 1f)] public float pressedAlphaOverride = 1f;
            public string selectedGradientID = "";
            public bool selectedOverrideAlpha;
            [Range(0f, 1f)] public float selectedAlphaOverride = 1f;

            public Gradient ResolveGradient(StylerPreset preset, InteractionState state, bool useInteraction, Gradient fallback)
            {
                string id = GetMapping(state, useInteraction).id;
                return preset != null && !string.IsNullOrEmpty(id) && preset.TryGetGradient(id, out Gradient value) ? value : fallback;
            }

            /// <summary>
            /// Returns the peak alpha for the resolved gradient, or -1 when it keeps its own alpha.
            /// </summary>
            public float ResolveAlphaOverride(InteractionState state, bool useInteraction)
            {
                var mapping = GetMapping(state, useInteraction);
                return mapping.overrideAlpha ? Mathf.Clamp01(mapping.alpha) : -1f;
            }

            (string id, bool overrideAlpha, float alpha) GetMapping(InteractionState state, bool useInteraction)
            {
                if (!useInteraction || !enableInteraction)
                    return (gradientID, overrideAlpha, alphaOverride);

                return state switch
                {
                    InteractionState.Disabled => (disabledGradientID, disabledOverrideAlpha, disabledAlphaOverride),
                    InteractionState.Highlighted => (highlightedGradientID, highlightedOverrideAlpha, highlightedAlphaOverride),
                    InteractionState.Pressed => (pressedGradientID, pressedOverrideAlpha, pressedAlphaOverride),
                    InteractionState.Selected => (selectedGradientID, selectedOverrideAlpha, selectedAlphaOverride),
                    _ => (normalGradientID, normalOverrideAlpha, normalAlphaOverride)
                };
            }
        }
        #endregion

        #region Registration
        /// <summary>
        /// Registers a live handler for allocation-free global preset changes.
        /// </summary>
        public static void Register(IStylerHandler handler)
        {
            if (IsMissingHandler(handler))
                return;

            SyncIndices();
            if (handlerIndices.TryAdd(handler, RegisteredHandlers.Count))
                RegisteredHandlers.Add(handler);

            // Unassigned handlers (including generated custom graphics) and kept presets opt out
            if (Application.isPlaying && PersistentPreset != null && handler.Preset != null && handler.Preset != PersistentPreset
                && !handler.Preset.keepOnGlobalApply)
                ApplyPresetTo(PersistentPreset, handler);
        }

        /// <summary>
        /// Removes a handler from the live registry.
        /// </summary>
        public static void Unregister(IStylerHandler handler)
        {
            if (handler == null)
                return;

            SyncIndices();
            // The last handler takes the removed one's place: the list's order carries no meaning
            if (handlerIndices.Remove(handler, out int index))
            {
                if (index < RegisteredHandlers.Count && ReferenceEquals(RegisteredHandlers[index], handler))
                {
                    int last = RegisteredHandlers.Count - 1;
                    if (index != last)
                    {
                        IStylerHandler moved = RegisteredHandlers[last];
                        RegisteredHandlers[index] = moved;
                        handlerIndices[moved] = index;
                    }

                    RegisteredHandlers.RemoveAt(last);
                }
                // Only if the list was changed directly
                else
                {
                    RegisteredHandlers.Remove(handler);
                    SyncIndices(true);
                }
            }

            if (handler is Behaviour owner)
                ColorTransition.Cancel(owner);
        }

        // RegisteredHandlers is public: after code adds to it, removes from it or clears it directly, the positions are
        // rebuilt from the list, so registration behaves as it did when membership was read from the list itself
        static void SyncIndices(bool force = false)
        {
            if (!force && handlerIndices.Count == RegisteredHandlers.Count)
                return;

            handlerIndices.Clear();

            for (int i = 0; i < RegisteredHandlers.Count; i++)
            {
                if (RegisteredHandlers[i] != null)
                    handlerIndices[RegisteredHandlers[i]] = i;
            }
        }

        sealed class HandlerComparer : IEqualityComparer<IStylerHandler>
        {
            public bool Equals(IStylerHandler a, IStylerHandler b) => ReferenceEquals(a, b);
            public int GetHashCode(IStylerHandler handler) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(handler);
        }

        static bool IsMissingHandler(IStylerHandler handler)
        {
            return handler == null || (handler is UnityEngine.Object unityObject && unityObject == null);
        }

        static bool KeepsPreset(IStylerHandler handler, StylerPreset newPreset)
        {
            StylerPreset current = handler.Preset;
            return current != null && current != newPreset && current.keepOnGlobalApply;
        }
        #endregion

        #region Utilities
        internal static Color ApplyAlphaOverride(Color color, bool overrideAlpha, float alpha)
        {
            if (overrideAlpha)
                color.a = alpha;

            return color;
        }

        /// <summary>
        /// Returns the color mapping associated with an interaction state.
        /// </summary>
        public static ColorMapping GetColorMappingForState(InteractionState state, ColorMapping disabled,
            ColorMapping normal, ColorMapping highlighted, ColorMapping pressed, ColorMapping selected)
        {
            return state switch
            {
                InteractionState.Disabled => disabled,
                InteractionState.Normal => normal,
                InteractionState.Highlighted => highlighted,
                InteractionState.Pressed => pressed,
                InteractionState.Selected => selected,
                _ => normal
            };
        }
        #endregion

        #region Preset Items
        /// <summary>
        /// Base data shared by all named preset items.
        /// </summary>
        [Serializable]
        public abstract class Item
        {
            public string itemID;

            /// <summary>
            /// Previous IDs of this item. References that still use one resolve to this item
            /// unless another item currently has that ID.
            /// </summary>
            public List<string> formerIDs = new();

            protected Item(string id = "")
            {
                itemID = id;
            }
        }

        /// <summary>
        /// Used to specify AudioClip values.
        /// </summary>
        [Serializable]
        public class AudioItem : Item
        {
            public AudioClip audioAsset;

            public AudioItem(string id = "", AudioClip asset = null) : base(id)
            {
                audioAsset = asset;
            }
        }

        /// <summary>
        /// Used to specify Color values. 
        /// </summary>
        [Serializable]
        public class ColorItem : Item
        {
            public Color colorValue;

            public ColorItem(string id = "", Color color = default) : base(id)
            {
                colorValue = color;
            }
        }

        /// <summary>
        /// Used to specify font values for each text provider.
        /// </summary>
        [Serializable]
        public class FontItem : Item
        {
            [Tooltip("TextMesh Pro font asset.")]
            public TMP_FontAsset fontAsset;
            [Tooltip("Font assets for other text providers, such as UniText. Each provider uses the first entry it supports.")]
            public List<UnityEngine.Object> providerFonts = new();

            public FontItem(string id = "", TMP_FontAsset asset = null) : base(id)
            {
                fontAsset = asset;
            }

            /// <summary>
            /// Returns the font this item defines for the backend, or null.
            /// </summary>
            public UnityEngine.Object GetFont(TextBackend backend)
            {
                if (backend == null)
                    return null;
                if (backend.IsFont(fontAsset))
                    return fontAsset;

                if (providerFonts != null)
                {
                    for (int i = 0; i < providerFonts.Count; i++)
                    {
                        if (backend.IsFont(providerFonts[i]))
                            return providerFonts[i];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Used to specify Gradient values. 
        /// </summary>
        [Serializable]
        public class GradientItem : Item
        {
            public Gradient gradientValue;

            public GradientItem(string id = "", Gradient gradient = null) : base(id)
            {
                gradientValue = gradient ?? new Gradient();
            }
        }

        /// <summary>
        /// A selectively applied Procedural Shape style. Color and behavior settings remain local.
        /// </summary>
        [Serializable]
        public class ShapeStyleItem : Item
        {
            [UnityEngine.Serialization.FormerlySerializedAs("appliesTo")]
            [Tooltip("Procedural Shape feature groups overridden by this style.")]
            public ShapeStyleFlags flags = ShapeStyleFlags.None;

            // Graphic
            public Sprite sprite;
            public ProceduralShape.ScaleMode scaleMode = ProceduralShape.ScaleMode.Cover;
            [Range(0f, 32f)] public float softness;

            // Shape
            public ProceduralShape.ShapeType shape = ProceduralShape.ShapeType.Rectangle;
            [Range(ProceduralShape.MinPolygonSides, ProceduralShape.MaxPolygonSides)]
            public int polygonSides = 6;
            [Tooltip("Rotates the shape graphic about its center without changing its RectTransform or children.")]
            [Range(0f, 360f)] public float shapeAngle;
            [Tooltip("X/Y skew in degrees, clamped to -80..80. Applied horizontally, then vertically, before rotation.")]
            public Vector2 shapeSkew = Vector2.zero;
            public ProceduralShape.RadiusMode radiusMode = ProceduralShape.RadiusMode.Pixels;
            public bool independentCorners;
            public bool squircleCorners;
            public Vector4 cornerRadius = Vector4.zero;

            // Fill
            public bool fillCenter = true;

            // Outline
            [Min(0f)] public float outlineWidth;

            // Inner Shadow
            public Vector2 innerShadowOffset = Vector2.zero;
            [Min(0f)] public float innerShadowSize;
            [Min(0f)] public float innerShadowSoftness;

            // Outer Shadow
            public Vector2 outerShadowOffset = Vector2.zero;
            [Min(0f)] public float outerShadowSize;
            [Min(0f)] public float outerShadowSoftness;
            public bool outerShadowOutsideOnly;

            public ShapeStyleItem(string id = "") : base(id) { }

            /// <summary>
            /// Applies enabled feature groups and reports whether any stored shape value changed.
            /// Property setters handle validation and dirty state.
            /// </summary>
            internal bool ApplyTo(ProceduralShape target)
            {
                if (target == null || flags == ShapeStyleFlags.None)
                    return false;

                bool changed = false;

                if (Includes(ShapeStyleFlags.Graphic))
                {
                    changed |= SetValue(target, sprite, static t => t.Sprite, static (t, v) => t.Sprite = v);
                    changed |= SetValue(target, scaleMode, static t => t.ImageScaleMode, static (t, v) => t.ImageScaleMode = v);
                    changed |= SetValue(target, softness, static t => t.Softness, static (t, v) => t.Softness = v);
                }

                if (Includes(ShapeStyleFlags.Shape))
                {
                    changed |= SetValue(target, shape, static t => t.Shape, static (t, v) => t.Shape = v);
                    changed |= SetValue(target, polygonSides, static t => t.PolygonSides, static (t, v) => t.PolygonSides = v);
                    changed |= SetValue(target, shapeAngle, static t => t.ShapeAngle, static (t, v) => t.ShapeAngle = v);
                    changed |= SetValue(target, shapeSkew, static t => t.ShapeSkew, static (t, v) => t.ShapeSkew = v);
                    changed |= SetValue(target, radiusMode, static t => t.CornerRadiusMode, static (t, v) => t.CornerRadiusMode = v);
                    changed |= SetValue(target, independentCorners, static t => t.IndependentCorners, static (t, v) => t.IndependentCorners = v);
                    changed |= SetValue(target, squircleCorners, static t => t.SquircleCorners, static (t, v) => t.SquircleCorners = v);
                    changed |= SetValue(target, cornerRadius, static t => t.CornerRadius, static (t, v) => t.CornerRadius = v);
                }

                if (Includes(ShapeStyleFlags.Fill))
                    changed |= SetValue(target, fillCenter, static t => t.FillCenter, static (t, v) => t.FillCenter = v);

                if (Includes(ShapeStyleFlags.Outline))
                    changed |= SetValue(target, outlineWidth, static t => t.OutlineWidth, static (t, v) => t.OutlineWidth = v);

                if (Includes(ShapeStyleFlags.InnerShadow))
                {
                    changed |= SetValue(target, innerShadowOffset, static t => t.InnerShadowOffset, static (t, v) => t.InnerShadowOffset = v);
                    changed |= SetValue(target, innerShadowSize, static t => t.InnerShadowSize, static (t, v) => t.InnerShadowSize = v);
                    changed |= SetValue(target, innerShadowSoftness, static t => t.InnerShadowSoftness, static (t, v) => t.InnerShadowSoftness = v);
                }

                if (Includes(ShapeStyleFlags.OuterShadow))
                {
                    changed |= SetValue(target, outerShadowOffset, static t => t.OuterShadowOffset, static (t, v) => t.OuterShadowOffset = v);
                    changed |= SetValue(target, outerShadowSize, static t => t.OuterShadowSize, static (t, v) => t.OuterShadowSize = v);
                    changed |= SetValue(target, outerShadowSoftness, static t => t.OuterShadowSoftness, static (t, v) => t.OuterShadowSoftness = v);
                    changed |= SetValue(target, outerShadowOutsideOnly, static t => t.OuterShadowOutsideOnly, static (t, v) => t.OuterShadowOutsideOnly = v);
                }

                return changed;
            }

            bool Includes(ShapeStyleFlags feature) => (flags & feature) != 0;

            // Static callbacks are cached per call site and never capture a target instance.
            static bool SetValue<T>(ProceduralShape target, T value, Func<ProceduralShape, T> get, Action<ProceduralShape, T> set)
            {
                T previous = get(target);
                if (EqualityComparer<T>.Default.Equals(previous, value))
                    return false;

                set(target, value);
                // Read back the validated value: clamping or wrapping may leave it unchanged.
                return !EqualityComparer<T>.Default.Equals(previous, get(target));
            }
        }

        /// <summary>
        /// Used to specify Sprite values and optional Image Component overrides. 
        /// </summary>
        [Serializable]
        public class SpriteItem : Item
        {
            public Sprite spriteAsset;

            [Tooltip("Override Image component settings when applying this sprite.")]
            public bool applyImageSettings = false;
            public Image.Type imageType = Image.Type.Simple;
            public bool preserveAspect = false;
            public float pixelsPerUnitMultiplier = 1f;
            public Image.FillMethod fillMethod = Image.FillMethod.Radial360;
            public int fillOrigin = 0;
            public float fillAmount = 1f;
            public bool fillClockwise = true;

            public SpriteItem(string id = "", Sprite asset = null) : base(id)
            {
                spriteAsset = asset;
            }
        }

        /// <summary>
        /// Used to specify Text Style values and overrides.
        /// </summary>
        [Serializable]
        public class TextStyleItem : Item
        {
            [Tooltip("Text feature groups overridden by this style.")]
            public TextStyleFlags flags = TextStyleFlags.None;

            // Size
            [Min(0f)] public float fontSize = 36f;
            public bool enableAutoSizing = false;
            [Min(0f)] public float fontSizeMin = 18f;
            [Min(0f)] public float fontSizeMax = 72f;
            public float characterWidthAdjustment = 0f;
            public float lineSpacingAdjustment = 0f;

            // Alignment
            public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;

            // Wrapping and Overflow
            public bool enableWordWrapping = true;
            public TextOverflowModes overflowMode = TextOverflowModes.Overflow;

            // Spacing
            public float characterSpacing = 0f;
            public float wordSpacing = 0f;
            public float lineSpacing = 0f;
            public float paragraphSpacing = 0f;

            // Margin
            public Vector4 margin = Vector4.zero;

            // Font Style
            public FontStyles fontStyle = FontStyles.Normal;

            public TextStyleItem(string id = "") : base(id) { }
        }
        #endregion

        #region Preset Getters
        /// <summary>
        /// Keeps a preset locked to the default in sync. Edit mode follows the current default,
        /// while play mode only fills a missing preset so runtime changes stay applied.
        /// </summary>
        internal static void SyncDefaultPreset(ref StylerPreset preset, bool useDefaultPreset)
        {
            if (useDefaultPreset && (!Application.isPlaying || preset == null))
                preset = GetDefaultPreset(false);
        }

        /// <summary>
        /// Keeps a preset locked to the default in sync while the provider is Styler.
        /// </summary>
        internal static void SyncDefaultPreset(ref StylerPreset preset, StyleProvider provider, bool useDefaultPreset)
            => SyncDefaultPreset(ref preset, provider == StyleProvider.Styler && useDefaultPreset);

        /// <summary>
        /// Gets the default preset. 
        /// Prioritizes the preset defined in the config file. Falls back to default.
        /// </summary>
        public static StylerPreset GetDefaultPreset(bool generateLog = true, bool bypassCache = false)
        {
            if (!bypassCache && CachedDefaultPreset != null)
                return CachedDefaultPreset;

            // Try to load from config
            TextAsset config = Resources.Load<TextAsset>(Constants.StylerConfigPath);
            if (config != null && !string.IsNullOrEmpty(config.text))
            {
                string path = config.text.Trim();
                StylerPreset preset = Resources.Load<StylerPreset>(path);
                if (preset != null)
                {
                    CachedDefaultPreset = preset;
                    return CachedDefaultPreset;
                }
            }

            // Try the expected default path as a fallback
            StylerPreset defaultPreset = Resources.Load<StylerPreset>(Constants.StylerFallbackPath);
            if (defaultPreset != null)
            {
                CachedDefaultPreset = defaultPreset;
                return CachedDefaultPreset;
            }

            if (generateLog)
                Debug.LogWarning($"[Styler] No default preset found.");

            return null;
        }

        /// <summary>
        /// Get an audio clip from the provided mapping based on the specified source.
        /// </summary>
        public static AudioClip GetAudio(StyleProvider provider, AudioMapping mapping, StylerPreset preset)
        {
            if (provider != StyleProvider.Styler)
                return mapping.audioClip;
            if (preset != null && preset.TryGetAudio(mapping.stylerID, out var clip))
                return mapping.Remember(clip);

            return mapping.LastValueOr(mapping.audioClip);
        }

        /// <summary>
        /// Get a color from the provided mapping based on the specified source.
        /// </summary>
        public static Color GetColor(StyleProvider provider, ColorMapping mapping, StylerPreset preset)
        {
            if (mapping == null)
                return Color.clear;
            if (provider != StyleProvider.Styler)
                return mapping.color;
            if (string.IsNullOrEmpty(mapping.stylerID))
                return mapping.Remember(Color.clear);
            if (preset != null && preset.TryGetColor(mapping.stylerID, out var color))
                return mapping.Remember(ApplyAlphaOverride(color, mapping.overrideAlpha, mapping.alphaOverride));

            return mapping.LastValueOr(ApplyAlphaOverride(mapping.color, mapping.overrideAlpha, mapping.alphaOverride));
        }

        /// <summary>
        /// Get a font for the text backend from the provided mapping based on the specified source.
        /// Returns null when no font of the backend's type resolves.
        /// </summary>
        public static UnityEngine.Object GetFont(StyleProvider provider, FontMapping mapping, StylerPreset preset, TextBackend backend)
        {
            if (mapping == null || backend == null)
                return null;

            UnityEngine.Object font = mapping.font;
            if (provider == StyleProvider.Styler)
            {
                if (preset != null && preset.TryGetFont(mapping.stylerID, backend, out var presetFont))
                    return mapping.Remember(presetFont);

                font = mapping.LastValueOr(font);
            }

            return backend.IsFont(font) ? font : null;
        }

        /// <summary>
        /// Get a gradient from the provided mapping based on the specified source.
        /// </summary>
        public static Gradient GetGradient(StyleProvider provider, GradientMapping mapping, StylerPreset preset)
        {
            if (provider != StyleProvider.Styler)
                return mapping.gradient;
            if (preset != null && preset.TryGetGradient(mapping.stylerID, out var gradient))
                return mapping.Remember(gradient);

            return mapping.LastValueOr(mapping.gradient);
        }

        /// <summary>
        /// Get a sprite from the provided mapping based on the specified source.
        /// </summary>
        public static Sprite GetSprite(StyleProvider provider, SpriteMapping mapping, StylerPreset preset)
        {
            if (provider != StyleProvider.Styler)
                return mapping.sprite;
            if (preset != null && preset.TryGetSprite(mapping.stylerID, out var sprite))
                return mapping.Remember(sprite);

            return mapping.LastValueOr(mapping.sprite);
        }
        #endregion

        #region Preset Application
        /// <summary>
        /// Applies to registered handlers. Persistent changes also apply when assigned handlers register later.
        /// </summary>
        public static void ApplyPreset(StylerPreset newPreset, bool isPersistent = true, float transitionDuration = 0f)
        {
            if (newPreset == null)
                return;

            if (isPersistent && Application.isPlaying)
                PersistentPreset = newPreset;

            var snapshot = ListPool<IStylerHandler>.Get();
            snapshot.AddRange(RegisteredHandlers);

            try
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var handler = snapshot[i];
                    if (IsMissingHandler(handler))
                        Unregister(handler);
                    else if (!KeepsPreset(handler, newPreset))
                        ApplyPresetTo(newPreset, handler, transitionDuration);
                }
            }
            finally
            {
                ListPool<IStylerHandler>.Release(snapshot);
            }
        }

        /// <summary>
        /// Applies to one handler without changing the persistent preset.
        /// </summary>
        public static void ApplyPresetTo(StylerPreset newPreset, IStylerHandler target, float transitionDuration = 0f)
        {
            if (newPreset == null || IsMissingHandler(target))
                return;

            using (ColorTransition.Begin(target as Behaviour, transitionDuration))
            {
                if (target.Preset == newPreset)
                    target.UpdateStyler();
                else
                    target.Preset = newPreset;
            }
        }

        /// <summary>
        /// Applies to a specific object and optionally its children, including inactive children.
        /// </summary>
        public static void ApplyPresetTo(StylerPreset newPreset, GameObject target, bool includeChildren = true,
            float transitionDuration = 0f)
        {
            if (newPreset == null || target == null)
                return;

            var buffer = ListPool<IStylerHandler>.Get();
            if (includeChildren)
                target.GetComponentsInChildren(true, buffer);
            else
                target.GetComponents(buffer);

            try
            {
                for (int i = 0; i < buffer.Count; i++)
                    ApplyPresetTo(newPreset, buffer[i], transitionDuration);
            }
            finally
            {
                ListPool<IStylerHandler>.Release(buffer);
            }
        }
        #endregion

        #region Obsolete API
        [Obsolete("Use GetFont(provider, mapping, preset, backend) to support every text provider.")]
        public static TMP_FontAsset GetFont(StyleProvider provider, FontMapping mapping, StylerPreset preset)
            => GetFont(provider, mapping, preset, TMPTextBackend.Instance) as TMP_FontAsset;

        [Obsolete("Use ApplyPreset(newPreset, isPersistent) instead.")]
        public static void ApplyPresetToRegisteredObjects(StylerPreset newPreset, bool updateCache = true)
        {
            if (updateCache)
                SetLegacyDefaultPreset(newPreset);

            ApplyPreset(newPreset, updateCache);
        }

        [Obsolete("Use PersistentPreset to choose a runtime preset, or configure the default preset in Styler Browser.")]
        public static void UpdateCachedDefaultPreset(StylerPreset newDefaultPreset) => SetLegacyDefaultPreset(newDefaultPreset);

        static void SetLegacyDefaultPreset(StylerPreset preset)
        {
            if (preset != null)
                CachedDefaultPreset = preset;
        }

        [Obsolete("Use ApplyPresetTo(GetDefaultPreset(), target) instead.")]
        public static void ApplyDefaultPresetTo(IStylerHandler target) => ApplyPresetTo(GetDefaultPreset(), target);

        [Obsolete("Use ApplyPresetTo(GetDefaultPreset(), target, includeChildren) instead.")]
        public static void ApplyDefaultPresetTo(GameObject target, bool includeChildren = true)
            => ApplyPresetTo(GetDefaultPreset(), target, includeChildren);
        #endregion
    }

    /// <summary>
    /// Interface to implement Styler to classes.
    /// </summary>
    public interface IStylerHandler
    {
        /// <summary>
        /// Points to the local variable.
        /// </summary>
        StylerPreset Preset { get; set; }

        /// <summary>
        /// The method to run when the preset changes.
        /// </summary>
        void UpdateStyler();
    }

    /// <summary>
    /// Interface for Selectables to broadcast their state to StylerObject.
    /// </summary>
    public interface IStylerInteractable
    {
        /// <summary>
        /// Gets the transition duration used for animating state changes.
        /// </summary>
        float TransitionDuration { get; }

        /// <summary>
        /// Gets the current interaction state of this UI element.
        /// </summary>
        InteractionState InteractionState { get; }

        /// <summary>
        /// Event triggered when the interaction state of the element changes.
        /// </summary>
        event Action<InteractionState> OnStateChanged;
    }
}