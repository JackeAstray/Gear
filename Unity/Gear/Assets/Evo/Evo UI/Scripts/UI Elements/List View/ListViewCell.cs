using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Internal binding and view cache for a generated ListView cell.
    /// </summary>
    [AddComponentMenu(""), DisallowMultipleComponent]
    public sealed class ListViewCell : MonoBehaviour, ILayoutGroup
    {
        const string ContentName = "Content [ListView]";
        const string IconName = "Icon [ListView]";
        const string LabelName = "Label [ListView]";
        const string CustomWrapperName = "Custom Object [ListView]";

        [SerializeField, HideInInspector] ListView owner;
        [SerializeField, HideInInspector] string bindingKey;
        [SerializeField, HideInInspector] int rowIndex;
        [SerializeField, HideInInspector] int columnIndex;
        [SerializeField, HideInInspector] int appliedContentHash = int.MinValue;
        [SerializeField, HideInInspector] int appliedVisualHash = int.MinValue;
        [SerializeField, HideInInspector] GameObject customObjectSource;

        [SerializeField, HideInInspector] ProceduralShape background;
        [SerializeField, HideInInspector] Button button;
        [SerializeField, HideInInspector] RectTransform content;
        [SerializeField, HideInInspector] HorizontalLayoutGroup layout;
        [SerializeField, HideInInspector] ContentSizeFitter contentFitter;
        [SerializeField, HideInInspector] Image iconImage;
        [SerializeField, HideInInspector, TextComponent] Graphic label;
        [SerializeField, HideInInspector] RectTransform customWrapper;
        [System.NonSerialized] bool buttonListenerBound;

        /// <summary>
        /// Gets the stable row/column binding key assigned by the owning ListView.
        /// </summary>
        public string BindingKey => bindingKey;

        /// <summary>
        /// Gets the source object assigned to the current GameObject preset.
        /// </summary>
        public GameObject CustomObjectSource => customObjectSource;

        /// <summary>
        /// Gets the live custom-object instance hosted by this cell, if any.
        /// </summary>
        public GameObject CustomObjectInstance => customWrapper != null && customWrapper.childCount > 0
            ? customWrapper.GetChild(0).gameObject
            : null;

        internal ListView Owner => owner;
        internal int RowIndex => rowIndex;
        internal int ColumnIndex => columnIndex;

        // The row this cell last drew, which tells the owner when a row gets cells it didn't have
        internal ListViewRow ShownRow { get; set; }

        internal void Bind(ListView listView, string key, int row, int column)
        {
            bool changed = owner != listView || bindingKey != key || rowIndex != row || columnIndex != column;
            owner = listView;
            bindingKey = key;
            rowIndex = row;
            columnIndex = column;
            EnsureCoreComponents();

            if (changed)
                MarkDirty();
        }

        // A negative sibling index leaves the order to the owner
        internal void SetRect(float x, float y, float width, float height, int siblingIndex)
        {
            var rect = (RectTransform)transform;
            Vector2 topLeft = new(0, 1);
            Vector2 size = new(Mathf.Max(0, width), Mathf.Max(0, height));
            Vector2 position = new(x, -y);
            bool reorder = siblingIndex >= 0 && transform.GetSiblingIndex() != siblingIndex;
            bool changed = rect.anchorMin != topLeft || rect.anchorMax != topLeft || rect.pivot != topLeft ||
                rect.sizeDelta != size || rect.anchoredPosition != position || reorder;

            if (!changed)
                return;

            rect.anchorMin = topLeft;
            rect.anchorMax = topLeft;
            rect.pivot = topLeft;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            if (reorder)
                transform.SetSiblingIndex(siblingIndex);

#if UNITY_EDITOR
            // Positions and sizes follow the list's rect, which is saved on its own, so like a layout group's they don't dirty
            // the scene, such as when the Game view sizes itself after the Editor starts. An instance saves only recorded values.
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
#endif
        }

        internal void Apply(ListViewCellType type, string text, Sprite icon, GameObject customObject, bool interactive,
            bool drawBackground, TextAnchor alignment, Color backgroundColor, Color borderColor, Vector4 cornerRadii, Color textColor,
            Object font, int fontSize, FontStyles fontStyle, ListViewStyle style, int contentHash, int visualHash)
        {
            EnsureCoreComponents();
            ConfigureInteraction(interactive);

            bool visualChanged = appliedVisualHash != visualHash;
            if (!visualChanged) { ConfigureRaycast(interactive && drawBackground); }
            else
            {
                ConfigureBackground(style, backgroundColor, borderColor, cornerRadii, interactive, drawBackground);
                appliedVisualHash = visualHash;
            }

            // Object references aren't part of the saved hash, so they're compared here
            bool customChanged = customObjectSource != (type == ListViewCellType.GameObject ? customObject : null);
            bool customMissing = type == ListViewCellType.GameObject &&
                (customWrapper == null || (customObject != null && CustomObjectInstance == null));
            bool builtInMissing = type != ListViewCellType.GameObject &&
                (layout == null || contentFitter == null || iconImage == null || label == null);
            bool builtInChanged = type != ListViewCellType.GameObject && !builtInMissing && BuiltInReferencesChanged(icon, font);

            if (appliedContentHash == contentHash && !customChanged && !customMissing && !builtInMissing && !builtInChanged)
            {
                if (visualChanged)
                    MarkDirty();

                return;
            }

            if (type == ListViewCellType.GameObject)
                ConfigureCustomObject(customObject);
            else
                ConfigureBuiltInContent(text, icon, alignment, textColor, font, fontSize, fontStyle, style);

            appliedContentHash = contentHash;
            MarkDirty();
        }

        internal void PrepareForRemoval()
        {
            if (button != null && buttonListenerBound)
            {
                button.onClick.RemoveListener(HandleButtonClicked);
                buttonListenerBound = false;
            }
        }

        internal void Invalidate()
        {
            appliedContentHash = int.MinValue;
            appliedVisualHash = int.MinValue;
        }

        void EnsureCoreComponents()
        {
            if (background == null)
            {
                if (!TryGetComponent(out background))
                    background = gameObject.AddComponent<ProceduralShape>();

                appliedVisualHash = int.MinValue;
            }

            if (content == null)
            {
                Transform existing = transform.Find(ContentName);
                content = existing as RectTransform;
            }

            if (content == null)
            {
                var contentObject = new GameObject(ContentName, typeof(RectTransform));
                contentObject.transform.SetParent(transform, false);
                content = (RectTransform)contentObject.transform;
            }
        }

        void EnsureBuiltInVisuals()
        {
            if (layout == null && !content.TryGetComponent(out layout))
                layout = content.gameObject.AddComponent<ListViewCellLayout>();

            if (contentFitter == null && !content.TryGetComponent(out contentFitter))
                contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();

            if (iconImage == null)
            {
                Transform existing = content.Find(IconName);
                if (existing != null)
                    iconImage = existing.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                var iconObject = new GameObject(IconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(content, false);
                iconImage = iconObject.GetComponent<Image>();
            }

            if (label == null)
            {
                Transform existing = content.Find(LabelName);
                if (existing != null)
                    TextProvider.TryGetText(existing, out label);
            }

            // Generated labels follow the provider saved by the owning list
            TextBackend backend = owner != null ? owner.LabelBackend : TextProvider.Active;
            if (label != null && !backend.IsText(label))
            {
                DestroyGeneratedChild(label.gameObject);
                label = null;
            }

            if (label == null)
            {
                var labelObject = new GameObject(LabelName, typeof(RectTransform), typeof(CanvasRenderer), backend.ComponentType);
                labelObject.transform.SetParent(content, false);
                label = labelObject.GetComponent<Graphic>();
            }
        }

        bool BuiltInReferencesChanged(Sprite icon, Object font)
        {
            bool hasIcon = icon != null;
            if (iconImage.gameObject.activeSelf != hasIcon || (hasIcon && iconImage.sprite != icon))
                return true;

            // A font the backend can't use is never applied, so it can't count as a change
            TextBackend backend = TextProvider.Find(label);
            Object targetFont = font != null ? font : backend?.DefaultFont;
            return backend != null && backend.IsFont(targetFont) && backend.GetFont(label) != targetFont;
        }

        void ConfigureInteraction(bool interactive)
        {
            if (interactive && button == null)
            {
                if (!TryGetComponent(out button))
                    button = gameObject.AddComponent<Button>();

                buttonListenerBound = false;
            }

            if (button == null)
                return;

            if (interactive)
            {
                button.targetGraphic = background;
                button.enabled = true;
                button.interactable = true;

                if (!buttonListenerBound)
                {
                    button.onClick.AddListener(HandleButtonClicked);
                    buttonListenerBound = true;
                }
            }
            else
            {
                if (buttonListenerBound)
                {
                    button.onClick.RemoveListener(HandleButtonClicked);
                    buttonListenerBound = false;
                }

                // Detach Selectable tinting before disabling a legacy Button component. Otherwise its
                // disabled-state color can leave a GameObject cell background partially transparent.
                button.targetGraphic = null;
                button.interactable = true;
                button.enabled = false;
                background.color = Color.white;
            }
        }

        void ConfigureBackground(ListViewStyle style, Color backgroundColor, Color borderColor, Vector4 cornerRadii,
            bool interactive, bool drawBackground)
        {
            background.enabled = drawBackground;
            background.Shape = ProceduralShape.ShapeType.Rectangle;
            background.Sprite = null;
            background.FillCenter = true;
            background.fillColorMode = ProceduralShape.ColorMode.Custom;
            ColorTransition.SetColor(background, 5, background.FillColor, backgroundColor, static (target, value) =>
            {
                var shape = (ProceduralShape)target;
                shape.FillColor = value;
            });
            background.outlineColorMode = ProceduralShape.ColorMode.Custom;
            ColorTransition.SetColor(background, 6, background.OutlineColor, borderColor, static (target, value) =>
            {
                var shape = (ProceduralShape)target;
                shape.OutlineColor = value;
            });
            background.OutlineWidth = style.showBorder ? Mathf.Max(0, style.borderWidth) : 0;
            background.CornerRadiusMode = style.radiusMode;
            // Outer corner overrides make a cell's radii uneven even when the list's corners are linked
            background.IndependentCorners = style.independentCorners || cornerRadii != Vector4.one * cornerRadii.x;
            background.CornerRadius = cornerRadii;
            background.SquircleCorners = style.squircleCorners;
            background.Softness = style.shapeSoftness;
            background.color = Color.white;
            ConfigureRaycast(interactive && drawBackground);
        }

        void ConfigureRaycast(bool interactive)
        {
            background.raycastTarget = interactive;
            background.RaycastDetectionMode = interactive ? ProceduralShape.RaycastMode.Standard : ProceduralShape.RaycastMode.None;
        }

        void ConfigureBuiltInContent(string text, Sprite icon, TextAnchor alignment, Color textColor,
            Object font, int fontSize, FontStyles fontStyle, ListViewStyle style)
        {
            ClearCustomObject();
            EnsureBuiltInVisuals();

            content.gameObject.SetActive(true);
            ConfigureContentRect(alignment, stretch: false);

            // Layout group setters box their values, so only changes are written
            RectOffset padding = style.contentPadding ?? new RectOffset();
            layout.enabled = true;
            if (layout.childAlignment != alignment)
                layout.childAlignment = alignment;
            if (!layout.childControlWidth)
                layout.childControlWidth = true;
            if (!layout.childControlHeight)
                layout.childControlHeight = true;
            if (layout.childForceExpandWidth)
                layout.childForceExpandWidth = false;
            if (layout.childForceExpandHeight)
                layout.childForceExpandHeight = false;
            if (layout.spacing != style.contentSpacing)
                layout.spacing = style.contentSpacing;
            if (layout.padding != padding)
                layout.padding = padding;

            contentFitter.enabled = true;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            bool hasIcon = icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                iconImage.sprite = icon;
                ColorTransition.SetGraphicColor(iconImage, textColor);
                iconImage.preserveAspect = true;
                iconImage.type = Image.Type.Simple;

                RectTransform iconRect = iconImage.rectTransform;
                iconRect.sizeDelta = new Vector2(style.iconSize, style.iconSize);
                LayoutElement iconLayout = iconImage.GetComponent<LayoutElement>();
                iconLayout.preferredWidth = style.iconSize;
                iconLayout.preferredHeight = style.iconSize;
            }

            TextBackend backend = TextProvider.Find(label);
            label.gameObject.SetActive(true);
            backend.SetText(label, text ?? string.Empty);
            ColorTransition.SetGraphicColor(label, textColor);
            backend.SetFont(label, font != null ? font : backend.DefaultFont);
            backend.SetFontSize(label, Mathf.Max(0, fontSize));
            backend.SetFontStyle(label, fontStyle);
            backend.SetAlignment(label, ToTextAlignment(alignment));
            backend.SetOverflow(label, TextOverflowModes.Ellipsis);
            backend.SetWordWrap(label, false);
        }

        void ConfigureCustomObject(GameObject source)
        {
            EnsureCustomWrapper();
            content.gameObject.SetActive(true);
            ConfigureContentRect(TextAnchor.MiddleCenter, stretch: true);

            if (layout != null)
                layout.enabled = false;
            if (contentFitter != null)
                contentFitter.enabled = false;
            if (iconImage != null)
                iconImage.gameObject.SetActive(false);
            if (label != null)
                label.gameObject.SetActive(false);

            customWrapper.gameObject.SetActive(true);
            if (customObjectSource == source && (source == null || CustomObjectInstance != null))
                return;

            ClearChildren(customWrapper);
            customObjectSource = source;

            if (source == null)
                return;

            GameObject instance = InstantiateCustomObject(source, customWrapper);
            instance.name = source.name + " [ListView Instance]";
            if (instance.TryGetComponent(out RectTransform rect))
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            else
            {
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
            }
        }

        void EnsureCustomWrapper()
        {
            if (customWrapper != null)
                return;

            Transform existing = content.Find(CustomWrapperName);
            customWrapper = existing as RectTransform;
            if (customWrapper != null)
                return;

            var wrapperObject = new GameObject(CustomWrapperName, typeof(RectTransform));
            wrapperObject.transform.SetParent(content, false);
            customWrapper = (RectTransform)wrapperObject.transform;
            Stretch(customWrapper);
        }

        void ClearCustomObject()
        {
            if (customWrapper != null)
            {
                ClearChildren(customWrapper);
                customWrapper.gameObject.SetActive(false);
            }

            customObjectSource = null;
        }

        void ConfigureContentRect(TextAnchor alignment, bool stretch)
        {
            if (stretch)
            {
                Stretch(content);
                return;
            }

            float x = IsLeftAligned(alignment) ? 0 : IsRightAligned(alignment) ? 1 : 0.5f;
            content.anchorMin = new Vector2(x, 0);
            content.anchorMax = new Vector2(x, 1);
            content.pivot = new Vector2(x, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 0);
        }

        void HandleButtonClicked() => owner.NotifyCellClicked(rowIndex, columnIndex);

        // The cell is the layout root of its content, and the content's own layout group and fitter do the work. A content
        // that's its own root lays out twice and starts a coroutine (an allocation) whenever its fitter resizes it to new text.
        void ILayoutController.SetLayoutHorizontal() { }
        void ILayoutController.SetLayoutVertical() { }

        static GameObject InstantiateCustomObject(GameObject source, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(source))
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source, parent);
#endif
            return Instantiate(source, parent, false);
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyGeneratedChild(parent.GetChild(i).gameObject);
        }

        static void DestroyGeneratedChild(Object target)
        {
            if (target == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(target); }
            else
            {
                if (target is GameObject gameObject)
                {
                    gameObject.SetActive(false);
                    gameObject.transform.SetParent(null, false);
                }

                Destroy(target);
            }
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        static bool IsLeftAligned(TextAnchor alignment) =>
            alignment == TextAnchor.UpperLeft || alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.LowerLeft;

        static bool IsRightAligned(TextAnchor alignment) =>
            alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight;

        static TextAlignmentOptions ToTextAlignment(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        void MarkDirty()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                UnityEditor.EditorUtility.SetDirty(this);

                // An instance saves only recorded values. Its applied hashes are saved, so the content they describe must be too.
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                {
                    GeneratedObjects.Record(this, transform, background, button, content, layout, contentFitter,
                        iconImage, label, customWrapper, GameObjectOf(content), GameObjectOf(iconImage), GameObjectOf(label),
                        GameObjectOf(customWrapper), iconImage != null ? iconImage.rectTransform : null,
                        iconImage != null ? iconImage.GetComponent<LayoutElement>() : null, label != null ? label.rectTransform : null);
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

#if UNITY_EDITOR
        static GameObject GameObjectOf(Component component) => component != null ? component.gameObject : null;
#endif
    }
}