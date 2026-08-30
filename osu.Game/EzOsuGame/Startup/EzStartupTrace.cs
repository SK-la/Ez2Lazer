// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// No-op placeholder until debug timeline logging is wired in a later commit.
    /// </summary>
    internal static class EzStartupTrace
    {
        public static long ElapsedMilliseconds => 0;

        public static void Log(string message)
        {
        }
    }
}
