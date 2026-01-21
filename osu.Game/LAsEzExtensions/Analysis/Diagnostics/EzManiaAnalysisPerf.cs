// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;
using osu.Framework.Logging;

namespace osu.Game.LAsEzExtensions.Analysis.Diagnostics
{
    public static class EzManiaAnalysisPerf
    {
        /// <summary>
        /// Enables aggregated logging for mania analysis/persistence.
        /// Intended for performance debugging only.
        /// </summary>
        public static volatile bool Enabled;

        static EzManiaAnalysisPerf()
        {
            // Default to disabled to minimise overhead. Enable explicitly via environment variable.
            // Accepted values: 1/true/yes/on (case-insensitive)
            string? raw = Environment.GetEnvironmentVariable("EZ_MANIA_ANALYSIS_PERF");
            if (raw == null)
                return;

            raw = raw.Trim();
            Enabled = raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
                      || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private const string log_category = "mania_analysis_perf";

        private static long last_log_timestamp;

        private static long request_count;
        private static long compute_started_count;
        private static long compute_completed_count;
        private static long compute_cancelled_count;

        private static long compute_total_ticks;
        private static long compute_non_persist_ticks;
        private static long compute_persist_hit_ticks;

        private static long persistence_tryget_count;
        private static long persistence_hit_count;
        private static long persistence_deserialize_ticks;
        private static long persistence_kps_json_chars;
        private static long persistence_cols_json_chars;
        private static long persistence_holds_json_chars;

        private static long persistence_store_count;
        private static long persistence_serialize_ticks;

        private static long eviction_count;

        private static long ui_update_count;
        private static long ui_update_ticks;
        private static long ui_update_alloc_bytes;

        private static long ui_graph_set_count;
        private static long ui_graph_set_ticks;
        private static long ui_graph_set_alloc_bytes;
        private static long ui_graph_points_total;

        private static long ui_kpc_update_count;
        private static long ui_kpc_update_ticks;
        private static long ui_kpc_update_alloc_bytes;
        private static long ui_kpc_columns_total;
        private static long ui_kpc_barchart_count;

        private static int in_memory_cache_size;
        private static int in_memory_cache_limit;
        private static int high_priority_inflight;
        private static int low_priority_inflight;

        public static void RecordRequest()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref request_count);
        }

