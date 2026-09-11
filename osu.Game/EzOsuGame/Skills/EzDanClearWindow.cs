// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Hub <c>weightedDanClearWindow</c> / <c>danIgnoredStrayCount</c> / <c>danFromClears</c> averaging.
    /// </summary>
    public static class EzDanClearWindow
    {
        public const int STRAY_REFERENCE = 5;
        public const double STRAY_GAP = 5;
        public const int STRAY_MAX_IGNORED = 3;
        public const double FAMILY_DECAY = 0.9;

        public readonly record struct Entry(
            EzDanClearEvidenceRow Clear,
            double RepeatWeight,
            double Weight,
            bool IgnoredAsStray);

        public static int IgnoredStrayCount(IReadOnlyList<double> sortedDesc)
        {
            if (sortedDesc.Count == 0)
                return 0;

            int take = Math.Min(STRAY_REFERENCE, sortedDesc.Count);
            double referenceMean = 0;

            for (int i = 0; i < take; i++)
                referenceMean += sortedDesc[i];

            referenceMean /= take;
            double cut = referenceMean - STRAY_GAP;
            int room = Math.Min(STRAY_MAX_IGNORED, sortedDesc.Count - EzDanAlgorithm.CLEAR_QUORUM);
            if (room <= 0)
                return 0;

            int ignored = 0;

            while (ignored < room && sortedDesc[sortedDesc.Count - 1 - ignored] < cut)
                ignored++;

            return ignored;
        }

        /// <summary>
        /// Family key for repeat decay. Without hub <c>chartFamily</c>, fall back to beatmap hash
        /// so same chart at different rates still decays inside one pool.
        /// </summary>
        public static string FamilyKey(string beatmapHash, int keyCount, string side, bool inverse = false)
            => $"{keyCount}:{side}:beatmap:{beatmapHash}:{inverse}";

        public static (IReadOnlyList<Entry> Entries, IReadOnlyList<Entry> Window, double Have) Select(
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            int need = -1)
        {
            if (need < 0)
                need = EzDanAlgorithm.CLEAR_WINDOW;

            if (clears.Count == 0)
                return (Array.Empty<Entry>(), Array.Empty<Entry>(), 0);

            var ordered = clears
                          .OrderByDescending(c => c.CreditedDan)
                          .ThenBy(c => c.BeatmapHash, StringComparer.Ordinal)
                          .ThenByDescending(c => c.Rate)
                          .ToList();

            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            double have = 0;
            var entries = new List<Entry>(ordered.Count);

            foreach (var clear in ordered)
            {
                string key = FamilyKey(clear.BeatmapHash, clear.KeyCount, clear.Side);
                int rank = ranks.GetValueOrDefault(key);
                ranks[key] = rank + 1;
                double repeatWeight = Math.Pow(FAMILY_DECAY, rank);
                double weight = Math.Min(repeatWeight, Math.Max(0, need - have));
                have += weight;
                if (need - have < 1e-10)
                    have = need;

                entries.Add(new Entry(clear, repeatWeight, weight, IgnoredAsStray: false));
            }

            var window = entries.Where(e => e.Weight > 0).ToList();
            int ignored = IgnoredStrayCount(window.Select(e => e.Clear.CreditedDan).ToList());

            for (int i = window.Count - ignored; i < window.Count; i++)
            {
                var e = window[i];
                window[i] = e with { IgnoredAsStray = true };
            }

            return (entries, window, have);
        }

        /// <summary>
        /// Weighted mean of windowed clears (hub <c>danFromClears</c> rawDan). Null under quorum.
        /// </summary>
        public static double? AverageRawDan(IReadOnlyList<EzDanClearEvidenceRow> clears, int need = -1)
        {
            if (clears.Count < EzDanAlgorithm.CLEAR_QUORUM)
                return null;

            var (_, window, _) = Select(clears, need);
            var counted = window.Where(e => !e.IgnoredAsStray).ToList();
            double weight = counted.Sum(e => e.Weight);
            if (!(weight > 0))
                return null;

            double raw = counted.Sum(e => e.Clear.CreditedDan * e.Weight) / weight;
            return Math.Round(raw * 100) / 100;
        }
    }
}
