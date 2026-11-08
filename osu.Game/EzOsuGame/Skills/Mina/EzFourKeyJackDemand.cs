// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>Port of mania-hub <c>classifyFourKeyJackDemand</c> (jack-demand.ts).</summary>
    public static class EzFourKeyJackDemand
    {
        public const int VERSION = 20240102;

        private const double dense_chord_ratio_min = 0.70;
        private const double dense_chord_overlap_min = 0.58;
        private const double two_back_rehit_excess_min = 0.30;
        private const double jackable_max_cluster_bpm = 230;
        private const double jack_cluster_share_min = 0.60;
        private const double jack_cluster_corroborated_share_min = 0.25;
        private const double jack_cluster_corroborated_chordjack_min = 0.5;
        private const double jack_cluster_corroborated_pressure_min = 150;
        private const string jack_cluster_pattern = "Jacks";
        private const double marathon_duration_min_ms = 240_000;
        private const double marathon_jack_score_min = 0.75;
        private const double marathon_jack_pressure_min = 175;
        private const double marathon_chord_ratio_min = 0.45;
        private const double marathon_chord_overlap_min = 0.55;

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

            bool denseAlternatingChords = input.ChordRatio >= dense_chord_ratio_min
                                          && input.ChordColumnOverlapRatio >= dense_chord_overlap_min
                                          && input.TwoBackColumnRehitExcess >= two_back_rehit_excess_min;
            if (denseAlternatingChords)
                reasons.Add("dense_alternating_chords");

            double? meanClusterBpm = weightedMeanClusterBpm(input.Clusters);
            bool jackableSpeed = meanClusterBpm == null || meanClusterBpm <= jackable_max_cluster_bpm;

            double jackClusterShare = jackClusterImportanceShare(input.Clusters);
            bool jackClusterDominant = jackableSpeed
                                       && meanClusterBpm != null
                                       && jackClusterShare >= jack_cluster_share_min;
            if (jackClusterDominant)
                reasons.Add("jack_cluster_dominant");

            bool jackClusterCorroborated = !jackClusterDominant
                                           && jackableSpeed
                                           && meanClusterBpm != null
                                           && jackClusterShare >= jack_cluster_corroborated_share_min
                                           && patternScore(input.Patterns, EzManiaRCAxis.Chordjack.ToId()) >= jack_cluster_corroborated_chordjack_min
                                           && input.JackPressure >= jack_cluster_corroborated_pressure_min;
            if (jackClusterCorroborated)
                reasons.Add("jack_cluster_corroborated");

            bool jackMarathon = jackableSpeed
                                && input.DurationMs >= marathon_duration_min_ms
                                && patternScore(input.Patterns, EzManiaRCAxis.Jack.ToId()) >= marathon_jack_score_min
                                && input.JackPressure >= marathon_jack_pressure_min
                                && input.ChordRatio >= marathon_chord_ratio_min
                                && input.ChordColumnOverlapRatio >= marathon_chord_overlap_min;
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
                if (!double.IsFinite(cluster.Bpm) || cluster.Bpm <= 0 || cluster.Bpm > jackable_max_cluster_bpm)
                    continue;

                if (cluster.Pattern == jack_cluster_pattern)
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
