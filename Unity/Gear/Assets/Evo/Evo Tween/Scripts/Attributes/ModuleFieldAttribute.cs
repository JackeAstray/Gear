using System;

namespace Evo.Tween
{
    /// <summary>
    /// Well-known serialized-property references supported by module field metadata.
    /// Unqualified references resolve relative to the module itself.
    /// </summary>
    public static class ModulePropertyReference
    {
        public const string SettingsPrefix = "$settings.";
        public const string Duration = SettingsPrefix + "duration";
    }

    /// <summary>
    /// Supplies layout metadata for a serialized <see cref="TweenModule"/> field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ModuleFieldAttribute : Attribute
    {
        /// <summary>
        /// Inspector section that contains the field.
        /// </summary>
        public ModuleFieldGroup Group { get; }

        /// <summary>
        /// Optional semantic role used for From/To behavior and layout.
        /// </summary>
        public ModuleFieldRole Role { get; }

        /// <summary>
        /// Sort order within the selected inspector group.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Optional label override. The field name is nicified when omitted.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Places a field in an inspector group with no special semantic role.
        /// </summary>
        public ModuleFieldAttribute(ModuleFieldGroup group, int order = 0) : this(group, ModuleFieldRole.None, order) { }

        /// <summary>
        /// Places a field in an inspector group and assigns its semantic role.
        /// </summary>
        public ModuleFieldAttribute(ModuleFieldGroup group, ModuleFieldRole role, int order = 0)
        {
            Group = group;
            Role = role;
            Order = order;
        }
    }

    /// <summary>
    /// Controls module field visibility in the tween editor using a serialized-property reference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ModuleShowIfAttribute : Attribute
    {
        public string ReferenceName { get; }
        public object[] ExpectedValues { get; }

        /// <summary>
        /// Shows the field when none of the expected values match instead.
        /// </summary>
        public bool Invert { get; set; }

        /// <summary>
        /// Shows the field when the referenced boolean is true.
        /// </summary>
        public ModuleShowIfAttribute(string referenceName) : this(referenceName, true) { }

        /// <summary>
        /// Shows the field when the referenced property matches any expected value.
        /// </summary>
        public ModuleShowIfAttribute(string referenceName, params object[] expectedValues)
        {
            ReferenceName = referenceName;
            ExpectedValues = expectedValues;
        }
    }

    /// <summary>
    /// Draws a numeric module field with bounds that can be constants or serialized-property references.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ModuleRangeAttribute : Attribute
    {
        public float Minimum { get; }
        public float Maximum { get; }
        public string MinimumReference { get; }
        public string MaximumReference { get; }

        public ModuleRangeAttribute(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public ModuleRangeAttribute(float minimum, string maximumReference)
        {
            Minimum = minimum;
            MaximumReference = maximumReference;
        }

        public ModuleRangeAttribute(string minimumReference, float maximum)
        {
            MinimumReference = minimumReference;
            Maximum = maximum;
        }

        public ModuleRangeAttribute(string minimumReference, string maximumReference)
        {
            MinimumReference = minimumReference;
            MaximumReference = maximumReference;
        }
    }
}