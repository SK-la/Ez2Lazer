// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.ComponentModel;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Graphics
{
    public static class OsuFont
    {
        /// <summary>
        /// The default font size.
        /// </summary>
        public const float DEFAULT_FONT_SIZE = 16;

        private static readonly Dictionary<Typeface, string> family_overrides = new Dictionary<Typeface, string>();

        /// <summary>
        /// Template font styles which should be preferred whenever possible for UI elements.
        /// </summary>
        public static class Style
        {
            /// <summary>
            /// Equivalent to Torus with 32px size and semi-bold weight.
            /// </summary>
            public static FontUsage Title => GetFont(Typeface.TorusAlternate, size: 32, weight: FontWeight.Regular);

            /// <summary>
            /// Default UI typeface with 28px size and regular weight.
            /// </summary>
            public static FontUsage Subtitle => GetFont(size: 28, weight: FontWeight.Regular);

            /// <summary>
            /// Default UI typeface with 22px size and bold weight.
            /// </summary>
            public static FontUsage Heading1 => GetFont(size: 22, weight: FontWeight.Bold);

            /// <summary>
            /// Default UI typeface with 18px size and semi-bold weight.
            /// </summary>
            public static FontUsage Heading2 => GetFont(size: 18, weight: FontWeight.SemiBold);

            /// <summary>
            /// Default UI typeface with 16px size and regular weight.
            /// </summary>
            public static FontUsage Body => GetFont(size: DEFAULT_FONT_SIZE, weight: FontWeight.Regular);

            /// <summary>
            /// Default UI typeface with 14px size and regular weight.
            /// </summary>
            public static FontUsage Caption1 => GetFont(size: 14, weight: FontWeight.Regular);

            /// <summary>
            /// Default UI typeface with 12px size and regular weight.
            /// </summary>
            public static FontUsage Caption2 => GetFont(size: 12, weight: FontWeight.Regular);
        }

        /// <summary>
        /// The default font.
        /// </summary>
        public static FontUsage Default => GetFont(weight: FontWeight.Medium);

        /// <summary>
        /// Font face for numeric display.
        /// </summary>
        public static FontUsage Numeric => GetFont(Typeface.Venera, weight: FontWeight.Bold);

        /// <summary>
        /// Default font face for UI and game elements.
        /// </summary>
        public static FontUsage Torus => GetFont(Typeface.Torus, weight: FontWeight.Regular);

        /// <summary>
        /// Default font face with alternate character set for headings and flair text.
        /// </summary>
        public static FontUsage TorusAlternate => GetFont(Typeface.TorusAlternate, weight: FontWeight.Regular);

        public static FontUsage Inter => GetFont(Typeface.Inter, weight: FontWeight.Regular);

        /// <summary>
        /// Remap a built-in <see cref="Typeface"/> to an alternate <see cref="FontUsage"/> family id
        /// (e.g. a registered system outline font). Pass null/empty to clear.
        /// </summary>
        public static void SetFamilyOverride(Typeface typeface, string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                family_overrides.Remove(typeface);
            else
                family_overrides[typeface] = familyName;
        }

        public static void ClearFamilyOverrides() => family_overrides.Clear();

        public static bool HasFamilyOverride(Typeface typeface) => family_overrides.ContainsKey(typeface);

        /// <summary>
        /// Retrieves a <see cref="FontUsage"/>.
        /// </summary>
        public static FontUsage GetFont(Typeface typeface = Typeface.Torus, float size = DEFAULT_FONT_SIZE, FontWeight weight = FontWeight.Medium, bool italics = false, bool fixedWidth = false)
        {
            string familyString = GetFamilyString(typeface);

            // System outline fonts are registered under a single stable family id without BMFont weight suffixes.
            string weightString = HasFamilyOverride(typeface) ? null : GetWeightString(familyString, weight);

            return new FontUsage(familyString, size, weightString, getItalics(italics), fixedWidth);
        }

        private static bool getItalics(in bool italicsRequested)
        {
            // right now none of our fonts support italics.
            // should add exceptions to this rule if they come up.
            return false;
        }

        /// <summary>
        /// Retrieves the string representation of a <see cref="Typeface"/>.
        /// </summary>
        public static string GetFamilyString(Typeface typeface)
        {
            if (family_overrides.TryGetValue(typeface, out string overrideFamily))
                return overrideFamily;

            switch (typeface)
            {
                case Typeface.Venera:
                    return @"Venera";

                case Typeface.Torus:
                    return @"Torus";

                case Typeface.TorusAlternate:
                    return @"Torus-Alternate";

                case Typeface.Inter:
                    return @"Inter";
            }

            return null;
        }

        /// <summary>
        /// Retrieves the string representation of a <see cref="FontWeight"/>.
        /// </summary>
        public static string GetWeightString(string family, FontWeight weight)
        {
            // Built-in Torus family (not remapped system fonts).
            if ((family == @"Torus" || family == @"Torus-Alternate") && weight == FontWeight.Medium)
                weight = FontWeight.Regular;

            return weight.ToString();
        }
    }

    public static class OsuFontExtensions
    {
        public static FontUsage With(this FontUsage usage, Typeface? typeface = null, float? size = null, FontWeight? weight = null, bool? italics = null, bool? fixedWidth = null)
        {
            string familyString = typeface != null ? OsuFont.GetFamilyString(typeface.Value) : usage.Family;
            string weightString = weight != null
                ? (typeface != null && OsuFont.HasFamilyOverride(typeface.Value) ? null : OsuFont.GetWeightString(familyString, weight.Value))
                : usage.Weight;

            if (typeface != null && OsuFont.HasFamilyOverride(typeface.Value))
                weightString = null;

            return usage.With(familyString, size, weightString, italics, fixedWidth);
        }
    }

    public enum Typeface
    {
        Venera,
        Torus,

        [Description("Torus (alternate)")]
        TorusAlternate,
        Inter,
    }

    public enum FontWeight
    {
        Light = 300,
        Regular = 400,
        Medium = 500,
        SemiBold = 600,
        Bold = 700,
        Black = 900
    }
}
