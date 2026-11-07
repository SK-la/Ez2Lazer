// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    ///     Port of mania-hub <c>dan-estimator/patterns.ts</c> scoring used for skillset filing tags.
    /// </summary>
    public static class EzPatternAnalyzer
    {
        private static readonly HashSet<string> ln_subtype_ids = new HashSet<string>(EzManiaLNAxisExtensions.SubtypeIds, StringComparer.Ordinal);

        private const double INVERSE_WINDOW_MS = 8000;
        private const int INVERSE_WINDOW_MIN_PAIRS = 20;
        private const int INVERSE_WINDOW_MIN_WINDOWS = 3;
        private const double INVERSE_WINDOW_RATIO = 0.65;

        public static EzPatternAnalysis Analyze(EzManiaChartInput map, double rate = 1, EzChartFeatureExtraction? precomputed = null)
            => Analyze(map, rate, precomputed, CancellationToken.None);

        public static EzPatternAnalysis Analyze(EzManiaChartInput map, double rate, EzChartFeatureExtraction? precomputed, CancellationToken cancellationToken)
        {
            var features = precomputed ?? EzChartFeatureExtractor.Extract(map, rate, cancellationToken);
            var metrics = features.Metrics;
            var orderedRows = features.OrderedRows;
            var stats = getRowPatternStats(orderedRows, metrics.KeyCount, cancellationToken);
            int rowCount = Math.Max(1, stats.RowCount);
            double chordRatio = metrics.ChordRatio;
            double twoNoteRatio = ratio(stats.TwoNoteRows, rowCount);
            double threeNoteRatio = ratio(stats.ThreeNoteRows, rowCount);
            double threePlusRatio = ratio(stats.ThreePlusRows, rowCount);
            double fourPlusRatio = ratio(stats.FourPlusRows, rowCount);
            double repeatedChordRatio = ratio(stats.RepeatedChordRows, Math.Max(1, stats.ChordRows - 1));
            double lowChordGate = EzChartFeatureMath.Clamp01((0.34 - chordRatio) / 0.3);
            double streamActivity = Math.Max(
                pressure(metrics.StreamPressure, 1.5, 5.5),
                pressure(metrics.SustainedNps10s, metrics.KeyCount >= 6 ? 7 : 12, metrics.KeyCount >= 6 ? 18 : 27));
            double chordstreamGate = EzChartFeatureMath.MinGate(
                pressure(chordRatio, metrics.KeyCount >= 6 ? 0.14 : 0.24, metrics.KeyCount >= 6 ? 0.5 : 0.58),
                pressure(metrics.SustainedNps10s, metrics.KeyCount >= 6 ? 6 : 11, metrics.KeyCount >= 6 ? 17 : 25));
            double chordOverlapGate = pressure(metrics.ChordColumnOverlapRatio, 0.18, 0.4);
            double chordjackBase = Math.Max(
                EzChartFeatureMath.MinGate(pressure(chordRatio, 0.28, 0.64), pressure(metrics.ChordjackPressure, 70, 185), chordOverlapGate),
                EzChartFeatureMath.MinGate(pressure(chordRatio, 0.36, 0.72), pressure(metrics.JackPressure, 80, 180), chordOverlapGate));
            double techScore = Math.Max(
                EzChartFeatureMath.MinGate(pressure(metrics.TechPressure, 3.5, 8.5), pressure(metrics.RowPatternChangeRate, 0.34, 0.66)),
                EzChartFeatureMath.MinGate(
                    pressure(metrics.ChordSizeChangeRate, 0.25, 0.58),
                    pressure(metrics.DirectionChangeRate, 0.35, 0.72),
                    pressure(metrics.RowIntervalEntropy, 1.1, 2.4)));
            double dataConfidence = EzChartFeatureMath.Clamp01(0.35 + Math.Min(0.4, metrics.NoteCount / 2500.0) + Math.Min(0.25, stats.RowCount / 900.0));
            var candidates = new List<EzPatternHit>();
            double beatLengthMs = double.IsFinite(map.Bpm) && map.Bpm > 0 ? 60000 / (map.Bpm * rate) : 0;
            var lnStats = getLnPatternStats(features.Notes, orderedRows, metrics.KeyCount, beatLengthMs, cancellationToken);
            double lnScore = Math.Max(
                pressure(metrics.HoldRatio, 0.03, 0.32),
                Math.Max(
                    EzChartFeatureMath.MinGate(pressure(metrics.LnDensity, 0.02, 0.18), pressure(metrics.LnOverlapPressure, 0.4, 2.4)),
                    Math.Max(
                        EzChartFeatureMath.MinGate(pressure(metrics.LnReleasePressure, 1.2, 5.5), pressure(metrics.HoldRatio, 0.015, 0.16)),
                        EzChartFeatureMath.MinGate(pressure(metrics.LnChordPressure, 0.15, 0.65), pressure(metrics.HoldRatio, 0.02, 0.18)))));

            bool lnSubtypeKeys = metrics.KeyCount is 7 or 4;
            double lnSubtypeGate = lnSubtypeKeys ? pressure(lnScore, 0.18, 0.58) : 0;
            double lnInverseShape = Math.Max(
                pressure(lnStats.InverseReleaseRatio, 0.24, 0.62) * EzChartFeatureMath.Clamp01((0.16 - lnStats.MixedRowRatio) / 0.16),
                metrics.KeyCount == 7
                    ? pressure(lnStats.InverseWindowCoverage, 0.35, 0.75) * EzChartFeatureMath.Clamp01((0.45 - lnStats.MixedRowRatio) / 0.25)
                    : 0);
            double lnInverseScore = lnSubtypeGate * EzChartFeatureMath.MinGate(
                lnInverseShape,
                pressure(metrics.LnDensity, 0.12, 0.5),
                Math.Max(pressure(metrics.LnOverlapPressure, 1.1, 3.1), pressure(metrics.LnHoldDurationP90, 260, 520)));

            double lnReleaseWall = EzChartFeatureMath.MinGate(
                pressure(metrics.HoldRatio, 0.85, 0.97),
                pressure(lnStats.HeldWhileReleaseRatio, 0.45, 0.65),
                pressure(metrics.LnReleasePressure, 32, 46),
                EzChartFeatureMath.Clamp01((0.5 - lnStats.InverseReleaseRatio) / 0.15));
            double lnReleaseScore = metrics.KeyCount == 7
                ? lnSubtypeGate * EzChartFeatureMath.MinGate(
                    pressure(metrics.HoldRatio, 0.2, 0.46),
                    Math.Max(pressure(lnStats.ActiveReleaseRatio, 0.55, 0.88), lnReleaseWall),
                    pressure(metrics.LnReleasePressure, 2.5, 6),
                    Math.Max(
                        EzChartFeatureMath.MinGate(
                            pressure(lnStats.HoldDurationP50, 150, 330),
                            Math.Max(
                                pressure(lnStats.CoordinatedReleaseRatio, 0.34, 0.68),
                                pressure(lnStats.HeldWhileReleaseRatio, 0.32, 0.62))),
                        Math.Max(
                            EzChartFeatureMath.MinGate(
                                pressure(lnStats.HeldWhileReleaseRatio, 0.45, 0.7),
                                pressure(metrics.HoldRatio, 0.6, 0.85),
                                pressure(lnStats.ActiveReleaseRatio, 0.7, 0.9)),
                            lnReleaseWall)))
                : 0;

            double lnTechBurst = Math.Max(pressure(metrics.FastRowRatio, 0.18, 0.36), pressure(metrics.RowBurstPressure, 16, 26));
            double lnTechCoordination = metrics.KeyCount == 4
                ? Math.Max(pressure(lnStats.TapWhileHoldingRatio, 0.1, 0.25), pressure(lnStats.HeadTailSwitchRatio, 0.3, 0.55))
                : Math.Max(
                    pressure(lnStats.TapWhileHoldingRatio, 0.04, 0.11),
                    Math.Max(pressure(lnStats.HeadTailSwitchRatio, 0.52, 0.72), pressure(metrics.ChordSizeChangeRate, 0.55, 0.78)));
            double lnTechContentFloor = metrics.KeyCount == 4 ? pressure(metrics.HoldRatio, 0.2, 0.4) : 1;
            double lnTechScore = lnSubtypeGate * EzChartFeatureMath.MinGate(
                lnTechBurst,
                lnTechCoordination,
                lnTechContentFloor,
                Math.Max(pressure(metrics.TechPressure, 4.2, 8.4), pressure(metrics.RowIntervalEntropy, 2.0, 2.45)),
                EzChartFeatureMath.Clamp01((0.6 - lnStats.InverseReleaseRatio) / 0.34),
                EzChartFeatureMath.Clamp01((0.66 - lnStats.ReleaseOnlyRatio) / 0.22));
            double lnGeneralCoverage = Math.Max(
                EzChartFeatureMath.MinGate(
                    pressure(metrics.HoldRatio, 0.35, 0.82),
                    pressure(metrics.LnChordPressure, 0.32, 0.66),
                    pressure(lnStats.HeadTailSwitchRatio, 0.35, 0.62)),
                EzChartFeatureMath.MinGate(
                    pressure(metrics.LnDensity, 0.12, 0.42),
                    pressure(metrics.LnReleasePressure, 8, 24),
                    pressure(chordRatio, 0.28, 0.62)));
            double lnSpecialtyScore = Math.Max(lnInverseScore, Math.Max(lnReleaseScore, lnTechScore));
            double lnGeneralScore = lnSubtypeGate * lnGeneralCoverage * EzChartFeatureMath.Clamp01((0.7 - lnSpecialtyScore) / 0.4);

            candidates.Add(hit(EzManiaRCAxis.Ln, metrics.KeyCount == 7 ? lnScore * 0.62 : lnScore, dataConfidence));

            if (lnSubtypeKeys)
            {
                candidates.Add(hit(EzManiaLNAxis.General, lnGeneralScore, dataConfidence));
                candidates.Add(hit(EzManiaLNAxis.Inverse, lnInverseScore, dataConfidence));
                candidates.Add(hit(EzManiaLNAxis.Tech, lnTechScore, dataConfidence));
                if (metrics.KeyCount == 7)
                    candidates.Add(hit(EzManiaLNAxis.ReleaseSubtype, lnReleaseScore, dataConfidence));
            }

            if (metrics.KeyCount == 4)
            {
                double jackScore = Math.Max(
                    pressure(metrics.JackPressure, 75, 185) * (0.55 + lowChordGate * 0.35),
                    EzChartFeatureMath.MinGate(pressure(metrics.JackPressure, 110, 200), pressure(metrics.FastRowRatio, 0.05, 0.28)));
                candidates.Add(hit(EzManiaRCAxis.Jack, jackScore, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Chordjack, chordjackBase, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Speedjack, chordjackBase * EzChartFeatureMath.MinGate(
                    pressure(twoNoteRatio, 0.18, 0.42),
                    pressure(metrics.JackPressure, 115, 205),
                    EzChartFeatureMath.Clamp01((0.68 - threePlusRatio) / 0.35)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Handjack, chordjackBase * EzChartFeatureMath.MinGate(
                    pressure(threePlusRatio, 0.08, 0.28),
                    pressure(stats.AverageChordSize, 2.15, 3.1),
                    pressure(metrics.JackPressure, 95, 180)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Tech, techScore, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Stream, lowChordGate * streamActivity * EzChartFeatureMath.Clamp01((150 - metrics.JackPressure) / 120), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Dumpstream, lowChordGate * streamActivity * EzChartFeatureMath.MinGate(
                    pressure(metrics.RowPatternEntropy, 1.8, 3.5),
                    pressure(metrics.RowIntervalEntropy, 1.2, 2.7),
                    EzChartFeatureMath.Clamp01((0.75 - metrics.RhythmMotifRepeatRatio) / 0.45)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Jumpstream, EzChartFeatureMath.MinGate(
                    pressure(twoNoteRatio, 0.14, 0.36),
                    pressure(chordRatio, 0.24, 0.56),
                    pressure(metrics.JumpstreamPressure, 8, 22)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Handstream, EzChartFeatureMath.MinGate(
                    pressure(threeNoteRatio, 0.06, 0.22),
                    pressure(chordRatio, 0.32, 0.62),
                    pressure(metrics.SustainedNps10s, 13, 27),
                    EzChartFeatureMath.Clamp01((175 - metrics.JackPressure) / 120)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Quadstream, EzChartFeatureMath.MinGate(
                    pressure(fourPlusRatio, 0.015, 0.08),
                    pressure(chordRatio, 0.36, 0.72),
                    pressure(metrics.SustainedNps10s, 12, 25)), dataConfidence));
            }
            else if (metrics.KeyCount is >= 6 and <= 8)
            {
                double nonLnFlowGate = EzChartFeatureMath.Clamp01((0.3 - metrics.HoldRatio) / 0.22);
                double nonLnPatternGate = EzChartFeatureMath.Clamp01((0.68 - metrics.HoldRatio) / 0.56);
                double bracketOverlapGate = EzChartFeatureMath.Clamp01((0.62 - metrics.ChordColumnOverlapRatio) / 0.17);
                double bracketWindowRatio = ratio(stats.BracketWindowRows, rowCount);
                double wideChordstream = Math.Max(
                    chordstreamGate,
                    EzChartFeatureMath.MinGate(pressure(chordRatio, 0.2, 0.62), pressure(metrics.ChordSizeChangeRate, 0.18, 0.52)));
                var singleJack = getSingleJackStats(orderedRows, cancellationToken);
                double chordRepeatGate = Math.Max(
                    pressure(ratio(stats.MultiOverlapChordPairs, Math.Max(1, stats.ChordPairs)), 0.16, 0.26),
                    Math.Max(
                        pressure(singleJack.Jack1Share, 0.16, 0.26),
                        pressure(stats.ChordRows / (double)Math.Max(1, stats.ChordRuns), 4, 8)));
                double chordjackScore = nonLnPatternGate * chordRepeatGate * Math.Max(
                    chordjackBase,
                    EzChartFeatureMath.MinGate(pressure(chordRatio, 0.34, 0.72), pressure(repeatedChordRatio, 0.04, 0.22)));
                double delayChordjackVeto = EzChartFeatureMath.Clamp01((0.8 - chordjackScore) / 0.05);
                double delayScore = nonLnFlowGate * delayChordjackVeto * pressure(metrics.OffGridRowShare, 0.2, 0.5);
                double jackScore = nonLnPatternGate * Math.Max(
                    pressure(singleJack.Jack1Share, 0.08, 0.28),
                    pressure(singleJack.TrillRunShare, 0.03, 0.12));
                candidates.Add(hit(EzManiaRCAxis.Delay, delayScore, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Jack, jackScore, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Chordjack, chordjackScore, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Tech, nonLnPatternGate * Math.Max(
                    techScore,
                    wideChordstream * EzChartFeatureMath.MinGate(pressure(metrics.RowPatternChangeRate, 0.38, 0.72), pressure(metrics.FastRowRatio, 0.08, 0.36))), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Bracket, nonLnPatternGate * bracketOverlapGate * EzChartFeatureMath.MinGate(
                    pressure(chordRatio, 0.28, 0.62),
                    pressure(bracketWindowRatio, 0.18, 0.38)), dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Chordstream, nonLnPatternGate * wideChordstream * EzChartFeatureMath.Clamp01((165 - metrics.JackPressure) / 130), dataConfidence));
            }
            else
            {
                candidates.Add(hit(EzManiaRCAxis.Stream, lowChordGate * streamActivity, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Chordstream, chordstreamGate, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Chordjack, chordjackBase, dataConfidence));
                candidates.Add(hit(EzManiaRCAxis.Tech, techScore, dataConfidence));
            }

            var allPatterns = candidates
                              .Select(c => new EzPatternHit
                              {
                                  Id = c.Id,
                                  Label = c.Label,
                                  Score = roundedScore(c.Score),
                                  Confidence = roundedScore(c.Confidence),
                                  Evidence = c.Evidence
                              })
                              .OrderByDescending(p => p.Score)
                              .ThenBy(p => p.Label, StringComparer.Ordinal)
                              .ToList();

            var visiblePatterns = allPatterns.Where(p => p.Score >= 0.2).Take(5).ToList();
            string lnId = EzManiaRCAxis.Ln.ToId();
            var lnPattern = allPatterns.FirstOrDefault(p => p.Id == lnId);
            bool hasLnSignal = lnPattern != null && (lnPattern.Score > 0 || metrics.HoldRatio > 0);
            var lnAxisPatterns = lnPattern != null && hasLnSignal && visiblePatterns.All(p => p.Id != lnId)
                ? visiblePatterns.Append(lnPattern).ToList()
                : visiblePatterns;
            var subtypeOverflow = allPatterns.Where(p => ln_subtype_ids.Contains(p.Id)
                                                         && p.Score >= 0.2
                                                         && lnAxisPatterns.All(visible => visible.Id != p.Id)).ToList();
            var patterns = subtypeOverflow.Count > 0 ? lnAxisPatterns.Concat(subtypeOverflow).ToList() : lnAxisPatterns;

            return new EzPatternAnalysis
            {
                KeyCount = metrics.KeyCount,
                Primary = patterns.FirstOrDefault() ?? allPatterns.FirstOrDefault(),
                Patterns = patterns,
                AllPatterns = allPatterns,
                Metrics = metrics,
                Warnings = features.Warnings
            };
        }

        private static EzPatternHit hit(EzManiaRCAxis tag, double score, double dataConfidence)
        {
            return new EzPatternHit
            {
                Id = tag.ToId(),
                Label = tag.Meta().NameEn,
                Score = roundedScore(score),
                Confidence = roundedScore(score * dataConfidence)
            };
        }

        private static EzPatternHit hit(EzManiaLNAxis axis, double score, double dataConfidence)
        {
            return new EzPatternHit
            {
                Id = axis.ToId(),
                Label = axis.Meta().NameEn,
                Score = roundedScore(score),
                Confidence = roundedScore(score * dataConfidence)
            };
        }

        private static double ratio(int count, int total)
        {
            return total > 0 ? (double)count / total : 0;
        }

        private static double pressure(double value, double low, double high)
        {
            return EzChartFeatureMath.Clamp01((value - low) / Math.Max(0.001, high - low));
        }

        private static double roundedScore(double value)
        {
            return Math.Round(EzChartFeatureMath.Clamp01(value) * 1000) / 1000;
        }

        private static int bitCount(int mask)
        {
            int count = 0;

            while (mask != 0)
            {
                count++;
                mask &= mask - 1;
            }

            return count;
        }

        private static List<int> rowColumns(IReadOnlyList<EzManiaNote> rowNotes)
        {
            // Ascending distinct columns, built in place: the previous LINQ pipeline allocated three collections per
            // row for what is a sort plus a de-duplication.
            var columns = new List<int>(rowNotes.Count);

            for (int i = 0; i < rowNotes.Count; i++)
                columns.Add(rowNotes[i].Column);

            columns.Sort();

            int write = 0;

            for (int read = 0; read < columns.Count; read++)
            {
                if (write > 0 && columns[write - 1] == columns[read])
                    continue;

                columns[write++] = columns[read];
            }

            if (write != columns.Count)
                columns.RemoveRange(write, columns.Count - write);

            return columns;
        }

        private static bool isRollBetween(IReadOnlyList<int> previous, IReadOnlyList<int> current)
        {
            if (previous.Count == 0 || current.Count == 0)
                return false;

            return previous[0] > current[^1] || previous[^1] < current[0];
        }

        private static int sharedColumnCount(IReadOnlyList<int> previous, IReadOnlyList<int> current)
        {
            int shared = 0;

            foreach (int column in current)
            {
                if (previous.Contains(column))
                    shared++;
            }

            return shared;
        }

        private static RowPatternStats getRowPatternStats(IReadOnlyList<(double Time, IReadOnlyList<EzManiaNote> Notes)> orderedRows, int keyCount,
                                                          CancellationToken cancellationToken)
        {
            int chordRows = 0, twoNoteRows = 0, threeNoteRows = 0, fourPlusRows = 0, threePlusRows = 0, singleRows = 0;
            int repeatedChordRows = 0, bracketWindowRows = 0, totalChordSize = 0;
            int chordPairs = 0, multiOverlapChordPairs = 0, chordRuns = 0;
            int? previousChordMask = null;
            int previousRowMask = 0;
            int previousRowSize = 0;
            double previousRowTime = double.NegativeInfinity;
            var previousColumns = new List<int>();
            var beforePreviousColumns = new List<int>();

            int rowIndex = 0;

            foreach ((double time, var rowNotes) in orderedRows)
            {
                if ((rowIndex++ & 0xFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                var columns = rowColumns(rowNotes);
                int size = columns.Count;
                int mask = 0;
                foreach (int column in columns)
                    mask |= 1 << column;

                if (size <= 1)
                    singleRows++;

                if (size >= 2)
                {
                    chordRows++;
                    totalChordSize += size;
                    if (mask == previousChordMask)
                        repeatedChordRows++;
                    previousChordMask = mask;
                    if (previousRowSize < 2)
                        chordRuns++;

                    if (previousRowSize >= 2 && time - previousRowTime < 1000)
                    {
                        chordPairs++;
                        if (bitCount(mask & previousRowMask) >= 2)
                            multiOverlapChordPairs++;
                    }
                }
                else
                    previousChordMask = null;

                previousRowMask = mask;
                previousRowSize = size;
                previousRowTime = time;
                if (size == 2) twoNoteRows++;
                if (size == 3) threeNoteRows++;
                if (size >= 4) fourPlusRows++;
                if (size >= 3) threePlusRows++;

                if (keyCount >= 6 && size >= 2
                                  && beforePreviousColumns.Count >= 2 && previousColumns.Count >= 2
                                  && !isRollBetween(beforePreviousColumns, previousColumns)
                                  && !isRollBetween(previousColumns, columns)
                                  && sharedColumnCount(beforePreviousColumns, previousColumns) == 0
                                  && sharedColumnCount(previousColumns, columns) == 0)
                    bracketWindowRows++;

                beforePreviousColumns = previousColumns;
                previousColumns = columns;
            }

            return new RowPatternStats
            {
                RowCount = orderedRows.Count,
                ChordRows = chordRows,
                TwoNoteRows = twoNoteRows,
                ThreeNoteRows = threeNoteRows,
                FourPlusRows = fourPlusRows,
                ThreePlusRows = threePlusRows,
                SingleRows = singleRows,
                RepeatedChordRows = repeatedChordRows,
                BracketWindowRows = bracketWindowRows,
                AverageChordSize = chordRows > 0 ? (double)totalChordSize / chordRows : 0,
                ChordPairs = chordPairs,
                MultiOverlapChordPairs = multiOverlapChordPairs,
                ChordRuns = chordRuns
            };
        }

        private static SingleJackStats getSingleJackStats(IReadOnlyList<(double Time, IReadOnlyList<EzManiaNote> Notes)> orderedRows, CancellationToken cancellationToken)
        {
            var masks = new List<int>();
            var times = new List<double>();
            int noteCount = 0;
            int jack1 = 0;

            int rowIndex = 0;

            foreach ((double time, var rowNotes) in orderedRows)
            {
                if ((rowIndex++ & 0xFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                int mask = 0;
                foreach (var note in rowNotes)
                    mask |= 1 << note.Column;
                noteCount += rowNotes.Count;
                int last = masks.Count - 1;

                if (last >= 0 && time - times[last] <= 400)
                {
                    int overlap = mask & masks[last];

                    while (overlap != 0)
                    {
                        jack1++;
                        overlap &= overlap - 1;
                    }
                }

                masks.Add(mask);
                times.Add(time);
            }

            int trillNotes = 0;
            int i = 0;

            while (i < masks.Count - 5)
            {
                int a = masks[i];
                int b = masks[i + 1];

                if (a == 0 || b == 0 || a == b || times[i + 1] - times[i] > 200)
                {
                    i++;
                    continue;
                }

                int j = i + 2;
                while (j < masks.Count && masks[j] == ((j - i) % 2 == 0 ? a : b) && times[j] - times[j - 1] <= 200)
                    j++;

                if (j - i >= 6)
                {
                    for (int m = i; m < j; m++)
                        trillNotes += bitCount(masks[m]);
                    i = j;
                }
                else
                    i++;
            }

            return new SingleJackStats
            {
                Jack1Share = noteCount > 0 ? (double)jack1 / noteCount : 0,
                TrillRunShare = noteCount > 0 ? (double)trillNotes / noteCount : 0
            };
        }

        private static double inverseGapCapMs(double beatLengthMs)
        {
            if (!double.IsFinite(beatLengthMs) || beatLengthMs <= 0)
                return 120;

            return Math.Min(250, Math.Max(120, beatLengthMs * 0.27));
        }

        private static LnPatternStats getLnPatternStats(
            IReadOnlyList<EzManiaNote> notes,
            IReadOnlyList<(double Time, IReadOnlyList<EzManiaNote> Notes)> orderedRows,
            int keyCount,
            double beatLengthMs,
            CancellationToken cancellationToken)
        {
            var releaseRows = new Dictionary<double, List<EzManiaNote>>();
            var headTimes = new HashSet<double>();
            var holdEvents = new List<(double Time, int Delta)>();
            var holdSpans = new List<EzManiaNote>();
            var notesByColumn = Enumerable.Range(0, Math.Max(1, keyCount)).Select(_ => new List<EzManiaNote>()).ToArray();

            int scanIndex = 0;

            foreach (var note in notes)
            {
                if ((scanIndex++ & 0xFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                if (note.Column >= 0 && note.Column < notesByColumn.Length)
                    notesByColumn[note.Column].Add(note);
                if (!note.IsHold || note.EndTimeMs <= note.TimeMs)
                    continue;

                holdSpans.Add(note);

                if (!releaseRows.TryGetValue(note.EndTimeMs, out var releaseRow))
                {
                    releaseRow = new List<EzManiaNote>();
                    releaseRows[note.EndTimeMs] = releaseRow;
                }

                releaseRow.Add(note);
                holdEvents.Add((note.TimeMs, 1));
                holdEvents.Add((note.EndTimeMs, -1));
            }

            holdEvents.Sort((left, right) =>
            {
                int cmp = left.Time.CompareTo(right.Time);
                return cmp != 0 ? cmp : right.Delta.CompareTo(left.Delta);
            });

            int mixedRows = 0, tapWhileHoldingRows = 0, headTailSwitchRows = 0;
            int activeHolds = 0;
            int eventIndex = 0;
            int rowScan = 0;

            foreach ((double time, var rowNotes) in orderedRows)
            {
                if ((rowScan++ & 0xFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                headTimes.Add(time);

                while (eventIndex < holdEvents.Count && holdEvents[eventIndex].Time < time)
                {
                    activeHolds = Math.Max(0, activeHolds + holdEvents[eventIndex].Delta);
                    eventIndex++;
                }

                bool hasHold = false;
                bool hasTap = false;

                foreach (var rowNote in rowNotes)
                {
                    if (rowNote.IsHold)
                        hasHold = true;
                    else
                        hasTap = true;

                    if (hasHold && hasTap)
                        break;
                }

                if (hasHold && hasTap) mixedRows++;
                if (hasTap && activeHolds > 0) tapWhileHoldingRows++;
                if (releaseRows.ContainsKey(time)) headTailSwitchRows++;
            }

            int releaseOnlyRows = releaseRows.Keys.Count(time => !headTimes.Contains(time));
            var sameColumnGaps = new List<double>();
            double gapCap = inverseGapCapMs(beatLengthMs);
            int inverseLikeHolds = 0;
            int sameColumnNextHolds = 0;

            var holdsByStart = holdSpans.OrderBy(h => h.TimeMs).ToList();
            var holdStarts = holdsByStart.Select(h => h.TimeMs).ToList();
            var latestEndByStart = new List<double>();
            double latestEnd = double.NegativeInfinity;

            foreach (var hold in holdsByStart)
            {
                latestEnd = Math.Max(latestEnd, hold.EndTimeMs);
                latestEndByStart.Add(latestEnd);
            }

            bool heldThrough(double time)
            {
                int low = 0;
                int high = holdStarts.Count;

                while (low < high)
                {
                    int mid = (low + high) >> 1;
                    if (holdStarts[mid] < time) low = mid + 1;
                    else high = mid;
                }

                return low > 0 && latestEndByStart[low - 1] > time;
            }

            int activeReleaseHolds = 0, coordinatedReleaseHolds = 0, heldWhileReleaseHolds = 0;
            var holdDurations = new List<double>();
            var windowPairs = new Dictionary<int, (int Pairs, int Inverse)>();

            int columnScan = 0;

            foreach (var columnNotes in notesByColumn)
            {
                if ((columnScan++ & 0xF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                columnNotes.Sort((left, right) =>
                {
                    int cmp = left.TimeMs.CompareTo(right.TimeMs);
                    return cmp != 0 ? cmp : left.EndTimeMs.CompareTo(right.EndTimeMs);
                });

                for (int index = 0; index < columnNotes.Count; index++)
                {
                    var note = columnNotes[index];
                    if (!note.IsHold || note.EndTimeMs <= note.TimeMs)
                        continue;

                    double holdDuration = Math.Max(1, note.EndTimeMs - note.TimeMs);
                    holdDurations.Add(holdDuration);

                    EzManiaNote? nextNote = index + 1 < columnNotes.Count ? columnNotes[index + 1] : null;
                    double gap = nextNote != null ? nextNote.Value.TimeMs - note.EndTimeMs : double.PositiveInfinity;

                    if (nextNote != null && gap >= 0)
                    {
                        sameColumnNextHolds++;
                        sameColumnGaps.Add(gap);
                        int windowKey = (int)Math.Floor(note.TimeMs / INVERSE_WINDOW_MS);
                        windowPairs.TryGetValue(windowKey, out var window);
                        window.Pairs++;
                        if (gap <= gapCap && gap <= holdDuration)
                            window.Inverse++;
                        windowPairs[windowKey] = window;
                    }

                    if (gap >= 0 && gap <= gapCap && gap / holdDuration <= 0.7)
                    {
                        if (nextNote != null)
                            inverseLikeHolds++;
                        continue;
                    }

                    activeReleaseHolds++;
                    if (headTimes.Contains(note.EndTimeMs))
                        coordinatedReleaseHolds++;
                    if (heldThrough(note.EndTimeMs))
                        heldWhileReleaseHolds++;
                }
            }

            int rowCount = Math.Max(1, orderedRows.Count);
            int releaseRowCount = releaseRows.Count;
            int holdCount = Math.Max(1, holdDurations.Count);
            int judgedWindows = 0;
            int inverseWindows = 0;

            foreach (var window in windowPairs.Values)
            {
                if (window.Pairs < INVERSE_WINDOW_MIN_PAIRS)
                    continue;

                judgedWindows++;
                if (window.Inverse / (double)window.Pairs >= INVERSE_WINDOW_RATIO)
                    inverseWindows++;
            }

            return new LnPatternStats
            {
                InverseReleaseRatio = sameColumnNextHolds > 0 ? (double)inverseLikeHolds / sameColumnNextHolds : 0,
                SameColumnReleaseGapP50 = EzChartFeatureMath.Quantile(sameColumnGaps, 0.5),
                ReleaseOnlyRatio = releaseRowCount > 0 ? (double)releaseOnlyRows / releaseRowCount : 0,
                HeadTailSwitchRatio = (double)headTailSwitchRows / rowCount,
                MixedRowRatio = (double)mixedRows / rowCount,
                TapWhileHoldingRatio = (double)tapWhileHoldingRows / rowCount,
                ActiveReleaseRatio = (double)activeReleaseHolds / holdCount,
                CoordinatedReleaseRatio = (double)coordinatedReleaseHolds / holdCount,
                HeldWhileReleaseRatio = (double)heldWhileReleaseHolds / holdCount,
                HoldDurationP50 = EzChartFeatureMath.Quantile(holdDurations, 0.5),
                InverseWindowCoverage = judgedWindows >= INVERSE_WINDOW_MIN_WINDOWS ? (double)inverseWindows / judgedWindows : 0
            };
        }

        private sealed class RowPatternStats
        {
            public int RowCount;
            public int ChordRows;
            public int TwoNoteRows;
            public int ThreeNoteRows;
            public int FourPlusRows;
            public int ThreePlusRows;
            public int SingleRows;
            public int RepeatedChordRows;
            public int BracketWindowRows;
            public double AverageChordSize;
            public int ChordPairs;
            public int MultiOverlapChordPairs;
            public int ChordRuns;
        }

        private sealed class SingleJackStats
        {
            public double Jack1Share;
            public double TrillRunShare;
        }

        private sealed class LnPatternStats
        {
            public double InverseReleaseRatio;
            public double SameColumnReleaseGapP50;
            public double ReleaseOnlyRatio;
            public double HeadTailSwitchRatio;
            public double MixedRowRatio;
            public double TapWhileHoldingRatio;
            public double ActiveReleaseRatio;
            public double CoordinatedReleaseRatio;
            public double HeldWhileReleaseRatio;
            public double HoldDurationP50;
            public double InverseWindowCoverage;
        }
    }
}
