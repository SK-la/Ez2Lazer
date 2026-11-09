// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The single entry point for live chart analysis. Consumers ask this instead of running their own pass, so
    /// every surface sees the same values for the same chart, ruleset and mods.
    /// </summary>
    /// <remarks>
    /// <see cref="TryGet"/> is for read paths (HUD, labels): it never computes, so a miss must be handled by the
    /// caller as "not available yet". <see cref="GetOrCompute"/> is for batch work that runs off the update thread
    /// and legitimately needs to fill the store.
    /// </remarks>
    public interface IEzChartAnalysisLookup
    {
        /// <summary>The cached result for this chart + mods, or null when nothing has been computed yet.</summary>
        EzAnalysisResult? TryGet(BeatmapInfo beatmap, IReadOnlyList<Mod>? mods);

        /// <summary>
        /// The result for this chart + mods, computing it into the shared store when absent.
        /// </summary>
        /// <remarks>Must not be called on the update thread: the pass is synchronous and heavy.</remarks>
        EzAnalysisResult? GetOrCompute(BeatmapInfo beatmap, RulesetInfo ruleset, IReadOnlyList<Mod>? mods);
    }
}
