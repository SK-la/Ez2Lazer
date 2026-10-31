// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Startup metrics. Search runtime logs for <c>[EzStartupTrace]</c>.
    /// Each line is prefixed with <c>+elapsed ms</c> from process start.
    ///
    /// Two entry points:
    /// <list type="bullet">
    /// <item><see cref="LogDiagnostic"/> — fine-grained investigation lines. Debug builds only; nothing is written in a release build.</item>
    /// <item><see cref="Log"/> — high-level milestones that stay useful in a release build. Gated by <see cref="EzStartupTuning.EnableStartupTrace"/> (default off).</item>
    /// </list>
    /// </summary>
    public static class EzStartupTrace
    {
        private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public static long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// High-level startup milestones (BDSP, song-select preload, settings ready, effective GC mode, enter timings).
        /// Runs in a release build when <see cref="EzStartupTuning.EnableStartupTrace"/> is enabled.
        /// </summary>
        public static void Log(string message)
        {
            if (!EzStartupTuning.EnableStartupTrace)
                return;

            write(message);
        }

        /// <summary>
        /// Fine-grained investigation lines (per-section timings, frame/GC sampling, sweep timings).
        /// Debug builds only; never written in a release build.
        /// </summary>
        public static void LogDiagnostic(string message)
        {
            if (!DebugUtils.IsDebugBuild)
                return;

            write(message);
        }

        private static void write(string message)
        {
            string line = $"[EzStartupTrace] +{stopwatch.ElapsedMilliseconds}ms {message}";
            Logger.Log(line, Ez2ConfigManager.LOGGER_NAME);
        }
    }
}
