// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    public interface IEzStartupContentPreloader
    {
        /// <summary>
        /// Request settings preparation after the settings overlay has loaded.
        /// </summary>
        void ScheduleSettingsPreload();

        /// <summary>
        /// Emit a snapshot of preload state to the startup trace log.
        /// </summary>
        void LogStatus(string context);

        /// <summary>
        /// Whether settings section background construction has finished.
        /// </summary>
        bool AreSettingsLoaded { get; }

        /// <summary>
        /// Whether all settings sections and sidebar entries are mounted and ready. This can complete while the overlay remains hidden.
        /// </summary>
        bool AreSettingsReadyForDisplay { get; }
    }
}
