// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>
    /// Pure C# port of mania-hub LeoBlack patterns thin pipeline
    /// (<c>service.js</c> → <c>summary.fromChart</c>).
    /// </summary>
    public static class EzLeoBlackPatternAnalyzer
    {
        private static readonly HashSet<string> ln_core_patterns = new HashSet<string>(StringComparer.Ordinal)
        {
            "Coordination", "Density", "Wildcard"
        };

        /// <summary>
        /// Analyze a mania chart. Returns <c>null</c> on empty/invalid input or internal failure
        /// (caller should fall back to tags).
        /// </summary>
        public static EzLeoBlackPatternResult? TryAnalyze(EzManiaChartInput chart)
        {
            try
            {
                if (chart.Notes.Count == 0 || chart.KeyCount < 1)
                    return null;

                var lbChart = EzLeoBlackChartBuilder.TryBuild(chart);

                if (lbChart == null)
                    return null;

                return fromChart(lbChart);
            }
            catch
            {
                return null;
            }
        }

        private static EzLeoBlackPatternResult fromChart(EzLeoBlackChart chart)
        {
            double lnRatio = EzLeoBlackPrimitives.LnPercent(chart);
            double hbRatio = hbRowRatio(chart);
            string modeTag = resolveModeTag(lnRatio, hbRatio);

            var patterns = EzLeoBlackFindPatterns.Find(chart);

            if (modeTag == "RC")
                patterns = patterns.Where(p => !ln_core_patterns.Contains(p.Pattern)).ToList();

            var clusters = EzLeoBlackClustering.CalculateClusteredPatterns(patterns, modeTag)
                                               .Where(c => c.BPM > 25 || c.BPM == 0)
                                               .OrderByDescending(c => c.Amount)
                                               .ToList();

            bool canBePruned(EzLeoBlackInternalCluster cluster)
            {
                foreach (var other in clusters)
                {
                    if (other.Pattern == cluster.Pattern
                        && other.Amount * 0.5 > cluster.Amount
                        && other.BPM > cluster.BPM)
                    {
                        return true;
                    }
                }

                return false;
            }

            var filtered = clusters.Where(c => !canBePruned(c)).ToList();

            var prunedClusters = new List<EzLeoBlackInternalCluster>();

            foreach (string pattern in EzLeoBlackCorePattern.List)
                prunedClusters.AddRange(filtered.Where(c => c.Pattern == pattern).Take(3));

            prunedClusters.Sort((a, b) => b.Importance.CompareTo(a.Importance));

            double svAmount = EzLeoBlackPrimitives.SvTime(chart);
            string category = EzLeoBlackCategorise.CategoriseChart(chart.Keys, prunedClusters, svAmount);

            var publicClusters = prunedClusters.Select(toPublic).ToList();

            return new EzLeoBlackPatternResult
            {
                Clusters = publicClusters,
                Category = category,
                TopFiveClusters = publicClusters.Take(5).ToList(),
            };
        }

        private static EzLeoBlackCluster toPublic(EzLeoBlackInternalCluster c) => new EzLeoBlackCluster
        {
            Pattern = c.Pattern,
            Bpm = c.BPM,
            Amount = c.Amount,
            Importance = c.Importance,
            Label = c.Format(),
        };

        private static double hbRowRatio(EzLeoBlackChart chart)
        {
            var rows = chart.Notes;

            if (rows.Count == 0)
                return 0;

            int hbRows = 0;

            foreach (var row in rows)
            {
                int[] data = row.Data;
                bool hasHead = false;
                bool hasNormal = false;

                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == EzLeoBlackNoteType.HOLDHEAD)
                        hasHead = true;

                    if (data[i] == EzLeoBlackNoteType.NORMAL)
                        hasNormal = true;
                }

                if (hasHead && hasNormal)
                    hbRows++;
            }

            return (double)hbRows / rows.Count;
        }

        private static string resolveModeTag(double lnRatio, double hbRatio)
        {
            string tag = EzLeoBlackConfig.ModeTagFromLnRatio(lnRatio);

            if (tag != "Mix")
                return tag;

            if (hbRatio >= EzLeoBlackConfig.HB_ROW_RATIO_THRESHOLD)
                return "HB";

            return "Mix";
        }
    }
}
