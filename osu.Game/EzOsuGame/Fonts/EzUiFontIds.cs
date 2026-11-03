// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Fonts
{
    /// <summary>
    /// Stable <see cref="osu.Framework.Graphics.Sprites.FontUsage"/> family ids for system outline fonts.
    /// English remaps are primary FontUsage; localized ids are empty-name fallbacks ahead of Noto CJK.
    /// </summary>
    public static class EzUiFontIds
    {
        public const string UI_DEFAULT = "EzSys-UiDefault";
        public const string UI_DEFAULT_LOCALIZED = "EzSys-UiDefault-Loc";
        public const string TITLE_ALTERNATE = "EzSys-TitleAlternate";
        public const string TITLE_ALTERNATE_LOCALIZED = "EzSys-TitleAlternate-Loc";
        public const string NUMERIC = "EzSys-Numeric";

        /// <summary>
        /// Platform colour emoji outline font, registered ahead of resource BMFont emoji for empty-name fallback.
        /// </summary>
        public const string EMOJI = "EzSys-Emoji";
    }
}
