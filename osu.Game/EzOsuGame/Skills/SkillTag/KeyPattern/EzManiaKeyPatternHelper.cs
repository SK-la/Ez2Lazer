// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern
{
    public readonly record struct EzManiaKeyPatternRow(double Time, double BeatLength, int[] Columns)
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

    public readonly record struct EzManiaAdjacentRowPatternMetrics(
        double Delta,
        double DumpDensity,
        double DelayDensity,
        double CentreStep,
        double NormalizedCentreStep,
        double StructureSimilarity,
        int Direction);

    public static class EzManiaKeyPatternHelper
    {
        public const double TIME_TOLERANCE = 3.0;

        /// <summary>
        /// The chart's notes in ascending time order, which is what row grouping requires. An extraction arrives in
        /// that order already, so a copy is only made for a hand-built input that is not.
        /// </summary>
        public static IReadOnlyList<EzManiaNote> NotesInTimeOrder(EzManiaChartInput chart)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var notes = chart.Notes;

            for (int i = 1; i < notes.Count; i++)
            {
                if (notes[i].TimeMs < notes[i - 1].TimeMs)
                {
                    var ordered = new List<EzManiaNote>(notes);
                    ordered.Sort(static (left, right) => left.TimeMs.CompareTo(right.TimeMs));
                    return ordered;
                }
            }

            return notes;
        }

        /// <summary>
        /// Rows grouped from notes in time order, with the beat length supplied by the caller, so the grouping rule
        /// exists once.
        /// </summary>
        public static List<EzManiaKeyPatternRow> BuildRows(IReadOnlyList<EzManiaNote> orderedNotes, Func<double, double> beatLengthAt,
                                                           CancellationToken cancellationToken)
        {
            var rows = new List<EzManiaKeyPatternRow>(orderedNotes.Count);

            if (orderedNotes.Count == 0)
                return rows;

            double currentTime = orderedNotes[0].TimeMs;
            var currentColumns = new List<int> { orderedNotes[0].Column };
            var seenColumns = new HashSet<int> { orderedNotes[0].Column };

            void flushCurrentRow()
            {
                if (currentColumns.Count == 0)
                    return;

                // Columns are collected in hit-object order; sorting and de-duplicating in place gives the same
                // ascending distinct array as a LINQ pipeline without the intermediate allocations.
                currentColumns.Sort();
                int write = 0;

                for (int read = 0; read < currentColumns.Count; read++)
                {
                    if (write > 0 && currentColumns[write - 1] == currentColumns[read])
                        continue;

                    currentColumns[write++] = currentColumns[read];
                }

                int[] columns = new int[write];
                currentColumns.CopyTo(0, columns, 0, write);

                rows.Add(new EzManiaKeyPatternRow(currentTime, beatLengthAt(currentTime), columns));
                currentColumns.Clear();
                seenColumns.Clear();
            }

            for (int i = 1; i < orderedNotes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                EzManiaNote note = orderedNotes[i];

                if (Math.Abs(note.TimeMs - currentTime) > TIME_TOLERANCE)
                {
                    flushCurrentRow();
                    currentTime = note.TimeMs;
                }

                if (seenColumns.Add(note.Column))
                    currentColumns.Add(note.Column);
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

        /// <summary>
        /// The uninherited beat length in effect at <paramref name="time"/>, matching
        /// <c>ControlPointInfo.TimingPointAt(time).BeatLength</c>: the last point at or before the time, otherwise the
        /// first. Falls back to the chart BPM when the extraction has no usable timing point.
        /// </summary>
        public static double BeatLengthAt(EzManiaChartInput chart, double time)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var points = chart.TimingPoints;

            if (points.Count == 0)
                return chart.Bpm > 0 ? 60000 / chart.Bpm : 0;

            int low = 0;
            int high = points.Count;

            while (low < high)
            {
                int mid = (low + high) >> 1;

                if (points[mid].TimeMs <= time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low > 0 ? points[low - 1].BeatLengthMs : points[0].BeatLengthMs;
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
