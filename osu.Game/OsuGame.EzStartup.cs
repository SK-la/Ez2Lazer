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
        private IEzStartupContentPreloader startupContentPreloader = null!;
        private IEzStartupWorkCoordinator startupWorkCoordinator = null!;

        /// <summary>
        /// Creates a song select screen. Overridable for tests and preloading.
        /// </summary>
        protected virtual SoloSongSelect CreateSongSelectScreen() => new SoloSongSelect();

        private void loadStartupContentPreloader()
        {
            var factory = new SongSelectScreenFactory(this);
            dependencies.CacheAs<ISongSelectScreenFactory>(factory);

            var preloader = new EzStartupContentPreloader();
            preloader.Configure(Settings, detachedBeatmapStore, factory);
            startupContentPreloader = preloader;

            var coordinator = new EzStartupWorkCoordinator();
            coordinator.Configure(preloader);
            startupWorkCoordinator = coordinator;

            loadComponentSingleFile(coordinator, Add, true);
            dependencies.CacheAs<IEzStartupWorkCoordinator>(coordinator);

            loadComponentSingleFile(preloader, Add, true);
            dependencies.CacheAs<IEzStartupContentPreloader>(preloader);

            EzStartupTrace.Log("OsuGame startup preloader registered");
            // Settings overlay is queued earlier in the constructor; start section preload here (after the chain is submitted)
            // so it does not compete with dozens of overlays still loading — see EzStartupTrace timeline.
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
