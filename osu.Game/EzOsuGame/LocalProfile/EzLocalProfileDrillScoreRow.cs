// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mods;
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

        /// <summary>Persisted Insights inputs (avoid Realm beatmap scans on overlay open).</summary>
        public double Bpm { get; init; }

        public int KeyCount { get; init; }
        public bool IsConvert { get; init; }
        public double Rate { get; init; } = 1;
        public string ModAcronymsJson { get; init; } = "[]";

        public bool HasInsightMeta => KeyCount > 0 || Bpm > 0 || IsConvert || Math.Abs(Rate - 1) > 0.001
                                      || (!string.IsNullOrEmpty(ModAcronymsJson) && ModAcronymsJson != "[]");

        public string FormatPpText() => PpResolved > 0 ? $"{EzLocalProfileFormat.FormatPp(PpResolved)}pp" : "—";

        public IReadOnlyList<string> ReadModAcronyms()
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(ModAcronymsJson) ?? new List<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

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
            bool hasKps,
            double? avgAbsOffsetMs = null,
            double? starRating = null,
            double? xxyStarRating = null)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.Metadata;
            var maniaSummary = analysis.ManiaSummary;

            double resolvedXxy = xxyStarRating
                                 ?? (hasKps && maniaSummary?.XxySr is double analysisXxy && analysisXxy >= 0
                                     ? analysisXxy
                                     : beatmap.XxyStarRating);

            int keyCount = 0;

            if (maniaSummary?.ColumnCounts is { Count: > 0 } columns)
                keyCount = columns.Keys.Max() + 1;
            else
            {
                int fromCs = (int)Math.Round(beatmap.Difficulty.CircleSize);
                if (fromCs > 0)
                    keyCount = fromCs;
            }

            double rate = 1;

            foreach (var mod in score.Mods)
            {
                if (mod is ModRateAdjust rateAdjust)
                    rate *= rateAdjust.SpeedChange.Value;
            }

            if (!double.IsFinite(rate) || rate <= 0)
                rate = 1;
            else
                rate = Math.Clamp(rate, 0.5, 2.0);

            var modAcronyms = score.Mods
                                   .Select(m => m.Acronym)
                                   .Where(a => !string.IsNullOrEmpty(a))
                                   .Distinct(StringComparer.Ordinal)
                                   .ToList();

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
                Title = metadata.Title,
                Artist = metadata.Artist,
                DifficultyName = beatmap.DifficultyName,
                MapperUsername = metadata.Author.Username,
                BeatmapStatus = beatmap.Status,
                StarRating = starRating ?? beatmap.StarRating,
                XxyStarRating = resolvedXxy,
                MapPerformancePoints = beatmap.PerformancePoints,
                KpsAvg = hasKps ? analysis.AverageKps : 0,
                KpsMax = hasKps ? analysis.MaxKps : 0,
                KpsListJson = JsonSerializer.Serialize(hasKps ? analysis.KpsList.ToList() : new List<double>()),
                ColumnCountsJson = JsonSerializer.Serialize(maniaSummary?.ColumnCounts ?? new Dictionary<int, int>()),
                HoldCountsJson = JsonSerializer.Serialize(maniaSummary?.HoldNoteCounts ?? new Dictionary<int, int>()),
                AvgAbsOffsetMs = avgAbsOffsetMs ?? ComputeAvgAbsOffsetMs(score),
                HasVideo = beatmap.HasVideo == true,
                HasStoryboard = beatmap.HasStoryboard == true,
                Date = score.Date,
                Bpm = beatmap.BPM > 0 && double.IsFinite(beatmap.BPM) ? beatmap.BPM : 0,
                KeyCount = keyCount,
                IsConvert = beatmap.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID,
                Rate = rate,
                ModAcronymsJson = JsonSerializer.Serialize(modAcronyms),
            };
        }

        public static double? ComputeAvgAbsOffsetMs(ScoreInfo score)
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
