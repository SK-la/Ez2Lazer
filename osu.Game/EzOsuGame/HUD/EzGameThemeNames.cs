// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.HUD
{
    public static class EzGameThemeNames
    {
        public const string Default = "Celeste_Lumiere";

        public static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? Default : value;
    }
}
