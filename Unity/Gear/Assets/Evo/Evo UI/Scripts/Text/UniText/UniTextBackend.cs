#if EVO_UNITEXT
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using LightSide;
using Object = UnityEngine.Object;

namespace Evo.UI
{
    /// <summary>
    /// UniText backend. Compiled when the UniText package (media.lightside.unitext) is installed.
    /// Settings without a UniText property map to whole-text styles, which are created once
    /// and toggled afterwards so repeated styling stays allocation-free.
    /// Input fields are <see cref="UniTextEditable"/> components on the edited text object.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed class UniTextBackend : TextBackend
    {
        public const string ID = "UniText";
        public static readonly UniTextBackend Instance = new();

        // TMP spacing values are hundredths of an em
        const float SpacingPerEm = 100f;

        // OpenType tabular figures, so every digit has the same advance. Spelled with its value,
        // so a style the user added for tabular figures is never toggled
        const string TabularDigits = "tnum 1";

        // Fields disabled through SetInteractable, held weakly so destroyed fields are released.
        // The value tells whether disabling made the field read-only, so an authored read-only field stays read-only
        static readonly ConditionalWeakTable<UniTextEditable, object> disabledFields = new();
        static readonly object MadeReadOnly = new();
        static readonly object KeptReadOnly = new();

        // Set while a value is set without notification, so listeners skip that edit
        static bool settingSilently;

        static readonly string[] FontSizePaths = { "fontSize" };

        static readonly (FontStyles style, Type modifier)[] FontStyleModifiers =
        {
            (FontStyles.Bold, typeof(BoldModifier)),
            (FontStyles.Italic, typeof(ItalicModifier)),
            (FontStyles.Underline, typeof(UnderlineModifier)),
            (FontStyles.Strikethrough, typeof(StrikethroughModifier)),
            (FontStyles.LowerCase, typeof(LowercaseModifier)),
            (FontStyles.UpperCase, typeof(UppercaseModifier)),
            (FontStyles.SmallCaps, typeof(SmallCapsModifier))
        };

        UniTextBackend() { }

        public override string Id => ID;
        public override string DisplayName => "UniText";
        public override Type TextType => typeof(UniTextBase);
        public override Type ComponentType => typeof(LightSide.UniText);
        public override Type FontType => typeof(UniTextFontStack);
        public override string TextProperty => "text";
        public override IReadOnlyList<string> FontSizeProperties => FontSizePaths;

        public override bool IsText(Graphic graphic) => graphic is UniTextBase;
        public override bool IsFont(Object font) => font is UniTextFontStack || font is UniTextFont;

        protected override void StripInput(GameObject copy)
        {
            // Dependents go first, as each requires the next
            if (copy.TryGetComponent(out UniTextEditable editable))
                Object.DestroyImmediate(editable);
            if (copy.TryGetComponent(out UniTextSelectable selectable))
                Object.DestroyImmediate(selectable);
            if (copy.TryGetComponent(out UniTextFocusable focusable))
                Object.DestroyImmediate(focusable);

            // The field creates its caret as a hidden child of the text
            foreach (InputCaretRenderer caret in copy.GetComponentsInChildren<InputCaretRenderer>(true))
                Object.DestroyImmediate(caret.gameObject);
        }

        #region Content
        public override string GetText(Graphic text)
        {
            var uniText = (UniTextBase)text;

            // Text set from a buffer is not serialized, so it is read from the runtime source
            return (uniText.TextOverride & TextOverrideSource.SetText) != 0 ? uniText.RawText.ToString() : uniText.Text;
        }

        public override void SetText(Graphic text, string value) => ((UniTextBase)text).Text = value;

        // The span overload copies the characters and skips the rebuild when they did not change
        public override void SetText(Graphic text, char[] buffer, int start, int length) => ((UniTextBase)text).SetText(new ReadOnlySpan<char>(buffer, start, length));

        public override void SetText(Graphic text, StringBuilder buffer) => ((UniTextBase)text).SetText(buffer);
        #endregion

        #region Font
        public override Object GetFont(Graphic text)
        {
            var uniText = (UniTextBase)text;
            return uniText.Font != null ? uniText.Font : uniText.FontStack;
        }

        public override void SetFont(Graphic text, Object font)
        {
            var uniText = (UniTextBase)text;

            if (font is UniTextFontStack stack)
            {
                // A primary font would override the stack, so the stack takes over completely
                uniText.FontStack = stack;
                uniText.Font = null;
            }
            else if (font is UniTextFont single)
                uniText.Font = single;
        }

        public override float GetFontSize(Graphic text) => ((UniTextBase)text).FontSize;
        public override void SetFontSize(Graphic text, float size) => ((UniTextBase)text).FontSize = size;

        public override void SetAutoSize(Graphic text, bool enabled, float min, float max,
            float characterWidthAdjustment = 0f, float lineSpacingAdjustment = 0f)
        {
            var uniText = (UniTextBase)text;
            uniText.AutoSize = enabled;

            if (!enabled)
                return;

            uniText.MinFontSize = min;
            uniText.MaxFontSize = max;
        }

        public override void SetFontStyle(Graphic text, FontStyles style)
        {
            var uniText = (UniTextBase)text;
            for (int i = 0; i < FontStyleModifiers.Length; i++)
                EnableWholeText(uniText, FontStyleModifiers[i].modifier, (style & FontStyleModifiers[i].style) != 0);
        }

        // Styles added outside play mode would be saved with the text, also from prefab assets
        public override void SetTabularDigits(Graphic text, bool enabled)
        {
            if (Application.isPlaying)
                EnableWholeText((UniTextBase)text, typeof(FontFeatureModifier), enabled, TabularDigits);
        }
        #endregion

        #region Layout
        public override TextAlignmentOptions GetAlignment(Graphic text)
        {
            var uniText = (UniTextBase)text;

            HorizontalAlignmentOptions horizontal = uniText.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => HorizontalAlignmentOptions.Center,
                HorizontalAlignment.End or HorizontalAlignment.Right => HorizontalAlignmentOptions.Right,
                HorizontalAlignment.Justify => HorizontalAlignmentOptions.Justified,
                _ => HorizontalAlignmentOptions.Left
            };

            VerticalAlignmentOptions vertical = uniText.VerticalAlignment switch
            {
                VerticalAlignment.Middle => VerticalAlignmentOptions.Middle,
                VerticalAlignment.Bottom => VerticalAlignmentOptions.Bottom,
                _ => VerticalAlignmentOptions.Top
            };

            return (TextAlignmentOptions)((int)horizontal | (int)vertical);
        }

