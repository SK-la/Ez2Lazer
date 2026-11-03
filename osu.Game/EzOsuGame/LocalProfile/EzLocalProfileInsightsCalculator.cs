// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Ports mania-hub <c>calculateUserProfileInsights</c> for local drill scores (no tracked tail merge).
    /// </summary>
    public static class EzLocalProfileInsightsCalculator
    {
        public const double KEY_PP_DECAY = 0.95;
        public const int KEY_PP_WINDOW_MIN = 100;
        public const int KEY_PP_LIST_LIMIT = 200;
        public const int TOP_PLAY_WINDOW = 200;

        public static EzLocalProfileInsights Calculate(IReadOnlyList<EzLocalProfileInsightPlay> windowPlays)
        {
            var scores = windowPlays
                         .Where(p => p.Pp > 0 && p.KeyCount > 0)
                         .OrderByDescending(p => p.Pp)
                         .ThenBy(p => p.BeatmapOnlineId)
                         .Take(TOP_PLAY_WINDOW)
                         .ToList();

            var keyCounts = new Dictionary<int, int>();
            var keyPpByKey = new Dictionary<int, List<(int BeatmapId, double Pp)>>();
            int convertPlays = 0;
            var modCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int moddedPlayCount = 0;
            var bpmEntries = new List<(double Bpm, double Weight, int? KeyCount, EzLocalProfileInsightPlay Play)>();
            var ppValues = new List<double>();
            var datedScores = new List<(EzLocalProfileInsightPlay Play, long Ms)>();

            for (int index = 0; index < scores.Count; index++)
            {
                var play = scores[index];

                if (play.KeyCount > 0)
                {
                    keyCounts.TryGetValue(play.KeyCount, out int existing);
                    keyCounts[play.KeyCount] = existing + 1;
                }

                if (play.IsConvert)
                {
                    convertPlays++;
                }
                else if (play.KeyCount > 0)
                {
                    if (!keyPpByKey.TryGetValue(play.KeyCount, out var bucket))
                        keyPpByKey[play.KeyCount] = bucket = new List<(int, double)>();

                    int beatmapId = play.BeatmapOnlineId > 0 ? play.BeatmapOnlineId : -(index + 1);
                    bucket.Add((beatmapId, play.Pp));
                }

                if (play.ModAcronyms.Count > 0)
                {
                    moddedPlayCount++;

                    foreach (string mod in play.ModAcronyms)
                    {
                        modCounts.TryGetValue(mod, out int count);
                        modCounts[mod] = count + 1;
                    }
                }

                if (play.Bpm > 0 && double.IsFinite(play.Bpm))
                {
                    double rate = play.Rate > 0 && double.IsFinite(play.Rate) ? play.Rate : 1;
                    bpmEntries.Add((play.Bpm * rate, Math.Pow(KEY_PP_DECAY, index), play.KeyCount > 0 ? play.KeyCount : null, play));
                }

                ppValues.Add(play.Pp);

                long ms = play.Date.ToUnixTimeMilliseconds();
                if (ms > 0)
                    datedScores.Add((play, ms));
            }

            var sortedKeySplit = keyCounts
                                 .Select(kv => new EzLocalProfileKeySplit(kv.Key, kv.Value))
                                 .OrderByDescending(k => k.Count)
                                 .ThenBy(k => k.KeyCount)
                                 .ToList();

            datedScores.Sort((a, b) => a.Ms.CompareTo(b.Ms));
            var sortedPpValues = ppValues.OrderByDescending(v => v).ToList();

            EzLocalProfileBpmRange? bpmRange = null;

            if (bpmEntries.Count > 0)
            {
                var minEntry = bpmEntries[0];
                var maxEntry = bpmEntries[0];

                foreach (var entry in bpmEntries)
                {
                    if (entry.Bpm < minEntry.Bpm)
                        minEntry = entry;
                    if (entry.Bpm > maxEntry.Bpm)
                        maxEntry = entry;
                }

                bpmRange = new EzLocalProfileBpmRange(minEntry.Bpm, maxEntry.Bpm, minEntry.Play, maxEntry.Play);
            }

            var bpmByKeyMap = new Dictionary<int, List<(double Value, double Weight)>>();

            foreach (var entry in bpmEntries)
            {
                if (entry.KeyCount is not int key)
                    continue;

                if (!bpmByKeyMap.TryGetValue(key, out var list))
                    bpmByKeyMap[key] = list = new List<(double, double)>();

                list.Add((entry.Bpm, entry.Weight));
            }

            var bpmByKeyMode = bpmByKeyMap
                               .Select(kv => new EzLocalProfileBpmByKey(kv.Key, GetWeightedMedian(kv.Value) ?? 0, kv.Value.Count))
                               .OrderBy(b => b.KeyCount)
                               .ToList();

            double keyPpCutoff = scores.Count >= KEY_PP_WINDOW_MIN && sortedPpValues.Count > 0
                ? sortedPpValues[^1]
                : 0;

            var keyPp = buildKeyPpBuckets(keyPpByKey, keyPpCutoff);

            return new EzLocalProfileInsights
            {
                SampleSize = scores.Count,
                KeySplit = sortedKeySplit,
                KeyPp = keyPp,
                KeyPpConverts = convertPlays,
                KeyPpCutoff = keyPpCutoff,
                MostUsedMod = getTopCountEntry(modCounts, moddedPlayCount),
                ModBreakdown = modCounts
                               .OrderByDescending(kv => kv.Value)
                               .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                               .Select(kv => new EzLocalProfileCountEntry(kv.Key, kv.Value, scores.Count))
                               .ToList(),
                MedianBpm = GetWeightedMedian(bpmEntries.Select(e => (e.Bpm, e.Weight)).ToList()),
                BpmRange = bpmRange,
                BpmByKeyMode = bpmByKeyMode,
                NewestTopPlay = datedScores.Count > 0 ? datedScores[^1].Play : null,
                OldestTopPlay = datedScores.Count > 0 ? datedScores[0].Play : null,
                PpRange = sortedPpValues.Count > 0
                    ? new EzLocalProfilePpRange(sortedPpValues[0], sortedPpValues[^1])
                    : null,
                PpDistribution = BuildPpDistribution(sortedPpValues),
                PpCumulative = BuildPpCumulativeDistribution(sortedPpValues),
            };
        }

        public static IReadOnlyList<EzLocalProfilePpDistributionBucket> BuildPpDistribution(IReadOnlyList<double> ppValues)
        {
            if (ppValues.Count == 0)
                return Array.Empty<EzLocalProfilePpDistributionBucket>();

            double top = ppValues.Max();
            double bottom = ppValues.Min();
            int step = getPpDistributionStep(top);
            int maxThreshold = Math.Max(step, (int)Math.Floor(top / step) * step);
            int minThreshold = Math.Max(step, (int)Math.Floor(bottom / step) * step);
            int total = ppValues.Count;
            var buckets = new List<EzLocalProfilePpDistributionBucket>();

            for (int threshold = maxThreshold; threshold >= minThreshold; threshold -= step)
            {
                int? upper = threshold == maxThreshold ? null : threshold + step;
                int count = ppValues.Count(pp => pp >= threshold && (upper == null || pp < upper));

                if (count > 0)
                {
                    buckets.Add(new EzLocalProfilePpDistributionBucket(
                        threshold,
                        upper - 1,
                        count,
                        total));
                }
            }

            int belowCount = ppValues.Count(pp => pp < minThreshold);

            if (belowCount > 0)
            {
                buckets.Add(new EzLocalProfilePpDistributionBucket(
                    null,
                    minThreshold - 1,
                    belowCount,
                    total));
            }

            return buckets;
        }

        public static IReadOnlyList<EzLocalProfilePpCumulativeRow> BuildPpCumulativeDistribution(IReadOnlyList<double> ppValuesDescending)
        {
            var ppValues = ppValuesDescending
                           .Where(double.IsFinite)
                           .OrderByDescending(pp => pp)
                           .ToList();

            if (ppValues.Count == 0)
                return Array.Empty<EzLocalProfilePpCumulativeRow>();

            double top = ppValues[0];
            double bottom = ppValues[^1];
            int step = top < 250 ? 50 : 100;
            int maxThreshold = Math.Max(0, (int)Math.Floor(top / step) * step);
            int minThreshold = Math.Max(0, (int)Math.Floor(bottom / step) * step);
            var rows = new List<EzLocalProfilePpCumulativeRow>();
            int count = 0;

            for (int threshold = maxThreshold; threshold >= minThreshold; threshold -= step)
            {
                while (count < ppValues.Count && ppValues[count] >= threshold)
                    count++;

                rows.Add(new EzLocalProfilePpCumulativeRow(threshold, count, ppValues.Count));
            }

            return rows;
        }

        public static double? GetWeightedMedian(IReadOnlyList<(double Value, double Weight)> entries)
        {
            if (entries.Count == 0)
                return null;

            var sorted = entries.OrderBy(e => e.Value).ToList();
            double totalWeight = sorted.Sum(e => e.Weight);
            if (totalWeight <= 0)
                return null;

            double cumulative = 0;

            foreach (var entry in sorted)
            {
                cumulative += entry.Weight;
                if (cumulative >= totalWeight / 2)
                    return entry.Value;
            }

            return sorted[^1].Value;
        }

        private static IReadOnlyList<EzLocalProfileKeyPpBucket> buildKeyPpBuckets(
            Dictionary<int, List<(int BeatmapId, double Pp)>> windowByKeyCount,
            double cutoffPp)
        {
            return windowByKeyCount
                   .Select(kv =>
                   {
                       var merged = kv.Value
                                      .OrderByDescending(p => p.Pp)
                                      .ThenBy(p => p.BeatmapId)
                                      .Take(KEY_PP_LIST_LIMIT)
                                      .ToList();

                       double weighted = 0;

                       for (int i = 0; i < merged.Count; i++)
                           weighted += merged[i].Pp * Math.Pow(KEY_PP_DECAY, i);

                       return new EzLocalProfileKeyPpBucket(
                           kv.Key,
                           weighted,
                           merged.Count,
                           getKeyPpMissingBound(merged.Count, cutoffPp));
                   })
                   .OrderByDescending(b => b.WeightedPp)
                   .ThenBy(b => b.KeyCount)
                   .ToList();
        }

        private static double getKeyPpMissingBound(int count, double cutoffPp)
        {
            if (cutoffPp <= 0)
                return 0;

            return cutoffPp * Math.Pow(KEY_PP_DECAY, count) / (1 - KEY_PP_DECAY);
        }

        private static int getPpDistributionStep(double top)
        {
            if (top < 250) return 50;
            if (top < 1000) return 100;
            if (top < 2000) return 250;

            return 500;
        }

        private static EzLocalProfileCountEntry? getTopCountEntry(Dictionary<string, int> counts, int total)
        {
            if (counts.Count == 0 || total <= 0)
                return null;

            var top = counts
                      .OrderByDescending(kv => kv.Value)
                      .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                      .First();

            return new EzLocalProfileCountEntry(top.Key, top.Value, total);
        }
    }
}
