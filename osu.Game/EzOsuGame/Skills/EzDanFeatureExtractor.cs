// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Port of mania-hub <c>dan-estimator/features.ts</c> <c>extractDanFeatures</c>.</summary>
    public static class EzDanFeatureExtractor
    {
        private static readonly int[] snap_divisors = { 1, 2, 3, 4, 6, 8, 12, 16, 24, 32 };

        public static EzDanFeatureExtractionResult Extract(EzManiaChartInput map, double rate = 1)
        {
            var notes = getRatedNotes(map, rate);
            var warnings = new List<string>();
            var noteTimes = new List<double>(notes.Count);
            var releaseTimes = new List<double>();
            var holdDurations = new List<double>();
            var holdEvents = new List<(double Time, int Delta)>();
            int holdNoteCount = 0;
            double totalHoldMs = 0;
            bool noteTimesSorted = true;
            bool releaseTimesSorted = true;
            double previousNoteTime = double.NegativeInfinity;
            double previousReleaseTime = double.NegativeInfinity;

            foreach (var note in notes)
            {
                if (note.TimeMs < previousNoteTime)
                    noteTimesSorted = false;
                previousNoteTime = note.TimeMs;
                noteTimes.Add(note.TimeMs);

                if (!note.IsHold)
                    continue;

                holdNoteCount++;
                if (note.EndTimeMs <= note.TimeMs)
                    continue;

                double holdDuration = note.EndTimeMs - note.TimeMs;
                holdDurations.Add(holdDuration);
                totalHoldMs += holdDuration;
                if (note.EndTimeMs < previousReleaseTime)
                    releaseTimesSorted = false;
                previousReleaseTime = note.EndTimeMs;
                releaseTimes.Add(note.EndTimeMs);
                holdEvents.Add((note.TimeMs, 1));
                holdEvents.Add((note.EndTimeMs, -1));
            }

            if (!noteTimesSorted)
                noteTimes.Sort();
            if (!releaseTimesSorted)
                releaseTimes.Sort();

            double durationMs = Math.Max(map.TotalLengthMs / rate, noteTimes.Count > 0 ? noteTimes[^1] : 0);

            if (notes.Count < 50 || durationMs <= 0)
                warnings.Add("This map has very little note data, so the estimate is low confidence.");

            var orderedRows = groupNotesByTime(notes);
            double holdRatio = notes.Count > 0 ? (double)holdNoteCount / notes.Count : 0;

            int keyCount = Math.Max(1, map.KeyCount);
            double[] lastByColumn = Enumerable.Repeat(double.NegativeInfinity, keyCount).ToArray();
            var jackValues = new List<double>();
            var streamValues = new List<double>();
            var jumpstreamValues = new List<double>();
            var rowDensities = new List<double>();
            var rowDensityWeights = new List<double>();
            var rowIntervals = new List<double>();
            var rowRates = new List<double>();
            var columnIntervals = new List<double>();
            var columnRates = new List<double>();
            var tailIntervals = new List<double>();
            var tailRates = new List<double>();
            int[] columnCounts = new int[keyCount];
            int chordRows = 0;
            int twoNoteChordRows = 0;
            int holdRows = 0;
            int lnChordRows = 0;
            int longGapCount = 0;
            double longGapMs = 0;
            int fastRowCount = 0;
            int directionChanges = 0;
            int? previousColumn = null;
            int previousDirection = 0;
            int previousChordSize = 0;
            int chordSizeChanges = 0;
            double? previousRowTime = null;
            int? previousRowMask = null;
            int? rowMaskTwoBack = null;
            int repeatedRowPatterns = 0;
            int alternatingRowPatterns = 0;
            int chordPairCount = 0;
            int chordPairOverlapCount = 0;
            int adjacentColumnRehitNotes = 0;
            int twoBackColumnRehitNotes = 0;
            double rowPatternChangeSum = 0;
            var rowMasks = new List<int>();
            var rowTimes = new List<double>();
            var rowSignatures = new List<int>();
            int rowMaskBase = Math.Max(32, (1 << Math.Max(1, map.KeyCount)) + 1);
            int rowSignatureBase = rowMaskBase * 256;

            foreach ((double time, var rowNotes) in orderedRows)
            {
                var columns = new List<int>();
                int rowMask = 0;
                bool rowHasHold = false;

                foreach (var note in rowNotes)
                {
                    columns.Add(note.Column);
                    rowMask |= 1 << note.Column;
                    if (note.IsHold)
                        rowHasHold = true;
                }

                columns.Sort();

                if (columns.Count >= 2)
                    chordRows++;
                if (columns.Count == 2)
                    twoNoteChordRows++;

                if (rowHasHold)
                {
                    holdRows++;
                    if (columns.Count >= 2)
                        lnChordRows++;
                }

                if (previousRowMask != null && previousChordSize >= 2 && columns.Count >= 2
                    && previousRowTime != null && time - previousRowTime < 1000)
                {
                    chordPairCount++;
                    if ((rowMask & previousRowMask.Value) != 0)
                        chordPairOverlapCount++;
                }

                rowMasks.Add(rowMask);
                rowTimes.Add(time);

                if (previousRowMask != null)
                {
                    if (previousRowTime != null && time - previousRowTime <= 500)
                        adjacentColumnRehitNotes += bitCount(rowMask & previousRowMask.Value);
                    if (rowMask == previousRowMask.Value)
                        repeatedRowPatterns++;
                    rowPatternChangeSum += bitCount(rowMask ^ previousRowMask.Value) / (double)Math.Max(1, map.KeyCount);
                }

                if (rowMaskTwoBack != null && rowTimes.Count >= 3 && time - rowTimes[^3] <= 500)
                    twoBackColumnRehitNotes += bitCount(rowMask & rowMaskTwoBack.Value);
                if (rowMaskTwoBack != null && rowMask == rowMaskTwoBack.Value)
                    alternatingRowPatterns++;

                rowMaskTwoBack = previousRowMask;
                previousRowMask = rowMask;

                foreach (int column in columns)
                    columnCounts[column]++;

                if (previousChordSize != 0 && previousChordSize != columns.Count)
                    chordSizeChanges++;

                int intervalBucket = 0;

                if (previousRowTime != null)
                {
                    double rowDelta = time - previousRowTime.Value;

                    if (rowDelta >= 2500)
                    {
                        longGapCount++;
                        longGapMs += rowDelta;
                    }

                    if (rowDelta > 0 && rowDelta < 1200)
                    {
                        rowIntervals.Add(rowDelta);
                        rowRates.Add(1000 / rowDelta);
                        rowDensities.Add(columns.Count * 1000 / rowDelta);
                        rowDensityWeights.Add(Math.Max(1, rowDelta));
                        if (columns.Count == 2)
                            jumpstreamValues.Add(Math.Min(60, columns.Count * 1000 / rowDelta));
                        if (rowDelta <= 80)
                            fastRowCount++;
                        intervalBucket = (int)Math.Round(rowDelta / 5);
                    }
                    else
                        intervalBucket = -1;
                }

                rowSignatures.Add(rowMask * 256 + intervalBucket + 1);
                previousChordSize = columns.Count;
                previousRowTime = time;

                foreach (int column in columns)
                {
                    double sameDelta = time - lastByColumn[column];
                    if (sameDelta > 0 && sameDelta < 1000)
                        jackValues.Add(Math.Min(230, 15000 / sameDelta));

                    if (sameDelta > 0 && sameDelta < 1600)
                    {
                        columnIntervals.Add(sameDelta);
                        columnRates.Add(1000 / sameDelta);
                    }

                    int leftNeighbor = column - 1;

                    if (leftNeighbor >= 0)
                    {
                        double delta = time - lastByColumn[leftNeighbor];
                        if (delta > 0 && delta < 260)
                            streamValues.Add((260 - delta) / 35);
                    }

                    int rightNeighbor = column + 1;

                    if (rightNeighbor < map.KeyCount)
                    {
                        double delta = time - lastByColumn[rightNeighbor];
                        if (delta > 0 && delta < 260)
                            streamValues.Add((260 - delta) / 35);
                    }

                    if (previousColumn != null)
                    {
                        int direction = Math.Sign(column - previousColumn.Value);
                        if (direction != 0 && previousDirection != 0 && direction != previousDirection)
                            directionChanges++;
                        if (direction != 0)
                            previousDirection = direction;
                    }

                    previousColumn = column;
                    lastByColumn[column] = time;
                }
            }

            for (int i = 1; i < releaseTimes.Count; i++)
            {
                double tailDelta = releaseTimes[i] - releaseTimes[i - 1];

                if (tailDelta > 0 && tailDelta < 1600)
                {
                    tailIntervals.Add(tailDelta);
                    tailRates.Add(1000 / tailDelta);
                }
            }

            double chordRatio = orderedRows.Count > 0 ? (double)chordRows / orderedRows.Count : 0;
            double twoNoteChordRatio = orderedRows.Count > 0 ? (double)twoNoteChordRows / orderedRows.Count : 0;
            double peakNps1s = EzDanFeatureMath.CountInWindow(noteTimes, 1000);
            double peakNps5s = EzDanFeatureMath.CountInWindow(noteTimes, 5000) / 5.0;
            var nps5sSamples = sampledWindowNps(noteTimes, 5000, durationMs);
            double[] npsQuantiles = EzDanFeatureMath.Quantiles(nps5sSamples, 0.5, 0.9, 0.95);
            double sustainedNps10s = EzDanFeatureMath.CountInWindow(noteTimes, 10000) / 10.0;
            double sustainedNps30s = EzDanFeatureMath.CountInWindow(noteTimes, 30000) / 30.0;
            double sustainedNps60s = EzDanFeatureMath.CountInWindow(noteTimes, 60000) / 60.0;
            double noteSpanMs = noteTimes.Count >= 2 ? Math.Max(1, noteTimes[^1] - noteTimes[0]) : durationMs;
            double activeNps = notes.Count / Math.Max(1, noteSpanMs / 1000);
            double longGapRatio = durationMs > 0 ? longGapMs / durationMs : 0;
            double jackPressure = EzDanFeatureMath.Quantile(jackValues, 0.92);
            double streamPressure = EzDanFeatureMath.Quantile(streamValues, 0.9);
            double jumpstreamPressure = EzDanFeatureMath.Quantile(jumpstreamValues, 0.9);
            double burstDensity = EzDanFeatureMath.Quantile(rowDensities, 0.9);
            double rowBurstPressure = EzDanFeatureMath.Quantile(rowRates, 0.9);
            double fastRowRatio = rowIntervals.Count > 0 ? (double)fastRowCount / rowIntervals.Count : 0;
            double rowIntervalEntropy = EzDanFeatureMath.BucketEntropy(rowIntervals, 5);
            double offGridShare = offGridRowShare(map);
            double rowPatternEntropy = EzDanFeatureMath.BucketEntropy(rowMasks.Select(m => (double)m).ToList(), 1);
            double rowPatternVariety = rowMasks.Count > 0
                ? rowMasks.Distinct().Count() / (double)Math.Min(rowMasks.Count, 1 << Math.Max(1, map.KeyCount))
                : 0;
            double rowMotifRepeatRatio = numericNgramRepeatRatio(rowMasks, 4, rowMaskBase);
            double rhythmMotifRepeatRatio = numericNgramRepeatRatio(rowSignatures, 4, rowSignatureBase);
            double adjacentMotifRepeatRatio = adjacentNgramRepeatRatio(rowSignatures, 4);
            double repeatedRowPatternRatio = orderedRows.Count > 1 ? (double)repeatedRowPatterns / (orderedRows.Count - 1) : 0;
            double alternatingRowPatternRatio = orderedRows.Count > 2 ? (double)alternatingRowPatterns / (orderedRows.Count - 2) : 0;
            double rowPatternChangeRate = orderedRows.Count > 1 ? rowPatternChangeSum / (orderedRows.Count - 1) : 0;
            double patternVariety = 0.5 * EzDanFeatureMath.RaoQuadraticEntropyLog(EzDanFeatureMath.BucketValues(rowIntervals, 5), 1)
                                    + 1.125 * EzDanFeatureMath.RaoQuadraticEntropyLog(EzDanFeatureMath.BucketValues(columnIntervals, 5), 2)
                                    + 0.11 * EzDanFeatureMath.RaoQuadraticEntropyLog(EzDanFeatureMath.BucketValues(tailIntervals, 5), 1);
            double spikiness = EzDanFeatureMath.StrainSpikiness(rowDensities, rowDensityWeights);
            double sustainedPressureRatio = sustainedNps10s / Math.Max(1, Math.Max(peakNps1s, peakNps5s));
            double averageColumnCount = EzDanFeatureMath.Average(columnCounts);
            double columnImbalance = averageColumnCount > 0
                ? columnCounts.Sum(count => Math.Abs(count - averageColumnCount)) / (columnCounts.Length * averageColumnCount)
                : 0;
            double anchorPressure = columnImbalance * (0.5 + Math.Min(1.5, jackPressure / 150))
                                    + Math.Max(0, EzDanFeatureMath.Quantile(columnRates, 0.9) - 7) * 0.04;
            double lnReleasePressure = releaseTimes.Count > 0
                ? EzDanFeatureMath.CountInWindow(releaseTimes, 5000) / 5.0 + EzDanFeatureMath.Quantile(tailRates, 0.9) * 0.15
                : 0;
            double lnDensity = durationMs > 0 ? totalHoldMs / (durationMs * Math.Max(1, map.KeyCount)) : 0;
            double lnChordPressure = holdRows > 0 ? (double)lnChordRows / holdRows : 0;
            double lnHoldDurationAvg = EzDanFeatureMath.Average(holdDurations);
            double lnHoldDurationP90 = EzDanFeatureMath.Quantile(holdDurations, 0.9);

            holdEvents.Sort((left, right) =>
            {
                int cmp = left.Time.CompareTo(right.Time);
                return cmp != 0 ? cmp : right.Delta.CompareTo(left.Delta);
            });

            int activeHolds = 0;
            int activeHoldPeak = 0;
            double activeHoldArea = 0;
            double previousHoldEventTime = holdEvents.Count > 0 ? holdEvents[0].Time : 0;

            foreach (var evt in holdEvents)
            {
                activeHoldArea += Math.Max(0, evt.Time - previousHoldEventTime) * activeHolds;
                activeHolds = Math.Max(0, activeHolds + evt.Delta);
                activeHoldPeak = Math.Max(activeHoldPeak, activeHolds);
                previousHoldEventTime = evt.Time;
            }

            double averageActiveHolds = durationMs > 0 ? activeHoldArea / durationMs : 0;
            double lnOverlapPressure = averageActiveHolds + activeHoldPeak * 0.35;
            double chordSizeChangeRate = orderedRows.Count > 0 ? (double)chordSizeChanges / orderedRows.Count : 0;
            double directionChangeRate = notes.Count > 0 ? (double)directionChanges / notes.Count : 0;
            double chordjackPressure = jackPressure * (0.28 + chordRatio * 1.35) + burstDensity * chordRatio * 0.6;
            double chordColumnOverlapRatio = chordPairCount > 0 ? (double)chordPairOverlapCount / chordPairCount : 0;
            double adjacentColumnRehitShare = notes.Count > 0 ? (double)adjacentColumnRehitNotes / notes.Count : 0;
            double twoBackColumnRehitShare = notes.Count > 0 ? (double)twoBackColumnRehitNotes / notes.Count : 0;
            double twoBackColumnRehitExcess = twoBackColumnRehitShare - adjacentColumnRehitShare;
            double techPressure = orderedRows.Count > 0
                ? directionChanges / (double)orderedRows.Count * 4.4
                  + chordSizeChanges / (double)orderedRows.Count * 3.5
                  + chordRatio * 1.6
                  + EzDanFeatureMath.Average(rowDensities) * 0.018
                : 0;

            return new EzDanFeatureExtractionResult
            {
                Notes = notes,
                NoteTimes = noteTimes,
                DurationMs = durationMs,
                OrderedRows = orderedRows,
                Warnings = warnings,
                Metrics = new EzDanFeatureMetrics
                {
                    KeyCount = map.KeyCount,
                    NoteCount = notes.Count,
                    DurationMs = durationMs,
                    HoldRatio = holdRatio,
                    ChordRatio = chordRatio,
                    TwoNoteChordRatio = twoNoteChordRatio,
                    PeakNps1s = peakNps1s,
                    PeakNps5s = peakNps5s,
                    Nps5sP50 = npsQuantiles[0],
                    Nps5sP90 = npsQuantiles[1],
                    Nps5sP95 = npsQuantiles[2],
                    SustainedNps10s = sustainedNps10s,
                    SustainedNps30s = sustainedNps30s,
                    SustainedNps60s = sustainedNps60s,
                    ActiveNps = activeNps,
                    LongGapRatio = longGapRatio,
                    LongGapCount = longGapCount,
                    JackPressure = jackPressure,
                    StreamPressure = streamPressure,
                    JumpstreamPressure = jumpstreamPressure,
                    ChordjackPressure = chordjackPressure,
                    ChordColumnOverlapRatio = chordColumnOverlapRatio,
                    AdjacentColumnRehitShare = adjacentColumnRehitShare,
                    TwoBackColumnRehitShare = twoBackColumnRehitShare,
                    TwoBackColumnRehitExcess = twoBackColumnRehitExcess,
                    TechPressure = techPressure,
                    RowBurstPressure = rowBurstPressure,
                    FastRowRatio = fastRowRatio,
                    RowIntervalEntropy = rowIntervalEntropy,
                    OffGridRowShare = offGridShare,
                    PatternVariety = patternVariety,
                    RowPatternEntropy = rowPatternEntropy,
                    RowPatternVariety = rowPatternVariety,
                    RepeatedRowPatternRatio = repeatedRowPatternRatio,
                    AlternatingRowPatternRatio = alternatingRowPatternRatio,
                    RowPatternChangeRate = rowPatternChangeRate,
                    RowMotifRepeatRatio = rowMotifRepeatRatio,
                    RhythmMotifRepeatRatio = rhythmMotifRepeatRatio,
                    AdjacentMotifRepeatRatio = adjacentMotifRepeatRatio,
                    StrainSpikiness = spikiness,
                    SustainedPressureRatio = sustainedPressureRatio,
                    AnchorPressure = anchorPressure,
                    LnReleasePressure = lnReleasePressure,
                    LnDensity = lnDensity,
                    LnOverlapPressure = lnOverlapPressure,
                    LnChordPressure = lnChordPressure,
                    LnHoldDurationAvg = lnHoldDurationAvg,
                    LnHoldDurationP90 = lnHoldDurationP90,
                    ChordSizeChangeRate = chordSizeChangeRate,
                    DirectionChangeRate = directionChangeRate,
                    StaminaPressure = sustainedNps10s
                }
            };
        }

        private static List<EzManiaNote> getRatedNotes(EzManiaChartInput map, double rate)
        {
            var notes = new List<EzManiaNote>();

            foreach (var note in map.Notes)
            {
                if (note.Column < 0 || note.Column >= map.KeyCount)
                    continue;

                if (rate == 1)
                {
                    notes.Add(note);
                    continue;
                }

                notes.Add(new EzManiaNote(note.TimeMs / rate, note.Column, note.IsHold, note.EndTimeMs / rate));
            }

            return notes;
        }

        private static List<(double Time, IReadOnlyList<EzManiaNote> Notes)> groupNotesByTime(IReadOnlyList<EzManiaNote> notes)
        {
            var rows = new Dictionary<double, List<EzManiaNote>>();

            foreach (var note in notes)
            {
                if (!rows.TryGetValue(note.TimeMs, out var row))
                {
                    row = new List<EzManiaNote>();
                    rows[note.TimeMs] = row;
                }

                row.Add(note);
            }

            return rows.OrderBy(kvp => kvp.Key)
                       .Select(kvp => (kvp.Key, (IReadOnlyList<EzManiaNote>)kvp.Value))
                       .ToList();
        }

        private static int bitCount(int value)
        {
            int count = 0;
            int remaining = value;

            while (remaining > 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }

            return count;
        }

        private static double numericNgramRepeatRatio(IReadOnlyList<int> values, int size, int baseValue)
        {
            if (values.Count < size + 1)
                return 0;

            var seen = new HashSet<long>();
            int repeated = 0;
            int total = 0;

            for (int index = 0; index <= values.Count - size; index++)
            {
                long key = 0;
                for (int offset = 0; offset < size; offset++)
                    key = key * baseValue + values[index + offset];

                if (!seen.Add(key))
                    repeated++;
                total++;
            }

            return total > 0 ? (double)repeated / total : 0;
        }

        private static double adjacentNgramRepeatRatio(IReadOnlyList<int> values, int size)
        {
            if (values.Count < size * 2)
                return 0;

            int repeated = 0;
            int total = 0;

            for (int index = size; index <= values.Count - size; index++)
            {
                total++;
                bool matches = true;

                for (int offset = 0; offset < size; offset++)
                {
                    if (values[index + offset] != values[index - size + offset])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    repeated++;
            }

            return total > 0 ? (double)repeated / total : 0;
        }

        private static List<double> sampledWindowNps(IReadOnlyList<double> noteTimes, double windowMs, double durationMs)
        {
            if (noteTimes.Count == 0 || durationMs <= 0)
                return new List<double>();

            var samples = new List<double>();
            int left = 0;
            int right = 0;

            for (double start = 0; start <= durationMs; start += 1000)
            {
                while (left < noteTimes.Count && noteTimes[left] < start)
                    left++;
                while (right < noteTimes.Count && noteTimes[right] < start + windowMs)
                    right++;
                samples.Add((right - left) / (windowMs / 1000));
            }

            return samples;
        }

        private static int gapSnapDivisor(double gapMs, double beatLengthMs)
        {
            double tolerance = 2 + gapMs * 0.01;

            foreach (int divisor in snap_divisors)
            {
                double unit = beatLengthMs / divisor;
                int steps = (int)Math.Round(gapMs / unit);
                if (steps >= 1 && Math.Abs(steps * unit - gapMs) <= tolerance)
                    return divisor;
            }

            return 0;
        }

        private static double offGridRowShare(EzManiaChartInput map)
        {
            var timingPoints = map.TimingPoints
                                  .Where(point => double.IsFinite(point.TimeMs) && point.BeatLengthMs > 0)
                                  .OrderBy(point => point.TimeMs)
                                  .ToList();
            double fallbackBeatLength = map.Bpm > 0 ? 60000 / map.Bpm : double.PositiveInfinity;
            var rows = groupNotesByTime(map.Notes.Where(note => note.Column >= 0 && note.Column < map.KeyCount).ToList());
            if (rows.Count < 2)
                return 0;

            int offGrid = 0;
            int timingIndex = 0;

            for (int i = 1; i < rows.Count; i++)
            {
                double time = rows[i].Time;
                while (timingIndex + 1 < timingPoints.Count && timingPoints[timingIndex + 1].TimeMs <= time + 1)
                    timingIndex++;

                double beatLength = timingPoints.Count > 0 && timingPoints[timingIndex].TimeMs <= time + 1
                    ? timingPoints[timingIndex].BeatLengthMs
                    : timingPoints.Count > 0
                        ? timingPoints[0].BeatLengthMs
                        : fallbackBeatLength;
                double gap = time - rows[i - 1].Time;

                if (gap <= 55)
                {
                    offGrid++;
                    continue;
                }

                if (gap > beatLength)
                    continue;

                int divisor = gapSnapDivisor(gap, beatLength);
                if (divisor >= 6 || divisor == 0 && gap <= beatLength / 2)
                    offGrid++;
            }

            return (double)offGrid / rows.Count;
        }
    }
}
