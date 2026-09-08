// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// One mania play in the Insights top-PP window (aligned with mania-hub InsightScoreSnapshot inputs).
    /// </summary>
    public sealed class EzLocalProfileInsightPlay
    {
        public required EzLocalProfileDrillScoreRow Row { get; init; }
        public double Pp { get; init; }
        public int KeyCount { get; init; }
        public int BeatmapOnlineId { get; init; }
        public bool IsConvert { get; init; }
        public double Bpm { get; init; }
        public double Rate { get; init; } = 1;
        public IReadOnlyList<string> ModAcronyms { get; init; } = Array.Empty<string>();
        public DateTimeOffset Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string DifficultyName { get; init; } = string.Empty;
        public double StarRating { get; init; }
        public string Rank { get; init; } = string.Empty;
    }

    public sealed record EzLocalProfileKeySplit(int KeyCount, int Count);

    public sealed record EzLocalProfileKeyPpBucket(int KeyCount, double WeightedPp, int Count, double MissingBound);

    public sealed record EzLocalProfileCountEntry(string Label, int Count, int Total);

    public sealed record EzLocalProfileBpmRange(
        double Min,
        double Max,
        EzLocalProfileInsightPlay MinPlay,
        EzLocalProfileInsightPlay MaxPlay);

    public sealed record EzLocalProfileBpmByKey(int KeyCount, double Median, int Count);

    public sealed record EzLocalProfilePpRange(double Top, double Bottom);

    public sealed record EzLocalProfilePpDistributionBucket(int? Min, int? Max, int Count, int Total);

    public sealed record EzLocalProfilePpCumulativeRow(int Threshold, int Count, int Total);

    /// <summary>
    /// Local Profile Track Insights result (mania-hub Profile Insights subset).
    /// </summary>
    public sealed class EzLocalProfileInsights
    {
        public int SampleSize { get; init; }
        public IReadOnlyList<EzLocalProfileKeySplit> KeySplit { get; init; } = Array.Empty<EzLocalProfileKeySplit>();
        public IReadOnlyList<EzLocalProfileKeyPpBucket> KeyPp { get; init; } = Array.Empty<EzLocalProfileKeyPpBucket>();
        public int KeyPpConverts { get; init; }
        public double KeyPpCutoff { get; init; }
        public EzLocalProfileCountEntry? MostUsedMod { get; init; }
        public IReadOnlyList<EzLocalProfileCountEntry> ModBreakdown { get; init; } = Array.Empty<EzLocalProfileCountEntry>();
        public double? MedianBpm { get; init; }
        public EzLocalProfileBpmRange? BpmRange { get; init; }
        public IReadOnlyList<EzLocalProfileBpmByKey> BpmByKeyMode { get; init; } = Array.Empty<EzLocalProfileBpmByKey>();
        public EzLocalProfileInsightPlay? NewestTopPlay { get; init; }
        public EzLocalProfileInsightPlay? OldestTopPlay { get; init; }
        public EzLocalProfilePpRange? PpRange { get; init; }
        public IReadOnlyList<EzLocalProfilePpDistributionBucket> PpDistribution { get; init; } = Array.Empty<EzLocalProfilePpDistributionBucket>();
        public IReadOnlyList<EzLocalProfilePpCumulativeRow> PpCumulative { get; init; } = Array.Empty<EzLocalProfilePpCumulativeRow>();
    }
}
