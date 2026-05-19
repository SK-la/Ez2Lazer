// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Optional precomputed EZ-analysis source for beatmap panels in song select.
    /// </summary>
    /// <remarks>
    /// Rulesets that store offline PP / KPS / xxySR data (BMS <c>analytics.sqlite</c>, etc.)
    /// can <c>[Cached(typeof(IPanelEzAnalysisProvider))]</c> an implementation on the song-select
    /// screen so <see cref="PanelBeatmap"/> / <see cref="PanelBeatmapStandalone"/> reuse the same
    /// Mania EZ widgets (<see cref="osu.Game.EzOsuGame.UserInterface.EzDisplayKps"/>, …) without
    /// routing through the global <see cref="EzAnalysisCache"/>.
    /// </remarks>
    public interface IPanelEzAnalysisProvider
    {
        /// <summary>
        /// Whether Mania-style EZ widgets should be shown for <paramref name="ruleset"/> while this screen is active.
        /// </summary>
        bool SupportsEzDisplay(RulesetInfo ruleset);

        /// <summary>
        /// Resolve a bindable with the best-known stored analysis for <paramref name="beatmap"/>.
        /// Implementations may return a bindable with the default value when no row exists.
        /// </summary>
        IBindable<EzAnalysisResult> GetBindableAnalysis(BeatmapInfo beatmap, CancellationToken cancellationToken = default, int computationDelay = 0);

        /// <summary>
        /// Synchronous lookup for wedge / details UI that cannot subscribe to a bindable.
        /// </summary>
        bool TryGetStoredAnalysis(BeatmapInfo beatmap, out EzAnalysisResult result);
    }
}
