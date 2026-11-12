using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Notification"), HelpURL(Constants.HelpUrl + "ui-elements/notification")]
    public class Notification : MonoBehaviour
    {
        // Content
        [SerializeField] private Sprite icon;
        [SerializeField] private string title = "Notification Title";
        [SerializeField, TextArea(2, 5)] private string description = "Notification description text goes here.";

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        // Settings
        [Tooltip("Check for other notifications under the same parent and display them one at a time.")]
        public bool enableStacking = true;
        public bool playOnEnable = true;
        public bool destroyAfter = false;
        public bool autoClose = true;
        [Range(0f, 60f)] public float autoCloseDelay = 3f;
        public bool showDurationBar = true;

        // Animations
        public TweenSequence openAnimation = CreateAnimation(true);
        public TweenSequence closeAnimation = CreateAnimation(false);

        // Styling
        public StyleProvider styleProvider = StyleProvider.Styler;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        public StylerPreset stylerPreset;

        // Sound Effects
        public bool soundEffects = true;
        public AudioMapping openSFX = new() { stylerID = "Open SFX" };
        public AudioMapping closeSFX = new() { stylerID = "Close SFX" };

        // References
        public Image iconImage;
        [TextComponent] public Graphic titleText;
        [TextComponent] public Graphic descriptionText;
        public ProceduralShape durationBar;

        // Helpers
        CanvasGroup canvasGroup;
        TweenHandle currentAnimation;
        TweenHandle autoCloseTimer;
        System.Action autoCloseCallback;
        bool isQueued;
        bool isInitialized;
        bool suppressPlayOnEnable;

        bool ShowsDurationBar => durationBar != null && showDurationBar && autoClose && autoCloseDelay > 0f;

        void Awake() => Initialize();

        void OnEnable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif

            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);

            if (playOnEnable && !suppressPlayOnEnable)
                Open();
        }

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif

            // Killing the animation skips OnCloseComplete, so a created notification
            // interrupted while open, closing or queued would never be destroyed
            bool interrupted = IsOpen || isQueued || currentAnimation.IsActive;
            bool wasVisible = IsOpen || (currentAnimation.IsActive && !isQueued);

            StopCurrentOperations();
            IsOpen = false;
            isQueued = false;

            if (isInitialized)
                SetVisualState(false);

            // Hiding the visible notification directly lets the next queued one in, as a normal close does
            if (wasVisible && !gameObject.activeSelf && Application.isPlaying)
                OpenNextQueued();
            if (destroyAfter && interrupted && Application.isPlaying)
                Destroy(gameObject);
        }

        void OnDestroy() => currentAnimation.Kill();

        void Initialize()
        {
            if (isInitialized)
                return;

            canvasGroup = GetComponent<CanvasGroup>();
            SetVisualState(false);
            isInitialized = true;
        }

        static TweenSequence CreateAnimation(bool open)
        {
            TweenSequence sequence = new() { TimeMode = TimeMode.Unscaled };

            TweenClip fade = new();
            fade.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = !open, from = 0f, to = open ? 1f : 0f };
            fade.Settings.Duration = 0.1f;
            fade.Settings.Ease = Ease.InOutSine;

            TweenClip slide = new();
            slide.Settings.Module = new RectTransformSlideModule
            {
                fromCurrent = !open,
                from = open ? new Vector2(0f, -25f) : Vector2.zero,
                to = open ? Vector2.zero : new Vector2(0f, -25f)
            };
            slide.Settings.Duration = open ? 0.15f : 0.1f;
            slide.Settings.Ease = open ? Ease.OutCubic : Ease.InCubic;

            sequence.InlineClips.Add(fade);
            sequence.InlineClips.Add(slide);
            return sequence;
        }

        void UpdateUI()
        {
            string newTitle = title ?? string.Empty;
            string newDescription = description ?? string.Empty;

            bool hasIcon = icon != null;
            bool hasTitle = !string.IsNullOrEmpty(newTitle);
            bool hasDescription = !string.IsNullOrEmpty(newDescription);

            if (titleText != null)
            {
                titleText.gameObject.SetActive(hasTitle);
                if (hasTitle)
                    TextProvider.SetText(titleText, newTitle);
            }

            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription)
                    TextProvider.SetText(descriptionText, newDescription);
            }

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon && iconImage.sprite != icon)
                    iconImage.sprite = icon;
            }

            if (Application.isPlaying && durationBar != null)
            {
                bool showBar = ShowsDurationBar;
                if (durationBar.gameObject.activeSelf != showBar)
                    durationBar.gameObject.SetActive(showBar);
            }
        }

        void SetVisualState(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            SetInteraction(visible);
        }

        void SetInteraction(bool value)
        {
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        void StopCurrentOperations()
        {
            currentAnimation.Kill();
            currentAnimation = default;
            autoCloseTimer.Kill();
            autoCloseTimer = default;
        }

        void OnOpenComplete()
        {
            currentAnimation = default;

            if (!autoClose || autoCloseDelay <= 0f)
                return;

            // One pooled tween counts down and drives the bar, so idle notifications cost nothing per frame
            autoCloseCallback ??= Close;
            autoCloseTimer = EvoTween.Create(1f, 0f, autoCloseDelay)
                .WithTimeMode(openAnimation.TimeMode)
                .OnComplete(autoCloseCallback)
                .Bind(this, static (notification, remaining) => notification.SetDurationBar(remaining), 0);
        }

        void SetDurationBar(float remaining)
        {
            if (durationBar != null && showDurationBar)
                durationBar.ClipAmount = remaining;
        }

        void OnCloseComplete()
        {
            currentAnimation = default;
            SetVisualState(false);
            gameObject.SetActive(false);
            OpenNextQueued();

            if (destroyAfter)
                Destroy(gameObject);
        }

        void OpenNextQueued()
        {
            if (!enableStacking || transform.parent == null)
                return;

            foreach (Transform child in transform.parent)
            {
                if (child == transform)
                    continue;

                if (child.gameObject.activeInHierarchy && child.TryGetComponent(out Notification sibling) &&
                    sibling.enableStacking && sibling.isQueued)
                {
                    sibling.Open();
                    break;
                }
            }
        }

        bool TryQueue()
        {
            if (!enableStacking || transform.parent == null)
                return false;

            foreach (Transform child in transform.parent)
            {
                if (child == transform || !child.gameObject.activeInHierarchy)
                    continue;

                if (child.TryGetComponent(out Notification sibling) && sibling.enableStacking && sibling.IsOpen)
                {
                    isQueued = true;
                    ActivateWithoutPlaying();
                    SetVisualState(false);
                    return true;
                }
            }
            return false;
        }

        void ActivateWithoutPlaying()
        {
            if (gameObject.activeSelf)
                return;

            suppressPlayOnEnable = true;
            gameObject.SetActive(true);
            suppressPlayOnEnable = false;
        }

        #region Public API
        public bool IsOpen { get; private set; }

        public Sprite Icon
        {
            get => icon;
            set
            {
                icon = value;
                UpdateUI();
            }
        }

        public string Title
        {
            get => title;
            set
            {
                title = value;
                UpdateUI();
            }
        }

        public string Description
        {
            get => description;
            set
            {
                description = value;
                UpdateUI();
            }
        }

        public void Open()
        {
            if (IsOpen)
                return;

            if (!isInitialized)
                Initialize();

            StopCurrentOperations();

            if (TryQueue())
                return;

            ActivateWithoutPlaying();

            if (!gameObject.activeInHierarchy)
            {
                // A created notification under an inactive parent could never open or close
                if (destroyAfter && Application.isPlaying)
                    Destroy(gameObject);

                return;
            }

            isQueued = false;
            IsOpen = true;
            UpdateUI();
            SetDurationBar(1f);
            SetVisualState(true);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, openSFX, stylerPreset));

            currentAnimation = openAnimation.Trigger(gameObject).OnComplete(OnOpenComplete);
            if (!currentAnimation.IsActive)
                OnOpenComplete();
        }

        public void Close()
        {
            if (!IsOpen || !gameObject.activeInHierarchy)
                return;

            if (!isInitialized)
                Initialize();

            IsOpen = false;
            StopCurrentOperations();
            SetInteraction(false);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, closeSFX, stylerPreset));

            currentAnimation = closeAnimation.Trigger(gameObject).OnComplete(OnCloseComplete);
            if (!currentAnimation.IsActive)
            {
                SetVisualState(false);
                OnCloseComplete();
            }
        }

        public void ForceClose()
        {
            if (!isInitialized)
                Initialize();

            bool wasVisible = IsOpen || (currentAnimation.IsActive && !isQueued);
            StopCurrentOperations();
            SetVisualState(false);

            isQueued = false;
            IsOpen = false;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            if (wasVisible && Application.isPlaying)
                OpenNextQueued();
            if (destroyAfter && Application.isPlaying)
                Destroy(gameObject);
        }

        public void SetContent(Sprite newIcon, string newTitle, string newDescription)
        {
            icon = newIcon;
            title = newTitle;
            description = newDescription;

            UpdateUI();
        }

        public static Notification Create(GameObject preset, Sprite icon, string title, string description,
            Transform parent, bool openAfter = true)
        {
            if (preset == null)
            {
                Debug.LogError("[Notification] A notification preset object is required.");
                return null;
            }

            if (parent == null)
            {
                Debug.LogError("[Notification] A parent transform must be specified.");
                return null;
            }

            GameObject ntfGo = Instantiate(preset, parent);
            if (!ntfGo.TryGetComponent(out Notification ntf))
            {
                Debug.LogError("[Notification] Assigned preset does not contain the 'Notification' component.");
                Destroy(ntfGo);
                return null;
            }

            ntf.icon = icon;
            ntf.title = title;
            ntf.description = description;
            ntf.destroyAfter = true;

            // A preset with Play On Enable is already open with its own content
            ntf.UpdateUI();

            if (openAfter)
                ntf.Open();

            return ntf;
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null)
                return;

            bool changed = false;

            if (!string.IsNullOrEmpty(titleKey)) 
            { 
                string newTitle = localizedObject.GetString(titleKey) ?? title;
                if (title != newTitle)
                {
                    title = newTitle;
                    changed = true;
                }
            }

            if (!string.IsNullOrEmpty(descriptionKey)) 
            { 
                string newDescription = localizedObject.GetString(descriptionKey) ?? description;
                if (description != newDescription)
                {
                    description = newDescription;
                    changed = true;
                }
            }

            if (changed)
                UpdateUI();
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);

            if (Application.isPlaying)
                return;

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                UpdateUI();
        }

        void Reset()
        {
            useDefaultStylerPreset = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }
#endif

        #region Obsolete API
        [System.Obsolete("Use Icon instead.")]
        public Sprite SetIcon { get => Icon; set => Icon = value; }

        [System.Obsolete("Use Title instead.")]
        public string SetTitle { get => Title; set => Title = value; }

        [System.Obsolete("Use Description instead.")]
        public string SetDescription { get => Description; set => Description = value; }
        #endregion
    }
}