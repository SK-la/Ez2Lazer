// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.KeyPattern
{
    internal readonly record struct EzManiaKeyPatternColumnHitObject(double StartTime, int Column);

    internal readonly record struct EzManiaKeyPatternRow(double Time, double BeatLength, int[] Columns)
    {
        public int NoteCount => Columns.Length;

        public int MinColumn => Columns.Length == 0 ? 0 : Columns[0];

        public int MaxColumn => Columns.Length == 0 ? 0 : Columns[^1];

        public int ColumnSpan => Columns.Length <= 1 ? 0 : MaxColumn - MinColumn;

        public int AdjacentLinkCount
        {
            get
            {
                int count = 0;

                for (int i = 1; i < Columns.Length; i++)
                {
                    if (Columns[i] - Columns[i - 1] == 1)
                        count++;
                }

                return count;
            }
        }

        public int SeparatedLinkCount
        {
            get
            {
                int count = 0;

                for (int i = 1; i < Columns.Length; i++)
                {
                    if (Columns[i] - Columns[i - 1] > 1)
                        count++;
                }

                return count;
            }
        }

        public int TotalGapCount
        {
            get
            {
                int count = 0;

                for (int i = 1; i < Columns.Length; i++)
                    count += Math.Max(0, Columns[i] - Columns[i - 1] - 1);

                return count;
            }
        }

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

    internal readonly record struct EzManiaAdjacentRowPatternMetrics(
        double Delta,
        double DumpDensity,
        double DelayDensity,
        double CentreStep,
        double NormalizedCentreStep,
        double StructureSimilarity,
        int Direction);

    internal static class EzManiaKeyPatternHelper
    {
        public const double TIME_TOLERANCE = 3.0;

        public static List<EzManiaKeyPatternColumnHitObject> GetColumnObjects(ManiaBeatmap beatmap)
        {
            var columnObjects = new List<EzManiaKeyPatternColumnHitObject>(beatmap.HitObjects.Count);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                ManiaHitObject hitObject = beatmap.HitObjects[i];
                columnObjects.Add(new EzManiaKeyPatternColumnHitObject(hitObject.StartTime, hitObject.Column));
            }

            columnObjects.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
            return columnObjects;
        }

        public static List<EzManiaKeyPatternRow> BuildRows(ManiaBeatmap beatmap, IReadOnlyList<EzManiaKeyPatternColumnHitObject> columnObjects, CancellationToken cancellationToken)
        {
            var rows = new List<EzManiaKeyPatternRow>(columnObjects.Count);

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

                rows.Add(new EzManiaKeyPatternRow(currentTime, beatmap.ControlPointInfo.TimingPointAt(currentTime).BeatLength, columns));
                currentColumns.Clear();
            }

            for (int i = 1; i < columnObjects.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                EzManiaKeyPatternColumnHitObject hitObject = columnObjects[i];

                if (Math.Abs(hitObject.StartTime - currentTime) > TIME_TOLERANCE)
                {
                    flushCurrentRow();
                    currentTime = hitObject.StartTime;
                }

                currentColumns.Add(hitObject.Column);
            }

            flushCurrentRow();
            return rows;
        }

        public static bool IsOnDivision(double time, double beatLength, int divisor)
        {
            if (beatLength <= 0 || divisor <= 0)
                return false;

            double interval = beatLength / divisor;

            if (interval <= 0)
                return false;

            double mod = time % interval;
            return mod <= TIME_TOLERANCE || Math.Abs(interval - mod) <= TIME_TOLERANCE;
        }

        public static bool TryGetAdjacentPatternMetrics(EzManiaKeyPatternRow previous, EzManiaKeyPatternRow current, int totalColumns,
                                                        out EzManiaAdjacentRowPatternMetrics metrics)
        {
            metrics = default;

            double beatLength = previous.BeatLength > 0 ? previous.BeatLength : current.BeatLength;
            double delta = current.Time - previous.Time;

            if (beatLength <= 0 || delta <= TIME_TOLERANCE || delta > beatLength / 2.0 + TIME_TOLERANCE)
                return false;

            double centreStep = Math.Abs(current.AverageColumn - previous.AverageColumn);
            int direction = Math.Sign(current.AverageColumn - previous.AverageColumn);
            double normalizedCentreStep = totalColumns <= 1 ? 0 : Math.Clamp(centreStep / (totalColumns - 1.0), 0, 1);
            double structureSimilarity = GetRowStructureSimilarity(previous, current);

            metrics = new EzManiaAdjacentRowPatternMetrics(
                delta,
                GetDumpDensityScore(delta, beatLength),
                GetDelayDensityScore(delta, beatLength),
                centreStep,
                normalizedCentreStep,
                structureSimilarity,
                direction);

            return true;
        }

        public static double GetRowStructureSimilarity(EzManiaKeyPatternRow left, EzManiaKeyPatternRow right)
        {
            double noteSimilarity = 1 - Math.Abs(left.NoteCount - right.NoteCount) / (double)Math.Max(1, Math.Max(left.NoteCount, right.NoteCount));
            double spanSimilarity = 1 - Math.Abs(left.ColumnSpan - right.ColumnSpan) / (double)Math.Max(1, Math.Max(left.ColumnSpan, right.ColumnSpan));
            double gapSimilarity = 1 - Math.Abs(left.TotalGapCount - right.TotalGapCount) / (double)Math.Max(1, Math.Max(left.TotalGapCount, right.TotalGapCount));

            return Math.Clamp(noteSimilarity * 0.45 + spanSimilarity * 0.3 + gapSimilarity * 0.25, 0, 1);
        }

        public static double GetDumpDensityScore(double delta, double beatLength)
        {
            if (beatLength <= 0 || delta <= TIME_TOLERANCE)
                return 0;

            if (delta <= beatLength / 16.0 + TIME_TOLERANCE)
                return 1.0;

            if (delta <= beatLength / 12.0 + TIME_TOLERANCE)
                return 0.95;

            if (delta <= beatLength / 8.0 + TIME_TOLERANCE)
                return 0.85;

            if (delta <= beatLength / 6.0 + TIME_TOLERANCE)
                return 0.75;

            if (delta <= beatLength / 4.0 + TIME_TOLERANCE)
                return 0.6;

            if (delta <= beatLength / 3.0 + TIME_TOLERANCE)
                return 0.45;

            if (delta <= beatLength / 2.0 + TIME_TOLERANCE)
                return 0.3;

            return 0;
        }

        public static double GetDelayDensityScore(double delta, double beatLength)
        {
            if (beatLength <= 0 || delta <= TIME_TOLERANCE)
                return 0;

            if (delta <= beatLength / 16.0 + TIME_TOLERANCE)
                return 1.0;

            if (delta <= beatLength / 12.0 + TIME_TOLERANCE)
                return 0.9;

            if (delta <= beatLength / 8.0 + TIME_TOLERANCE)
                return 0.75;

            if (delta <= beatLength / 6.0 + TIME_TOLERANCE)
                return 0.6;

            return 0;
        }

        public static double GetFineTimingSeverity(double time, double beatLength)
        {
            if (beatLength <= 0)
                return 0;

            if (IsOnDivision(time, beatLength, 1)
                || IsOnDivision(time, beatLength, 2)
                || IsOnDivision(time, beatLength, 3)
                || IsOnDivision(time, beatLength, 4))
                return 0;

            if (IsOnDivision(time, beatLength, 6))
                return 0.55;

            if (IsOnDivision(time, beatLength, 8))
                return 0.68;

            if (IsOnDivision(time, beatLength, 12))
                return 0.82;

            if (IsOnDivision(time, beatLength, 16))
                return 0.9;

            if (IsOnDivision(time, beatLength, 24))
                return 0.96;

            if (IsOnDivision(time, beatLength, 32) || IsOnDivision(time, beatLength, 48))
                return 1.0;

            return 1.0;
        }

        public static double GetRelativeDifference(double left, double right)
        {
            double denominator = Math.Max(Math.Max(Math.Abs(left), Math.Abs(right)), 0.0001);
            return Math.Clamp(Math.Abs(left - right) / denominator, 0, 1);
        }

        public static double ScaleToTen(double ratio)
            => ToKeyPatternScore(ratio * 10.0);

        public static double ToKeyPatternScore(double value)
            => Math.Round(Math.Clamp(value, 0, 10), 2, MidpointRounding.AwayFromZero);
    }
}
