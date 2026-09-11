// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Internal cluster before public projection (hub clustering.js).</summary>
    internal sealed class EzLeoBlackInternalCluster
    {
        public required string Pattern { get; init; }
        public required List<(string Name, double Ratio)> SpecificTypes { get; init; }
        public double RatingMultiplier { get; set; }
        public double BPM { get; init; }
        public bool Mixed { get; init; }
        public double Amount { get; init; }

        public double Importance => Amount * RatingMultiplier * BPM;

        public string Format(double rate = 1.0)
        {
            string name = SpecificTypes.Count > 0
                          && SpecificTypes[0].Ratio >= EzLeoBlackConfig.CLUSTER_SPECIFIC_NAME_MIN_RATIO
                ? SpecificTypes[0].Name
                : Pattern;

            if (Mixed)
                return $"~{Math.Round(BPM * rate)}BPM Mixed {name}";

            return $"{Math.Round(BPM * rate)}BPM {name}";
        }
    }

    /// <summary>Hub <c>patterns/clustering.js</c>.</summary>
    internal static class EzLeoBlackClustering
    {
        public static List<EzLeoBlackInternalCluster> CalculateClusteredPatterns(
            IReadOnlyList<EzLeoBlackFoundPattern> patterns,
            string modeTag = "Mix")
        {
            var pwc = assignClusters(patterns);
            return specificClusters(pwc, modeTag);
        }

        private static double patternAmount(List<(double Start, double End)> sortedStartsEnds)
        {
            double totalTime = 0;
            double currentStart = sortedStartsEnds[0].Start;
            double currentEnd = sortedStartsEnds[0].End;

            foreach (var (start, end) in sortedStartsEnds)
            {
                if (currentEnd < end)
                {
                    totalTime += currentEnd - currentStart;
                    currentStart = start;
                    currentEnd = end;
                }
                else
                {
                    currentEnd = Math.Max(currentEnd, end);
                }
            }

            totalTime += currentEnd - currentStart;
            return totalTime;
        }

        private static bool isTimedMspb(double value)
            => value >= EzLeoBlackConfig.CLUSTER_TIMED_MIN_MSPB;

        private sealed class ClusterBuilder
        {
            public double SumMs;
            public int TimedCount;
            public double OriginalMsPerBeat;
            public int Count = 1;
            public double BPM;

            public ClusterBuilder(double value)
            {
                bool timed = isTimedMspb(value);
                SumMs = timed ? value : 0;
                TimedCount = timed ? 1 : 0;
                OriginalMsPerBeat = value;
            }

            public void Add(double v)
            {
                Count++;

                if (isTimedMspb(v))
                {
                    SumMs += v;
                    TimedCount++;
                }
            }

            public void Calculate()
                => BPM = TimedCount == 0 ? 0 : Math.Round(60000.0 / (SumMs / TimedCount));

            public double Value => BPM;
        }

        private static List<(EzLeoBlackFoundPattern Pattern, ClusterBuilder Cluster)> assignClusters(
            IReadOnlyList<EzLeoBlackFoundPattern> patterns)
        {
            var bpmsNonMixed = new List<ClusterBuilder>();
            var bpmsMixed = new Dictionary<string, ClusterBuilder>(StringComparer.Ordinal);

            ClusterBuilder addToCluster(double msPerBeat)
            {
                foreach (var c in bpmsNonMixed)
                {
                    if (Math.Abs(c.OriginalMsPerBeat - msPerBeat) < EzLeoBlackConfig.BPM_CLUSTER_THRESHOLD)
                    {
                        c.Add(msPerBeat);
                        return c;
                    }
                }

                var created = new ClusterBuilder(msPerBeat);
                bpmsNonMixed.Add(created);
                return created;
            }

            ClusterBuilder addToMixedCluster(string pattern, double value)
            {
                if (bpmsMixed.TryGetValue(pattern, out var existing))
                {
                    existing.Add(value);
                    return existing;
                }

                var created = new ClusterBuilder(value);
                bpmsMixed[pattern] = created;
                return created;
            }

            var patternsWithClusters = new List<(EzLeoBlackFoundPattern, ClusterBuilder)>();

            foreach (var p in patterns)
            {
                var c = p.Mixed ? addToMixedCluster(p.Pattern, p.MsPerBeat) : addToCluster(p.MsPerBeat);
                patternsWithClusters.Add((p, c));
            }

            foreach (var c in bpmsNonMixed)
                c.Calculate();

            foreach (var c in bpmsMixed.Values)
                c.Calculate();

            return patternsWithClusters;
        }

        private static List<EzLeoBlackInternalCluster> specificClusters(
            List<(EzLeoBlackFoundPattern Pattern, ClusterBuilder Cluster)> patternsWithClusters,
            string modeTag)
        {
            var groups = new Dictionary<string, (string Pattern, bool Mixed, double Bpm, List<(EzLeoBlackFoundPattern, ClusterBuilder)> Data)>(StringComparer.Ordinal);

            foreach (var (p, c) in patternsWithClusters)
            {
                string key = $"{p.Pattern}@@{(p.Mixed ? 1 : 0)}@@{c.Value}";

                if (!groups.TryGetValue(key, out var group))
                {
                    group = (p.Pattern, p.Mixed, c.Value, []);
                    groups[key] = group;
                }

                group.Data.Add((p, c));
            }

            var outList = new List<EzLeoBlackInternalCluster>();

            foreach (var group in groups.Values)
            {
                var startsEnds = group.Data
                                      .Select(m => (m.Item1.Start, m.Item1.End))
                                      .OrderBy(x => x.Start)
                                      .ToList();

                int dataCount = group.Data.Count;
                var counter = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var (m, _) in group.Data)
                {
                    if (m.SpecificType != null)
                        counter[m.SpecificType] = counter.GetValueOrDefault(m.SpecificType) + 1;
                }

                var specificTypes = counter
                                    .Select(kv => (Name: kv.Key, Ratio: (double)kv.Value / dataCount))
                                    .OrderByDescending(x => x.Ratio)
                                    .ToList();

                string? dominantSpecific = specificTypes.Count > 0 ? specificTypes[0].Name : null;
                double amount = startsEnds.Count > 0 ? patternAmount(startsEnds) : 0;

                outList.Add(new EzLeoBlackInternalCluster
                {
                    Pattern = group.Pattern,
                    SpecificTypes = specificTypes,
                    RatingMultiplier = EzLeoBlackFindPatterns.ResolveRatingMultiplier(group.Pattern, dominantSpecific, modeTag),
                    BPM = group.Bpm,
                    Mixed = group.Mixed,
                    Amount = amount,
                });
            }

            bool hasDw = outList.Any(c => c.Pattern is "Density" or "Wildcard");

            if (hasDw && EzLeoBlackConfig.RELEASE_WITH_DW_MULTIPLIER != 1.0)
            {
                foreach (var c in outList)
                {
                    if (c.SpecificTypes.Any(st => st.Name == "Release" && st.Ratio > 0))
                        c.RatingMultiplier *= EzLeoBlackConfig.RELEASE_WITH_DW_MULTIPLIER;
                }
            }

            return outList;
        }
    }
}
