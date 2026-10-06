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
    /// Aggregated counters for playable-beatmap conversion and its cache.
    /// Intended for performance debugging only; disabled by default.
    /// </summary>
    /// <remarks>
    /// Only the conversion path and <c>EzPlayableBeatmapCache</c> report here. Counters are cumulative
    /// until <see cref="MaybeLog"/> resets them once per second, so a caller that never logs accumulates
    /// plain integers rather than triggering any IO.
    /// </remarks>
    public static class EzModConversionPerf
    {
        public static volatile bool Enabled;

        /// <summary>Per-thread allocation counter must be read on the thread being measured.</summary>
        public static long CurrentThreadAllocatedBytes => GC.GetAllocatedBytesForCurrentThread();

        static EzModConversionPerf()
        {
            // Accepted values: 1/true/yes/on (case-insensitive)
            string? raw = Environment.GetEnvironmentVariable("EZ_MOD_CONVERSION_PERF");
            if (raw == null)
                return;

            raw = raw.Trim();
            Enabled = raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static long last_log_timestamp;

        private static long conversion_count;
        private static long conversion_ticks;
        private static long conversion_alloc_bytes;
        private static long conversion_hit_objects;

        private static long cache_hit_count;
        private static long cache_miss_count;
        private static long cache_eviction_count;
        private static long cache_invalidation_count;
        private static long cache_hit_ticks;

        private static int unbound_entry_count;
        private static int bound_entry_count;
        private static int cached_hit_object_count;

        /// <summary>
        /// Records one actual conversion (i.e. a cache miss that had to run the converter and mod chain).
        /// </summary>
        public static void RecordConversion(long elapsedTicks, long allocatedBytes, int hitObjectCount)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref conversion_count);
            Interlocked.Add(ref conversion_ticks, elapsedTicks);
            Interlocked.Add(ref conversion_alloc_bytes, allocatedBytes);
            Interlocked.Add(ref conversion_hit_objects, hitObjectCount);
        }

        public static void RecordCacheHit(long elapsedTicks = 0)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref cache_hit_count);
            Interlocked.Add(ref cache_hit_ticks, elapsedTicks);
        }

        public static void RecordCacheMiss()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref cache_miss_count);
        }

        public static void RecordEviction()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref cache_eviction_count);
        }

        public static void RecordInvalidation()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref cache_invalidation_count);
        }

        /// <summary>
        /// Publishes current cache occupancy. Cheap enough to call after every insert/evict.
        /// </summary>
        public static void UpdateCacheGauges(int unboundEntries, int boundEntries, int cachedHitObjects)
        {
            if (!Enabled) return;

            Volatile.Write(ref unbound_entry_count, unboundEntries);
            Volatile.Write(ref bound_entry_count, boundEntries);
            Volatile.Write(ref cached_hit_object_count, cachedHitObjects);
        }

        public static void MaybeLog()
        {
            if (!Enabled) return;

            long now = Stopwatch.GetTimestamp();
            long last = Volatile.Read(ref last_log_timestamp);

            if (now - last < Stopwatch.Frequency)
                return;

            if (Interlocked.CompareExchange(ref last_log_timestamp, now, last) != last)
                return;

            long conversions = Interlocked.Exchange(ref conversion_count, 0);
            long ticks = Interlocked.Exchange(ref conversion_ticks, 0);
            long allocBytes = Interlocked.Exchange(ref conversion_alloc_bytes, 0);
            long hitObjects = Interlocked.Exchange(ref conversion_hit_objects, 0);

            long hits = Interlocked.Exchange(ref cache_hit_count, 0);
            long misses = Interlocked.Exchange(ref cache_miss_count, 0);
            long evictions = Interlocked.Exchange(ref cache_eviction_count, 0);
            long invalidations = Interlocked.Exchange(ref cache_invalidation_count, 0);
            long hitTicks = Interlocked.Exchange(ref cache_hit_ticks, 0);

            int unbound = Volatile.Read(ref unbound_entry_count);
            int bound = Volatile.Read(ref bound_entry_count);
            int cachedObjects = Volatile.Read(ref cached_hit_object_count);

            double ticksToMs(long t) => t * 1000.0 / Stopwatch.Frequency;

            long lookups = hits + misses;
            string hitRate = lookups > 0 ? $"{(double)hits / lookups:P0}" : "n/a";

            double avgConversionMs = conversions > 0 ? ticksToMs(ticks) / conversions : 0;
            double avgHitMs = hits > 0 ? ticksToMs(hitTicks) / hits : 0;
            double avgConversionKb = conversions > 0 ? allocBytes / 1024.0 / conversions : 0;

            Logger.Log(
                $"convert(count/ms/avgMs/KB/avgKB/notes)={conversions}/{ticksToMs(ticks):F2}/{avgConversionMs:F2}/{allocBytes / 1024.0:F1}/{avgConversionKb:F1}/{hitObjects} " +
                $"cache(hit/miss/hitRate/avgHitMs)={hits}/{misses}/{hitRate}/{avgHitMs:F3} " +
                $"entries(unbound/bound/notes)={unbound}/{bound}/{cachedObjects} " +
                $"evict={evictions} invalidate={invalidations}",
                Ez2ConfigManager.LOGGER_NAME,
                LogLevel.Important);
        }
    }
}
