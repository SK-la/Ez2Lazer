// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// No-op placeholder until debug enter-path tracing is wired in a later commit.
    /// </summary>
    internal static class EzSongSelectEnterTrace
    {
        public static void RecordPlayPressed(bool preloadHit)
        {
        }

        public static void RecordSongSelectEntering()
        {
        }

        public static void RecordCarouselReady()
        {
        }
    }
}
