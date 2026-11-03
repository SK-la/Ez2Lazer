// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Port of mania-hub <c>classifyFourKeyJackDemand</c> (jack-demand.ts).</summary>
    public static class EzFourKeyJackDemand
    {
        public const int VERSION = 1;

        private const double DENSE_CHORD_RATIO_MIN = 0.70;
        private const double DENSE_CHORD_OVERLAP_MIN = 0.58;
        private const double TWO_BACK_REHIT_EXCESS_MIN = 0.30;
        private const double JACKABLE_MAX_CLUSTER_BPM = 230;
        private const double JACK_CLUSTER_SHARE_MIN = 0.60;
        private const double JACK_CLUSTER_CORROBORATED_SHARE_MIN = 0.25;
        private const double JACK_CLUSTER_CORROBORATED_CHORDJACK_MIN = 0.5;
        private const double JACK_CLUSTER_CORROBORATED_PRESSURE_MIN = 150;
        private const string JACK_CLUSTER_PATTERN = "Jacks";
        private const double MARATHON_DURATION_MIN_MS = 240_000;
        private const double MARATHON_JACK_SCORE_MIN = 0.75;
        private const double MARATHON_JACK_PRESSURE_MIN = 175;
        private const double MARATHON_CHORD_RATIO_MIN = 0.45;
        private const double MARATHON_CHORD_OVERLAP_MIN = 0.55;

        public sealed class Cluster
        {
            public string Label { get; init; } = "";
            public string Pattern { get; init; } = "";
            public double Bpm { get; init; }
            public double Importance { get; init; }
        }

        public sealed class PatternScore
        {
            public string Id { get; init; } = "";
            public double Score { get; init; }
        }

        public sealed class Input
        {
            public int KeyCount { get; init; }
            public double DurationMs { get; init; }
            public double ChordRatio { get; init; }
            public double ChordColumnOverlapRatio { get; init; }
            public double TwoBackColumnRehitExcess { get; init; }
            public double JackPressure { get; init; }
            public IReadOnlyList<PatternScore> Patterns { get; init; } = [];
            public IReadOnlyList<Cluster> Clusters { get; init; } = [];
        }

        public sealed class Verdict
        {
            public int Version { get; init; } = VERSION;
            public bool Detected { get; init; }
            public string[] Reasons { get; init; } = [];
        }

        public static Verdict Classify(Input input)
        {
            if (input.KeyCount != 4)
                return new Verdict { Detected = false, Reasons = [] };

            var reasons = new List<string>();

            bool denseAlternatingChords = input.ChordRatio >= DENSE_CHORD_RATIO_MIN
                                          && input.ChordColumnOverlapRatio >= DENSE_CHORD_OVERLAP_MIN
                                          && input.TwoBackColumnRehitExcess >= TWO_BACK_REHIT_EXCESS_MIN;
            if (denseAlternatingChords)
                reasons.Add("dense_alternating_chords");

            double? meanClusterBpm = weightedMeanClusterBpm(input.Clusters);
            bool jackableSpeed = meanClusterBpm == null || meanClusterBpm <= JACKABLE_MAX_CLUSTER_BPM;

            double jackClusterShare = jackClusterImportanceShare(input.Clusters);
            bool jackClusterDominant = jackableSpeed
                                       && meanClusterBpm != null
                                       && jackClusterShare >= JACK_CLUSTER_SHARE_MIN;
            if (jackClusterDominant)
                reasons.Add("jack_cluster_dominant");

            bool jackClusterCorroborated = !jackClusterDominant
                                           && jackableSpeed
                                           && meanClusterBpm != null
                                           && jackClusterShare >= JACK_CLUSTER_CORROBORATED_SHARE_MIN
                                           && patternScore(input.Patterns, "chordjack") >= JACK_CLUSTER_CORROBORATED_CHORDJACK_MIN
                                           && input.JackPressure >= JACK_CLUSTER_CORROBORATED_PRESSURE_MIN;
            if (jackClusterCorroborated)
                reasons.Add("jack_cluster_corroborated");

            bool jackMarathon = jackableSpeed
                                && input.DurationMs >= MARATHON_DURATION_MIN_MS
                                && patternScore(input.Patterns, "jack") >= MARATHON_JACK_SCORE_MIN
                                && input.JackPressure >= MARATHON_JACK_PRESSURE_MIN
                                && input.ChordRatio >= MARATHON_CHORD_RATIO_MIN
                                && input.ChordColumnOverlapRatio >= MARATHON_CHORD_OVERLAP_MIN;
            if (jackMarathon)
                reasons.Add("jack_marathon");

            return new Verdict
            {
                Detected = reasons.Count > 0,
                Reasons = reasons.ToArray(),
            };
        }

        private static double patternScore(IReadOnlyList<PatternScore> patterns, string id)
        {
            double score = 0;

            foreach (var pattern in patterns)
            {
                if (pattern.Id != id)
                    continue;
                if (double.IsFinite(pattern.Score))
                    score = Math.Max(score, pattern.Score);
            }

            return score;
        }

        private static double jackClusterImportanceShare(IReadOnlyList<Cluster> clusters)
        {
            double total = 0;
            double jack = 0;

            foreach (var cluster in clusters)
            {
                if (!double.IsFinite(cluster.Importance) || cluster.Importance <= 0)
                    continue;

                total += cluster.Importance;
                if (!double.IsFinite(cluster.Bpm) || cluster.Bpm <= 0 || cluster.Bpm > JACKABLE_MAX_CLUSTER_BPM)
                    continue;
                if (cluster.Pattern == JACK_CLUSTER_PATTERN)
                    jack += cluster.Importance;
            }

            return total > 0 ? jack / total : 0;
        }

        private static double? weightedMeanClusterBpm(IReadOnlyList<Cluster> clusters)
        {
            double weight = 0;
            double weighted = 0;

            foreach (var cluster in clusters)
            {
                if (!double.IsFinite(cluster.Importance) || cluster.Importance <= 0)
                    continue;
                if (!double.IsFinite(cluster.Bpm) || cluster.Bpm <= 0)
                    continue;

                weight += cluster.Importance;
                weighted += cluster.Importance * cluster.Bpm;
            }

            return weight > 0 ? weighted / weight : null;
        }
    }
}
