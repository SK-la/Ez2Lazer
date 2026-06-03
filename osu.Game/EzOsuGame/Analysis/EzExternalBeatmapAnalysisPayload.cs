// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Offline analysis produced by an externally hosted ruleset (BMS, future contributors).
    /// Committed into the same Realm baseline + main SQLite NoMod slice standard song select expects.
    /// </summary>
    public readonly record struct EzExternalBeatmapAnalysisPayload(
        double? StarRating,
        double? XxyStarRating,
        double? PerformancePoints,
        EzAnalysisResult? NoModSlice);
}
