// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// One measured stage: wall-clock distribution, managed allocation and GC pressure.
    /// </summary>
    /// <remarks>
    /// Allocation and GC counts are per run; they are the numbers a zero-allocation refactor is expected to drive
    /// down. Wall clock is reported for context but moves with machine load, so budgets on it stay loose.
    /// </remarks>
    public readonly record struct EzPerfSample(
        string Label,
        int Runs,
        double MinMs,
        double MedianMs,
        double P95Ms,
        double MaxMs,
        double AverageKb,
        double Gen0PerRun,
        double Gen1PerRun,
        int Gen2);

    /// <summary>
    /// Measures an action the way the analysis hot path runs it: warm, then repeated on one thread.
    /// </summary>
    public static class EzPerfMeasurer
    {
        /// <summary>
        /// Runs <paramref name="action"/> <paramref name="runs"/> times after warming and forcing a full collection,
        /// returning the per-run figures. The warm-up runs are not counted, so JIT and one-time static initialisation
        /// do not land in the sample.
        /// </summary>
        public static EzPerfSample Measure(string label, int runs, Action action, int warmupRuns = 2)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);

            for (int i = 0; i < warmupRuns; i++)
                action();

            Stabilise();

            var ticks = new long[runs];
            long allocated = 0;

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);

            for (int i = 0; i < runs; i++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();

                action();

                ticks[i] = Stopwatch.GetTimestamp() - start;
                allocated += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            }

            int gen0 = GC.CollectionCount(0) - gen0Before;
            int gen1 = GC.CollectionCount(1) - gen1Before;
            int gen2 = GC.CollectionCount(2) - gen2Before;

            Array.Sort(ticks);

            double toMs(long tick) => tick * 1000.0 / Stopwatch.Frequency;

            double median = (runs & 1) == 1
                ? toMs(ticks[runs / 2])
                : (toMs(ticks[runs / 2 - 1]) + toMs(ticks[runs / 2])) / 2;

            int p95Index = Math.Min(runs - 1, (int)Math.Ceiling(runs * 0.95) - 1);

            return new EzPerfSample(
                label,
                runs,
                toMs(ticks[0]),
                median,
                toMs(ticks[p95Index]),
                toMs(ticks[^1]),
                allocated / 1024.0 / runs,
                (double)gen0 / runs,
                (double)gen1 / runs,
                gen2);
        }

        /// <summary>
        /// Managed bytes retained by whatever <paramref name="build"/> creates, measured between two full
        /// collections. The factory is invoked twice so one-time statics it touches are not attributed to the sample.
        /// </summary>
        public static double MeasureRetainedKb<T>(Func<T> build)
            where T : class
        {
            // Let any static the factory warms up settle outside the measurement window.
            GC.KeepAlive(build());
            Stabilise();

            long before = GC.GetTotalMemory(true);
            T handle = build();
            long after = GC.GetTotalMemory(true);

            GC.KeepAlive(handle);

            return (after - before) / 1024.0;
        }

        /// <summary>Full blocking collection, so the next sample starts from an empty nursery.</summary>
        public static void Stabilise()
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        }

        public static void Report(IEnumerable<EzPerfSample> samples)
        {
            NUnit.Framework.TestContext.Progress.WriteLine("stage | median ms | p95 ms | max ms | KB/run | gen0/run | gen1/run | gen2");

            foreach (var sample in samples)
            {
                NUnit.Framework.TestContext.Progress.WriteLine(
                    $"{sample.Label,-22} | {sample.MedianMs,9:F2} | {sample.P95Ms,6:F2} | {sample.MaxMs,6:F2} | " +
                    $"{sample.AverageKb,7:F1} | {sample.Gen0PerRun,8:F2} | {sample.Gen1PerRun,8:F2} | {sample.Gen2}");
            }
        }
    }
}
