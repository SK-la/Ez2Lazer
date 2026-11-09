// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Developer knobs for carousel / detached beatmap store sync during bulk Realm metadata writes. Not persisted.
    /// </summary>
    public static class EzDetachedBeatmapStoreTuning
    {
        /// <summary>
        /// After a screen push/exit, carousel Replace drain is muted for this many milliseconds.
        /// </summary>
        public static double ScreenTransitionMuteMs { get; set; } = 450;

        /// <summary>
        /// When true, drain only runs inside repeating time windows (see <see cref="DrainIntervalMs"/>).
        /// </summary>
        public static bool UseDrainIntervalGating { get; set; }

        /// <summary>
        /// Spacing between drain windows when <see cref="UseDrainIntervalGating"/> is true.
        /// </summary>
        public static double DrainIntervalMs { get; set; } = 300;

        /// <summary>
        /// Length of each drain window when interval gating is enabled.
        /// </summary>
        public static double DrainWindowMs { get; set; } = 120;

        /// <summary>
        /// Maximum update-thread time spent applying pending detached-store ops per frame.
        /// </summary>
        public static double DrainTimeBudgetMs { get; set; } = 0.75;

        /// <summary>
        /// Hard cap on ops per frame (safety rail at very high FPS).
        /// </summary>
        public static int DrainMaxOpsPerFrame { get; set; } = 4;
    }
}
