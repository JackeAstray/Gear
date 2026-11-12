using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Evo.EditorTools
{
    public static partial class EvoEditorUITK
    {
        #region Header
        /// <summary>
        /// Header with a fixed title and a short explanation, for single-purpose tools.
        /// </summary>
        public static VisualElement CreateHeader(string title, string subtitle)
        {
            var header = new VisualElement();
            header.AddToClassList("evo-header");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("evo-title");
            header.Add(titleLabel);

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("evo-subtitle");
            header.Add(subtitleLabel);

            return header;
        }

        /// <summary>
        /// Header whose title opens a menu, such as a preset or library switcher, with a more menu and a row for details.
        /// </summary>
        public static VisualElement CreateMenuHeader(string titleTooltip, string moreTooltip,
            out ToolbarMenu titleMenu, out ToolbarMenu moreMenu, out VisualElement metaRow)
        {
            var header = new VisualElement();
            header.AddToClassList("evo-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("evo-header__title-row");
            header.Add(titleRow);

            titleMenu = new ToolbarMenu { tooltip = titleTooltip };
            titleMenu.AddToClassList("evo-title-menu");
            titleRow.Add(titleMenu);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("evo-header__spacer");
            titleRow.Add(spacer);

            moreMenu = CreateMoreMenu(moreTooltip);
            titleRow.Add(moreMenu);

            metaRow = new VisualElement();
            metaRow.AddToClassList("evo-header__meta");
            header.Add(metaRow);

            return header;
        }

        /// <summary>
        /// Borderless button with the editor's menu icon that opens a menu of actions, like the one in a menu header.
        /// </summary>
        public static ToolbarMenu CreateMoreMenu(string tooltip)
        {
            var menu = new ToolbarMenu { tooltip = tooltip };
            menu.AddToClassList("evo-more-menu");

            var icon = new Image { image = EditorGUIUtility.IconContent("_Menu").image, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("evo-more-menu__icon");
            menu.Add(icon);

            return menu;
        }

        /// <summary>
        /// Folder path for a header's detail row that shows the asset in the Project window when clicked.
        /// </summary>
        public static Label CreatePathLabel(Action onClick)
        {
            var label = new Label();
            label.AddToClassList("evo-header__path");
            label.RegisterCallback<ClickEvent>(_ => onClick());
            return label;
        }

        public static void SetPath(Label label, Object asset)
        {
            label.text = GetDisplayFolder(asset);
            label.tooltip = AssetDatabase.GetAssetPath(asset) + "\nClick to show in the Project window.";
        }
        #endregion

        #region Body
        /// <summary>
        /// Scrolling body for form windows.
        /// </summary>
        public static ScrollView CreateBody()
        {
            var body = new ScrollView(ScrollViewMode.Vertical);
            body.AddToClassList("evo-body");
            body.contentContainer.AddToClassList("evo-body__content");
            body.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return body;
        }

        /// <summary>
        /// Small capitalized heading above a group of fields or a list. Add evo-heading--first to the one at the top of a body,
        /// which has no group above it to keep apart from.
        /// </summary>
        public static Label CreateHeading(string text, string className = null)
        {
            var heading = new Label(text.ToUpperInvariant());
            heading.AddToClassList("evo-heading");
            if (!string.IsNullOrEmpty(className))
                heading.AddToClassList(className);

            return heading;
        }

        /// <summary>
        /// Small muted text under a field or a group, such as a hint or what an option does. Add evo-description--warning to flag a problem.
        /// </summary>
        public static Label CreateDescription(string text = null, string className = null)
        {
            var description = new Label(text ?? "");
            description.AddToClassList("evo-description");
            if (!string.IsNullOrEmpty(className))
                description.AddToClassList(className);

            return description;
        }
        #endregion
    }
}