// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Developer knobs for startup pipeline A/B testing. Not persisted to config.
    /// </summary>
    public static class EzStartupTuning
    {
        /// <summary>
        /// Preload scheduling strategy. Default: <see cref="EzStartupPreloadTiming.PipelineDefault"/>.
        /// </summary>
        public static EzStartupPreloadTiming PreloadTiming { get; set; } = EzStartupPreloadTiming.PipelineDefault;

        /// <summary>
        /// BDSP <c>StartupBackfillDelay</c> in seconds. Default 0 — run Realm backfill during intro so carousel Replace storms finish before song select.
        /// Increase for A/B testing (e.g. 2 vs 5) if bulk backfill should defer past early main menu.
        /// </summary>
        public static int BdspStartupBackfillDelaySeconds { get; set; } = 0;

        /// <summary>
        /// If BDSP startup processing has not finished by this many ms after main menu entry, song select preload starts anyway.
        /// </summary>
        public static double SongSelectPreloadFallbackDelayMs { get; set; } = 30_000;

        /// <summary>
        /// After MainMenu becomes visible, defer resuming song-select preload until UI settle (logo/button fade) completes.
        /// </summary>
        public static double MainMenuUiSettleBeforeHeavyWorkMs { get; set; } = 600;

        /// <summary>
        /// First carousel filter after entering song select from main menu; staggers with screen fade-in.
        /// </summary>
        public static double SongSelectEnterFilterDelayMs { get; set; } = 400;

        /// <summary>
        /// When true, settings section async preload waits for BDSP startup processing to finish
        /// instead of starting from game load. Set false for A/B against immediate preload.
        /// </summary>
        public static bool DeferSettingsPreloadUntilBdspFinished { get; set; } = true;

        /// <summary>
        /// After BDSP finishes, wait this long before settings async preload (song select preload uses its own +500ms buffer).
        /// Spreads Gen2 pressure on the main menu.
        /// </summary>
        public static double SettingsPreloadDelayAfterBdspMs { get; set; } = 1_500;

        /// <summary>
        /// Master switch for the high-level <see cref="EzStartupTrace.Log"/> milestones in a release build.
        /// Default off; enable to trace startup on a shipped build. Fine-grained investigation lines use
        /// <see cref="EzStartupTrace.LogDiagnostic"/> and are debug-build only regardless of this flag.
        /// </summary>
        public static bool EnableStartupTrace { get; set; }

        /// <summary>
        /// Diagnostic: skip the settings-section async preload entirely.
        /// Used to A/B whether the startup frame-rate dip is caused by settings preload or by other
        /// startup work (BDSP backfill, song-select preload). Sections then load on first PopIn instead.
        /// </summary>
        public static bool SkipSettingsPreload { get; set; }
    }
}
