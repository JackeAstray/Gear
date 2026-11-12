using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/UI Elements/Modal Window"), HelpURL(Constants.HelpUrl + "ui-elements/modal-window")]
    public class ModalWindow : MonoBehaviour
    {
        // Content
        [SerializeField] private Sprite icon;
        [SerializeField] private string title = "Modal Title";
        [SerializeField, TextArea(2, 5)] private string description = "Modal description text.";

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
        public string titleKey;
        public string descriptionKey;
#endif

        // References
        public Image iconImage;
        [TextComponent] public Graphic titleText;
        [TextComponent] public Graphic descriptionText;
        public Button confirmButton;
        public Button cancelButton;

        // Settings
        [SerializeField, Tooltip("Allows manual editing of window content, including icon and text.")]
        private bool customContent = false;
        [Tooltip("Closes the window on confirm button press.")]
        public bool closeOnConfirm = true;
        [Tooltip("Closes the window on cancel button press.")]
        public bool closeOnCancel = true;
        [SerializeField, Tooltip("How the Modal Window will react at runtime start.")] 
        private StartBehavior startBehavior = StartBehavior.Open;
        [Tooltip("How the Modal Window will react when closed.")]
        public CloseBehavior closeBehavior = CloseBehavior.Disable;
        [Tooltip("When set to Focused, UI navigation is restricted to only " +
                 "the Confirm and Cancel buttons while the window remains open.")]
        public NavigationMode navigationMode = NavigationMode.Free;

        // Animations
        public TweenSequence openAnimation = new();
        public TweenSequence closeAnimation = new();

        // Events
        public UnityEvent onOpen = new();
        public UnityEvent onClose = new();
        public UnityEvent onConfirm = new();
        public UnityEvent onCancel = new();

        // Enums
        enum StartBehavior { Open = 0, Disabled = 1 }
        public enum CloseBehavior { Disable = 0, Destroy = 1 }
        public enum NavigationMode { Focused = 0, Free = 1 }

        // Properties
        public bool IsOpen { get; private set; }

        // Helpers
        CanvasGroup canvasGroup;
        TweenHandle currentAnimation;
        GameObject previousNavObject;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
        }

        void Start()
        {
            ApplyContent();

            // Open() can run before Start when the window starts inactive
            if (IsOpen)
                return;

            switch (startBehavior)
            {
                case StartBehavior.Open:
                    Open();
                    break;
                case StartBehavior.Disabled:
                    gameObject.SetActive(false);
                    break;
            }
        }

#if EVO_LOCALIZATION
        void OnEnable()
        {
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
        }
#endif

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif

            if (currentAnimation.IsActive)
            {
                TweenHandle interruptedAnimation = currentAnimation;
                currentAnimation = default;
                interruptedAnimation.Complete();
            }
        }

        void OnDestroy()
        {
            currentAnimation.Kill();

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        void Update()
        {
            if (!IsOpen || navigationMode == NavigationMode.Free)
                return;

            HandleFocusedNavigation();
        }

        void ApplyContent()
        {
            if (customContent)
                return;

            TextProvider.SetText(titleText, title);
            TextProvider.SetText(descriptionText, description);

            if (iconImage != null)
            {
                bool hasIcon = icon != null;
                
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);
                if (hasIcon && iconImage.sprite != icon)
                    iconImage.sprite = icon;
            }
        }

        void HandleFocusedNavigation()
        {
            if (EventSystem.current == null || (cancelButton == null && confirmButton == null))
                return;

            // Check if current selection is already one of our buttons
            var currentSelected = EventSystem.current.currentSelectedGameObject;
            bool isButtonSelected = (cancelButton != null && currentSelected == cancelButton.gameObject) ||
                                    (confirmButton != null && currentSelected == confirmButton.gameObject);

            // If neither button is selected, select the first available one
            if (!isButtonSelected)
            {
                if (cancelButton != null)
                    EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
                else if (confirmButton != null)
                    EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
            }
        }

        void OnConfirmClicked()
        {
            onConfirm?.Invoke();

            if (closeOnConfirm)
                Close();
        }

        void OnCancelClicked()
        {
            onCancel?.Invoke();

            if (closeOnCancel)
                Close();
        }

        void SetInteraction(bool value)
        {
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        void CompleteOpen() => currentAnimation = default;

        void CompleteClose()
        {
            currentAnimation = default;

            switch (closeBehavior)
            {
                case CloseBehavior.Disable:
                    gameObject.SetActive(false);
                    break;
                case CloseBehavior.Destroy:
                    Destroy(gameObject);
                    break;
            }
        }

        #region Public API
        public Sprite Icon
        {
            get => icon;
            set
            {
                icon = value;
                if (iconImage != null)
                {
                    bool hasIcon = icon != null;
                    if (iconImage.gameObject.activeSelf != hasIcon)
                        iconImage.gameObject.SetActive(hasIcon);
                    if (hasIcon && iconImage.sprite != icon)
                        iconImage.sprite = icon;
                }
            }
        }

        public string Title
        {
            get => title;
            set
            {
                title = value;
                TextProvider.SetText(titleText, title);
            }
        }

        public string Description
        {
            get => description;
            set
            {
                description = value;
                TextProvider.SetText(descriptionText, description);
            }
        }

        public void Open()
        {
            if (IsOpen)
                return;

            if (startBehavior == StartBehavior.Disabled)
                startBehavior = StartBehavior.Open;

            gameObject.SetActive(true);

            if (!gameObject.activeInHierarchy)
                return;

            if (navigationMode == NavigationMode.Focused)
                previousNavObject = Utilities.GetSelectedObject();

            currentAnimation.Kill();
            SetInteraction(true);
            IsOpen = true;
            onOpen?.Invoke();

            currentAnimation = openAnimation.Trigger(gameObject).OnComplete(CompleteOpen);
            if (!currentAnimation.IsActive)
                CompleteOpen();
        }

        public void Close()
        {
            if (!IsOpen || !gameObject.activeInHierarchy)
                return;

            currentAnimation.Kill();
            SetInteraction(false);

            if (navigationMode == NavigationMode.Focused && previousNavObject != null)
                Utilities.SetSelectedObject(previousNavObject);

            IsOpen = false;
            onClose?.Invoke();

            currentAnimation = closeAnimation.Trigger(gameObject).OnComplete(CompleteClose);
            if (!currentAnimation.IsActive)
                CompleteClose();
        }
        #endregion

        #region Obsolete API
        /// <summary>
        /// Sets the given sprite as the button icon.
        /// </summary>
        [System.Obsolete("Use the Icon property instead.")]
        public void SetIcon(Sprite newIcon) => Icon = newIcon;

        /// <summary>
        /// Sets the given string as the button text.
        /// </summary>
        [System.Obsolete("Use the Title property instead.")]
        public void SetTitle(string newText) => Title = newText;

        /// <summary>
        /// Sets the given string as the button text.
        /// </summary>
        [System.Obsolete("Use the Description property instead.")]
        public void SetDescription(string newText) => Description = newText;
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
                ApplyContent();
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying || customContent)
                return;

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                ApplyContent();
        }
#endif
    }
}