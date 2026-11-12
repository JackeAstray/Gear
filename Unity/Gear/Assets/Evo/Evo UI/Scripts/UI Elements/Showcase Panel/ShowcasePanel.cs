using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, ExecuteAlways]
    [AddComponentMenu("Evo/UI/UI Elements/Showcase Panel"), HelpURL(Constants.HelpUrl + "ui-elements/showcase-panel")]
    public class ShowcasePanel : MonoBehaviour
    {
        // Content
        [SerializeField] private int currentIndex;
        public List<Item> items = new();

        // Settings
        public bool useUnscaledTime = false;
        public bool setWithTimer = true;
        [Range(1f, 30f)] public float timer = 3f;
        [Min(0.05f)] public float animationDuration = 0.25f;
        public Vector2 slideOffset = new(0f, 15f);

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        public Transform buttonParent;
        public GameObject buttonPreset;
        [TextComponent] public Graphic textDisplay;
        [Tooltip("Takes priority over Background Image when assigned.")]
        public ProceduralShape backgroundShape;
        [Tooltip("Takes priority over Background Shadow when assigned. Item shadow colors drive its Inner Shadow color.")]
        public ProceduralShape shadowShape;
        public Image backgroundImage;
        public Image backgroundShadow;

        // Serialized Ownership
        [SerializeField, HideInInspector] private List<GameObject> generatedObjects = new();
        [SerializeField, HideInInspector] private GameObject generatedButtonPreset;

        // Cache
        static readonly GradientColorKey[] shadowColorKeys = new GradientColorKey[2];
        static readonly GradientAlphaKey[] shadowAlphaKeys = new GradientAlphaKey[2];
        readonly List<Item> boundItems = new();
        readonly GeneratedNames generatedNames = new();
        RectTransform textRect;
        CanvasGroup textCanvasGroup;
        CanvasGroup backgroundCanvasGroup;
        Vector2 textPosition;
        Vector2 animationStartPosition;
        Vector2 animationEndPosition;
        float animationStartAlpha;
        float backgroundStartAlpha;
        TweenHandle contentTween;
        TweenHandle shadowTween;
        Action<float> animateContent;
        Action<Vector4> animateShadow;
        Action completeOut;

        // State
        int displayedIndex = -1;
        int hoveredIndex = -1;
        float timerCount;
        bool isInitialized;
        bool isAnimatingOut;

        // Properties
        TimeMode AnimationTimeMode => useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled;
        Graphic BackgroundGraphic => backgroundShape != null ? backgroundShape : backgroundImage;
        bool HasShadow => shadowShape != null || backgroundShadow != null;
        bool HasShadowGradient => shadowShape != null && shadowShape.fillColorMode == ProceduralShape.ColorMode.Gradient;
        Color ShadowColor
        {
            get => HasShadowGradient ? shadowShape.FillGradient.Evaluate(0f)
                : shadowShape != null ? shadowShape.FillColor : backgroundShadow != null ? backgroundShadow.color : Color.clear;
            set
            {
                if (HasShadowGradient) { SetShadowGradient(value); }
                else if (shadowShape != null) { shadowShape.FillColor = value; }
                else if (backgroundShadow != null) { backgroundShadow.color = value; }
            }
        }

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
            {
#if UNITY_EDITOR
                // Unity skips OnValidate for objects created in the editor, such as menu copies
                OnValidate();
#endif
                return;
            }

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization, refreshImmediately: false);
#endif
            Initialize();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
            refreshPending = false;
