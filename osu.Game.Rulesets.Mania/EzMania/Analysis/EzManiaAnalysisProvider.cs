// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis
{
    public class EzManiaAnalysisProvider : IEzAnalysisProvider
    {
        private const double time_tolerance = 3.0;

        private readonly record struct ColumnHitObject(double StartTime, int Column);

        private readonly record struct KeyPatternRow(double Time, double BeatLength, int[] Columns)
        {
            public int NoteCount => Columns.Length;

            public double AverageColumn
            {
                get
                {
                    if (Columns.Length == 0)
                        return 0;

                    double sum = 0;

                    for (int i = 0; i < Columns.Length; i++)
                        sum += Columns[i];

                    return sum / Columns.Length;
                }
            }
        }

        public bool TryCompute(in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis)
        {
            analysis = null!;

            if (request.Beatmap is not ManiaBeatmap beatmap || request.RequestedScopes == EzAnalysisScope.None)
                return false;

            var bag = new EzAnalysisBag();
            bool hasXxySr = false;
            double xxySr = 0;

            if (request.RequestedScopes.HasFlag(EzAnalysisScope.XxySr) && tryCalculateXxySr(beatmap, request.ClockRate, out xxySr))
            {
                bag.Set(EzAnalysisFields.XXY_SR, xxySr);
                hasXxySr = true;
            }

            if (request.RequestedScopes.HasFlag(EzAnalysisScope.RulesetSpecificRadarData))
            {
                if (!hasXxySr && tryCalculateXxySr(beatmap, request.ClockRate, out double computedXxySr))
                {
                    xxySr = computedXxySr;
                    bag.Set(EzAnalysisFields.XXY_SR, xxySr);
                    hasXxySr = true;
                }

                bag.Set(EzAnalysisFields.RULESET_SPECIFIC_RADAR_DATA, computeRadarData(beatmap, hasXxySr ? xxySr : null, cancellationToken));
            }

            analysis = bag;
            return true;
        }

        private static bool tryCalculateXxySr(IBeatmap beatmap, double clockRate, out double sr)
        {
            sr = 0;

            int keyCount = beatmap is ManiaBeatmap maniaBeatmap && maniaBeatmap.TotalColumns > 0
                ? maniaBeatmap.TotalColumns
                : Math.Max(1, (int)Math.Round(beatmap.BeatmapInfo.Difficulty.CircleSize));

            if (keyCount >= 11 && keyCount % 2 == 1)
                return false;

            sr = SRCalculator.CalculateSR(beatmap, clockRate);
            return !double.IsNaN(sr) && !double.IsInfinity(sr);
        }

        private static EzRadarChartData<string> computeRadarData(ManiaBeatmap beatmap, double? xxySr, CancellationToken cancellationToken)
        {
            var columnObjects = getColumnObjects(beatmap);

            if (columnObjects.Count == 0)
                return createRadarData(0, 0, 0, 0, 0, xxySr ?? 0);

            var rows = buildRows(beatmap, columnObjects, cancellationToken);
            int totalColumns = columnObjects.Max(obj => obj.Column) + 1;

            return createRadarData(
                computeBracketScore(rows, totalColumns, cancellationToken),
                computeChordScore(rows, cancellationToken),
                computeDelayScore(rows, cancellationToken),
                computeDumpScore(rows, cancellationToken),
                computeJackScore(beatmap, columnObjects, cancellationToken),
                xxySr ?? 0);
        }

        private static EzRadarChartData<string> createRadarData(double bracket, double chord, double delay, double dump, double jack, double xxySr)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", toKeyPatternScore(bracket), "0.00"),
                new EzRadarAxisValue<string>("Chord", toKeyPatternScore(chord), "0.00"),
                new EzRadarAxisValue<string>("Delay", toKeyPatternScore(delay), "0.00"),
                new EzRadarAxisValue<string>("Dump", toKeyPatternScore(dump), "0.00"),
                new EzRadarAxisValue<string>("Jack", toKeyPatternScore(jack), "0.00"),
                new EzRadarAxisValue<string>("XXYSR", xxySr, "0.00"));

        private static List<ColumnHitObject> getColumnObjects(ManiaBeatmap beatmap)
        {
            var columnObjects = new List<ColumnHitObject>(beatmap.HitObjects.Count);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                ManiaHitObject hitObject = beatmap.HitObjects[i];
                columnObjects.Add(new ColumnHitObject(hitObject.StartTime, hitObject.Column));
            }

            columnObjects.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
            return columnObjects;
        }

        private static List<KeyPatternRow> buildRows(ManiaBeatmap beatmap, IReadOnlyList<ColumnHitObject> columnObjects, CancellationToken cancellationToken)
        {
            var rows = new List<KeyPatternRow>(columnObjects.Count);

            if (columnObjects.Count == 0)
                return rows;

            double currentTime = columnObjects[0].StartTime;
            var currentColumns = new List<int> { columnObjects[0].Column };

            void flushCurrentRow()
            {
                if (currentColumns.Count == 0)
                    return;

                int[] columns = currentColumns.Distinct()
                                              .OrderBy(column => column)
                                              .ToArray();

                rows.Add(new KeyPatternRow(currentTime, beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength, columns));
                currentColumns.Clear();
            }

            for (int i = 1; i < columnObjects.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ColumnHitObject hitObject = columnObjects[i];

                if (Math.Abs(hitObject.StartTime - currentTime) > time_tolerance)
                {
                    flushCurrentRow();
                    currentTime = hitObject.StartTime;
                }

                currentColumns.Add(hitObject.Column);
            }

            flushCurrentRow();
            return rows;
        }

        private static double computeBracketScore(IReadOnlyList<KeyPatternRow> rows, int totalColumns, CancellationToken cancellationToken)
        {
            if (rows.Count < 3 || totalColumns <= 0)
                return 0;

            double interlockScore = 0;

            for (int i = 1; i < rows.Count - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                KeyPatternRow previous = rows[i - 1];
                KeyPatternRow current = rows[i];
                KeyPatternRow next = rows[i + 1];

                double beatLength = current.BeatLength > 0 ? current.BeatLength : Math.Max(previous.BeatLength, next.BeatLength);

                if (beatLength <= 0)
                    continue;

                if (current.Time - previous.Time > beatLength + time_tolerance || next.Time - current.Time > beatLength + time_tolerance)
                    continue;

                HashSet<int> surrounding = previous.Columns.Concat(next.Columns).ToHashSet();

                if (surrounding.Count == 0)
                    continue;

                int surroundingMin = surrounding.Min();
                int surroundingMax = surrounding.Max();

                if (surroundingMax - surroundingMin < Math.Max(1, totalColumns / 3))
                    continue;

                int gapFillCount = 0;
                int outsideCount = 0;
                bool overlaps = false;

                for (int columnIndex = 0; columnIndex < current.Columns.Length; columnIndex++)
                {
                    int column = current.Columns[columnIndex];

                    if (surrounding.Contains(column))
                    {
                        overlaps = true;
                        break;
                    }

                    if (column > surroundingMin && column < surroundingMax)
                        gapFillCount++;
                    else
                        outsideCount++;
                }

                if (overlaps)
                    continue;

                bool hasCrossShape = outsideCount > 0 && Math.Abs(previous.AverageColumn - next.AverageColumn) >= 1.5;

                if (gapFillCount == 0 && !hasCrossShape)
                    continue;

                interlockScore += Math.Min(1.0, (gapFillCount + outsideCount * 0.5) / Math.Max(1.0, current.NoteCount));
            }

            return scaleToTen(Math.Min(1.0, interlockScore / Math.Max(1, rows.Count - 2) * 2.0));
        }

        private static double computeChordScore(IReadOnlyList<KeyPatternRow> rows, CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
                return 0;

            double chordIntensity = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                chordIntensity += Math.Clamp((rows[i].NoteCount - 1) / 2.0, 0, 1);
            }

            return scaleToTen(chordIntensity / rows.Count);
        }

        private static double computeDelayScore(IReadOnlyList<KeyPatternRow> rows, CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
                return 0;

            double fineRows = 0;
            double staggerPairs = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                KeyPatternRow row = rows[i];

                if (row.BeatLength > 0
                    && !isOnDivision(row.Time, row.BeatLength, 1)
                    && !isOnDivision(row.Time, row.BeatLength, 2)
                    && !isOnDivision(row.Time, row.BeatLength, 3)
                    && !isOnDivision(row.Time, row.BeatLength, 4))
                    fineRows++;

                if (i == 0)
                    continue;

                KeyPatternRow previous = rows[i - 1];
                double beatLength = previous.BeatLength > 0 ? previous.BeatLength : row.BeatLength;
                double delta = row.Time - previous.Time;

                if (beatLength > 0 && delta > time_tolerance && delta <= beatLength / 8.0 + time_tolerance)
                    staggerPairs++;
            }

            double fineRatio = fineRows / rows.Count;
            double staggerRatio = staggerPairs / Math.Max(1, rows.Count - 1);

            return scaleToTen(fineRatio * 0.65 + staggerRatio * 0.35);
        }

        private static double computeDumpScore(IReadOnlyList<KeyPatternRow> rows, CancellationToken cancellationToken)
        {
            int singleRowCount = 0;
            int runContribution = 0;
            int currentRunLength = 0;
            int currentDirection = 0;

            void flushRun()
            {
                if (currentRunLength >= 3)
                    runContribution += currentRunLength;

                currentRunLength = 0;
                currentDirection = 0;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rows[i].NoteCount == 1)
                    singleRowCount++;

                if (i == 0)
                    continue;

                KeyPatternRow previous = rows[i - 1];
                KeyPatternRow current = rows[i];

                if (previous.NoteCount != 1 || current.NoteCount != 1)
                {
                    flushRun();
                    continue;
                }

                double beatLength = previous.BeatLength > 0 ? previous.BeatLength : current.BeatLength;
                double delta = current.Time - previous.Time;
                int columnDelta = current.Columns[0] - previous.Columns[0];

                if (beatLength <= 0 || delta > beatLength / 2.0 + time_tolerance || columnDelta == 0)
                {
                    flushRun();
                    continue;
                }

                int direction = Math.Sign(columnDelta);

                if (currentRunLength == 0)
                {
                    currentRunLength = 2;
                    currentDirection = direction;
                    continue;
                }

                if (direction == currentDirection)
                    currentRunLength++;
                else
                {
                    flushRun();
                    currentRunLength = 2;
                    currentDirection = direction;
                }
            }

            flushRun();
            return scaleToTen(runContribution / (double)Math.Max(1, singleRowCount));
        }

        private static double computeJackScore(ManiaBeatmap beatmap, IReadOnlyList<ColumnHitObject> columnObjects, CancellationToken cancellationToken)
        {
            var groupedByColumn = columnObjects.GroupBy(obj => obj.Column)
                                               .Select(group => group.OrderBy(obj => obj.StartTime).ToList())
                                               .ToList();

            int totalPairs = 0;
            double weightedPairs = 0;

            for (int groupIndex = 0; groupIndex < groupedByColumn.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ColumnHitObject> columnNotes = groupedByColumn[groupIndex];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    ColumnHitObject previous = columnNotes[i - 1];
                    ColumnHitObject current = columnNotes[i];
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(previous.StartTime).BeatLength;

                    if (beatLength <= 0)
                        continue;

                    totalPairs++;

                    double delta = current.StartTime - previous.StartTime;

                    if (delta <= beatLength / 4.0 + time_tolerance)
                        weightedPairs += 1.0;
                    else if (delta <= beatLength / 2.0 + time_tolerance)
                        weightedPairs += 0.6;
                }
            }

            if (totalPairs == 0)
                return 0;

            return scaleToTen(weightedPairs / totalPairs);
        }

        private static bool isOnDivision(double time, double beatLength, int divisor)
        {
            if (beatLength <= 0 || divisor <= 0)
                return false;

            double interval = beatLength / divisor;

            if (interval <= 0)
                return false;

            double mod = time % interval;
            return mod <= time_tolerance || Math.Abs(interval - mod) <= time_tolerance;
        }

        private static double scaleToTen(double ratio)
            => toKeyPatternScore(ratio * 10.0);

        private static double toKeyPatternScore(double value)
            => Math.Round(Math.Clamp(value, 0, 10), 2, MidpointRounding.AwayFromZero);
    }
}
