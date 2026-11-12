using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Styler/Styler Object"), HelpURL(Constants.HelpUrl + "styler")]
    public class StylerObject : MonoBehaviour, IStylerHandler
    {
        // References
        [UnityEngine.Serialization.FormerlySerializedAs("presetSource")]
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = true;
        [Tooltip("The preset containing style definitions.")]
        [SerializeField] private StylerPreset preset;
        [UnityEngine.Serialization.FormerlySerializedAs("targetImage")]
        [Tooltip("Graphic component to style")]
        public Graphic targetGraphic;
        [Tooltip("Text component to style.")]
        [TextComponent] public Graphic targetText;
        [Tooltip("ImageGradient component to style.")]
        public ImageGradient targetGradient;

        // Settings
        [Tooltip("Type of UI object to style.")]
        public ObjectType objectType = ObjectType.Graphic;
        public string colorID = "Primary";
        public string fontID = "Regular";
        public string textStyleID = "";
        public string gradientID = "";
        public string spriteID = "";
        [Tooltip("Set a current color instead of getting from the preset.")]
        public bool useCustomColor = false;
        [Tooltip("Override the alpha channel with a custom value.")]
        public bool overrideAlpha = false;
        [Tooltip("Alpha value to apply (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)] public float alphaOverride = 1f;

        // Interaction
        [Tooltip("Enable state-based interactions.")]
        public bool enableInteraction = false;
        public Selectable interactableObject;

        [Tooltip("Enable state-based color animation.")]
        public bool enableColorInteraction = true;
        public ColorMapping disabledColor = new() { stylerID = "Primary" };
        public ColorMapping normalColor = new() { stylerID = "Primary" };
        public ColorMapping highlightedColor = new() { stylerID = "Primary" };
        public ColorMapping pressedColor = new() { stylerID = "Primary" };
        public ColorMapping selectedColor = new() { stylerID = "Primary" };

        [Tooltip("Enable state-based font swapping.")]
        public bool enableFontInteraction = false;
        public FontMapping disabledFont = new() { stylerID = "Regular" };
        public FontMapping normalFont = new() { stylerID = "Regular" };
        public FontMapping highlightedFont = new() { stylerID = "Regular" };
        public FontMapping pressedFont = new() { stylerID = "Regular" };
        public FontMapping selectedFont = new() { stylerID = "Regular" };

        // IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value)
                    return;

                preset = value;
                UpdateStyler();
            }
        }

        // Enums
        public enum ObjectType
        {
            [Tooltip("Sets the color of the Graphic variable.")]
            Graphic = 0,

            [Tooltip("Sets the font, text style and color of the text variable.")]
            Text = 1,

            [Tooltip("Sets the sprite and color of the Image variable.")]
            Image = 2,

            [Tooltip("Sets the gradient of the ImageGradient variable.")]
            Gradient = 3
        }

        // Cache
        readonly List<string> cachedColorIDs = new();
        readonly List<string> cachedFontIDs = new();
        readonly List<string> cachedTextStyleIDs = new();
        readonly List<string> cachedGradientIDs = new();
        readonly List<string> cachedSpriteIDs = new();

        // State
        InteractionState currentState;
        IStylerInteractable interactableInterface;

        void OnEnable()
        {
            Styler.SyncDefaultPreset(ref preset, useDefaultStylerPreset);
            Styler.Register(this);
            UpdateStyler();
        }

        void OnDisable()
        {
            UnsubscribeInteraction();
            Styler.Unregister(this);

            if (currentState != InteractionState.Selected && currentState != InteractionState.Disabled)
                currentState = InteractionState.Normal;
        }

        void UnsubscribeInteraction()
        {
            if (interactableInterface != null)
                interactableInterface.OnStateChanged -= OnInteractableStateChanged;

            interactableInterface = null;
        }

        void RefreshInteractionSubscription()
        {
            var next = Application.isPlaying && isActiveAndEnabled && enableInteraction && interactableObject != null
                ? interactableObject as IStylerInteractable 
                : null;

            if (ReferenceEquals(interactableInterface, next))
                return;

            UnsubscribeInteraction();
            interactableInterface = next;

            if (interactableInterface != null)
            {
                interactableInterface.OnStateChanged += OnInteractableStateChanged;
                currentState = interactableInterface.InteractionState;
            }
        }

        void ResolveComponents()
        {
            switch (objectType)
            {
                case ObjectType.Graphic:
                case ObjectType.Image:
                default:
                    if (targetGraphic == null) { TryGetComponent(out targetGraphic); }
                    break;
                case ObjectType.Text:
                    if (targetText == null) { TextProvider.TryGetText(this, out targetText); }
                    break;
                case ObjectType.Gradient:
                    if (targetGradient == null) { TryGetComponent(out targetGradient); }
                    if (targetGraphic == null && targetGradient != null) { targetGradient.TryGetComponent(out targetGraphic); }
                    break;
            }
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (currentState == newState || !isActiveAndEnabled || !enableInteraction || objectType == ObjectType.Gradient)
                return;

            currentState = newState;
            AnimateToState(newState);
        }

        void AnimateToState(InteractionState state)
        {
            if (objectType == ObjectType.Gradient)
                return;
            if ((objectType == ObjectType.Text && targetText == null) ||
                ((objectType == ObjectType.Graphic || objectType == ObjectType.Image) && targetGraphic == null))
                return;

            // Handle Font instantly (no crossfade for fonts)
            if (enableFontInteraction && objectType == ObjectType.Text)
            {
                TextBackend backend = TextProvider.Find(targetText);
                if (backend != null)
                {
                    Object tFont = GetInteractionFont(backend, state, backend.GetFont(targetText));
                    if (tFont != null)
                        backend.SetFont(targetText, tFont);
                }
            }

            // Handle Color transition
            if (enableColorInteraction)
            {
                float tDuration = Mathf.Max(0f, interactableObject is IStylerInteractable interactable ? interactable.TransitionDuration : 0f);
                Graphic tGraphic = objectType == ObjectType.Text ? targetText : targetGraphic;
                Color tColor = GetInteractionColor(state, tGraphic.color);
                using (ColorTransition.Begin(this, tDuration, easeOut: true))
                    ColorTransition.SetGraphicColor(tGraphic, tColor);
            }
        }

        Object GetTargetFont(TextBackend backend, Object currentFont)
        {
            if (enableInteraction && enableFontInteraction && interactableObject is IStylerInteractable interactable)
                return GetInteractionFont(backend, interactable.InteractionState, currentFont);
            if (string.IsNullOrEmpty(fontID) || preset == null)
                return currentFont;
            if (preset.TryGetFont(fontID, backend, out var baseFont))
                return baseFont;

            return currentFont;
        }

        Color GetTargetColor(Color currentColor)
        {
            if (enableInteraction && enableColorInteraction && interactableObject is IStylerInteractable interactable)
                return GetInteractionColor(interactable.InteractionState, currentColor);
            if (string.IsNullOrEmpty(colorID))
                return Color.clear;
            if (preset != null && preset.TryGetColor(colorID, out Color baseColor))
                return Styler.ApplyAlphaOverride(baseColor, overrideAlpha, alphaOverride);

            return currentColor;
        }

        Object GetInteractionFont(TextBackend backend, InteractionState state, Object currentFont)
        {
            FontMapping mapping = GetFontMappingForState(state);
            if (mapping == null || (preset != null && string.IsNullOrEmpty(mapping.stylerID)))
                return currentFont;

            return Styler.GetFont(StyleProvider.Styler, mapping, preset, backend);
        }

        Color GetInteractionColor(InteractionState state, Color currentColor)
        {
            ColorMapping mapping = Styler.GetColorMappingForState(state, disabledColor, normalColor,
                highlightedColor, pressedColor, selectedColor);
            if (mapping == null)
                return currentColor;

            StyleProvider provider = !useCustomColor ? StyleProvider.Styler : StyleProvider.Custom;
            return Styler.GetColor(provider, mapping, preset);
        }

        FontMapping GetFontMappingForState(InteractionState state)
        {
            return state switch
            {
                InteractionState.Disabled => disabledFont,
                InteractionState.Normal => normalFont,
                InteractionState.Highlighted => highlightedFont,
                InteractionState.Pressed => pressedFont,
                InteractionState.Selected => selectedFont,
                _ => null
            };
        }

        void ApplySpriteSettings(Image targetImage, Styler.SpriteItem item)
        {
            if (targetImage == null)
                return;

            Sprite targetSprite = item?.spriteAsset;

            if (targetImage.sprite != targetSprite)
                targetImage.sprite = targetSprite;

            if (item != null && item.applyImageSettings)
            {
                if (targetImage.type != item.imageType)
                    targetImage.type = item.imageType;
               
                if (targetImage.preserveAspect != item.preserveAspect)
                    targetImage.preserveAspect = item.preserveAspect;

                if (targetImage.pixelsPerUnitMultiplier != item.pixelsPerUnitMultiplier)
                    targetImage.pixelsPerUnitMultiplier = item.pixelsPerUnitMultiplier;

                if (item.imageType == Image.Type.Filled)
                {
                    if (targetImage.fillMethod != item.fillMethod)
                        targetImage.fillMethod = item.fillMethod;
                    
                    if (targetImage.fillOrigin != item.fillOrigin)
                        targetImage.fillOrigin = item.fillOrigin;

                    if (targetImage.fillAmount != item.fillAmount)
                        targetImage.fillAmount = item.fillAmount;

                    if (targetImage.fillClockwise != item.fillClockwise)
                        targetImage.fillClockwise = item.fillClockwise;
                }
            }
        }

        /// <summary>
        /// Force-updates the styler object. Useful to call when any of its parameters are changed.
        /// </summary>
        public void UpdateStyler()
        {
            Styler.SyncDefaultPreset(ref preset, useDefaultStylerPreset);

            ResolveComponents();
            RefreshInteractionSubscription();

            if (targetGraphic == null && targetText == null && targetGradient == null)
                return;

            // Handle Gradient completely independently
            if (objectType == ObjectType.Gradient)
            {
                if (preset != null && targetGradient != null && preset.TryGetGradient(gradientID, out var grad))
                {
#if UNITY_EDITOR
                    // Compare first so re-applying an unchanged preset doesn't dirty the scene
                    bool gradientChanged = !Application.isPlaying && !grad.Equals(targetGradient.gradientEffect);
#endif
                    // The target owns its copy and refreshes its mesh when updated
                    targetGradient.TransitionTo(grad);

#if UNITY_EDITOR
                    // Tell Unity the object changed so the Inspector visually updates the gradient block
                    if (gradientChanged)
                        UnityEditor.EditorUtility.SetDirty(targetGradient);
#endif
                }

                // Gradients don't process standard colors or interaction
                return;
            }

            // Handle Fonts, Text Styles & Sprites
            if (objectType == ObjectType.Text && TextProvider.Find(targetText) is TextBackend backend)
            {
                Object targetFont = GetTargetFont(backend, backend.GetFont(targetText));
                if (targetFont != null)
                    backend.SetFont(targetText, targetFont);
                if (!string.IsNullOrEmpty(textStyleID) && preset != null && preset.TryGetTextStyleItem(textStyleID, out var textStyleItem))
                    backend.ApplyTextStyle(targetText, textStyleItem);
            }
            else if (preset != null && objectType == ObjectType.Image && targetGraphic is Image targetImage)
            {
                if (!string.IsNullOrEmpty(spriteID) && preset.TryGetSpriteItem(spriteID, out var spriteItem))
                    ApplySpriteSettings(targetImage, spriteItem);
            }

            // Handle Colors
            if (useCustomColor && (!enableInteraction || !enableColorInteraction))
                return; // User is manually managing the graphic's color

            Color currentColor = objectType == ObjectType.Text && targetText != null
                ? targetText.color
                : (targetGraphic != null ? targetGraphic.color : Color.white);

            ColorTransition.SetGraphicColor(objectType == ObjectType.Text && targetText != null
                ? targetText 
                : targetGraphic, GetTargetColor(currentColor));
        }

        public List<string> GetAvailableColorIDs()
        {
            cachedColorIDs.Clear();

            if (preset == null)
                return cachedColorIDs;

            for (int i = 0; i < preset.colorItems.Count; i++)
                cachedColorIDs.Add(preset.colorItems[i].itemID);

            return cachedColorIDs;
        }

        public List<string> GetAvailableFontIDs()
        {
            cachedFontIDs.Clear();

            if (preset == null)
                return cachedFontIDs;

            for (int i = 0; i < preset.fontItems.Count; i++)
                cachedFontIDs.Add(preset.fontItems[i].itemID);

            return cachedFontIDs;
        }

        public List<string> GetAvailableTextStyleIDs()
        {
            cachedTextStyleIDs.Clear();

            if (preset == null)
                return cachedTextStyleIDs;

            for (int i = 0; i < preset.textStyleItems.Count; i++)
                cachedTextStyleIDs.Add(preset.textStyleItems[i].itemID);

            return cachedTextStyleIDs;
        }

        public List<string> GetAvailableSpriteIDs()
        {
            cachedSpriteIDs.Clear();

            if (preset == null)
                return cachedSpriteIDs;

            for (int i = 0; i < preset.spriteItems.Count; i++)
                cachedSpriteIDs.Add(preset.spriteItems[i].itemID);

            return cachedSpriteIDs;
        }

        public List<string> GetAvailableGradientIDs()
        {
            cachedGradientIDs.Clear();

            if (preset == null)
                return cachedGradientIDs;

            for (int i = 0; i < preset.gradientItems.Count; i++)
                cachedGradientIDs.Add(preset.gradientItems[i].itemID);

            return cachedGradientIDs;
        }

        #region Obsolete API
        [System.Obsolete("Use UpdateStyler() instead.")]
        public void UpdateStyle() => UpdateStyler();

        [System.Obsolete("Use targetGraphic instead.")]
        public Image targetImage { get => targetGraphic as Image; set => targetGraphic = value; }

        [System.Obsolete("Use useDefaultStylerPreset instead.")]
        public PresetSource presetSource
        {
            get => useDefaultStylerPreset ? PresetSource.Default : PresetSource.UserDefined;
            set => useDefaultStylerPreset = value == PresetSource.Default;
        }

        [System.Obsolete("Use useDefaultStylerPreset instead.")]
        public enum PresetSource
        {
            UserDefined = 0,
            Default = 1
        }
        #endregion

#if UNITY_EDITOR
        void Reset()
        {
            ResolveComponents();
            preset = Styler.GetDefaultPreset(false);

            if (preset != null)
            {
                if (preset.fontItems.Count > 0 && preset.fontItems[0] != null)
                    fontID = preset.fontItems[0].itemID;

                if (preset.textStyleItems.Count > 0 && preset.textStyleItems[0] != null)
                    textStyleID = preset.textStyleItems[0].itemID;

                if (preset.colorItems.Count > 0 && preset.colorItems[0] != null)
                    colorID = preset.colorItems[0].itemID;
            }
        }

        void OnValidate()
        {
            if (!enabled)
                return;

            UpdateStyler();
        }
#endif
    }
}