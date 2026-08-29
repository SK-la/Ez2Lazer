// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// One drill score row persisted in <c>ez-local-profile.sqlite</c> and used by the overlay at runtime.
    /// </summary>
    public sealed class EzLocalProfileDrillScoreRow
    {
        public Guid ScoreId { get; init; }
        public string ScoreHash { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public int RulesetId { get; init; }
        public ScoreRank Rank { get; init; }
        public double PpResolved { get; init; }
        public double Accuracy { get; init; }
        public int MaxCombo { get; init; }
        public int MaxAchievableCombo { get; init; }
        public long TotalScore { get; init; }
        public string ModsJson { get; init; } = string.Empty;
        public long TotalKeys { get; init; }
        public string BeatmapHash { get; init; } = string.Empty;
        public Guid BeatmapId { get; init; }
        public Guid? BeatmapSetId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string DifficultyName { get; init; } = string.Empty;
        public string MapperUsername { get; init; } = string.Empty;
        public BeatmapOnlineStatus BeatmapStatus { get; init; }
        public double StarRating { get; init; }
        public double XxyStarRating { get; init; }
        public double MapPerformancePoints { get; init; }
        public double KpsAvg { get; init; }
        public double KpsMax { get; init; }
        public string KpsListJson { get; init; } = "[]";
        public string ColumnCountsJson { get; init; } = "{}";
        public string HoldCountsJson { get; init; } = "{}";
        public double? AvgAbsOffsetMs { get; init; }
        public bool HasVideo { get; init; }
        public bool HasStoryboard { get; init; }
        public DateTimeOffset Date { get; init; }

        public string FormatPpText() => PpResolved > 0 ? $"{EzLocalProfileFormat.FormatPp(PpResolved)}pp" : "—";

        public IReadOnlyList<double> ReadKpsList()
        {
            try
            {
                return JsonSerializer.Deserialize<List<double>>(KpsListJson) ?? new List<double>();
            }
            catch
            {
                return Array.Empty<double>();
            }
        }

        public EzManiaSummary ReadManiaSummary()
        {
            try
            {
                var columns = JsonSerializer.Deserialize<Dictionary<int, int>>(ColumnCountsJson) ?? new Dictionary<int, int>();
                var holds = JsonSerializer.Deserialize<Dictionary<int, int>>(HoldCountsJson) ?? new Dictionary<int, int>();
                return new EzManiaSummary(columns, holds, XxyStarRating >= 0 ? XxyStarRating : null);
            }
            catch
            {
                return EzManiaSummary.EMPTY;
            }
        }

        public static EzLocalProfileDrillScoreRow FromScore(
            ScoreInfo score,
            string username,
            double ppResolved,
            EzAnalysisResult analysis,
            bool hasKps)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.Metadata;
            var maniaSummary = analysis.ManiaSummary;

            return new EzLocalProfileDrillScoreRow
            {
                ScoreId = score.ID,
                ScoreHash = score.Hash,
                Username = username,
                RulesetId = score.Ruleset.OnlineID,
                Rank = score.Rank,
                PpResolved = ppResolved,
                Accuracy = score.Accuracy,
                MaxCombo = score.MaxCombo,
                MaxAchievableCombo = score.GetMaximumAchievableCombo(),
                TotalScore = score.TotalScore,
                ModsJson = score.ModsJson,
                TotalKeys = countKeys(score),
                BeatmapHash = score.BeatmapHash,
                BeatmapId = beatmap.ID,
                BeatmapSetId = beatmap.BeatmapSet?.ID,
                Title = metadata.Title ?? "?",
                Artist = metadata.Artist ?? string.Empty,
                DifficultyName = beatmap.DifficultyName,
                MapperUsername = metadata.Author.Username ?? string.Empty,
                BeatmapStatus = beatmap.Status,
                StarRating = beatmap.StarRating,
                XxyStarRating = beatmap.XxyStarRating,
                MapPerformancePoints = beatmap.PerformancePoints,
                KpsAvg = hasKps ? analysis.AverageKps : 0,
                KpsMax = hasKps ? analysis.MaxKps : 0,
                KpsListJson = JsonSerializer.Serialize(hasKps ? analysis.KpsList.ToList() : new List<double>()),
                ColumnCountsJson = JsonSerializer.Serialize(maniaSummary?.ColumnCounts ?? new Dictionary<int, int>()),
                HoldCountsJson = JsonSerializer.Serialize(maniaSummary?.HoldNoteCounts ?? new Dictionary<int, int>()),
                AvgAbsOffsetMs = computeAvgAbsOffset(score),
                HasVideo = beatmap.HasVideo == true,
                HasStoryboard = beatmap.HasStoryboard == true,
                Date = score.Date,
            };
        }

        private static double? computeAvgAbsOffset(ScoreInfo score)
        {
            if (score.HitEvents.Count == 0)
                return null;

            var hits = score.HitEvents.Where(e => e.Result.IsBasic() && e.Result.IsHit()).ToList();
            if (hits.Count == 0)
                return null;

            return hits.Average(e => Math.Abs(e.TimeOffset));
        }

        private static long countKeys(ScoreInfo score)
        {
            long fromMaximum = sumCountable(score.MaximumStatistics);
            if (fromMaximum > 0)
                return fromMaximum;

            return sumCountable(score.Statistics);
        }

        private static long sumCountable(IReadOnlyDictionary<HitResult, int> statistics)
        {
            long total = 0;

            foreach (var (result, count) in statistics)
            {
                if (result.IsScorable() && !result.IsBonus())
                    total += count;
            }

            return total;
        }
    }
}
