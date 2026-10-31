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

            if (!EzStartupTuning.DeferSettingsPreloadUntilBdspFinished)
                preloader.ScheduleSettingsPreload();
        }

        // 保证主菜单始终持有一个预加载完成的 SongSelect 实例。
        // 将约 400ms 的界面创建与加载成本转移到后台执行，
        // 使进入 SongSelect 的前台响应时间降低到约 3ms。
        private void startupSongSelectPreload(IOsuScreen? lastScreen, IOsuScreen? newScreen)
        {
            if (lastScreen is SoloSongSelect && newScreen is MainMenu mainMenu)
                mainMenu.ScheduleSongSelectPreloadAfterUiSettle();
        }

        // 解耦 SongSelect 的创建逻辑，使其可以在主菜单中预加载。避免 MainMenu 直接依赖 OsuGame。也
        private partial class SongSelectScreenFactory : ISongSelectScreenFactory
        {
            private readonly OsuGame game;

            public SongSelectScreenFactory(OsuGame game) => this.game = game;

            public SoloSongSelect Create() => game.CreateSongSelectScreen();
        }
    }
}
