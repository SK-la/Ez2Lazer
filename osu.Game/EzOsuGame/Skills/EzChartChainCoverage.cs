// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Whether the chart-side chain will ever produce a row for a chart, so a play waiting on one is either
    /// "missing" (worth reporting and worth another pass) or "settled" (nothing will ever come of a retry).
    /// </summary>
    /// <remarks>
    /// The player pass reads chart-side data but never computes it, so a chart with no row has to be handed to the
    /// chain. Reporting a chart the chain never even looks at leaves a stale flag the status readout can never clear
    /// and re-queues the chain on every pass, so this mirrors the chain's own candidate gate
    /// (<c>BackgroundDataStoreProcessor.collectManiaChartCandidates</c> plus its keymode filter) instead of judging
    /// the chart on its own merits: a chart with no beatmap set, and a mania score played on a non-mania beatmap
    /// (convert), never reach a stage that could write a row.
    /// </remarks>
    internal static class EzChartChainCoverage
    {
        /// <summary>
        /// Whether the chain can rate a chart at all. Every stage starts from the same mania candidate list, so a
        /// chart outside it - keymode the engine cannot rate, no beatmap set left, or a convert play - is skipped by
        /// MSD first and by CSI / ChartDan after it.
        /// </summary>
        public static bool IsRateableChart(BeatmapInfo beatmapInfo)
            => beatmapInfo.BeatmapSet != null
               && beatmapInfo.Ruleset.OnlineID == EzLocalProfileConstants.MANIA_RULESET_ID
               && IsRateableKeyCount((int)Math.Round(beatmapInfo.Difficulty.CircleSize));

        /// <summary>
        /// The chain's keymode gate, mirrored exactly - including its "non-positive column count is not a skip"
        /// fallback, because a chart with no CS still enters the chain and simply fails later.
        /// </summary>
        public static bool IsRateableKeyCount(int keyCount)
            => keyCount <= 0 || EzNKeyMsdEngine.IsSupportedKeyCount(keyCount);
    }
}
