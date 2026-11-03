// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Hub <c>patterns/chart.js</c> note cell types.</summary>
    internal static class EzLeoBlackNoteType
    {
        public const int NOTHING = 0;
        public const int NORMAL = 1;
        public const int HOLDHEAD = 2;
        public const int HOLDBODY = 3;
        public const int HOLDTAIL = 4;
    }

    internal readonly struct EzLeoBlackBpm
    {
        public EzLeoBlackBpm(int meter, double msPerBeat)
        {
            Meter = meter;
            MsPerBeat = msPerBeat;
        }

        public int Meter { get; }
        public double MsPerBeat { get; }
    }

    internal sealed class EzLeoBlackTimeItem<T>
    {
        public EzLeoBlackTimeItem(double time, T data)
        {
            Time = time;
            Data = data;
        }

        public double Time { get; }
        public T Data { get; }
    }

    /// <summary>In-memory LeoBlack chart (hub <c>createChart</c>).</summary>
    internal sealed class EzLeoBlackChart
    {
        public EzLeoBlackChart(
            int keys,
            IReadOnlyList<EzLeoBlackTimeItem<int[]>> notes,
            IReadOnlyList<EzLeoBlackTimeItem<EzLeoBlackBpm>> bpm,
            IReadOnlyList<EzLeoBlackTimeItem<double>> sv)
        {
            Keys = keys;
            Notes = notes;
            BPM = bpm;
            SV = sv;
        }

        public int Keys { get; }
        public IReadOnlyList<EzLeoBlackTimeItem<int[]>> Notes { get; }
        public IReadOnlyList<EzLeoBlackTimeItem<EzLeoBlackBpm>> BPM { get; }
        public IReadOnlyList<EzLeoBlackTimeItem<double>> SV { get; }

        public double FirstNote => Notes[0].Time;
        public double LastNote => Notes[^1].Time;
    }

    /// <summary>
    /// Builds a LeoBlack chart from <see cref="EzManiaChartInput"/> (notes + uninherited timing),
    /// matching hub interlude/pattern row semantics without re-parsing .osu text.
    /// </summary>
    internal static class EzLeoBlackChartBuilder
    {
        public static EzLeoBlackChart? TryBuild(EzManiaChartInput input)
        {
            if (input.KeyCount < 1 || input.Notes.Count == 0)
                return null;

            int keys = input.KeyCount;
            var rowMap = new Dictionary<double, int[]>();
            var holdSpans = new List<(int Column, double Start, double End)>();

            foreach (var note in input.Notes)
            {
                if (note.Column < 0 || note.Column >= keys || !double.IsFinite(note.TimeMs))
                    continue;

                if (note.IsHold && double.IsFinite(note.EndTimeMs) && note.EndTimeMs > note.TimeMs)
                {
                    setNoteType(rowMap, keys, note.TimeMs, note.Column, EzLeoBlackNoteType.HOLDHEAD);
                    setNoteType(rowMap, keys, note.EndTimeMs, note.Column, EzLeoBlackNoteType.HOLDTAIL);
                    holdSpans.Add((note.Column, note.TimeMs, note.EndTimeMs));
                }
                else
                {
                    setNoteType(rowMap, keys, note.TimeMs, note.Column, EzLeoBlackNoteType.NORMAL);
                }
            }

            if (rowMap.Count == 0)
                return null;

            var sortedTimes = rowMap.Keys.OrderBy(t => t).ToList();

            foreach (var (column, start, end) in holdSpans)
            {
                int t = lowerBoundGreater(sortedTimes, start);

                for (; t < sortedTimes.Count && sortedTimes[t] < end; t++)
                {
                    int[] row = rowMap[sortedTimes[t]];

                    if (row[column] == EzLeoBlackNoteType.NOTHING)
                        row[column] = EzLeoBlackNoteType.HOLDBODY;
                }
            }

            var notes = new List<EzLeoBlackTimeItem<int[]>>(sortedTimes.Count);

            foreach (double time in sortedTimes)
            {
                int[] data = rowMap[time];

                if (isRowEmpty(data))
                    continue;

                notes.Add(new EzLeoBlackTimeItem<int[]>(time, data));
            }

            if (notes.Count == 0)
                return null;

            var bpm = new List<EzLeoBlackTimeItem<EzLeoBlackBpm>>();

            if (input.TimingPoints.Count > 0)
            {
                foreach (var tp in input.TimingPoints)
                {
                    if (!double.IsFinite(tp.BeatLengthMs) || tp.BeatLengthMs <= 0)
                        continue;

                    bpm.Add(new EzLeoBlackTimeItem<EzLeoBlackBpm>(tp.TimeMs, new EzLeoBlackBpm(4, tp.BeatLengthMs)));
                }
            }

            if (bpm.Count == 0)
            {
                double mspb = double.IsFinite(input.Bpm) && input.Bpm > 0 ? 60000.0 / input.Bpm : 500.0;
                bpm.Add(new EzLeoBlackTimeItem<EzLeoBlackBpm>(0, new EzLeoBlackBpm(4, mspb)));
            }

            // Inherited SV points are not on EzManiaChartInput; empty SV => svTime 0 (categorise ignores it).
            return new EzLeoBlackChart(keys, notes, bpm, Array.Empty<EzLeoBlackTimeItem<double>>());
        }

        private static void setNoteType(Dictionary<double, int[]> rowMap, int keys, double time, int column, int noteType)
        {
            if (!rowMap.TryGetValue(time, out int[]? row))
            {
                row = new int[keys];
                rowMap[time] = row;
            }

            if (row[column] == EzLeoBlackNoteType.NOTHING)
                row[column] = noteType;
        }

        private static bool isRowEmpty(int[] row)
        {
            for (int i = 0; i < row.Length; i++)
            {
                int n = row[i];

                if (n != EzLeoBlackNoteType.NOTHING && n != EzLeoBlackNoteType.HOLDBODY)
                    return false;
            }

            return true;
        }

        private static int lowerBoundGreater(IReadOnlyList<double> sortedAsc, double target)
        {
            int lo = 0;
            int hi = sortedAsc.Count;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;

                if (sortedAsc[mid] <= target)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