#endif
            if (isInitialized)
                StopAnimations();
            UnbindItems();
            hoveredIndex = -1;
            isInitialized = false;

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
        }

        void Update()
        {
            if (!isInitialized)
                return;

            if (hoveredIndex >= 0)
            {
                Button hoveredButton = IsValidIndex(hoveredIndex) ? items[hoveredIndex].button : null;
                if (hoveredButton != null && hoveredButton.isActiveAndEnabled && hoveredButton.IsInteractable())
                    return;

                hoveredIndex = -1;
            }

            if (!setWithTimer || contentTween.IsActive || !IsValidIndex(currentIndex))
                return;

            timerCount += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (timerCount < Mathf.Max(0f, timer))
                return;

            timerCount = 0f;
            SetItem(FindItem(currentIndex + 1));
        }

        /// <summary>
        /// Reuses prepared objects, repairs missing setup, and plays the entrance animation.
        /// </summary>
        public void Initialize()
        {
            Refresh();

            if (isInitialized && IsValidIndex(currentIndex))
                PlayInAnimation();
        }

        #region Types
        [Serializable]
        public class Item
        {
            public string title = "Item Title";
            public string url;
            public Sprite icon;
            public Sprite background;
            public Color shadowColor = Color.clear;
            [TextArea(2, 4)] public string description = "Item description";
            public UnityEvent onClick = new();
            [HideInInspector] public Button button;

#if EVO_LOCALIZATION
            public string titleKey;
            public string descriptionKey;
#endif

            ShowcasePanel owner;
            Button boundButton;
            int index;

            internal void Bind(ShowcasePanel panel, int itemIndex)
            {
                index = itemIndex;

                // Items that keep their button stay bound, so a refresh doesn't register listeners again
                if (owner == panel && boundButton == button)
                    return;

                Unbind();
                owner = panel;
                boundButton = button;
                boundButton.onClick.AddListener(OnClick);
                boundButton.onPointerEnter.AddListener(OnPointerEnter);
                boundButton.onPointerExit.AddListener(OnPointerExit);
            }

            internal void Unbind()
            {
                if (boundButton != null)
                {
                    boundButton.onClick.RemoveListener(OnClick);
                    boundButton.onPointerEnter.RemoveListener(OnPointerEnter);
                    boundButton.onPointerExit.RemoveListener(OnPointerExit);
                }

                owner = null;
                boundButton = null;
            }

            bool IsBound => owner != null && owner.isInitialized && owner.IsValidIndex(index) && owner.items[index] == this;

            void OnClick()
            {
                if (!IsBound)
                    return;

                string targetUrl = url;
                onClick?.Invoke();

                if (!string.IsNullOrEmpty(targetUrl))
                    Application.OpenURL(targetUrl);
            }

            void OnPointerEnter()
            {
                if (!IsBound)
                    return;

                owner.hoveredIndex = index;
                owner.SetItem(index);
            }

            void OnPointerExit()
            {
                if (!IsBound || owner.hoveredIndex != index)
                    return;

                owner.hoveredIndex = -1;
                owner.timerCount = 0f;
                owner.HighlightCurrentButton();
            }
        }
        #endregion

        #region Setup
        bool RefreshButtons(bool runtime)
        {
            bool changed = false;

            // Adopt the original preview buttons once; non-button children are left alone.
            if (generatedButtonPreset == null && generatedObjects.Count == 0)
            {
                for (int i = 0; i < buttonParent.childCount; i++)
                {
                    Transform child = buttonParent.GetChild(i);
                    if (child.gameObject != buttonPreset && child.TryGetComponent<Button>(out _))
                        generatedObjects.Add(child.gameObject);
                }
                changed |= generatedObjects.Count > 0;
            }

            bool presetChanged = generatedButtonPreset != null && generatedButtonPreset != buttonPreset;
            changed |= generatedButtonPreset != buttonPreset || generatedObjects.Count != items.Count;
            generatedButtonPreset = buttonPreset;
            string linkPath = items.Count > 0 ? GeneratedObjects.GetLinkPath(this, buttonPreset) : null;

            for (int i = generatedObjects.Count - 1; i >= 0; i--)
            {
                GameObject instance = generatedObjects[i];
                if (presetChanged || i >= items.Count || items[i] == null || instance == null || !instance.TryGetComponent<Button>(out _)
                    || GeneratedObjects.NeedsRelink(instance, buttonPreset, linkPath))
                {
                    changed |= instance != null;
                    DestroyGeneratedObject(instance);
                    generatedObjects[i] = null;
                }
            }

            if (generatedObjects.Count > items.Count)
                generatedObjects.RemoveRange(items.Count, generatedObjects.Count - items.Count);
            while (generatedObjects.Count < items.Count)
                generatedObjects.Add(null);

            if (linkPath != null)
                changed |= GeneratedObjects.ClaimOrphans(generatedObjects, items.Count, buttonParent, buttonPreset, linkPath, DestroyGeneratedObject);

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                GameObject instance = generatedObjects[i];
                if (instance == null)
                {
                    generatedObjects[i] = instance = GeneratedObjects.Create(buttonPreset, buttonParent, linkPath);
                    changed = true;
                }
                else if (instance.transform.parent != buttonParent)
                {
                    instance.transform.SetParent(buttonParent, false);
                    changed = true;
                }

                string title = item.title ?? string.Empty;
                changed |= generatedNames.Apply(i, instance, title);

                if (!instance.activeSelf)
                {
                    instance.SetActive(true);
                    changed = true;
                }

                if (instance.transform.GetSiblingIndex() != i)
                {
                    instance.transform.SetSiblingIndex(i);
                    changed = true;
                }

                if (item.button == null || item.button.gameObject != instance)
                {
                    item.button = instance.GetComponent<Button>();
                    changed = true;
                }

#if EVO_LOCALIZATION
                if (item.button.enableLocalization)
                {
                    item.button.enableLocalization = false;
                    changed = true;
                }
#endif
                // Icon-only buttons have no text object, so Text always reads empty
                if (item.button.textObject != null && item.button.Text != title)
                {
                    item.button.Text = title;
                    changed = true;
                }

                // Visibility can lag behind the sprite, e.g. when the sprite was assigned while hidden
                if (item.button.Icon != item.icon || (item.button.imageObject != null && item.button.EnableIcon != (item.icon != null)))
                {
                    item.button.Icon = item.icon;
                    changed = true;
                }

                if (runtime && isActiveAndEnabled)
                {
                    item.Bind(this, i);
                    boundItems.Add(item);
                }
            }
            return changed;
        }

        static void DestroyGeneratedObject(GameObject instance)
        {
            if (instance == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(instance); }
            else
            {
                instance.SetActive(false);
                Destroy(instance);
            }
        }

        void SetupAnimation()
        {
            textRect = textDisplay != null ? textDisplay.rectTransform : null;
            textPosition = textRect != null ? textRect.anchoredPosition : Vector2.zero;
            textCanvasGroup = GetCanvasGroup(textDisplay);
            backgroundCanvasGroup = GetCanvasGroup(BackgroundGraphic);
        }

        static CanvasGroup GetCanvasGroup(Component target)
        {
            if (target == null)
                return null;

            return target.TryGetComponent<CanvasGroup>(out var group) ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        void UnbindItems(bool keepListed = false)
        {
            for (int i = 0; i < boundItems.Count; i++)
            {
                if (!keepListed || items == null || !items.Contains(boundItems[i]))
                    boundItems[i].Unbind();
            }

            boundItems.Clear();
        }
        #endregion

        #region Content
        bool IsValidIndex(int index) => items != null && index >= 0 && index < items.Count && items[index] != null;

        int FindItem(int start)
        {
            for (int i = 0; i < items.Count; i++)
            {
                int index = (start + i) % items.Count;
                if (items[index] != null)
                    return index;
            }
            return -1;
        }

        void HighlightCurrentButton()
        {
            for (int i = 0; i < items.Count; i++)
            {
                Button button = items[i]?.button;
                if (button != null && button.IsInteractable())
                    button.SetState(i == currentIndex ? InteractionState.Highlighted : InteractionState.Normal);
            }
        }

        bool SetItemContent(int index)
        {
            Item item = IsValidIndex(index) ? items[index] : null;
            bool changed = false;
            string description = item?.description ?? string.Empty;
            changed |= TextProvider.UpdateText(textDisplay, description);

            Graphic backgroundGraphic = BackgroundGraphic;
            if (backgroundGraphic != null)
            {
                Sprite background = item?.background;
                if (backgroundShape != null)
                {
                    if (backgroundShape.Sprite != background)
                    {
                        backgroundShape.Sprite = background;
                        changed = true;
                    }
                }
                else if (backgroundImage.sprite != background)
                {
                    backgroundImage.sprite = background;
                    changed = true;
                }

                bool hasBackground = background != null;
                if (backgroundGraphic.gameObject.activeSelf != hasBackground)
                {
                    backgroundGraphic.gameObject.SetActive(hasBackground);
                    changed = true;
                }
            }

            return changed;
        }

        #endregion

        #region Animation
        void PlayContentAnimation(Action onComplete = null)
        {
            animationStartPosition = textRect != null ? textRect.anchoredPosition : textPosition;
            animationStartAlpha = textCanvasGroup != null ? textCanvasGroup.alpha : 0f;
            backgroundStartAlpha = backgroundCanvasGroup != null ? backgroundCanvasGroup.alpha : 0f;
            contentTween = EvoTween.Create(0f, 1f, animationDuration * 0.5f)
                .WithTimeMode(AnimationTimeMode).OnComplete(onComplete).Bind(animateContent);
        }

        void AnimateContent(float progress)
        {
            float alpha = isAnimatingOut ? 0f : 1f;
            if (textRect != null)
                textRect.anchoredPosition = Vector2.LerpUnclamped(animationStartPosition, animationEndPosition, progress);
            if (textCanvasGroup != null)
                textCanvasGroup.alpha = Mathf.LerpUnclamped(animationStartAlpha, alpha, progress);
            if (backgroundCanvasGroup != null)
                backgroundCanvasGroup.alpha = Mathf.LerpUnclamped(backgroundStartAlpha, alpha, progress);
        }

        void CompleteOutAnimation()
        {
            isAnimatingOut = false;
           
            if (!isInitialized || !IsValidIndex(currentIndex))
                return;

            SetItemContent(currentIndex);
            displayedIndex = currentIndex;

            if (HasShadow)
            {
                shadowTween.Kill();
                Color color = items[currentIndex].shadowColor;
                Color startColor = ShadowColor;
                if (startColor != color)
                {
                    shadowTween = EvoTween.Create(Utilities.PremultiplyColor(startColor), Utilities.PremultiplyColor(color), animationDuration)
                        .WithEase(Ease.OutQuad).WithTimeMode(AnimationTimeMode).Bind(animateShadow);
                }
            }

            PlayInAnimation();
        }

        void PlayInAnimation()
        {
            contentTween.Kill();
            isAnimatingOut = false;
          
            if (textRect != null)
                textRect.anchoredPosition = textPosition + slideOffset;
            if (textCanvasGroup != null)
                textCanvasGroup.alpha = 0f;
            if (backgroundCanvasGroup != null)
                backgroundCanvasGroup.alpha = 0f;

            animationEndPosition = textPosition;
            PlayContentAnimation();
        }

        void AnimateShadow(Vector4 color) => ShadowColor = Utilities.UnpremultiplyColor(color);

        // Gradient shadows fade from the color at its alpha to fully transparent
        void SetShadowGradient(Color color)
        {
            shadowColorKeys[0] = new GradientColorKey(color, 0f);
            shadowColorKeys[1] = new GradientColorKey(color, 1f);
            shadowAlphaKeys[0] = new GradientAlphaKey(color.a, 0f);
            shadowAlphaKeys[1] = new GradientAlphaKey(0f, 1f);

            // Keys are edited in place, so FillGradient's setter would skip the update
            shadowShape.FillGradient.SetKeys(shadowColorKeys, shadowAlphaKeys);
            shadowShape.SetMaterialDirty();
        }

        bool IsShadowColor(Color color)
        {
            if (ShadowColor != color)
                return false;

            // The fade end is checked too, since a gradient set by hand can share only the start
            return !HasShadowGradient || shadowShape.FillGradient.Evaluate(1f) == new Color(color.r, color.g, color.b, 0f);
        }

        void StopAnimations()
        {
            contentTween.Kill();
            shadowTween.Kill();
            contentTween = default;
            shadowTween = default;
            isAnimatingOut = false;

            if (textRect != null)
                textRect.anchoredPosition = textPosition;
            if (textCanvasGroup != null)
                textCanvasGroup.alpha = 1f;
            if (backgroundCanvasGroup != null)
                backgroundCanvasGroup.alpha = 1f;
        }
        #endregion

        #region Public API
        public int CurrentIndex
        {
            get => currentIndex;
            set => SetItem(value);
        }

        /// <summary>
        /// Refreshes the preview or runtime content after changing items or references.
        /// </summary>
        public void Refresh()
        {
#if UNITY_EDITOR
            if (refreshPending)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
                refreshPending = false;
            }

            if (UnityEditor.EditorUtility.IsPersistent(this) || (Application.isPlaying && !Application.IsPlaying(gameObject)))
                return;
#endif

            bool runtime = Application.IsPlaying(gameObject);
            if (runtime && isInitialized)
                StopAnimations();

            isInitialized = false;

            if (buttonParent == null || (items != null && items.Count > 0 && (buttonPreset == null || !buttonPreset.TryGetComponent<Button>(out _))))
            {
                UnbindItems();
                Debug.LogWarning("[Showcase Panel] Assign a Button Parent and a Button Preset with a Button before refreshing.", this);
                return;
            }

            // Items still listed stay bound, and RefreshButtons rebinds only those whose button changed
            UnbindItems(runtime && isActiveAndEnabled);

            bool changed = items == null || generatedObjects == null;
            items ??= new List<Item>();
            generatedObjects ??= new List<GameObject>();
            changed |= RefreshButtons(runtime);

            if (runtime)
                SetupAnimation();

            int index = FindItem(Mathf.Clamp(currentIndex, 0, Mathf.Max(0, items.Count - 1)));
            changed |= currentIndex != index;
            currentIndex = index;
            changed |= SetItemContent(currentIndex);

            Color shadowColor = IsValidIndex(currentIndex) ? items[currentIndex].shadowColor : Color.clear;
            if (HasShadow && !IsShadowColor(shadowColor))
            {
                ShadowColor = shadowColor;
                changed = true;
            }

            displayedIndex = currentIndex;
            hoveredIndex = -1;
            timerCount = 0f;
            isInitialized = runtime && isActiveAndEnabled;
            if (isInitialized)
            {
                animateContent ??= AnimateContent;
                animateShadow ??= AnimateShadow;
                completeOut ??= CompleteOutAnimation;
                HighlightCurrentButton();
#if EVO_LOCALIZATION
                UpdateLocalization();
#endif
            }

            if (changed && buttonParent is RectTransform rect)
                LayoutRebuilder.MarkLayoutForRebuild(rect);

#if UNITY_EDITOR
            if (!runtime && changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);

                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i]?.button != null)
                        items[i].button.RecordContentChanges();
                }

                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        /// <summary>
        /// Smoothly switches to an item. Rapid requests share the current outgoing animation.
        /// </summary>
        public void SetItem(int index)
        {
            if (!isInitialized || !IsValidIndex(index))
                return;

            currentIndex = index;
            timerCount = 0f;
            HighlightCurrentButton();

            // Button state callbacks can disable the panel or change its items
            if (!isInitialized || currentIndex != index || !IsValidIndex(index) || isAnimatingOut || displayedIndex == index)
                return;

            contentTween.Kill();
            isAnimatingOut = true;
            animationEndPosition = textPosition - slideOffset;
            PlayContentAnimation(completeOut);
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || items == null)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrEmpty(item.titleKey)) { item.title = localizedObject.GetString(item.titleKey) ?? item.title; }
                if (!string.IsNullOrEmpty(item.descriptionKey)) { item.description = localizedObject.GetString(item.descriptionKey) ?? item.description; }
                if (item.button != null && item.button.Text != item.title) { item.button.Text = item.title; }
            }
            SetItemContent(displayedIndex);
        }
#endif

#if UNITY_EDITOR
        bool refreshPending;

        void OnValidate()
        {
            GeneratedObjects.Watch(this, OnValidate);
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        void RefreshDelayed()
        {
            refreshPending = false;

            if (this == null || UnityEditor.EditorApplication.isCompiling
                || (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying))
                return;

            Refresh();
        }
#endif
    }
}