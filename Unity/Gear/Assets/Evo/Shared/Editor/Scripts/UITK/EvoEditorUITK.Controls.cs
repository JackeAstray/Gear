using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evo.EditorTools
{
    public static partial class EvoEditorUITK
    {
        #region Buttons
        /// <summary>
        /// Button in EvoEditorGUI.Button's style: a rounded fill that brightens and gains an outline on hover.
        /// Set text and iconImage on it later like on any Button, and pass null text for an icon-only button.
        /// The icon takes the text color, like Evo's single-color icons. Add evo-button--colored-icon to keep an icon's own colors,
        /// evo-button--icon-after to put it after the text, and evo-flat or evo-inverted for the other fills.
        /// </summary>
        public static Button CreateButton(string text, string tooltip, Action onClick)
        {
            return CreateButton(text, (Texture2D)null, tooltip, onClick);
        }

        public static Button CreateButton(string text, IconDefinition icon, string tooltip, Action onClick)
        {
            return CreateButton(text, EvoEditorGUI.GetIcon(icon), tooltip, onClick);
        }

        public static Button CreateButton(string text, Texture2D icon, string tooltip, Action onClick)
        {
            // Unity counts empty or cut-off text as elided and swaps the tooltip for that text, losing the one set here
            var button = new Button(onClick) { text = text ?? "", tooltip = tooltip, displayTooltipWhenElided = false };
            button.AddToClassList("evo-button");
            if (icon != null)
                button.iconImage = Background.FromTexture2D(icon);

            return button;
        }

        /// <summary>
        /// Borderless button that shows only an icon, such as a help button. The tooltip says what it does.
        /// </summary>
        public static Button CreateIconButton(Texture icon, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { tooltip = tooltip };
            MakeIconButton(button, icon);
            return button;
        }

        // Also restyles buttons Unity creates itself, such as a search field's clear button
        static void MakeIconButton(Button button, Texture icon)
        {
            button.AddToClassList("evo-icon-button");

            var image = new Image { image = icon, pickingMode = PickingMode.Ignore };
            image.AddToClassList("evo-icon-button__icon");
            button.Add(image);
        }
        #endregion

        #region Segments
        /// <summary>
        /// Segmented control in EvoEditorGUI's style: options side by side on a well, with the chosen one raised.
        /// Add options with AddOption, then set the choice with SetValueWithoutNotify. Clicking another option sets value,
        /// which sends a ChangeEvent to onChanged and RegisterValueChangedCallback. Clicking the chosen one does nothing.
        /// </summary>
        public class SegmentedControl<T> : VisualElement, INotifyValueChanged<T>
        {
            readonly List<(T option, Button button)> segments = new();
            T current;

            public SegmentedControl(Action<T> onChanged = null)
            {
                AddToClassList("evo-well");
                AddToClassList("evo-segments");
                if (onChanged != null)
                    this.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            }

            public SegmentedControl<T> AddOption(T option, string text, string tooltip)
            {
                var button = new Button(() => value = option) { text = text, tooltip = tooltip, focusable = false };
                button.AddToClassList("evo-segment");
                button.EnableInClassList("evo-segment--selected", Same(option, current));
                segments.Add((option, button));
                Add(button);
                return this;
            }

            public T value
            {
                get => current;
                set
                {
                    if (Same(value, current))
                        return;

                    T previous = current;
                    SetValueWithoutNotify(value);
                    using var evt = ChangeEvent<T>.GetPooled(previous, value);
                    evt.target = this;
                    SendEvent(evt);
                }
            }

            public void SetValueWithoutNotify(T newValue)
            {
                current = newValue;
                foreach (var (option, button) in segments)
                    button.EnableInClassList("evo-segment--selected", Same(option, newValue));
            }

            static bool Same(T left, T right) => EqualityComparer<T>.Default.Equals(left, right);
        }
        #endregion

        #region Fields
        /// <summary>
        /// Toggle in EvoEditorGUI.Toggle's style: a full-width row with the label on the left, and the state and a check box
        /// on the right. Clicking anywhere on the row switches it. Leave onChanged null when the toggle is bound with BindProperty,
        /// and pass null for both onText and offText to show only the check box.
        /// </summary>
        public static Toggle CreateToggle(string label, bool value, string tooltip, Action<bool> onChanged = null,
            string onText = "On", string offText = "Off")
        {
            var toggle = new EvoToggle(label) { value = value, tooltip = tooltip };
            toggle.AddToClassList("evo-toggle");

            VisualElement input = toggle.Q(className: Toggle.inputUssClassName);
            if (!string.IsNullOrEmpty(onText) || !string.IsNullOrEmpty(offText))
            {
                // Both states are always there and the stylesheet shows the one matching :checked, so values set by a binding show too
                var state = new VisualElement { pickingMode = PickingMode.Ignore };
                state.AddToClassList("evo-toggle__state");
                state.Add(CreateToggleState(onText, "evo-toggle__on"));
                state.Add(CreateToggleState(offText, "evo-toggle__off"));
                input.Add(state);
            }

            // Evo's own box replaces the built-in check mark, whose image the editor theme swaps on hover and focus
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("evo-toggle__box");
            input.Add(box);

            // The light check mark is tinted per skin, like EvoEditorGUI's, so it reads on the filled box in both
            var check = new VisualElement { pickingMode = PickingMode.Ignore };
            check.style.backgroundImage = EvoEditorGUI.GetIcon("d_checkmark", false);
            check.AddToClassList("evo-toggle__check");
            box.Add(check);

            var dash = new VisualElement { pickingMode = PickingMode.Ignore };
            dash.AddToClassList("evo-toggle__dash");
            box.Add(dash);

            if (onChanged != null)
                toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));

            return toggle;
        }

        static Label CreateToggleState(string text, string className)
        {
            var label = new Label(text ?? "") { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        // Unity marks mixed values, as when several objects are edited at once, only on its own check mark, which Evo's box replaces
        sealed class EvoToggle : Toggle
        {
            public EvoToggle(string label) : base(label) { }

            protected override void UpdateMixedValueContent()
            {
                base.UpdateMixedValueContent();
                EnableInClassList("evo-toggle--mixed", showMixedValue);
            }
        }

        /// <summary>
        /// Dropdown in EvoEditorGUI.Dropdown's style: the label and the editor's popup on a rounded fill.
        /// onChanged gets the chosen index. With a null label there's no row, and the popup alone is shaped like a button.
        /// </summary>
        public static DropdownField CreateDropdown(string label, List<string> choices, int index, string tooltip, Action<int> onChanged = null)
        {
            var dropdown = new DropdownField(label, choices, index) { tooltip = tooltip };
            dropdown.AddToClassList("evo-dropdown");

            // Only the popup opens the menu, so it takes the pointer itself and lights up on its own, not when the label is hovered
            VisualElement popup = dropdown.Q(className: DropdownField.inputUssClassName);
            popup.pickingMode = PickingMode.Position;

            // On the row's fill, the popup takes the lighter one, like the editor's own popup
            if (!string.IsNullOrEmpty(label))
                popup.AddToClassList("evo-inverted");

            if (onChanged != null)
                dropdown.RegisterValueChangedCallback(_ => onChanged(dropdown.index));

            return dropdown;
        }

        /// <summary>
        /// Changes a dropdown's options and choice without calling its change handler. An index outside the options clears the choice.
        /// </summary>
        public static void SetChoices(DropdownField dropdown, List<string> choices, int index)
        {
            dropdown.choices = choices;
            dropdown.SetValueWithoutNotify(index >= 0 && index < choices.Count ? choices[index] : "");
        }

        /// <summary>
        /// Text field in Evo's style: the editor's input on a rounded fill, with an outline on hover and the accent color while typing,
        /// like the search field. With a label it's a row like CreateDropdown's, and the input takes the lighter fill, like the popup.
        /// A multiline field wraps its text.
        /// </summary>
        public static TextField CreateTextField(string label, string value, string tooltip, Action<string> onChanged = null, bool multiline = false)
        {
            var field = new TextField(label) { value = value ?? "", tooltip = tooltip, multiline = multiline };
            field.AddToClassList("evo-text-field");
            if (multiline)
                field.AddToClassList("evo-text-field--multiline");

            if (!string.IsNullOrEmpty(label))
                field.Q(className: TextField.inputUssClassName).AddToClassList("evo-inverted");

            if (onChanged != null)
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));

            return field;
        }

        /// <summary>
        /// Object field in Evo's style: with a label it's a row like CreateDropdown's, and the editor's object picker takes the lighter fill.
        /// Scene objects are allowed only when asked for, since most Evo fields pick assets, such as a folder.
        /// </summary>
        public static ObjectField CreateObjectField(string label, Type objectType, Object value, string tooltip,
            Action<Object> onChanged = null, bool allowSceneObjects = false)
        {
            var field = new ObjectField(label) { objectType = objectType, allowSceneObjects = allowSceneObjects, tooltip = tooltip };
            field.SetValueWithoutNotify(value);
            field.AddToClassList("evo-object-field");

            if (!string.IsNullOrEmpty(label))
                field.Q(className: ObjectField.inputUssClassName).AddToClassList("evo-inverted");

            if (onChanged != null)
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));

            return field;
        }
        #endregion

        #region Callout
        /// <summary>
        /// Message box in EvoEditorGUI.Callout's style: an icon and wrapping text on a rounded fill, tinted for warnings and errors.
        /// The message supports rich text. Change it later with SetCallout.
        /// </summary>
        public static VisualElement CreateCallout(string message, CalloutType type = CalloutType.Default)
        {
            var callout = new VisualElement();
            callout.AddToClassList("evo-callout");

            var icon = new Image { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("evo-callout__icon");
            callout.Add(icon);

            var text = new Label();
            text.AddToClassList("evo-callout__text");
            callout.Add(text);

            SetCallout(callout, message, type);
            return callout;
        }

        public static void SetCallout(VisualElement callout, string message, CalloutType type = CalloutType.Default)
        {
            callout.Q<Label>(className: "evo-callout__text").text = message ?? "";
            callout.Q<Image>(className: "evo-callout__icon").image = type switch
            {
                CalloutType.Warning => EvoEditorGUI.GetIcon("Warning", false),
                CalloutType.Error => EvoEditorGUI.GetIcon("Error", false),
                _ => EvoEditorGUI.GetIcon("Info", false)
            };

            callout.EnableInClassList("evo-callout--warning", type == CalloutType.Warning);
            callout.EnableInClassList("evo-callout--error", type == CalloutType.Error);
        }
        #endregion

        #region Search
        /// <summary>
        /// Search field in Evo's style: a magnifier, the text, and a clear button on a rounded fill whose outline shows on hover
        /// and takes the accent color while typing. The tooltip gets the platform's shortcut for HandleSearchKeys appended,
        /// such as "(Ctrl+F)". The window adds its own class to size and place it, and evo-inverted for the lighter fill.
        /// </summary>
        public static ToolbarSearchField CreateSearchField(string tooltip, Action<string> onChanged)
        {
            string shortcut = Application.platform == RuntimePlatform.OSXEditor ? "Cmd+F" : "Ctrl+F";
            var field = new EvoSearchField { tooltip = $"{tooltip} ({shortcut})", placeholderText = "Search" };
            field.AddToClassList("evo-search");

            // The magnifier only marks the field, so a click on it goes to the field and starts typing.
            // Both icons are the light ones, tinted per skin like the toggle's check mark.
            var searchButton = field.Q<Button>(className: ToolbarSearchField.searchButtonUssClassName);
            searchButton.pickingMode = PickingMode.Ignore;
            searchButton.style.backgroundImage = EvoEditorGUI.GetIcon("d_SearchWindow", false);

            // The clear button shows an icon, since a text cross sits off center in the editor font.
            // Clearing leaves the caret in the field, so the next search can be typed right away.
            var cancelButton = field.Q<Button>(className: ToolbarSearchField.cancelButtonUssClassName);
            cancelButton.tooltip = "Clear";
            MakeIconButton(cancelButton, EvoEditorGUI.GetIcon("d_clear", false));
            cancelButton.clicked += field.Focus;

            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue ?? ""));
            return field;
        }

        // Unity drops the placeholder's color only when the text changes with a notification or the field gains focus,
        // so text a window restores with SetValueWithoutNotify would otherwise look like the placeholder
        sealed class EvoSearchField : ToolbarSearchField
        {
            public override void SetValueWithoutNotify(string newValue)
            {
                base.SetValueWithoutNotify(newValue);
                if (!string.IsNullOrEmpty(newValue))
                    this.Q(className: textInputUssClassName)?.RemoveFromClassList(TextField.placeholderUssClassName);
            }
        }

        /// <summary>
        /// Handles the keys a search field shares with the results it filters: Ctrl+F (Cmd+F on macOS) focuses the field, Escape clears it,
        /// and Down moves focus to the results, then calls onEnterResults so the window can select something.
        /// Call it first from a key handler registered with TrickleDown. Returns true when it handled the key.
        /// </summary>
        public static bool HandleSearchKeys(KeyDownEvent evt, ToolbarSearchField searchField, VisualElement results, Action onEnterResults = null)
        {
            if (evt.actionKey && evt.keyCode == KeyCode.F)
            {
                searchField.Q<TextField>()?.Focus();
                evt.StopPropagation();
                return true;
            }

            if (evt.target is not VisualElement target || !searchField.Contains(target))
                return false;

            switch (evt.keyCode)
            {
                case KeyCode.Escape when !string.IsNullOrEmpty(searchField.value):
                    searchField.value = "";
                    break;

                case KeyCode.DownArrow:
                    results.Focus();
                    onEnterResults?.Invoke();
                    break;

                default:
                    return false;
            }

            evt.StopPropagation();
            return true;
        }
        #endregion

        #region Menus
        /// <summary>
        /// Menu status for an option that shows a check mark while it's chosen.
        /// </summary>
        public static DropdownMenuAction.Status MenuChecked(bool isChecked)
        {
            return isChecked ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        /// <summary>
        /// Clears a switcher menu and adds a checkable entry per asset, labelled so duplicate names stay distinguishable.
        /// A separator follows when there are assets, so the window's own actions can go below.
        /// </summary>
        public static void RebuildAssetMenu<T>(DropdownMenu menu, List<T> assets, Func<T> current, Action<T> select) where T : Object
        {
            menu.MenuItems().Clear();

            List<string> labels = GetMenuLabels(assets);
            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                menu.AppendAction(labels[i], _ => select(asset), _ => MenuChecked(current() == asset));
            }

            if (assets.Count > 0)
                menu.AppendSeparator();
        }
        #endregion
    }
}