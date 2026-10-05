// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Startup;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Select;

namespace osu.Game
{
    public partial class OsuGame
    {
        /// <summary>
        /// Creates a song select screen. Overridable for tests and preloading.
        /// </summary>
        protected virtual SoloSongSelect CreateSongSelectScreen() => new SoloSongSelect();

        private void loadStartupContentPreloader()
        {
            var factory = new SongSelectScreenFactory(this);
            Dependencies.CacheAs<ISongSelectScreenFactory>(factory);

            var preloader = new EzStartupContentPreloader();
            preloader.Configure(Settings, factory);

            var coordinator = new EzStartupWorkCoordinator();
            coordinator.Configure(preloader);

            loadComponentSingleFile(coordinator, Add, true);
            Dependencies.CacheAs<IEzStartupWorkCoordinator>(coordinator);

            loadComponentSingleFile(preloader, Add, true);
            Dependencies.CacheAs<IEzStartupContentPreloader>(preloader);

            preloader.ScheduleSettingsPreload();
        }

        private void onScreenExitedForStartupPreload(IOsuScreen? lastScreen, IOsuScreen? newScreen)
        {
            if (lastScreen is SoloSongSelect && newScreen is MainMenu mainMenu)
                mainMenu.ScheduleSongSelectPreloadAfterUiSettle();
        }

        private partial class SongSelectScreenFactory : ISongSelectScreenFactory
        {
            private readonly OsuGame game;

            public SongSelectScreenFactory(OsuGame game) => this.game = game;

            public SoloSongSelect Create() => game.CreateSongSelectScreen();
        }
    }
}
