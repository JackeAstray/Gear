using UnityEditor;

namespace Evo.UI.Editor
{
    public static class ToolsMenu
    {
        [MenuItem("Tools/Evo UI/Open Default Styler", false, 0)]
        static void OpenDefaultStyler() => Selection.activeObject = Styler.GetDefaultPreset();

        [MenuItem("Tools/Evo UI/UI Catalog", false, 11)]
        public static void ShowCatalog() => CatalogWindow.ShowWindow();

        [MenuItem("Tools/Evo UI/Styler Browser", false, 12)]
        public static void OpenStylerBrowser() => StylerBrowserWindow.GetWindow<StylerBrowserWindow>().Show();

        [MenuItem("Tools/Evo UI/Icon Browser", false, 13)]
        public static void ShowIconBrowser() => IconBrowserWindow.GetWindow<IconBrowserWindow>().Show();

        [MenuItem("Tools/Evo UI/Sprite Optimizer", false, 14)]
        public static void ShowSpriteOptimizer() => SpriteOptimizerWindow.GetWindow<SpriteOptimizerWindow>().Show();

        [MenuItem("Tools/Evo UI/Online Documentation", false, 44)]
        static void OpenDocs() => UnityEngine.Application.OpenURL(Constants.HelpUrl);

        [MenuItem("Tools/Evo UI/Discord Server", false, 45)]
        static void JoinDiscord() => UnityEngine.Application.OpenURL("https://discord.gg/VXpHyUt");
    }
}