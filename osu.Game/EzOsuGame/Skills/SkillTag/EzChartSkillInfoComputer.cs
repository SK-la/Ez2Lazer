// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Builds <see cref="EzChartSkillInfo"/> from note data (hub chart-analysis lean filing subset).
    /// LeoBlack patterns fill cluster columns; failure falls back to analyzer tags only.
    /// </summary>
    public static class EzChartSkillInfoComputer
    {
        private static readonly HashSet<string> ln_pattern_ids = createLnPatternIds();

        private static HashSet<string> createLnPatternIds()
        {
            var ids = new HashSet<string>(EzManiaLNAxisExtensions.SubtypeIds, StringComparer.Ordinal)
            {
                EzManiaRCAxis.Ln.ToId(),
            };
            return ids;
        }

        public static EzChartSkillInfo Compute(
            EzManiaChartInput chart,
            double rate = 1) => Compute(chart, rate, out _, out _);

        /// <summary>
        /// Same pass, also handing out the extracted features and pattern scores so callers that need the raw
        /// structure (the CSI axis table) do not run the analysers a second time.
        /// </summary>
        public static EzChartSkillInfo Compute(
            EzManiaChartInput chart,
            double rate,
            out EzChartFeatureExtraction features,
            out EzPatternAnalysis patterns)
        {
            ArgumentNullException.ThrowIfNull(chart);

            features = EzChartFeatureExtractor.Extract(chart, rate);
            patterns = EzPatternAnalyzer.Analyze(chart, rate, features);
            var patternScores = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var hit in patterns.AllPatterns)
            {
                if (!patternScores.TryGetValue(hit.Id, out double existing) || hit.Score > existing)
                    patternScores[hit.Id] = hit.Score;
            }

            var leo = EzLeoBlackPatternAnalyzer.TryAnalyze(chart);
            IReadOnlyList<EzLeoBlackCluster> leoClusters = leo?.Clusters ?? [];
            double? jackShare = leo != null ? EzLeoBlackClusterMetrics.JackShare(leoClusters) : null;
            double? streamShare = leo != null ? EzLeoBlackClusterMetrics.StreamShare(leoClusters) : null;
            bool? techCategory = leo != null ? EzLeoBlackClusterMetrics.TechCategory(leo.Category) : null;
            bool? clusterTrill = leo != null ? EzLeoBlackClusterMetrics.ClusterTrill(leo.Category) : null;
            bool? handstreamCluster = leo != null ? EzLeoBlackClusterMetrics.HandstreamCluster(leo.Category) : null;

            int keyCount = chart.KeyCount;
            double chordjackScore = patternScores.GetValueOrDefault(EzManiaRCAxis.Chordjack.ToId());
            double jackScore = patternScores.GetValueOrDefault(EzManiaRCAxis.Jack.ToId());
            bool isJack = EzPatternTagRules.ChartIsJack(keyCount, chordjackScore, jackScore, jackShare);
            bool vetoesTech = EzPatternTagRules.JackVetoesTech(keyCount, chordjackScore, jackScore, jackShare);
            double lnRatio = features.Metrics.HoldRatio;
            bool chartIsLn = EzPatternTagRules.IsLnPrimary(keyCount, lnRatio);

            var patternIds = patternScores
                             .Where(kvp =>
                                 kvp.Value >= EzPatternTagRules.PatternTagMinScore(kvp.Key)
                                 && !(kvp.Key == EzManiaRCAxis.Tech.ToId() && vetoesTech)
                                 && !(ln_pattern_ids.Contains(kvp.Key) && !chartIsLn))
                             .Select(kvp => kvp.Key)
                             .ToList();

            if (EzPatternTagRules.UsesPatternSkillAxes(keyCount) && isJack && !patternIds.Contains(EzManiaRCAxis.Jack.ToId()))
                patternIds.Add(EzManiaRCAxis.Jack.ToId());

            var jackDemandClusters = new List<EzFourKeyJackDemand.Cluster>(leoClusters.Count);

            foreach (var cluster in leoClusters)
            {
                jackDemandClusters.Add(new EzFourKeyJackDemand.Cluster
                {
                    Label = cluster.Label,
                    Pattern = cluster.Pattern,
                    Bpm = cluster.Bpm,
                    Importance = cluster.Importance,
                });
            }

            var jackDemand = EzFourKeyJackDemand.Classify(new EzFourKeyJackDemand.Input
            {
                KeyCount = keyCount,
                DurationMs = features.DurationMs,
                ChordRatio = features.Metrics.ChordRatio,
                ChordColumnOverlapRatio = features.Metrics.ChordColumnOverlapRatio,
                TwoBackColumnRehitExcess = features.Metrics.TwoBackColumnRehitExcess,
                JackPressure = features.Metrics.JackPressure,
                Patterns = patterns.AllPatterns
                                   .Select(p => new EzFourKeyJackDemand.PatternScore { Id = p.Id, Score = p.Score })
                                   .ToList(),
                Clusters = jackDemandClusters,
            });

            double? lengthSeconds = features.DurationMs > 0 ? features.DurationMs / 1000.0 : chart.TotalLengthMs > 0 ? chart.TotalLengthMs / 1000.0 : null;

            return new EzChartSkillInfo
            {
                Patterns = patternIds.ToArray(),
                JackDemand = jackDemand.Detected,
                JackShare = jackShare,
                StreamShare = streamShare,
                TechCategory = techCategory,
                ClusterTrill = clusterTrill,
                HandstreamCluster = handstreamCluster,
                TechScore = vetoesTech ? 0 : patternScores.GetValueOrDefault(EzManiaRCAxis.Tech.ToId()),
                ChordjackScore = chordjackScore,
                JackScore = jackScore > 0 ? jackScore : null,
                Motion = EzCSIMotionComputer.Compute(features.Notes, keyCount),
                LnRatio = lnRatio,
                Vibro = false,
                DanEligible = true,
                LengthSeconds = lengthSeconds,
                KeyCount = keyCount,
            };
        }

        /// <summary>
        /// Axis table of the structure pass, keyed by bare axis id, together with the typed summary the same pass
        /// produced. Rate is not folded in: these values are rate-invariant.
        /// </summary>
        /// <remarks>
        /// The single producer of the structure axis table. Both the live pass and the background write go through
        /// it, so a persisted frame and a computed one cannot disagree on which axes exist.
        /// </remarks>
        public static IReadOnlyDictionary<string, double> ComputeAxes(EzManiaChartInput chart, out EzChartSkillInfo info)
        {
            ArgumentNullException.ThrowIfNull(chart);

            info = Compute(chart, 1, out var features, out var patterns);

            var axes = new Dictionary<string, double>(EzManiaLNAxisExtensions.FromFeatures(features.Metrics), StringComparer.Ordinal);

            foreach (var hit in patterns.AllPatterns)
            {
                if (!double.IsFinite(hit.Score) || hit.Score <= 0)
                    continue;

                if (!EzManiaLNAxisExtensions.SubtypeIds.Contains(hit.Id) && !EzManiaRCAxisExtensions.AllIds.Contains(hit.Id))
                    continue;

                if (!axes.TryGetValue(hit.Id, out double existing) || hit.Score > existing)
                    axes[hit.Id] = hit.Score;
            }

            return axes;
        }

        public static EzManiaChartInput FromPlayable(IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(playable);

            int keyCount = EzHoldMetrics.ResolveKeyCount(playable);
            var notes = new List<EzManiaNote>();
            double lastTime = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn columnObj)
                    continue;

                int column = columnObj.Column;
                if (column < 0 || column >= keyCount)
                    continue;

                bool isHold = obj is IHasDuration duration && duration.Duration > 0;
                double endTime = isHold && obj is IHasDuration d ? d.EndTime : obj.StartTime;
                notes.Add(new EzManiaNote(obj.StartTime, column, isHold, endTime));
                lastTime = Math.Max(lastTime, endTime);
            }

            var timingPoints = new List<EzManiaTimingPoint>();
            double bpm = 0;

            foreach (var point in playable.ControlPointInfo.TimingPoints)
            {
                if (point.BeatLength <= 0)
                    continue;

                timingPoints.Add(new EzManiaTimingPoint(point.Time, point.BeatLength));
                if (bpm <= 0)
                    bpm = 60000 / point.BeatLength;
            }

            if (bpm <= 0 && playable.ControlPointInfo.TimingPoints.Any())
            {
                var first = playable.ControlPointInfo.TimingPoints.First();
                if (first.BeatLength > 0)
                    bpm = 60000 / first.BeatLength;
            }

            double totalLength = playable.HitObjects.Count > 0
                ? Math.Max(lastTime, playable.HitObjects.Max(h => h.GetEndTime()))
                : 0;

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = bpm,
                TotalLengthMs = totalLength,
                Notes = notes,
                TimingPoints = timingPoints,
            };
        }

        /// <summary>
        /// Port of mania-hub <c>motion-features.ts</c>. 4K wrist-versus-roll shares for the speed/tech model.
        /// </summary>
        public static class EzCSIMotionComputer
        {
            private const double row_epsilon_ms = 10;
            private const double min_gap_ms = 25;
            private const double max_gap_ms = 400;
            private const int min_rows = 24;

            /// <summary>Null when the chart is not 4K or is too short to measure.</summary>
            public static EzMotionFeatures? Compute(IReadOnlyList<EzManiaNote> notes, int keyCount)
            {
                if (keyCount != 4 || notes.Count < 32)
                    return null;

                var rows = buildRows(notes);
                if (rows.Count < min_rows)
                    return null;

                double pairW = 0, sameHandW = 0, miniJackW = 0, chordSwingW = 0;
                double tripW = 0, trillW = 0, crossTrillW = 0;
                double quadW = 0, roll4W = 0;
                double rhythmW = 0, rhythmBreakW = 0;

                for (int i = 0; i + 1 < rows.Count; i++)
                {
                    var a = rows[i];
                    var b = rows[i + 1];
                    double gap = b.Time - a.Time;
                    if (gap <= 0 || gap > max_gap_ms)
                        continue;

                    double w = weightFor(gap);
                    pairW += w;
                    if (a.Columns.Count != b.Columns.Count)
                        chordSwingW += w;

                    if (a.Columns.Count == 1 && b.Columns.Count == 1)
                    {
                        int ca = a.Columns[0];
                        int cb = b.Columns[0];
                        if (ca == cb)
                            miniJackW += w;
                        else if (hand(ca) == hand(cb))
                            sameHandW += w;
                    }
                }

                for (int i = 0; i + 2 < rows.Count; i++)
                {
                    var a = rows[i];
                    var b = rows[i + 1];
                    var c = rows[i + 2];
                    double g0 = b.Time - a.Time;
                    double g1 = c.Time - b.Time;
                    if (g0 <= 0 || g1 <= 0 || g0 > max_gap_ms || g1 > max_gap_ms)
                        continue;

                    double w = weightFor(Math.Max(g0, g1));
                    rhythmW += w;
                    if (!ratioIsMusical(g1 / g0))
                        rhythmBreakW += w;

                    if (a.Columns.Count != 1 || b.Columns.Count != 1 || c.Columns.Count != 1)
                        continue;

                    tripW += w;
                    int ca = a.Columns[0];
                    int cb = b.Columns[0];
                    int cc = c.Columns[0];

                    if (ca == cc && ca != cb)
                    {
                        if (hand(ca) == hand(cb))
                            trillW += w;
                        else
                            crossTrillW += w;
                    }
                }

                for (int i = 0; i + 3 < rows.Count; i++)
                {
                    var window = new[] { rows[i], rows[i + 1], rows[i + 2], rows[i + 3] };
                    double[] gaps =
                    {
                        window[1].Time - window[0].Time,
                        window[2].Time - window[1].Time,
                        window[3].Time - window[2].Time,
                    };

                    if (gaps.Any(gap => gap <= 0 || gap > max_gap_ms))
                        continue;
                    if (window.Any(row => row.Columns.Count != 1))
                        continue;

                    double w = weightFor(gaps.Max());
                    quadW += w;
                    int[] columns = window.Select(row => row.Columns[0]).ToArray();
                    int[] steps = { columns[1] - columns[0], columns[2] - columns[1], columns[3] - columns[2] };
                    if (steps.All(step => step == 1) || steps.All(step => step == -1))
                        roll4W += w;
                }

                // chordSwing / oneHandTrill / roll4 are measured in hub but unused by the filing subset.
                _ = chordSwingW;
                _ = trillW;
                _ = roll4W;

                return new EzMotionFeatures
                {
                    SameHand = round4(share(sameHandW, pairW)),
                    MiniJack = round4(share(miniJackW, pairW)),
                    CrossHandTrill = round4(share(crossTrillW, tripW)),
                    RhythmBreak = round4(share(rhythmBreakW, rhythmW)),
                };
            }

            private static int hand(int column) => column < 2 ? 0 : 1;

            private static double weightFor(double gap) => 1 / Math.Max(gap, min_gap_ms);

            private static double share(double numerator, double denominator) => denominator > 0 ? numerator / denominator : 0;

            private static double round4(double value) => Math.Round(value * 10000) / 10000;

            private static bool ratioIsMusical(double ratio)
            {
                foreach (double target in new[] { 1, 2, 0.5, 1.5, 2.0 / 3.0, 3, 1.0 / 3.0, 4, 0.25 })
                {
                    if (Math.Abs(ratio - target) <= target * 0.08)
                        return true;
                }

                return false;
            }

            private static List<Row> buildRows(IReadOnlyList<EzManiaNote> notes)
            {
                var sorted = notes.OrderBy(n => n.TimeMs).ToList();
                var rows = new List<Row>();

                foreach (var note in sorted)
                {
                    if (rows.Count > 0 && note.TimeMs - rows[^1].Time <= row_epsilon_ms)
                        rows[^1].Columns.Add(note.Column);
                    else
                        rows.Add(new Row(note.TimeMs, new List<int> { note.Column }));
                }

                foreach (var row in rows)
                    row.Columns.Sort();

                return rows;
            }

            private sealed class Row
            {
                public Row(double time, List<int> columns)
                {
                    Time = time;
                    Columns = columns;
                }

                public double Time { get; }
                public List<int> Columns { get; }
            }
        }
    }
}
