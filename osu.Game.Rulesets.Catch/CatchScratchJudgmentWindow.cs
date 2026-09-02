// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Catch
{
    internal static class CatchScratchJudgmentWindow
    {
        internal const double EARLY_MS = 10;
        internal const double LATE_MS = 20;

        internal static double EarlyWindow(bool assist) => assist ? EARLY_MS : 0;

        internal static double LateWindow(bool assist) => assist ? LATE_MS : 0;

        internal static bool ShouldBeginChecking(double timeOffset, bool assist) =>
            timeOffset >= -EarlyWindow(assist);

        internal static bool ShouldApplyMiss(double timeOffset, bool assist) =>
            timeOffset > LateWindow(assist);
    }
}
