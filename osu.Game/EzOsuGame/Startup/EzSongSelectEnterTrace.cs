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
    }
}
