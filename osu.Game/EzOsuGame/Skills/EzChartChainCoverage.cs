// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Whether the chart-side chain will ever produce a row for a chart, so a play waiting on one is either
    /// "missing" (worth reporting and worth another pass) or "settled" (nothing will ever come of a retry).
    /// </summary>
    /// <remarks>
    /// The player pass reads chart-side data but never computes it, so a chart with no row has to be handed to the
    /// chain. Reporting a chart the chain will never rate would re-queue the chain and re-fold the player on every
    /// pass - the exact fail-loop <c>BackgroundDataStoreProcessor.populateMissingBeatmapMsd</c> avoids by dropping
    /// unsupported keymodes at candidate time.
    /// </remarks>
    internal static class EzChartChainCoverage
    {
        /// <summary>
        /// Whether the chain can rate a chart at all. MSD is gated on the CS-derived column count and both later
        /// stages (CSI / ChartDan) wait on MSD, so a chart outside the engine's keymode range is skipped by the whole
        /// chain.
        /// </summary>
        public static bool IsRateableChart(BeatmapInfo beatmapInfo)
            => IsRateableKeyCount((int)Math.Round(beatmapInfo.Difficulty.CircleSize));

        /// <summary>
        /// The chain's candidate gate, mirrored exactly - including its "non-positive column count is not a skip"
        /// fallback, because a chart with no CS still enters the chain and simply fails later.
        /// </summary>
        public static bool IsRateableKeyCount(int keyCount)
            => keyCount <= 0 || EzNKeyMsdEngine.IsSupportedKeyCount(keyCount);
    }
}