        public override void SetAlignment(Graphic text, TextAlignmentOptions alignment)
        {
            var uniText = (UniTextBase)text;

            // Start and End follow the writing direction, matching UniText's own TMP migration
            uniText.HorizontalAlignment = (HorizontalAlignmentOptions)((int)alignment & 0xFF) switch
            {
                HorizontalAlignmentOptions.Center or HorizontalAlignmentOptions.Geometry => HorizontalAlignment.Center,
                HorizontalAlignmentOptions.Right => HorizontalAlignment.End,
                HorizontalAlignmentOptions.Justified or HorizontalAlignmentOptions.Flush => HorizontalAlignment.Justify,
                _ => HorizontalAlignment.Start
            };

            uniText.VerticalAlignment = (VerticalAlignmentOptions)((int)alignment & 0xFF00) switch
            {
                VerticalAlignmentOptions.Top => VerticalAlignment.Top,
                VerticalAlignmentOptions.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Middle
            };
        }

        public override void SetWordWrap(Graphic text, bool enabled) => ((UniTextBase)text).WordWrap = enabled;

        public override void SetOverflow(Graphic text, TextOverflowModes mode)
        {
            var uniText = (UniTextBase)text;
            EnableWholeText(uniText, typeof(EllipsisModifier), mode == TextOverflowModes.Ellipsis);
            EnableWholeText(uniText, typeof(TruncateModifier), mode == TextOverflowModes.Truncate);
        }

