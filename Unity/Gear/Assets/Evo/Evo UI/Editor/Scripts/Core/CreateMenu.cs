using System.IO;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI.Editor
{
    [NoAutoStaticsCleanup]
    public static class CreateMenu
    {
        const string MenuPrefix = "GameObject/Evo UI/";

        static string objectPath;

        // Unity sorts by priority and draws a separator when an item is more than 10 above the previous one.
        // A submenu takes the lowest priority among its items, so each entry gets its own value.
        // Ties are what let single items drop to the bottom after a recompile.
        // Submenus match the UI Catalog categories and the Prefabs subfolders; Essentials stay at the top level.
        enum Order
        {
            OpenCatalog = 7, // Also places Evo UI in the GameObject menu; 7 matches Unity's UI submenu
            ProceduralShape = OpenCatalog + 11,
            Image,
            Text,
            Buttons = Text + 11,
            Color,
            DataDisplay,
            DateAndTime,
            LayoutAndNavigation,
            Overlays,
            ProgressAndLoading,
            Selection,
            Sliders,
            TextInput,
            TogglesAndSwitches
        }

        static void GetObjectPath()
        {
            // Return cached path unless the folder was moved or deleted
            if (!string.IsNullOrEmpty(objectPath) && AssetDatabase.IsValidFolder(objectPath.TrimEnd('/')))
                return;

            // The runtime assembly definition sits at the Evo UI root, wherever the folder lives
            string asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Evo.UI");
            if (!string.IsNullOrEmpty(asmdefPath) && TrySetObjectPath(Path.GetDirectoryName(asmdefPath)))
                return;

            // Fallback: an Evo UI folder that has prefabs. Other packages can have their own
            // Evo UI folders (e.g. Evo Tween's module), so the name alone isn't enough.
            foreach (string guid in AssetDatabase.FindAssets("Evo UI t:folder"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "Evo UI" && TrySetObjectPath(path))
                    return;
            }

            // Return to default if no fallback
            objectPath = "Assets/Evo/Evo UI/Prefabs/";
        }

        static bool TrySetObjectPath(string rootPath)
        {
            string prefabsPath = rootPath.Replace('\\', '/') + "/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabsPath))
                return false;

            objectPath = prefabsPath + "/";
            return true;
        }

        static Canvas GetCanvas()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
                return canvas;

            // Create if not found
            var canvasGO = new GameObject("Canvas", typeof(Canvas));
            canvas = canvasGO.GetComponent<Canvas>();
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Set options
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1440);
            scaler.matchWidthOrHeight = 0.5f;

            // Create EventSystem if not present
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventGO = new GameObject("Event System", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventGO, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            Selection.activeObject = canvasGO;
            return canvas;
        }

        internal static string GetPrefabPath(string path)
        {
            GetObjectPath();
            return objectPath + path + ".prefab";
        }

        /// <summary>
        /// Selected scene object, then the open prefab's root, then the scene canvas.
        /// Returns null instead of creating a canvas when <paramref name="createCanvas"/> is false.
        /// </summary>
        internal static Transform GetDefaultParent(bool createCanvas = true)
        {
            // Selected assets can't be parents
            GameObject selected = Selection.activeGameObject;
            if (selected != null && !EditorUtility.IsPersistent(selected))
                return selected.transform;

            return GetRootParent(createCanvas);
        }

        /// <summary>
        /// Same as <see cref="GetDefaultParent"/> but ignores the selection.
        /// </summary>
        internal static Transform GetRootParent(bool createCanvas = true)
        {
            // Keep Prefab Mode creations inside the prefab instead of the main scene
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                return prefabStage.prefabContentsRoot.transform;

            if (createCanvas)
                return GetCanvas().transform;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }

        internal static GameObject CreateObject(string path, Transform parent = null)
        {
            string fullPath = GetPrefabPath(path);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Evo UI", $"Prefab not found at: {fullPath}\nPlease check your Evo UI installation.", "OK");
                return null;
            }

            // Unlike Object.Instantiate, prefab instantiation runs OnValidate on inactive objects too, like a scene load.
            // Otherwise their refreshes (relinking generated objects) would wait for the next load and dirty the scene.
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fullPath));
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Templates ship with TextMesh Pro; convert the whole unpacked copy so they stay untouched
            if (TextProvider.Active != TMPTextBackend.Instance)
                TextConverter.ConvertHierarchy(clone, TextProvider.Active, allTexts: true);

            clone.transform.SetParent(parent != null ? parent : GetDefaultParent(), false);

            Undo.RegisterCreatedObjectUndo(clone, $"Create {clone.name}");
            Selection.activeObject = clone;
            return clone;
        }

        internal static GameObject CreateQuickAccess(string name, Transform parent = null)
        {
            GameObject obj = CreateObject("Essentials/" + name, parent);
            ApplyDefaultPreset(obj);
            return obj;
        }

        internal static void ApplyDefaultPreset(GameObject obj)
        {
            if (obj != null && obj.TryGetComponent(out IStylerHandler handler))
                handler.Preset = Styler.GetDefaultPreset(false, true);
        }

        [MenuItem(MenuPrefix + "Open Catalog...", false, (int)Order.OpenCatalog)]
        static void OpenCatalog() => CatalogWindow.ShowWindow();

        [MenuItem(MenuPrefix + "Procedural Shape", false, (int)Order.ProceduralShape)]
        static void CreateProceduralShape() => CreateQuickAccess("Procedural Shape");

        [MenuItem(MenuPrefix + "Image", false, (int)Order.Image)]
        static void CreateImage() => CreateQuickAccess("Image");

        [MenuItem(MenuPrefix + "Text", false, (int)Order.Text)]
        static void CreateText() => CreateQuickAccess("Text");

        [MenuItem(MenuPrefix + "Buttons/Button", false, (int)Order.Buttons)]
        static void CreateButton() => CreateObject("Buttons/Button");

        [MenuItem(MenuPrefix + "Buttons/Button (Outline)", false, (int)Order.Buttons + 20)]
        static void CreateButtonOutline() => CreateObject("Buttons/Button (Outline)");

        [MenuItem(MenuPrefix + "Buttons/Button (Pill)", false, (int)Order.Buttons + 21)]
        static void CreateButtonPill() => CreateObject("Buttons/Button (Pill)");

        [MenuItem(MenuPrefix + "Buttons/Button (Pill Outline)", false, (int)Order.Buttons + 22)]
        static void CreateButtonPillOutline() => CreateObject("Buttons/Button (Pill Outline)");

        [MenuItem(MenuPrefix + "Buttons/Button (Icon Only)", false, (int)Order.Buttons + 40)]
        static void CreateButtonIconOnly() => CreateObject("Buttons/Button (Icon Only)");

        [MenuItem(MenuPrefix + "Buttons/Button (Magnetic Sway)", false, (int)Order.Buttons + 41)]
        static void CreateButtonMagneticSway() => CreateObject("Buttons/Button (Magnetic Sway)");

        [MenuItem(MenuPrefix + "Buttons/Progress Button", false, (int)Order.Buttons + 60)]
        static void CreateProgressButton() => CreateObject("Buttons/Progress Button");

        [MenuItem(MenuPrefix + "Buttons/Progress Button (Alt)", false, (int)Order.Buttons + 61)]
        static void CreateProgressButtonAlt() => CreateObject("Buttons/Progress Button (Alt)");

        [MenuItem(MenuPrefix + "Color/Color Picker (Compact)", false, (int)Order.Color)]
        static void CreateColorPickerCompact() => CreateObject("Color/Color Picker (Compact)");

        [MenuItem(MenuPrefix + "Color/Color Picker (Square)", false, (int)Order.Color + 1)]
        static void CreateColorPickerSquare() => CreateObject("Color/Color Picker (Square)");

        [MenuItem(MenuPrefix + "Color/Color Picker (Wheel)", false, (int)Order.Color + 2)]
        static void CreateColorPickerWheel() => CreateObject("Color/Color Picker (Wheel)");

        [MenuItem(MenuPrefix + "Color/Color Picker (Palette)", false, (int)Order.Color + 3)]
        static void CreateColorPickerPalette() => CreateObject("Color/Color Picker (Palette)");

        [MenuItem(MenuPrefix + "Data Display/Horizontal Chart", false, (int)Order.DataDisplay)]
        static void CreateHorizontalChart() => CreateObject("Data Display/Horizontal Chart");

        [MenuItem(MenuPrefix + "Data Display/Vertical Chart", false, (int)Order.DataDisplay + 1)]
        static void CreateVerticalChart() => CreateObject("Data Display/Vertical Chart");

        [MenuItem(MenuPrefix + "Data Display/Line Chart", false, (int)Order.DataDisplay + 2)]
        static void CreateLineChart() => CreateObject("Data Display/Line Chart");

        [MenuItem(MenuPrefix + "Data Display/Pie Chart", false, (int)Order.DataDisplay + 3)]
        static void CreatePieChart() => CreateObject("Data Display/Pie Chart");

        [MenuItem(MenuPrefix + "Data Display/Radar Chart", false, (int)Order.DataDisplay + 4)]
        static void CreateRadarChart() => CreateObject("Data Display/Radar Chart");

        [MenuItem(MenuPrefix + "Data Display/List View", false, (int)Order.DataDisplay + 20)]
        static void CreateListView() => CreateObject("Data Display/List View");

        [MenuItem(MenuPrefix + "Data Display/Counter", false, (int)Order.DataDisplay + 21)]
        static void CreateCounter() => CreateObject("Data Display/Counter");

        [MenuItem(MenuPrefix + "Data Display/Showcase Panel", false, (int)Order.DataDisplay + 22)]
        static void CreateShowcasePanel() => CreateObject("Data Display/Showcase Panel");

        [MenuItem(MenuPrefix + "Date and Time/Calendar", false, (int)Order.DateAndTime)]
        static void CreateCalendar() => CreateObject("Date and Time/Calendar");

        [MenuItem(MenuPrefix + "Date and Time/Time Picker", false, (int)Order.DateAndTime + 1)]
        static void CreateTimePicker() => CreateObject("Date and Time/Time Picker");

        [MenuItem(MenuPrefix + "Date and Time/Timer", false, (int)Order.DateAndTime + 20)]
        static void CreateTimer() => CreateObject("Date and Time/Timer");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Horizontal)", false, (int)Order.DateAndTime + 21)]
        static void CreateTimerHorizontal() => CreateObject("Date and Time/Timer (Horizontal)");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Vertical)", false, (int)Order.DateAndTime + 22)]
        static void CreateTimerVertical() => CreateObject("Date and Time/Timer (Vertical)");

        [MenuItem(MenuPrefix + "Date and Time/Timer (Radial)", false, (int)Order.DateAndTime + 23)]
        static void CreateTimerRadial() => CreateObject("Date and Time/Timer (Radial)");

        [MenuItem(MenuPrefix + "Layout and Navigation/Page Manager", false, (int)Order.LayoutAndNavigation)]
        static void CreatePageManager() => CreateObject("Layout and Navigation/Page Manager");

        [MenuItem(MenuPrefix + "Layout and Navigation/Tab Manager", false, (int)Order.LayoutAndNavigation + 1)]
        static void CreateTabManager() => CreateObject("Layout and Navigation/Tab Manager");

        [MenuItem(MenuPrefix + "Layout and Navigation/Stepper", false, (int)Order.LayoutAndNavigation + 2)]
        static void CreateStepper() => CreateObject("Layout and Navigation/Stepper");

        [MenuItem(MenuPrefix + "Layout and Navigation/Carousel", false, (int)Order.LayoutAndNavigation + 20)]
        static void CreateCarousel() => CreateObject("Layout and Navigation/Carousel");

        [MenuItem(MenuPrefix + "Layout and Navigation/Scroll View (Horizontal)", false, (int)Order.LayoutAndNavigation + 40)]
        static void CreateScrollViewHorizontal() => CreateObject("Layout and Navigation/Scroll View (Horizontal)");

        [MenuItem(MenuPrefix + "Layout and Navigation/Scroll View (Vertical)", false, (int)Order.LayoutAndNavigation + 41)]
        static void CreateScrollViewVertical() => CreateObject("Layout and Navigation/Scroll View (Vertical)");

        [MenuItem(MenuPrefix + "Layout and Navigation/Scrollbar (Horizontal)", false, (int)Order.LayoutAndNavigation + 42)]
        static void CreateScrollbarHorizontal() => CreateObject("Layout and Navigation/Scrollbar (Horizontal)");

        [MenuItem(MenuPrefix + "Layout and Navigation/Scrollbar (Vertical)", false, (int)Order.LayoutAndNavigation + 43)]
        static void CreateScrollbarVertical() => CreateObject("Layout and Navigation/Scrollbar (Vertical)");

        [MenuItem(MenuPrefix + "Layout and Navigation/Menu Bar", false, (int)Order.LayoutAndNavigation + 60)]
        static void CreateMenuBar() => CreateObject("Layout and Navigation/Menu Bar");

        [MenuItem(MenuPrefix + "Overlays/Toast Notification", false, (int)Order.Overlays)]
        static void CreateNotification() => CreateObject("Overlays/Toast Notification");

        [MenuItem(MenuPrefix + "Overlays/Modal Window", false, (int)Order.Overlays + 1)]
        static void CreateModalWindow() => CreateObject("Overlays/Modal Window");

        [MenuItem(MenuPrefix + "Overlays/Modal Window (Alt)", false, (int)Order.Overlays + 2)]
        static void CreateModalWindowAlt() => CreateObject("Overlays/Modal Window (Alt)");

        [MenuItem(MenuPrefix + "Overlays/Popover", false, (int)Order.Overlays + 3)]
        static void CreatePopover() => CreateObject("Overlays/Popover");

        [MenuItem(MenuPrefix + "Progress and Loading/Progress Bar (Horizontal)", false, (int)Order.ProgressAndLoading)]
        static void CreateProgressBarHorizontal() => CreateObject("Progress and Loading/Progress Bar (Horizontal)");

        [MenuItem(MenuPrefix + "Progress and Loading/Progress Bar (Vertical)", false, (int)Order.ProgressAndLoading + 1)]
        static void CreateProgressBarVertical() => CreateObject("Progress and Loading/Progress Bar (Vertical)");

        [MenuItem(MenuPrefix + "Progress and Loading/Progress Bar (Radial)", false, (int)Order.ProgressAndLoading + 2)]
        static void CreateProgressBarRadial() => CreateObject("Progress and Loading/Progress Bar (Radial)");

        [MenuItem(MenuPrefix + "Progress and Loading/Distribution Bar", false, (int)Order.ProgressAndLoading + 3)]
        static void CreateDistributionBar() => CreateObject("Progress and Loading/Distribution Bar");

        [MenuItem(MenuPrefix + "Progress and Loading/Spinner (Horizontal)", false, (int)Order.ProgressAndLoading + 20)]
        static void CreateSpinnerHorizontal() => CreateObject("Progress and Loading/Spinner (Horizontal)");

        [MenuItem(MenuPrefix + "Progress and Loading/Spinner (Vertical)", false, (int)Order.ProgressAndLoading + 21)]
        static void CreateSpinnerVertical() => CreateObject("Progress and Loading/Spinner (Vertical)");

        [MenuItem(MenuPrefix + "Progress and Loading/Spinner (Radial)", false, (int)Order.ProgressAndLoading + 22)]
        static void CreateSpinnerRadial() => CreateObject("Progress and Loading/Spinner (Radial)");

        [MenuItem(MenuPrefix + "Selection/Dropdown", false, (int)Order.Selection)]
        static void CreateDropdown() => CreateObject("Selection/Dropdown");

        [MenuItem(MenuPrefix + "Selection/Dropdown (Input)", false, (int)Order.Selection + 1)]
        static void CreateDropdownInput() => CreateObject("Selection/Dropdown (Input)");

        [MenuItem(MenuPrefix + "Selection/Dropdown (Multi Select)", false, (int)Order.Selection + 2)]
        static void CreateDropdownMultiSelect() => CreateObject("Selection/Dropdown (Multi Select)");

        [MenuItem(MenuPrefix + "Selection/Selector", false, (int)Order.Selection + 20)]
        static void CreateSelector() => CreateObject("Selection/Selector");

        [MenuItem(MenuPrefix + "Selection/Quantity Selector", false, (int)Order.Selection + 21)]
        static void CreateQuantitySelector() => CreateObject("Selection/Quantity Selector");

        [MenuItem(MenuPrefix + "Sliders/Slider", false, (int)Order.Sliders)]
        static void CreateSlider() => CreateObject("Sliders/Slider");

        [MenuItem(MenuPrefix + "Sliders/Slider (Input)", false, (int)Order.Sliders + 2)]
        static void CreateSliderInput() => CreateObject("Sliders/Slider (Input)");

        [MenuItem(MenuPrefix + "Sliders/Slider (Alt)", false, (int)Order.Sliders + 1)]
        static void CreateSliderAlt() => CreateObject("Sliders/Slider (Alt)");

        [MenuItem(MenuPrefix + "Sliders/Radial Slider", false, (int)Order.Sliders + 20)]
        static void CreateRadialSlider() => CreateObject("Sliders/Radial Slider");

        [MenuItem(MenuPrefix + "Sliders/Radial Slider (Knob)", false, (int)Order.Sliders + 21)]
        static void CreateRadialSliderKnob() => CreateObject("Sliders/Radial Slider (Knob)");

        [MenuItem(MenuPrefix + "Sliders/Range Slider", false, (int)Order.Sliders + 40)]
        static void CreateRangeSlider() => CreateObject("Sliders/Range Slider");

        [MenuItem(MenuPrefix + "Sliders/Comparison Slider", false, (int)Order.Sliders + 60)]
        static void CreateComparisonSlider() => CreateObject("Sliders/Comparison Slider");

        [MenuItem(MenuPrefix + "Text Input/Input Field", false, (int)Order.TextInput)]
        static void CreateInputField() => CreateObject("Text Input/Input Field");

        [MenuItem(MenuPrefix + "Text Input/Input Field (Icon)", false, (int)Order.TextInput + 1)]
        static void CreateInputFieldIcon() => CreateObject("Text Input/Input Field (Icon)");

        [MenuItem(MenuPrefix + "Text Input/Input Field (Minimal)", false, (int)Order.TextInput + 2)]
        static void CreateInputFieldMinimal() => CreateObject("Text Input/Input Field (Minimal)");

        [MenuItem(MenuPrefix + "Text Input/Input Field (Multi Line)", false, (int)Order.TextInput + 20)]
        static void CreateInputFieldMultiLine() => CreateObject("Text Input/Input Field (Multi Line)");

        [MenuItem(MenuPrefix + "Text Input/Input Field (Multi Line Scrollbar)", false, (int)Order.TextInput + 21)]
        static void CreateInputFieldMultiLineScrollbar() => CreateObject("Text Input/Input Field (Multi Line Scrollbar)");

        [MenuItem(MenuPrefix + "Toggles and Switches/Toggle", false, (int)Order.TogglesAndSwitches)]
        static void CreateToggle() => CreateObject("Toggles and Switches/Toggle");

        [MenuItem(MenuPrefix + "Toggles and Switches/Switch", false, (int)Order.TogglesAndSwitches + 20)]
        static void CreateSwitch() => CreateObject("Toggles and Switches/Switch");

        [MenuItem(MenuPrefix + "Toggles and Switches/Switch (Indicator)", false, (int)Order.TogglesAndSwitches + 21)]
        static void CreateSwitchIndicator() => CreateObject("Toggles and Switches/Switch (Indicator)");

        [MenuItem(MenuPrefix + "Toggles and Switches/Switch (Label)", false, (int)Order.TogglesAndSwitches + 22)]
        static void CreateSwitchLabel() => CreateObject("Toggles and Switches/Switch (Label)");

        [MenuItem(MenuPrefix + "Toggles and Switches/Switch (Indicator + Label)", false, (int)Order.TogglesAndSwitches + 23)]
        static void CreateSwitchIndicatorAndLabel() => CreateObject("Toggles and Switches/Switch (Indicator + Label)");

        [MenuItem(MenuPrefix + "Toggles and Switches/Radio Button", false, (int)Order.TogglesAndSwitches + 40)]
        static void CreateRadioButton() => CreateObject("Toggles and Switches/Radio Button");

        [MenuItem(MenuPrefix + "Toggles and Switches/Radio Button (Alt)", false, (int)Order.TogglesAndSwitches + 41)]
        static void CreateRadioButtonAlt() => CreateObject("Toggles and Switches/Radio Button (Alt)");

        [MenuItem(MenuPrefix + "Toggles and Switches/Radio Button Group", false, (int)Order.TogglesAndSwitches + 60)]
        static void CreateRadioButtonGroup() => CreateObject("Toggles and Switches/Radio Button Group");

        [MenuItem(MenuPrefix + "Toggles and Switches/Radio Button Group (Alt)", false, (int)Order.TogglesAndSwitches + 61)]
        static void CreateRadioButtonGroupAlt() => CreateObject("Toggles and Switches/Radio Button Group (Alt)");
    }
}