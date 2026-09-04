// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public static class EzLocalProfileConstants
    {
        public const string UNKNOWN_USERNAME = "(unknown)";
        public const int OSU_RULESET_ID = 0;
        public const int MANIA_RULESET_ID = 3;
    }

    public readonly record struct EzLocalProfileUsernameCount(string Username, int ScoreCount);

    /// <summary>
    /// Progress payload for local profile compute (scores processed / total, then save).
    /// </summary>
    public readonly record struct EzLocalProfileComputeProgress(int Processed, int Total, bool Saving);

    public sealed class EzLocalProfileSnapshot
    {
        public bool HasData { get; init; }

        /// <summary>
        /// True when stored stats were produced by an older analysis logic version and should be recomputed.
        /// </summary>
        public bool NeedsRecompute { get; init; }

        public DateTimeOffset? LastComputedAt { get; init; }
        public IReadOnlyList<string> IncludedUsernames { get; init; } = Array.Empty<string>();
        public IReadOnlyList<EzLocalProfileRulesetStats> RulesetStats { get; init; } = Array.Empty<EzLocalProfileRulesetStats>();
        public IReadOnlyList<EzLocalProfileManiaKeyStats> ManiaKeyStats { get; init; } = Array.Empty<EzLocalProfileManiaKeyStats>();
        public IReadOnlyList<EzLocalProfileManiaColumnStats> ManiaColumnStats { get; init; } = Array.Empty<EzLocalProfileManiaColumnStats>();
        public IReadOnlyList<EzLocalProfileGradeCount> GradeCounts { get; init; } = Array.Empty<EzLocalProfileGradeCount>();
        public IReadOnlyList<EzLocalProfileStarPlayCount> StarPlayCounts { get; init; } = Array.Empty<EzLocalProfileStarPlayCount>();
        public IReadOnlyList<EzLocalProfileXxyPlayCount> XxyPlayCounts { get; init; } = Array.Empty<EzLocalProfileXxyPlayCount>();
        public IReadOnlyList<EzLocalProfileStdAttrAffinity> StdAttrAffinities { get; init; } = Array.Empty<EzLocalProfileStdAttrAffinity>();
        public IReadOnlyList<EzLocalProfileDrillScoreRow> DrillScores { get; init; } = Array.Empty<EzLocalProfileDrillScoreRow>();
    }

    public readonly record struct EzLocalProfileRulesetStats(
        int RulesetId,
        long TotalKeys,
        double AvgKps,
        double MaxKps,
        int ScoreCount,
        int KpsSampleCount,
        double TotalPp,
        long TotalDurationMs);

    public readonly record struct EzLocalProfileManiaKeyStats(
        int KeyCount,
        long TotalKeys,
        double AvgKps,
        double MaxKps,
        int ScoreCount,
        int KpsSampleCount,
        double TotalPp,
        long TotalDurationMs);

    public readonly record struct EzLocalProfileManiaColumnStats(
        int KeyCount,
        int ColumnIndex,
        long TotalKeys,
        double AvgKps,
        double MaxKps,
        int ScoreCount,
        int KpsSampleCount);

    public readonly record struct EzLocalProfileGradeCount(int RulesetId, ScoreRank Rank, int Count);

    public readonly record struct EzLocalProfileStarPlayCount(int RulesetId, int StarBucket, int Count);

    /// <summary>
    /// xxy SR play distribution; bucket semantics mirror <see cref="EzLocalProfileStarPlayCount"/>.
    /// </summary>
    public readonly record struct EzLocalProfileXxyPlayCount(int RulesetId, int StarBucket, int Count);

    public enum EzLocalProfileStdAttr
    {
        ApproachRate = 0,
        CircleSize = 1,
    }

    public readonly record struct EzLocalProfileStdAttrAffinity(
        EzLocalProfileStdAttr Attr,
        double Value,
        int PlayCount,
        int HighGradeCount);

    /// <summary>
    /// Online API score metadata persisted for profile stats when local .osr import is unavailable.
    /// </summary>
    public readonly record struct EzLocalProfileOnlineScoreContribution(
        long OnlineId,
        int RulesetId,
        ScoreRank Rank,
        double StarRating,
        float CircleSize,
        float ApproachRate,
        long KeyCount,
        double Pp,
        long DurationMs);

    /// <summary>
    /// In-memory aggregation buffer written atomically to SQLite.
    /// </summary>
    public sealed class EzLocalProfileAggregationResult
    {
        public IReadOnlyList<string> IncludedUsernames { get; set; } = Array.Empty<string>();
        public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;
        public Dictionary<int, MutableRulesetStats> RulesetStats { get; } = new Dictionary<int, MutableRulesetStats>();
        public Dictionary<int, MutableManiaKeyStats> ManiaKeyStats { get; } = new Dictionary<int, MutableManiaKeyStats>();
        public Dictionary<(int KeyCount, int Column), MutableManiaColumnStats> ManiaColumnStats { get; } = new Dictionary<(int KeyCount, int Column), MutableManiaColumnStats>();
        public Dictionary<(int RulesetId, ScoreRank Rank), int> GradeCounts { get; } = new Dictionary<(int RulesetId, ScoreRank Rank), int>();
        public Dictionary<(int RulesetId, int StarBucket), int> StarPlayCounts { get; } = new Dictionary<(int RulesetId, int StarBucket), int>();
        public Dictionary<(int RulesetId, int StarBucket), int> XxyPlayCounts { get; } = new Dictionary<(int RulesetId, int StarBucket), int>();
        public Dictionary<(EzLocalProfileStdAttr Attr, double Value), MutableStdAttr> StdAttrAffinities { get; } = new Dictionary<(EzLocalProfileStdAttr Attr, double Value), MutableStdAttr>();
        public List<EzLocalProfileDrillScoreRow> DrillScores { get; } = new List<EzLocalProfileDrillScoreRow>();

        public sealed class MutableRulesetStats
        {
            public long TotalKeys;
            public double KpsSum;
            public int KpsSampleCount;
            public double MaxKps;
            public int ScoreCount;
            public double TotalPp;
            public long TotalDurationMs;
        }

        public sealed class MutableManiaKeyStats
        {
            public long TotalKeys;
            public double KpsSum;
            public int KpsSampleCount;
            public double MaxKps;
            public int ScoreCount;
            public double TotalPp;
            public long TotalDurationMs;
        }

        public sealed class MutableManiaColumnStats
        {
            public long TotalKeys;
            public double KpsSum;
            public int KpsSampleCount;
            public double MaxKps;
            public int ScoreCount;
        }

        public sealed class MutableStdAttr
        {
            public int PlayCount;
            public int HighGradeCount;
        }
    }
}