        public override void SetSpacing(Graphic text, float character, float word, float line, float paragraph)
        {
            var uniText = (UniTextBase)text;

            if (EnableWholeText(uniText, typeof(LetterSpacingModifier), character != 0f) is LetterSpacingModifier letterSpacing)
                letterSpacing.Spacing = UnitValue.Em(character / SpacingPerEm);

            if (EnableWholeText(uniText, typeof(WordSpacingModifier), word != 0f) is WordSpacingModifier wordSpacing)
                wordSpacing.Spacing = UnitValue.Em(word / SpacingPerEm);

            // Line height has no em unit, so the TMP value resolves against the current size
            if (EnableWholeText(uniText, typeof(LineHeightModifier), line != 0f) is LineHeightModifier lineHeight)
                lineHeight.HeightValue = UnitValue.Delta(line / SpacingPerEm * uniText.FontSize);

            if (EnableWholeText(uniText, typeof(ParagraphSpacingModifier), paragraph != 0f) is ParagraphSpacingModifier paragraphSpacing)
                paragraphSpacing.After = UnitValue.Em(paragraph / SpacingPerEm);
        }

        // TMP margin is left, top, right, bottom. UniText padding is left, bottom, right, top.
        public override void SetMargin(Graphic text, Vector4 margin) => ((UniTextBase)text).Padding = new Vector4(margin.x, margin.w, margin.z, margin.y);

        public override Vector2 GetPreferredValues(Graphic text, string value, float width, float height)
        {
            var options = new TextMeasureOptions { text = value };
            if (!float.IsInfinity(width))
                options.maxWidth = width;
            if (!float.IsInfinity(height))
                options.maxHeight = height;

            return ((UniTextBase)text).MeasureText(in options);
        }
        #endregion

        #region Animation
        // The animator adds a style, which would be saved with the text outside play mode
        public override TextGlyphAnimator CreateGlyphAnimator(Graphic text)
            => Application.isPlaying ? new UniTextGlyphAnimator((UniTextBase)text) : null;
        #endregion

        #region Input
        public override Type InputType => typeof(UniTextEditable);

        public override Graphic GetTextComponent(MonoBehaviour input) => ((UniTextEditable)input).TextComponent;

        public override Graphic GetPlaceholder(MonoBehaviour input) => ((UniTextEditable)input).GetBehavior<PlaceholderDecorator>()?.Target;

        public override void SetPlaceholder(MonoBehaviour input, Graphic placeholder)
        {
            var editable = (UniTextEditable)input;
            PlaceholderDecorator decorator = editable.GetBehavior<PlaceholderDecorator>();

            // Detaching shows the previous target, as UniText restores it
            if (decorator != null)
                decorator.Target = placeholder as UniTextBase;
            else if (placeholder is UniTextBase target)
                editable.Behaviors.Add(new PlaceholderDecorator { Target = target });
        }

        public override string GetValue(MonoBehaviour input) => ((UniTextEditable)input).Text;

        public override bool ValueEquals(MonoBehaviour input, ReadOnlySpan<char> value) => ((UniTextEditable)input).TextEquals(value);

        public override void SetValue(MonoBehaviour input, string value, bool notify)
        {
            var editable = (UniTextEditable)input;
            value ??= string.Empty;

#if UNITY_EDITOR
            // Outside play mode the field imports its text component's serialized text, which is what gets saved
            if (!Application.isPlaying)
            {
                UniTextBase text = editable.TextComponent;
                if (text.Text != value)
                {
                    text.Text = value;
                    UnityEditor.EditorUtility.SetDirty(text);
                }
            }
#endif

            // Setting resets the caret and undo history, and like TMP an unchanged value reports nothing
            if (editable.TextEquals(value))
                return;

            if (notify)
            {
                editable.SetText(value);
                return;
            }

            settingSilently = true;
            try { editable.SetText(value); }
            finally { settingSilently = false; }
        }

        public override void SetCharacterLimit(MonoBehaviour input, int limit)
        {
            var editable = (UniTextEditable)input;
            LengthLimitBehavior behavior = editable.GetBehavior<LengthLimitBehavior>();

            if (behavior != null)
            {
                if (behavior.Limit != limit)
                    behavior.Limit = limit;
            }
            else if (limit > 0)
                editable.Behaviors.Add(new LengthLimitBehavior { Limit = limit, Unit = TextLengthUnit.Utf16Units });
        }

        public override bool IsFocused(MonoBehaviour input) => ((UniTextEditable)input).IsActive;

