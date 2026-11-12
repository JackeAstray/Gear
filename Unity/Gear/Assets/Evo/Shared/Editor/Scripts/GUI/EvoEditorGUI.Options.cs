using System;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public enum CalloutType
    {
        Default = 0,
        Warning = 1,
        Error = 2
    }

    public enum ContentAlignment
    {
        Center = 0,
        Left = 1,
        Right = 2,
        SpaceBetween = 3
    }

    public enum IconPosition
    {
        BeforeText = 0,
        AfterText = 1
    }

    [Flags]
    enum LayoutOptionOverrides
    {
        None = 0,
        Background = 1 << 0,
        Space = 1 << 1
    }

    [Flags]
    enum FieldOptionOverrides
    {
        None = 0,
        AddSpace = 1 << 0,
        Background = 1 << 1
    }

    [Flags]
    enum FoldoutOptionOverrides
    {
        None = 0,
        Background = 1 << 0,
        BoldText = 1 << 1,
        TextColor = 1 << 2
    }

    [Flags]
    enum ButtonOptionOverrides
    {
        None = 0,
        FontSize = 1 << 0,
        IconSize = 1 << 1,
        ContentSpacing = 1 << 2,
        DrawIdleBackground = 1 << 3
    }

    [Flags]
    enum ReorderableListOptionOverrides
    {
        None = 0,
        AddLabel = 1 << 0,
        DeleteTooltip = 1 << 1,
        AddIcon = 1 << 2,
        ListClipboard = 1 << 3
    }

    public readonly struct LayoutOptions
    {
        readonly LayoutOptionOverrides overrides;
        readonly bool background;
        readonly bool invertColor;
        readonly float space;

        LayoutOptions(LayoutOptionOverrides overrides, bool background, bool invertColor, float space)
        {
            this.overrides = overrides;
            this.background = background;
            this.invertColor = invertColor;
            this.space = space;
        }

        public static LayoutOptions WithSpace(LayoutOptions options = default) => options.WithSpace();

        public static LayoutOptions WithSpace(float value, LayoutOptions options = default) => options.WithSpace(value);

        public LayoutOptions WithSpace(float value = EvoEditorGUI.Metrics.LayoutSpacing) => new(overrides | LayoutOptionOverrides.Space, background, invertColor, value);

        public static LayoutOptions WithoutSpace(LayoutOptions options = default) => options.WithoutSpace();

        public LayoutOptions WithoutSpace() => new(overrides & ~LayoutOptionOverrides.Space, background, invertColor, 0f);

        public static LayoutOptions WithBackground(bool value, LayoutOptions options = default) => options.WithBackground(value);

        public LayoutOptions WithBackground(bool value = true) => new(overrides | LayoutOptionOverrides.Background, value, invertColor, space);

        public static LayoutOptions WithoutBackground(LayoutOptions options = default) => options.WithoutBackground();

        public LayoutOptions WithoutBackground() => WithBackground(false);

        public static LayoutOptions WithInvertedColor(LayoutOptions options = default) => options.WithInvertedColor();

        public static LayoutOptions WithInvertedColor(bool value, LayoutOptions options = default) => options.WithInvertedColor(value);

        public LayoutOptions WithInvertedColor(bool value = true) => new(overrides, background, value, space);

        internal static LayoutOptions FromFieldOptions(FieldOptions options)
        {
            LayoutOptionOverrides overrides = LayoutOptionOverrides.Background;
            float space = 0f;

            if (options.AddSpace)
            {
                overrides |= LayoutOptionOverrides.Space;
                space = EvoEditorGUI.Metrics.LayoutSpacing;
            }

            return new LayoutOptions(overrides, options.Background, options.InvertColor, space);
        }

        internal bool Background => (overrides & LayoutOptionOverrides.Background) == 0 || background;
        internal bool InvertColor => invertColor;
        internal bool HasSpace => (overrides & LayoutOptionOverrides.Space) != 0;
        internal float Space => space;
    }

    public readonly struct FieldOptions
    {
        readonly FieldOptionOverrides overrides;
        readonly string tooltip;
        readonly bool addSpace;
        readonly bool background;
        readonly bool invertColor;
        readonly int labelWidth;

        FieldOptions(FieldOptionOverrides overrides, string tooltip, bool addSpace, bool background, bool invertColor, int labelWidth)
        {
            this.overrides = overrides;
            this.tooltip = tooltip;
            this.addSpace = addSpace;
            this.background = background;
            this.invertColor = invertColor;
            this.labelWidth = labelWidth;
        }

        public static FieldOptions WithTooltip(string value, FieldOptions options = default) => options.WithTooltip(value);

        public FieldOptions WithTooltip(string value) => new(overrides, value, addSpace, background, invertColor, labelWidth);

        public static FieldOptions WithSpace(bool value, FieldOptions options = default) => options.WithSpace(value);

        public FieldOptions WithSpace(bool value = true) => new(overrides | FieldOptionOverrides.AddSpace, tooltip, value, background, invertColor, labelWidth);

        public static FieldOptions WithoutSpace(FieldOptions options = default) => options.WithoutSpace();

        public FieldOptions WithoutSpace() => WithSpace(false);

        public static FieldOptions WithBackground(bool value, FieldOptions options = default) => options.WithBackground(value);

        public FieldOptions WithBackground(bool value = true) => new(overrides | FieldOptionOverrides.Background, tooltip, addSpace, value, invertColor, labelWidth);

        public static FieldOptions WithoutBackground(FieldOptions options = default) => options.WithoutBackground();

        public FieldOptions WithoutBackground() => WithBackground(false);

        public static FieldOptions WithInvertedColor(FieldOptions options = default) => options.WithInvertedColor();

        public static FieldOptions WithInvertedColor(bool value, FieldOptions options = default) => options.WithInvertedColor(value);

        public FieldOptions WithInvertedColor(bool value = true) => new(overrides, tooltip, addSpace, background, value, labelWidth);

        public static FieldOptions WithLabelWidth(int value, FieldOptions options = default) => options.WithLabelWidth(value);

        public FieldOptions WithLabelWidth(int value) => new(overrides, tooltip, addSpace, background, invertColor, value);

        internal string Tooltip => tooltip;
        internal bool AddSpace => (overrides & FieldOptionOverrides.AddSpace) == 0 || addSpace;
        internal bool Background => (overrides & FieldOptionOverrides.Background) == 0 || background;
        internal bool InvertColor => invertColor;
        internal int LabelWidth => labelWidth;
        internal LayoutOptions Layout => LayoutOptions.FromFieldOptions(this);
    }

    public readonly struct FoldoutOptions
    {
        readonly FoldoutOptionOverrides overrides;
        readonly Texture2D icon;
        readonly bool background;
        readonly bool invertColor;
        readonly bool boldText;
        readonly Color textColor;

        FoldoutOptions(FoldoutOptionOverrides overrides, Texture2D icon, bool background, bool invertColor, bool boldText, Color textColor)
        {
            this.overrides = overrides;
            this.icon = icon;
            this.background = background;
            this.invertColor = invertColor;
            this.boldText = boldText;
            this.textColor = textColor;
        }

        public static FoldoutOptions WithIcon(string value, bool isCustom = true, FoldoutOptions options = default) => options.WithIcon(value, isCustom);

        public FoldoutOptions WithIcon(IconDefinition value) => WithIcon(EvoEditorGUI.GetIcon(value));

        public FoldoutOptions WithIcon(string value, bool isCustom = true) => WithIcon(EvoEditorGUI.GetIcon(value, isCustom));

        public static FoldoutOptions WithIcon(Texture2D value, FoldoutOptions options = default) => options.WithIcon(value);

        public FoldoutOptions WithIcon(Texture2D value) => new(overrides, value, background, invertColor, boldText, textColor);

        public static FoldoutOptions WithBackground(bool value, FoldoutOptions options = default) => options.WithBackground(value);

        public FoldoutOptions WithBackground(bool value = true) => new(overrides | FoldoutOptionOverrides.Background, icon, value, invertColor, boldText, textColor);

        public static FoldoutOptions WithoutBackground(FoldoutOptions options = default) => options.WithoutBackground();

        public FoldoutOptions WithoutBackground() => WithBackground(false);

        public static FoldoutOptions WithInvertedColor(FoldoutOptions options = default) => options.WithInvertedColor();

        public static FoldoutOptions WithInvertedColor(bool value, FoldoutOptions options = default) => options.WithInvertedColor(value);

        public FoldoutOptions WithInvertedColor(bool value = true) => new(overrides, icon, background, value, boldText, textColor);

        public static FoldoutOptions WithBoldText(bool value, FoldoutOptions options = default) => options.WithBoldText(value);

        public FoldoutOptions WithBoldText(bool value = true) => new(overrides | FoldoutOptionOverrides.BoldText, icon, background, invertColor, value, textColor);

        public static FoldoutOptions WithoutBoldText(FoldoutOptions options = default) => options.WithoutBoldText();

        public FoldoutOptions WithoutBoldText() => WithBoldText(false);

        public static FoldoutOptions WithTextColor(Color value, FoldoutOptions options = default) => options.WithTextColor(value);

        public FoldoutOptions WithTextColor(Color value) => new(overrides | FoldoutOptionOverrides.TextColor, icon, background, invertColor, boldText, value);

        internal Texture2D Icon => icon;
        internal bool Background => (overrides & FoldoutOptionOverrides.Background) == 0 || background;
        internal bool InvertColor => invertColor;
        internal bool BoldText => (overrides & FoldoutOptionOverrides.BoldText) == 0 || boldText;
        internal Color TextColor => (overrides & FoldoutOptionOverrides.TextColor) == 0 ? EvoEditorGUI.Palette.Content : textColor;
    }

    public readonly struct ButtonOptions
    {
        readonly ButtonOptionOverrides overrides;
        readonly string tooltip;
        readonly float width;
        readonly float height;
        readonly float contentSpacing;
        readonly int fontSize;
        readonly int iconSize;
        readonly bool invertBackgroundColor;
        readonly bool drawIdleBackground;
        readonly Color? normalColor;
        readonly Color? hoverColor;
        readonly Color? pressColor;
        readonly ContentAlignment contentAlignment;
        readonly IconPosition iconPosition;

        ButtonOptions(ButtonOptionOverrides overrides, string tooltip, float width, float height, int fontSize, int iconSize,
            ContentAlignment contentAlignment, IconPosition iconPosition, float contentSpacing, Color? normalColor,
            Color? hoverColor, Color? pressColor, bool invertBackgroundColor, bool drawIdleBackground)
        {
            this.overrides = overrides;
            this.tooltip = tooltip;
            this.width = width;
            this.height = height;
            this.fontSize = fontSize;
            this.iconSize = iconSize;
            this.contentAlignment = contentAlignment;
            this.iconPosition = iconPosition;
            this.contentSpacing = contentSpacing;
            this.normalColor = normalColor;
            this.hoverColor = hoverColor;
            this.pressColor = pressColor;
            this.invertBackgroundColor = invertBackgroundColor;
            this.drawIdleBackground = drawIdleBackground;
        }

        public static ButtonOptions WithTooltip(string value, ButtonOptions options = default) => options.WithTooltip(value);

        public ButtonOptions WithTooltip(string value)
        {
            return new ButtonOptions(overrides, value, width, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithWidth(float value, ButtonOptions options = default) => options.WithWidth(value);

        public ButtonOptions WithWidth(float value)
        {
            return new ButtonOptions(overrides, tooltip, value, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithHeight(float value, ButtonOptions options = default) => options.WithHeight(value);

        public ButtonOptions WithHeight(float value)
        {
            return new ButtonOptions(overrides, tooltip, width, value, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithSize(float width, float height, ButtonOptions options = default) => options.WithSize(width, height);

        public ButtonOptions WithSize(float width, float height)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithFontSize(int value, ButtonOptions options = default) => options.WithFontSize(value);

        public ButtonOptions WithFontSize(int value)
        {
            return new ButtonOptions(overrides | ButtonOptionOverrides.FontSize, tooltip, width, height,
                value, iconSize, contentAlignment, iconPosition, contentSpacing, normalColor, hoverColor,
                pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithIconSize(int value, ButtonOptions options = default) => options.WithIconSize(value);

        public ButtonOptions WithIconSize(int value)
        {
            return new ButtonOptions(overrides | ButtonOptionOverrides.IconSize, tooltip, width, height,
                fontSize, value, contentAlignment, iconPosition, contentSpacing, normalColor, hoverColor,
                pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithContentAlignment(ContentAlignment value, ButtonOptions options = default)
        {
            return options.WithContentAlignment(value);
        }

        public ButtonOptions WithContentAlignment(ContentAlignment value)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, value, iconPosition,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithIconPosition(IconPosition value, ButtonOptions options = default) => options.WithIconPosition(value);

        public ButtonOptions WithIconPosition(IconPosition value)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment, value,
                contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithContentSpacing(float value, ButtonOptions options = default) => options.WithContentSpacing(value);

        public ButtonOptions WithContentSpacing(float value)
        {
            return new ButtonOptions(overrides | ButtonOptionOverrides.ContentSpacing, tooltip, width, height,
                fontSize, iconSize, contentAlignment, iconPosition, value, normalColor, hoverColor,
                pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithNormalColor(Color? value, ButtonOptions options = default) => options.WithNormalColor(value);

        public ButtonOptions WithNormalColor(Color? value)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, value, hoverColor, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithHoverColor(Color? value, ButtonOptions options = default) => options.WithHoverColor(value);

        public ButtonOptions WithHoverColor(Color? value)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, value, pressColor, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithPressColor(Color? value, ButtonOptions options = default) => options.WithPressColor(value);

        public ButtonOptions WithPressColor(Color? value)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment, iconPosition,
                contentSpacing, normalColor, hoverColor, value, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithColors(Color? normal, Color? hover, Color? press, ButtonOptions options = default)
        {
            return options.WithColors(normal, hover, press);
        }

        public ButtonOptions WithColors(Color? normal, Color? hover, Color? press)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment,
                iconPosition, contentSpacing, normal, hover, press, invertBackgroundColor, drawIdleBackground);
        }

        public static ButtonOptions WithInvertedColor(ButtonOptions options = default) => options.WithInvertedColor();

        public static ButtonOptions WithInvertedColor(bool value, ButtonOptions options = default) => options.WithInvertedColor(value);

        public ButtonOptions WithInvertedColor(bool value = true)
        {
            return new ButtonOptions(overrides, tooltip, width, height, fontSize, iconSize, contentAlignment,
                iconPosition, contentSpacing, normalColor, hoverColor, pressColor, value, drawIdleBackground);
        }

        public static ButtonOptions WithIdleBackground(bool value, ButtonOptions options = default) => options.WithIdleBackground(value);

        public ButtonOptions WithIdleBackground(bool value = true)
        {
            return new ButtonOptions(overrides | ButtonOptionOverrides.DrawIdleBackground, tooltip, width, height, fontSize, iconSize,
                contentAlignment, iconPosition, contentSpacing, normalColor, hoverColor, pressColor, invertBackgroundColor, value);
        }

        public static ButtonOptions WithoutIdleBackground(ButtonOptions options = default) => options.WithoutIdleBackground();

        public ButtonOptions WithoutIdleBackground() => WithIdleBackground(false);

        internal string Tooltip => tooltip;
        internal float Width => width;
        internal float Height => height;
        internal float ContentSpacing => (overrides & ButtonOptionOverrides.ContentSpacing) != 0 ? contentSpacing : EvoEditorGUI.Metrics.ButtonContentSpacing;
        internal int FontSize => (overrides & ButtonOptionOverrides.FontSize) != 0 ? fontSize : EvoEditorGUI.Metrics.ButtonFontSize;
        internal int IconSize => (overrides & ButtonOptionOverrides.IconSize) != 0 ? iconSize : EvoEditorGUI.Metrics.ButtonIconSize;
        internal bool InvertBackgroundColor => invertBackgroundColor;
        internal bool DrawIdleBackground => (overrides & ButtonOptionOverrides.DrawIdleBackground) == 0 || drawIdleBackground;
        internal ContentAlignment ContentAlignment => contentAlignment;
        internal IconPosition IconPosition => iconPosition;
        internal Color? NormalColor => normalColor;
        internal Color? HoverColor => hoverColor;
        internal Color? PressColor => pressColor;
    }

    public readonly struct ReorderableListOptions
    {
        readonly ReorderableListOptionOverrides overrides;
        readonly string controlName;
        readonly string title;
        readonly string emptyMessage;
        readonly string addLabel;
        readonly string deleteTooltip;
        readonly bool allowListPaste;
        readonly bool itemsReadOnly;
        readonly bool disableClipboard;
        readonly Action beforeChange;
        readonly Action afterChange;
        readonly Action contentHeader;
        readonly Texture2D icon;
        readonly Texture2D addIcon;
        readonly Action<ReorderableListAddContext> addAction;
        readonly Action<SerializedProperty> initializeNewItem;
        readonly Action<SerializedProperty> copyItem;
        readonly Action<SerializedProperty> pasteItem;
        readonly Func<SerializedProperty, bool> canPasteItem;
        readonly Func<SerializedProperty, bool> deleteItem;
        readonly Func<SerializedProperty, Texture2D> itemIconGetter;
        readonly Action<SerializedProperty, Rect> drawItemPreview;
        readonly SerializedProperty listClipboardRoot;
        readonly Vector2 itemPreviewSize;

        ReorderableListOptions(ReorderableListOptionOverrides overrides, string controlName,
            Func<SerializedProperty, Texture2D> itemIconGetter, Action beforeChange, Action afterChange, string title,
            Texture2D icon, string emptyMessage, string addLabel, Texture2D addIcon, Action<ReorderableListAddContext> addAction,
            Action<SerializedProperty> initializeNewItem, string deleteTooltip, SerializedProperty listClipboardRoot,
            bool allowListPaste, Action<SerializedProperty> copyItem, Func<SerializedProperty, bool> canPasteItem,
            Action<SerializedProperty> pasteItem, Action contentHeader, bool itemsReadOnly,
            Action<SerializedProperty, Rect> drawItemPreview, Vector2 itemPreviewSize,
            Func<SerializedProperty, bool> deleteItem, bool disableClipboard)
        {
            this.deleteItem = deleteItem;
            this.disableClipboard = disableClipboard;
            this.overrides = overrides;
            this.controlName = controlName;
            this.itemIconGetter = itemIconGetter;
            this.beforeChange = beforeChange;
            this.afterChange = afterChange;
            this.title = title;
            this.icon = icon;
            this.emptyMessage = emptyMessage;
            this.addLabel = addLabel;
            this.addIcon = addIcon;
            this.addAction = addAction;
            this.initializeNewItem = initializeNewItem;
            this.deleteTooltip = deleteTooltip;
            this.listClipboardRoot = listClipboardRoot;
            this.allowListPaste = allowListPaste;
            this.copyItem = copyItem;
            this.canPasteItem = canPasteItem;
            this.pasteItem = pasteItem;
            this.contentHeader = contentHeader;
            this.itemsReadOnly = itemsReadOnly;
            this.drawItemPreview = drawItemPreview;
            this.itemPreviewSize = itemPreviewSize;
        }

        public static ReorderableListOptions WithControlName(string value, ReorderableListOptions options = default)
        {
            return options.WithControlName(value);
        }

        public ReorderableListOptions WithControlName(string value)
        {
            return new ReorderableListOptions(overrides, value, itemIconGetter, beforeChange, afterChange, title, icon, emptyMessage,
                addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithItemIcon(Func<SerializedProperty, Texture2D> value, ReorderableListOptions options = default)
        {
            return options.WithItemIcon(value);
        }

        public ReorderableListOptions WithItemIcon(Func<SerializedProperty, Texture2D> value)
        {
            return new ReorderableListOptions(overrides, controlName, value, beforeChange, afterChange, title, icon, emptyMessage,
                addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithItemPreview(Action<SerializedProperty, Rect> draw, Vector2 size, ReorderableListOptions options = default)
        {
            return options.WithItemPreview(draw, size);
        }

        /// <summary>
        /// Draws fixed-size content between each item's foldout arrow and title.
        /// </summary>
        public ReorderableListOptions WithItemPreview(Action<SerializedProperty, Rect> draw, Vector2 size)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange,
                title, icon, emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot,
                allowListPaste, copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly, draw, size, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithChangeCallbacks(Action before, Action after, ReorderableListOptions options = default)
        {
            return options.WithChangeCallbacks(before, after);
        }

        public ReorderableListOptions WithChangeCallbacks(Action before, Action after)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, before, after, title, icon, emptyMessage,
                addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithTitle(string value, ReorderableListOptions options = default) => options.WithTitle(value);

        public ReorderableListOptions WithTitle(string value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, value, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithIcon(string value, bool isCustom = true, ReorderableListOptions options = default)
        {
            return options.WithIcon(value, isCustom);
        }

        public ReorderableListOptions WithIcon(IconDefinition value) => WithIcon(EvoEditorGUI.GetIcon(value));

        public ReorderableListOptions WithIcon(string value, bool isCustom = true) => WithIcon(EvoEditorGUI.GetIcon(value, isCustom));

        public static ReorderableListOptions WithIcon(Texture2D value, ReorderableListOptions options = default) => options.WithIcon(value);

        public ReorderableListOptions WithIcon(Texture2D value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, value,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithEmptyMessage(string value, ReorderableListOptions options = default)
        {
            return options.WithEmptyMessage(value);
        }

        public ReorderableListOptions WithEmptyMessage(string value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon, value,
                addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem, canPasteItem,
                pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithAddLabel(string value, ReorderableListOptions options = default)
        {
            return options.WithAddLabel(value);
        }

        public ReorderableListOptions WithAddLabel(string value)
        {
            return new ReorderableListOptions(overrides | ReorderableListOptionOverrides.AddLabel, controlName, itemIconGetter,
                beforeChange, afterChange, title, icon, emptyMessage, value, addIcon, addAction, initializeNewItem, deleteTooltip,
                listClipboardRoot, allowListPaste, copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview,
                itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithAddIcon(IconDefinition value, ReorderableListOptions options = default)
        {
            return options.WithAddIcon(value);
        }

        public ReorderableListOptions WithAddIcon(IconDefinition value) => WithAddIcon(EvoEditorGUI.GetIcon(value));

        public static ReorderableListOptions WithAddIcon(string value, bool isCustom = true, ReorderableListOptions options = default)
        {
            return options.WithAddIcon(value, isCustom);
        }

        public ReorderableListOptions WithAddIcon(string value, bool isCustom = true) => WithAddIcon(EvoEditorGUI.GetIcon(value, isCustom));

        public static ReorderableListOptions WithAddIcon(Texture2D value, ReorderableListOptions options = default)
        {
            return options.WithAddIcon(value);
        }

        public ReorderableListOptions WithAddIcon(Texture2D value)
        {
            return new ReorderableListOptions(overrides | ReorderableListOptionOverrides.AddIcon, controlName, itemIconGetter,
                beforeChange, afterChange, title, icon, emptyMessage, addLabel, value, addAction, initializeNewItem, deleteTooltip,
                listClipboardRoot, allowListPaste, copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview,
                itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithoutAddButton(ReorderableListOptions options = default) => options.WithoutAddButton();

        public ReorderableListOptions WithoutAddButton() => WithAddLabel(null);

        public static ReorderableListOptions WithAddAction(Action<ReorderableListAddContext> value, ReorderableListOptions options = default)
        {
            return options.WithAddAction(value);
        }

        public ReorderableListOptions WithAddAction(Action<ReorderableListAddContext> value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, value, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithItemInitializer(Action<SerializedProperty> value, ReorderableListOptions options = default)
        {
            return options.WithItemInitializer(value);
        }

        public ReorderableListOptions WithItemInitializer(Action<SerializedProperty> value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, value, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithDeleteTooltip(string value, ReorderableListOptions options = default)
        {
            return options.WithDeleteTooltip(value);
        }

        public ReorderableListOptions WithDeleteTooltip(string value)
        {
            return new ReorderableListOptions(overrides | ReorderableListOptionOverrides.DeleteTooltip, controlName, itemIconGetter,
                beforeChange, afterChange, title, icon, emptyMessage, addLabel, addIcon, addAction, initializeNewItem, value,
                listClipboardRoot, allowListPaste, copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly,
                drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithListClipboard(SerializedProperty root, bool allowPaste = true, ReorderableListOptions options = default)
        {
            return options.WithListClipboard(root, allowPaste);
        }

        public ReorderableListOptions WithListClipboard(SerializedProperty root, bool allowPaste = true)
        {
            return new ReorderableListOptions(overrides | ReorderableListOptionOverrides.ListClipboard, controlName, itemIconGetter,
                beforeChange, afterChange, title, icon, emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip,
                root, allowPaste, copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize,
                deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithClipboard(Action<SerializedProperty> copy, Func<SerializedProperty, bool> canPaste,
            Action<SerializedProperty> paste, ReorderableListOptions options = default)
        {
            return options.WithClipboard(copy, canPaste, paste);
        }

        public ReorderableListOptions WithClipboard(Action<SerializedProperty> copy, Func<SerializedProperty, bool> canPaste,
            Action<SerializedProperty> paste)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste,
                copy, canPaste, paste, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithContentHeader(Action value, ReorderableListOptions options = default)
        {
            return options.WithContentHeader(value);
        }

        public ReorderableListOptions WithContentHeader(Action value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste,
                copyItem, canPasteItem, pasteItem, value, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithReadOnlyItems(ReorderableListOptions options = default) => options.WithReadOnlyItems();

        public ReorderableListOptions WithReadOnlyItems()
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, true, drawItemPreview, itemPreviewSize, deleteItem, disableClipboard);
        }

        public static ReorderableListOptions WithDeleteAction(Func<SerializedProperty, bool> value, ReorderableListOptions options = default)
        {
            return options.WithDeleteAction(value);
        }

        /// <summary>
        /// Handles deletion before removing the row. Return false to leave the row unchanged.
        /// </summary>
        public ReorderableListOptions WithDeleteAction(Func<SerializedProperty, bool> value)
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste, copyItem,
                canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, value, disableClipboard);
        }

        public static ReorderableListOptions WithoutClipboard(ReorderableListOptions options = default) => options.WithoutClipboard();

        public ReorderableListOptions WithoutClipboard()
        {
            return new ReorderableListOptions(overrides, controlName, itemIconGetter, beforeChange, afterChange, title, icon,
                emptyMessage, addLabel, addIcon, addAction, initializeNewItem, deleteTooltip, listClipboardRoot, allowListPaste,
                copyItem, canPasteItem, pasteItem, contentHeader, itemsReadOnly, drawItemPreview, itemPreviewSize, deleteItem, true);
        }

        internal string ControlName => controlName;
        internal string Title => title;
        internal string EmptyMessage => emptyMessage;
        internal string AddLabel => (overrides & ReorderableListOptionOverrides.AddLabel) != 0 ? addLabel : "Add Item";
        internal string DeleteTooltip => (overrides & ReorderableListOptionOverrides.DeleteTooltip) != 0 ? deleteTooltip : "Delete item";
        internal bool AllowItemChanges => !itemsReadOnly;
        internal bool AllowClipboard => !disableClipboard;
        internal bool AllowListPaste => (overrides & ReorderableListOptionOverrides.ListClipboard) != 0 ? allowListPaste : !itemsReadOnly;
        internal Texture2D Icon => icon;
        internal Texture2D AddIcon => (overrides & ReorderableListOptionOverrides.AddIcon) != 0 ? addIcon : EvoEditorGUI.GetIcon(IconLibrary.Add);
        internal Action ContentHeader => contentHeader;
        internal Action AfterChange => afterChange;
        internal Action BeforeChange => beforeChange;
        internal Action<ReorderableListAddContext> AddAction => addAction;
        internal Action<SerializedProperty> InitializeNewItem => initializeNewItem;
        internal Action<SerializedProperty> CopyItem => copyItem;
        internal Action<SerializedProperty> PasteItem => pasteItem;
        internal Action<SerializedProperty, Rect> DrawItemPreview => drawItemPreview;
        internal Func<SerializedProperty, Texture2D> ItemIconGetter => itemIconGetter;
        internal Func<SerializedProperty, bool> DeleteItem => deleteItem;
        internal Func<SerializedProperty, bool> CanPasteItem => canPasteItem;
        internal SerializedProperty ListClipboardRoot => listClipboardRoot;
        internal Vector2 ItemPreviewSize => itemPreviewSize;
    }

    public readonly struct ReorderableListAddContext
    {
        readonly Action<Action<SerializedProperty>> addItem;
        internal ReorderableListAddContext(Action<Action<SerializedProperty>> addItem) => this.addItem = addItem;
        public void AddItem(Action<SerializedProperty> initialize = null) => addItem?.Invoke(initialize);
    }
}