// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Session-only analysis results. Nothing here is persisted; the stored baseline is owned by the background
    /// backfill.
    /// </summary>
    /// <remarks>
    /// Results are keyed by chart, ruleset and mods, because that is what determines an analysis. Every surface
    /// that wants live values for the same chart reads the same entry instead of computing its own.
    /// </remarks>
    public sealed class EzAnalysisLiveCache
    {
        /// <summary>Inputs that determine an analysis. The key count is derived from the playable, not an input.</summary>
        public readonly record struct AnalysisKey(string BeatmapHash, int RulesetOnlineId, int ModsSignature);

        private sealed class Entry
        {
            public readonly EzAnalysisResult Result;
            public long LastAccess;

            public Entry(EzAnalysisResult result, long lastAccess)
            {
                Result = result;
                LastAccess = lastAccess;
            }
        }

        private readonly ConcurrentDictionary<AnalysisKey, Entry> analyses = new ConcurrentDictionary<AnalysisKey, Entry>();

        /// <summary>
        /// Upper bound on retained analyses. Each entry holds a KPS series and a mania summary, so an unbounded
        /// store grows with everything the user browses.
        /// </summary>
        private const int capacity = 512;

        /// <summary>
        /// Entries dropped at once when the bound is reached. Batching amortises the recency scan over many inserts;
        /// dropping only the single oldest would make every insert past the bound pay a full scan.
        /// </summary>
        private const int eviction_batch = capacity / 4;

        private long accessClock;

        public EzAnalysisResult? Get(AnalysisKey key)
        {
            if (!analyses.TryGetValue(key, out var entry))
            {
                EzAnalysisPassPerf.RecordCacheMiss();
                return null;
            }

            Interlocked.Exchange(ref entry.LastAccess, Interlocked.Increment(ref accessClock));
            EzAnalysisPassPerf.RecordCacheHit();
            return entry.Result;
        }

        public void Set(AnalysisKey key, EzAnalysisResult value)
        {
            long stamp = Interlocked.Increment(ref accessClock);

            // The caller already checked for a hit before asking for a pass, so an absent key at the bound means the
            // insert is new and the oldest quarter has to make room. Keeping the newest entries is what stops a long
            // scroll from evicting the chart that is on screen.
            if (!analyses.ContainsKey(key) && analyses.Count >= capacity)
                evictLeastRecentlyUsed();

            analyses[key] = new Entry(value, stamp);
            EzAnalysisPassPerf.UpdateCacheGauges(analyses.Count, capacity);
        }

        private void evictLeastRecentlyUsed()
        {
            var snapshot = analyses.ToArray();

            Array.Sort(snapshot, static (left, right) => left.Value.LastAccess.CompareTo(right.Value.LastAccess));

            // Never drop the whole table: the entry being inserted has not been added yet, so leaving the newest
            // quarter in place keeps the working set alive even if every entry was touched long ago.
            int toDrop = Math.Min(eviction_batch, snapshot.Length - eviction_batch);

            for (int i = 0; i < toDrop; i++)
            {
                if (analyses.TryRemove(snapshot[i].Key, out _))
                    EzAnalysisPassPerf.RecordCacheEviction();
            }
        }

        /// <summary>Drops every entry, for a bump that can invalidate more than one.</summary>
        public void ClearAll()
        {
            EzAnalysisPassPerf.RecordCacheClear();
            analyses.Clear();
            EzAnalysisPassPerf.UpdateCacheGauges(0, capacity);
        }

        public void InvalidateSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject))
                return;

            bool removed = false;

            foreach (var key in analyses.Keys)
            {
                if (string.Equals(key.BeatmapHash, subject, StringComparison.Ordinal))
                    removed |= analyses.TryRemove(key, out _);
            }

            if (removed)
                EzAnalysisPassPerf.RecordCachePurge();
        }

        public void Clear()
        {
            EzAnalysisPassPerf.RecordCacheClear();
            analyses.Clear();
            EzAnalysisPassPerf.UpdateCacheGauges(0, capacity);
        }
    }
}