        public static void RecordComputeStart(bool isLowPriority)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref compute_started_count);

            if (isLowPriority)
                Interlocked.Increment(ref low_priority_inflight);
            else
                Interlocked.Increment(ref high_priority_inflight);
        }

        public static void RecordComputeEnd(bool isLowPriority)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref compute_completed_count);

            if (isLowPriority)
                Interlocked.Decrement(ref low_priority_inflight);
            else
                Interlocked.Decrement(ref high_priority_inflight);
        }

        public static void RecordComputeCancelled()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref compute_cancelled_count);
        }

        public static void RecordComputeElapsedTicks(long elapsedTicks, bool wasPersistHit)
        {
            if (!Enabled) return;

            Interlocked.Add(ref compute_total_ticks, elapsedTicks);

            if (wasPersistHit)
                Interlocked.Add(ref compute_persist_hit_ticks, elapsedTicks);
            else
                Interlocked.Add(ref compute_non_persist_ticks, elapsedTicks);
        }

        public static void RecordPersistenceTryGet(bool hit, long deserializeTicks, int kpsJsonChars, int colsJsonChars, int holdsJsonChars)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref persistence_tryget_count);

            if (hit)
                Interlocked.Increment(ref persistence_hit_count);

            Interlocked.Add(ref persistence_deserialize_ticks, deserializeTicks);
            Interlocked.Add(ref persistence_kps_json_chars, kpsJsonChars);
            Interlocked.Add(ref persistence_cols_json_chars, colsJsonChars);
            Interlocked.Add(ref persistence_holds_json_chars, holdsJsonChars);
        }

        public static void RecordPersistenceStore(long serializeTicks)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref persistence_store_count);
            Interlocked.Add(ref persistence_serialize_ticks, serializeTicks);
        }

        public static void RecordEviction()
        {
            if (!Enabled) return;

            Interlocked.Increment(ref eviction_count);
        }

        public static void UpdateCacheGauges(int currentSize, int limit)
        {
            if (!Enabled) return;

            Volatile.Write(ref in_memory_cache_size, currentSize);
            Volatile.Write(ref in_memory_cache_limit, limit);
        }

        public static void RecordUiUpdate(long elapsedTicks, long allocatedBytes)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref ui_update_count);
            Interlocked.Add(ref ui_update_ticks, elapsedTicks);
            Interlocked.Add(ref ui_update_alloc_bytes, allocatedBytes);
        }

        public static void RecordUiGraphSet(int points, long elapsedTicks, long allocatedBytes)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref ui_graph_set_count);
            Interlocked.Add(ref ui_graph_set_ticks, elapsedTicks);
            Interlocked.Add(ref ui_graph_set_alloc_bytes, allocatedBytes);
            Interlocked.Add(ref ui_graph_points_total, points);
        }

        public static void RecordUiKpcUpdate(int columns, bool isBarChart, long elapsedTicks, long allocatedBytes)
        {
            if (!Enabled) return;

            Interlocked.Increment(ref ui_kpc_update_count);
            Interlocked.Add(ref ui_kpc_update_ticks, elapsedTicks);
            Interlocked.Add(ref ui_kpc_update_alloc_bytes, allocatedBytes);
            Interlocked.Add(ref ui_kpc_columns_total, columns);
            if (isBarChart)
                Interlocked.Increment(ref ui_kpc_barchart_count);
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

            long req = Interlocked.Exchange(ref request_count, 0);
            long started = Interlocked.Exchange(ref compute_started_count, 0);
            long completed = Interlocked.Exchange(ref compute_completed_count, 0);
            long cancelled = Interlocked.Exchange(ref compute_cancelled_count, 0);

            long computeTicks = Interlocked.Exchange(ref compute_total_ticks, 0);
            long computeNonPersistTicks = Interlocked.Exchange(ref compute_non_persist_ticks, 0);
            long computePersistHitTicks = Interlocked.Exchange(ref compute_persist_hit_ticks, 0);

            long tryGet = Interlocked.Exchange(ref persistence_tryget_count, 0);
            long hit = Interlocked.Exchange(ref persistence_hit_count, 0);
            long deserTicks = Interlocked.Exchange(ref persistence_deserialize_ticks, 0);
            long kpsChars = Interlocked.Exchange(ref persistence_kps_json_chars, 0);
            long colsChars = Interlocked.Exchange(ref persistence_cols_json_chars, 0);
            long holdsChars = Interlocked.Exchange(ref persistence_holds_json_chars, 0);

            long store = Interlocked.Exchange(ref persistence_store_count, 0);
            long serTicks = Interlocked.Exchange(ref persistence_serialize_ticks, 0);

            long evict = Interlocked.Exchange(ref eviction_count, 0);

            long uiCount = Interlocked.Exchange(ref ui_update_count, 0);
            long uiTicks = Interlocked.Exchange(ref ui_update_ticks, 0);
            long uiAlloc = Interlocked.Exchange(ref ui_update_alloc_bytes, 0);

            long graphCount = Interlocked.Exchange(ref ui_graph_set_count, 0);
            long graphTicks = Interlocked.Exchange(ref ui_graph_set_ticks, 0);
            long graphAlloc = Interlocked.Exchange(ref ui_graph_set_alloc_bytes, 0);
            long graphPoints = Interlocked.Exchange(ref ui_graph_points_total, 0);

            long kpcCount = Interlocked.Exchange(ref ui_kpc_update_count, 0);
            long kpcTicks = Interlocked.Exchange(ref ui_kpc_update_ticks, 0);
            long kpcAlloc = Interlocked.Exchange(ref ui_kpc_update_alloc_bytes, 0);
            long kpcCols = Interlocked.Exchange(ref ui_kpc_columns_total, 0);
            long kpcBar = Interlocked.Exchange(ref ui_kpc_barchart_count, 0);

            int cacheSize = Volatile.Read(ref in_memory_cache_size);
            int cacheLimit = Volatile.Read(ref in_memory_cache_limit);
            int highInflight = Volatile.Read(ref high_priority_inflight);
            int lowInflight = Volatile.Read(ref low_priority_inflight);

            double ticksToMs(long t) => t * 1000.0 / Stopwatch.Frequency;

            double avgComputeMs = completed > 0 ? ticksToMs(computeTicks) / completed : 0;
            double avgComputeNonPersistMs = completed > 0 ? ticksToMs(computeNonPersistTicks) / completed : 0;
            double avgComputePersistHitMs = completed > 0 ? ticksToMs(computePersistHitTicks) / completed : 0;

            double totalDeserMs = ticksToMs(deserTicks);
            double avgDeserMs = tryGet > 0 ? totalDeserMs / tryGet : 0;

            double totalSerMs = ticksToMs(serTicks);
            double avgSerMs = store > 0 ? totalSerMs / store : 0;

            double uiTotalMs = ticksToMs(uiTicks);
            double uiAvgMs = uiCount > 0 ? uiTotalMs / uiCount : 0;
            double uiTotalKb = uiAlloc / 1024.0;
            double uiAvgKb = uiCount > 0 ? uiTotalKb / uiCount : 0;

            double graphTotalMs = ticksToMs(graphTicks);
            double graphAvgMs = graphCount > 0 ? graphTotalMs / graphCount : 0;
            double graphTotalKb = graphAlloc / 1024.0;
            double graphAvgKb = graphCount > 0 ? graphTotalKb / graphCount : 0;

            double kpcTotalMs = ticksToMs(kpcTicks);
            double kpcAvgMs = kpcCount > 0 ? kpcTotalMs / kpcCount : 0;
            double kpcTotalKb = kpcAlloc / 1024.0;
            double kpcAvgKb = kpcCount > 0 ? kpcTotalKb / kpcCount : 0;

            string persistRate = tryGet > 0 ? $"{hit}/{tryGet}" : "0/0";

            Logger.Log(
                $"req={req} compute(started/completed/cancelled)={started}/{completed}/{cancelled} " +
                $"avgComputeMs={avgComputeMs:F2} (nonPersist~{avgComputeNonPersistMs:F2}, persistHit~{avgComputePersistHitMs:F2}) " +
                $"persist(hit/try)={persistRate} deserMs(total/avg)={totalDeserMs:F2}/{avgDeserMs:F2} " +
                $"jsonChars(kps/cols/holds)={kpsChars}/{colsChars}/{holdsChars} " +
                $"store={store} serMs(total/avg)={totalSerMs:F2}/{avgSerMs:F2} " +
                $"ui(upd count/ms/KB)={uiCount}/{uiTotalMs:F2}/{uiTotalKb:F1} avg={uiAvgMs:F2}ms/{uiAvgKb:F1}KB " +
                $"graph(set count/ms/KB pts)={graphCount}/{graphTotalMs:F2}/{graphTotalKb:F1} pts={graphPoints} avg={graphAvgMs:F2}ms/{graphAvgKb:F1}KB " +
                $"kpc(upd count/ms/KB cols bar)={kpcCount}/{kpcTotalMs:F2}/{kpcTotalKb:F1} cols={kpcCols} bar={kpcBar} avg={kpcAvgMs:F2}ms/{kpcAvgKb:F1}KB " +
                $"cache={cacheSize}/{cacheLimit} evict={evict} inflight(H/L)={highInflight}/{lowInflight}",
                log_category,
                LogLevel.Important);
        }
    }
}
