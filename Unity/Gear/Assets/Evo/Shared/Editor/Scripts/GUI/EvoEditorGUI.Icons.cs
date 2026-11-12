using Unity.Scripting.LifecycleManagement;

namespace Evo.EditorTools
{
    [NoAutoStaticsCleanup]
    public static partial class IconLibrary
    {
        public static readonly IconDefinition Add = IconDefinition.Custom("Add");
        public static readonly IconDefinition Collapse = IconDefinition.Native("icon dropdown open");
        public static readonly IconDefinition Delete = IconDefinition.Custom("Delete");
        public static readonly IconDefinition Edit = IconDefinition.Custom("Edit");
        public static readonly IconDefinition Expand = IconDefinition.Native("icon dropdown");
        public static readonly IconDefinition Help = IconDefinition.Native("_Help");
        public static readonly IconDefinition Info = IconDefinition.Native("Info");
        public static readonly IconDefinition Link = IconDefinition.Native("Linked");
        public static readonly IconDefinition More = IconDefinition.Native("More");
        public static readonly IconDefinition Object = IconDefinition.Native("GameObject Icon");
        public static readonly IconDefinition Opacity = IconDefinition.Native("Grid.FillTool");
        public static readonly IconDefinition Pause = IconDefinition.Native("PauseButton");
        public static readonly IconDefinition Play = IconDefinition.Native("PlayButton");
        public static readonly IconDefinition Preset = IconDefinition.Custom("Preset");
        public static readonly IconDefinition Refresh = IconDefinition.Native("Refresh");
        public static readonly IconDefinition Reorder = IconDefinition.Custom("Reorder");
        public static readonly IconDefinition Stop = IconDefinition.Native("PreMatQuad");
        public static readonly IconDefinition ToggleOn = IconDefinition.Custom("ToggleOn");
        public static readonly IconDefinition ToggleOff = IconDefinition.Custom("ToggleOff");
        public static readonly IconDefinition Unlink = IconDefinition.Native("UnLinked");
    }

    public readonly struct IconDefinition
    {
        internal string Name { get; }
        internal bool IsCustom { get; }

        IconDefinition(string name, bool isCustom)
        {
            Name = name;
            IsCustom = isCustom;
        }

        public static IconDefinition Custom(string name) => new(name, true);

        public static IconDefinition Native(string name) => new(name, false);
    }
}