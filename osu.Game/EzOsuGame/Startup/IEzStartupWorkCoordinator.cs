// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Schedules Ez startup preload work around official Intro / MainMenu / BDSP timing.
    /// </summary>
    public interface IEzStartupWorkCoordinator
    {
        void OnPrepareMenuLoad();

        void OnLoadMenu();

        /// <summary>
        /// MainMenu finished offscreen load during intro.
        /// </summary>
        void OnMainMenuOffscreenReady(IEzStartupSongSelectPreloadHost songSelectHost);

        void OnMainMenuEntered(IEzStartupSongSelectPreloadHost songSelectHost);
    }
}