        public override void Focus(MonoBehaviour input)
        {
            var editable = (UniTextEditable)input;

            // Selecting through the EventSystem lets a later click elsewhere end editing
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != editable.gameObject)
                eventSystem.SetSelectedGameObject(editable.gameObject);
            if (!editable.IsActive)
                editable.Activate();
        }

        public override bool IsInteractable(MonoBehaviour input) => !disabledFields.TryGetValue((UniTextEditable)input, out _);

        // UniText fields have no interactable state, so a disabled field becomes read-only until it is enabled again
        public override void SetInteractable(MonoBehaviour input, bool interactable)
        {
            var editable = (UniTextEditable)input;
            bool disabled = disabledFields.TryGetValue(editable, out object state);
            if (interactable == !disabled)
                return;

            if (interactable)
            {
                disabledFields.Remove(editable);
                if (state == MadeReadOnly)
                    editable.ReadOnly = false;
            }
            else
            {
                disabledFields.Add(editable, editable.ReadOnly ? KeptReadOnly : MadeReadOnly);
                editable.ReadOnly = true;
            }
        }

        public override object AddListener(MonoBehaviour input, TextInputListener listener) => new InputSubscription((UniTextEditable)input, listener);

        public override void RemoveListener(MonoBehaviour input, object subscription) => ((InputSubscription)subscription).Remove();

        public override GameObject GetInputHost(Graphic text) => text.gameObject;

        public override void CollectInputComponents(MonoBehaviour input, List<Component> results)
        {
            var editable = (UniTextEditable)input;
            results.Add(editable);

            if (editable.TryGetComponent(out UniTextSelectable selectable))
                results.Add(selectable);
            if (editable.TryGetComponent(out UniTextFocusable focusable))
                results.Add(focusable);
        }

        public override void CaptureInput(MonoBehaviour input, TextInputSettings target)
        {
            var editable = (UniTextEditable)input;
            IntegerFilter integer = editable.GetBehavior<IntegerFilter>();
            bool password = editable.GetBehavior<PasswordBehavior>() != null;

            target.value = editable.Text;
            target.placeholder = GetPlaceholder(editable);
            target.characterLimit = editable.GetBehavior<LengthLimitBehavior>()?.Limit ?? 0;
            target.lineType = editable.GetBehavior<SubmitKeyBehavior>() != null ? TMP_InputField.LineType.MultiLineSubmit
                : editable.GetBehavior<SingleLineBehavior>() != null ? TMP_InputField.LineType.SingleLine
                : TMP_InputField.LineType.MultiLineNewline;
            target.contentType = password ? (integer != null && !integer.AllowNegative ? TMP_InputField.ContentType.Pin : TMP_InputField.ContentType.Password)
                : integer != null ? TMP_InputField.ContentType.IntegerNumber
                : editable.GetBehavior<DecimalFilter>() != null ? TMP_InputField.ContentType.DecimalNumber
                : editable.GetBehavior<AlphanumericFilter>() != null ? TMP_InputField.ContentType.Alphanumeric
                : editable.GetBehavior<NameFilter>() != null ? TMP_InputField.ContentType.Name
                : editable.GetBehavior<EmailFilter>() != null ? TMP_InputField.ContentType.EmailAddress
                : TMP_InputField.ContentType.Standard;
            target.readOnly = editable.ReadOnly;
            target.interactable = true;
            target.enabled = editable.enabled;
            target.selectAllOnFocus = editable.GetBehavior<SelectAllOnFocusBehavior>() != null;
            target.restoreOnEscape = editable.GetBehavior<RestoreOnCancelBehavior>() != null;

            PaintRef paint = editable.Selectable.SelectionHighlight.Paint;
            if (paint.kind == PaintRefKind.Color)
                target.selectionColor = paint.color;
        }

        public override void ApplyInput(MonoBehaviour input, Graphic text, TextInputSettings settings)
        {
            var editable = (UniTextEditable)input;
            var uniText = (UniTextBase)text;
            var behaviors = editable.Behaviors;

            // Behaviors these settings define are replaced, any other policy stays
            for (int i = behaviors.Count - 1; i >= 0; i--)
            {
                if (behaviors[i] is InputFilterBase or LengthLimitBehavior or SingleLineBehavior or SubmitKeyBehavior
                    or SelectAllOnFocusBehavior or RestoreOnCancelBehavior or DefocusOnCancelBehavior or PlaceholderDecorator)
                    behaviors.RemoveAt(i);
            }

            switch (settings.contentType)
            {
                case TMP_InputField.ContentType.IntegerNumber: behaviors.Add(new IntegerFilter { AllowNegative = true }); break;
                case TMP_InputField.ContentType.DecimalNumber: behaviors.Add(new DecimalFilter { AllowNegative = true }); break;
                case TMP_InputField.ContentType.Alphanumeric: behaviors.Add(new AlphanumericFilter()); break;
                case TMP_InputField.ContentType.Name: behaviors.Add(new NameFilter()); break;
                case TMP_InputField.ContentType.EmailAddress: behaviors.Add(new EmailFilter()); break;
                case TMP_InputField.ContentType.Pin: behaviors.Add(new IntegerFilter { AllowNegative = false }); break;
            }

            if (settings.characterLimit > 0)
                behaviors.Add(new LengthLimitBehavior { Limit = settings.characterLimit, Unit = TextLengthUnit.Utf16Units });

            // A password field is single-line on its own
            if (settings.contentType is TMP_InputField.ContentType.Password or TMP_InputField.ContentType.Pin)
                behaviors.Add(new PasswordBehavior());
            else if (settings.lineType == TMP_InputField.LineType.SingleLine)
                behaviors.Add(new SingleLineBehavior());
            else if (settings.lineType == TMP_InputField.LineType.MultiLineSubmit)
                behaviors.Add(new SubmitKeyBehavior { Submit = SubmitKey.Enter, KeepFocusOnSubmit = false });

            if (settings.selectAllOnFocus)
                behaviors.Add(new SelectAllOnFocusBehavior());

            behaviors.Add(settings.restoreOnEscape ? new RestoreOnCancelBehavior() : new DefocusOnCancelBehavior());

            if (settings.placeholder is UniTextBase placeholder)
                behaviors.Add(new PlaceholderDecorator { Target = placeholder });

            // The edited text takes pointer input itself to place the caret
            uniText.raycastTarget = true;
            uniText.WordWrap = settings.lineType != TMP_InputField.LineType.SingleLine;
            uniText.Text = settings.value ?? string.Empty;
            editable.Selectable.SelectionHighlight.Paint = PaintRef.Solid(settings.selectionColor);
            editable.ReadOnly = settings.readOnly || !settings.interactable;
            editable.Selectable.enabled = settings.enabled;
            editable.enabled = settings.enabled;
        }

        /// <summary>
        /// Forwards a field's events. UniText reports changes once per frame, so edits are noted as they apply
        /// and a value is reported when any of them was not set silently.
        /// </summary>
        sealed class InputSubscription
        {
            readonly UniTextEditable editable;
            readonly TextInputListener listener;
            bool valueChanged;

            public InputSubscription(UniTextEditable editable, TextInputListener listener)
            {
                this.editable = editable;
                this.listener = listener;

                editable.EditApplied += OnEditApplied;
                editable.DocumentChanged += OnDocumentChanged;
                editable.Submitted += listener.RaiseSubmit;
                editable.Focused += listener.RaiseSelect;
                editable.Defocused += OnDefocused;
            }

            public void Remove()
            {
                editable.EditApplied -= OnEditApplied;
                editable.DocumentChanged -= OnDocumentChanged;
                editable.Submitted -= listener.RaiseSubmit;
                editable.Focused -= listener.RaiseSelect;
                editable.Defocused -= OnDefocused;
            }

            void OnEditApplied(EditShape _) => valueChanged |= !settingSilently;

            void OnDocumentChanged(string _)
            {
                listener.RaiseTextUpdated();
                if (!valueChanged)
                    return;

                valueChanged = false;
                if (listener.onValueChanged != null)
                    listener.RaiseValueChanged(editable.Text);
            }

            void OnDefocused(EditingEndReason _)
            {
                if (listener.onEndEdit != null)
                    listener.RaiseEndEdit(editable.Text);

                listener.RaiseDeselect();
            }
        }
        #endregion

        #region Styles
        public override void CaptureTextStyle(Graphic text, Styler.TextStyleItem target)
        {
            var uniText = (UniTextBase)text;
            float fontSize = uniText.FontSize;

            target.fontSize = fontSize;
            target.enableAutoSizing = uniText.AutoSize;
            target.fontSizeMin = uniText.MinFontSize;
            target.fontSizeMax = uniText.MaxFontSize;
            target.characterWidthAdjustment = 0f;
            target.lineSpacingAdjustment = 0f;
            target.alignment = GetAlignment(text);
            target.enableWordWrapping = uniText.WordWrap;
            target.overflowMode = FindWholeText(uniText, typeof(TruncateModifier)) != null ? TextOverflowModes.Truncate
                : FindWholeText(uniText, typeof(EllipsisModifier)) != null ? TextOverflowModes.Ellipsis
                : TextOverflowModes.Overflow;

            target.characterSpacing = FindWholeText(uniText, typeof(LetterSpacingModifier)) is LetterSpacingModifier letterSpacing
                ? ToSpacing(letterSpacing.Spacing, fontSize) : 0f;
            target.wordSpacing = FindWholeText(uniText, typeof(WordSpacingModifier)) is WordSpacingModifier wordSpacing
                ? ToSpacing(wordSpacing.Spacing, fontSize) : 0f;
            target.lineSpacing = FindWholeText(uniText, typeof(LineHeightModifier)) is LineHeightModifier lineHeight
                ? ToSpacing(lineHeight.HeightValue, fontSize) : 0f;
            target.paragraphSpacing = FindWholeText(uniText, typeof(ParagraphSpacingModifier)) is ParagraphSpacingModifier paragraphSpacing
                ? ToSpacing(paragraphSpacing.After, fontSize) : 0f;

            Vector4 padding = uniText.Padding;
            target.margin = new Vector4(padding.x, padding.w, padding.z, padding.y);

            FontStyles fontStyle = FontStyles.Normal;
            for (int i = 0; i < FontStyleModifiers.Length; i++)
            {
                if (FindWholeText(uniText, FontStyleModifiers[i].modifier) != null)
                    fontStyle |= FontStyleModifiers[i].style;
            }

            target.fontStyle = fontStyle;
        }

        static float ToSpacing(UnitValue value, float fontSize) => value.unit switch
        {
            UnitKind.Em => value.value * SpacingPerEm,
            UnitKind.Absolute or UnitKind.Delta when fontSize > 0f => value.value / fontSize * SpacingPerEm,
            _ => 0f
        };

        /// <summary>
        /// Returns the enabled whole-text modifier of the exact type, or null.
        /// </summary>
        static BaseModifier FindWholeText(UniTextBase text, Type modifierType)
        {
            Style style = FindWholeTextStyle(text, modifierType, null);
            return style != null && style.Enabled ? style.Modifier : null;
        }

        /// <summary>
        /// Finds a local whole-text style of the exact modifier type. A parameter also has to match when given.
        /// </summary>
        static Style FindWholeTextStyle(UniTextBase text, Type modifierType, string parameter)
        {
            var styles = text.Styles;
            for (int i = 0; i < styles.Count; i++)
            {
                Style style = styles[i];
                if (style?.Modifier != null && style.Modifier.GetType() == modifierType && UniTextBase.IsWholeTextStyle(style)
                    && (parameter == null || style.DefaultParameter == parameter))
                    return style;
            }
            return null;
        }

        /// <summary>
        /// Toggles a whole-text modifier, creating it on first use. Returns it when enabled.
        /// </summary>
        static BaseModifier EnableWholeText(UniTextBase text, Type modifierType, bool enabled, string parameter = null)
        {
            Style style = FindWholeTextStyle(text, modifierType, parameter);
            if (style == null)
            {
                if (!enabled)
                    return null;

                style = Style.WholeText((BaseModifier)Activator.CreateInstance(modifierType), parameter);
                text.Styles.Add(style);
            }
            else if (style.Enabled != enabled)
                style.Enabled = enabled;

            return enabled ? style.Modifier : null;
        }
        #endregion
    }
}
#endif