// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Per-stage timing and allocation counters for one full L3 analysis pass, plus the live cache's
    /// hit / miss / eviction counts. Intended for performance debugging only; disabled by default.
    /// </summary>
    /// <remarks>
    /// Stages are a fixed enum so a pass never allocates a dictionary key to report itself. Counters are
    /// cumulative until <see cref="MaybeLog"/> flushes them once per second, so an enabled trace adds
    /// two interlocked adds per stage and nothing else.
    /// </remarks>
    public static class EzAnalysisPassPerf
    {
        /// <summary>Stages a full pass reports. <see cref="Count"/> must stay last.</summary>
        public enum Stage
        {
            Convert,
            Kps,
            Measurements,
            Mina,
            Structure,
            Ppp,
            Official,
            Radar,
            Dan,
            Axes,
            Total,
            Count,
        }

        public static volatile bool Enabled;

        static EzAnalysisPassPerf()
        {
            // Accepted values: 1/true/yes/on (case-insensitive)
            string? raw = Environment.GetEnvironmentVariable("EZ_ANALYSIS_PASS_PERF");
            if (raw == null)
                return;

            raw = raw.Trim();
            Enabled = raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private const int stage_count = (int)Stage.Count;

        private static readonly long[] stage_ticks = new long[stage_count];
        private static readonly long[] stage_allocated = new long[stage_count];
        private static readonly long[] stage_counters = new long[stage_count];

        private static long pass_count;
        private static long pass_failed_count;

        private static long cache_hit_count;
        private static long cache_miss_count;
        private static long cache_eviction_count;
        private static long cache_clear_count;
        private static long cache_purge_count;

        private static int cache_entry_count;
        private static int cache_capacity;

        private static long last_log_timestamp;

        /// <summary>Measures one stage of the current pass. A no-op struct when tracing is disabled.</summary>
        public readonly struct MeasureScope : IDisposable
        {
            private readonly Stage stage;
            private readonly long startTicks;
            private readonly long startAllocated;

            internal MeasureScope(Stage stage)
            {
                this.stage = stage;
                startTicks = Stopwatch.GetTimestamp();
                startAllocated = GC.GetAllocatedBytesForCurrentThread();
            }

            public void Dispose()
            {
                Record(stage, Stopwatch.GetTimestamp() - startTicks, GC.GetAllocatedBytesForCurrentThread() - startAllocated);
            }
        }

        /// <summary>Starts measuring <paramref name="stage"/>, or returns a no-op scope when disabled.</summary>
        public static MeasureScope Measure(Stage stage) => Enabled ? new MeasureScope(stage) : default;

        private static void Record(Stage stage, long elapsedTicks, long allocatedBytes)
        {
            if (!Enabled)
                return;

            int index = (int)stage;

            Interlocked.Increment(ref stage_counters[index]);
            Interlocked.Add(ref stage_ticks[index], elapsedTicks);
            Interlocked.Add(ref stage_allocated[index], allocatedBytes);
        }

        /// <summary>Records one completed pass. A pass that fails before producing a result is counted separately.</summary>
        public static void RecordPass(bool succeeded)
        {
            if (!Enabled)
                return;

            if (succeeded)
                Interlocked.Increment(ref pass_count);
            else
                Interlocked.Increment(ref pass_failed_count);
        }

        public static void RecordCacheHit()
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cache_hit_count);
        }

        public static void RecordCacheMiss()
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cache_miss_count);
        }

        public static void RecordCacheEviction()
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cache_eviction_count);
        }

        /// <summary>A full-table drop, which is a different event from a bounded eviction.</summary>
        public static void RecordCacheClear()
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cache_clear_count);
        }

        public static void RecordCachePurge()
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref cache_purge_count);
        }

        /// <summary>Publishes live cache occupancy. Cheap enough to call after every insert/evict.</summary>
        public static void UpdateCacheGauges(int entries, int capacity)
        {
            if (!Enabled)
                return;

            Volatile.Write(ref cache_entry_count, entries);
            Volatile.Write(ref cache_capacity, capacity);
        }

        public static void MaybeLog()
        {
            if (!Enabled)
                return;

            long now = Stopwatch.GetTimestamp();
            long last = Volatile.Read(ref last_log_timestamp);

            if (now - last < Stopwatch.Frequency)
                return;

            if (Interlocked.CompareExchange(ref last_log_timestamp, now, last) != last)
                return;

            long passes = Interlocked.Exchange(ref pass_count, 0);
            long failed = Interlocked.Exchange(ref pass_failed_count, 0);
            long hits = Interlocked.Exchange(ref cache_hit_count, 0);
            long misses = Interlocked.Exchange(ref cache_miss_count, 0);
            long evictions = Interlocked.Exchange(ref cache_eviction_count, 0);
            long clears = Interlocked.Exchange(ref cache_clear_count, 0);
            long purges = Interlocked.Exchange(ref cache_purge_count, 0);

            int entries = Volatile.Read(ref cache_entry_count);
            int capacity = Volatile.Read(ref cache_capacity);

            double ticksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

            var stages = new System.Text.StringBuilder();

            for (int i = 0; i < stage_count; i++)
            {
                long count = Interlocked.Exchange(ref stage_counters[i], 0);

                if (count <= 0)
                    continue;

                long ticks = Interlocked.Exchange(ref stage_ticks[i], 0);
                long allocated = Interlocked.Exchange(ref stage_allocated[i], 0);

                if (stages.Length > 0)
                    stages.Append(' ');

                stages.Append((Stage)i);
                stages.Append('=');
                stages.Append(ticksToMs(ticks).ToString("F2"));
                stages.Append("ms/");
                stages.Append((allocated / 1024.0 / count).ToString("F1"));
                stages.Append("KB");
            }

            long lookups = hits + misses;
            string hitRate = lookups > 0 ? $"{(double)hits / lookups:P0}" : "n/a";

            Logger.Log(
                $"l3(pass/fail)={passes}/{failed} cache(hit/miss/hitRate/entries/capacity)={hits}/{misses}/{hitRate}/{entries}/{capacity} " +
                $"evict={evictions} clear={clears} purge={purges} stages[{stages}]",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }
    }
}
