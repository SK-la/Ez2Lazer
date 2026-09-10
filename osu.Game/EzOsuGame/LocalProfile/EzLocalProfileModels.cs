// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public static class EzLocalProfileConstants
    {
        /// <summary>Canonical bucket for scores with empty Realm username (matches API <c>GuestUser</c>).</summary>
        public const string GUEST_USERNAME = "Guest";

        /// <summary>Legacy empty-username bucket written before Guest normalisation.</summary>
        public const string LEGACY_UNKNOWN_USERNAME = "(unknown)";

        [Obsolete("Use GUEST_USERNAME")]
        public const string UNKNOWN_USERNAME = GUEST_USERNAME;

        /// <summary>
        /// Sentinel for the player filter: archive-wide totals (no per-player username filter).
        /// Career / Drill / evidence reads treat this as unfiltered; SSR / Dan headlines are
        /// materialised under this key as a hypothetical player (small rows only — evidence is not duplicated).
        /// </summary>
        public const string ALL_PLAYERS = "All";

        public const int OSU_RULESET_ID = 0;
        public const int MANIA_RULESET_ID = 3;

        /// <summary>
        /// True when the filter means archive-wide / no per-player username constraint
        /// (<see langword="null"/>, blank, or <see cref="ALL_PLAYERS"/>).
        /// </summary>
        public static bool IsAllPlayersFilter(string? username)
            => string.IsNullOrWhiteSpace(username)
               || string.Equals(username.Trim(), ALL_PLAYERS, StringComparison.Ordinal);

        public static string NormaliseUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return GUEST_USERNAME;

            string trimmed = username.Trim();

            if (string.Equals(trimmed, LEGACY_UNKNOWN_USERNAME, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, GUEST_USERNAME, StringComparison.OrdinalIgnoreCase))
                return GUEST_USERNAME;

            return trimmed;
        }

        public static bool IsGuestUsername(string? username)
            => string.Equals(NormaliseUsername(username), GUEST_USERNAME, StringComparison.Ordinal);
    }

    public readonly record struct EzLocalProfileUsernameCount(string Username, int ScoreCount);

    public enum EzLocalProfileComputePhase
    {
        Analysing,
        Saving,
        Skills,
    }

    /// <summary>
    /// Progress payload for local score analysis (scores processed / total, then save / skills).
    /// </summary>
    public readonly record struct EzLocalProfileComputeProgress(
        int Processed,
        int Total,
        EzLocalProfileComputePhase Phase = EzLocalProfileComputePhase.Analysing);

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
