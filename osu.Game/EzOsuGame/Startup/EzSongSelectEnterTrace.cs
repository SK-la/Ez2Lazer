// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Debug-only MainMenu Play → SongSelect push → carousel ready metrics.
    /// </summary>
    public static class EzSongSelectEnterTrace
    {
        private static double? playPressedAt;
        private static double? songSelectEnteringAt;
        private static double? gameplayStartAt;

        public static void RecordPlayPressed(bool preloadHit)
        {
            playPressedAt = EzStartupTrace.ElapsedMilliseconds;
            EzStartupTrace.Log($"EnterPath PlayPressed preloadHit={preloadHit}");
        }

        public static void RecordSongSelectEntering()
        {
            songSelectEnteringAt = EzStartupTrace.ElapsedMilliseconds;
            double? pushFromPlay = songSelectEnteringAt - playPressedAt;
            EzStartupTrace.Log($"EnterPath SongSelect.OnEntering pushFromPlay={pushFromPlay}ms");
        }

        public static void RecordCarouselReady()
        {
            double now = EzStartupTrace.ElapsedMilliseconds;
            double? fromPlay = now - playPressedAt;
            double? fromEnter = now - songSelectEnteringAt;
            EzStartupTrace.Log($"EnterPath CarouselItemsPresented fromPlay={fromPlay}ms fromEnter={fromEnter}ms");

            playPressedAt = null;
            songSelectEnteringAt = null;
        }

        /// <summary>
        /// 玩家在选歌界面按下开始（<c>SongSelect.SelectAndRun</c>），
        /// 是「进局」唯一漏斗：按钮、回车、右键菜单、转动触发都汇到这一处。
        /// </summary>
        public static void RecordGameplayStart()
        {
            gameplayStartAt = EzStartupTrace.ElapsedMilliseconds;
            EzStartupTrace.Log("EnterPath GameplayStart");
        }

        /// <summary><c>PlayerLoader</c> 进入时调用：转圈开始。</summary>
        public static void RecordLoaderEntered() => EzStartupTrace.Log("EnterPath PlayerLoader.Entered");

        /// <summary>预热门控放行时调用：转圈结束。分段用于把「解码/预热」和「Player 构建」分开看。</summary>
        public static void RecordWarmUpFinished()
        {
            double now = EzStartupTrace.ElapsedMilliseconds;
            double? fromStart = gameplayStartAt.HasValue ? now - gameplayStartAt.Value : null;
            EzStartupTrace.Log($"EnterPath GameplayStart.gateReleased fromStart={fromStart}ms");
        }

        /// <summary><c>Player</c> 进入时调用：真正进局。</summary>
        public static void RecordPlayerEntered()
        {
            double now = EzStartupTrace.ElapsedMilliseconds;
            double? fromStart = gameplayStartAt.HasValue ? now - gameplayStartAt.Value : null;
            EzStartupTrace.Log($"EnterPath Player.Entered fromStart={fromStart}ms");

            gameplayStartAt = null;
        }
    }
}
