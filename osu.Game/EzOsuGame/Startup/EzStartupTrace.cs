// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Logging;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Debug-only startup metrics. Search runtime logs for <c>[EzStartupTrace]</c>.
    /// Each line is prefixed with <c>+elapsed ms</c> from process start.
    /// Kept events: BDSP finished, settings async preload duration, main menu frame cost, EnterPath timings.
    /// </summary>
    public static class EzStartupTrace
    {
        private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public static long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        public static void Log(string message)
        {
            Logger.Log($"[EzStartupTrace] +{stopwatch.ElapsedMilliseconds}ms {message}", LoggingTarget.Runtime, LogLevel.Debug);
        }
    }
}
