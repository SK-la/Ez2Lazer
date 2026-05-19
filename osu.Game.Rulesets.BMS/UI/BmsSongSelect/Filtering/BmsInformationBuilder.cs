// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    internal readonly record struct BmsInformationRow(
        string Sha256,
        int N,
        int Ln,
        int S,
        int Ls,
        double Total,
        double Density,
        double PeakDensity,
        double EndDensity,
        double MainBpm);

    internal static class BmsInformationBuilder
    {
        public static BmsInformationRow Build(BMSChartCache chart, string pathKey, BmsAnalyticsSqliteRepository? analytics)
        {
            int notes = Math.Max(1, chart.TotalNotes);
            int ln = chart.LongNoteCount;
            int n = Math.Max(0, notes - ln);

            double lengthSec = Math.Max(1.0, chart.Duration / 1000.0);
            double mainBpm = chart.Bpm > 0 ? chart.Bpm : 130;

            double density = notes / lengthSec * 4.0;
            double peakDensity = density;
            double endDensity = density * 0.85;

            if (analytics != null && analytics.TryGet(pathKey, out var record))
            {
                if (record.AvgKps is > 0)
                    density = record.AvgKps.Value;

                if (record.MaxKps is > 0)
                    peakDensity = record.MaxKps.Value;

                if (record.XxySr is > 0)
                    endDensity = record.XxySr.Value;
            }

            return new BmsInformationRow(
                pathKey,
                n,
                ln,
                0,
                0,
                chart.Total,
                density,
                peakDensity,
                endDensity,
                mainBpm);
        }
    }
}
