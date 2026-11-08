// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    /// <summary>
    /// Mania-specific constraints for whether sunnySunnySR can be calculated for a beatmap.
    /// </summary>
    public static class EzManiaSunnyStarRating
    {
        /// <summary>
        /// Revision of the SunnySR algorithm, owned by <see cref="EzSSRUnit"/>.
        /// </summary>
        /// <remarks>
        /// The number is not defined here: the ruleset only reports it. Bumping it is done at the owner, so the
        /// stored rows and this provider can never disagree.
        /// </remarks>
        public static int VERSION => EzSSRUnit.VERSION;

        public static bool IsPatternSupported(IBeatmap beatmap)
        {
            int keyCount = beatmap is ManiaBeatmap maniaBeatmap && maniaBeatmap.TotalColumns > 0
                ? maniaBeatmap.TotalColumns
                : Math.Max(1, (int)Math.Round(beatmap.Difficulty.CircleSize));

            return keyCount >= 1 && keyCount <= CrossMatrixProvider.MAX_SUPPORTED_KEY_COUNT && CrossMatrixProvider.GetMatrix(keyCount) != null;
        }
    }
}
