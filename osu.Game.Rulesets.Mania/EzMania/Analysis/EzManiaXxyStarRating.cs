// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    /// <summary>
    /// Mania-specific constraints for whether sunnyxxy SR can be calculated for a beatmap.
    /// </summary>
    public static class EzManiaXxyStarRating
    {
        /// <summary>
        /// yyyyMMdd revision of the Mania xxy SR algorithm. Bump when calculation logic changes (independent of official <see cref="Difficulty.ManiaDifficultyCalculator.Version"/>).
        /// </summary>
        public const int VERSION = 20250415;

        public static bool IsPatternSupported(IBeatmap beatmap)
        {
            int keyCount = beatmap is ManiaBeatmap maniaBeatmap && maniaBeatmap.TotalColumns > 0
                ? maniaBeatmap.TotalColumns
                : Math.Max(1, (int)Math.Round(beatmap.BeatmapInfo.Difficulty.CircleSize));

            return keyCount < 11 || keyCount % 2 == 0;
        }
    }
}
