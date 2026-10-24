// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Beatmaps
{
    /// <summary>
    /// Mirrors osu!stable's split between gameplay parsing and difficulty parsing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// osu!stable loads hitobjects without range validation for gameplay, but its difficulty calculation re-parses the
    /// same file with validation and aborts on an out-of-range value (`BeatmapDifficultyCalculator.GetDifficulty`
    /// catches the exception and returns 0). Maps that rely on this - aspire maps in particular - are playable yet have
    /// no star rating or performance points.
    /// </para>
    /// <para>
    /// This class is the single place that decides whether a beatmap may be handed to a difficulty calculator, so a
    /// map stable would rate at 0 does not get a wildly inflated rating here instead.
    /// </para>
    /// </remarks>
    public static class EzStableDifficultyCompatibility
    {
        /// <summary>
        /// Whether difficulty (and therefore performance points) can be calculated for <paramref name="beatmap"/>.
        /// </summary>
        /// <remarks>
        /// Reads only a flag recorded at decode time, so this is safe to call on hot paths and never triggers a
        /// conversion or a path approximation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCalculate(IBeatmap? beatmap) => beatmap is not IStableDifficultyLimitReport report || !report.ExceedsStableDifficultyLimits;

        /// <inheritdoc cref="CanCalculate(IBeatmap)"/>
        public static bool CanCalculate(IWorkingBeatmap? workingBeatmap) => workingBeatmap == null || CanCalculate(workingBeatmap.Beatmap);
    }
}
