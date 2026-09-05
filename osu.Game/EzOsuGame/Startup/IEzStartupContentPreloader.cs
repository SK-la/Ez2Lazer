// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    public interface IEzStartupContentPreloader
    {
        /// <summary>
        /// Begin preloading settings sections (lightweight).
        /// </summary>
        void BeginLight();

        /// <summary>
        /// Wait until the settings overlay is loaded, then start section preload. Call once from OsuGame after overlay chain is submitted.
        /// </summary>
        void ScheduleSettingsPreload();

        /// <summary>
        /// Warm the detached beatmap store on a background thread. Avoid running during BDSP Ez backfill.
        /// </summary>
        void ScheduleDetachWarmup();

        /// <summary>
        /// Emit a snapshot of preload state to the startup trace log.
        /// </summary>
        void LogStatus(string context);

        /// <summary>
        /// Whether settings section async construction has finished (mount may still be pending until PopIn).
        /// </summary>
        bool AreSettingsLoaded { get; }

        /// <summary>
        /// Whether settings sections are mounted and the sidebar is ready (first visible open complete).
        /// </summary>
        bool AreSettingsReadyForDisplay { get; }
    }
}
