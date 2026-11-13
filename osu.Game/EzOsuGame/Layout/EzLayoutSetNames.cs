// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Theme layout packs under <see cref="EzModifyPath.CONFIG_ROOT_PATH"/> as <c>layout</c> or <c>layout-{suffix}</c>.
    /// </summary>
    public static class EzLayoutSetNames
    {
        public const string THEME_PREFIX = "layout-";

        public static bool IsLayoutSetDirectory(string directoryName) =>
            !string.IsNullOrEmpty(directoryName)
            && (directoryName.Equals(EzModifyPath.LAYOUT_SET_DEFAULT, StringComparison.OrdinalIgnoreCase)
                || directoryName.StartsWith(THEME_PREFIX, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Folder name for a theme suffix. Empty or whitespace maps to the default <c>layout</c> pack.
        /// </summary>
        public static string DirectoryNameForThemeSuffix(string? themeSuffix)
        {
            if (string.IsNullOrWhiteSpace(themeSuffix))
                return EzModifyPath.LAYOUT_SET_DEFAULT;

            string trimmed = themeSuffix.Trim();

            if (trimmed.Equals(EzModifyPath.LAYOUT_SET_DEFAULT, StringComparison.OrdinalIgnoreCase))
                return EzModifyPath.LAYOUT_SET_DEFAULT;

            if (trimmed.StartsWith(THEME_PREFIX, StringComparison.OrdinalIgnoreCase))
                return trimmed;

            return THEME_PREFIX + trimmed;
        }

        public static string Normalize(string? layoutSetName)
        {
            if (string.IsNullOrWhiteSpace(layoutSetName))
                return EzModifyPath.LAYOUT_SET_DEFAULT;

            string trimmed = layoutSetName.Trim();

            return IsLayoutSetDirectory(trimmed)
                ? trimmed.Equals(EzModifyPath.LAYOUT_SET_DEFAULT, StringComparison.OrdinalIgnoreCase)
                    ? EzModifyPath.LAYOUT_SET_DEFAULT
                    : trimmed
                : DirectoryNameForThemeSuffix(trimmed);
        }

        /// <summary>
        /// Suffix after <c>layout-</c>, or empty for the default pack.
        /// </summary>
        public static string ThemeSuffix(string layoutSetName)
        {
            layoutSetName = Normalize(layoutSetName);

            if (layoutSetName.Equals(EzModifyPath.LAYOUT_SET_DEFAULT, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return layoutSetName.StartsWith(THEME_PREFIX, StringComparison.OrdinalIgnoreCase)
                ? layoutSetName[THEME_PREFIX.Length..]
                : layoutSetName;
        }
    }
}
