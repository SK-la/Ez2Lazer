// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Per-username persisted aggregation slice. Recomputing a name replaces only this slice.
    /// </summary>
    public sealed class EzLocalProfilePartitionPayload
    {
        /// <summary>
        /// Analysis content version (<see cref="EzLocalProfileStore.CONTENT_VERSION"/>) this slice was computed with.
        /// A slice from an older version is stale and cannot be reused as an incremental cache.
        /// Missing/0 means pre-versioning data.
        /// </summary>
        public int ContentVersion { get; set; }

        public List<PartitionRulesetStats> RulesetStats { get; set; } = new List<PartitionRulesetStats>();
        public List<PartitionManiaKeyStats> ManiaKeyStats { get; set; } = new List<PartitionManiaKeyStats>();
        public List<PartitionManiaColumnStats> ManiaColumnStats { get; set; } = new List<PartitionManiaColumnStats>();
        public List<PartitionGradeCount> GradeCounts { get; set; } = new List<PartitionGradeCount>();
        public List<PartitionStarPlayCount> StarPlayCounts { get; set; } = new List<PartitionStarPlayCount>();
        public List<PartitionXxyPlayCount> XxyPlayCounts { get; set; } = new List<PartitionXxyPlayCount>();
        public List<PartitionStdAttrAffinity> StdAttrAffinities { get; set; } = new List<PartitionStdAttrAffinity>();

        /// <summary>
        /// Legacy drill detail. No longer written — drill rows live in the <c>drill_scores</c> table (shaped like the
        /// chart-side <c>chart_dan</c> store) so a partition stays a small, fixed-size slice and a single new score
        /// costs O(1) instead of re-serialising every play. Still populated when reading a pre-v3 payload; callers
        /// must treat it as read-only history, not as the "already analysed" ledger.
        /// </summary>
        public List<EzLocalProfileDrillScoreRow> DrillScores { get; set; } = new List<EzLocalProfileDrillScoreRow>();

        public static EzLocalProfilePartitionPayload FromAggregation(EzLocalProfileAggregationResult result)
        {
            var payload = new EzLocalProfilePartitionPayload
            {
                ContentVersion = EzLocalProfileStore.CONTENT_VERSION,
            };

            foreach (var (rulesetId, stats) in result.RulesetStats)
            {
                payload.RulesetStats.Add(new PartitionRulesetStats
                {
                    RulesetId = rulesetId,
                    TotalKeys = stats.TotalKeys,
                    KpsSum = stats.KpsSum,
                    KpsSampleCount = stats.KpsSampleCount,
                    MaxKps = stats.MaxKps,
                    ScoreCount = stats.ScoreCount,
                    TotalPp = stats.TotalPp,
                    TotalDurationMs = stats.TotalDurationMs,
                });
            }

            foreach (var (keyCount, stats) in result.ManiaKeyStats)
            {
                payload.ManiaKeyStats.Add(new PartitionManiaKeyStats
                {
                    KeyCount = keyCount,
                    TotalKeys = stats.TotalKeys,
                    KpsSum = stats.KpsSum,
                    KpsSampleCount = stats.KpsSampleCount,
                    MaxKps = stats.MaxKps,
                    ScoreCount = stats.ScoreCount,
                    TotalPp = stats.TotalPp,
                    TotalDurationMs = stats.TotalDurationMs,
                });
            }

            foreach (var ((keyCount, column), stats) in result.ManiaColumnStats)
            {
                payload.ManiaColumnStats.Add(new PartitionManiaColumnStats
                {
                    KeyCount = keyCount,
                    ColumnIndex = column,
                    TotalKeys = stats.TotalKeys,
                    KpsSum = stats.KpsSum,
                    KpsSampleCount = stats.KpsSampleCount,
                    MaxKps = stats.MaxKps,
                    ScoreCount = stats.ScoreCount,
                });
            }

            foreach (var ((rulesetId, rank), count) in result.GradeCounts)
                payload.GradeCounts.Add(new PartitionGradeCount { RulesetId = rulesetId, Rank = (int)rank, Count = count });

            foreach (var ((rulesetId, starBucket), count) in result.StarPlayCounts)
                payload.StarPlayCounts.Add(new PartitionStarPlayCount { RulesetId = rulesetId, StarBucket = starBucket, Count = count });

            foreach (var ((rulesetId, starBucket), count) in result.XxyPlayCounts)
                payload.XxyPlayCounts.Add(new PartitionXxyPlayCount { RulesetId = rulesetId, StarBucket = starBucket, Count = count });

            foreach (var ((attr, value), stats) in result.StdAttrAffinities)
            {
                payload.StdAttrAffinities.Add(new PartitionStdAttrAffinity
                {
                    Attr = (int)attr,
                    Value = value,
                    PlayCount = stats.PlayCount,
                    HighGradeCount = stats.HighGradeCount,
                });
            }

            // Drill detail is deliberately not copied: it belongs to the drill_scores table, not to the slice.
            return payload;
        }

        /// <summary>
        /// Merge this slice into an aggregation buffer. Deliberately stats-only: drill detail is read from
        /// <c>drill_scores</c>, so a slice loaded from disk never re-introduces its legacy drill rows.
        /// </summary>
        public void MergeInto(EzLocalProfileAggregationResult target)
            => MergeStatsInto(target);

        /// <summary>
        /// Merge aggregate counters only (no drill rows). Used when rebuilding All without holding every partition's drills in memory.
        /// </summary>
        public void MergeStatsInto(EzLocalProfileAggregationResult target)
        {
            foreach (var row in RulesetStats)
            {
                var stats = getOrCreate(target.RulesetStats, row.RulesetId, () => new EzLocalProfileAggregationResult.MutableRulesetStats());
                stats.TotalKeys += row.TotalKeys;
                stats.KpsSum += row.KpsSum;
                stats.KpsSampleCount += row.KpsSampleCount;
                stats.ScoreCount += row.ScoreCount;
                stats.TotalPp += row.TotalPp;
                stats.TotalDurationMs += row.TotalDurationMs;
                if (row.MaxKps > stats.MaxKps)
                    stats.MaxKps = row.MaxKps;
            }

            foreach (var row in ManiaKeyStats)
            {
                var stats = getOrCreate(target.ManiaKeyStats, row.KeyCount, () => new EzLocalProfileAggregationResult.MutableManiaKeyStats());
                stats.TotalKeys += row.TotalKeys;
                stats.KpsSum += row.KpsSum;
                stats.KpsSampleCount += row.KpsSampleCount;
                stats.ScoreCount += row.ScoreCount;
                stats.TotalPp += row.TotalPp;
                stats.TotalDurationMs += row.TotalDurationMs;
                if (row.MaxKps > stats.MaxKps)
                    stats.MaxKps = row.MaxKps;
            }

            foreach (var row in ManiaColumnStats)
            {
                var stats = getOrCreate(target.ManiaColumnStats, (row.KeyCount, row.ColumnIndex), () => new EzLocalProfileAggregationResult.MutableManiaColumnStats());
                stats.TotalKeys += row.TotalKeys;
                stats.KpsSum += row.KpsSum;
                stats.KpsSampleCount += row.KpsSampleCount;
                stats.ScoreCount += row.ScoreCount;
                if (row.MaxKps > stats.MaxKps)
                    stats.MaxKps = row.MaxKps;
            }

            foreach (var row in GradeCounts)
            {
                var key = (row.RulesetId, (ScoreRank)row.Rank);
                target.GradeCounts.TryGetValue(key, out int existing);
                target.GradeCounts[key] = existing + row.Count;
            }

            foreach (var row in StarPlayCounts)
            {
                var key = (row.RulesetId, row.StarBucket);
                target.StarPlayCounts.TryGetValue(key, out int existing);
                target.StarPlayCounts[key] = existing + row.Count;
            }

            foreach (var row in XxyPlayCounts)
            {
                var key = (row.RulesetId, row.StarBucket);
                target.XxyPlayCounts.TryGetValue(key, out int existing);
                target.XxyPlayCounts[key] = existing + row.Count;
            }

            foreach (var row in StdAttrAffinities)
            {
                var key = ((EzLocalProfileStdAttr)row.Attr, row.Value);
                var stats = getOrCreate(target.StdAttrAffinities, key, () => new EzLocalProfileAggregationResult.MutableStdAttr());
                stats.PlayCount += row.PlayCount;
                stats.HighGradeCount += row.HighGradeCount;
            }
        }

        private static TValue getOrCreate<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, Func<TValue> factory)
            where TKey : notnull
            where TValue : class
        {
            if (dict.TryGetValue(key, out var existing))
                return existing;

            var created = factory();
            dict[key] = created;
            return created;
        }
    }

    public sealed class PartitionRulesetStats
    {
        public int RulesetId { get; set; }
        public long TotalKeys { get; set; }
        public double KpsSum { get; set; }
        public int KpsSampleCount { get; set; }
        public double MaxKps { get; set; }
        public int ScoreCount { get; set; }
        public double TotalPp { get; set; }
        public long TotalDurationMs { get; set; }
    }

    public sealed class PartitionManiaKeyStats
    {
        public int KeyCount { get; set; }
        public long TotalKeys { get; set; }
        public double KpsSum { get; set; }
        public int KpsSampleCount { get; set; }
        public double MaxKps { get; set; }
        public int ScoreCount { get; set; }
        public double TotalPp { get; set; }
        public long TotalDurationMs { get; set; }
    }

    public sealed class PartitionManiaColumnStats
    {
        public int KeyCount { get; set; }
        public int ColumnIndex { get; set; }
        public long TotalKeys { get; set; }
        public double KpsSum { get; set; }
        public int KpsSampleCount { get; set; }
        public double MaxKps { get; set; }
        public int ScoreCount { get; set; }
    }

    public sealed class PartitionGradeCount
    {
        public int RulesetId { get; set; }
        public int Rank { get; set; }
        public int Count { get; set; }
    }

    public sealed class PartitionStarPlayCount
    {
        public int RulesetId { get; set; }
        public int StarBucket { get; set; }
        public int Count { get; set; }
    }

    public sealed class PartitionXxyPlayCount
    {
        public int RulesetId { get; set; }
        public int StarBucket { get; set; }
        public int Count { get; set; }
    }

    public sealed class PartitionStdAttrAffinity
    {
        public int Attr { get; set; }
        public double Value { get; set; }
        public int PlayCount { get; set; }
        public int HighGradeCount { get; set; }
    }
}
