// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationCardDifficultyDisplay
    {
        public static double ResolveBadgeRating(BeatmapInfo beatmap)
            => beatmap.SupportsSunnyStarRating()
                ? beatmap.GetPersistedSunnyStarRating() ?? beatmap.StarRating
                : beatmap.StarRating;

        public static IconUsage ResolveBadgeIcon(BeatmapInfo beatmap)
            => beatmap.SupportsSunnyStarRating() ? FontAwesome.Solid.Moon : FontAwesome.Solid.Star;
    }
}
