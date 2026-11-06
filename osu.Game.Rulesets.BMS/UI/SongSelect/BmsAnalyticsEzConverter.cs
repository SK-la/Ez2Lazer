// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public static class BmsAnalyticsEzConverter
    {
        public static EzAnalysisResult? ToEzAnalysisResult(BmsAnalyticsRecord record)
        {
            if (record.Pp == null && record.XxySr == null && record.AvgKps == null && record.MaxKps == null && string.IsNullOrEmpty(record.ColumnCountsJson))
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

            var maniaSummary = new EzManiaSummary(columnCounts, holdNoteCounts: null, xxySr: record.XxySr);

            // The stored pp is a chart-level baseline, which is what the official performance producer carries.
            Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>? axes = null;

            if (record.Pp is double pp && double.IsFinite(pp) && pp > 0)
            {
                axes = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>
                {
                    [EzChartMetricProducer.OfficialPerformance.Id()] = new Dictionary<string, double>
                    {
                        [EzMetricAxisIds.PERFORMANCE_POINTS] = pp,
                    },
                };
            }

            return new EzAnalysisResult(kpsSummary, maniaSummary, axes: axes);
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
