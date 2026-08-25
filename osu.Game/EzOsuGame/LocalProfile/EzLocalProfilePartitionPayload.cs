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
        public List<PartitionRulesetStats> RulesetStats { get; set; } = new List<PartitionRulesetStats>();
        public List<PartitionManiaKeyStats> ManiaKeyStats { get; set; } = new List<PartitionManiaKeyStats>();
        public List<PartitionManiaColumnStats> ManiaColumnStats { get; set; } = new List<PartitionManiaColumnStats>();
        public List<PartitionGradeCount> GradeCounts { get; set; } = new List<PartitionGradeCount>();
        public List<PartitionStarPlayCount> StarPlayCounts { get; set; } = new List<PartitionStarPlayCount>();
        public List<PartitionStdAttrAffinity> StdAttrAffinities { get; set; } = new List<PartitionStdAttrAffinity>();

        public static EzLocalProfilePartitionPayload FromAggregation(EzLocalProfileAggregationResult result)
        {
            var payload = new EzLocalProfilePartitionPayload();

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

            return payload;
        }

        public void MergeInto(EzLocalProfileAggregationResult target)
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

    public sealed class PartitionStdAttrAffinity
    {
        public int Attr { get; set; }
        public double Value { get; set; }
        public int PlayCount { get; set; }
        public int HighGradeCount { get; set; }
    }
}
