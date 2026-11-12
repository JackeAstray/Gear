using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Button"), HelpURL(Constants.HelpUrl + "ui-elements/button")]
    public class Button : Interactive
    {
        // Settings
        [SerializeField] private bool allowDoubleClick = false;
        [SerializeField, Range(0.1f, 1)] private float doubleClickDuration = 0.25f;

        // References
        [Tooltip("Image object which the button icon will be applied to.")]
        public Image imageObject;
        [Tooltip("Handles layout size for the icon.")]
        [SerializeField] private LayoutElement iconElement;
        [Tooltip("Text object which the button text will be applied to.")]
        [TextComponent] public Graphic textObject;
        [Tooltip("Parent container for text. Toggled together with the text object.")]
        [SerializeField] private RectTransform textContainer;
        [Tooltip("Handles automatic/dynamic layout scaling.")]
        [SerializeField] private ContentSizeFitter contentFitter;
        [Tooltip("Manages icon/text layout adjustments.")]
        [SerializeField] private HorizontalLayoutGroup contentLayout;

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Cache
        double doubleClickDeadline;

#if EVO_LOCALIZATION
        protected override void OnEnable()
        {
            base.OnEnable();
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
        }

        protected override void OnDisable()
        {
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
            base.OnDisable();
        }

        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null)
                return;

            string localizedText = localizedObject.GetString(localizedObject.tableKey);
            if (localizedText != null)
                Text = localizedText;
        }
#endif

        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);

            if (!IsInteractable() || !allowDoubleClick || eventData.button != PointerEventData.InputButton.Left)
                return;

            double clickTime = Time.unscaledTimeAsDouble;
            if (waitingForDoubleClickInput && clickTime <= doubleClickDeadline)
            {
                waitingForDoubleClickInput = false;
                onDoubleClick?.Invoke();
                return;
            }

            waitingForDoubleClickInput = true;
            doubleClickDeadline = clickTime + Mathf.Max(0f, doubleClickDuration);
        }

        void RebuildLayout()
        {
            RectTransform contentRect = contentLayout != null ? (RectTransform)contentLayout.transform : null;
            if (contentRect != null)
                LayoutRebuilder.MarkLayoutForRebuild(contentRect);
            if (transform is RectTransform buttonRect && buttonRect != contentRect)
                LayoutRebuilder.MarkLayoutForRebuild(buttonRect);
        }

#if UNITY_EDITOR
        // For buttons generated from a prefab preset: Text and Icon also toggle their objects
        internal void RecordContentChanges()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                GeneratedObjects.Record(this, gameObject, textObject, imageObject, textObject != null ? textObject.gameObject : null,
                    imageObject != null ? imageObject.gameObject : null, textContainer != null ? textContainer.gameObject : null);
        }
#endif

        #region Properties
        public bool EnableIcon
        {
            get => imageObject != null && imageObject.gameObject.activeSelf;
            set
            {
                if (imageObject != null && imageObject.gameObject.activeSelf != value)
                    imageObject.gameObject.SetActive(value);
            }
        }

        public Sprite Icon
        {
            get => imageObject != null ? imageObject.sprite : null;
            set
            {
                if (imageObject != null && imageObject.sprite != value)
                    imageObject.sprite = value;
              
                EnableIcon = value != null;
            }
        }

        public float IconSize
        {
            get => iconElement != null ? iconElement.preferredWidth : 0f;
            set
            {
                if (iconElement == null)
                    return;

                float size = Mathf.Max(0f, value);
                if (!Mathf.Approximately(iconElement.preferredWidth, size))
                    iconElement.preferredWidth = size;
                if (!Mathf.Approximately(iconElement.preferredHeight, size))
                    iconElement.preferredHeight = size;
            }
        }

        public bool EnableText
        {
            get => textObject != null && textObject.gameObject.activeSelf
                && (textContainer == null || textContainer.gameObject.activeSelf);
            set
            {
                if (textObject != null && textObject.gameObject.activeSelf != value)
                    textObject.gameObject.SetActive(value);
                if (textContainer != null && textContainer.gameObject.activeSelf != value)
                    textContainer.gameObject.SetActive(value);
            }
        }

        public string Text
        {
            get => TextProvider.GetText(textObject);
            set
            {
                TextProvider.SetText(textObject, value);
                EnableText = !string.IsNullOrWhiteSpace(value);
            }
        }

        public float TextSize
        {
            get => TextProvider.GetFontSize(textObject);
            set => TextProvider.SetFontSize(textObject, Mathf.Max(0f, value));
        }

        public bool DynamicScale
        {
            get => contentFitter != null && contentFitter.enabled;
            set
            {
                if (contentFitter != null && contentFitter.enabled != value)
                    contentFitter.enabled = value;
            }
        }

        public bool ReverseArrangement
        {
            get => contentLayout != null && contentLayout.reverseArrangement;
            set
            {
                if (contentLayout != null && contentLayout.reverseArrangement != value)
                    contentLayout.reverseArrangement = value;
            }
        }

        public int Spacing
        {
            get => contentLayout != null ? Mathf.RoundToInt(contentLayout.spacing) : 0;
            set
            {
                if (contentLayout != null && !Mathf.Approximately(contentLayout.spacing, value))
                    contentLayout.spacing = value;
            }
        }

        public RectOffset Padding
        {
            get => contentLayout != null ? contentLayout.padding : null;
            set
            {
                if (contentLayout == null)
                    return;

                contentLayout.padding = value ?? new RectOffset();
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)contentLayout.transform);
            }
        }

        public bool AllowDoubleClick
        {
            get => allowDoubleClick;
            set
            {
                allowDoubleClick = value;
                if (!value)
                    waitingForDoubleClickInput = false;
            }
        }

        public float DoubleClickDuration
        {
            get => doubleClickDuration;
            set => doubleClickDuration = Mathf.Clamp(value, 0.1f, 1f);
        }
        #endregion

        #region Obsolete API
        /// <summary>
        /// Requests a layout rebuild using the current content.
        /// </summary>
        [System.Obsolete("Set the relevant button properties instead.")]
        public void UpdateUI() => RebuildLayout();

        /// <summary>
        /// Requests a layout rebuild using the current layout settings.
        /// </summary>
        [System.Obsolete("Set the relevant layout properties instead (such as Padding).")]
        public void UpdateLayout() => RebuildLayout();

        /// <summary>
        /// Sets the given sprite as the button icon.
        /// </summary>
        [System.Obsolete("Use the Icon property instead.")]
        public void SetIcon(Sprite newIcon) => Icon = newIcon;

        /// <summary>
        /// Sets the given string as the button text.
        /// </summary>
        [System.Obsolete("Use the Text property instead.")]
        public void SetText(string newText) => Text = newText;
        #endregion
    }
}