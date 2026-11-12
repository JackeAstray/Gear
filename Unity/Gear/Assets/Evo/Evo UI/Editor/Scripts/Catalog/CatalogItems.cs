using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.UI.Editor
{
    internal sealed class CatalogCategory
    {
        public string Name { get; }

        /// <summary>
        /// Prefab folder and GameObject menu name. Ampersands become "and", since Unity reads them as menu shortcuts.
        /// </summary>
        public string FolderName { get; }
        public IReadOnlyList<CatalogItem> Items { get; }

        public CatalogCategory(string name, params CatalogItem[] items)
        {
            Name = name;
            FolderName = name.Replace(" & ", " and ");
            Items = items;

            foreach (CatalogItem item in items)
                item.Category = this;
        }

        /// <summary>
        /// Name of the item whose component icon represents the category. Defaults to the first item.
        /// </summary>
        public string IconItem { get; set; }

        public Texture Icon
        {
            get
            {
                foreach (CatalogItem item in Items)
                {
                    if (item.Name == IconItem)
                        return item.Icon;
                }

                return Items.Count > 0 ? Items[0].Icon : null;
            }
        }
    }

    internal sealed class CatalogItem
    {
        readonly bool quickAccess;
        readonly string searchText;
        GameObject prefab;
        Component mainComponent;

        /// <summary>
        /// Prefab file name, unique across the catalog. Also used as a stable ID.
        /// </summary>
        public string Name { get; }
        public string Description { get; }
        public CatalogCategory Category { get; internal set; }
        public int Order { get; internal set; }

        /// <summary>
        /// Prefab path relative to the Evo UI Prefabs folder, without the extension.
        /// </summary>
        public string PrefabPath => Category.FolderName + "/" + Name;

        public CatalogItem(string name, string description, string tags = "", bool quickAccess = false)
        {
            Name = name;
            Description = description;
            this.quickAccess = quickAccess;
            searchText = (Name + " " + tags).ToLowerInvariant();
        }

        public string AssetPath => CreateMenu.GetPrefabPath(PrefabPath);

        public GameObject Prefab
        {
            get
            {
                if (prefab == null)
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
                    mainComponent = FindMainComponent(prefab);
                }

                return prefab;
            }
        }

        /// <summary>
        /// The root component that defines the element, such as Button or Slider.
        /// </summary>
        public Component MainComponent => Prefab != null ? mainComponent : null;

        public string ComponentName => MainComponent != null ? ObjectNames.NicifyVariableName(MainComponent.GetType().Name) : null;

        public Texture Icon
        {
            get
            {
                if (MainComponent != null)
                    return EditorGUIUtility.ObjectContent(mainComponent, mainComponent.GetType()).image;

                return Prefab != null ? AssetPreview.GetMiniThumbnail(prefab) : null;
            }
        }

        public string DocumentationUrl
        {
            get
            {
                var helpUrl = MainComponent != null ? mainComponent.GetType().GetCustomAttribute<HelpURLAttribute>(true) : null;
                return helpUrl != null ? helpUrl.URL : Constants.HelpUrl;
            }
        }

        public GameObject Create(Transform parent = null, int siblingIndex = -1)
        {
            GameObject obj = quickAccess
                ? CreateMenu.CreateQuickAccess(Name, parent)
                : CreateMenu.CreateObject(PrefabPath, parent);

            if (obj != null && siblingIndex >= 0)
                obj.transform.SetSiblingIndex(siblingIndex);

            return obj;
        }

        /// <summary>
        /// Gives preview instances the same setup as created objects.
        /// </summary>
        public void PrepareInstance(GameObject instance)
        {
            if (quickAccess)
                CreateMenu.ApplyDefaultPreset(instance);
        }

        /// <summary>
        /// Returns -1 when any term is missing, otherwise lower is a better match.
        /// </summary>
        public int GetMatchScore(string query, string[] terms)
        {
            if (terms.Length == 0)
                return 0;

            string component = ComponentName;
            string categoryName = Category.Name.ToLowerInvariant();
            bool allInName = true;

            foreach (string term in terms)
            {
                if (Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                allInName = false;
                if (searchText.Contains(term) || categoryName.Contains(term)
                    || (component != null && component.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                return -1;
            }

            if (Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 0;

            if (Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            return allInName ? 2 : 3;
        }

        static Component FindMainComponent(GameObject root)
        {
            if (root == null)
                return null;

            // Prefer Evo components; Image and Text roots fall back to their graphic
            Component fallback = null;
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour is StylerObject)
                    continue;

                string space = behaviour.GetType().Namespace;
                if (space != null && space.StartsWith("Evo", StringComparison.Ordinal))
                    return behaviour;

                fallback = fallback != null ? fallback : behaviour;
            }
            return fallback;
        }
    }

    /// <summary>
    /// Everything the Catalog window offers, in the same order as the GameObject/Evo UI menu.
    /// Each category maps to a Prefabs subfolder; add new prefabs here as well as in <see cref="CreateMenu"/>.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class Catalog
    {
        public static readonly CatalogCategory[] Categories =
        {
            new("Essentials",
                new CatalogItem("Procedural Shape", "Anti-aliased vector shape with fills, patterns, outlines, and shadows. A good base for panels and backgrounds.", "shape rectangle circle rounded outline shadow vector sdf panel background", true),
                new CatalogItem("Image", "Image that takes its look from your Styler preset.", "sprite picture icon graphic", true),
                new CatalogItem("Text", "Text that takes its color and text style from your Styler preset.", "label tmp textmeshpro font typography", true)),

            new("Buttons",
                new CatalogItem("Button", "Button with an optional icon and label, styled by your Styler preset.", "click press action cta"),
                new CatalogItem("Button (Outline)", "Outlined variant of the standard button.", "click press action border secondary"),
                new CatalogItem("Button (Pill)", "Pill variant of the standard button.", "click press action pill"),
                new CatalogItem("Button (Pill Outline)", "Pill, outlined variant of the standard button.", "click press action pill border secondary"),
                new CatalogItem("Button (Icon Only)", "Compact button that shows only an icon.", "click press action icon compact"),
                new CatalogItem("Button (Magnetic Sway)", "Button that sways smoothly toward the cursor.", "click press action hover magnetic motion"),
                new CatalogItem("Progress Button", "Hold-to-confirm button that fills up while pressed.", "click hold confirm long press"),
                new CatalogItem("Progress Button (Alt)", "Alternative style of the hold-to-confirm button.", "click hold confirm long press")),

            new("Color",
                new CatalogItem("Color Picker (Compact)", "Compact color picker.", "colour hue rgb hex"),
                new CatalogItem("Color Picker (Square)", "Color picker with a saturation and value square.", "colour hue rgb hex"),
                new CatalogItem("Color Picker (Wheel)", "Color picker with a hue wheel.", "colour hue rgb hex radial circle"),
                new CatalogItem("Color Picker (Palette)", "Picks from a predefined set of colors.", "colour swatch swatches preset"))
            { IconItem = "Color Picker (Palette)" },

            new("Data Display",
                new CatalogItem("Horizontal Chart", "Horizontal bar chart.", "chart graph bar data statistics"),
                new CatalogItem("Vertical Chart", "Vertical bar (column) chart.", "chart graph bar column data statistics"),
                new CatalogItem("Line Chart", "Line chart for trends over time.", "chart graph line trend data statistics"),
                new CatalogItem("Pie Chart", "Pie chart for proportions.", "chart graph pie donut share data statistics"),
                new CatalogItem("Radar Chart", "Radar chart for comparing several attributes.", "chart graph radar spider web stats data"),
                new CatalogItem("List View", "Table-style list with reusable cells.", "table grid rows columns data list"),
                new CatalogItem("Counter", "Animated number display with rolling digits.", "number score odometer value animated"),
                new CatalogItem("Showcase Panel", "Rotating panel of featured items with an auto-advance timer.", "featured banner hero slideshow news"))
            { IconItem = "Pie Chart" },

            new("Date & Time",
                new CatalogItem("Calendar", "Month calendar for picking dates.", "date day month year schedule"),
                new CatalogItem("Time Picker", "Time picker with hour, minute, and AM/PM wheels.", "clock hour minute wheel"),
                new CatalogItem("Timer", "Countdown, count-up, or system clock display.", "countdown clock stopwatch"),
                new CatalogItem("Timer (Horizontal)", "Timer with a horizontal progress bar.", "countdown clock stopwatch bar"),
                new CatalogItem("Timer (Vertical)", "Timer with a vertical progress bar.", "countdown clock stopwatch bar"),
                new CatalogItem("Timer (Radial)", "Timer with a circular progress ring.", "countdown clock stopwatch radial circle ring")),

            new("Layout & Navigation",
                new CatalogItem("Page Manager", "Swipeable pages with animated transitions.", "pages swipe pager screens"),
                new CatalogItem("Tab Manager", "Tabbed container that switches between panels.", "tabs tab bar panels"),
                new CatalogItem("Stepper", "Step indicator for multi-step flows.", "steps wizard progress onboarding"),
                new CatalogItem("Carousel", "Auto-sliding carousel with indicators.", "slides slideshow gallery banner"),
                new CatalogItem("Scroll View (Horizontal)", "Horizontally scrolling content area.", "scroll list container"),
                new CatalogItem("Scroll View (Vertical)", "Vertically scrolling content area.", "scroll list container"),
                new CatalogItem("Scrollbar (Horizontal)", "Horizontal scrollbar.", "scroll"),
                new CatalogItem("Scrollbar (Vertical)", "Vertical scrollbar.", "scroll"),
                new CatalogItem("Menu Bar", "Desktop-style menu bar with dropdown menus.", "menu file toolbar navigation")),

            new("Overlays",
                new CatalogItem("Toast Notification", "Toast notification with open and close animations.", "toast alert message snackbar"),
                new CatalogItem("Modal Window", "Dialog window for messages and confirmations.", "dialog popup window alert confirm"),
                new CatalogItem("Modal Window (Alt)", "Dialog window with a Negative confirm button, for destructive actions like delete.", "dialog popup window alert confirm negative danger destructive delete remove"),
                new CatalogItem("Popover", "Animated popover panel that opens and closes on demand.", "popup panel flyout"))
               { IconItem = "Modal Window" },

            new("Progress & Loading",
                new CatalogItem("Progress Bar (Horizontal)", "Horizontal progress bar.", "loading percent bar fill"),
                new CatalogItem("Progress Bar (Vertical)", "Vertical progress bar.", "loading percent bar fill"),
                new CatalogItem("Progress Bar (Radial)", "Circular progress indicator.", "loading percent radial circle ring"),
                new CatalogItem("Distribution Bar", "Segmented bar showing how a total splits across values.", "segments share stacked breakdown"),
                new CatalogItem("Spinner (Horizontal)", "Looping horizontal loading indicator.", "loading busy wait loader"),
                new CatalogItem("Spinner (Vertical)", "Looping vertical loading indicator.", "loading busy wait loader"),
                new CatalogItem("Spinner (Radial)", "Looping circular loading indicator.", "loading busy wait loader radial circle")),

            new("Selection",
                new CatalogItem("Dropdown", "Dropdown for choosing one option from a list.", "select combo combobox picker list menu"),
                new CatalogItem("Dropdown (Input)", "Dropdown that filters its items as you type.", "select combo combobox picker list search autocomplete filter"),
                new CatalogItem("Dropdown (Multi Select)", "Dropdown for choosing several options.", "select combo combobox picker list multiple checkbox"),
                new CatalogItem("Selector", "Shows one option at a time with arrow navigation and indicators.", "option choice arrows horizontal carousel cycle"),
                new CatalogItem("Quantity Selector", "Number stepper with increase and decrease buttons.", "number stepper increment decrement amount count")),

            new("Sliders",
                new CatalogItem("Slider", "Slider for picking a value from a range.", "range value volume number"),
                new CatalogItem("Slider (Input)", "Slider with an editable value field.", "range value volume number field"),
                new CatalogItem("Slider (Alt)", "Alternative, bigger-styled slider.", "range value volume number"),
                new CatalogItem("Radial Slider", "Circular slider.", "range value radial circle dial"),
                new CatalogItem("Radial Slider (Knob)", "Rotary knob variant of the radial slider.", "range value radial circle dial knob rotary"),
                new CatalogItem("Range Slider", "Slider with two handles for selecting a range.", "range min max between interval"),
                new CatalogItem("Comparison Slider", "Before and after slider that reveals one side over the other.", "before after compare reveal image")),

            new("Text Input",
                new CatalogItem("Input Field", "Single-line text input with focus animations.", "text type entry textbox textfield form"),
                new CatalogItem("Input Field (Icon)", "Text input with an icon.", "text type entry textbox textfield form search"),
                new CatalogItem("Input Field (Minimal)", "Minimal-style text input.", "text type entry textbox textfield form"),
                new CatalogItem("Input Field (Multi Line)", "Multi-line text area.", "text type entry textarea paragraph form"),
                new CatalogItem("Input Field (Multi Line Scrollbar)", "Multi-line text area with a scrollbar.", "text type entry textarea paragraph form scroll")),

            new("Toggles & Switches",
                new CatalogItem("Toggle", "Checkbox for turning an option on or off.", "checkbox check bool boolean option"),
                new CatalogItem("Switch", "On/off switch with an animated handle.", "toggle bool boolean on off"),
                new CatalogItem("Switch (Indicator)", "Switch variant with a state indicator.", "toggle bool boolean on off"),
                new CatalogItem("Switch (Label)", "Switch with a text label.", "toggle bool boolean on off"),
                new CatalogItem("Switch (Indicator + Label)", "Switch with both a state indicator and a text label.", "toggle bool boolean on off"),
                new CatalogItem("Radio Button", "Single option to place inside a radio button group.", "option choice select exclusive"),
                new CatalogItem("Radio Button (Alt)", "Alternative style of the radio button.", "option choice select exclusive"),
                new CatalogItem("Radio Button Group", "Group of mutually exclusive options.", "option choice select exclusive segmented"),
                new CatalogItem("Radio Button Group (Alt)", "Alternative style of the radio button group.", "option choice select exclusive segmented"))
            { IconItem = "Switch" }
        };

        public static readonly IReadOnlyList<CatalogItem> Items = CollectItems();

        static readonly Dictionary<string, CatalogItem> itemsById = CreateLookup();

        public static CatalogItem Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return itemsById.TryGetValue(Path.GetFileName(id), out CatalogItem item) ? item : null;
        }

        public static CatalogCategory FindCategory(string name)
        {
            foreach (CatalogCategory category in Categories)
            {
                if (category.Name == name)
                    return category;
            }
            return null;
        }

        static List<CatalogItem> CollectItems()
        {
            var items = new List<CatalogItem>();
            foreach (CatalogCategory category in Categories)
            {
                foreach (CatalogItem item in category.Items)
                {
                    item.Order = items.Count;
                    items.Add(item);
                }
            }
            return items;
        }

        static Dictionary<string, CatalogItem> CreateLookup()
        {
            var lookup = new Dictionary<string, CatalogItem>();
            foreach (CatalogItem item in Items)
                lookup[item.Name] = item;

            return lookup;
        }
    }
}