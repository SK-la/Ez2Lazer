// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public static class BmsAnalyticsEzConverter
    {
        public static EzAnalysisResult? ToEzAnalysisResult(BmsAnalyticsRecord record)
        {
            if (record.Pp == null && record.SunnySR == null && record.AvgKps == null && record.MaxKps == null && string.IsNullOrEmpty(record.ColumnCountsJson))
                return null;

            double avgKps = record.AvgKps ?? 0;
            double maxKps = record.MaxKps ?? 0;
            IReadOnlyList<double> kpsList = tryDeserializeKpsList(record.KpsListJson);
            var kpsSummary = new KpsSummary(avgKps, maxKps, kpsList);

            Dictionary<int, int>? columnCounts = null;

            if (!string.IsNullOrEmpty(record.ColumnCountsJson))
            {
                try
                {
                    columnCounts = JsonSerializer.Deserialize<Dictionary<int, int>>(record.ColumnCountsJson);
                }
                catch
                {
                    // Ignore malformed JSON from older scans.
                }
            }

            EzManiaSummary? maniaSummary = columnCounts == null ? null : new EzManiaSummary(columnCounts, holdNoteCounts: null);

            var ratings = new Dictionary<EzAnalysisSystem, EzScalarRating>();

            if (record.Pp is double pp && double.IsFinite(pp) && pp > 0)
                ratings[EzAnalysisSystem.Official] = new EzScalarRating(null, pp);

            if (record.SunnySR is double sunny && double.IsFinite(sunny))
                ratings[EzAnalysisSystem.SSR] = new EzScalarRating(sunny, null);

            return new EzAnalysisResult(kpsSummary, maniaSummary, ratings: ratings.Count == 0 ? null : ratings);
        }

        private static IReadOnlyList<double> tryDeserializeKpsList(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return Array.Empty<double>();

            try
            {
                return JsonSerializer.Deserialize<List<double>>(json) ?? new List<double>();
            }
            catch
            {
                return Array.Empty<double>();
            }
        }
    }
}
