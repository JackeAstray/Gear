using UnityEngine;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Contains all visual and layout settings used to render a ListView.
    /// </summary>
    [System.Serializable]
    public class ListViewStyle
    {
        // Text Settings
        [Tooltip("Font used by column-header labels.")]
        [TextFont] public Object headerFont;
        [Tooltip("Font used by row-cell labels.")]
        [TextFont] public Object rowFont;
        [Tooltip("Font size used by column-header labels.")]
        [Min(0)] public int headerFontSize = 24;
        [Tooltip("Font size used by row-cell labels.")]
        [Min(0)] public int rowFontSize = 24;
        [Tooltip("Font style used by column-header labels.")]
        public FontStyles headerFontStyle = FontStyles.Bold;
        [Tooltip("Font style used by row-cell labels.")]
        public FontStyles rowFontStyle = FontStyles.Normal;
        [Tooltip("Color used by column-header labels and icons.")]
        public Color headerTextColor = Color.white;
        [Tooltip("Color used by row-cell labels and icons.")]
        public Color rowTextColor = Color.black;

        // Layout Settings
        [Tooltip("Height of the header row in pixels.")]
        [Min(0)] public float headerHeight = 60;
        [Tooltip("Height of each data row in pixels.")]
        [Min(0)] public float rowHeight = 60;
        [Tooltip("Horizontal space between columns in pixels. Negative values overlap neighboring columns.")]
        public float columnSpacing = 0;
        [Tooltip("Vertical space between rows in pixels. Negative values overlap neighboring rows.")]
        public float rowSpacing = 0;
        [Tooltip("Width and height of built-in cell icons in pixels.")]
        [Min(0)] public float iconSize = 16;
        [Tooltip("Space between the icon and label in built-in cells. Negative values overlap them.")]
        public float contentSpacing = 15;
        [Tooltip("Padding applied around built-in cell content.")]
        public RectOffset contentPadding = new();

        // Procedural Background Settings
        [Tooltip("Procedural fill color used by header cells.")]
        public Color headerBackgroundColor = Color.gray;
        [Tooltip("Procedural fill color used by row cells.")]
        public Color rowBackgroundColor = Color.white;
        [Tooltip("Uses a different fill color on every second row.")]
        public bool useAlternatingRowColor = false;
        [Tooltip("Procedural fill color used by alternating rows.")]
        public Color alternatingRowColor = new(0.9f, 0.9f, 0.9f, 1f);
        [Tooltip("Uniform corner radius used when Independent Corners is disabled.")]
        [Min(0)] public float cornerRadius = 0;
        [Tooltip("Allows each corner to use a different radius.")]
        public bool independentCorners = false;
        [Tooltip("Per-corner radii used when Independent Corners is enabled.")]
        public Vector4 cornerRadii = Vector4.zero;
        [Tooltip("Replaces the radius of the list's four outer corners on the first and last cells.")]
        public bool overrideOuterCorners = false;
        [Tooltip("Radius of the list's four outer corners, in the same unit as the corner radius.")]
        [Min(0)] public float outerCornerRadius = 0;
        [Tooltip("Determines whether corner radii use pixels or percentages.")]
        public ProceduralShape.RadiusMode radiusMode = ProceduralShape.RadiusMode.Pixels;
        [Tooltip("Uses smooth squircle corners instead of circular rounded corners.")]
        public bool squircleCorners = false;
        [Tooltip("Additional anti-aliasing softness around procedural cell edges.")]
        [Range(0, 32)] public float shapeSoftness = 0;
        [Tooltip("Draws an inward procedural outline around each cell.")]
        public bool showBorder = true;
        [Tooltip("Color of the procedural cell outline.")]
        public Color borderColor = Color.black;
        [Tooltip("Width of the procedural cell outline in pixels.")]
        [Min(0)] public float borderWidth = 1;

        /// <summary>
        /// Identifies a ListView value that can be mapped to a Styler preset item.
        /// </summary>
        public enum Type
        {
            HeaderFont,
            RowFont,
            HeaderTextColor,
            RowTextColor,
            HeaderBackgroundColor,
            RowBackgroundColor,
            AlternatingRowColor,
            BorderColor
        }

        /// <summary>
        /// Maps one style value to a color or font in a Styler preset.
        /// </summary>
        [System.Serializable]
        public class Mapping
        {
            [Tooltip("ListView style value controlled by this mapping.")]
            public Type type;

            [Tooltip("ID of the mapped Styler color. Empty means transparent for color mappings.")]
            public string colorID = "";

            [Tooltip("Override the alpha channel of the mapped Styler color.")]
            public bool overrideAlpha = false;

            [Tooltip("Alpha value to apply (0 = transparent, 1 = opaque).")]
            [Range(0f, 1f)] public float alphaOverride = 1f;

            [Tooltip("ID of the mapped Styler font. Empty uses the local fallback font.")]
            public string fontID = "";
        }

        internal Vector4 GetCornerRadii()
        {
            float radius = Mathf.Max(0, cornerRadius);
            if (!independentCorners)
                return new Vector4(radius, radius, radius, radius);

            return new Vector4(
                Mathf.Max(0, cornerRadii.x),
                Mathf.Max(0, cornerRadii.y),
                Mathf.Max(0, cornerRadii.z),
                Mathf.Max(0, cornerRadii.w));
        }

        public override int GetHashCode()
        {
            System.HashCode hash = new();
            hash.Add(headerFont);
            hash.Add(rowFont);
            hash.Add(headerFontSize);
            hash.Add(rowFontSize);
            hash.Add(headerFontStyle);
            hash.Add(rowFontStyle);
            hash.Add(headerTextColor);
            hash.Add(rowTextColor);
            hash.Add(headerHeight);
            hash.Add(rowHeight);
            hash.Add(columnSpacing);
            hash.Add(rowSpacing);
            hash.Add(iconSize);
            hash.Add(contentSpacing);
            hash.Add(contentPadding?.left ?? 0);
            hash.Add(contentPadding?.right ?? 0);
            hash.Add(contentPadding?.top ?? 0);
            hash.Add(contentPadding?.bottom ?? 0);
            hash.Add(headerBackgroundColor);
            hash.Add(rowBackgroundColor);
            hash.Add(alternatingRowColor);
            hash.Add(useAlternatingRowColor);
            hash.Add(cornerRadius);
            hash.Add(independentCorners);
            hash.Add(cornerRadii);
            hash.Add(overrideOuterCorners);
            hash.Add(outerCornerRadius);
            hash.Add(radiusMode);
            hash.Add(squircleCorners);
            hash.Add(shapeSoftness);
            hash.Add(showBorder);
            hash.Add(borderColor);
            hash.Add(borderWidth);
            return hash.ToHashCode();
        }
    }
}