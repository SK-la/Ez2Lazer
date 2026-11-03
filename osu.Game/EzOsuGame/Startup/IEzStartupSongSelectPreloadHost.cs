// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Host for preloading <see cref="SoloSongSelect"/> under a screen that shares the OsuScreenStack dependency context.
    /// </summary>
    public interface IEzStartupSongSelectPreloadHost
    {
        void ScheduleSongSelectPreload();

        /// <summary>
        /// Resume song-select preload after main menu UI settle window.
        /// </summary>
        void ScheduleSongSelectPreloadAfterUiSettle();

        bool TryConsumePreloadedSongSelect(out SoloSongSelect? screen);

        void LogSongSelectPreloadStatus(string context);
    }
}
