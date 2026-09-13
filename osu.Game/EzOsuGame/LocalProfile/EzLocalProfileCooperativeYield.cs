// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Cooperative yielding for the analysis hot loops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loops used to call <c>Thread.Sleep(1)</c> every batch to stay polite. On Windows the default timer
    /// resolution is ~15.6ms, so each of those calls parked the worker for about 16ms — a long analysis doing it
    /// hundreds of times produced exactly the "long intermittent freeze" pattern the GC-saturating passes showed.
    /// </para>
    /// <para>
    /// <see cref="Thread.Yield"/> hands the rest of the current time slice to another ready thread with no timer
    /// involvement, and costs microseconds when there is nothing else to run. Doing it on every batch would still
    /// burn a syscall per batch, so it is rate-limited to once per <see cref="yield_interval_ms"/>.
    /// </para>
    /// </remarks>
    public static class EzLocalProfileCooperativeYield
    {
        private const long yield_interval_ms = 4;

        [ThreadStatic]
        private static long lastYieldTimestamp;

        /// <summary>
        /// Yield the current time slice if at least <see cref="yield_interval_ms"/> have passed since the last call
        /// on this thread. Otherwise do nothing (and no syscall).
        /// </summary>
        public static void MaybeYield()
        {
            long now = Stopwatch.GetTimestamp();
            long last = lastYieldTimestamp;

            if (last != 0 && (now - last) * 1000 / Stopwatch.Frequency < yield_interval_ms)
                return;

            lastYieldTimestamp = now;
            Thread.Yield();
        }
    }
}
